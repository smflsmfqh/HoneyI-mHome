# 물리 충돌 테스트 면접 공부 자료

기준: 2026-11-02 Asia/Seoul. 진행 문서, 실제 소스, 결과 JSON/JSONL, 오늘 추가된 pilot-02c 파일을 읽고 작성했다. 이번 설명 작업에서는 Unity/Windows 물리 시험을 새로 실행하지 않았다. 기존 분석기 회귀 테스트만 다시 실행해 11/11 통과를 확인했다. 게임 코드·하네스·동결 조건·진행 문서는 변경하지 않았다.

## 먼저 기억할 문장

현재 하는 작업은 **Windows 빌드의 바닥 낙하와 고속 연석 끼임을 별개 문제로 나누고, 과거 환경을 복원해 원시 궤적·접촉 기록으로 원인 후보와 변경 효과를 검증하는 자동화 테스트**다.

지금은 탐색 단계인 M5다. 원래 버그의 정확한 과거 빌드 재현과 정식 개선율 측정은 완료되지 않았다. 건물 관통은 별도 미해결 버그다.

## 1. 처음 수정한 이유와 현재 검증 질문

| 구분 | 개발 당시 관찰/수정 | 테스트에서 답하려는 질문 |
|---|---|---|
| A 바닥 낙하 | 에디터에서는 괜찮았는데 Windows 빌드에서 개미가 바닥 아래로 낙하. 도로·인도·연석은 Box, 접촉 소품은 Capsule 등 기본 형상으로 교체 | 실제 충돌면이 유효했나? 고속으로 유효한 면을 놓쳤나, 빌드에서 충돌면이 생성되지 않았나? 숨은 받침판이 문제를 가렸나? |
| Convex 대안 | Convex를 켜니 서로 다른 높이의 도로·연석·인도 경계가 hull로 메워져 시각 지형과 충돌면이 달라짐 | 단순 형상으로 충돌을 안정화하면서 의도한 지형 높이·단차를 유지할 수 있나? 정식 형상 오차 비교는 아직 미수행 |
| B 연석 끼임 | 기본 콜라이더 전환 이후 고속으로 연석에 돌진하면 끼임. 이후 ContinuousDynamic·dt 0.01 및 다른 이동 관련 수정 | 충돌 후 정상 차단되는가? 후진/좌우로 실제 바닥에 복귀하는가? 침투·바닥 낙하·연석 통과를 탈출 성공으로 오인하지 않는가? |

처음의 “터널링 추정”과 지금의 “원인 확정”은 구분한다. 터널링은 유효한 충돌면을 시간 간격 사이에 통과하는 문제다. Read/Write/cooking 등의 이유로 충돌면이 준비되지 않았다면, 화면상 같은 낙하라도 원인이 다르다.

Unity는 비기본 cooking 옵션 등의 조건에서 메시 Read/Write를 요구한다. Mesh cooking은 렌더링용 메시 데이터를 물리가 사용할 충돌 데이터로 준비하는 과정이다. Read/Write가 꺼졌다는 이유만으로 모든 정적 MeshCollider가 잘못됐다고 말하면 안 된다. [Unity 6.3 공식 설명](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/collision-section/collider-shapes/mesh-colliders/prepare-mesh-for-mesh-collider)

## 2. 어떤 과거 환경을 비교하나

H는 history 버전 ID다. 한 파일의 설정값만 바꾸는 것이 아니라 해당 시점 루트 저장소·별도 에셋 저장소·씬·프리팹·임포트 설정을 함께 복원한다.

| ID | 의미 | 확인한 특징 | 주의 |
|---|---|---|---|
| Hm1/H-1 | `d3a9856`, H0 직전 | MovePosition 이동, Discrete, dt 약 0.02, constraints 116. Move만 구현 | Y 위치 고정 때문에 원본 상태는 낙하 불가. 당시 에셋은 추정 복원 |
| H0 | `1a44741`, 커밋 메시지에 수직 낙하 “수정 중” | linearVelocity 이동, Discrete, dt 약 0.02, Move/Look/Jump 구현. 기본 cooking | 이미 수정이 일부 들어간 버전. 원래 오류를 본 exe와 동일하다고 증명하지 못함 |
| H1 | `9b83a24`, Box 전환 직전 | Continuous·Interpolate, dt 약 0.02. 바닥 Mesh 외에 맵 전체 Ground Box 받침판 존재 | 순수 Mesh만의 지지 상태가 아님 |
| H2 | `442b99c` | 바닥 기본 콜라이더 조정. 이동 파일은 H1과 동일 | 아직 새 복원/런타임 비교 미수행 |
| H3/H4 | `7cca610` / `dd2d880` | Continuous 직전/ContinuousDynamic 적용 직후, dt 0.02 | 아직 새 런타임 비교 미수행 |
| H5 | `9c71144` | 부스트 접촉 법선 방향 성분 처리 추가 | CCD 외 코드 변경을 분리해야 함 |
| H6 | `d746a7b` | dt 약 0.01 변경, 이동 파일은 H5와 동일 | 아직 새 런타임 비교 미수행 |
| H7 | `b8efce3`, 등록된 현재 기준 | ContinuousDynamic, dt 약 0.01. 마찰 0·주/자식 충돌 무시 등도 포함 | CCD와 dt만 바뀐 조건이 아님. 이 ID는 새 HEAD로 자동 치환하지 않음 |

