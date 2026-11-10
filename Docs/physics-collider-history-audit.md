# 플레이어·맵 충돌 구성의 커밋 감사

최초 작성일: 2026-10-29. M5 보충: 2026-10-31. 본문은 승인 전에 Git 객체·직렬화 파일·소스를 읽은 정적 감사다. 이후 승인된 M1/M2의 복원·에디터 감사 결과는 아래 보충과 진행 현황에 별도로 기록한다. 정적 감사와 Windows 물리 테스트 결과를 혼동하지 않는다.

작업 재개: [마일스톤 실행 지침](physics-collider-execution-plan.md)과 [현재 진행 현황](physics-collider-progress.md)을 먼저 확인한다. 아래 커밋 감사는 실행 완료 증거가 아니다.

M2 보충: H1을 해당 루트·에셋 SHA로 별도 복원하고 Unity 6000.3.15f1에서 임포트·컴파일·Edit Mode 감사를 완료했다. 씬 상속이 반영된 주 플레이어는 Continuous/Interpolate, 자식 body는 Discrete, Standalone backend는 Mono2x, Fixed Timestep 실측은 0.0199999921초였다. 원본 5,889개 파일은 임포트·감사 후에도 Git blob과 일치했다. 증거는 `Benchmarks/PhysicsValidation/Evidence/H1-editor-resolved.json`과 `M2-completion.json`이다. 이는 Awake 이후 감사나 Windows 버그 재현 결과는 아니다.

M5 보충(2026-10-31, [진행 현황](physics-collider-progress.md)과 `Benchmarks/PhysicsValidation/Evidence/M5/` 기준):

- **H0는 최초 낙하 기록이 아니라 낙하 "수정 중" 커밋이다.** H0 메시지는 "플레이어 특정 맵 구간에서 수직 낙하 수정 중"이므로 낙하는 H0 이전에 관측됐다. 직전 루트 커밋 `d3a9856`(05-18 16:40)을 **Hm1**(문서상 H-1)로 추가 복원했다. Hm1은 `Rigidbody.MovePosition`으로만 이동하고(Look·점프·접지 판정 없음) constraints 116(FreezePositionY + 모든 회전)이라 Y가 고정돼 커밋 상태 그대로는 떨어질 수 없다(Mac 확인). H0에서 constraints가 80(X·Z 회전만)으로 바뀌었다. 원래 낙하는 Hm1→H0 사이의 미커밋 상태였을 가능성이 있으나 가설이다. 근거 `M5-audit-04-Hm1.json`
- **H0 에셋은 `23de6f3c` 추정이다.** H0·Hm1 루트에는 에셋 gitlink가 없다. H0 참조 비패키지 GUID 195개 중 184개가 `23de6f3c`(05-18 20:06, 다음 루트 커밋 `31beed1`이 처음 가리킴)에 있고, 누락 11개는 URP 렌더러 기능 참조(물리 무관)다. 당시 로컬 미커밋 상태를 재현했다고 주장하지 않는다. Hm1도 같은 에셋으로 고정해 H0와의 차이를 루트 변경으로 한정했다. 근거 `M5-audit-01.json`, `M5-audit-04-Hm1.json`
- **연표 보충**(`M5-audit-02-timeline.json`, `M5-audit-04-Hm1.json`): 05-22 07:20 에셋 `de8bfd48`이 Cartoon City 프리팹 79개의 cooking을 30→26(EnableMeshCleaning 제거)으로 바꿨고, 05-22 07:21 루트 `5859367`이 맵 전체 Ground 받침판 BoxCollider(윗면 y=0)를 추가했다. `4d73f8d`(05-22, 플레이어 y 하드코딩)와 `e4b5c50`(05-26, road 타일 공중 뜸·바닥 타일 경계 버그 수정)도 H1 이전이다. H1은 순수 Mesh 바닥이 아니라 받침판이 바닥 MeshCollider 실패를 가리는 구성이다
- **실행으로 확인한 것**(Mac 진단과 Windows를 구분):
  - H1 받침판 제거 통제: 68건 중 35건 수직 낙하, 도로·타일 MeshCollider 접촉 0. Mac과 Windows pilot-01 모두 같음 → H1 빌드에서 바닥 MeshCollider가 작동하지 않음. 근거 `M5-windows-pilot-01-summary.json`
  - H0: Mac e1·Windows pilot-01에서 낙하 0, Read/Write 오류 0. 원래 낙하는 이 탐색 범위에서 미재현
  - 재구성 통제 Hm1 + constraints 80(Mac 진단, 역사 버전 아님): 경로 안 낙하 22건, 모두 접촉 0. 연석 바깥 경계 0.2m 뒤와 도로→인도 타일 메시 경계 0.25~0.5m 뒤. 116→80은 FreezePositionY와 FreezeRotationY를 함께 풀기 때문에 단일 요인 효과가 아니고, H0의 다른 차이(convex 2개·레이어)는 넣지 않았다. Hm1에는 점프가 없어 jump case 낙하도 보행과 같다. 근거 `M5-mac-explore-e2-summary.json`
  - H7: S3 연석 고속 접근 시 도로면 아래로 파고들어 연석 아래로 끼어든 뒤 후진 이탈(`M5-audit-01.json`). 남쪽 `road_009_floor` Box 3개가 시각 도로보다 0.1m 높아 보이지 않는 단차가 있음(`M5-audit-05-H7-support.json`). 런타임 cooking이 필요한 MeshCollider 46개는 모두 소품(`M5-audit-03-H7-runtime-cooking.json`)
  - Windows로 Hm1·Hm1+80·H0·H1·H7을 함께 보는 pilot-02b는 실행 대기다. 위 Mac 진단은 Windows 증거가 아니다

