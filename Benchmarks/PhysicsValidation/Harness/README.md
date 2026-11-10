# 물리 검증 하네스 (M3)

설계: [검증 계획](../../../Docs/physics-collider-validation-plan.md) / 진행: [진행 현황](../../../Docs/physics-collider-progress.md).

이 폴더가 하네스의 **원본**이다. 복원 작업 공간에는 `Tools/sync_harness.py`로 `Assets/PhysicsBench/`에 복사만 한다. 사용자 원본 프로젝트의 `Assets/`에는 넣지 않으며, 도구가 원본 프로젝트 설치를 거부한다. 복사할 때마다 SHA 목록을 `Evidence/M3/harness-sync-*.json`에 남긴다.

## 구성

| 파일 | 역할 |
|---|---|
| `Runtime/PhysicsBenchRunner.cs` | 부트스트랩·인수·trial 루프·씬 재로드 초기화·관측·watchdog·status·manifest/environment 기록 |
| `Runtime/PhysicsBenchInput.cs` | 가상 키보드·마우스. PlayerInput에는 이 장치만 페어링하고, 액션 수신 시각은 관측만 한다 |
| `Runtime/PhysicsBenchFixtures.cs` | 판정기 검증용 합성 Box 지형. 맵 아래 (0,-100,0)에 만들며, 영역에 기존 충돌체가 있으면 만들지 않는다 |
| `Runtime/PhysicsBenchData.cs` | case·profile·결과 스키마 (JsonUtility) |
| `Editor/PhysicsBenchProbe.cs` | 에디터 읽기 도구(씬 저장 없음)다. 실제 맵 case의 기준 높이를 물리 콜라이더가 아닌 시각 메시 삼각형에서 읽는다. 그 밖에 다음을 기록한다: MeshCollider별 빌드 cooking 조건(Read/Write·스케일·cooking 옵션), 숨은 바닥 등 지지 콜라이더 범위, 집(Destination) 위치 |
| `Windows/RunSmoke.cmd`, `README-Windows.txt` | Windows 실행 스크립트·사용자 안내. 각 exe를 순서대로 `start /wait`로 실행하고, 다음을 남긴 뒤 결과를 ZIP으로 묶는다: exe별 종료 코드, `windows-environment.txt`. 결과 폴더에 쓸 수 없으면 `%LOCALAPPDATA%`를 쓴다 |
| `Cases/smoke-s1.json`, `map-smoke-s1.json` | M4 smoke(판정 검증 15건 + 실제 맵 5건). 맵 기준값 출처는 `Evidence/M4/probe-H1-points-*.json` |
| `Editor/PhysicsBenchBuild.cs` | `PHYSICS_BENCH` define을 이 빌드에만 추가하는 배치 빌드. 씬·PlayerSettings를 저장하지 않음 |
| `Tools/analyze_physics_bench.py` | 원시 결과 판정·집계. 판정은 런타임이 아니라 여기서 임계값 JSON으로 수행 |
| `Cases/classifier-checks.json` | 판정기 검증 15건과 기대 결과 (r3) |
| `Cases/profile-H1.json` / `profile-H7.json` | 키 바인딩(H1~H7 모두 Move=WASD, Sprint=LeftShift, Jump=Space, Roll=R — `git show`로 확인)·통제 대상. H7은 `HangoverEffect`(시작 후 이동 속도 배율)·`TutorialManager`를 추가로 끈다. 버전 간 속도 조건을 섞지 않기 위함이며 정상 게임 흐름 확인은 M10에서 따로 한다 |
| `Cases/thresholds-r2.json` | 잠정 임계값(r=캡슐 월드 반지름 기준, 입력 수신은 물리 시간 기준). r1은 초기 분석 재현용으로 보존. M6에서 동결 |

런타임 코드는 전부 `#if PHYSICS_BENCH`로 감쌌다. 일반 빌드와 에디터에는 포함되지 않는다. `UnityEditor` 참조와 절대 경로가 없고 Newtonsoft에 의존하지 않는다. 게임플레이 코드에는 접근하지 않고 공개 API만 쓴다(`InputDir`, `Yaw`, `IsGrounded`, `OnRollActive`, `SetSpeedBoost`, `PlayerHealth.SetInvincible`). 이 API는 H1~H7 모두에 있음을 `git show`로 확인했다. 리플렉션은 쓰지 않는다. 초기 방향은 가상 마우스 delta를 실제 Look 경로로 보내 맞춘다.

## trial 절차

1. 원본 씬을 `LoadScene`으로 다시 불러 초기화한다. `sceneLoaded`는 Awake 뒤, Start와 첫 물리 스텝 전에 호출된다. 이때 다음을 처리한다.
   - 통제 대상을 끈다(profile의 `disableComponents` / `deactivateObjectsWith`).
   - 무적을 건다.
   - 통제 트랙이면 dt/CCD를 덮어쓴다(`-benchDt`, `-benchCcd`). 역사 실행에서는 쓰지 않는다.
   - 모든 플레이어 Rigidbody에 접촉 기록기를 붙인다.
   - 출발점에 배치한다.
