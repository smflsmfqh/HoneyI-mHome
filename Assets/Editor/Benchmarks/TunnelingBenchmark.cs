using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 벽·연석 관통 재현율 측정 (Troubleshooting 01 검증용).
///
/// 플레이어와 같은 크기·질량·충돌 설정의 탐침을 연석과 도로 외 MeshCollider에 반복해서 부딪히고,
/// 결과를 구성(Fixed Timestep · Collision Detection · 도로 콜라이더 종류)별로 센다.
///
/// 결과 분류
///   Blocked     : 벽 앞에서 막혔다.
///   Penetrated  : 캡슐이 반지름 이상 벽에 겹쳤다가 다시 밀려 나왔다(일시적 파고듦).
///   Through     : 측정이 끝났을 때 벽 안에 끼어 있거나 반대편으로 넘어가 있다(플레이어가 겪는 관통·갇힘).
///   Skipped     : 그 구성의 지형에서 개미가 그 벽 높이에 설 수 없어 시험하지 않음.
///
/// 측정 방식
/// - 열려 있는 씬은 건드리지 않는다. 대상과 주변 정적 콜라이더를 미리보기 씬(별도 PhysicsScene)에 복제해 시뮬레이션한다.
/// - 탐침은 중력을 켜고 실제 바닥 위에 내려놓은 뒤, PlayerMovement.FixedUpdate처럼 매 스텝 수평 속도를 대입해 벽으로 민다.
/// - D·E 구성은 도로 타일의 BoxCollider(바닥·연석)를 도로 메시 MeshCollider 하나로 바꾼 상태(수정 전 구조)다.
/// - 판정은 그 구성에서 실제로 부딪히는 콜라이더 기준이다.
/// </summary>
public static class TunnelingBenchmark
{
    // ---- 측정 구성 ----------------------------------------------------------
    private readonly struct Config
    {
        public readonly string Name;
        public readonly float FixedDt;
        public readonly CollisionDetectionMode Mode;
        public readonly bool RoadAsMesh;

        public Config(string name, float fixedDt, CollisionDetectionMode mode, bool roadAsMesh)
        {
            Name = name; FixedDt = fixedDt; Mode = mode; RoadAsMesh = roadAsMesh;
        }
    }

    private static readonly Config[] Configs =
    {
        new Config("A_current",       0.01f, CollisionDetectionMode.ContinuousDynamic, false),
        new Config("B_timestep_0.02", 0.02f, CollisionDetectionMode.ContinuousDynamic, false),
        new Config("C_discrete",      0.01f, CollisionDetectionMode.Discrete,          false),
        new Config("D_road_mesh",     0.01f, CollisionDetectionMode.ContinuousDynamic, true),
        new Config("E_all_reverted",  0.02f, CollisionDetectionMode.Discrete,          true),
    };

    // PlayerMovement 직렬화 값 기준: 이동 0.8, 구르기 2.0, 달리기 0.8+1.7, 부스트 3.5(VDSpeedBoostSO), 부스트+달리기 3.5+1.7
    private static readonly (string Label, float Speed)[] Speeds =
    {
        ("Move", 0.8f), ("Roll", 2.0f), ("Sprint", 2.5f), ("Boost", 3.5f), ("BoostSprint", 5.2f),
    };

    // 벽 법선 기준 입사각. 큰 각도는 벽을 따라 미끄러져 모서리를 돌아 나가는 경우가 섞여서 제외했다.
    private static readonly float[] AnglesDeg = { 0f, 20f };

    private const int SamplesPerTarget = 4;
    private const int MaxTargetsPerGroup = 60;
    private const float MinCurbLength = 1.0f;     // 모서리 조각(짧은 연석)은 미끄러짐과 구분이 어려워 제외
    private const float NeighborMargin = 0.6f;    // 대상 주변에서 함께 복제할 범위(바닥 포함)
    private const int SettleSteps = 15;           // 바닥에 내려앉히는 스텝 수
    private const int Seed = 12345;

    private const string GroupCurb = "RoadCurb";
    private const string GroupMesh = "MeshCollider";

    private enum Outcome { Blocked, Penetrated, Through, Skipped }

    // ---- 진입점 -------------------------------------------------------------
    [MenuItem("Tools/Physics/Run Tunneling Benchmark")]
    public static void RunFromMenu() => UnityEngine.Debug.Log(Run());

    /// <summary>메뉴나 외부(coplay execute_script)에서 호출. 요약 문자열을 돌려준다.</summary>
    public static string Run() => Run(null);

