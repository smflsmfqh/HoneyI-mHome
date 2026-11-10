#if PHYSICS_BENCH
// 물리 검증 하네스 전용. PHYSICS_BENCH define이 있는 테스트 빌드에서만 컴파일된다.
// 런타임은 원시 사실만 기록하고, 성공/실패 판정은 Tools/analyze_physics_bench.py가 임계값 JSON으로 수행한다.
using System;
using UnityEngine;

namespace PhysicsBench
{
    [Serializable]
    public class BenchCaseFile
    {
        public string experimentId;
        public string caseSetRevision;
        public BenchCase[] cases;
    }

    [Serializable]
    public class BenchCase
    {
        public string id;
        // hold | walk | jump | curb
        public string scenario;
        // map: 실제 AntDemoScene 지형 / fixture: 맵 밖에 생성한 합성 판정 검증 지형
        public string environment = "map";
        public string fixture;
        public float[] start;            // 캡슐 바닥 기준 월드 좌표 x, y, z (모든 구성에서 동일)
        public float yaw;                // 진행 방향 yaw(도)
        // Move | Sprint | Boost | BoostSprint | Roll
        public string moveMode = "Move";
        public float boostSpeed = 3.5f;
        public float settleSec = 0.6f;
        public float moveSec = 1.0f;     // walk/jump: 이동 입력 유지 시간
        public float jumpAtSec = -1f;    // jump: 이동 시작 후 점프 입력 시각
        public float observeSec = 1.5f;  // 입력 해제 후 관찰 시간
        public float approachTimeoutSec = 3f; // curb: 최초 벽 접촉까지 제한 시간
        public float pushAfterContactSec = 1.0f;
        public float reverseSec = 1.0f;
        public float sideSec = 1.0f;
        public string sideOrder = "LR";  // LR | RL
        public bool rescueJump = true;
        public bool rescueRoll = true;
        public float rescueSec = 1.0f;
        public float minAllowedY;        // 경로상 합법적인 가장 낮은 지지면 높이
        // curb: 기준 형상의 벽면(플레이어 쪽을 향한 법선, 면 위 한 점, 윗면 높이). 비스듬한 접근에서도 같은 벽을 판정한다.
        public float[] wallNormal;
        public float[] wallPoint;
        public float wallTopY;
        public string sabotage;          // 판정기 검증용 의도적 하네스 결함: no_input | partial_input | time_scale_zero | hang | slow_load
        // 대상 벽 식별: 기준 벽면에서 이 거리(m) 안의 접점만 대상 벽 접촉으로 본다. 0이면 캡슐 반지름의 2배.
        public float wallPlaneTolerance;
        // 선택: 대상 벽 collider 경로 접두사. 비어 있으면 기하 기준만 쓴다.
        public string[] wallTargets;
        public string expect;            // 판정기 검증 사례의 기대 결과 (분석기가 비교)
        public string notes;
    }

    // 복원 버전별 차이(키 바인딩·통제 대상)를 코드 대신 데이터로 둔다.
    [Serializable]
    public class BenchProfile
    {
        public string versionId;
        public string sceneName = "AntDemoScene";
        public string controlScheme = "Keyboard&Mouse";
        public string actionMap = "Player";
        public string keyForward = "W";
        public string keyBack = "S";
        public string keyLeft = "A";
        public string keyRight = "D";
        public string keySprint = "LeftShift";
        public string keyJump = "Space";
        public string keyRoll = "R";
        // Start 전에 비활성화할 컴포넌트 타입 이름. 통제 목록으로 manifest에 기록된다.
        public string[] disableComponents;
        // 오브젝트째 비활성화할 컴포넌트 타입 이름(NPC 본체 등 충돌체를 가진 이동체).
        public string[] deactivateObjectsWith;
        // 통제 조건 전용: 계층 경로로 지정한 오브젝트를 Start 전에 비활성화한다(예: H1 받침판 제거 조건).
        // 역사 복원 실행의 profile에는 넣지 않는다.
        public string[] deactivatePaths;
        // 통제 조건 전용: 주 Rigidbody constraints를 이 값(RigidbodyConstraints 정수)으로 바꾼다. 빈 문자열이면 건드리지 않는다.
        // 예: Hm1(Y 위치 고정 116) 코드에 H0의 제약(80)만 적용한 재구성 조건. 역사 복원 실행의 profile에는 넣지 않는다.
        public string playerConstraints = "";
        public float invincibleSec = 100000f;
    }

    [Serializable]
    public class BenchColliderInfo
    {
        public int id;
        public string path;
        public string type;
        public string tag;
        public int layer;
        public bool trigger;
        public bool convex;
    }

    [Serializable]
    public class BenchPhaseMark
    {
        public string phase;
        public int step;
        public float fixedTime;
        public string keys;
    }