근거: [커밋 감사](/Users/ihaneul/unityProject/HoneyI-mHome/Docs/physics-collider-history-audit.md:29), [현재 진행 문서](/Users/ihaneul/unityProject/HoneyI-mHome/Docs/physics-collider-progress.md).

역사 비교는 “실제로 게임이 어떻게 달라졌나”를 본다. 통제 비교는 같은 환경에서 특정 요소만 바꿔 원인 후보를 본다. H1 받침판 제거와 Hm1 constraints 변경은 후자이며 역사 원본으로 부르지 않는다.

## 3. 전체 스크립트 연결

```text
과거 Git 상태 복원 → 원본 대조 → 계측 코드 복사
→ 시각 지형/콜라이더 조사 → case·profile 준비
→ Unity 테스트 빌드 → Windows 전달 ZIP → cmd 실행
→ Runner가 실제 플레이어 입력·물리를 실행하며 원시 기록 저장
→ Python 분석기가 판정 → 근거별 집계와 사례 해석
```

| 파일 | 역할 | 공부할 핵심 |
|---|---|---|
| [prepare_snapshot.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/prepare_snapshot.py:94) | 등록 SHA에서 별도 프로젝트 추출 | 현재 프로젝트를 과거로 reset하지 않음. 루트·에셋 SHA를 함께 사용. H0/Hm1 에셋 추정 한계를 기록 |
| [verify_snapshot.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/verify_snapshot.py:20) | 복원 파일을 Git blob과 대조 | 변경·누락·추가 계측 파일을 구분. 체크섬은 출처를 확인하며 물리 정확성을 보장하는 것은 아님 |
| [PhysicsRestoreAudit.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/Editor/PhysicsRestoreAudit.cs:50) | Editor가 해석한 씬/프리팹 상태 조사 | YAML 텍스트 검색만으로 알 수 없는 상속·실제 컴포넌트 상태 확인. 런타임 Awake 이후 상태와는 구분 |
| [sync_harness.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/sync_harness.py:18) | 전용 복원본에 계측 소스 복사 | 사용자 원본 Assets 설치 거부. 복사한 C# SHA 기록 |
| [PhysicsBenchProbe.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Editor/PhysicsBenchProbe.cs:37) | 시각 메시 높이·콜라이더 범위·Read/Write/cooking 조사 | 시작 높이는 물리 콜라이더가 아닌 렌더링 메시 삼각형에서 산출. “잘못된 콜라이더가 있는 높이”를 정답으로 삼지 않음 |
| [make_explore_e2.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/make_explore_e2.py:163) | 버전별 시작 높이와 탐색 경로로 case 생성 | e1의 버전별 높이 차이를 교정. 경로/점프/이음매/단차/소품/사후 표적 생성 |
| [make_cases_e2c.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/make_cases_e2c.py:25) | e2b에서 미지원 동작 제거 | Hm1 Jump 26건 제외, Hm1/H0 단차 접근 Sprint→Move, 미지원 구제 동작 제거. 원래 e2b 보존 |
| [PhysicsBenchData.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchData.cs:15) | case/profile/trial/trace 데이터 정의 | case는 어디서 무슨 행동을 할지, profile은 버전별 키·통제 대상을 지정 |
| [PhysicsBenchBuild.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Editor/PhysicsBenchBuild.cs:39) | 전용 실행 파일 빌드 | PHYSICS_BENCH define, Mono2x/플랫폼/Unity/씬/계측 SHA 기록. H0/Hm1 어댑터 define 분기 |
| [make_smoke_bundle.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/make_smoke_bundle.py:26) | 전체 실행 폴더·cmd·case/profile·체크섬 묶기 | 이름은 smoke지만 pilot도 생성. exe 하나만 보내지 않음 |
| [RunSmoke.cmd](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Windows/RunSmoke.cmd) | H1/H7 Windows 실행 경로 확인 | 종료 코드·환경·로그·결과 ZIP 수집 |
| [RunPilot.cmd](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Windows/RunPilot.cmd) | pilot-01 네 조건 실행 | H0 → H1 → H1-nocatch → H7 |
| [RunPilot02.cmd](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Windows/RunPilot02.cmd) | pilot-02b 다섯 조건 실행 | Hm1 → Hm1+80 → H0 → H1 → H7 |
| [RunPilot02c.cmd](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Windows/RunPilot02c.cmd:23) | 최신 02c 여섯 조건 실행 | Hm1 → Hm1+80 → Hm1+112 → H0 → H1 → H7 |
| [PhysicsBenchRunner.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchRunner.cs:101) | 전체 실험 진행과 원시 기록 | 초기화, 순서 섞기, 입력, 물리 스텝 관측, watchdog, 결과 저장 |
| [PhysicsBenchInput.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchInput.cs:55) | 가상 입력 장치와 액션 수신 관측 | 실제 PlayerInput 경로를 사용. 조합 입력은 필요한 액션을 모두 수신해야 함 |
| [PhysicsBenchPlayerApi.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchPlayerApi.cs:19) | 버전별 플레이어 연결 | 새 플레이어로 과거 플레이어를 대체하지 않음. 구버전 private 관측과 Hm1 준비 시 카메라 yaw 설정은 예외로 기록 |
| [PhysicsBenchFixtures.cs](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchFixtures.cs:16) | 알려진 형태의 합성 검증 지형 | 판정기 검증용. 실제 맵 개선율과 합산하지 않음 |
| [analyze_physics_bench.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Tools/analyze_physics_bench.py:95) | 기록으로 결과 판정 | 입력 오류·낙하·벽 통과·이탈·시각 body 실패·지원 동작을 구분 |
| [test_analyzer.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Tools/test_analyzer.py:40) | 분석기 회귀 검증 | 기록의 복사본을 메모리에서 변형. 실제 Unity 버그 재현과 다름 |
| [audit_support_e2.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/audit_support_e2.py:26) | 시각 지면과 지지 후보 높이 차이 조사 | bounds 기반 정적 감사. 실제 지지 여부는 런타임 접촉과 함께 판단 |
| [summarize_falls_e2.py](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Tools/summarize_falls_e2.py:21) | 낙하 지점과 경로·메시 전환을 연결 | 경로 안/밖, 마지막 메시 전환, 단차와의 거리 기록. 원 판정을 바꾸는 도구는 아님 |

