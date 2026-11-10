using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// 플레이 모드: 개미를 연석 앞에 옮기고 입력 필드를 직접 설정해 2초 돌진, 위치를 파일로 기록.
// args: {"id","x","y","z","yaw","sprint":bool,"boost":float,"secs":float,"wallX","dirX","out"}
public class CurbDash
{
    public static string Execute(JObject a)
    {
        if (!EditorApplication.isPlaying) return "not playing";
        var pmType = Type.GetType("PlayerMovement, Assembly-CSharp");
        var pm = UnityEngine.Object.FindFirstObjectByType(pmType) as MonoBehaviour;
        if (pm == null) return "PlayerMovement not found";
        var rb = pm.GetComponent<Rigidbody>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var start = new Vector3((float)a["x"], (float)a["y"], (float)a["z"]);
        float yaw = (float)a["yaw"];
        rb.linearVelocity = Vector3.zero;
        rb.position = start;
        pm.transform.position = start;
        pm.transform.rotation = Quaternion.Euler(0, yaw, 0);
        rb.rotation = pm.transform.rotation;
        pmType.GetProperty("Yaw", flags).SetValue(pm, yaw);
        pmType.GetField("_inputDir", flags).SetValue(pm, Vector2.zero);
        pmType.GetField("_isSprint", flags).SetValue(pm, false);
        Physics.SyncTransforms();
        string id = (string)a["id"];
        string outPath = (string)a["out"];
        float secs = (float)a["secs"];
        bool sprint = (bool)a["sprint"];
        float boost = (float)a["boost"];
        float wallX = (float)a["wallX"];
        float dirX = (float)a["dirX"];
        var sb = new StringBuilder();
        sb.AppendLine($"# {id} timeScale={Time.timeScale} fixedDt={Time.fixedDeltaTime} start={start} yaw={yaw}");
        double t0 = EditorApplication.timeSinceStartup;
        bool started = false;
        float minY = 99, maxBeyond = -99, maxY = -99;
        EditorApplication.CallbackFunction cb = null;
        cb = () =>
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            if (!started && t > 0.4)
            {
                started = true;
                pmType.GetField("_inputDir", flags).SetValue(pm, new Vector2(0, 1));
                pmType.GetField("_isSprint", flags).SetValue(pm, sprint);
                if (boost > 0) pmType.GetMethod("SetSpeedBoost").Invoke(pm, new object[] { boost, secs });
            }
            var p = rb.position;
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            float beyond = (p.x - wallX) * dirX;
            maxBeyond = Mathf.Max(maxBeyond, beyond);
            sb.AppendLine($"{t:F2} p=({p.x:F3},{p.y:F4},{p.z:F3}) v=({rb.linearVelocity.x:F2},{rb.linearVelocity.y:F2},{rb.linearVelocity.z:F2}) beyond={beyond:F3}");
            if (t > 0.4 + secs)
            {
                pmType.GetField("_inputDir", flags).SetValue(pm, Vector2.zero);
                pmType.GetField("_isSprint", flags).SetValue(pm, false);
                sb.AppendLine($"# END final=({p.x:F3},{p.y:F4},{p.z:F3}) beyondFinal={beyond:F3} maxBeyond={maxBeyond:F3} minY={minY:F4} maxY={maxY:F4}");
                File.WriteAllText(outPath, sb.ToString());
                EditorApplication.update -= cb;
            }
        };
        EditorApplication.update += cb;
        return $"started {id} at {start}, timeScale={Time.timeScale}";
    }
}
