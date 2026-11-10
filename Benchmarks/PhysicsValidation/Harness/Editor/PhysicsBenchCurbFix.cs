// b 단계: 실제 게임에 넣을 연석 수정 후보(CurbFix). S3c와 대상·계산은 같지만 테스트용 통제(Zone 트리거화 등)는 넣지 않는다.
// 복제 작업 공간 전용. 원본 적용은 사용자 확인 후 별도로 한다.
//
// 대상(S3c와 동일): 활성·비트리거 BoxCollider 중 경로에 /CurbColliders/가 있거나 바닥 오브젝트(road_*, Set_B_Tiles_*)에 직접 붙은 것,
//   월드 윗면 0.1±0.005·월드 높이 0.2±0.005, 위쪽 축 정렬(up.y >= 0.999). 밑면만 -0.3으로 내린다(윗면 유지).
//
// 사용:
//   -executeMethod PhysicsBench.EditorTools.PhysicsBenchCurbFix.ApplyAndSave -benchVariantRecord <새 json>
//   -executeMethod PhysicsBench.EditorTools.PhysicsBenchCurbFix.Verify -benchVariantRecord <새 json>   (씬 저장 안 함)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchCurbFix
    {
        public const float NEW_BOTTOM = -0.3f;

        [Serializable]
        public class BoxState
        {
            public string path;
            public Vector3 size;
            public Vector3 center;
        }

        [Serializable]
        public class FixRecord
        {
            public string variant = "CurbFix";
            public string scene = PhysicsBenchVariants.SCENE;
            public int candidates;
            public int extended;
            public int skippedShape;
            public int skippedNotAxisAligned;
            public int inPrefabInstance;
            public string[] prefabRoots;
            public float newBottomY = NEW_BOTTOM;
            public string appliedUtc;
        }

        [Serializable]
        public class VerifyRecord
        {
            public FixRecord fix;
            public int s3cExtended;
            public int boxesCompared;
            public int boxMismatches;
            public string[] mismatchSamples;
            public int nonCurbCollidersChanged;
            public string[] nonCurbChangedSamples;
            public int bandOverlapBoxes;
            public int bandOverlapWithFloorColliders;
            public int bandOverlapOther;
            public string[] bandOverlapSamples;
            public string[] bandOverlapOtherSamples;
            public string verifiedUtc;
        }

        private static List<BoxCollider> Targets(FixRecord rec)
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
                rec.candidates++;
                var bounds = b.bounds;
                if (Mathf.Abs(bounds.max.y - 0.1f) > 0.005f || Mathf.Abs(bounds.size.y - 0.2f) > 0.005f)
                {
                    rec.skippedShape++;
                    continue;
                }
                if (b.transform.up.y < 0.999f)
                {
                    rec.skippedNotAxisAligned++;
                    continue;
                }
                list.Add(b);
            }
            return list;
        }

        public static FixRecord Apply()
        {
            var rec = new FixRecord { appliedUtc = DateTime.UtcNow.ToString("O") };
            var roots = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var b in Targets(rec))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(b))
                {
                    rec.inPrefabInstance++;
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(b.gameObject);
                    if (root != null)
                        roots.Add(PhysicsBenchVariants.PathOf(root.transform) + " <- " + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)));
                }
                var bounds = b.bounds;
                float sy = b.transform.lossyScale.y;
                float addWorld = bounds.min.y - NEW_BOTTOM;
                var size = b.size;
                var center = b.center;
                size.y += addWorld / Mathf.Abs(sy);
                center.y -= (addWorld / 2f) / sy;
                b.size = size;
                b.center = center;
                rec.extended++;
            }
            rec.prefabRoots = roots.ToArray();
            Physics.SyncTransforms();
            return rec;
        }

        public static void ApplyAndSave()
        {
            int code = 1;
            try
            {
                string recordPath = PhysicsBenchVariants.Arg("-benchVariantRecord") ?? throw new Exception("missing -benchVariantRecord");
                if (File.Exists(recordPath))
                    throw new Exception("record exists, refusing to overwrite: " + recordPath);
                var scene = EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
                var rec = Apply();
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(recordPath, JsonUtility.ToJson(rec, true));
                Debug.Log("[PhysicsBenchCurbFix] candidates=" + rec.candidates + " extended=" + rec.extended + " prefabInstance=" + rec.inPrefabInstance);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchCurbFix] " + e);
            }
            EditorApplication.Exit(code);
        }

        private static Dictionary<string, string> ColliderStates()
        {
            var d = new Dictionary<string, string>();
            int dup = 0;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string key = PhysicsBenchVariants.PathOf(c.transform) + "|" + c.GetType().Name + "|" + c.GetInstanceID();
                string v = c.enabled + "|" + c.isTrigger;
                if (c is BoxCollider bc)
                    v += "|" + bc.size.ToString("F5") + "|" + bc.center.ToString("F5");
                d[key] = v;
                dup++;
            }
            return d;
        }

        // 같은 씬에서 (1) 원본 상태 기록 → CurbFix 적용 → 연석 외 콜라이더 불변 확인 → 연장 띠 겹침 검사
        // (2) 씬을 다시 열어 S3c 적용 → 연석 Box 크기·중심이 CurbFix와 같은지 비교
        public static void Verify()
        {
            int code = 1;
            try
            {
                string recordPath = PhysicsBenchVariants.Arg("-benchVariantRecord") ?? throw new Exception("missing -benchVariantRecord");
                if (File.Exists(recordPath))
                    throw new Exception("record exists, refusing to overwrite: " + recordPath);
                var v = new VerifyRecord();

                EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
                var before = ColliderStates();
                var fix = Apply();
                v.fix = fix;
                var tmp = new FixRecord();
                var fixedBoxes = new HashSet<BoxCollider>();
                // 연장된 Box 식별: 밑면이 NEW_BOTTOM인 연석 후보
                var floors = new HashSet<GameObject>(PhysicsBenchVariants.FloorObjects());
                var fixState = new Dictionary<string, BoxState>();
                foreach (var b in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!b.enabled || b.isTrigger)
                        continue;
                    string p = PhysicsBenchVariants.PathOf(b.transform);
                    if (!(p.Contains("/CurbColliders/") || floors.Contains(b.gameObject)))
                        continue;
                    if (Mathf.Abs(b.bounds.min.y - NEW_BOTTOM) < 0.002f && Mathf.Abs(b.bounds.max.y - 0.1f) < 0.005f)
                    {
                        fixedBoxes.Add(b);
                        fixState[p + "|" + b.GetInstanceID()] = new BoxState { path = p, size = b.size, center = b.center };
                    }
                }
                var after = ColliderStates();
                var changed = new List<string>();
                foreach (var kv in after)
                {
                    bool isFixed = fixedBoxes.Any(b => kv.Key.EndsWith("|" + b.GetInstanceID()));
                    if (isFixed)
                        continue;
                    if (!before.TryGetValue(kv.Key, out var bv) || bv != kv.Value)
                        changed.Add(kv.Key);
                }
                v.nonCurbCollidersChanged = changed.Count;
                v.nonCurbChangedSamples = changed.Take(20).ToArray();

                // 연장 띠(y NEW_BOTTOM ~ -0.1)에서 다른 비트리거 콜라이더와 겹치는지
                var overlaps = new List<string>();
                foreach (var b in fixedBoxes)
                {
                    var bb = b.bounds;
                    var c = new Vector3(bb.center.x, (NEW_BOTTOM + (-0.1f)) / 2f, bb.center.z);
                    var half = new Vector3(bb.extents.x - 0.001f, (-0.1f - NEW_BOTTOM) / 2f - 0.001f, bb.extents.z - 0.001f);
                    foreach (var o in Physics.OverlapBox(c, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (o == b || (o is BoxCollider ob && fixedBoxes.Contains(ob)))
                            continue;
                        overlaps.Add(PhysicsBenchVariants.PathOf(b.transform) + " x " + PhysicsBenchVariants.PathOf(o.transform) + " (" + o.GetType().Name + ")");
                    }
                }
                v.bandOverlapBoxes = overlaps.Count;
                var other = overlaps.Where(s => !s.Contains(" x PostProcessingVolume/Demonstration/FloorColliders/")).ToList();
                v.bandOverlapWithFloorColliders = overlaps.Count - other.Count;
                v.bandOverlapOther = other.Count;
                v.bandOverlapSamples = overlaps.Distinct().Take(10).ToArray();
                v.bandOverlapOtherSamples = other.Distinct().Take(200).ToArray();

                // S3c와 비교
                EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
                var s3c = PhysicsBenchVariantCurbExt.Apply();
                v.s3cExtended = s3c.extended;
                var mism = new List<string>();
                int compared = 0;
                foreach (var b in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    string p = PhysicsBenchVariants.PathOf(b.transform);
                    if (!(Mathf.Abs(b.bounds.min.y - NEW_BOTTOM) < 0.002f) || b.isTrigger || !b.enabled)
                        continue;
                    if (!fixState.Values.Any(s => s.path == p))
                    {
                        mism.Add("onlyS3c:" + p);
                        compared++;
                        continue;
                    }
                    var match = fixState.Values.FirstOrDefault(s => s.path == p && (s.size - b.size).sqrMagnitude < 1e-10f && (s.center - b.center).sqrMagnitude < 1e-10f);
                    compared++;
                    if (match == null)
                        mism.Add(p);
                }
                v.boxesCompared = compared;
                v.boxMismatches = mism.Count;
                v.mismatchSamples = mism.Take(20).ToArray();
                v.verifiedUtc = DateTime.UtcNow.ToString("O");
                File.WriteAllText(recordPath, JsonUtility.ToJson(v, true));
                Debug.Log("[PhysicsBenchCurbFix] verify extended=" + fix.extended + " s3c=" + s3c.extended + " compared=" + compared + " mismatch=" + mism.Count + " nonCurbChanged=" + changed.Count + " bandOverlap=" + overlaps.Count);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchCurbFix] " + e);
            }
            EditorApplication.Exit(code);
        }
    }
}