## 1. 결론

“수정 전”은 하나의 상태가 아니다. 낙하 관측 전 상태(Hm1), 낙하 수정 착수 시점(H0), Box 전환 직전, Box 전환 후, CCD 변경 후, 이동 코드 변경 후, 시간 간격 변경 후, 현재를 구분해야 한다.

**현재 코드에서 Mesh/Box·CCD·dt만 바꾸는 것으로는 실제 과거 환경의 테스트가 되지 않는다.** 역사 버전을 그대로 복원하는 비교를 필수로 수행하고, 한 요인씩 바꾸는 통제 실험을 별도 수행한다.

## 2. 복원 기준표

| ID | 루트 커밋 | 에셋 저장소 커밋 | 확인한 플레이어·물리 구성 | 용도 |
|---|---|---|---|---|
| Hm1 (M5 추가) | `d3a9856` / 05-18 16:40 | gitlink 없음. H0와 같은 `23de6f3c` 고정 | 씬 직접 배치 Capsule, MovePosition 이동, constraints 116(FreezePositionY 포함) | H0 직전 상태. 커밋 상태로는 낙하 불가. 재구성 통제(constraints 80)의 바탕 |
| H0 | `1a44741` / 05-18 18:05 | gitlink 없음. `23de6f3c` 추정(M5, GUID 대조) | 씬 직접 배치 Capsule, Discrete, dt 0.02, 접촉 오프셋 0.01, constraints 80 | 낙하 "수정 중" 커밋(M5 정정: 낙하는 이전에 관측). 원래 Windows 실행 파일의 정확한 버전이라는 뜻은 아님 |
| H1 | `9b83a24` = `2dd8695^` / 05-29 | `ffd2c2e0be38…` | 개미 프리팹 Capsule, Continuous, Interpolate, dt 0.02, 접촉 오프셋 0.0001 | Box 전환 직전의 주 기준 |
| H2 | `442b99c` / 06-01 | `8fd1b6c726fc…` | H1과 같은 주 Capsule·Rigidbody 값. **PlayerMovement 파일 내용도 H1과 동일** | 바닥 콜라이더 재조정 1차 완료. 이후 연석 추가 작업은 별도 |
| H3 | `7cca610` = `dd2d880^` / 06-05 | `5617eccdef11…` | Capsule, Continuous, dt 0.02, 부스트 접촉 법선 처리 없음 | ContinuousDynamic 적용 직전 |
| H4 | `dd2d880` / 06-08 | `71b2dc714cf6…` | 주 Rigidbody 씬 오버라이드가 ContinuousDynamic으로 변경, dt 0.02 | CCD 적용 직후 |
| H5 | `9c71144` / 06-10 | `71b2dc714cf6…` | ContinuousDynamic 유지, dt 0.02, 부스트 접촉 법선 처리 추가 | 이동 코드 수정 후 |
| H6 | `d746a7b` / 06-16 | `a160f46591e2…` | dt 약 0.01로 변경. PlayerMovement 파일은 H5와 동일. 씬은 바이너리이므로 모든 최종 값은 에디터에서 재확인 필요 | 시간 간격 변경 후의 역사 구성 |
| H7 | `b8efce3526d3…` = 조사 시점 HEAD | `a160f46591e2…` | 현재 코드에는 마찰 0·자식 충돌 무시·추가 수정이 들어 있음. 기존 10-23 에디터 기록은 ContinuousDynamic, dt 0.009999993 | 현재 구성의 회귀 검증 |

