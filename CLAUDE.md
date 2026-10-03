# 개미 심부름 — 프로젝트 가이드

## 게임 컨셉

**장르**: 3D 캐주얼 코믹 스텔스 어드벤처  
**플랫폼**: PC (Windows)  
**시점**: 3인칭 (개미 등 뒤 추격 카메라)  
**한 판 플레이**: 10분  

> "어제 회식에서 만취해 집에 못 들어간 아빠 개미가, 분노한 아내의 메신저 폭격을 받으며 도시에서 심부름을 마치고 집으로 황급히 돌아간다."

플레이어는 거대한 사람들의 발 사이를 피해(Sprint/Roll/Jump/Hide), 도시 곳곳의 상점에서 아내가 시킨 심부름 아이템을 E키로 픽업하고 집(개미굴)으로 귀가하면 클리어. 체력 0이면 게임 오버.

---

## 기술 스택

| 항목 | 내용 |
|------|------|
| 엔진 | Unity 6.3 (6000.3.15f1) |
| 렌더 파이프라인 | URP (Universal Render Pipeline 17.3.0) |
| 입력 | New Input System (1.19.0) |
| 카메라 | Cinemachine 3.x |
| NPC AI | NavMesh (com.unity.ai.navigation 2.0.12) |
| 트위닝 | DOTween |
| 언어 | C# |
| 버전 관리 | Git — `git@github.com:Devel-Rocket-ClassRoom/minigame-project-smflsmfqh.git` |

---

## 현재 코드베이스 상태

> **1주차 진행 중** — 임시(프로토타입) 스크립트로 데모씬 구동 중. 아래 스크립트들은 최종 아키텍처로 교체될 예정.

### 현재 스크립트 (`Assets/Scripts/`)

| 파일 | 클래스 | 역할 | 비고 |
|------|--------|------|------|
| `PlayerMovement.cs` | `PlayerMovement` | WASD 이동, 마우스 카메라, 점프 (moveSpeed: 3, jumpForce: 1) | 임시 — `AntController`로 교체 예정 |
| `FollowCamera.cs` | `FollowCamera` | 3인칭 팔로우 카메라, 마우스 휠 줌 (기본 거리: 0.35) | 임시 — Cinemachine으로 교체 예정 |
| `PatrolTest.cs` | `PatrolCharacter` | A↔B 두 지점 왕복 순찰, 지점 도착 시 1초 대기 | 임시 — NavMeshAgent 기반 `PatrollerNPC`로 교체 예정 |

### 현재 씬

- `Assets/Scenes/AntDemoScene.unity` — 플레이어(Capsule), FollowCamera, NPCSpawner, StoreMap, IceCreamCart 배치된 데모 씬

---

## 계획된 아키텍처 (GDD v2.3)

### 폴더 구조 (목표)

```
Assets/
  _Project/
    Input/
      AntInputActions.inputactions   ← New Input System 액션 자산
      InputReader.asset              ← ScriptableObject 인스턴스
      InputReader.cs
    Player/
      AntController.cs               ← WASD 이동 + Sprint
      AntRoller.cs                   ← 구르기 (무적 + 쿨다운)
      AntInteractor.cs               ← E키 픽업
      Ant.prefab
    Camera/
      AntFollowCamera.prefab         ← Cinemachine 3rd person
      CameraZoomController.cs        ← 마우스 휠 줌
    NPC/
      PatrollerNPC.cs                ← NavMeshAgent 왕복
      RunnerNPC.cs                   ← 빠른 직선 이동
      SpawnManager.cs                ← 스폰 포인트 N개 랜덤 활성화
    Systems/
      HealthSystem.cs                ← 체력 4칸, 자동 회복
      SafeZone.cs                    ← 밟힘 면제 + 회복 가속
      HideSpot.cs                    ← F 토글, NPC 시야 차단
      ProximityFeedbackSystem.cs     ← NPC 거리 기반 intensity
      CameraShakeController.cs
      GroundParticleController.cs
      SoundPitchController.cs
    Mission/
      MissionItem.cs                 ← IInteractable 구현
      PickupSystem.cs                ← E키 픽업 처리
      MissionManager.cs
    UI/
      KakaoTalkUI.cs                 ← 일시정지 컷씬 (Time.timeScale=0)
      DirectionArrowUI.cs
      HealthBarUI.cs
      MissionChecklistUI.cs
  Prefabs/
    CharacterPrefabs/                ← Character 1~25.prefab (현재 존재)
    IceCreamCart.prefab
    Playground.prefab
  Scenes/
    AntDemoScene.unity               ← 현재 데모 씬
    GameScene.unity                  ← 목표 최종 씬
```

---

## 컴포넌트별 핵심 설계

