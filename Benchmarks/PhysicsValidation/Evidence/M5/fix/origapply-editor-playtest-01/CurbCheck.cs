using System.Linq;
using UnityEngine;

public class CurbCheck
{
    // 편집 모드: 연석 BoxCollider 모양 집계 (씬 변경 없음)
    public static string Execute()
    {
        int extended = 0, oldShape = 0, other = 0;
        foreach (var b in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!b.enabled || b.isTrigger) continue;
            var t = b.transform; string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            bool floor = b.gameObject.name.StartsWith("road_") || b.gameObject.name.StartsWith("Set_B_Tiles_");
            if (!(p.Contains("/CurbColliders/") || floor)) continue;
            var bo = b.bounds;
            if (Mathf.Abs(bo.max.y - 0.1f) <= 0.005f && Mathf.Abs(bo.min.y + 0.3f) <= 0.005f) extended++;
            else if (Mathf.Abs(bo.max.y - 0.1f) <= 0.005f && Mathf.Abs(bo.size.y - 0.2f) <= 0.005f) oldShape++;
            else other++;
        }
        return $"extended(top 0.1, bottom -0.3)={extended} oldShape(top 0.1, h 0.2)={oldShape} otherCurbOrFloorBoxes={other}";
    }
}