읽은 각 시점의 `ProjectVersion.txt`는 모두 `6000.3.15f1`이다. 과거 소스 때문에 Unity를 다른 버전으로 올리거나 내릴 근거는 현재 없다. 각 버전의 `Packages/manifest.json`과 `packages-lock.json`은 별도로 복원해야 한다. Input System 1.19.0 / URP 17.3.0 / AI Navigation 2.0.12는 위 시점들에서 같았지만 전체 lock 파일은 일부 달랐다.

H0와 Hm1은 `MiniGameProject/` 아래에 프로젝트가 있던 시점이다. 현재 경로의 파일만 조회하면 파일이 없다는 잘못된 결론이 나온다. H1 이후는 루트 프로젝트 경로를 사용한다.

## 3. 실제 플레이어 Rigidbody와 Collider

### 3.1 낙하 수정 착수 시점 H0

M5 정정: 처음에는 H0를 최초 낙하 기록으로 적었으나, 커밋 메시지상 낙하는 H0 이전에 관측됐다. 직전 Hm1(`d3a9856`)의 같은 씬 플레이어 Rigidbody는 constraints 116이다.

`MiniGameProject/Assets/Scenes/AntDemoScene.unity`의 플레이어 객체 `1753235910`:

- Rigidbody `1753235911`: mass 1, linear damping 0, angular damping 0.05, use gravity 1, is kinematic 0, interpolation 0(None), collision detection 0(Discrete), constraints 80(X·Z 회전만 고정. Hm1의 116에서 FreezePositionY·FreezeRotationY 해제).
- CapsuleCollider `1753235912`: radius 0.5, height 2, center (0,0,0), direction 1(Y), Trigger 꺼짐, 별도 재질 없음.
- Transform: local scale (0.03,0.03,0.03), position (6.96,0.02,-15.685), 루트에 직접 배치.
- PlayerMovement 씬 값: moveSpeed 3, jumpForce 1.
- Fixed Timestep 0.02, default contact offset 0.01.

이때 플레이어도 Mesh였던 것은 아니다. **플레이어는 Capsule이고, 조사 대상 Mesh는 지형 충돌면이다.**

### 3.2 Box 전환 직전 H1 및 1차 완료 H2

에셋 파일 `Quirky Series_Ant/Insect Bundle/Insect Vol.1/Prefabs/Ant_LODs.prefab`의 동일 주 컴포넌트를 확인했다.

| 항목 | 직렬화 값 |
|---|---|
| 프리팹 GUID | `19e4e08e919e8014ebec0d397225440d` |
| 주 Rigidbody file ID | `8033754215170696240` |
| Capsule file ID | `1782073863268281047` |
| mass / damping | 1 / linear 0 / angular 0.05 |
| gravity / kinematic | 켜짐 / 꺼짐 |
| constraints | 112 |
| Collision Detection | 프리팹 값 1 = Continuous. 해당 씬에 다른 모드 오버라이드 없음 |
| Interpolation | 프리팹 0이지만 **씬 오버라이드 1 = Interpolate** |
| Capsule | radius 0.06266444, height 0.11960572, center (0,0.054116175,0.0019655228), Y축 |
| Capsule 재질 / Trigger | 별도 재질 없음 / 꺼짐 |
| 플레이어 scale | (0.15,0.15,0.15) |
| 씬 PlayerMovement 오버라이드 | jumpForce 6, yNormal 0.1 |
| dt / default contact offset | 0.02 / 0.0001 |

위 Capsule 수치는 로컬 직렬화 값이다. 높이와 지름의 관계, 런타임 형태 변경 때문에 실제 bounds는 실행 후 측정해야 한다. 단순히 height×scale만으로 유효 충돌 높이를 확정하지 않는다.

같은 프리팹에 자식 body Rigidbody `5185058614609499232`도 있고 그 모드는 Discrete다. **자식 값을 주 플레이어 값으로 오인하면 안 된다.** 주 캡슐과 자식 충돌체를 모두 복원해야 한다.