    /// <summary>
    /// 지정한 재현 지점에서만 측정한다. 각 지점은 (이름, 개미가 서 있을 위치, 벽을 향한 방향).
    /// 씬을 바꾸지 않고 코드에서 지점을 넘길 때 쓴다(Benchmarks/RunTunnelingSpots.cs).
    /// </summary>
    public static string RunSpots(IEnumerable<(string Name, Vector3 Position, Vector3 Direction)> spots) => Run(spots.ToList());

    private static string Run(List<(string Name, Vector3 Position, Vector3 Direction)> spotList)
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null) return "[Tunneling] PlayerMovement를 찾지 못했습니다. AntDemoScene을 열어 주세요.";

        var spec = ProbeSpec.From(player);
        var rng = new System.Random(Seed);
        // 씬에 TunnelSpot_* 오브젝트가 있으면 그 지점만 측정(재현 지점 모드), 없으면 맵 전체 무작위 표본
        if (spotList == null)
            spotList = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
                .Where(t => t.name.StartsWith("TunnelSpot", StringComparison.Ordinal)).OrderBy(t => t.name)
                .Select(t => (t.name, t.position, t.forward)).ToList();
        var targets = CollectSpotTargets(player, spec, spotList, out string collectLog);
        if (targets.Count == 0) targets = CollectTargets(player, spec, rng, out collectLog);
        if (targets.Count == 0) return "[Tunneling] 측정할 대상 콜라이더가 없습니다.\n" + collectLog;

        var rows = new List<ResultRow>();
        var hotspots = new Dictionary<string, int>();
        var notes = new StringBuilder();
        var cases = new List<string>();

        bool prevBackfaces = Physics.queriesHitBackfaces;
        try
        {
            for (int ci = 0; ci < Configs.Length; ci++)
            {
                var cfg = Configs[ci];
                using var world = new PreviewWorld(spec, cfg);
                notes.AppendLine(world.SanityCheck());

                for (int ti = 0; ti < targets.Count; ti++)
                {
                    var t = targets[ti];
                    if (EditorUtility.DisplayCancelableProgressBar("Tunneling Benchmark",
                            $"{cfg.Name}  {t.Group}  {ti + 1}/{targets.Count}",
                            (ci + (float)ti / targets.Count) / Configs.Length))
                        return "[Tunneling] 사용자가 취소했습니다.";

                    world.Activate(t);
                    foreach (var (label, speed) in Speeds)
                    foreach (float angle in AnglesDeg)
                    foreach (var s in t.Samples)
                    {
                        var outcome = world.RunTrial(s, speed, angle * s.AngleSign, out string trace);
                        var row = FindOrAdd(rows, cfg.Name, t.Group, label, speed, angle);
                        switch (outcome)
                        {
                            case Outcome.Skipped: row.Skipped++; continue;
                            case Outcome.Penetrated: row.Penetrated++; break;
                            case Outcome.Through:
                                row.Through++;
                                string key = $"{cfg.Name}|{t.Group}|{t.Path}";
                                hotspots[key] = hotspots.TryGetValue(key, out int n) ? n + 1 : 1;
                                if (cases.Count < 60) cases.Add($"{cfg.Name} {t.Group} {t.Path} v={speed} a={angle * s.AngleSign:0} {trace}");
                                break;
                        }
                        row.Trials++;
                    }
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = prevBackfaces;
            EditorUtility.ClearProgressBar();
        }

        string csvPath = WriteResults(rows, hotspots, collectLog, notes.ToString(), cases);
        return BuildSummary(rows, collectLog, notes.ToString(), csvPath);
    }

    // ---- 탐침 사양 (플레이어 복제) ----------------------------------------------
    private sealed class ProbeSpec
    {
        public Vector3 Scale;
        public float Radius, Height;
        public Vector3 Center;
        public int Direction;
        public float Mass;
        public float WorldRadius;
        public float HalfHeight;   // 캡슐 중심에서 바닥까지(월드)
        public int Layer;          // 플레이어 레이어(충돌 매트릭스 그대로 적용)
        public int CollisionMask;  // 플레이어 레이어와 충돌하는 레이어

        public static ProbeSpec From(PlayerMovement player)
        {
            var cap = player.GetComponent<CapsuleCollider>();
            var rb = player.GetComponent<Rigidbody>();
            var scale = player.transform.lossyScale;
            return new ProbeSpec
            {
                Scale = scale,
                Radius = cap.radius,
                Height = cap.height,
                Center = cap.center,
                Direction = cap.direction,
                Mass = rb.mass,
                WorldRadius = cap.radius * Mathf.Max(scale.x, scale.z),
                HalfHeight = Mathf.Max(cap.height * 0.5f, cap.radius) * scale.y,
                Layer = player.gameObject.layer,
                CollisionMask = MaskFor(player.gameObject.layer),
            };
        }

        public float CenterYOffset => Center.y * Scale.y;

        private static int MaskFor(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
                if (!Physics.GetIgnoreLayerCollision(layer, i)) mask |= 1 << i;
            return mask;
        }
    }

    // ---- 대상 수집 ----------------------------------------------------------
    private sealed class Sample
    {
        public Vector3 SurfacePoint;  // 벽 표면 점(수평 위치만 사용)
        public Vector3 Normal;        // 바깥쪽 수평 법선
        public float AngleSign;       // 입사각 좌우
    }

    private sealed class Target
    {
        public string Group;
        public string Path;
        public Collider Source;
        public Transform RoadRoot;                                       // 도로 타일 루트(연석일 때)
        public readonly List<Collider> Neighbors = new List<Collider>(); // 함께 복제할 주변 정적 콜라이더(대상 포함)
        public readonly List<Sample> Samples = new List<Sample>();
    }

    private static int s_mask = ~0;
    // 플레이어와 실제로 부딪히는 정적 콜라이더만(충돌 매트릭스에서 무시되는 레이어 제외)
    private static bool IsStaticSolid(Collider c, Transform player)
        => c.enabled && !c.isTrigger && c.attachedRigidbody == null && !c.transform.IsChildOf(player)
           && (s_mask & (1 << c.gameObject.layer)) != 0;

    private static List<Target> CollectTargets(PlayerMovement player, ProbeSpec spec, System.Random rng, out string log)
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        s_mask = spec.CollisionMask;
        var curbs = new List<Target>();
        var meshes = new List<Target>();
        int shortCurbs = 0;

        var all = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        foreach (var c in all)
        {
            if (!IsStaticSolid(c, player.transform)) continue;

            Transform roadRoot = FindRoadRoot(c.transform);
            if (roadRoot != null && c is BoxCollider box)
            {
                bool onObstacle = obstacleLayer >= 0 && c.gameObject.layer == obstacleLayer;
                if (!onObstacle && !LooksLikeCurb(box, spec)) continue;
                if (LongSide(box) < MinCurbLength) { shortCurbs++; continue; }
                curbs.Add(new Target { Group = GroupCurb, Path = PathOf(c.transform), Source = c, RoadRoot = roadRoot });
            }
            else if (roadRoot == null && c is MeshCollider mc && mc.sharedMesh != null && c.bounds.size.y > 0.05f)
            {
                meshes.Add(new Target { Group = GroupMesh, Path = PathOf(c.transform), Source = c });
            }
        }

        int curbTotal = curbs.Count, meshTotal = meshes.Count;
        curbs = Pick(curbs, MaxTargetsPerGroup, rng);
        meshes = Pick(meshes, MaxTargetsPerGroup, rng);

        foreach (var t in curbs) SampleBox(t, (BoxCollider)t.Source, rng);
        foreach (var t in meshes) SampleMesh(t, rng);

        var result = curbs.Concat(meshes).Where(t => t.Samples.Count > 0).ToList();
        foreach (var t in result) AddNeighbors(t, player, spec);

        log = $"대상: 연석 BoxCollider(길이 {MinCurbLength}m 이상) {curbTotal}개 중 {curbs.Count}개 · 짧은 모서리 조각 {shortCurbs}개 제외, " +
              $"도로 외 MeshCollider {meshTotal}개 중 {meshes.Count}개 무작위 선택 (seed {Seed})\n" +
              $"샘플이 잡힌 대상 {result.Count}개, 대상당 샘플 최대 {SamplesPerTarget}개, 입사각 0/±20°, 주변 {NeighborMargin}m 정적 콜라이더 함께 복제\n" +
              $"탐침: 월드 반지름 {spec.WorldRadius:0.0000}, 질량 {spec.Mass}, 마찰 0, 중력 켬(바닥에 내려놓은 뒤 출발)";
        return result;
    }

    /// <summary>
    /// 재현 지점 모드. 씬에 이름이 "TunnelSpot"으로 시작하는 빈 오브젝트를 벽 앞에 두고 파란 화살표(forward)를 벽 쪽으로 돌려 두면,
    /// 그 방향으로 처음 맞는 콜라이더를 벽으로 삼아 좌우로 조금씩 옮긴 위치에서 반복 측정한다.
    /// </summary>
    private static List<Target> CollectSpotTargets(PlayerMovement player, ProbeSpec spec,
        List<(string Name, Vector3 Position, Vector3 Direction)> spots, out string log)
    {
        s_mask = spec.CollisionMask;
        var result = new List<Target>();
        var sb = new StringBuilder();
        foreach (var spot in spots)
        {
            Vector3 dir = Flat(spot.Direction);
            if (dir == Vector3.zero) { sb.AppendLine($"  {spot.Name}: 방향이 수평이 아님"); continue; }
            var hits = Physics.RaycastAll(spot.Position, dir, 3f, spec.CollisionMask, QueryTriggerInteraction.Ignore)
                .Where(h => IsStaticSolid(h.collider, player.transform)).OrderBy(h => h.distance).ToList();
            if (hits.Count == 0) { sb.AppendLine($"  {spot.Name}: 3m 안에 벽이 없음"); continue; }
            var hit = hits[0];
            var t = new Target
            {
                Group = "Spot:" + spot.Name,
                Path = PathOf(hit.collider.transform),
                Source = hit.collider,
                RoadRoot = hit.collider is BoxCollider ? FindRoadRoot(hit.collider.transform) : null,
            };
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            foreach (float off in SpotOffsets)
                t.Samples.Add(new Sample { SurfacePoint = hit.point + side * off, Normal = -dir, AngleSign = off >= 0f ? 1f : -1f });
            result.Add(t);
            sb.AppendLine($"  {spot.Name} → {t.Path} ({hit.distance:0.000}m 앞, 표면 {hit.point:F3})");
        }
        foreach (var t in result) AddNeighbors(t, player, spec);
        log = result.Count == 0 ? "" :
            $"재현 지점 모드: {result.Count}곳, 지점당 좌우 오프셋 {SpotOffsets.Length}개 × 입사각 {AnglesDeg.Length}개 × 속도 {Speeds.Length}개\n" + sb;
        return result;
    }

    private static readonly float[] SpotOffsets = { -0.04f, -0.02f, 0f, 0.02f, 0.04f };

    private static void AddNeighbors(Target t, PlayerMovement player, ProbeSpec spec)
    {
        Bounds b = t.Source.bounds;
        Vector3 ext = b.extents + Vector3.one * NeighborMargin;
        // 큰 건물·바닥판은 지점 주변만 보면 충분하다
        if (t.Samples.Count > 0 && t.Group.StartsWith("Spot:", StringComparison.Ordinal))
        {
            b = new Bounds(t.Samples[0].SurfacePoint, Vector3.zero);
            ext = Vector3.one * (NeighborMargin + 0.5f);
        }
        var found = Physics.OverlapBox(b.center, ext, Quaternion.identity, spec.CollisionMask, QueryTriggerInteraction.Ignore);
        t.Neighbors.Add(t.Source);
        foreach (var c in found)
            if (c != t.Source && IsStaticSolid(c, player.transform)) t.Neighbors.Add(c);
    }

    private static Transform FindRoadRoot(Transform t)
    {
        for (var cur = t; cur != null; cur = cur.parent)
            if (cur.name.StartsWith("road_", StringComparison.Ordinal) && cur.GetComponent<MeshFilter>() != null)
                return cur;
        return null;
    }

    private static Vector3 WorldSize(BoxCollider box) => Vector3.Scale(box.size, Abs(box.transform.lossyScale));
    private static float LongSide(BoxCollider box) { var s = WorldSize(box); return Mathf.Max(s.x, s.z); }

    // 바닥판이 아니라 세워진 연석인지: 높이가 탐침 지름보다 충분히 크고, 수평 한 변이 얇다
    private static bool LooksLikeCurb(BoxCollider box, ProbeSpec spec)
    {
        Vector3 s = WorldSize(box);
        float thin = Mathf.Min(s.x, s.z), longSide = Mathf.Max(s.x, s.z);
        return s.y >= spec.WorldRadius * 4f && thin < longSide * 0.25f;
    }

    private static void SampleBox(Target t, BoxCollider box, System.Random rng)
    {
        var tr = box.transform;
        Vector3 ws = WorldSize(box);
        bool normalIsX = ws.x <= ws.z;                      // 얇은 수평축이 법선
        Vector3 axisN = normalIsX ? Vector3.right : Vector3.forward;
        Vector3 axisT = normalIsX ? Vector3.forward : Vector3.right;
        float halfN = (normalIsX ? box.size.x : box.size.z) * 0.5f;
        float halfT = (normalIsX ? box.size.z : box.size.x) * 0.5f;

        for (int i = 0; i < SamplesPerTarget; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;              // 양쪽 면을 번갈아
            float u = (float)(rng.NextDouble() - 0.5);       // 가운데 50% 구간
            Vector3 mid = tr.TransformPoint(box.center + axisT * (halfT * u));
            Vector3 p = tr.TransformPoint(box.center + axisN * (halfN * side) + axisT * (halfT * u));
            Vector3 n = Flat(p - mid);                       // 음수 스케일이어도 바깥쪽을 향하도록 중심→면 방향으로 계산
            if (n == Vector3.zero) continue;
            t.Samples.Add(new Sample { SurfacePoint = p, Normal = n, AngleSign = rng.Next(2) == 0 ? 1f : -1f });
        }
    }

    private static void SampleMesh(Target t, System.Random rng)
    {
        Bounds b = t.Source.bounds;
        int attempts = 0;
        while (t.Samples.Count < SamplesPerTarget && attempts++ < SamplesPerTarget * 8)
        {
            float a = (float)(rng.NextDouble() * Math.PI * 2);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 origin = b.center + dir * (b.extents.magnitude + 1f);
            origin.y = b.min.y + 0.02f;                       // 바닥 근처 벽면
            if (!t.Source.Raycast(new Ray(origin, -dir), out var hit, b.extents.magnitude * 2f + 2f)) continue;
            Vector3 n = Flat(hit.normal);
            if (n == Vector3.zero || Mathf.Abs(hit.normal.y) > 0.5f) continue;
            t.Samples.Add(new Sample { SurfacePoint = hit.point, Normal = n, AngleSign = rng.Next(2) == 0 ? 1f : -1f });
        }
    }

    // ---- 판정 ---------------------------------------------------------------
    /// <summary>점이 콜라이더 안에 있는지. 오목한 MeshCollider는 뒷면까지 맞는 레이로 확인한다.</summary>
    private static bool IsInside(Collider c, Vector3 p, Vector3 probeDir)
    {
        if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider || (c is MeshCollider m && m.convex))
            return (c.ClosestPoint(p) - p).sqrMagnitude < 1e-12f;

        Physics.queriesHitBackfaces = true;
        bool inside = c.Raycast(new Ray(p, probeDir), out var hit, 1000f) && Vector3.Dot(hit.normal, probeDir) > 0f;
        Physics.queriesHitBackfaces = false;
        return inside;
    }

    /// <summary>시작점과 끝점 사이에 벽(앞면→앞면)이 끼어 있는지 = 반대편으로 넘어갔는지</summary>
    private static bool IsOnFarSide(Collider c, Vector3 start, Vector3 end)
    {
        Vector3 d = end - start;
        float len = d.magnitude;
        if (len < 1e-6f) return false;
        Vector3 dir = d / len;
        return c.Raycast(new Ray(start, dir), out _, len) && c.Raycast(new Ray(end, -dir), out _, len);
    }

    // ---- 미리보기 물리 월드 ----------------------------------------------------
    private sealed class PreviewWorld : IDisposable
    {
        private readonly Scene _scene;
        private readonly PhysicsScene _physics;
        private readonly Config _cfg;
        private readonly ProbeSpec _spec;
        private readonly Rigidbody _probe;
        private readonly Collider _probeCol;
        private readonly Dictionary<Collider, GameObject> _copies = new Dictionary<Collider, GameObject>();
        private readonly Dictionary<Transform, GameObject> _roadMeshCopies = new Dictionary<Transform, GameObject>();
        private readonly List<GameObject> _active = new List<GameObject>();
        private Collider _wall;    // 이번 대상에서 판정 기준이 되는 복제 콜라이더
        private Target _target;

        public PreviewWorld(ProbeSpec spec, Config cfg)
        {
            _spec = spec;
            _cfg = cfg;
            _scene = EditorSceneManager.NewPreviewScene();
            _physics = _scene.GetPhysicsScene();

            var go = new GameObject("Probe");
            SceneManager.MoveGameObjectToScene(go, _scene);
            go.layer = spec.Layer;
            go.transform.localScale = spec.Scale;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = spec.Radius; cap.height = spec.Height; cap.center = spec.Center; cap.direction = spec.Direction;
            cap.sharedMaterial = new PhysicsMaterial("ProbeNoFriction")
            {
                dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
            _probeCol = cap;
            _probe = go.AddComponent<Rigidbody>();
            _probe.mass = spec.Mass;
            _probe.useGravity = true;
            _probe.constraints = RigidbodyConstraints.FreezeRotation;
            _probe.interpolation = RigidbodyInterpolation.None;
            _probe.collisionDetectionMode = cfg.Mode;
        }

        public void Activate(Target t)
        {
            foreach (var go in _active) go.SetActive(false);
            _active.Clear();
            _target = t;
            _wall = null;

            foreach (var c in t.Neighbors)
            {
                Transform road = _cfg.RoadAsMesh && c is BoxCollider ? FindRoadRoot(c.transform) : null;
                GameObject go = road != null ? GetRoadMeshCopy(road) : GetCopy(c);
                if (go == null) continue;
                if (!go.activeSelf) { go.SetActive(true); _active.Add(go); }
                if (c == t.Source) _wall = go.GetComponent<Collider>();
            }
            Physics.SyncTransforms();
        }

        private GameObject GetRoadMeshCopy(Transform road)
        {
            if (_roadMeshCopies.TryGetValue(road, out var go)) return go;
            go = NewCopyRoot(road, "RoadMesh_" + road.name);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = road.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = false;
            _roadMeshCopies[road] = go;
            return go;
        }

        private GameObject GetCopy(Collider c)
        {
            if (_copies.TryGetValue(c, out var go)) return go;
            switch (c)
            {
                case BoxCollider b:
                    go = NewCopyRoot(c.transform, "Copy_" + c.name);
                    var nb = go.AddComponent<BoxCollider>(); nb.center = b.center; nb.size = b.size; break;
                case MeshCollider m when m.sharedMesh != null:
                    go = NewCopyRoot(c.transform, "Copy_" + c.name);
                    var nm = go.AddComponent<MeshCollider>(); nm.sharedMesh = m.sharedMesh; nm.convex = m.convex; break;
                case SphereCollider s:
                    go = NewCopyRoot(c.transform, "Copy_" + c.name);
                    var ns = go.AddComponent<SphereCollider>(); ns.center = s.center; ns.radius = s.radius; break;
                case CapsuleCollider k:
                    go = NewCopyRoot(c.transform, "Copy_" + c.name);
                    var nk = go.AddComponent<CapsuleCollider>(); nk.center = k.center; nk.radius = k.radius; nk.height = k.height; nk.direction = k.direction; break;
                default:
                    go = null; break;   // Terrain 등은 복제하지 않는다
            }
            _copies[c] = go;
            return go;
        }

        // 부모의 회전·스케일(음수 스케일 포함)까지 똑같이 재현하도록 조상 계층을 그대로 복제한다
        private GameObject NewCopyRoot(Transform src, string name)
        {
            var chain = new List<Transform>();
            for (var cur = src; cur != null; cur = cur.parent) chain.Add(cur);
            chain.Reverse();

            Transform parent = null;
            GameObject go = null;
            foreach (var node in chain)
            {
                go = new GameObject(node == src ? name : node.name);
                if (parent == null) SceneManager.MoveGameObjectToScene(go, _scene);
                else go.transform.SetParent(parent, false);
                go.transform.localPosition = node.localPosition;
                go.transform.localRotation = node.localRotation;
                go.transform.localScale = node.localScale;
                parent = go.transform;
            }
            go.layer = src.gameObject.layer;
            go.SetActive(false);
            return go;
        }

        public Outcome RunTrial(Sample s, float speed, float angleDeg, out string trace)
        {
            trace = "";
            if (_wall == null) return Outcome.Skipped;
            float travel = Thickness(_target) + 0.04f;
            return Simulate(_wall, s, speed, angleDeg, travel, out trace);
        }

        // 벽 두께 + 여유만큼 밀어 본다(관통했다면 반대편까지 나갈 수 있게)
        private static float Thickness(Target t)
        {
            if (t.Source is BoxCollider b) { var ws = WorldSize(b); return Mathf.Min(ws.x, ws.z); }
            return Mathf.Min(0.5f, Mathf.Min(t.Source.bounds.size.x, t.Source.bounds.size.z));
        }

        private Outcome Simulate(Collider wall, Sample s, float speed, float angleDeg, float travel, out string trace)
        {
            trace = "";
            float dt = _cfg.FixedDt;
            float r = _spec.WorldRadius;
            float gap = 0.01f + speed * dt * 3f;                      // 몇 스텝 가속 구간
            Vector3 dir = Quaternion.AngleAxis(angleDeg, Vector3.up) * -s.Normal;
            Vector3 q = s.SurfacePoint + s.Normal * (r + gap);

            // 이 구성의 지형에서 출발점 바닥 높이
            float top = wall.bounds.max.y;
            if (!_physics.Raycast(new Vector3(q.x, top + 1f, q.z), Vector3.down, out var ground, wall.bounds.size.y + 5f,
                    _spec.CollisionMask, QueryTriggerInteraction.Ignore))
                return Outcome.Skipped;
            if (ground.collider == wall && ground.point.y > top - 0.001f) return Outcome.Skipped; // 벽 위에 선 경우
            float feet = ground.point.y;
            if (feet + _spec.HalfHeight * 2f > top) return Outcome.Skipped;                       // 개미가 벽보다 높은 곳에 선 경우

            Vector3 startCenter = new Vector3(q.x, feet + _spec.HalfHeight + 0.002f, q.z);
            Vector3 start = startCenter - Vector3.up * _spec.CenterYOffset;
            _probe.position = start;
            _probe.transform.position = start;
            _probe.linearVelocity = Vector3.zero;
            _probe.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();

            for (int i = 0; i < SettleSteps; i++)
            {
                _probe.linearVelocity = new Vector3(0f, _probe.linearVelocity.y, 0f);
                _physics.Simulate(dt);
            }
            Vector3 settled = _probe.position + Vector3.up * _spec.CenterYOffset;
            if (settled.y < feet - 0.05f) return Outcome.Skipped;     // 바닥이 없어서 떨어짐

            int steps = Mathf.Clamp(Mathf.CeilToInt((gap + travel) / (speed * dt)) + 3, 5, 600);
            float maxOverlap = 0f;
            for (int i = 0; i < steps; i++)
            {
                Vector3 v = dir * speed;                 // PlayerMovement와 같이 매 스텝 수평 속도 대입, y는 유지
                v.y = _probe.linearVelocity.y;
                _probe.linearVelocity = v;
                _physics.Simulate(dt);
                if (Physics.ComputePenetration(_probeCol, _probe.position, _probe.rotation,
                        wall, wall.transform.position, wall.transform.rotation, out _, out float depth))
                    maxOverlap = Mathf.Max(maxOverlap, depth);
            }

            Vector3 end = _probe.position + Vector3.up * _spec.CenterYOffset;
            bool inside = IsInside(wall, end, dir);
            bool far = IsOnFarSide(wall, new Vector3(settled.x, end.y, settled.z), end); // 수평 경로로 판정(낙하와 구분)
            trace = $"start={settled:F3} end={end:F3} feet={feet:F3} ground={ground.collider.name} inside={inside} far={far} maxOverlap={maxOverlap:0.0000}";
            if (inside || far) return Outcome.Through;
            return maxOverlap > r ? Outcome.Penetrated : Outcome.Blocked;
        }

        /// <summary>
        /// 측정 도구 검증: 바닥 위의 두꺼운 상자는 느린 속도에서 항상 막혀야 하고,
        /// 아주 얇은 판(4mm)을 빠르게 치면 Discrete는 뚫리고 Continuous는 막혀야 CCD가 이 씬에서 동작하는 것이다.
        /// </summary>
        public string SanityCheck()
        {
            string thick = RunWall(new Vector3(1f, 1f, 1f), 0.8f, 20);
            string thin = RunWall(new Vector3(0.004f, 1f, 1f), 5.2f, 20);
            return $"[{_cfg.Name}] 검증  1m 상자 0.8m/s: {thick}  |  4mm 판 5.2m/s: {thin}";
        }

        private string RunWall(Vector3 size, float speed, int n)
        {
            var floor = new GameObject("SanityFloor");
            SceneManager.MoveGameObjectToScene(floor, _scene);
            floor.transform.position = new Vector3(0f, 999.5f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(4f, 1f, 4f);   // 윗면 y=1000
            var wallGo = new GameObject("SanityWall");
            SceneManager.MoveGameObjectToScene(wallGo, _scene);
            wallGo.transform.position = new Vector3(0f, 1000f + size.y * 0.5f, 0f);
            var bc = wallGo.AddComponent<BoxCollider>();
            bc.size = size;
            Physics.SyncTransforms();

            var sample = new Sample { SurfacePoint = new Vector3(size.x * 0.5f, 1000f, 0f), Normal = Vector3.right, AngleSign = 1f };
            int blocked = 0, pen = 0, through = 0, skipped = 0;
            for (int i = 0; i < n; i++)
            {
                switch (Simulate(bc, sample, speed, 0f, size.x + 0.04f, out _))
                {
                    case Outcome.Blocked: blocked++; break;
                    case Outcome.Penetrated: pen++; break;
                    case Outcome.Through: through++; break;
                    default: skipped++; break;
                }
            }
            UnityEngine.Object.DestroyImmediate(wallGo);
            UnityEngine.Object.DestroyImmediate(floor);
            return $"막힘 {blocked}/파고듦 {pen}/관통 {through}" + (skipped > 0 ? $"/제외 {skipped}" : "");
        }

        public void Dispose() => EditorSceneManager.ClosePreviewScene(_scene);
    }

    // ---- 결과 -----------------------------------------------------------------
    private sealed class ResultRow
    {
        public string Config, Group, SpeedLabel;
        public float Speed, Angle;
        public int Trials, Penetrated, Through, Skipped;
    }

    private static ResultRow FindOrAdd(List<ResultRow> rows, string cfg, string group, string label, float speed, float angle)
    {
        foreach (var r in rows)
            if (r.Config == cfg && r.Group == group && r.SpeedLabel == label && Mathf.Approximately(r.Angle, angle))
                return r;
        var row = new ResultRow { Config = cfg, Group = group, SpeedLabel = label, Speed = speed, Angle = angle };
        rows.Add(row);
        return row;
    }

    private static string WriteResults(List<ResultRow> rows, Dictionary<string, int> hotspots, string collectLog,
        string notes, List<string> cases)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string dir = Path.Combine(root, "Benchmarks", "Tunneling");
        Directory.CreateDirectory(dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var inv = CultureInfo.InvariantCulture;
        var utf8 = new UTF8Encoding(false);

        var csv = new StringBuilder("config,group,speed_label,speed,angle_deg,trials,penetrated,through,through_rate,skipped\n");
        foreach (var r in rows)
            csv.AppendLine(string.Join(",", r.Config, r.Group, r.SpeedLabel, r.Speed.ToString(inv), r.Angle.ToString(inv),
                r.Trials, r.Penetrated, r.Through, (r.Trials == 0 ? 0f : (float)r.Through / r.Trials).ToString("0.0000", inv), r.Skipped));
        string csvPath = Path.Combine(dir, $"tunneling_{stamp}.csv");
        File.WriteAllText(csvPath, csv.ToString(), utf8);

        var hot = new StringBuilder("config,group,target_path,through\n");
        foreach (var kv in hotspots.OrderByDescending(k => k.Value))
        {
            var p = kv.Key.Split('|');
            hot.AppendLine($"{p[0]},{p[1]},\"{p[2]}\",{kv.Value}");
        }
        File.WriteAllText(Path.Combine(dir, $"tunneling_{stamp}_hotspots.csv"), hot.ToString(), utf8);

        var meta = new StringBuilder();
        meta.AppendLine($"# Tunneling Benchmark {stamp}");
        meta.AppendLine(collectLog);
        meta.AppendLine(notes);
        meta.AppendLine($"속도: {string.Join(", ", Speeds.Select(s => $"{s.Label} {s.Speed}"))}");
        meta.AppendLine("구성: " + string.Join(" / ", Configs.Select(c => $"{c.Name}(dt {c.FixedDt}, {c.Mode}, roadMesh {c.RoadAsMesh})")));
        meta.AppendLine();
        meta.AppendLine("## 관통 사례 (최대 60건)");
        foreach (var c in cases) meta.AppendLine("- " + c);
        File.WriteAllText(Path.Combine(dir, $"tunneling_{stamp}_meta.md"), meta.ToString(), utf8);
        return csvPath;
    }

    private static string BuildSummary(List<ResultRow> rows, string collectLog, string notes, string csvPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Tunneling] 측정 완료 (관통 = 끝났을 때 벽 안에 끼었거나 반대편, 파고듦 = 반지름 이상 겹쳤다 밀려 나옴)");
        sb.AppendLine(collectLog);
        sb.Append(notes);
        var groups = rows.Select(r => r.Group).Distinct().ToList();
        foreach (var cfg in Configs)
        foreach (var group in groups)
        {
            var sel = rows.Where(r => r.Config == cfg.Name && r.Group == group).ToList();
            if (sel.Count == 0) continue;
            int trials = sel.Sum(r => r.Trials), thr = sel.Sum(r => r.Through), pen = sel.Sum(r => r.Penetrated), skip = sel.Sum(r => r.Skipped);
            var bySpeed = string.Join("  ", Speeds.Select(sp =>
            {
                var s = sel.Where(r => r.SpeedLabel == sp.Label).ToList();
                return $"{sp.Label} {s.Sum(r => r.Through)}/{s.Sum(r => r.Trials)}";
            }));
            sb.AppendLine($"{cfg.Name,-16} {group,-12} 관통 {thr}/{trials} ({100f * thr / Math.Max(1, trials):0.0}%) 파고듦 {pen} 제외 {skip}  | {bySpeed}");
        }
        sb.AppendLine("CSV: " + csvPath);
        return sb.ToString();
    }

    // ---- 유틸 -----------------------------------------------------------------
    private static List<Target> Pick(List<Target> list, int max, System.Random rng)
        => list.Count <= max ? list : list.OrderBy(_ => rng.Next()).Take(max).ToList();

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 1e-8f ? Vector3.zero : v.normalized;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
        parts.Reverse();
        return string.Join("/", parts);
    }
}
