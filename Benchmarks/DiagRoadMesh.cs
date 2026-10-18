using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DiagRoadMesh
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        GameObject road = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID))
            if (go.name == "road_009" && go.GetComponents<BoxCollider>().Length > 0) { road = go; break; }
        if (road == null) return "no road_009";
        var tr = road.transform;
        sb.AppendLine($"road_009 pos {tr.position} rot {tr.eulerAngles} lossy {tr.lossyScale}");
        foreach (var b in road.GetComponents<BoxCollider>())
            sb.AppendLine($"  box center {b.center} size {b.size} world bounds {b.bounds.min} ~ {b.bounds.max}");
        var mesh = road.GetComponent<MeshFilter>().sharedMesh;
        sb.AppendLine($"  mesh {mesh.name} bounds {mesh.bounds} readable {mesh.isReadable}");

        var scene = EditorSceneManager.NewPreviewScene();
        var copy = new GameObject("m");
        SceneManager.MoveGameObjectToScene(copy, scene);
        copy.transform.SetPositionAndRotation(tr.position, tr.rotation);
        copy.transform.localScale = tr.lossyScale;
        var mc = copy.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        Physics.SyncTransforms();
        var ps = scene.GetPhysicsScene();

        // 첫 번째 연석 박스를 가로지르는 수평선 위에서 높이별로 메시와 부딪히는지
        var box = road.GetComponents<BoxCollider>()[0];
        Vector3 c = box.bounds.center;
        Vector3 axis = box.bounds.size.x < box.bounds.size.z ? Vector3.right : Vector3.forward;
        // 위에서 아래로: 연석 양쪽 바닥 높이
        foreach (float off in new[] { -0.6f, -0.3f, 0.3f, 0.6f })
        {
            Vector3 o = c + axis * off + Vector3.up * 2f;
            bool hm = ps.Raycast(o, Vector3.down, out var h, 5f);
            bool hs = Physics.Raycast(o, Vector3.down, out var h2, 5f);
            sb.AppendLine($"  down@{off:+0.0;-0.0}: mesh {(hm ? h.point.y.ToString("0.000") : "none")}  scene {(hs ? h2.point.y.ToString("0.000") + " " + h2.collider.name : "none")}");
        }
        foreach (float y in new[] { 0.0f, 0.005f, 0.011f, 0.02f, 0.05f, 0.09f })
        {
            Vector3 o1 = new Vector3(c.x, y, c.z) - axis * 1.0f;
            Vector3 o2 = new Vector3(c.x, y, c.z) + axis * 1.0f;
            bool a = ps.Raycast(o1, axis, out var h1, 2f);
            bool b2 = ps.Raycast(o2, -axis, out var h2, 2f);
            sb.AppendLine($"  y={y:0.000}: mesh hit from -side {(a ? h1.point.ToString("F3") : "none")}  from +side {(b2 ? h2.point.ToString("F3") : "none")}");
        }
        EditorSceneManager.ClosePreviewScene(scene);
        return sb.ToString();
    }
}
