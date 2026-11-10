// M10: 설계에 쓰지 않은 연석 지점 후보 목록(기하만, 물리 시뮬레이션·씬 저장 없음). 복제 작업 공간 전용.
// 대상 연석 Box는 PhysicsBenchCurbFix와 같은 규칙(경로 /CurbColliders/ 또는 바닥 오브젝트, 윗면 0.1·높이 0.2, 위쪽 축 정렬).
// 각 Box의 옆면(±x, ±z 로컬) 중 바깥쪽이 도로(-0.1)인 면마다 '면 중앙(mid)'과 '이웃 연석과 이어진 끝(joint)' 후보를 낸다.
// 후보마다 출발점(면에서 0.16m+r) 아래 바닥 높이·콜라이더, 면 너머(연석 위) 높이, 직선 경로를 막는 첫 콜라이더를 기록한다.
//
// 사용: -executeMethod PhysicsBench.EditorTools.PhysicsBenchM10Sites.Run -benchOut <새 json>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchM10Sites
    {
        const float R = 0.0094f;
        const float GAP = 0.16f + R;
        const float ROAD_Y = -0.1f;
        static int MASK = ~0;   // 플레이어 레이어와 충돌하는 레이어만(Run에서 설정)

        [Serializable]
        public class Probe
        {
            public float groundY = float.NaN;
            public string groundPath = "";
            public string groundType = "";
        }

        [Serializable]
        public class Site
        {
            public string kind;           // mid | joint
            public string box;            // 연석 Box 경로
            public int boxId;
            public string neighbor = "";  // joint일 때 이웃 Box
            public float faceLength;
            public Vector3 point;         // 면 위 접점(y = -0.1)
            public Vector3 normal;        // 면 바깥(도로 쪽) 법선
            public Probe start;           // 출발점 아래
            public Probe beyond;          // 면 안쪽 0.05m(연석 위)
            public Probe beyondFar;       // 면 안쪽 0.4m
            public string[] pathHits;     // c00·p02·m02·a30L·a30R 직선 경로 첫 충돌
            public bool[] pathHitTarget;  // 첫 충돌이 대상(또는 joint 이웃) 연석인지
            public string[] startOverlaps;// 출발 캡슐과 겹치는 콜라이더(트리거 포함)
        }

        [Serializable]
        public class Result
        {
            public string scene;
            public string sceneSha256;
            public int curbBoxes;
            public int faces;
            public Site[] sites;
            public string calibS2;
            public string playerLayer;
            public string[] maskLayers;        // 기존 S2 지점(-48.85, 52.5) 면 확인
            public string createdUtc;
        }

        static Probe Down(Vector3 p)
        {
            var pr = new Probe();
            if (Physics.Raycast(new Vector3(p.x, 1.0f, p.z), Vector3.down, out var hit, 3f, MASK, QueryTriggerInteraction.Ignore))
            {
                pr.groundY = hit.point.y;
                pr.groundPath = PhysicsBenchVariants.PathOf(hit.collider.transform);
                pr.groundType = hit.collider.GetType().Name;
            }
            return pr;
        }

        static List<BoxCollider> Curbs()
        {
            var floors = new HashSet<GameObject>(PhysicsBenchVariants.FloorObjects());
            var list = new List<BoxCollider>();
            foreach (var b in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!b.enabled || b.isTrigger)
                    continue;
                string p = PhysicsBenchVariants.PathOf(b.transform);
                if (!(p.Contains("/CurbColliders/") || floors.Contains(b.gameObject)))
                    continue;
                var bb = b.bounds;
                if (Mathf.Abs(bb.max.y - 0.1f) > 0.005f || Mathf.Abs(bb.size.y - 0.2f) > 0.005f || b.transform.up.y < 0.999f)
                    continue;
                list.Add(b);
            }
            return list;
        }

        struct Face
        {
            public BoxCollider box;
            public Vector3 center;   // 면 중심(y=-0.1)
            public Vector3 normal;   // 바깥 법선(수평)
            public Vector3 along;    // 면 방향 단위벡터
            public float half;       // 면 반길이
        }

        static IEnumerable<Face> Faces(BoxCollider b)
        {
            var t = b.transform;
            var ls = t.lossyScale;
            var half = new Vector3(Mathf.Abs(b.size.x * ls.x), 0, Mathf.Abs(b.size.z * ls.z)) / 2f;
            var c = t.TransformPoint(b.center);
            var ax = (t.rotation * Vector3.right);
            var az = (t.rotation * Vector3.forward);
            ax.y = 0; az.y = 0; ax.Normalize(); az.Normalize();
            foreach (var s in new[] { 1f, -1f })
            {
                yield return new Face { box = b, normal = ax * s, along = az, half = half.z, center = new Vector3(c.x, ROAD_Y, c.z) + ax * s * half.x };
                yield return new Face { box = b, normal = az * s, along = ax, half = half.x, center = new Vector3(c.x, ROAD_Y, c.z) + az * s * half.z };
            }
        }

        static string FirstHit(Vector3 start, Vector3 dir, float dist, out Collider col)
        {
            col = null;
            var o = new Vector3(start.x, ROAD_Y + R + 0.012f, start.z);
            if (Physics.SphereCast(o, R, dir, out var hit, dist, MASK, QueryTriggerInteraction.Ignore))
            {
                col = hit.collider;
                return PhysicsBenchVariants.PathOf(hit.collider.transform) + "|" + hit.collider.GetType().Name + "|d=" + hit.distance.ToString("F3");
            }
            return "(none)";
        }

        static Site Make(string kind, Face f, Vector3 point, BoxCollider neighbor)
        {
            var s = new Site
            {
                kind = kind, box = PhysicsBenchVariants.PathOf(f.box.transform), boxId = f.box.GetInstanceID(),
                neighbor = neighbor ? PhysicsBenchVariants.PathOf(neighbor.transform) : "", faceLength = f.half * 2f,
                point = point, normal = f.normal,
                start = Down(point + f.normal * GAP), beyond = Down(point - f.normal * 0.05f), beyondFar = Down(point - f.normal * 0.4f),
            };
            var hits = new List<string>();
            var ok = new List<bool>();
            // c00, p02, m02: 법선 반대 방향 직진, a30L/a30R: 30° 비스듬(같은 접점, 수직거리 GAP)
            var defs = new (Vector3 off, float ang)[] { (Vector3.zero, 0), (f.along * 0.02f, 0), (-f.along * 0.02f, 0), (Vector3.zero, 30), (Vector3.zero, -30) };
            foreach (var (off, ang) in defs)
            {
                var dir = Quaternion.Euler(0, ang, 0) * (-f.normal);
                float dist = GAP / Mathf.Cos(ang * Mathf.Deg2Rad);
                var st = point + off - dir * dist;
                hits.Add(FirstHit(st, dir, dist + 0.05f, out var col));
                ok.Add(col != null && (col == f.box || (neighbor && col == neighbor)));
            }
            s.pathHits = hits.ToArray();
            s.pathHitTarget = ok.ToArray();
            var sp = point + f.normal * GAP;
            var ov = Physics.OverlapCapsule(new Vector3(sp.x, ROAD_Y + 0.05f - R * 0.0f, sp.z), new Vector3(sp.x, ROAD_Y + 0.05f, sp.z), R, MASK, QueryTriggerInteraction.Ignore);
            s.startOverlaps = ov.Select(c => PhysicsBenchVariants.PathOf(c.transform) + "|" + c.GetType().Name + (c.isTrigger ? "|trigger" : "")).ToArray();
            return s;
        }

        public static void Run()
        {
            int code = 1;
            try
            {
                string outPath = PhysicsBenchVariants.Arg("-benchOut") ?? throw new Exception("missing -benchOut");
                if (File.Exists(outPath))
                    throw new Exception("exists, refusing to overwrite: " + outPath);
                EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
                // 런타임 Awake가 트리거로 바꾸는 구역(PhysicsBenchVariants와 같은 목록)을 메모리에서만 맞춘다(씬 저장 안 함)
                var rtTypes = new HashSet<string> { "NPCSpawnZone", "CarStopZone", "CrosswalkWaitZone" };
                foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (mb != null && rtTypes.Contains(mb.GetType().Name))
                        foreach (var c in mb.GetComponents<Collider>())
                            c.isTrigger = true;
                Physics.SyncTransforms();
                var pm = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(m => m != null && m.GetType().Name == "PlayerMovement");
                int pl = pm.gameObject.layer;
                MASK = 0;
                var ml = new List<string>();
                for (int i = 0; i < 32; i++)
                    if (!Physics.GetIgnoreLayerCollision(pl, i)) { MASK |= 1 << i; if (LayerMask.LayerToName(i) != "") ml.Add(i + ":" + LayerMask.LayerToName(i)); }
                var curbs = Curbs();
                var res = new Result { scene = PhysicsBenchVariants.SCENE, curbBoxes = curbs.Count, createdUtc = DateTime.UtcNow.ToString("O") };
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    res.sceneSha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(PhysicsBenchVariants.SCENE))).Replace("-", "").ToLowerInvariant();
                var faces = curbs.SelectMany(Faces).ToList();
                var sites = new List<Site>();
                foreach (var f in faces)
                {
                    // 바깥쪽이 도로(-0.1)인 면만
                    var road = Down(f.center + f.normal * 0.3f);
                    if (float.IsNaN(road.groundY) || Mathf.Abs(road.groundY - ROAD_Y) > 0.01f || f.half < 0.15f)
                        continue;
                    res.faces++;
                    sites.Add(Make("mid", f, f.center, null));
                    // 끝점: 같은 평면·같은 법선의 다른 연석 면이 1cm 안에서 이어지면 joint
                    foreach (var sgn in new[] { 1f, -1f })
                    {
                        var end = f.center + f.along * sgn * f.half;
                        foreach (var g in faces)
                        {
                            if (g.box == f.box || Vector3.Dot(g.normal, f.normal) < 0.999f)
                                continue;
                            if (Mathf.Abs(Vector3.Dot(g.center - f.center, f.normal)) > 0.002f)
                                continue;
                            float along = Vector3.Dot(end - g.center, g.along);
                            float off = Mathf.Abs(along) - g.half;
                            if (off <= 0.01f && off >= -0.01f && f.box.GetInstanceID() < g.box.GetInstanceID())
                            {
                                sites.Add(Make("joint", f, end, g.box));
                                break;
                            }
                        }
                    }
                }
                res.sites = sites.ToArray();
                res.playerLayer = pl + ":" + LayerMask.LayerToName(pl) + " (" + PhysicsBenchVariants.PathOf(pm.transform) + ")";
                res.maskLayers = ml.ToArray();
                var calib = sites.Where(s => Mathf.Abs(s.point.z - 52.5f) < 1.5f && Mathf.Abs(s.point.x + 48.85f) < 0.05f).Select(s => s.box + " x=" + s.point.x.ToString("F4") + " n=" + s.normal).ToArray();
                res.calibS2 = calib.Length > 0 ? string.Join("; ", calib) : "(S2 면 후보 없음)";
                File.WriteAllText(outPath, JsonUtility.ToJson(res, true));
                Debug.Log("[PhysicsBenchM10Sites] curbs=" + res.curbBoxes + " roadFaces=" + res.faces + " sites=" + sites.Count);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchM10Sites] " + e);
            }
            EditorApplication.Exit(code);
        }
    }
}
