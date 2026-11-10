// M10 사후 진단: 접합부 G07·G08 주변 콜라이더 기하 출력(읽기 전용, 씬 저장 없음, 결과를 본 뒤 만든 진단)
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchJointDump
    {
        static readonly Vector3[] PTS = { new Vector3(-30f, 0f, 28.6348f), new Vector3(-13.6338f, 0f, 45f), new Vector3(-22.5f, 0f, 28.6348f) };
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
            foreach (var pt in PTS)
            {
                sb.AppendLine($"== point {pt.x:F4},{pt.z:F4}");
                foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    var b = c.bounds;
                    if (b.max.y < -0.12f || b.min.y > 0.3f) continue;
                    if (pt.x < b.min.x - 0.3f || pt.x > b.max.x + 0.3f || pt.z < b.min.z - 0.3f || pt.z > b.max.z + 0.3f) continue;
                    string extra = "";
                    if (c is BoxCollider bc)
                    {
                        var t = c.transform;
                        extra = $"|center={bc.center.ToString("F4")}|size={bc.size.ToString("F4")}|rot={t.rotation.eulerAngles.ToString("F3")}|lossy={t.lossyScale.ToString("F4")}";
                    }
                    sb.AppendLine($"{PhysicsBenchVariants.PathOf(c.transform)}|{c.GetType().Name}|id={c.GetInstanceID()}|trig={c.isTrigger}|layer={LayerMask.LayerToName(c.gameObject.layer)}|min={b.min.ToString("F4")}|max={b.max.ToString("F4")}{extra}");
                }
            }
            File.WriteAllText(PhysicsBenchVariants.Arg("-benchOut"), sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
