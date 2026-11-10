// 트랙 B 바닥 구성 변형. 복제 작업 공간(project-H7B) 전용. 원본 프로젝트에서 실행하지 않는다.
// 같은 Apply 코드를 기하 감사(메모리에서만 적용, 저장 안 함)와 빌드(적용 후 복제본 씬 저장)에 함께 쓴다.
//
// 변형
//   S3  : 현재(H7) Box 바닥 그대로
//   S0a : Box 바닥·연석 끔, 바닥 메시마다 MeshCollider(기본 cooking 30) — H0식
//   S0b : 같은 구성, cooking 26(EnableMeshCleaning 제거) — H1식
//   S0p : S0b와 같음. 메시 Read/Write를 켠 상태에서 빌드하는 것이 차이(SetReadable로 별도 적용)
//   S1  : Box 바닥·연석 끔, 맵 전체 받침판 한 장(윗면 -0.1, 05-26 사용자가 맞춘 높이)
//   S1z : S1과 같고 받침판 윗면 0(05-22·H1 높이). 기하 감사 보조용
//   S2  : 바닥 메시마다 convex MeshCollider(기본 cooking). Read/Write 켠 상태 필요
// 모든 변형 공통 통제: Zone/CarDrivingZone 아래 비트리거 콜라이더를 트리거로 바꾼다(바닥 비교에서 도로 위 보조 지지면 제거).
//
// 사용(배치):
//   -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariants.ApplyAndSave -benchVariant S0a -benchVariantRecord <새 json>
//   -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariants.SetReadable -benchReadable true -benchVariantRecord <새 json>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchVariants
    {
        public const string SCENE = "Assets/Scenes/AntDemoScene.unity";
        private static readonly Regex FLOOR_NAME = new Regex(@"^(road_\d+|Set_B_Tiles_\d+)( \(\d+\))?$");
        private const MeshColliderCookingOptions COOK_DEFAULT = MeshColliderCookingOptions.CookForFasterSimulation
            | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
        private const MeshColliderCookingOptions COOK_NO_CLEAN = COOK_DEFAULT & ~MeshColliderCookingOptions.EnableMeshCleaning;
        // H1 받침판 실측 범위(probe-H1-e2c.json PostProcessingVolume/Ground). 높이만 변형별로 정한다
        private static readonly Vector2 PLATE_MIN_XZ = new Vector2(-108.5f, -83.75f);
        private static readonly Vector2 PLATE_MAX_XZ = new Vector2(94.5f, 87.25f);
        private const float PLATE_THICKNESS = 1.7f;

        public static readonly string[] ALL = { "S3", "S0a", "S0b", "S0p", "S1", "S1z", "S2" };

        [Serializable]
        public class Record
        {
            public string variant;
            public string scene;
            public int floorObjects;
            public string[] floorPaths;
            public int boxesDisabled;
            public int zonesMadeTrigger;
            public int runtimeTriggerZones;
            public int meshCollidersAdded;
            public string cooking;
            public bool convex;
            public bool plateAdded;
            public float plateTopY;
            public int floorMeshesReadable;
            public int floorMeshesTotal;
            public string[] logWarnings;
            public string appliedUtc;
        }

        public static List<GameObject> FloorObjects()
        {
            return UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(f => f.sharedMesh != null && FLOOR_NAME.IsMatch(f.gameObject.name)
                    && (PathOf(f.transform).Contains("/BasicSettingsMap/") || PathOf(f.transform).Contains("/StoreMap/")))
                .Select(f => f.gameObject)
                .OrderBy(g => PathOf(g.transform), StringComparer.Ordinal)
                .ToList();
        }

        public static Record Apply(string variant)
        {
            if (!ALL.Contains(variant))
                throw new Exception("unknown variant " + variant);
            var warnings = new List<string>();
            Application.LogCallback cb = (msg, st, type) =>
            {
                if (type == LogType.Warning || type == LogType.Error)
                    warnings.Add(type + ": " + msg);
            };
            Application.logMessageReceived += cb;
            try
            {
                var rec = new Record { variant = variant, scene = SCENE, appliedUtc = DateTime.UtcNow.ToString("O") };
                var floors = FloorObjects();
                rec.floorObjects = floors.Count;
                rec.floorPaths = floors.Select(g => PathOf(g.transform)).ToArray();
                rec.floorMeshesTotal = floors.Count;
                rec.floorMeshesReadable = floors.Count(g => g.GetComponent<MeshFilter>().sharedMesh.isReadable);

                // 공통 통제
                foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!c.isTrigger && PathOf(c.transform).StartsWith("Zone/CarDrivingZone"))
                    {
                        c.isTrigger = true;
                        rec.zonesMadeTrigger++;
                    }

                // 런타임 Awake가 트리거로 바꾸는 구역(NPCSpawnZone·CarStopZone·CrosswalkWaitZone)을 에디터에서도 같은 상태로 맞춘다
                var runtimeTriggerTypes = new HashSet<string> { "NPCSpawnZone", "CarStopZone", "CrosswalkWaitZone" };
                foreach (var mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (mb != null && runtimeTriggerTypes.Contains(mb.GetType().Name))
                        foreach (var c in mb.GetComponents<Collider>())
                            if (!c.isTrigger)
                            {
                                c.isTrigger = true;
                                rec.runtimeTriggerZones++;
                            }

                if (variant == "S3")
                    return rec;

                // Box 바닥·연석 끄기: FloorColliders·CurbColliders 하위, 그리고 바닥 오브젝트 자체에 붙은 Box
                var floorSet = new HashSet<GameObject>(floors);
                foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (c is MeshCollider || !c.enabled)
                        continue;
                    string p = PathOf(c.transform);
                    if (p.Contains("/FloorColliders/") || p.Contains("/CurbColliders/") || floorSet.Contains(c.gameObject))
                    {
                        c.enabled = false;
                        rec.boxesDisabled++;
                    }
                }

                int groundLayer = LayerMask.NameToLayer("Ground");
                if (variant == "S1" || variant == "S1z")
                {
                    float top = variant == "S1" ? -0.1f : 0f;
                    var go = new GameObject("PhysicsBenchPlate");
                    go.tag = "Ground";
                    if (groundLayer >= 0)
                        go.layer = groundLayer;
                    var box = go.AddComponent<BoxCollider>();
                    var min = new Vector3(PLATE_MIN_XZ.x, top - PLATE_THICKNESS, PLATE_MIN_XZ.y);
                    var max = new Vector3(PLATE_MAX_XZ.x, top, PLATE_MAX_XZ.y);
                    go.transform.position = (min + max) / 2f;
                    box.size = max - min;
                    rec.plateAdded = true;
                    rec.plateTopY = top;
                    return rec;
                }

                bool convex = variant == "S2";
                var cooking = variant == "S0a" || variant == "S2" ? COOK_DEFAULT : COOK_NO_CLEAN;
                foreach (var g in floors)
                {
                    // H1 바닥 MeshCollider와 같은 태그·레이어(probe-H1-e2c.json: Ground / 10)
                    g.tag = "Ground";
                    if (groundLayer >= 0)
                        g.layer = groundLayer;
                    var mc = g.AddComponent<MeshCollider>();
                    mc.cookingOptions = cooking;
                    mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
                    mc.convex = convex;
                    rec.meshCollidersAdded++;
                }
                rec.cooking = ((int)cooking) + " " + cooking;
                rec.convex = convex;
                return rec;
            }
            finally
            {
                Application.logMessageReceived -= cb;
            }
        }

        public static void ApplyAndSave()
        {
            int code = 1;
            try
            {
                string variant = Require("-benchVariant");
                string recordPath = Require("-benchVariantRecord");
                if (File.Exists(recordPath))
                    throw new Exception("record exists, refusing to overwrite: " + recordPath);
                var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
                var rec = Apply(variant);
                // S3도 공통 통제(Zone 트리거)가 들어가므로 항상 저장한다(복제본 씬만)
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(recordPath, JsonUtility.ToJson(rec, true));
                Debug.Log("[PhysicsBenchVariants] applied " + variant + " floors=" + rec.floorObjects + " boxesOff=" + rec.boxesDisabled + " mc=" + rec.meshCollidersAdded);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchVariants] " + e);
            }
            EditorApplication.Exit(code);
        }

        // 바닥 메시 FBX의 Read/Write를 바꾼다(복제본 에셋 임포트 설정만). S0p·S2 빌드 전 true, 그 뒤 false로 되돌린다
        public static void SetReadable()
        {
            int code = 1;
            try
            {
                bool value = bool.Parse(Require("-benchReadable"));
                string recordPath = Require("-benchVariantRecord");
                if (File.Exists(recordPath))
                    throw new Exception("record exists, refusing to overwrite: " + recordPath);
                EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
                var assets = FloorObjects().Select(g => AssetDatabase.GetAssetPath(g.GetComponent<MeshFilter>().sharedMesh))
                    .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
                var changed = new List<string>();
                foreach (var p in assets)
                {
                    var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (imp == null)
                        throw new Exception("not a model asset: " + p);
                    if (imp.isReadable != value)
                    {
                        imp.isReadable = value;
                        imp.SaveAndReimport();
                        changed.Add(p);
                    }
                }
                File.WriteAllText(recordPath, JsonUtility.ToJson(new ReadableRecord { value = value, assets = assets.ToArray(), changed = changed.ToArray() }, true));
                Debug.Log("[PhysicsBenchVariants] readable=" + value + " assets=" + assets.Count + " changed=" + changed.Count);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchVariants] " + e);
            }
            EditorApplication.Exit(code);
        }

        [Serializable]
        private class ReadableRecord
        {
            public bool value;
            public string[] assets;
            public string[] changed;
        }

        public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static string Require(string name) => Arg(name) ?? throw new Exception("missing argument " + name);

        public static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