2. 가상 장치를 페어링하고, 실제 Look 경로로 yaw를 맞춘다(허용 오차 0.01°).
3. 같은 출발점에 다시 배치한다. 자식 Rigidbody는 씬 로드 직후 로컬 자세로 되돌리고, 모든 Rigidbody 속도를 0으로 만든다. 첫 스텝 전의 초기 침투를 기록한다.
   - H1 개미는 주 캡슐과 별개로 non-kinematic 자식 body Rigidbody(다리·머리·더듬이 콜라이더)를 가진다. 정렬 중 어긋난 자세를 그대로 두면 run마다 0번 스텝 속도가 달라졌다(`Evidence/M3/trace-repeatability-ck02-05.json`).
4. 시나리오를 진행한다.
   - `hold`: 입력 없이 유지한다.
   - `walk`, `jump`: 이동 입력(점프)을 준 뒤 관찰한다.
   - `curb`: case의 기준 벽 법선(`wallNormal`)과 접촉 법선의 내적이 0.7을 넘으면 최초 벽 접촉으로 본다. 법선이 없을 때만 진행 방향 기준을 쓴다. 단계 순서는 최초 벽 접촉까지 접근 → 접촉 후 밀기 → 후진 → 좌·우 이동 → 구제 점프 → 구제 구르기 → 관찰. 각 단계의 시간 예산은 dt와 무관하게 **초 단위**다. 이탈 여부와 관계없이 모든 단계를 수행하고, 분석기가 가장 이른 이탈 단계를 판정한다.
5. 물리 스텝마다 trace 한 행을 남긴다. trial이 끝나면 `trials.jsonl` / `trace.jsonl`에 추가한다. 중단되면 끝난 trial만 남는다.

## 실행 인수

`-physicsBench`(필수), `-benchOut <루트>`(기본 `persistentDataPath/PhysicsBench`), `-benchRunId`, `-benchSeed`(순서 섞기), `-benchAttempt`, `-benchFps`(기본 60, vSync 끔), `-benchCaseFilter <접두사>`(smoke 부분집합), `-benchCapture end`(trial 끝 화면 1장), `-benchCases`/`-benchProfile`(기본은 StreamingAssets 사본), `-benchDt`/`-benchCcd`(통제 트랙 전용).

watchdog(실시간 기준)은 세 가지 경우에 trial을 `harness_error`로 끝내고 다음 trial로 넘어간다.
- `time_scale_zero`: `Time.timeScale == 0`
- `trial_realtime_budget_exceeded`: 예상 시간 × 4 + 30초 초과
- `fixed_step_stalled`: 15초 동안 관측 스텝이 없음

다음 trial은 시작할 때 `timeScale`=1, dt=프로젝트 값으로 되돌리고, 그 사실을 통제 목록에 남긴다. 판정 검증 case의 `sabotage`(`no_input`, `time_scale_zero`, `hang`, `slow_load`)로 각 경로를 실제로 실행해 확인했다.

알려진 미통제 경로: `Destination`(집) 트리거는 컴포넌트를 꺼도 트리거 콜백이 호출되어 GameClear/GameOver로 `timeScale`=0이 될 수 있다. watchdog이 해당 trial을 하네스 오류로 끊는다. 정식 case 위치는 집 트리거에서 떨어뜨린다.

같은 run ID 폴더가 이미 있으면 실행을 거부하고 종료 코드 1로 끝난다. 정상 종료는 0, 화면의 Stop 버튼으로 중단하면 2다.

## 결과 파일 (run 폴더)

| 파일 | 내용 |
|---|---|
| `manifest.json` | run/experiment ID, cases SHA-256, profile, build-info(빌드 ID·backend·define·원본 SHA·씬/하네스 SHA), seed, 인수, Player 로그 경로 |
| `environment.json` | OS·CPU·GPU·그래픽 API·화면·vSync·목표 FPS·dt·입력/물리 설정·포커스 |
| `order.json` | seed로 섞은 실행 순서 |
| `status.json` | `running` / `completed` / `interrupted` / `failed`, 예정·완료·하네스 오류 수 |
| `trials.jsonl` | trial별 메타데이터: 상태·하네스 오류 사유, dt·CCD·보간, 캡슐 r, 초기 침투, 최초 벽 접촉 시각·직전 속도, 단계 표식, 입력 요청/수신 시각, 관측된 collider 목록, 통제 목록, 오류 로그, 프레임 통계 |
| `trace.jsonl` | 스텝별 행: 위치·속도·캡슐 최저점, InputDir, yaw, 게임 자체 `IsGrounded`, 구르기 상태, 자식 Rigidbody 상대 위치·자식 콜라이더 외부 침투·자기 침투 등 (아래 참고) |
| `captures/` | `-benchCapture end`일 때 trial 끝 화면 |

