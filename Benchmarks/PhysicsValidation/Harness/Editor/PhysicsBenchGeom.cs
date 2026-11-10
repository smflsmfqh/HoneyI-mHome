// 트랙 B 기하 감사. 씬을 열고 변형을 메모리에서만 적용한 뒤(저장 안 함) 단면을 따라
// 시각 바닥 메시 윗면 높이와 물리 충돌면 윗면 높이(Physics.RaycastAll, 트리거·Rigidbody 제외)를 기록한다.
// 에디터에서는 메시를 항상 읽을 수 있으므로 Mesh 변형의 충돌면은 "의도한 모양"이다(빌드에서 비는지는 런타임 실험이 판단).
//
// 사용: -executeMethod PhysicsBench.EditorTools.PhysicsBenchGeom.Run -geomVariant S2 -geomLines <파일> -geomOut <새 json>
//   선 파일: 한 줄에 "id,x0,z0,x1,z1,step,lateral" (lateral = 좌우로 더 잴 거리, 0이면 중심선만)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PhysicsBench.EditorTools
{
    public static class PhysicsBenchGeom
    {
        private const float RAY_TOP = 3f;
        private const float RAY_LEN = 6f;

        public static void Run()
        {
            int code = 1;
            try
            {
                code = RunInternal();
            }
            catch (Exception e)
            {
                Debug.LogError("[PhysicsBenchGeom] " + e);
            }
            EditorApplication.Exit(code);
        }

        private class Tri
        {
            public Vector3 a, b, c;
            public float minX, maxX, minZ, maxZ;
        }

        private static int RunInternal()
        {
            string variant = PhysicsBenchVariants.Arg("-geomVariant") ?? throw new Exception("missing -geomVariant");
            string linesPath = PhysicsBenchVariants.Arg("-geomLines") ?? throw new Exception("missing -geomLines");
            string outPath = PhysicsBenchVariants.Arg("-geomOut") ?? throw new Exception("missing -geomOut");
            if (File.Exists(outPath))
                throw new Exception("output exists, refusing to overwrite: " + outPath);
            EditorSceneManager.OpenScene(PhysicsBenchVariants.SCENE, OpenSceneMode.Single);
            var rec = PhysicsBenchVariants.Apply(variant);
            Physics.SyncTransforms();

            // 시각 기준: 바닥 오브젝트(road_*, Set_B_Tiles_*)의 메시 삼각형만 쓴다(소품 윗면 제외)
            var tris = new List<Tri>();
            foreach (var g in PhysicsBenchVariants.FloorObjects())
            {
                var mf = g.GetComponent<MeshFilter>();
                var mesh = mf.sharedMesh;
                var v = mesh.vertices;
                var t = mesh.triangles;
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var tr = new Tri { a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]) };
                    tr.minX = Mathf.Min(tr.a.x, tr.b.x, tr.c.x); tr.maxX = Mathf.Max(tr.a.x, tr.b.x, tr.c.x);
                    tr.minZ = Mathf.Min(tr.a.z, tr.b.z, tr.c.z); tr.maxZ = Mathf.Max(tr.a.z, tr.b.z, tr.c.z);
                    tris.Add(tr);
                }
            }

            var grid = new Dictionary<long, List<Tri>>();
            foreach (var tr in tris)
                for (int gx = Mathf.FloorToInt(tr.minX); gx <= Mathf.FloorToInt(tr.maxX); gx++)
                    for (int gz = Mathf.FloorToInt(tr.minZ); gz <= Mathf.FloorToInt(tr.maxZ); gz++)
                    {
                        long key = ((long)gx << 32) ^ (uint)gz;
                        if (!grid.TryGetValue(key, out var list))
                            grid[key] = list = new List<Tri>();
                        list.Add(tr);
                    }

            var report = new GeomReport { variant = variant, record = rec, unityVersion = Application.unityVersion, lines = new List<LineResult>() };
            var colliderNames = new List<string>();
            var colliderIndex = new Dictionary<Collider, int>();
            foreach (var raw in File.ReadAllLines(linesPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                var f = line.Split(',');
                string id = f[0];
                float x0 = P(f[1]), z0 = P(f[2]), x1 = P(f[3]), z1 = P(f[4]), step = P(f[5]), lateral = P(f[6]);
                var dir = new Vector2(x1 - x0, z1 - z0);
                float len = dir.magnitude;
                dir /= len;
                var side = new Vector2(-dir.y, dir.x);
                var offsets = lateral > 0 ? new[] { -lateral, 0f, lateral } : new[] { 0f };
                foreach (float off in offsets)
                {
                    var lr = new LineResult { id = id, offset = off, step = step, along = new List<float>(), visualY = new List<float>(), colY = new List<float>(), colNormalY = new List<float>(), colIdx = new List<int>() };
                    int n = Mathf.FloorToInt(len / step + 1e-4f);
                    for (int i = 0; i <= n; i++)
                    {
                        float s = i * step;
                        float x = x0 + dir.x * s + side.x * off;
                        float z = z0 + dir.y * s + side.y * off;
                        lr.along.Add(s);
                        lr.visualY.Add(VisualTop(grid, x, z));
                        var hits = Physics.RaycastAll(new Vector3(x, RAY_TOP, z), Vector3.down, RAY_LEN, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => h.collider.attachedRigidbody == null)
                            .OrderBy(h => h.distance).ToArray();
                        if (hits.Length == 0)
                        {
                            lr.colY.Add(float.NaN);
                            lr.colNormalY.Add(float.NaN);
                            lr.colIdx.Add(-1);
                            continue;
                        }
                        var h0 = hits[0];
                        if (!colliderIndex.TryGetValue(h0.collider, out int ci))
                        {
                            ci = colliderNames.Count;
                            colliderIndex[h0.collider] = ci;
                            var mc = h0.collider as MeshCollider;
                            colliderNames.Add(PhysicsBenchVariants.PathOf(h0.collider.transform) + "|" + h0.collider.GetType().Name + (mc != null && mc.convex ? "|convex" : ""));
                        }
                        lr.colY.Add(h0.point.y);
                        lr.colNormalY.Add(h0.normal.y);
                        lr.colIdx.Add(ci);
                    }
                    report.lines.Add(lr);
                }
            }
            report.colliders = colliderNames.ToArray();
            File.WriteAllText(outPath, JsonUtility.ToJson(report, false).Replace("NaN", "null"));
            Debug.Log("[PhysicsBenchGeom] wrote " + outPath + " lines=" + report.lines.Count);
            return 0;
        }

        // 위쪽을 향한 바닥 삼각형 중 가장 높은 교차점. 없으면 NaN
        private static float VisualTop(Dictionary<long, List<Tri>> grid, float x, float z)
        {
            float best = float.NaN;
            long key = ((long)Mathf.FloorToInt(x) << 32) ^ (uint)Mathf.FloorToInt(z);
            if (!grid.TryGetValue(key, out var cell))
                return best;
            foreach (var t in cell)
            {
                if (x < t.minX || x > t.maxX || z < t.minZ || z > t.maxZ)
                    continue;
                var nrm = Vector3.Cross(t.b - t.a, t.c - t.a);
                if (nrm.y < 0) nrm = -nrm;
                nrm.Normalize();
                if (nrm.y < 0.5f)
                    continue;
                float d = (t.b.z - t.c.z) * (t.a.x - t.c.x) + (t.c.x - t.b.x) * (t.a.z - t.c.z);
                if (Mathf.Abs(d) < 1e-12f)
                    continue;
                float w1 = ((t.b.z - t.c.z) * (x - t.c.x) + (t.c.x - t.b.x) * (z - t.c.z)) / d;
                float w2 = ((t.c.z - t.a.z) * (x - t.c.x) + (t.a.x - t.c.x) * (z - t.c.z)) / d;
                float w3 = 1 - w1 - w2;
                if (w1 < -1e-6f || w2 < -1e-6f || w3 < -1e-6f)
                    continue;
                float y = w1 * t.a.y + w2 * t.b.y + w3 * t.c.y;
                if (float.IsNaN(best) || y > best)
                    best = y;
            }
            return best;
        }

        private static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        [Serializable]
        private class GeomReport
        {
            public string variant;
            public string unityVersion;
            public PhysicsBenchVariants.Record record;
            public string[] colliders;
            public List<LineResult> lines;
        }

        [Serializable]
        private class LineResult
        {
            public string id;
            public float offset;
            public float step;
            public List<float> along;
            public List<float> visualY;
            public List<float> colY;
            public List<float> colNormalY;
            public List<int> colIdx;
        }
    }
}
