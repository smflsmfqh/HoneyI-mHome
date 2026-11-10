// 트랙 B-02 변형 S3c: S3(현재 Box)에 더해 연석 Box의 밑면만 -0.3으로 내린다(윗면 유지). 복제 작업 공간 전용.
// 목적: 연석 관통 기전 가설(밑면 근처 진입 → 아래로 밀려 연석 밑 통과) 검증. 실제 게임을 고치는 것이 아니다.
// 동결된 PhysicsBenchVariants.cs는 바꾸지 않고 그 Apply("S3")를 호출한 뒤 연석만 바꾼다.
//
// 대상 정의(결과 전 고정): 활성·비트리거 BoxCollider 중 경로에 /CurbColliders/가 있거나 바닥 오브젝트(road_*, Set_B_Tiles_*)에 직접 붙은 것,
//   그리고 월드 윗면이 0.1±0.005, 월드 높이가 0.2±0.005인 것. 위쪽 축이 월드 위와 정렬(up.y > 0.999)되지 않은 것은 건드리지 않고 센다.
// 사용: -executeMethod PhysicsBench.EditorTools.PhysicsBenchVariantCurbExt.ApplyAndSave -benchVariantRecord <새 json>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchVariantCurbExt
    {
        public const float NEW_BOTTOM = -0.3f;

        [Serializable]
        public class CurbRecord
        {
            public string variant = "S3c";
            public PhysicsBenchVariants.Record baseRecord;
            public int candidates;
            public int extended;
            public int skippedNotAxisAligned;
            public int skippedShape;
            public float newBottomY = NEW_BOTTOM;
            public string appliedUtc;
        }

        public static CurbRecord Apply()
        {
            var rec = new CurbRecord { baseRecord = PhysicsBenchVariants.Apply("S3"), appliedUtc = DateTime.UtcNow.ToString("O") };
            var floors = new HashSet<GameObject>(PhysicsBenchVariants.FloorObjects());
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
                float sy = b.transform.lossyScale.y;
                float addWorld = bounds.min.y - NEW_BOTTOM; // 0.2
                var size = b.size;
                var center = b.center;
                size.y += addWorld / Mathf.Abs(sy);
                center.y -= (addWorld / 2f) / sy;
                b.size = size;
                b.center = center;
                rec.extended++;
            }
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
                Debug.Log("[PhysicsBenchVariantCurbExt] S3c candidates=" + rec.candidates + " extended=" + rec.extended + " skippedShape=" + rec.skippedShape + " skippedAxis=" + rec.skippedNotAxisAligned);
                code = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchVariantCurbExt] " + e);
            }
            EditorApplication.Exit(code);
        }
    }
}