H3도 위 주 Capsule 값과 주 Rigidbody 모드를 유지한다. H4에서 주 Rigidbody의 씬 오버라이드가 2(ContinuousDynamic)로 바뀐다. H3→H4의 PlayerMovement 차이는 확인 범위에서 구르기 종료·부스트 시작 Debug.Log 제거이며, 부스트 접촉 법선 수정은 H5에 들어간다.

### 3.3 현재 값의 확인 범위

현재 원본 플레이어 프리팹의 주 Capsule 직렬화 값은 위 값과 같다. 그러나 현재 씬은 바이너리이고 씬 오버라이드·Awake의 변경도 있으므로, 프리팹 값만으로 현재 최종 상태를 확정하지 않는다.

기존 `Benchmarks/CurbStuckRuntimeMatrix/20261023_023059/setup.json` 및 `editor_after.json`에는 ContinuousDynamic, dt 0.009999993, 동일 캡슐 크기·scale이 기록되어 있다. 이는 이전 측정의 근거이며 이번에 새로 실행해서 확인한 값은 아니다. 승인 후 현재 씬의 최종 적용값을 다시 수집한다.

## 4. 맵 쪽 Mesh→기본 콜라이더 근거

H1의 에셋 커밋 `ffd2c2e0`에서 다음 원본을 확인했다.

| 에셋 경로 (`ithappy/Cartoon_City_Free/Prefabs/` 기준) | H1의 Collider |
|---|---|
| `Roads/road_009.prefab` | MeshCollider, enabled 1, Trigger 0, Convex 0, cooking options 26 |
| `Roads/road_019.prefab` | MeshCollider, enabled 1, Trigger 0, Convex 0, cooking options 26 |
| `Sidewalks/Set_B_Tiles_01.prefab` | MeshCollider, enabled 1, Trigger 0, Convex 0, cooking options 26 |

에셋 저장소의 `8fd1b6c7` 메시지는 **`[Fix] 땅 버그`**다. 도로 7종·인도 5종 프리팹을 바꾸며 연석 Box와 곡선용 Curb 자식들을 추가한다.

이 에셋 커밋의 하위 road 프리팹에는 Mesh가 여전히 남아 있다. 그러나 H2의 상위 `Assets/Prefabs/PostProcessingVolume.prefab`에는 `road_009`의 Mesh 컴포넌트 `-1141712254389156803`을 제거하는 `m_RemovedComponents` 기록이 존재한다. **하위 에셋만 보거나 현재 씬에서 임시로 Collider를 바꾸는 것으로 실제 구성을 복원할 수 없다.**

M5 보충: H0 시점(에셋 `23de6f3c`)의 도시 맵 MeshCollider 343개는 모두 기본 cooking(30)이다. 05-22 에셋 `de8bfd48`이 30→26으로 바꿨으므로 위 H1의 26은 그 뒤 값이다. H1에는 또 `5859367`(05-22)이 추가한 맵 전체 Ground 받침판 BoxCollider(`PostProcessingVolume.prefab`, 윗면 y=0)가 있어, 개미는 도로 MeshCollider가 아니라 받침판에 지지된다. `2dd8695`부터 Ground는 Box 없는 빈 부모가 된다. 근거 `Evidence/M5/M5-audit-01.json`, `M5-audit-02-timeline.json`, `Evidence/M5/probe-H0-colliders.json`.

복원 대상은 루트 씬 + 상위 맵 프리팹 + 해당 시점 에셋 저장소 + 임포트 설정 + 오버라이드 전체다. 직렬화 파일에서 직접 나타나는 Collider 개수는 상속된 컴포넌트를 포함하지 않으므로 맵 전체 개수로 보고하지 않는다. 바이너리 씬에서 텍스트 검색이 0건이어도 Collider가 없다는 뜻이 아니다.

## 5. 움직임 관련 변경

