// M10 사후 진단: 지정 위치에 개미 크기 구(r=0.0094, 중심 y=-0.0906)를 두고 겹치는 비트리거 콜라이더 출력(읽기 전용, 씬 저장 없음)
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchPointProbe
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            const float R = 0.0094f;
            foreach (var line in File.ReadAllLines(PhysicsBenchVariants.Arg("-benchIn")))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var f = line.Split('|');
                var c = new Vector3(float.Parse(f[1]), -0.1f + R, float.Parse(f[2]));
                sb.AppendLine($"== {f[0]} @ {c.ToString("F4")}");
                foreach (var col in Physics.OverlapSphere(c, R, ~0, QueryTriggerInteraction.Ignore))
                {
                    var cp = col.ClosestPoint(c);
                    bool inside = (cp - c).sqrMagnitude < 1e-12f;
                    string depth = "";
                    if (col is BoxCollider bc)
                    {
                        var lp = bc.transform.InverseTransformPoint(c) - bc.center;
                        var h = bc.size * 0.5f;
                        depth = $"|localFromFaces x={h.x - Mathf.Abs(lp.x):F4} y={h.y - Mathf.Abs(lp.y):F4} z={h.z - Mathf.Abs(lp.z):F4}";
                    }
                    sb.AppendLine($"{PhysicsBenchVariants.PathOf(col.transform)}|{col.GetType().Name}|layer={LayerMask.LayerToName(col.gameObject.layer)}|centerInside={inside}|dist={(cp - c).magnitude:F4}{depth}");
                }
            }
            File.WriteAllText(PhysicsBenchVariants.Arg("-benchOut"), sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