`trace.jsonl` 행에는 다음이 들어간다.

- 외부 접촉: collider ID, 법선 y, 진행 방향과 법선의 내적, 접촉한 플레이어 Rigidbody 순번
- 자기 접촉 수
- 발 아래 짧은 SphereCast 결과: 반지름 2배 이내
- 가장 깊은 침투와 가장 깊은 수평 침투

## 판정 (analyze_physics_bench.py)

| 결과 | 규칙 요약 |
|---|---|
| `harness_error:*` | trial 상태 오류, trace 행 수 불일치, 입력 요청이 0.5초 안에 액션으로 수신되지 않음 |
| `fall` | 캡슐 최저점이 `minAllowedY - 3r` 아래로 내려감. 저속이거나 정착 중이어도 무효로 빼지 않는다 |
| `pass` | hold/walk/jump에서 낙하가 없고, 마지막 0.1초 동안 실제 지지가 있음 |
| `unsupported_end` | 낙하는 아니지만 마지막에 지지가 없음 |
| `blocked_escaped` | curb에서 후진 또는 좌·우 단계 안에 이탈. 이탈 조건: 밀기 종료 위치에서 2r 이상 이동, 벽 쪽으로 r 넘게 들어가지 않음, 수평 침투 0.1r 이하, 0.1초 연속 지지 |
| `recovered_after_penetration` | 위와 같지만 이탈 전에 수평 침투가 0.25r을 넘음(초기 침투 포함) |
| `escaped_by_rescue` | 이동 단계로는 실패하고 구제 점프·구르기로 이탈 |
| `rescue_failed` | 구제 단계까지 이탈 못 함 |
| `passed_curb` | 접근·밀기 단계에서 기준 벽면(`wallPoint`/`wallNormal`)을 3r 넘게 넘어감. 접촉 기록과 무관하게 위치로 판정한다(접촉 전 터널링 포함). 윗면 위에서 넘었으면 `climbed` 플래그 |
| `no_wall_contact` | 벽에 닿지도 넘지도 않음. 성공으로 세지 않음 |

지지는 다음 중 하나로 본다. 외부 접촉 법선 y ≥ 0.5, 또는 발 아래 짧은 검사에서 간격 0.5r 이하. 멀리 떨어진 면을 raycast로 찾았다는 이유만으로 지지로 보지 않는다.

이탈 뒤 구제 단계에서 생긴 낙하는 결과를 바꾸지 않고 `fall_after_escape` 플래그로 남긴다. 임계값은 M3 잠정값이다. M5 예비 결과를 보고 M6에서 동결하며, 결과에 맞춰 사후 조정하지 않는다.

개미 SkinnedMeshRenderer는 `body` 본(자식 Rigidbody)을 따라 그려진다. 화면에 보이는 개미와 주 캡슐이 다를 수 있으므로 다음 플래그를 주 결과와 따로 남긴다.
- `child_fall`: 자식 body가 `minAllowedY - 3r` 아래에 0.1초 이상 머무름
- `child_transient_dip`: 그보다 짧게 내려갔다가 복귀함
- `child_beyond_wall`: 자식 body가 기준 벽면을 3r 넘게 넘어감

자식 body 관측값(`maxChildPenWallDepthR`, `maxSelfPenDepthR`, `maxChildSeparationR`)은 결과 분류를 바꾸지 않고 요약에만 남긴다. 합성 지형에서 점프 착지 때 자식 body가 1스텝 동안 약 3cm 바닥 아래로 내려갔다가 복귀했다(3회 실행 모두 같은 스텝). M3 합성 연석 충돌(Sprint 약 2.2m/s)에서 자식 콜라이더는 연석에 약 1.9r 파고들었다. 같은 충돌에서 주 캡슐은 벽면 밖에 머물렀다. B 실험의 끼임 기전 해석에 쓰려면 M5 예비 실행에서 다시 확인한다.

## 기존 탐색 스크립트와의 관계

기존 파일은 수정하지 않는다. 이 하네스가 다음 역할을 대체한다.