## 4. 한 번의 trial은 어떻게 실행되나

run은 실행 파일을 한 번 실행한 묶음이고, trial은 그 안의 case 한 번이다. 물리 step은 위치·접촉을 한 번 관측하는 단위다. 세 개를 혼동하면 “72회 실행했다”는 표현이 불명확해진다.

Runner.RunAll은 case를 seed로 섞어 order.json에 저장한다. seed는 순서 재현을 돕지만 Unity 물리를 완전 결정적으로 만드는 것은 아니다. 현재 주된 실행은 seed 1이며 정식 반복·통계 실험은 아직 아니다.

Runner.RunTrial의 순서:

1. 입력·이전 로그·관측 버퍼를 정리하고 timeScale=1, 원래 dt로 되돌린다.
2. 매 trial 원본 씬을 재로드한다. 앞 시도의 자식 body 변형·충돌·NPC 상태가 그대로 넘어가지 않도록 한다.
3. profile에 적힌 NPC/스폰·속도 효과 등을 통제하고 무엇을 껐는지 controls에 남긴다. H1-nocatch는 여기서 Ground를 비활성화한다.
4. 플레이어의 주 Rigidbody와 자식 Rigidbody에 접점 기록기를 붙인다.
5. PlayerInput을 가상 키보드/마우스에 페어링하고 액션 콜백을 관측한다.
6. 진행 방향을 맞추고 출발점에 재배치한다. 캡슐 최저점이 지정 y보다 0.0005m 위에 오도록 한다. 자식 body 자세와 모든 body 속도를 초기화한다.
7. 배치 직후 침투를 기록하고 settle 단계부터 trace를 남긴다. **정착 중 낙하도 실패로 기록하며 무효 시도로 버리지 않는다.**
8. hold/walk/jump/curb 시나리오를 수행한다.
9. 원시 trial/trace, 필요 시 마지막 화면을 저장한다. 정상 완료는 exit 0, 사용자 중단은 별도 코드다. exit 0은 물리 문제가 없었다는 의미가 아니다.

근거: [RunTrial](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchRunner.cs:158), [씬 로드/통제](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchRunner.cs:651), [출발 배치](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchRunner.cs:756).

## 5. 입력을 직접 위치 이동으로 대체하지 않은 이유

준비 단계에서만 위치·속도를 초기화하고 시험 중에는 가상 키를 보내 원래 PlayerInput→PlayerMovement를 움직인다. 시험기가 transform을 계속 옮기면 이동 방식과 충돌 반응을 대신 구현해 원래 버그를 가릴 수 있기 때문이다.

PhysicsBenchInput은 바뀐 키에 대응하는 requiredActions와 콜백으로 확인한 receivedActions를 나눠 기록한다. Move+Sprint에서 Move만 수신했으면 Sprint 시험이 아니라 하네스 입력 오류다.

단, **입력 액션 수신과 실제 동작 구현은 다르다.** Hm1에는 Jump 처리가 없어 콜백이 와도 점프하지 않는다. capabilities-r1이 이 차이를 표시하고 e2c는 그런 동작을 제거한다.

H1/H7은 공통 공개 API를 사용하고, H0/Hm1은 일부 private 입력·접지 상태를 reflection으로 읽는다. Hm1은 Look API가 없어 준비 중 카메라 private yaw를 설정한다. “모든 버전에서 reflection 없이 공개 API만 사용”이라는 예전 설명은 현재 전체 범위에는 맞지 않는다.