    [Serializable]
    public class BenchInputEvent
    {
        public string action;
        public string value;
        public float requestFixedTime;
        public float requestRealtime;
        public string requiredActions;   // 키 상태가 바뀐 키에 묶인 액션들(쉼표 구분). 모두 수신돼야 완전 전달
        public string receivedActions;   // 실제 콜백이 온 액션들(쉼표 구분, 수신 순서)
        public string droppedKeys;       // 판정기 검증(partial_input)에서 장치로 보내지 않은 키
        public float firstReceivedFixedTime = -1f;
        public float receivedRealtime = -1f;   // 필요한 액션이 모두 수신된 시각
        public float receivedFixedTime = -1f;
    }

    [Serializable]
    public class BenchTrial
    {
        public string runId;
        public string experimentId;
        public string versionId;
        public string caseId;
        public int attempt;
        public int order;
        public string scenario;
        public string environment;
        public string moveMode;
        public string expect;
        public string sabotage;
        public float minAllowedY;
        public float[] wallNormal;
        public float[] wallPoint;
        public float wallTopY;
        public float wallPlaneTolerance;
        public string[] wallTargets;
        public float[] start;
        public float startYaw;
        public float yawAfterAlign;
        public float fixedDeltaTime;
        public string collisionDetection;
        public string interpolation;
        public float capsuleRadiusWorld;
        public float capsuleHalfHeightWorld;
        public float contactOffset;
        public string status;            // completed | harness_error
        public string harnessError;
        public float initialPenDepth;       // 배치 직후, 첫 물리 스텝 전
        public float initialPenWallDepth;
        public float firstWallContactTime = -1f;
        public float preContactSpeed = -1f;
        public float startRealtime;
        public float endRealtime;
        public int steps;
        public int frames;
        public float maxFrameMs;
        public BenchPhaseMark[] phases;
        public BenchInputEvent[] inputs;
        public BenchColliderInfo[] colliders;
        public string[] controls;
        public string[] logErrors;
    }

    [Serializable]
    public class BenchTraceRow
    {
        public string caseId;
        public int attempt;
        public int step;
        public float t;
        public string phase;
        public float[] p;      // rb.position
        public float[] v;      // rb.linearVelocity
        public float bottomY;  // 캡슐 최저점 월드 y
        public float[] input;  // PlayerMovement.InputDir
        public float yaw;
        public bool grounded;  // PlayerMovement.IsGrounded (게임 자체 판정)
        public bool rolling;
        public int selfContacts;          // 플레이어 자신의 콜라이더끼리 접촉 수(H1 자식 body 등)
        // 외부 콜라이더와의 개별 접점(평균하지 않음). 같은 인덱스끼리 한 접점이다.
        public int[] contactIds;
        public int[] contactBody;         // 접촉한 플레이어 Rigidbody 순번(0=주 캡슐)
        public float[] contactNormalY;
        public float[] contactNormalDot; // 진행 방향과 접촉 법선의 내적
        public float[] contactWallDist;  // 접점과 기준 벽면 사이 부호 거리(m, 벽 안쪽이 +). 기준 벽이 없으면 비움
        public bool targetWallContact;    // 이 스텝에 대상 벽 접점이 있었는지
        public int otherWallContacts;     // 대상이 아닌 수평 접점 수(인접 장애물 등)
        public int probeId = -1;          // 캡슐 바로 아래 짧은 SphereCast 결과
        public float probeDist = -1f;
        public float probeNormalY;
        public int penId = -1;            // 가장 깊은 침투 collider
        public float penDepth;
        public float penDirY;
        public int penWallId = -1;        // 수평 방향(|dir.y|<0.7) 침투 중 가장 깊은 collider
        public float penWallDepth;
        // 자식 Rigidbody·콜라이더 관측 (H1은 다리·머리 콜라이더를 가진 자식 body가 연석에 먼저 닿는다)
        public float[] bodyRel;           // 자식 Rigidbody 위치 - 주 Rigidbody 위치 (x,y,z 반복)
        public float selfPenDepth;        // 주 캡슐과 자식 콜라이더 사이 최대 침투
        public int childPenId = -1;       // 자식 콜라이더의 외부 최대 침투
        public float childPenDepth;
        public float childPenDirY;
        public float childPenWallDepth;
        public bool overlapSaturated;     // 관측 쿼리 버퍼가 가득 참(일부 충돌체 누락 가능)
    }

    [Serializable]
    public class BenchStatus
    {
        public string runId;
        public string state;   // starting | running | completed | interrupted | failed
        public int planned;
        public int finished;
        public int harnessErrors;
        public string currentCase;
        public string updatedUtc;
        public string message;
    }
}
#endif