### InputReader (ScriptableObject — 입력 SSOT)

모든 게임플레이 스크립트는 `Keyboard.current` 등 디바이스 API를 직접 쓰지 않고 `InputReader` 이벤트를 구독.

| 액션 | 키 | 설명 |
|------|----|------|
| Move | WASD / 방향키 | Vector2, 카메라 forward 기준 이동 |
| Look | 마우스 이동 | Vector2, 카메라 yaw/pitch |
| Zoom | 마우스 휠 | float, 카메라 거리 조절 |
| Sprint | LShift (Hold) | 이동 속도 ×1.5 (스태미나 없음) |
| Jump | Space | 길가 장애물 회피용 도약 |
| Roll | LCtrl | 사람 발 회피, 무적 프레임 포함 |
| Hide | F | 매대 밑 진입/이탈 토글 |
| Interact | E | 아이템 픽업 |
| Pause | Esc | UI 맵으로 전환 |

컷씬/포즈 시 `Player` 액션 맵 → `UI` 액션 맵으로 스위칭 (`Time.timeScale=0` 환경에서 UI Animator는 `Unscaled Time` 사용).

### AntController + 파생 컴포넌트

- **이동**: 카메라 yaw 기준 forward/right 계산, CharacterController 또는 Rigidbody
- **구르기(AntRoller)**: 무적 0.3s + 쿨다운 0.6s, 이동 방향 우선, 이동 거리 1.2m
- **픽업(AntInteractor)**: OverlapSphere 반경 1m, 가장 가까운 `IInteractable` 후보 → E 입력 시 `Interact(this)` 호출

### 카메라 (Cinemachine 3.x)

| 항목 | 값 |
|------|-----|
| 등 뒤 거리 | 0.3m (기본) |
| 앵글 | 머리·하늘 향한 대각선 위쪽 |
| 줌 범위 | 0.15m ~ 0.6m (CameraZoomController) |
| 회전 | 마우스 yaw → 카메라 + 캐릭터 동시 회전 |

`CinemachineFollow` + `CinemachineRotationComposer` + `CinemachineInputAxisController` 조합.

### NPC 시스템

**2종 타입**

| 타입 | 동작 | 난이도 | 회피 전략 |
|------|------|--------|----------|
| 패트롤러 | NavMeshAgent 경로 왕복 | 쉬움 | 패턴 예측, 안전지대 |
| 러너 | 빠른 속도 직선/곡선 | 어려움 | 구르기(LCtrl), 매대 밑 숨기(F) |

**SpawnManager**: 스폰 포인트 7~10개 사전 배치 → 매 플레이마다 N명 무작위 활성화 (초기값: 러너 1 + 패트롤러 3~4). 사람 NPC 프리팹 태그는 `Npc` (`NPC`는 태그가 아니라 레이어 이름). 근접 경고 대상 수집에는 태그를 쓰지 않는다 — 아래 코딩 주의사항 참고.

### 체력 시스템 (HealthSystem)

| 항목 | 값 |
|------|-----|
| 체력 | 4칸 (25%씩, 총 100%) |
| 피격 데미지 | -25% (한 칸) |
| 피격 무적 | 0.7s |
| 자동 회복 시작 | 마지막 피격 후 3s 지연 |
| 자동 회복 속도 | 초당 +4% (일반), +10% (안전지대) |
| 구르기 무적 | 0.3s (피격 무적과 별개) |

### ProximityFeedbackSystem

NPC와의 최소 거리를 intensity(0~1)로 환산, 3채널이 독립 구독.

```
intensity = 1 - clamp((minDist - panicRadius) / (dangerRadius - panicRadius), 0, 1)
```

| 파라미터 | 기본값 |
|---------|--------|
| dangerRadius | 8m |
| panicRadius | 2m |
| 카메라 흔들림 maxShake | 0.08 |
| 카메라 흔들림 frequency | 18 |
| 파티클 maxEmissionRate | 40 |
| 사운드 피치 범위 | 0.8 ~ 1.4 |

`ProximityFeedbackSystem`은 플레이어 오브젝트에 부착. `CameraShakeController`는 Cinemachine VirtualCamera 자식에 부착.

### 미션 시스템

- 게임 시작 시 아내 카톡으로 미션 2개 부여 (도넛, 립스틱)
- 게임 중반 추가 미션 카톡 이벤트 ("아 맞다, 우유도!")
- 상점 주변 1~2m 반경에 아이템 배치 → E키 픽업 (단순 체크 처리, 인벤토리 없음)
- 카톡 UI 트리거 조건: 90s 간격 + 반경 3m 내 NPC 없을 때만 발동

### 미션 아이템