Hm1 FollowCamera는 legacy 실제 마우스 입력을 사용하므로 가상 장치 페어링만으로 완전히 격리되지 않는다. 최신 Windows 안내도 Hm1 실행 중 실제 마우스를 움직이지 말라고 한다. 이 점과 방향 정렬 실패는 환경 한계로 기록해야 한다.

## 6. 실제 진행하는 테스트의 종류

### 6.1 측정 장비 자체의 검증: 합성 fixture와 CK 사례

실제 맵을 측정하기 전에, 정상 바닥을 실패로 잘못 세거나 입력 실패를 끼임으로 잘못 세지 않는지 확인한다. Runtime의 fixture 검증은 Unity의 실제 물리와 실제 플레이어를 실행한다.

| case | 만든 조건 | 검증 목적 |
|---|---|---|
| CK01/02 | 평평한 바닥 정지·보행 | 정상 바닥을 정상으로 판정 |
| CK03/09 | 구멍 보행 / 지지면 없음 | 저속·정착 중 낙하도 실패로 검출 |
| CK04 | 정상 2cm 내려가는 단차 | 합법적인 높이 변화와 낙하 구분 |
| CK05 | Box 연석 Sprint | 막힌 뒤 후진·좌우로 이탈하는 기본 경로 |
| CK06 | 지붕까지 있는 작은 trap | 실제 이탈 불가를 검출 |
| CK07 | 시작부터 벽에 겹친 상태 | 첫 스텝 전에 해소될 초기 침투도 검출 |
| CK08/16 | 전체 입력 누락 / Sprint만 누락 | 하네스 오류와 게임 물리 실패 구분 |
| CK10 | 실제 점프 후 착지 | 정상 착지 판정 |
| CK11 | Trigger 연석 | 접촉 콜백 없이 벽면을 넘어간 통과도 검출 |
| CK12 | 60도 비스듬한 연석 접근 | 진행 방향만으로 벽 접촉을 놓치지 않도록 검증 |
| CK13/14 | timeScale=0 / 기록 정지 | watchdog 중단·다음 trial 복구 |
| CK15 | 씬 로드 20초 지연 | 긴 정상 로드를 물리 정지로 오인하지 않음 |
| CK17 | 바닥·벽을 한 MeshCollider로 구성 | 법선 평균으로 벽 접점이 사라지지 않도록 접점별 기록 검증 |

근거: [classifier-checks.json](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Cases/classifier-checks.json), [Fixtures.Build](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchFixtures.cs:16).

최신 CK는 17개다. smoke-s2는 CK 17개+실제 맵 5개=22개다. 기대 판정은 H1에서 17/17, H7에서 16/17이다. CK12는 H7에서 침투 후 회복으로 달라져 계속 별도 추적한다. “H7도 전부 통과”라고 말하지 않는다. 또한 의도한 fall/harness_error가 기대대로 발생한 것도 기대 판정 일치에 포함된다.

### 6.2 분석기만의 회귀 테스트: test_analyzer.py

이 테스트는 Unity를 켜지 않는다. 기존 기록의 복사본을 메모리에서 바꿔 판정 경계를 확인한다.

- 원본 연석 기록은 원래대로 이탈 성공인가?
- 실제 이탈 확정 전 자식 body가 지속 낙하하면 visual_fall인가?
- 이탈 뒤 구제 단계에서만 낙하하면 별도 플래그로 남는가?
- 1스텝 순간 침투를 지속 낙하로 오인하지 않는가?
- 주 캡슐이 서 있어도 시각 body가 떨어지면 실패인가?
- Sprint만 누락한 조합 입력을 오류로 잡는가?
- 버전별 미지원 동작·Y 고정·정지 추론 지지를 구분하는가?

현재 **11/11** 통과다. 이 결과는 판정 규칙이 의도대로 작동함을 뜻하며 원래 물리 버그가 11번 해결됐다는 뜻은 아니다.

### 6.3 smoke: Windows에서 자동 시험 자체가 실행되는지

빌드 출력 전체와 cmd를 전달해 그래픽 초기화, 가상 입력, 창 포커스, 시간 정지 감시, 결과 저장, 종료 코드, 결과 ZIP 회수를 확인한다. Windows PC에 Unity/Python/Git를 설치할 필요가 없다.

smoke-01과 smoke-02는 Windows에서 실행·회수됐다. 이 단계의 성공은 측정 경로 확인이며 Mesh→Box 개선율 근거가 아니다.

### 6.4 pilot-01/e1: 실제 맵의 어디에서 떨어지는지 탐색

e1은 합성 sanity 4개 + 실제 맵 68개 = 총 72개다. 맵 68개는 정지 40개, 보행 14개, 점프 14개다. 지도에 정한 좌표에서 원래 플레이어를 움직인다. 무작위 사용자 68명·반복 68회라는 의미가 아니다.

RunPilot.cmd가 H0, H1, H1-nocatch, H7 네 조건을 실행한다. H1-nocatch는 profile-H1-nocatch.json의 deactivatePaths로 PostProcessingVolume/Ground만 끈다.

