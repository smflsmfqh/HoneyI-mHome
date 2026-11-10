using System.Text;
using UnityEditor;
using UnityEngine;

public class SelInspect
{
    static string PathOf(Transform t) { string p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; } return p; }

    // 선택 오브젝트(와 이름이 같은 계열 'fir tree group*')의 콜라이더·렌더러 정보 (씬 변경 없음)
    public static string Execute()
    {
        var sb = new StringBuilder();
        var roots = new System.Collections.Generic.List<GameObject>();
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go.name.StartsWith("fir tree group")) roots.Add(go.gameObject);
        foreach (var r in roots)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(r);
            sb.AppendLine($"## {PathOf(r.transform)} active={r.activeInHierarchy} pos={r.transform.position} lossy={r.transform.lossyScale} rot={r.transform.eulerAngles} prefab={(src ? AssetDatabase.GetAssetPath(src) : "-")} layer={LayerMask.LayerToName(r.layer)}");
            foreach (var c in r.GetComponentsInChildren<Collider>(true))
            {
                var b = c.bounds;
                string extra = "";
                if (c is MeshCollider mc)
                    extra = $" cook={(int)mc.cookingOptions} convex={mc.convex} readable={(mc.sharedMesh && mc.sharedMesh.isReadable)} mesh={(mc.sharedMesh ? mc.sharedMesh.name + " v" + mc.sharedMesh.vertexCount : "null")}";
                if (c is BoxCollider bc) extra = $" size={bc.size} center={bc.center}";
                sb.AppendLine($"  C {PathOf(c.transform)} {c.GetType().Name}{extra} en={c.enabled} trig={c.isTrigger} min=({b.min.x:F2},{b.min.y:F3},{b.min.z:F2}) max=({b.max.x:F2},{b.max.y:F3},{b.max.z:F2}) lossy={c.transform.lossyScale} layer={LayerMask.LayerToName(c.gameObject.layer)}");
            }
            foreach (var mr in r.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = mr.bounds;
                var mf = mr.GetComponent<MeshFilter>();
                sb.AppendLine($"  R {PathOf(mr.transform)} mesh={(mf && mf.sharedMesh ? mf.sharedMesh.name : "-")} min=({b.min.x:F2},{b.min.y:F3},{b.min.z:F2}) max=({b.max.x:F2},{b.max.y:F3},{b.max.z:F2})");
            }
        }
        System.IO.File.WriteAllText("/private/tmp/claude-501/-Users-ihaneul-unityProject-HoneyI-mHome/f01218c4-aaea-459a-a9be-a6a44ee3e3e3/scratchpad/firtree.txt", sb.ToString());
        return "roots=" + roots.Count;
    }
}
