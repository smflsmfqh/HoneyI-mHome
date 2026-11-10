// M10 보조: Zone/ 하위 콜라이더 구성 출력(읽기 전용, 씬 저장 없음)
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchZoneDump
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string p = PhysicsBenchVariants.PathOf(c.transform);
                if (!p.StartsWith("Zone/")) continue;
                var comps = string.Join(",", c.GetComponents<Component>().Select(x => x == null ? "null" : x.GetType().Name));
                var b = c.bounds;
                sb.AppendLine($"{p}|{c.GetType().Name}|trig={c.isTrigger}|en={c.enabled}|active={c.gameObject.activeInHierarchy}|layer={LayerMask.LayerToName(c.gameObject.layer)}|y={b.min.y:F3}..{b.max.y:F3}|comps={comps}");
            }
            File.WriteAllText(PhysicsBenchVariants.Arg("-benchOut"), sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