네 run 모두 Windows에서 72/72 완료, sanity 기대 판정 4/4다. H1-nocatch 지도 68개 중 35개가 정착 중 낙하했다. 다른 곳은 보조 Box가 받친다. H1/H7 e1의 2개 fall은 버전별 지면 높이가 달라 생긴 case 설정 문제라 버그 수에서 제외해 설명한다.

근거: [Windows pilot-01 요약](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Evidence/M5/M5-windows-pilot-01-summary.json), [받침판 제거 profile](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Cases/profile-H1-nocatch.json).

### 6.5 e2/e2b/e2c: 경계와 경로를 더 자세히 탐색

e2는 버전별 시각 지면으로 시작 y/minAllowedY를 다시 만든다. e2b는 Mac 탐색에서 발견한 경계를 다시 볼 사후 표적 K01~K12를 추가한다. e2c는 미지원 동작을 제거한 새 revision이다. 사후 표적에서의 재현율을 전체 맵 확률로 보고하면 안 된다.

| 접두사 | 장소/행동 | 검증 질문 |
|---|---|---|
| E1_P / E1_W | e1 지도 정지/보행/점프 재사용 | 시작 높이를 교정하면 기존 결과가 유지되나? |
| E2_L01~06 | 긴 도로 경로 6개 | 짧은 spot 검사가 놓친 먼 이음매에서 떨어지나? |
| E2_C01~12 | 도로·연석·인도 횡단, 지원 시 점프 | 단차·메시 경계를 지날 때 지지가 끊기나? |
| E2_M01~02 | 타일 이음매 위 보행 | 같은 높이 메시 경계도 문제가 되나? |
| E2_S* | 같은 높이 이음매 정지 및 1m drop | 정지 지지와 위에서 내려오는 스트레스를 구분해 확인 |
| E2_B01~04 | H7 도로 위 Box 끝에 접근 | 시각 도로보다 0.1m 높은 보이지 않는 단차에 막히거나 파고드나? |
| E2_P00~07 | H7 소품 AABB 중심선 접근 | cooking 오류 소품에 실제 접촉이 있나? AABB 통과만으로 관통 확정은 못 함 |
| E2_K01~12 | 발견한 낙하 경계 12곳의 짧은 보행·정지 | 긴 경로에서 그곳에 못 도달한 느린 버전도 같은 위치에서 확인 |

H7의 e2c는 154개, H0/H1은 146개, Hm1은 미지원 점프 26개를 제거해 120개다. e2c의 총 여섯 run 예정 trial은 806개다. 이는 다양한 탐색 조건의 합이며 동일 조건 806회 반복이라는 뜻이 아니다.

긴 경로의 실행 시간은 H0 속도를 기준으로 정해져 있어 버전별로 실제 이동 거리가 다르다. trace로 도달 범위를 확인한다. 맵 끝을 지나 떨어진 사건은 경로 밖으로 별도 분류한다. 점프 시각도 모든 버전에서 같은 경계 위치를 보장하지 않으므로 현재 탐색과 정식 통제 비교를 구분한다.

## 7. 연석 시험을 실제로 어떻게 판정하나

Runner.Curb는 접근 → 접촉 뒤 전진 유지 → 입력 해제 → 후진 → 좌/우 → 지원되는 구제 점프/구르기 → 관찰 순서다. 시간 예산은 초 단위여서 dt 0.02와 0.01에서 같은 스텝 수로 비교하지 않는다.

벽 접촉은 법선뿐 아니라 기준 벽면 거리와 선택적인 대상 Collider 경로로 확인한다. 예전에는 옆 장애물 V_surface2를 연석 접촉으로 잘못 센 문제가 있었고 수정됐다. 바닥+벽 접점은 평균내지 않고 각각 기록한다.

분석기는 전진 종료 위치에서 얼마나 이동했는지, 벽 속으로 더 들어갔는지, 침투가 해소됐는지, 지지가 유지되는지를 함께 본다. 단순히 키 입력 뒤 좌표가 바뀌었다고 탈출 성공으로 세지 않는다.

근거: [Curb](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchRunner.cs:318), [IsTargetWall](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Runtime/PhysicsBenchRunner.cs:552), [판정기](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Tools/analyze_physics_bench.py:172).

| 주요 잠정 기준 | 현재 값/의미 |
|---|---|
| r | 주 Capsule의 월드 반지름. H1/H7 약 0.0094m, H0/Hm1 약 0.015m |
| 낙하 | 캡슐 최저점 < minAllowedY − 3r |
| 지지 후보 | 접촉 법선 y≥0.5 또는 짧은 probe 간격≤0.5r |
| 안정 구간 | 약 0.1초 |
| 이탈 거리 | 전진 종료 위치에서 2r 이상, 벽 쪽으로 r 넘게 들어가지 않음 |
| 이탈 시 침투 | 수평 침투≤0.1r |
| 유의한 침투 | 0.25r 초과 |
| 벽 통과 | 기준 벽면을 3r 넘게 넘어감. 올라넘은 경우 climbed 플래그와 구분 |
| 입력 감사 | 필요한 액션 전부 수신, 물리 시간 기준 지연≤0.4초 |