| 기존 파일 | 상태 | 남은 한계(새 하네스에서 해결) |
|---|---|---|
| `Benchmarks/RunCurbStuckRuntimePilot.cs`, `RunCurbStuckRuntimeMatrix.cs` | 대체됨 | Editor·Coplay 전용, Mac 절대 경로, 리플렉션으로 내부 필드 초기화, 낙하·저속을 무효 처리, 점프 입력 확인이 후진 벡터만 검사, 실제 마우스 페어링, 주 캡슐 접촉만 관측 |
| `Benchmarks/RunFloorBoundaryBenchmark.cs` | 대체됨 | 에디터 전용, 도로 두 곳만, Discrete를 과거 설정으로 가정, 시작 바닥 없음을 제외 처리 |
| `Benchmarks/AnalyzeCurbStuckRuntime.py` | 대체됨 | 구 스키마 전용, 낙하·저속 자동 제외 |
| `Assets/Editor/Benchmarks/TunnelingBenchmark.cs` 등 | 참고만 | 별도 PhysicsScene 탐침이라 실제 플레이어·빌드가 아님 |

기존 결과 폴더(`CurbStuckRuntime/`, `CurbStuckRuntimeMatrix/` 등)는 새 실행과 합산하지 않는다.

## 버전별 해석 주의 (M4)

판정 검증 case 중 물리 결과에 따라 달라지는 case가 있다(CK05 연석, CK07 초기 침투, CK12 60° 연석). 이들의 `expect`는 **H1 물리 기준**이다. 다른 버전에서 기대와 다르면 궤적으로 판정이 맞는지 확인한다. 판정이 궤적과 맞으면 그 버전의 물리 차이로 기록한다.
- 예: H7 Mac 진단의 CK12는 첫 접촉에서 수평 침투 1.0r 후 회복해 `recovered_after_penetration`이었다.
- 하네스 경로 검증 case(CK08 입력 미전달, CK13·14·15 감시, CK09 지지면 없음)는 버전과 무관하게 기대와 같아야 한다.

## 2026-10-30 독립 리뷰 반영 (smoke-02 / 케이스 r4·s2)

리뷰: `Evidence/Review-20261030-codex/review.md`. 기존 M3/M4 빌드·run은 보존하고 새 빌드 ID(`M4-*-02`)로 다시 검증했다.

| 항목 | 변경 |
|---|---|
| R1 대상 벽 | 첫 벽 접촉은 다음을 모두 만족하는 접점만 인정한다. 그 외 수평 접점은 `otherWallContacts`로 따로 센다. trace에 `targetWallContact`, 접점별 `contactWallDist`가 추가됐다.<br>- 법선이 기준 벽 법선과 0.7 넘게 일치<br>- 접점이 기준 벽면에서 `wallPlaneTolerance`(기본 2r) 안<br>- `wallTargets` 경로 접두사가 있으면 그 collider |
| R2 조합 입력 | 요청할 때 상태가 바뀐 키에 묶인 액션을 `requiredActions`로 두고, 액션별 수신(`receivedActions`)을 기록한다. 분석기는 모두 수신돼야 전달로 본다. 키를 놓는 요청도 감사한다. 판정 검증 CK16(`partial_input`: Sprint 키만 장치로 보내지 않음)을 추가했다 |
| R3 화면 개미 | `rootOutcome`(주 캡슐)과 종합 `outcome`을 나눴다. 자식 body의 지속 낙하는 `visual_fall`, 벽 통과는 `visual_passed_curb`로 종합 실패에 넣는다. 단, 연석 case에서는 이탈 조건이 실제로 확정된 스텝(`escapeStep`, 단계 시작 아님) 이후에 시작된 자식 낙하만 제외하고 `child_fall_after_escape` 플래그로 남긴다(2차 리뷰 반영). 순간 침투는 플래그로만 남긴다 |
| R4 접점 평균 | 접점을 평균하지 않고 하나씩 기록한다. 판정 검증 CK17을 추가했다(`curb_mesh`: 바닥·연석이 한 MeshCollider) |
| R5 fixture 레이어 | Ground 레이어 설정을 `return` 앞으로 옮겼다. 생성된 collider의 레이어·태그를 통제 목록(`fixture_collider:*`)에 남긴다 |
| Windows 스크립트 | 환경 정보는 PowerShell이 UTF-8로 직접 쓴다. 결과 ZIP은 Windows 내장 `tar`로 만들어 `/` 경로를 쓴다(실패 시 Compress-Archive) |

H1 Mac 검증 `Evidence/M4/runs/M4-H1-mac-smoke02-01`에서 판정 검증 17/17이 기대와 일치했다. 실제 맵 S3 연석 case는 더 이상 `V_surface2` 접촉을 첫 접촉으로 쓰지 않는다(`no_wall_contact` → 벽면 교차로 `passed_curb`).

분석기 판정 경계 회귀 테스트: `python3 -I Tools/test_analyzer.py <run_dir> Cases/thresholds-r3.json`. 실제 run 기록을 메모리에서만 변형해 다음을 확인한다. 이탈 확정 전 자식 낙하 → `visual_fall`, 이탈 뒤 낙하 → 성공 유지와 플래그, 순간 침투, 정지 중 자식 낙하, 조합 입력의 Sprint 미수신. 분석기를 바꾸면 이 테스트를 다시 돌린다.
