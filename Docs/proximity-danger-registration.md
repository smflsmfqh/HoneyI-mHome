# ProximityFeedback 위험 등록 방식 정리

프로젝트: `HoneyI-mHome` (Unity 6, C#)
대상 브랜치: 현재 작업 브랜치 (작업 전 `git status` 확인 — `Assets/Scripts/Player/PlayerMovement.cs`에 커밋되지 않은 별도 수정이 있음. 이번 작업과 섞지 말 것)

## 배경

`ProximityFeedback`은 플레이어와 가장 가까운 위험(고양이·차량)까지의 거리로 경고 강도를 계산한다.
위험 목록은 6/16 `d746a7b`에서 "0.2초마다 `FindGameObjectsWithTag`"에서 "Start에서 한 번 수집 + Register/Unregister"로 바뀌었다.

현재 구조:

```csharp
// Assets/Scripts/ProximityFeedback/ProximityFeedback.cs
private readonly List<Transform> _dangers = new();

private void Start()
{
    foreach (var go in GameObject.FindGameObjectsWithTag("Danger"))
        _dangers.Add(go.transform);
}

public void RegisterDanger(Transform t) => _dangers.Add(t);
public void UnregisterDanger(Transform t) => _dangers.Remove(t);
```

- 고양이·차량은 `CatSpawnManager.Start` / `CarSpawnManager.Start`에서 `Instantiate`된다. 프리팹에 `Danger` 태그가 붙어 있다.
- 두 스폰 매니저에는 `[DefaultExecutionOrder(-1)]`이 있어서 `ProximityFeedback.Start`보다 먼저 실행된다. 그래서 **현재는 정상 동작한다** (게임에서 고양이 접근 시 경고 표시 확인됨).
- 다만 이 속성은 6/16 `a0b9620`에서 "빌드 환경 NPC 이동 버그" 때문에 추가된 것이다. 위험 감지가 이 순서에 기대고 있다는 사실은 코드 어디에도 드러나 있지 않다.
- 튜토리얼 고양이는 `TutorialManager`가 런타임에 생성하므로 이미 `RegisterDanger` / `UnregisterDanger`를 직접 호출한다.
- `Danger` 태그는 `Cat.prefab`과 차량 프리팹 6종의 루트에만 있고, 씬에 직접 배치된 Danger 오브젝트는 없다. 따라서 Start 수집을 제거해도 빠지는 위험 대상은 없다.
- `a0b9620`은 `NPCSpawnManager`(사람 NPC)에도 같은 속성을 추가했지만, 사람 NPC는 Danger 태그가 없어 이번 작업과 무관하다.

## 목표

위험 목록에 들어가는 경로를 **명시적 등록 하나**로 통일해서, 스크립트 실행 순서와 태그 검색에 의존하지 않게 한다.
등록 책임은 스폰 매니저가 아니라 **위험 오브젝트 자신**(`CatMovement` / `CarMovement`)이 진다. 두 스크립트는 이미 `Start`에서 플레이어의 `ProximityFeedback`을 `_proximity`로 가져오고 있으므로, 인스펙터 연결이나 새 필드 없이 등록할 수 있다.
동작(경고가 뜨는 조건, 강도 계산식, 이벤트 발행 조건)은 바꾸지 않는다.

## 수정 사항

### 1. `Assets/Scripts/ProximityFeedback/ProximityFeedback.cs`

- `Start()`의 `FindGameObjectsWithTag("Danger")` 수집을 제거한다 (`Start` 메서드 자체를 삭제).
- `RegisterDanger`에 null·중복 방어를 넣는다 (튜토리얼 고양이는 `TutorialManager`와 `CatMovement` 양쪽에서 등록되므로 중복 방어가 필요).
- `CalcMinDist`에서 파괴된 Transform을 건너뛴다 (안전망 — 정상 경로에서는 `OnDestroy`에서 해제됨).

```csharp
public void RegisterDanger(Transform t)
{
    if (t == null || _dangers.Contains(t))
        return;
    _dangers.Add(t);
}

public void UnregisterDanger(Transform t) => _dangers.Remove(t);

private float CalcMinDist()
{
    float minDistSq = float.MaxValue;
    Vector3 myPos = transform.position;

    for (int i = _dangers.Count - 1; i >= 0; i--)
    {
        Transform t = _dangers[i];
        if (t == null) // Unregister 없이 파괴된 경우
        {
            _dangers.RemoveAt(i);
            continue;
        }

        float dSq = (myPos - t.position).sqrMagnitude;
        if (dSq < minDistSq)
            minDistSq = dSq;
    }

    return minDistSq == float.MaxValue ? float.MaxValue : Mathf.Sqrt(minDistSq);
}
```

`Update`, `CalcIntensity`, 이벤트(`OnIntensityChanged`) 발행 조건은 건드리지 않는다.

### 2. `Assets/Scripts/NPC/Cat/CatMovement.cs`

`Start`에서 `_proximity`를 얻은 직후 자신을 등록하고, `OnDestroy`에서 해제한다.
`_player`는 `CatSpawnManager` / `TutorialManager`가 `Instantiate` 직후 `SetPlayer`로 넣어주므로 `Start` 시점에 이미 채워져 있다.

```csharp
private void Start()
{
    // ... 기존 코드
    if (_player != null)
    {
        _playerMovement = _player.GetComponent<PlayerMovement>();
        _proximity = _player.GetComponent<ProximityFeedback>();
    }
    if (_proximity != null)
        _proximity.RegisterDanger(transform);
    // ...
}

private void OnDestroy()
{
    if (_proximity != null)
        _proximity.UnregisterDanger(transform);
}
```

### 3. `Assets/Scripts/NPC/Car/CarMovement.cs`

`Start`에서 `Player` 태그로 `_proximity`를 얻은 직후 등록, `OnDestroy`에서 해제한다.

```csharp
var player = GameObject.FindGameObjectWithTag("Player");
if (player != null)
    _proximity = player.GetComponent<ProximityFeedback>();
if (_proximity != null)
    _proximity.RegisterDanger(transform);

private void OnDestroy()
{
    if (_proximity != null)
        _proximity.UnregisterDanger(transform);
}
```

`?.` 대신 `!= null`을 쓴다. 씬 종료 시 `ProximityFeedback`이 먼저 파괴된 경우 `?.`는 Unity의 null 판정을 우회하기 때문이다.

### 4. 유지할 것

- `CatSpawnManager` / `CarSpawnManager`는 **수정하지 않는다**. 직렬화 필드 추가·인스펙터 연결 작업도 없다.
- 두 스폰 매니저의 `[DefaultExecutionOrder(-1)]`은 **그대로 둔다** (NPC 이동 버그 수정용으로 추가된 것이라 이번 작업과 별개).
- 프리팹의 `Danger` 태그는 남겨둔다. 스크립트에서 쓰는 곳은 없어지지만 에디터에서 구분용으로 남겨도 무방하다. 지울지는 사용자에게 확인.
- `TutorialManager`의 Register / Unregister 호출은 그대로 둔다 (중복 방어로 무해, `OnDestroy`의 해제와 겹쳐도 `Remove`는 no-op).

## 확인 방법 (에디터에서 직접)

1. 플레이 → 일반 고양이에게 다가갈 때 화면 경고 패널과 근접 시 카메라 흔들림이 뜨는지
2. 차량에 다가갈 때 경고와 경적이 이전과 같은지
3. 튜토리얼 고양이 생성 → 경고가 뜨고, 사라진 뒤 콘솔에 `MissingReferenceException`이 없는지
4. 빌드 후 같은 확인 (실행 순서 문제는 빌드에서 드러나는 경우가 있었음)

## 커밋

사용자 확인 후 커밋. 메시지 예시:

```
[Refactor] ProximityFeedback 위험 등록을 위험 오브젝트 자체 등록으로 통일

- Start의 FindGameObjectsWithTag("Danger") 수집 제거
- CatMovement/CarMovement가 Start에서 RegisterDanger, OnDestroy에서 UnregisterDanger
- RegisterDanger 중복·null 방어, CalcMinDist에서 파괴된 Transform 제거
```