이 값들은 **프로젝트 분석기의 잠정 규칙**이다. Unity의 보편적인 물리 정답이나 임상적인 통계 기준이 아니다. M6에서 정식 동결할 예정이다. [thresholds-r4.json](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Cases/thresholds-r4.json)

결과는 pass/fall, blocked_escaped, recovered_after_penetration, escaped_by_rescue, rescue_failed, passed_curb, no_wall_contact, harness_error 등으로 나뉜다. 단순 끼임 비율 하나만 만들면 통과·낙하와 정상 차단을 혼동할 수 있어 여러 결과를 유지한다.

주 캡슐 기준 rootOutcome과 시각 개미 body를 포함한 outcome도 분리한다. 자식 body가 지속 낙하하거나 벽을 넘은 것을 정상 이탈로 세지 않기 위해서다. 순간 침투와 지속 낙하는 시간 기준으로 구분하고, escapeStep은 후진 단계 시작이 아니라 실제 조건 충족 확정 시점이다.

현재 pass에는 정지 상태로 지지를 추론한 결과도 포함될 수 있다. 따라서 summary.passEvidence의 pass_contact_or_probe / pass_rest_inferred / pass_rest_inferred_y_frozen을 구분해야 한다. Y 고정으로 공중에 떠 있는 것을 정상 착지로 설명하지 않는다. capabilities는 outcome 자체를 바꾸지 않고 플래그와 근거별 집계를 추가한다.

## 8. 실제 결과 하나를 코드와 연결해서 읽기

Windows pilot-01의 E1_P17R_hold를 예로 든다.

- 출발 캡슐 바닥 좌표: (46, 0, -51).
- H1 주 Rigidbody: Continuous, dt 약 0.02, r≈0.0093997m.
- 낙하 판정선: 0 − 3r≈−0.028199m.
- H1 base: 실제 접촉 대상이 PostProcessingVolume/Ground. settle 중 약간 파고든 뒤 바닥 최저점이 0 근처로 복귀한다.
- H1-nocatch: controls에 deactivatePath:PostProcessingVolume/Ground가 남는다. 첫 기록들의 bottomY는 약 −0.0068 → −0.0191 → −0.0353m다. 0부터 센 2번 스텝에서 낙하 판정선을 넘는다. 그때 contactIds는 비어 있고 probeId=-1이다.

이를 만드는 코드는 profile의 받침판 제거 지시 → Runner.ApplyControls → Runner.Record의 접촉/최저점 기록 → analyzer.first_fall이다. 판정은 settle 중에도 적용된다.

안전한 해석: “이 복원 H1 빌드에서 이 지점의 지지는 도로 Mesh가 아니라 Ground 받침판에 의존했고, 받침판을 제거한 통제에서 지지 상실 후 낙하했다.”

아직 할 수 없는 해석: “고속 터널링이 확정됐다”, “원래 사용자 빌드와 완전히 같은 버그를 재현했다”, “Box 전환으로 51.5% 개선됐다”.

원시 근거: [base trial](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Evidence/M5/windows/PhysicsBench-Pilot-Results-20261030T150726/W-H1-base-pilot-20261030T150726/trials.jsonl), [nocatch trial](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Evidence/M5/windows/PhysicsBench-Pilot-Results-20261030T150726/W-H1-nocatch-pilot-20261030T150726/trials.jsonl), 같은 폴더 trace.jsonl, [분석 결과](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Evidence/M5/analysis/W-H1-nocatch-pilot-a1/classified.jsonl).

## 9. 11월 2일 확인한 최신 진행 상태

| 단계/결과 | 상태 |
|---|---|
| M1~M3 | baseline 고정, 원본 복원/감사, 하네스 구현·검증 완료 |
| M4 | Windows smoke-01/02 실행·회수 완료 |
| M5 pilot-01 | 네 Windows run 실행·회수·분석 완료 |
| M5 e2/Hm1+80 | Mac 탐색 완료. 낙하 경계 후보 발견. 역사 버전 아님 |
| M5 pilot-02b | 이전 제작판으로 보존. 최신 진행 문서는 전달 대상으로 02c를 지정 |
| M5 pilot-02c | 오늘 case·profile·freeze·cmd·전달 ZIP 생성 확인. 여섯 run 예정, Windows 원시 결과는 아직 없음 |
| Hm1+112 e2c Mac | 120/120 기록 완료. 예상 입력/정지 오류 2건 외 yaw_align_failed 7건이 있어 전체 물리 성공을 주장할 수 없음 |
| M6~M11 | 정식 조건 동결·본 실행·독립 반복·최종 수치 보고 미완료 |

이번 자료 작성 도중 다른 작업자가 진행 문서도 11/02로 갱신했다. 최종 재확인 시 상단 대기 대상은 02c이며 02b는 보존만 한다. 이 공부 자료는 해당 최신 상태에 맞췄으며 진행 문서를 대신 갱신하지 않는다.

오늘 새 통제 조건:

- 116: Y 위치와 X/Y/Z 회전 고정 — Hm1 원본.
- 80: X/Z 회전 고정 — Y 위치와 Y 회전을 함께 해제.
- 112: X/Y/Z 회전 고정 — **Y 위치만** 해제.

