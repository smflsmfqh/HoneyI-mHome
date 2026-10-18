using System.Linq;
using System.Text;
using UnityEngine;
public static class DiagGap
{
    public static string Execute()
    {
        var p = Object.FindFirstObjectByType<PlayerMovement>();
        int m = 0; for (int i = 0; i < 32; i++) if (!Physics.GetIgnoreLayerCollision(p.gameObject.layer, i)) m |= 1 << i;
        var sb = new StringBuilder();
        foreach (float x in new[] { -48.9f, -48.95f, -49.0f, -49.05f, -49.1f, -49.2f, -49.5f, -50f, -52.5f })
        {
            var hs = Physics.RaycastAll(new Vector3(x, 2f, 52.5f), Vector3.down, 5f, m, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance)
                .Select(h => $"{h.collider.name}@{h.point.y:F3}{(h.collider.isTrigger ? "(T)" : "")}");
            sb.AppendLine($"x={x}: " + string.Join(", ", hs));
        }
        var f = GameObject.Find("PostProcessingVolume/Demonstration/BasicSettingsMap/road_009");
        foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.name.StartsWith("road_009_floor")))
            if (Vector3.Distance(c.bounds.center, new Vector3(-52.5f, 0, 52.5f)) < 10f) sb.AppendLine($"floor {c.name} bounds {c.bounds.min:F3}~{c.bounds.max:F3}");
        return sb.ToString();
    }
}
