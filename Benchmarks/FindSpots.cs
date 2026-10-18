using System.Linq;
using System.Text;
using UnityEngine;

public static class FindSpots
{
    static int Mask()
    {
        var p = Object.FindFirstObjectByType<PlayerMovement>();
        int m = 0; for (int i = 0; i < 32; i++) if (!Physics.GetIgnoreLayerCollision(p.gameObject.layer, i)) m |= 1 << i;
        return m;
    }

    public static string Execute()
    {
        int mask = Mask();
        var sb = new StringBuilder();
        string[] paths = {
            "PostProcessingVolume/Store1/Building_Bakery",
            "PostProcessingVolume/Store2/Building_Super Market",
            "PostProcessingVolume/Store2/Building_Coffee Shop",
            "PostProcessingVolume/Demonstration/BasicSettingsMap/road_009",
            "PostProcessingVolume/Demonstration/BasicSettingsMap/road_013/CurbColliders/Curb_7",
            "PostProcessingVolume/Demonstration/BasicSettingsMap/road_019/CurbColliders/Curb_18",
        };
        foreach (var path in paths)
        {
            var go = GameObject.Find(path);
            if (go == null) { sb.AppendLine($"{path}: 없음"); continue; }
            foreach (var col in go.GetComponents<Collider>().Where(c => c.enabled && !c.isTrigger))
            {
                Bounds b = col.bounds;
                sb.AppendLine($"{path} [{col.GetType().Name}] bounds {b.min:F2}~{b.max:F2} layer {LayerMask.LayerToName(go.layer)}");
                int found = 0;
                for (int k = 0; k < 24 && found < 4; k++)
                {
                    float a = k * Mathf.PI * 2f / 24f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    // 벽 바깥 지점들을 바깥→안으로 훑으며 바닥이 있는 첫 위치
                    for (float dist = b.extents.magnitude + 0.5f; dist > 0.05f; dist -= 0.05f)
                    {
                        Vector3 o = new Vector3(b.center.x, 0, b.center.z) + d * dist;
                        var gh = Physics.RaycastAll(new Vector3(o.x, b.max.y + 2f, o.z), Vector3.down, b.size.y + 6f, mask, QueryTriggerInteraction.Ignore)
                            .Where(h => h.collider != col && h.collider.attachedRigidbody == null && h.point.y < b.max.y)
                            .OrderBy(h => h.distance).ToList();
                        if (gh.Count == 0) continue;
                        var g = gh[0];
                        if (g.collider.name.StartsWith("Zone")) break;
                        float cy = g.point.y + 0.011f;
                        if (cy > b.max.y - 0.02f || cy < b.min.y) continue;
                        Vector3 start = new Vector3(o.x, cy, o.z);
                        if (col.Raycast(new Ray(start, -d), out var wh, 0.3f) && Mathf.Abs(wh.normal.y) < 0.3f)
                        {
                            sb.AppendLine($"   후보 p={wh.point:F3} n={wh.normal:F2} ground={g.collider.name}@{g.point.y:F3} 거리{wh.distance:F3}");
                            found++;
                            break;
                        }
                    }
                }
            }
        }
        return sb.ToString();
    }
}
