#if PHYSICS_BENCH
// 판정기 검증용 합성 지형. 맵 아래 100m 지점에 Box만으로 만든다. 원점 가까이 두어 float 정밀도를 유지한다.
// 결과는 판정기·입력 경로 검증에만 쓰며 원래 버그의 개선 증거로 쓰지 않는다.
using System.Collections.Generic;
using UnityEngine;

namespace PhysicsBench
{
    public static class PhysicsBenchFixtures
    {
        public static readonly Vector3 ORIGIN = new(0f, -100f, 0f);
        private static readonly Vector3 AREA_HALF_EXTENTS = new(3f, 1f, 3f);
        private const string GROUND_TAG = "Ground";

        // 생성한 fixture 루트를 돌려준다. 이름을 모르면 null.
        public static GameObject Build(string fixture, List<string> notes)
        {
            // 기존 맵 충돌체가 합성 지형 영역에 있으면 결과가 오염되므로 만들지 않는다.
            if (Physics.OverlapBox(ORIGIN, AREA_HALF_EXTENTS, Quaternion.identity, ~0, QueryTriggerInteraction.Collide).Length > 0)
            {
                notes.Add("fixture_area_occupied");
                return null;
            }
            var root = new GameObject("PhysicsBenchFixture_" + fixture);
            root.transform.position = ORIGIN;
            switch (fixture)
            {
                case "flat":
                    Box(root, "floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
                    break;
                case "hole":
                    // z 0~0.3 구간에 바닥이 없다. 아래로 합법적인 지지면 없음.
                    Box(root, "floor_near", new Vector3(0f, -0.05f, -2f), new Vector3(8f, 0.1f, 4f));
                    Box(root, "floor_far", new Vector3(0f, -0.05f, 2.3f), new Vector3(8f, 0.1f, 4f));
                    break;
                case "stepdown":
                    // z=0에서 2cm 낮은 바닥으로 내려간다. 정상 단차.
                    Box(root, "upper", new Vector3(0f, -0.05f, -2f), new Vector3(8f, 0.1f, 4f));
                    Box(root, "lower", new Vector3(0f, -0.07f, 2f), new Vector3(8f, 0.1f, 4f));
                    break;
                case "curb_mesh":
                {
                    // 바닥과 연석을 한 MeshCollider로 합친 사례. 한 collider에 바닥·벽 접점이 동시에 생긴다(H1 도로 메시와 같은 구조).
                    var go = new GameObject("floor_and_curb");
                    go.transform.SetParent(root.transform, false);
                    go.tag = GROUND_TAG;
                    var mesh = new Mesh { name = "PhysicsBenchFloorCurb" };
                    var verts = new List<Vector3>();
                    var tris = new List<int>();
                    AddBox(verts, tris, new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
                    AddBox(verts, tris, new Vector3(0f, 0.015f, 0.15f), new Vector3(8f, 0.03f, 0.2f));
                    mesh.SetVertices(verts);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    int layer = LayerMask.NameToLayer("Ground");
                    if (layer >= 0)
                        go.layer = layer;
                    break;
                }
                case "curb":
                    Box(root, "floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
                    Box(root, "curb", new Vector3(0f, 0.015f, 0.15f), new Vector3(8f, 0.03f, 0.2f));
                    break;
                case "curb_trigger":
                    // 연석을 Trigger로 둔 통과 사례. 분석기의 벽면 교차 판정 검증용.
                    Box(root, "floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
                    Box(root, "curb", new Vector3(0f, 0.015f, 0.15f), new Vector3(8f, 0.03f, 0.2f)).GetComponent<Collider>().isTrigger = true;
                    break;
                case "trap":
                {
                    // 개미 지름(약 1.9cm)보다 조금 큰 3cm 상자 안에 가둔다. 지붕까지 있어 어떤 이동으로도 못 나간다.
                    const float half = 0.015f;
                    const float wall = 0.02f;
                    const float h = 0.05f;
                    Box(root, "floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
                    Box(root, "wall_n", new Vector3(0f, h * 0.5f, half + wall * 0.5f), new Vector3(0.1f, h, wall), false);
                    Box(root, "wall_s", new Vector3(0f, h * 0.5f, -half - wall * 0.5f), new Vector3(0.1f, h, wall), false);
                    Box(root, "wall_e", new Vector3(half + wall * 0.5f, h * 0.5f, 0f), new Vector3(wall, h, 0.1f), false);
                    Box(root, "wall_w", new Vector3(-half - wall * 0.5f, h * 0.5f, 0f), new Vector3(wall, h, 0.1f), false);
                    Box(root, "roof", new Vector3(0f, h + wall * 0.5f, 0f), new Vector3(0.1f, wall, 0.1f), false);
                    break;
                }
                case "penetration":
                    // 벽면 z=0. 케이스 시작점이 벽 안쪽으로 겹쳐 있어 물리 엔진이 밀어내야 한다.
                    Box(root, "floor", new Vector3(0f, -0.05f, 0f), new Vector3(8f, 0.1f, 8f));
                    Box(root, "wall", new Vector3(0f, 0.025f, 0.1f), new Vector3(8f, 0.05f, 0.2f), false);
                    break;
                case "void":
                    // 지지면이 전혀 없다. 정착 중 낙하도 물리 실패로 남는지 확인한다.
                    break;
                default:
                    Object.Destroy(root);
                    notes.Add("unknown_fixture:" + fixture);
                    return null;
            }
            foreach (var c in root.GetComponentsInChildren<Collider>())
                notes.Add("fixture_collider:" + c.name + ":" + c.GetType().Name + ":layer=" + LayerMask.LayerToName(c.gameObject.layer) + ":tag=" + c.tag + (c.isTrigger ? ":trigger" : ""));
            return root;
        }

        // 축 정렬 상자의 바깥면 삼각형 12개를 추가한다.
        private static void AddBox(List<Vector3> verts, List<int> tris, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            int b = verts.Count;
            for (int i = 0; i < 8; i++)
                verts.Add(center + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
            int[] faces =
            {
                0, 4, 6, 0, 6, 2, // -x
                1, 3, 7, 1, 7, 5, // +x
                0, 1, 5, 0, 5, 4, // -y
                2, 6, 7, 2, 7, 3, // +y
                0, 2, 3, 0, 3, 1, // -z
                4, 5, 7, 4, 7, 6, // +z
            };
            foreach (int f in faces)
                tris.Add(b + f);
        }

        private static GameObject Box(GameObject root, string name, Vector3 localCenter, Vector3 size, bool groundTag = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            if (groundTag)
                go.tag = GROUND_TAG;
            // 캡처 확인용 표시 재질. 충돌에는 영향이 없다.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = groundTag ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.85f, 0.45f, 0.2f);
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            // 실제 맵 바닥과 같은 Ground 레이어(있을 때만). 반환 전에 적용해야 한다.
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                go.layer = groundLayer;
            return go;
        }
    }
}
#endif