02c는 112를 추가해 80의 결과가 Y 회전 해제와 섞였을 가능성을 분리하려는 탐색이다. 같은 exe에 다른 profile을 적용하므로 이동 코드 자체를 다른 것으로 교체하지 않는다.

112 Mac 집계는 pass 80, fall 27, passed_curb 4, 의도한 입력/시간 오류 2, 방향 정렬 오류 7이다. fall 27 중 CK03 의도 낙하 1개가 포함되며, pass 80 중 직접 접촉/probe 근거는 17개, 정지 추론은 63개다. 따라서 “120개 통과”나 “정상 착지 80개”라고 말하면 부정확하다. 이는 Windows 결과도 아니다.

근거: [02c freeze](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Evidence/M5/M5-pilot02c-freeze.json), [02c 파생 내역](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Cases/e2c/e2c-derivation.json), [112 Mac 분석](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Evidence/M5/analysis/M5-Hm1-unfreezePosY-mac-e2c-01-a1/summary.json), [112 profile](/Users/ihaneul/unityProject/HoneyI-mHome/Benchmarks/PhysicsValidation/Harness/Cases/profile-Hm1-unfreezePosY.json).

## 10. CCD와 dt를 면접에서 설명하는 법

dt 0.02는 초당 50회, dt 0.01은 초당 100회 물리 갱신을 뜻한다. 화면 렌더 FPS와 다르다. 예를 들어 2.5m/s 이동이면 한 스텝 이동거리는 각각 5cm와 2.5cm다. H1/H7 캡슐 지름 약 1.88cm와 비교하면 물리 간격을 검토할 이유를 알 수 있다. 이 산술만으로 터널링을 증명하지는 못하며 CCD는 그 사이의 충돌을 별도로 다룬다.

더 짧은 dt는 정확도/안정성을 높일 수 있지만 물리 호출 횟수와 CPU 부담을 늘린다. “물리 정확도가 2배”, “프레임 성능이 2배 개선”이라고 설명하지 않는다. 성능 효과는 Profiler로 별도 검증해야 한다. [Unity 6.3 fixed timestep 설명](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/physics-optimization/cpu/frequency)

Continuous와 ContinuousDynamic은 모두 sweep 기반 CCD다. Continuous는 정적 콜라이더와의 충돌, ContinuousDynamic은 움직이는 물리 body와의 충돌까지 고려할 때 선택하는 모드다. 정적 연석에 대해 이름만으로 Dynamic이 무조건 더 낫다고 단정하면 안 된다. H1은 이미 Continuous였으므로 “Discrete에서 바꿨다”는 일반 설명도 H1 비교에는 틀리다. [Unity 6.3 모드 선택](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/collision-section/collision-detection/choose-collision-detection-mode)

H7은 CCD/dt뿐 아니라 부스트 접촉 법선 처리, 마찰, 자기 충돌 무시, 지형 형상 등이 달라졌다. 정식 단계에서 역사 H3→H4, H5→H6 비교와 같은 지형·코드의 CCD×dt 통제 비교를 분리해야 요인별 효과를 설명할 수 있다.

Convex hull은 오목한 부분을 채워 볼록한 외곽으로 만들므로 도로/인도/연석을 하나로 감싸면 실제 단차와 다른 충돌면을 만들 수 있다. 이 프로젝트에서 해당 불일치는 당시 개발 관찰이며 정식 형상 오차 측정 수치는 아직 없다. [Unity 6.3 MeshCollider 설명](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/collision-section/collider-shapes/mesh-colliders/introduction)

## 11. 저장 파일을 보는 순서

1. manifest.json: 어떤 build/case/profile/seed로 실행했나?
2. environment.json: 실제 OS/CPU/GPU·dt·입력/물리 환경은?
3. status.json + cmd exitcode: 예정/완료/중단·오류 여부는?
4. order.json: case 실행 순서는?
5. trials.jsonl: case 메타데이터·입력·통제·Collider ID→경로 대응은?
6. trace.jsonl: 스텝별 위치·속도·접촉·침투·자식 body가 판정을 뒷받침하나?
7. classified.jsonl/summary.json: 어떤 임계값과 capability로 어떻게 집계했나?
8. Player.log/캡처: cooking 오류 등 원인 후보와 눈으로 본 장면이 수치와 맞나?

capturing은 실행 인수가 켜졌을 때만 한다. 기존 모든 실행에 영상이나 캡처가 있다고 가정하지 않는다. 체크섬은 전송·출처 무결성을 돕지만 누락된 동작이나 잘못된 판정 정의를 자동으로 고쳐주지는 않는다.

## 12. 기존 테스트와 무엇이 달라졌나

기존 RunFloorBoundaryBenchmark/RunCurbStuckRuntimePilot/Matrix 등은 에디터 탐색 자료다. 당시 설정·지점 참고로 보존하되 Windows 본 실험과 합산하지 않는다. 일부는 현재 컨트롤러로 과거 설정을 흉내 내거나 저속·낙하를 무효 시도로 처리하는 한계가 있었다.