| 아이템 | 위치 | 구분 |
|-------|------|------|
| 도넛 | 빵집 입구 | 기본 |
| 립스틱 | 화장품 가게 | 기본 |
| 우유 | 편의점 | 중반 추가 |
| 꽃다발 | 꽃집 | 선택 |

---

## 에셋 목록 (Asset Store)

| 용도 | 에셋명 |
|------|--------|
| 도시 맵 | Cartoon City Free — Low Poly City 3D Models Pack |
| 사람 NPC | Creative Characters Free Animated Pack |
| 개미 (플레이어/아내) | Ant Quirky Series |

현재 `Assets/minigame-project-assets-smflsmfqh/` (445MB)에 임포트 완료.

---

## 개발 스케줄 (3주)

| 주차 | 테마 | 핵심 목표 |
|------|------|---------|
| **1주차** | 맵 세팅 & 이동 시스템 | 맵 구성 + NavMesh + NPC 패트롤/스폰 + 개미 이동 + 카메라 |
| **2주차** | 게임 시스템 & UI | 체력·안전지대·미션·카톡UI + 캐릭터 에셋 연결 + 근접피드백 1차 |
| **3주차** | 폴리싱 & 빌드 | 사운드·시네마틱·밸런싱·버그픽스·최종 빌드 |

**체크포인트**
- 1주차 말: 맵 위에서 개미가 자유롭게 움직이고, NPC가 돌아다니며 밟는다
- 2주차 말: 체력이 깎이고, 아이템을 줍고, 카톡 컷씬이 뜨고, 캐릭터 에셋이 보인다
- 3주차 말: 소리·이펙트·시네마틱까지 완성된 빌드가 나온다

---

## MVP 범위 (Must/Should/Could)

### Must (필수)
- 개미 3D 이동 + 3인칭 카메라 (등 뒤 0.3m, 마우스 휠 줌)
- Sprint(LShift) / Jump(Space) / Roll(LCtrl) / Hide(F) 4가지 액션
- NPC 2종 (패트롤러/러너) + 밟기 충돌 판정
- 체력 바 (4칸) + 자동 회복
- 안전지대 (밟힘 면제 + 회복 가속)
- 매대 밑 은신처 (F 토글, 러너 카운터플레이)
- 도시 맵 1개 + 제약된 랜덤 스폰
- 아내 카톡 UI (일시정지 컷씬, 미션 부여)
- 미션 아이템 픽업 (E키)
- ProximityFeedbackSystem (카메라 흔들림·파티클·사운드 피치)
- 방향 화살표 UI + 오프닝 시네마틱

### Should (가능하면)
- 구르기 슬로우모션 + 잔상 이펙트
- 체력 25% 이하 적색 비네팅 + BGM 변화
- 중간 추가 미션 카톡 이벤트

### Could (여유 있으면)
- 페로몬 트레일 (화살표 대체)
- 시간 제한 + 아내 등장 분노 컷씬
- 다양한 NPC 타입 (자전거, 강아지 등)

---

## 코딩 주의사항

- **입력은 InputReader에서만**: 게임플레이 스크립트가 `Keyboard.current` / `Mouse.current` 직접 참조 금지. `InputReader` C# 이벤트 구독으로 처리.
- **NPC 태그·레이어 구분**: 사람 NPC 태그는 `Npc`, 레이어는 `NPC`. 프로젝트에 `"NPC"` 태그는 정의되어 있지 않으므로 `FindGameObjectsWithTag("NPC")`는 `UnityException`을 던지고, `CompareTag("NPC")`도 "Tag is not defined" 에러를 낸다. 고양이·차량 프리팹의 `Danger` 태그는 에디터 구분용이며 코드에서 참조하지 않는다.
- **근접 경고 대상은 자체 등록**: `ProximityFeedback`은 태그 검색을 하지 않는다. 위험 오브젝트가 스스로 `RegisterDanger` / `UnregisterDanger`를 호출한다 (현재 `CatMovement`·`CarMovement`의 `Start`/`OnDestroy`). 스크립트 실행 순서에 의존하지 않기 위함. 풀링(`SetActive` 재사용) 대상에 적용할 때는 `OnEnable`/`OnDisable`에서 등록·해제할 것. 자세한 배경은 `Docs/proximity-danger-registration.md`.
- **컷씬 일시정지**: `Time.timeScale = 0` 사용 시 UI Animator의 `Update Mode`를 `Unscaled Time`으로 설정.
- **수치는 밸런스 시작값**: GDD의 체력/구르기 무적/카메라 거리 등 모든 수치는 플레이테스트 후 조정 대상.
- **ProximityFeedbackSystem 채널 독립**: 채널(카메라/파티클/사운드) 간 직접 참조 없이 intensity float만 구독. 채널 추가/제거가 자유로워야 함.
