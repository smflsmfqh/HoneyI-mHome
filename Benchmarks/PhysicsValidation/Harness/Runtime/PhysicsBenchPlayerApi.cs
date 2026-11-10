#if PHYSICS_BENCH
// 버전별 플레이어 관측·준비 어댑터. 실행기는 PlayerMovement 공개 API를 직접 부르지 않고 이 클래스를 거친다.
// - H1~H7: 공통 공개 API(InputDir, IsGrounded, OnRollActive, SetSpeedBoost, PlayerHealth.SetInvincible)
// - H0(PHYSICS_BENCH_H0): 초기 프로토타입. 이동 입력·접지 판정이 private이라 관측 전용으로 리플렉션으로 읽는다.
//   부스트·구르기·체력이 없으므로 해당 기능은 미지원으로 돌려준다. 게임 상태를 바꾸는 리플렉션은 쓰지 않는다.
// - Hm1(PHYSICS_BENCH_HM1, 문서상 H-1): H0 직전. MovePosition 이동만 있고 Look·Yaw·점프·접지 판정이 없다.
//   플레이어 회전은 FollowCamera.LateUpdate가 매 프레임 카메라 _yaw로 덮어쓰고, _yaw는 legacy Input("Mouse X")로만
//   바뀌어 가상 장치로 줄 수 없다. 그래서 준비 단계에서만 FollowCamera._yaw를 case 방향으로 쓴다(다른 버전의 Look 정렬과
//   같은 역할, 이동·충돌 로직은 건드리지 않음). 그 밖에는 이동 입력만 읽는다.
using System;
using System.Reflection;
using UnityEngine;

namespace PhysicsBench
{
    public static class PhysicsBenchPlayerApi
    {
#if PHYSICS_BENCH_HM1
        public const string ADAPTER = "Hm1-reflection-read-only";
        public const bool HAS_LOOK = false;
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo INPUT_DIR = typeof(PlayerMovement).GetField("_inputDir", PRIVATE);

        public static Vector2 InputDir(PlayerMovement p) => INPUT_DIR != null ? (Vector2)INPUT_DIR.GetValue(p) : Vector2.zero;

        public static float Yaw(PlayerMovement p) => p.transform.eulerAngles.y;

        private static readonly FieldInfo CAMERA_YAW = typeof(FollowCamera).GetField("_yaw", PRIVATE);

        // 준비 전용 게임 상태 쓰기. 성공 여부를 돌려준다.
        public static bool PrepareYaw(PlayerMovement p, float yaw)
        {
            var cam = UnityEngine.Object.FindFirstObjectByType<FollowCamera>();
            if (cam == null || CAMERA_YAW == null)
                return false;
            CAMERA_YAW.SetValue(cam, yaw);
            return true;
        }

        // 이 버전에는 접지 판정 자체가 없다. 판정은 분석기가 접촉·발 아래 검사로 한다.
        public static bool IsGrounded(PlayerMovement p) => false;

        public static void SubscribeRoll(PlayerMovement p, Action<bool> handler) { }

        public static void UnsubscribeRoll(PlayerMovement p, Action<bool> handler) { }

        public static bool SetSpeedBoost(PlayerMovement p, float speed, float sec) => false;

        public static bool SetInvincible(PlayerMovement p, float sec) => false;

        public static string Check() => INPUT_DIR == null ? "hm1_adapter_member_missing" : null;
#elif PHYSICS_BENCH_H0
        public const string ADAPTER = "H0-reflection-read-only";
        public const bool HAS_LOOK = true;
        private const BindingFlags PRIVATE = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo INPUT_DIR = typeof(PlayerMovement).GetField("_inputDir", PRIVATE);
        private static readonly MethodInfo IS_GROUNDED = typeof(PlayerMovement).GetMethod("IsGrounded", PRIVATE);

        public static Vector2 InputDir(PlayerMovement p) => INPUT_DIR != null ? (Vector2)INPUT_DIR.GetValue(p) : Vector2.zero;

        public static float Yaw(PlayerMovement p) => p.Yaw;

        public static bool PrepareYaw(PlayerMovement p, float yaw) => false;

        // H0의 IsGrounded는 Raycast만 하는 순수 조회다.
        public static bool IsGrounded(PlayerMovement p) => IS_GROUNDED != null && (bool)IS_GROUNDED.Invoke(p, null);

        public static void SubscribeRoll(PlayerMovement p, Action<bool> handler) { }

        public static void UnsubscribeRoll(PlayerMovement p, Action<bool> handler) { }

        public static bool SetSpeedBoost(PlayerMovement p, float speed, float sec) => false;

        public static bool SetInvincible(PlayerMovement p, float sec) => false;

        public static string Check() => INPUT_DIR == null || IS_GROUNDED == null ? "h0_adapter_member_missing" : null;
#else
        public const string ADAPTER = "public-api";
        public const bool HAS_LOOK = true;

        public static Vector2 InputDir(PlayerMovement p) => p.InputDir;

        public static float Yaw(PlayerMovement p) => p.Yaw;

        public static bool PrepareYaw(PlayerMovement p, float yaw) => false;

        public static bool IsGrounded(PlayerMovement p) => p.IsGrounded;

        public static void SubscribeRoll(PlayerMovement p, Action<bool> handler) => p.OnRollActive += handler;

        public static void UnsubscribeRoll(PlayerMovement p, Action<bool> handler) => p.OnRollActive -= handler;

        public static bool SetSpeedBoost(PlayerMovement p, float speed, float sec)
        {
            p.SetSpeedBoost(speed, sec);
            return true;
        }

        public static bool SetInvincible(PlayerMovement p, float sec)
        {
            var health = p.GetComponent<PlayerHealth>();
            if (health == null)
                return false;
            health.SetInvincible(sec, "physics_bench");
            return true;
        }

        public static string Check() => null;
#endif
    }
}
#endif