새 환경은 과거 코드/씬을 복원하고 실제 런타임 입력을 주며, 저속·정착 중 낙하도 남기고, 미전달 입력과 물리 실패를 구분한다. 단, 최신 환경도 추정 에셋·구버전 입력 격리·정지 추론·아직 미실행된 역사 버전이라는 한계를 공개하고 있다.

## 13. 면접 답변 연습

### 60~90초 설명 예시

> Windows 빌드에서 개미가 바닥 아래로 떨어지는 문제 때문에 도로·인도·연석을 기본 콜라이더로 구성했습니다. 이후 고속 연석 충돌에서 끼임이 생겨 충돌 감지와 물리 갱신 간격도 조정했습니다. 이 두 문제를 같은 원인으로 단정하지 않고, Git의 과거 씬·플레이어 코드·에셋 버전을 복원하는 자동 검증 환경으로 분리해 확인하고 있습니다.
>
> 테스트는 실제 PlayerInput 경로에 가상 입력을 주고, 물리 스텝마다 캡슐 위치·접촉 대상·침투량과 화면 개미의 자식 body 상태를 기록합니다. 연석은 돌진 뒤 후진·좌우·구제 동작으로 실제 지형에 복귀하는지 보고, 결과 판정은 별도 Python 분석기로 처리합니다.
>
> 탐색에서 과거 H1에 맵 전체 받침판이 있어 바닥 Mesh 실패를 가리고 있음을 발견했습니다. 같은 빌드에서 받침판을 제거한 통제는 Windows의 지도 사례 68개 중 35개에서 낙하했고, Mac에서도 같은 결과였습니다. 다만 원래 오류를 본 정확한 빌드는 아직 확정하지 못했고, CCD와 dt의 개선율도 다른 코드 변경과 분리한 정식 측정 전이므로 원인이나 수치를 과장하지 않고 있습니다.

### 자주 나올 질문과 답변 포인트

| 질문 | 답변에 포함할 내용 |
|---|---|
| 왜 에디터가 아니라 빌드에서도 검사했나? | 원래 증상이 Windows 빌드에서 관찰됐고 메시 데이터 준비·실행 조건이 달라질 수 있음. Mac 결과를 Windows 결과로 대체하지 않음 |
| 왜 현재 맵을 Mesh로만 바꾸지 않았나? | 당시 이동 코드·캡슐·자식 body·마찰·레이어·프리팹 override가 달라 과거 실패가 가려질 수 있음 |
| Box가 더 빠르다는 것도 검증했나? | 현재 주목표는 충돌 안정성과 형상. CPU/메모리 최적화 개선율은 별도 측정하지 않았음 |
| 35/68은 개선율인가? | 아님. 선택된 지도 행동 68개를 H1 받침판 제거 통제에서 탐색한 낙하 건수. 원본 게임 발생 확률·Mesh→Box 개선율 아님 |
| 17/17은 버그 해결인가? | 아님. 의도된 낙하·입력 오류를 포함한 판정 기대값 일치. 측정기 검증임 |
| 72/72 완료면 전부 성공인가? | 아님. 실행 완료 수이며 fall/harness_error도 포함됨 |
| 터널링 원인으로 확정했나? | 아님. 유효한 충돌면 통과와 빌드 충돌면 생성 실패를 구분 중. H1에서는 cooking/ReadWrite와 접촉 부재가 중요한 근거 |
| 땅을 뚫고 옆으로 나오면 탈출 성공인가? | 아님. 정상 지지·침투 해소·벽 통과 여부·시각 body를 함께 확인 |
| 실패한 테스트는 어떻게 처리했나? | 원시 결과를 보존하고 환경/입력 오류·미지원 동작·맵 가장자리·case 높이 오류를 물리 버그와 구분해 기록 |
| 재현 안 된 결과도 의미 있나? | 탐색 범위에서 미재현이라고 보고하고 정확한 빌드/조건 불확실성을 좁히는 근거로 사용 |
| 건물 관통도 해결됐나? | 별도 남은 버그. 이번 성과에 포함하지 않음 |

실제 수행 범위를 공부하고 자신의 구현·검토·실행 역할에 맞춰 설명한다. 위 예시는 테스트 설계/분석을 이해하기 위한 답변이며, 직접 하지 않은 구현을 모두 수작업으로 작성했다고 주장하기 위한 문구가 아니다.

## 14. 아직 완료하지 않은 것

원래 오류를 본 Windows exe의 정확한 소스/에셋 상태 확정, H2~H6 런타임 비교, Convex와 기본 형상의 정식 오차 측정, CCD/dt·접촉 코드·마찰 등의 효과 분리, 공통 계측 revision 정리, 사전 지정 반복과 신뢰구간, 배포 빌드 조건 확인, 최종 포트폴리오 수치는 남아 있다.

“테스트 환경을 구성했고 실제 빌드에서 중요한 원인 후보와 가려진 지지 구조를 확인했다”는 현재 증거에 맞는다. “두 버그를 모두 해결했고 정량 개선을 입증했다”는 현재 증거를 넘는다.
