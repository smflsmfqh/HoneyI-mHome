using System;
using System.Collections.Generic;

public sealed class MissionPool<T>
    where T : class
{
    public T[] Items { get; }
    public float[] UnlockTimes { get; }

    // 보너스 미션 인덱스, 없으면 -1
    public int BonusIndex { get; }
    public IReadOnlyList<string> Warnings { get; }

    public MissionPool(T[] items, float[] unlockTimes, int bonusIndex, IReadOnlyList<string> warnings)
    {
        Items = items;
        UnlockTimes = unlockTimes;
        BonusIndex = bonusIndex;
        Warnings = warnings;
    }
}

// 미션 배분 규칙. 난수 생성기를 주입받아 시드 고정 테스트가 가능하다.
// 고정 미션 → 무작위 그룹 추첨(시각 슬롯 Fisher-Yates 셔플) → 앞 bonusGroupCount개 그룹 중 하나의 탈락 후보를 보너스로 → 시각순 정렬
public static class MissionPoolBuilder
{
    public static MissionPool<T> Build<T>(
        IReadOnlyList<T> fixedItems,
        IReadOnlyList<float> fixedTimes,
        IReadOnlyList<IReadOnlyList<T>> randomGroups,
        IReadOnlyList<float> randomTimes,
        int bonusGroupCount,
        Random rng
    )
        where T : class
    {
        var warnings = CollectWarnings(
            fixedItems?.Count ?? 0,
            fixedTimes?.Count ?? 0,
            CountNonEmptyGroups(randomGroups),
            randomTimes?.Count ?? 0
        );

        var pool = new List<(T item, float time)>();

        // 고정 미션
        int fixedCount = Math.Min(fixedItems?.Count ?? 0, fixedTimes?.Count ?? 0);
        for (int i = 0; i < fixedCount; i++)
            pool.Add((fixedItems[i], fixedTimes[i]));

        // randomTimes를 Fisher-Yates로 셔플
        var timeSlots = new List<float>(randomTimes ?? Array.Empty<float>());
        for (int i = timeSlots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (timeSlots[i], timeSlots[j]) = (timeSlots[j], timeSlots[i]);
        }

        int groupCount = randomGroups?.Count ?? 0;

        // 앞 bonusGroupCount개 그룹 중 하나를 추가 미션 후보 그룹으로 결정
        int bonusGroupIdx = rng.Next(0, Math.Min(bonusGroupCount, groupCount));
        T bonusItem = null;
        int timeSlotIdx = 0;

        for (int i = 0; i < groupCount; i++)
        {
            var candidates = randomGroups[i];
            if (candidates == null || candidates.Count == 0)
                continue;

            // 시각 슬롯이 모자라면 0초 (CollectWarnings에서 경고)
            float unlockTime = timeSlotIdx < timeSlots.Count ? timeSlots[timeSlotIdx++] : 0f;
            int picked = rng.Next(0, candidates.Count);
            pool.Add((candidates[picked], unlockTime));

            // 보너스 그룹의 탈락 후보 수집
            if (i == bonusGroupIdx && candidates.Count > 1)
            {
                var remaining = new List<T>(candidates);
                remaining.RemoveAt(picked);
                bonusItem = remaining[rng.Next(0, remaining.Count)];
            }
        }

        // 보너스 아이템을 풀 끝에 추가 (unlockTime = float.MaxValue → 트리거 전까지 미부여)
        int bonusIdx = -1;
        if (bonusItem != null)
        {
            bonusIdx = pool.Count;
            pool.Add((bonusItem, float.MaxValue));
        }

        // unlockTime 기준 정렬 (보너스는 항상 맨 뒤)
        pool.Sort((a, b) => a.time.CompareTo(b.time));

        // 정렬 후 보너스 인덱스 재탐색
        var items = new T[pool.Count];
        var unlockTimes = new float[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            items[i] = pool[i].item;
            unlockTimes[i] = pool[i].time;
            if (pool[i].time >= float.MaxValue)
                bonusIdx = i;
        }

        return new MissionPool<T>(items, unlockTimes, bonusIdx, warnings);
    }

    // 데이터 개수 불일치 검사. Build와 MissionManager.OnValidate가 같이 쓴다.
    // nonEmptyGroupCount: 후보가 1개 이상인 그룹 수 (빈 그룹은 시각 슬롯을 소비하지 않음)
    public static List<string> CollectWarnings(
        int fixedItemCount,
        int fixedTimeCount,
        int nonEmptyGroupCount,
        int randomTimeCount
    )
    {
        var warnings = new List<string>();

        if (fixedItemCount != fixedTimeCount)
            warnings.Add(
                $"고정 미션 {fixedItemCount}개와 해금 시각 {fixedTimeCount}개의 개수가 다릅니다. "
                    + $"짧은 쪽({Math.Min(fixedItemCount, fixedTimeCount)}개)까지만 배분됩니다."
            );

        if (randomTimeCount < nonEmptyGroupCount)
            warnings.Add(
                $"무작위 해금 시각이 {randomTimeCount}개로 무작위 그룹 {nonEmptyGroupCount}개보다 적습니다. "
                    + $"남는 그룹 {nonEmptyGroupCount - randomTimeCount}개는 0초에 배정됩니다."
            );

        return warnings;
    }

    public static int CountNonEmptyGroups<T>(IReadOnlyList<IReadOnlyList<T>> groups)
    {
        int count = 0;
        for (int i = 0; i < (groups?.Count ?? 0); i++)
            if (groups[i] != null && groups[i].Count > 0)
                count++;
        return count;
    }
}
