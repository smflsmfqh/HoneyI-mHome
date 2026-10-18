using UnityEngine;

// 재현 지점 측정 진입점(coplay execute_script). Assets 밖에 두어 빌드에 포함되지 않는다.
// 위치 = 개미가 서 있을 곳(벽에서 약 25cm), 방향 = 벽 쪽.
public static class RunTunnelingSpots
{
    public static string Execute() => TunnelingBenchmark.RunSpots(new (string, Vector3, Vector3)[]
    {
        // 차도 → 연석 (차도 바닥 y=-0.1 / 0.0, 연석 BoxCollider 높이 -0.1~0.1)
        ("S1_road013_Curb7_차도쪽",  new Vector3(50.805f, -0.089f, -69.138f), new Vector3(0.51f, 0f, -0.86f)),
        ("S2_road009_연석_차도쪽",   new Vector3(-49.11f, 0.011f, 52.50f),    new Vector3(1f, 0f, 0f)),
        ("S3_road019_Curb18_차도쪽", new Vector3(-55.78f, 0.011f, 22.70f),    new Vector3(-1f, 0f, 0f)),
        // 인도 → 건물 벽 (Bakery MeshCollider)
        ("S4_Bakery_동쪽벽_A",       new Vector3(20.06f, 0.438f, 31.835f),    new Vector3(-1f, 0f, 0f)),
        ("S5_Bakery_동쪽벽_B",       new Vector3(20.06f, 0.438f, 33.041f),    new Vector3(-1f, 0f, 0f)),
    });
}
