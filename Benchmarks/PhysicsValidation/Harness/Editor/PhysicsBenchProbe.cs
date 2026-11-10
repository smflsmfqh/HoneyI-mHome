// 에디터 전용 읽기 도구. 씬을 열기만 하고 저장하지 않는다.
// 1) 실제 맵 case의 기준 높이를 물리 콜라이더가 아닌 시각 메시(MeshFilter 삼각형)에서 읽는다.
//    빌드에서 의심받는 MeshCollider로 기준을 잡으면 순환 논리가 되기 때문이다.
// 2) MeshCollider마다 빌드 런타임 cooking 조건(Read/Write, 스케일, cooking 옵션)을 기록한다.
//
// 사용: Unity -batchmode -quit -projectPath <복원본> -executeMethod PhysicsBench.EditorTools.PhysicsBenchProbe.Run
//        -probeOut <새 json> [-probePoints "x,z;x,z;..." | -probePointsFile <x,z 한 줄씩>]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchProbe
    {
        private const string SCENE = "Assets/Scenes/AntDemoScene.unity";
        private const float RAY_TOP = 50f;

        public static void Run()
        {
            int code = 1;
            try
            {
                code = RunInternal();
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchProbe] " + e);
            }
            EditorApplication.Exit(code);
        }

        private static int RunInternal()
        {
            string outPath = Arg("-probeOut") ?? throw new Exception("missing -probeOut");
            if (File.Exists(outPath))
                throw new Exception("output exists, refusing to overwrite: " + outPath);
            var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);

            var report = new ProbeReport { scene = SCENE, sceneDirtyAfterOpen = scene.isDirty, unityVersion = Application.unityVersion };
            report.bakeCollisionMeshes = PlayerSettings.bakeCollisionMeshes;

            var colliders = new List<MeshColliderInfo>();
            foreach (var mc in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mesh = mc.sharedMesh;
                Vector3 s = mc.transform.lossyScale;
                bool uniform = Mathf.Approximately(Mathf.Abs(s.x), Mathf.Abs(s.y)) && Mathf.Approximately(Mathf.Abs(s.y), Mathf.Abs(s.z));
                bool negative = s.x < 0 || s.y < 0 || s.z < 0;
                bool defaultCooking = mc.cookingOptions == (MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning
                    | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase);
                bool readable = mesh != null && mesh.isReadable;
                colliders.Add(new MeshColliderInfo
                {
                    path = PathOf(mc.transform),
                    mesh = mesh != null ? mesh.name : null,
                    meshAsset = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null,
                    active = mc.gameObject.activeInHierarchy,
                    enabled = mc.enabled,
                    convex = mc.convex,
                    isTrigger = mc.isTrigger,
                    tag = mc.tag,
                    layer = mc.gameObject.layer,
                    lossyScale = new[] { s.x, s.y, s.z },
                    uniformScale = uniform,
                    negativeScale = negative,
                    cookingOptions = mc.cookingOptions.ToString(),
                    defaultCooking = defaultCooking,
                    readable = readable,
                    // Unity 로그 문구 기준: 읽기 불가 메시는 기본 cooking·균일 스케일일 때만 빌드 시 미리 처리된다.
                    needsRuntimeCookingWithoutReadWrite = !readable && (!uniform || negative || !defaultCooking),
                    otherColliders = mc.GetComponents<Collider>().Where(c => c != mc).Select(c => c.GetType().Name + (c.enabled ? "" : "(off)")).ToArray(),
                    boundsMin = new[] { mc.bounds.min.x, mc.bounds.min.y, mc.bounds.min.z },
                    boundsMax = new[] { mc.bounds.max.x, mc.bounds.max.y, mc.bounds.max.z },
                });
            }
            report.meshColliders = colliders.OrderBy(c => c.path, StringComparer.Ordinal).ToArray();

            // 메시가 아닌 지지 후보(숨은 바닥 포함): Ground/Road 태그 또는 Ground 레이어의 비트리거 콜라이더
            int groundLayer = LayerMask.NameToLayer("Ground");
            report.supportColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => !(c is MeshCollider) && !c.isTrigger && (c.CompareTag("Ground") || c.CompareTag("Road") || c.gameObject.layer == groundLayer))
                .Select(c => new BoundsInfo
                {
                    path = PathOf(c.transform),
                    type = c.GetType().Name,
                    tag = c.tag,
                    active = c.gameObject.activeInHierarchy && c.enabled,
                    min = new[] { c.bounds.min.x, c.bounds.min.y, c.bounds.min.z },
                    max = new[] { c.bounds.max.x, c.bounds.max.y, c.bounds.max.z },
                }).OrderBy(b => b.path, StringComparer.Ordinal).ToArray();
            report.meshColliderBounds = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.CompareTag("Ground") || c.CompareTag("Road"))
                .Select(c => new BoundsInfo
                {
                    path = PathOf(c.transform),
                    type = "MeshCollider",
                    tag = c.tag,
                    active = c.gameObject.activeInHierarchy && c.enabled,
                    min = new[] { c.bounds.min.x, c.bounds.min.y, c.bounds.min.z },
                    max = new[] { c.bounds.max.x, c.bounds.max.y, c.bounds.max.z },
                }).OrderBy(b => b.path, StringComparer.Ordinal).ToArray();

            var dests = new List<string>();
            foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mb.GetType().Name == "Destination")
                {
                    var b = mb.GetComponent<Collider>() != null ? mb.GetComponent<Collider>().bounds : new Bounds(mb.transform.position, Vector3.zero);
                    dests.Add(PathOf(mb.transform) + " center=" + b.center.ToString("F3") + " size=" + b.size.ToString("F3"));
                }
            report.destinations = dests.ToArray();

            var points = Arg("-probePoints");
            var pointsFile = Arg("-probePointsFile");
            if (!string.IsNullOrEmpty(pointsFile))
                points = string.Join(";", File.ReadAllLines(pointsFile).Select(l => l.Trim()).Where(l => l.Length > 0));
            if (!string.IsNullOrEmpty(points))
            {
                var filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(f => f.sharedMesh != null && f.GetComponent<Renderer>() != null && f.GetComponent<Renderer>().enabled).ToArray();
                var results = new List<PointResult>();
                foreach (var token in points.Split(';'))
                {
                    var xz = token.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    results.Add(ProbePoint(xz[0], xz[1], filters));
                }
                report.points = results.ToArray();
            }

            File.WriteAllText(outPath, JsonUtility.ToJson(report, true));
            Debug.Log("[PhysicsBenchProbe] wrote " + outPath + " meshColliders=" + colliders.Count);
            return 0;
        }

        // 위에서 아래로 쏜 광선과 시각 메시 삼각형의 교차를 모두 구한다(위쪽 면만).
        private static PointResult ProbePoint(float x, float z, MeshFilter[] filters)
        {
            var origin = new Vector3(x, RAY_TOP, z);
            var hits = new List<SurfaceHit>();
            foreach (var f in filters)
            {
                var b = f.GetComponent<Renderer>().bounds;
                if (x < b.min.x || x > b.max.x || z < b.min.z || z > b.max.z)
                    continue;
                var mesh = f.sharedMesh;
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                var m = f.transform.localToWorldMatrix;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(verts[tris[i]]);
                    Vector3 c1 = m.MultiplyPoint3x4(verts[tris[i + 1]]);
                    Vector3 c2 = m.MultiplyPoint3x4(verts[tris[i + 2]]);
                    if (!RayDown(origin, a, c1, c2, out float y, out Vector3 n))
                        continue;
                    if (n.y < 0.5f)
                        continue;
                    hits.Add(new SurfaceHit { y = y, normalY = n.y, path = PathOf(f.transform), mesh = mesh.name });
                }
            }
            var ordered = hits.OrderByDescending(h => h.y).ToList();
            // 같은 면의 인접 삼각형 중복 제거
            var unique = new List<SurfaceHit>();
            foreach (var h in ordered)
                if (!unique.Any(u => u.path == h.path && Mathf.Abs(u.y - h.y) < 1e-4f))
                    unique.Add(h);
            // 이 xz 열을 지나는 모든 비트리거 콜라이더(태그·레이어 무관). 숨은 지지면·대상 연석 식별용.
            var cols = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && x >= c.bounds.min.x && x <= c.bounds.max.x && z >= c.bounds.min.z && z <= c.bounds.max.z
                    && c.bounds.max.y > -2f && c.bounds.min.y < 2f)
                .Select(c => new ColumnCollider
                {
                    path = PathOf(c.transform),
                    type = c.GetType().Name,
                    tag = c.tag,
                    layer = LayerMask.LayerToName(c.gameObject.layer),
                    minY = c.bounds.min.y,
                    maxY = c.bounds.max.y,
                    min = new[] { c.bounds.min.x, c.bounds.min.y, c.bounds.min.z },
                    max = new[] { c.bounds.max.x, c.bounds.max.y, c.bounds.max.z },
                }).OrderByDescending(c => c.maxY).ToArray();
            return new PointResult { x = x, z = z, surfaces = unique.Take(8).ToArray(), colliders = cols };
        }

        private static bool RayDown(Vector3 o, Vector3 a, Vector3 b, Vector3 c, out float y, out Vector3 normal)
        {
            y = 0;
            normal = Vector3.Cross(b - a, c - a);
            if (normal.y < 0)
                normal = -normal;
            normal.Normalize();
            // xz 평면에서 무게중심 좌표로 점 포함 판정
            float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(d) < 1e-12f)
                return false;
            float w1 = ((b.z - c.z) * (o.x - c.x) + (c.x - b.x) * (o.z - c.z)) / d;
            float w2 = ((c.z - a.z) * (o.x - c.x) + (a.x - c.x) * (o.z - c.z)) / d;
            float w3 = 1 - w1 - w2;
            if (w1 < 0 || w2 < 0 || w3 < 0)
                return false;
            y = w1 * a.y + w2 * b.y + w3 * c.y;
            return true;
        }

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        [Serializable]
        private class ProbeReport
        {
            public string scene;
            public string unityVersion;
            public bool sceneDirtyAfterOpen;
            public bool bakeCollisionMeshes;
            public MeshColliderInfo[] meshColliders;
            public BoundsInfo[] supportColliders;
            public BoundsInfo[] meshColliderBounds;
            public string[] destinations;
            public PointResult[] points;
        }

        [Serializable]
        private class MeshColliderInfo
        {
            public string path;
            public string mesh;
            public string meshAsset;
            public bool active;
            public bool enabled;
            public bool convex;
            public bool isTrigger;
            public string tag;
            public int layer;
            public float[] lossyScale;
            public bool uniformScale;
            public bool negativeScale;
            public string cookingOptions;
            public bool defaultCooking;
            public bool readable;
            public bool needsRuntimeCookingWithoutReadWrite;
            public string[] otherColliders;
            public float[] boundsMin;
            public float[] boundsMax;
        }

        [Serializable]
        private class BoundsInfo
        {
            public string path;
            public string type;
            public string tag;
            public bool active;
            public float[] min;
            public float[] max;
        }

        [Serializable]
        private class PointResult
        {
            public float x;
            public float z;
            public SurfaceHit[] surfaces;
            public ColumnCollider[] colliders;
        }

        [Serializable]
        private class ColumnCollider
        {
            public string path;
            public string type;
            public string tag;
            public string layer;
            public float minY;
            public float maxY;
            public float[] min;
            public float[] max;
        }

        [Serializable]
        private class SurfaceHit
        {
            public float y;
            public float normalY;
            public string path;
            public string mesh;
        }
    }
}
