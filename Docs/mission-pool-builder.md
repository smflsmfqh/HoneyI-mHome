# 미션 배분 로직 분리 (MissionPoolBuilder)

프로젝트: `HoneyI-mHome` (Unity 6, C#)
성격: 출시 후 리팩토링 — 게임 동작은 바꾸지 않는다.

## 배경

`MissionManager.BuildMissionPool()`이 미션 배분 규칙을 직접 갖고 있었다.

1. 고정 미션(약국·아이스크림)을 `_fixedUnlockTimes` 시각에 넣는다.
2. `_randomUnlockTimes`를 Fisher-Yates로 섞는다.
3. 무작위 그룹마다 후보 하나를 뽑아 섞인 시각을 앞에서부터 배정한다.
4. A·B 그룹 중 하나에서 뽑히지 않은 후보를 보너스 미션(`float.MaxValue`)으로 둔다.
5. 시각순으로 정렬한다.

같은 클래스에 진행 상태(`_assigned`, `_completed`, `_elapsedTime`, 보너스 트리거, 히든 미션 해금)도 같이 있었다.

문제:

- `UnityEngine.Random`을 직접 쓰고 MonoBehaviour 안에 있어서, 배분 결과가 규칙대로인지 보려면 **씬을 실행해야 했다**.
- 개발 중에는 `GameManager` 디버그 키 → `DebugCompleteAll`로 확인했다. 판마다 결과가 달라서 특정 경우를 다시 만들 수 없었다.
- `_randomUnlockTimes` 개수가 그룹 수보다 적으면 남는 그룹이 조용히 0초에 배정된다. 그런데 이를 알려 주는 경고가 없었다.

## 분리한 구조

```
Assets/Scripts/Mission/Core/            ← HoneyIMHome.Mission.Core.asmdef (noEngineReferences, autoReferenced)
  MissionPoolBuilder.cs                 ← 배분 규칙 (순수 C#, System.Random 주입)
Assets/Scripts/MissionManager.cs        ← 진행 상태만. 빌더 결과를 받아 필드에 대입
Assets/Tests/EditMode/                  ← HoneyIMHome.Mission.Tests.asmdef (Editor 전용)
  MissionPoolBuilderTests.cs
```

```csharp
public static MissionPool<T> Build<T>(
    IReadOnlyList<T> fixedItems, IReadOnlyList<float> fixedTimes,
    IReadOnlyList<IReadOnlyList<T>> randomGroups, IReadOnlyList<float> randomTimes,
    int bonusGroupCount, System.Random rng) where T : class

// MissionPool<T>: T[] Items, float[] UnlockTimes, int BonusIndex(-1 = 없음), IReadOnlyList<string> Warnings
```

- `ItemData`에 의존하지 않는 제네릭 구조라서 테스트에서는 `string`으로 검증한다.
- 알고리즘은 기존 코드를 그대로 옮겼다. 다음 세 가지가 모두 같다.
  - 난수 호출 순서: 셔플 → 보너스 그룹 → 그룹별 추첨, 보너스 그룹에서는 탈락 후보 추첨
  - 정렬: `List.Sort`. 동률일 때의 순서까지 같다.
  - null·빈 그룹 처리: 빈 그룹은 건너뛰고 시각 슬롯도 소비하지 않는다.
- `UnityEngine.Random.Range(a, b)`(int, b 미포함)는 의미가 같은 `rng.Next(a, b)`로 바꿨다.
- `MissionManager`는 `new System.Random()`으로 매 판 다른 시드를 쓰고 `BONUS_GROUP_COUNT = 2`를 넘긴다("A·B 중" 규칙).
- 데이터 개수 불일치는 `Warnings`에 담고, `MissionManager`가 `Debug.LogWarning(…, this)`로 출력한다. 같은 검사(`MissionPoolBuilder.CollectWarnings`)를 `OnValidate`(에디터 전용)에서도 돌린다. 그래서 인스펙터에서 값을 바꾸면 바로 경고가 보인다. 이때 필드는 수정하지 않는다.
  - 고정 미션과 고정 시각 개수가 다를 때: 짧은 쪽까지만 배분된다.
  - 무작위 시각이 비어 있지 않은 그룹 수보다 적을 때: 남는 그룹은 0초에 배정된다.
- `MissionManager`의 `[SerializeField]` 필드는 그대로다. 씬 값(`_fixedUnlockTimes {20,90}`, `_randomUnlockTimes {40,65,80}`, 그룹 3개×후보 2개)도 그대로 쓰인다.

## 테스트 항목 (EditMode, 시드 0~199)

| 테스트 | 확인 내용 |
|---|---|
| `FixedMissions_KeepTheirUnlockTimes` | 고정 미션이 지정 시각 그대로 들어간다 |
| `EachRandomGroup_ContributesExactlyOneMission` | 무작위 그룹마다 후보가 정확히 하나 들어간다 |
| `Bonus_IsUnpickedCandidateOfFirstTwoGroups_AtMaxValue_Last` | 보너스는 A·B 중 한 그룹의 뽑히지 않은 후보이고, `float.MaxValue`이며 맨 뒤에 있다 |
| `UnlockTimes_ExceptBonus_AreAscending` | 보너스를 뺀 시각이 오름차순이다 |
| `RandomMissionTimes_AreShuffledRandomTimes` | 무작위 미션의 시각 집합이 `randomTimes`와 같다 |
| `SameSeed_ProducesSameResult` | 시드가 같으면 결과도 같다 |
| `DifferentSeeds_ProduceMultipleCombinations` | 그룹별 선택 조합이 2가지 이상 나온다 |
| `SettingsMatchingScene_ProduceNoWarnings` | 씬과 같은 구성이면 경고가 없다 |
| `TooFewRandomTimes_WarnsAndLeftoverGroupGetsZero` | 시각이 부족하면 경고가 나고 남는 그룹은 0초다 (현재 동작 고정) |
| `FixedCountMismatch_WarnsAndTruncatesToShorter` | 고정 개수가 다르면 경고가 나고 짧은 쪽으로 잘린다 |
| `NoGroups_HasNoBonus` / `EmptyBonusGroups_HaveNoBonus` / `SingleCandidateBonusGroups_HaveNoBonus` | 그룹이 없거나, 비었거나, 후보가 1개면 `BonusIndex == -1` |
| `NullInputs_ProduceEmptyPool` | 입력이 전부 null이어도 예외가 나지 않는다 |

실행: Window > General > Test Runner > EditMode > `HoneyIMHome.Mission.Tests`.
실행 전에 열린 씬이 dirty가 아닌지 확인한다. Test Runner가 실행 전에 수정된 씬을 저장할 수 있다. 실제로 이 작업 중에 `AntDemoScene`이 저장된 적이 있다.

## 특정 판 재현 (시드)

- `MissionManager`는 매 판 `Environment.TickCount`를 시드로 쓰고, 콘솔에 `[MissionManager] mission seed = N`을 남긴다.
- 에디터에서 같은 배분을 다시 보려면 아래 순서를 따른다.
  1. **Tools > Mission Seed** 창을 연다.
  2. `시드 덮어쓰기`를 켠다.
  3. 시드 칸에 N을 넣는다. 방금 플레이한 판이면 `마지막 판 시드 넣기`를 누른다.
  4. 플레이한다.
- 덮어쓰기가 켜져 있는 동안은 매 판 `미션 시드 덮어쓰기 사용 중` 경고가 뜬다. 재현이 끝나면 끈다.
- 값은 EditorPrefs(`HoneyIMHome.MissionSeed.*`)에 저장된다. 씬이나 직렬화 필드는 건드리지 않는다. 빌드에서는 덮어쓰기 코드가 빠지고 로그 한 줄만 남는다.
- 재현되는 범위는 **미션 배분**(종류·해금 시각·보너스)뿐이다. NPC·고양이·차량 스폰은 `UnityEngine.Random`을 쓰므로 판마다 다르다.
- 플레이 없이 보려면 EditMode 테스트에서 씬과 같은 입력에 `new System.Random(N)`을 넣는다.

## 게임 동작 변화 없음 확인 방법

1. EditMode 테스트 전부 통과.
2. `AntDemoScene` 플레이 → 2~3초 뒤 `MissionManager`의 `_missionPool`·`_unlockTimes`·`_bonusIdx`를 리플렉션으로 읽는다. 기대값은 아래와 같다.
   ```
   [고정 20s, 무작위(40·65·80 셔플), 고정 90s, 보너스 MaxValue]
   ```
   예: `ENERGYDRINK@20, AVOCADO@40, DONUT@65, EGG@80, ICECREAM@90, MUSHROOM@MaxValue (bonusIdx=5)`
3. 여러 번 플레이해서 조합이 판마다 달라지는지 본다.
4. 콘솔에 `[MissionManager]` 경고가 없어야 한다. 경고가 있으면 씬 데이터 개수가 어긋난 것이다.

남는 차이: 난수원이 `UnityEngine.Random`에서 `System.Random`으로 바뀌었다. 판마다 나오는 구체적 조합은 달라지지만 분포와 규칙은 같다. 프로젝트 어디에서도 `Random.InitState`로 시드를 고정하지 않으므로 재현성 면에서 잃는 것은 없다.