| 시점 | 확인한 동작/변경 | 테스트에 주는 영향 |
|---|---|---|
| Hm1 `d3a9856` / 05-18 16:40 | FixedUpdate에서 `MovePosition(position + move*dt)`만 수행. Look·Yaw·점프·접지 판정 없음. constraints 116 | Y 고정으로 낙하 불가. 회전은 FollowCamera가 덮어씀 |
| H0 `1a44741` / 05-18 18:05 | MovePosition에서 FixedUpdate의 linearVelocity 이동으로 변경. Y 속도 보존, ray 기반 접지, Yaw, 점프 추가. constraints 116→80, 씬 MeshCollider 2개 convex, Player 레이어·`_groundLayerMask` 추가 | 낙하 수정 중에 이동 방식이 달라짐. 현재 컨트롤러로 H0를 대신할 수 없음 |
| `4d73f8d` / 05-22 | 플레이어 y 하드코딩(`playerPosY` 0.17), 콜라이더 center 복원 | 연표에서 빠졌던 낙하 관련 변경(M5 보충) |
| `e4b5c50` / 05-26 | ray 접지에서 Ground/Road 태그 + 접촉 법선 HashSet으로 변경. default contact offset 0.01→0.0001 | 낙하·공중 정지·접지 판정 관련 별도 변경 |
| H1/H2 | FixedUpdate에서 수평 linearVelocity 대입, 기존 Y 보존. 접지 시 하강 Y=0. MoveRotation 사용. 부스트일 때 모드 None. 접촉 법선에 따른 벽 방향 속도 제거 없음 | 바닥 전환 비교의 코드 기준을 실제로 동일하게 둘 수 있음 |
| H5 `9c71144` | 부스트 중 접촉 법선 방향 이동 성분 제거, 부스트 모드 우선 처리 수정 | 끼임 개선을 CCD·dt 두 요인만으로 설명할 수 없음 |
| `966e5dd` | 연석의 Obstacle 레이어 지정 | 접촉 대상·레이어 행렬을 함께 복원해야 함 |
| `6235238` / 10-04 | 주 캡슐과 자식 non-trigger 충돌 무시, 주 캡슐 마찰 0, jumpForce 기본값 변경 | 현재 코드로 과거 설정을 흉내 내면 과거의 끼임이 가려질 수 있음 |
| 이후~HEAD | 앉기/일어서기 높이 처리, 접점 배열 처리, 코루틴→UniTask 등 | 재생성·타이머·입력·형태 초기화도 버전별 경로로 확인 필요 |

H1/H2의 벽타기 관련 `SetCrawling` 진입점도 확인했다. 당시 `Assets/Scripts/AntCrawl.cs`에는 주석 처리된 구현이 있으므로 파일명만 보고 벽타기가 활성화됐다고 단정하지 않는다. 복원 시 실제 활성 컴포넌트와 호출 경로를 조사한다.

## 6. 실행 전 남은 감사

1. H0의 미확정 에셋 참조와 원래 낙하 빌드 버전 확인. 확보되지 않으면 최초 기록 시점 자료로만 남기고 정확한 원본 복원이라고 부르지 않음.
2. H1~H7 각각의 씬/프리팹 상속을 Unity가 해석한 최종 상태 수집. binary 씬도 원본 그대로 불러와 조사.
3. 모든 Rigidbody·Collider·PhysicsMaterial·layer/tag·collision matrix·IgnoreCollision 및 Animator/root motion 기록. 주 캡슐 외 body도 포함.
4. 초기화 전후, 정지/달리기/앉기/구르기별 Capsule과 controller 적용값 수집.
5. Standalone backend 확인. 조사한 ProjectSettings에는 Android의 IL2CPP만 명시되고 Standalone 값은 명시되지 않아, 당시 Windows backend를 아직 확정하지 않음.
6. 초기화 코드·부스트 효과 에셋·입력 액션·플레이어 제어 차단 코드를 포함한 버전별 동작 확인.

M5 기준 상태(2026-10-31):

- 1번: H0 에셋은 `23de6f3c` 추정으로 복원(원본 2,429개 일치), 직전 Hm1도 복원. 원래 낙하 빌드 버전은 여전히 미확보이므로 H0·Hm1을 정확한 원본이라고 부르지 않는다
- 2~4번: H0·Hm1·H1·H7은 복원 후 Mac·Windows 실행 기록이 있다. H2~H6은 디스크 승인 전이라 아직 복원하지 않았다
- 5번: H1은 M2에서 Mono2x 확인. 다른 버전의 당시 backend는 미확정

이 감사 이후 원본 상태를 보존한 역사 비교를 실행한다. 비교를 쉽게 하려고 과거에 현재의 마찰 제거·접촉 법선 로직·입력 구조를 이식하지 않는다. 관측용 코드가 필요하면 별도 계측 변경으로 기록한다.
