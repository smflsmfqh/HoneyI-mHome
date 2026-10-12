using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class MissionPoolBuilderTests
{
    // AntDemoScene 값과 같은 구성: 고정 2개, 무작위 그룹 3개×후보 2개, 보너스는 A·B 중
    private static readonly string[] FixedItems = { "Pharmacy", "IceCream" };
    private static readonly float[] FixedTimes = { 20f, 90f };
    private static readonly string[][] RandomGroups =
    {
        new[] { "Avocado", "Banana" },
        new[] { "Egg", "Mushroom" },
        new[] { "Pizza", "Donut" },
    };
    private static readonly float[] RandomTimes = { 40f, 65f, 80f };
    private const int BONUS_GROUP_COUNT = 2;

    private static MissionPool<string> BuildDefault(int seed) =>
        MissionPoolBuilder.Build(
            FixedItems,
            FixedTimes,
            RandomGroups,
            RandomTimes,
            BONUS_GROUP_COUNT,
            new Random(seed)
        );

    private static IEnumerable<int> Seeds => Enumerable.Range(0, 200);

    // 각 무작위 그룹에서 본 미션으로 뽑힌 후보 (보너스 제외)
    private static string[] PickedPerGroup(MissionPool<string> pool)
    {
        var picked = new string[RandomGroups.Length];
        for (int i = 0; i < pool.Items.Length; i++)
        {
            if (i == pool.BonusIndex)
                continue;
            for (int g = 0; g < RandomGroups.Length; g++)
                if (RandomGroups[g].Contains(pool.Items[i]))
                    picked[g] = pool.Items[i];
        }
        return picked;
    }

    [Test]
    public void FixedMissions_KeepTheirUnlockTimes()
    {
        foreach (int seed in Seeds)
        {
            var pool = BuildDefault(seed);
            for (int f = 0; f < FixedItems.Length; f++)
            {
                int idx = Array.IndexOf(pool.Items, FixedItems[f]);
                Assert.GreaterOrEqual(idx, 0, $"seed {seed}: {FixedItems[f]} 누락");
                Assert.AreEqual(FixedTimes[f], pool.UnlockTimes[idx], $"seed {seed}");
            }
        }
    }

    [Test]
    public void EachRandomGroup_ContributesExactlyOneMission()
    {
        foreach (int seed in Seeds)
        {
            var pool = BuildDefault(seed);
            Assert.AreEqual(FixedItems.Length + RandomGroups.Length + 1, pool.Items.Length);

            for (int g = 0; g < RandomGroups.Length; g++)
            {
                int count = pool
                    .Items.Where((item, i) => i != pool.BonusIndex && RandomGroups[g].Contains(item))
                    .Count();
                Assert.AreEqual(1, count, $"seed {seed}: 그룹 {g}");
            }
        }
    }

    [Test]
    public void Bonus_IsUnpickedCandidateOfFirstTwoGroups_AtMaxValue_Last()
    {
        foreach (int seed in Seeds)
        {
            var pool = BuildDefault(seed);
            Assert.AreEqual(pool.Items.Length - 1, pool.BonusIndex, $"seed {seed}");
            Assert.AreEqual(float.MaxValue, pool.UnlockTimes[pool.BonusIndex], $"seed {seed}");

            string bonus = pool.Items[pool.BonusIndex];
            int bonusGroup = Array.FindIndex(RandomGroups, g => g.Contains(bonus));
            Assert.That(bonusGroup, Is.InRange(0, BONUS_GROUP_COUNT - 1), $"seed {seed}: {bonus}");
            Assert.AreNotEqual(PickedPerGroup(pool)[bonusGroup], bonus, $"seed {seed}");
        }
    }

    [Test]
    public void UnlockTimes_ExceptBonus_AreAscending()
    {
        foreach (int seed in Seeds)
        {
            var pool = BuildDefault(seed);
            var times = pool.UnlockTimes.Where((t, i) => i != pool.BonusIndex).ToArray();
            Assert.That(times, Is.Ordered.Ascending, $"seed {seed}");
        }
    }

    [Test]
    public void RandomMissionTimes_AreShuffledRandomTimes()
    {
        foreach (int seed in Seeds)
        {
            var pool = BuildDefault(seed);
            var randomTimes = pool
                .Items.Select((item, i) => (item, time: pool.UnlockTimes[i], i))
                .Where(x => x.i != pool.BonusIndex && RandomGroups.Any(g => g.Contains(x.item)))
                .Select(x => x.time);
            CollectionAssert.AreEquivalent(RandomTimes, randomTimes, $"seed {seed}");
        }
    }

    [Test]
    public void SameSeed_ProducesSameResult()
    {
        foreach (int seed in Seeds)
        {
            var a = BuildDefault(seed);
            var b = BuildDefault(seed);
            CollectionAssert.AreEqual(a.Items, b.Items, $"seed {seed}");
            CollectionAssert.AreEqual(a.UnlockTimes, b.UnlockTimes, $"seed {seed}");
            Assert.AreEqual(a.BonusIndex, b.BonusIndex, $"seed {seed}");
        }
    }

    [Test]
    public void DifferentSeeds_ProduceMultipleCombinations()
    {
        var combos = new HashSet<string>(
            Seeds.Select(seed => string.Join(",", PickedPerGroup(BuildDefault(seed))))
        );
        Assert.GreaterOrEqual(combos.Count, 2);
    }

    [Test]
    public void SettingsMatchingScene_ProduceNoWarnings()
    {
        Assert.IsEmpty(BuildDefault(0).Warnings);
    }

    [Test]
    public void TooFewRandomTimes_WarnsAndLeftoverGroupGetsZero()
    {
        var pool = MissionPoolBuilder.Build(
            FixedItems,
            FixedTimes,
            RandomGroups,
            new[] { 40f, 65f },
            BONUS_GROUP_COUNT,
            new Random(0)
        );

        Assert.AreEqual(1, pool.Warnings.Count);

        // 셔플된 슬롯은 앞 그룹부터 소비되므로 마지막 그룹(C)이 0초
        string groupC = PickedPerGroup(pool)[2];
        Assert.AreEqual(0f, pool.UnlockTimes[Array.IndexOf(pool.Items, groupC)]);
    }

    [Test]
    public void FixedCountMismatch_WarnsAndTruncatesToShorter()
    {
        var pool = MissionPoolBuilder.Build(
            FixedItems,
            new[] { 20f },
            RandomGroups,
            RandomTimes,
            BONUS_GROUP_COUNT,
            new Random(0)
        );

        Assert.AreEqual(1, pool.Warnings.Count);
        CollectionAssert.Contains(pool.Items, "Pharmacy");
        CollectionAssert.DoesNotContain(pool.Items, "IceCream");
    }

    [Test]
    public void NoGroups_HasNoBonus()
    {
        var pool = MissionPoolBuilder.Build(
            FixedItems,
            FixedTimes,
            Array.Empty<string[]>(),
            Array.Empty<float>(),
            BONUS_GROUP_COUNT,
            new Random(0)
        );

        Assert.AreEqual(-1, pool.BonusIndex);
        CollectionAssert.AreEqual(FixedItems, pool.Items);
    }

    [Test]
    public void EmptyBonusGroups_HaveNoBonus()
    {
        var groups = new[] { Array.Empty<string>(), null, new[] { "Pizza", "Donut" } };

        foreach (int seed in Seeds)
        {
            var pool = MissionPoolBuilder.Build(
                FixedItems,
                FixedTimes,
                groups,
                new[] { 40f },
                BONUS_GROUP_COUNT,
                new Random(seed)
            );

            Assert.AreEqual(-1, pool.BonusIndex, $"seed {seed}");
            Assert.IsEmpty(pool.Warnings, $"seed {seed}: 빈 그룹은 시각 슬롯을 소비하지 않음");
            Assert.AreEqual(40f, pool.UnlockTimes[Array.FindIndex(pool.Items, i => i == "Pizza" || i == "Donut")]);
        }
    }

    [Test]
    public void SingleCandidateBonusGroups_HaveNoBonus()
    {
        var groups = new[] { new[] { "Avocado" }, new[] { "Egg" }, new[] { "Pizza", "Donut" } };

        foreach (int seed in Seeds)
        {
            var pool = MissionPoolBuilder.Build(
                FixedItems,
                FixedTimes,
                groups,
                RandomTimes,
                BONUS_GROUP_COUNT,
                new Random(seed)
            );

            Assert.AreEqual(-1, pool.BonusIndex, $"seed {seed}");
            Assert.AreEqual(FixedItems.Length + groups.Length, pool.Items.Length);
        }
    }

    [Test]
    public void NullInputs_ProduceEmptyPool()
    {
        var pool = MissionPoolBuilder.Build<string>(null, null, null, null, BONUS_GROUP_COUNT, new Random(0));

        Assert.IsEmpty(pool.Items);
        Assert.AreEqual(-1, pool.BonusIndex);
        Assert.IsEmpty(pool.Warnings);
    }
}
