// c 단계: MeshCollider 런타임 cooking 필요 여부 감사(씬 저장 안 함). 복제 작업 공간 전용.
// Unity 경고 조건: Read/Write 꺼진 메시는 "기본 cooking + 비균일 스케일 아님 + 메시 dirty 아님"일 때만 빌드에서 쓸 수 있다.
// 이 감사는 씬의 모든 MeshCollider와, 인자로 받은 프리팹 목록(런타임 생성 차량 등)의 MeshCollider를 같은 조건으로 분류한다.
// 사용: -executeMethod PhysicsBench.EditorTools.PhysicsBenchCookAudit.Run -benchPrefabList <txt, 에셋 경로 한 줄씩> -benchVariantRecord <새 json>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchCookAudit
    {
        private const MeshColliderCookingOptions COOK_DEFAULT = MeshColliderCookingOptions.CookForFasterSimulation
            | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;

        [Serializable]
        public class Item
        {
            public string source;
            public string path;
            public string mesh;
            public int cooking;
            public bool convex;
            public bool readable;
            public bool nonUniformScale;
            public bool enabled;
            public bool activeInHierarchy;
            public string prefabAsset;
            public bool cookingOverriddenInScene;
            public bool needsRuntimeCooking;
            public string reason;
        }

        [Serializable]
        public class Record
        {
            public string scene = PhysicsBenchVariants.SCENE;
            public int sceneMeshColliders;
            public int sceneNeedsRuntime;
            public int prefabMeshColliders;
            public int prefabNeedsRuntime;
            public Item[] items;
            public string auditedUtc;
        }

        private static bool NonUniform(Vector3 s)
        {
            float a = Mathf.Abs(s.x), b = Mathf.Abs(s.y), c = Mathf.Abs(s.z);
            return Mathf.Abs(a - b) > 1e-4f * Mathf.Max(a, b) || Mathf.Abs(a - c) > 1e-4f * Mathf.Max(a, c);
        }

        private static Item Classify(MeshCollider mc, string source, string path)
        {
            var mesh = mc.sharedMesh;
            var it = new Item
            {
                source = source,
                path = path,
                mesh = mesh != null ? mesh.name : "",
                cooking = (int)mc.cookingOptions,
                convex = mc.convex,
                readable = mesh != null && mesh.isReadable,
                nonUniformScale = NonUniform(mc.transform.lossyScale),
                enabled = mc.enabled,
                activeInHierarchy = mc.gameObject.activeInHierarchy,
            };
            var src = PrefabUtility.GetCorrespondingObjectFromSource(mc);
            if (src != null)
            {
                it.prefabAsset = AssetDatabase.GetAssetPath(src);
                it.cookingOverriddenInScene = PrefabUtility.GetPropertyModifications(mc.gameObject)?
                    .Any(m => m.target == src && m.propertyPath == "m_CookingOptions") ?? false;
            }
            var reasons = new List<string>();
            if (mesh != null && !it.readable)
            {
                if (mc.cookingOptions != COOK_DEFAULT)
                    reasons.Add("cooking");
                if (it.nonUniformScale)
                    reasons.Add("nonUniformScale");
            }
            it.needsRuntimeCooking = reasons.Count > 0;
            it.reason = string.Join("+", reasons);
            return it;
        }

        public static void Run()
        {
            int code = 1;
            try
            {
                string recordPath = PhysicsBenchVariants.Arg("-benchVariantRecord") ?? throw new Exception("missing -benchVariantRecord");
                if (File.Exists(recordPath))
                    throw new Exception("record exists, refusing to overwrite: " + recordPath);
                string listPath = PhysicsBenchVariants.Arg("-benchPrefabList");
                var rec = new Record();
                var items = new List<Item>();
                EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
                foreach (var mc in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var it = Classify(mc, "scene", PhysicsBenchVariants.PathOf(mc.transform));
                    rec.sceneMeshColliders++;
                    if (it.needsRuntimeCooking)
                        rec.sceneNeedsRuntime++;
                    items.Add(it);
                }
                if (!string.IsNullOrEmpty(listPath))
                    foreach (var line in File.ReadAllLines(listPath).Select(l => l.Trim()).Where(l => l.Length > 0))
                    {
                        var root = AssetDatabase.LoadAssetAtPath<GameObject>(line);
                        if (root == null)
                            throw new Exception("prefab not found: " + line);
                        foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                        {
                            var it = Classify(mc, "prefab:" + line, PhysicsBenchVariants.PathOf(mc.transform));
                            rec.prefabMeshColliders++;
                            if (it.needsRuntimeCooking)
                                rec.prefabNeedsRuntime++;
                            items.Add(it);
                        }
                    }
                rec.items = items.OrderBy(i => i.source, StringComparer.Ordinal).ThenBy(i => i.path, StringComparer.Ordinal).ToArray();
                rec.auditedUtc = DateTime.UtcNow.ToString("O");
                File.WriteAllText(recordPath, JsonUtility.ToJson(rec, true));
                Debug.Log("[PhysicsBenchCookAudit] scene=" + rec.sceneMeshColliders + " needs=" + rec.sceneNeedsRuntime + " prefab=" + rec.prefabMeshColliders + " needs=" + rec.prefabNeedsRuntime);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchCookAudit] " + e);
            }
            EditorApplication.Exit(code);
        }
    }
}
