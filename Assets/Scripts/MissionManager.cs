using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Serializable]
    private struct HintEntry
    {
        public float unlockTime;
        public string csvKey;
    }

    [Serializable]
    private struct RandomMissionGroup
    {
        public ItemData[] candidates;
    }

    [Serializable]
    private struct PreMonologueEntry
    {
        public string itemName;
        public string csvKey;
    }

    [Header("인트로 독백")]
    [SerializeField]
    private PreMonologueEntry[] _preMonologues;

    [Header("고정 미션 (약국, 아이스크림)")]
    [SerializeField]
    private ItemData[] _fixedMissions;

    [SerializeField]
    private float[] _fixedUnlockTimes;

    [Header("랜덤 미션 그룹 (A: 아보카도/바나나  B: 달걀/버섯  C: 피자/도넛)")]
    [SerializeField]
    private RandomMissionGroup[] _randomGroups;

    [Tooltip("랜덤 그룹 수만큼 입력 — 셔플되어 각 그룹에 배분됨")]
    [SerializeField]
    private float[] _randomUnlockTimes;

    [Header("추가 미션 — A·B 중 랜덤 그룹의 탈락 후보")]
    [SerializeField]
    private int _bonusTriggerCount = 2;

    [SerializeField]
    private float _bonusDelay = 30f;

    [Header("힌트")]
    [SerializeField]
    private HintEntry[] _hints;

    [Header("히든 미션 (꽃)")]
    [SerializeField]
    private ItemData _optionalMission;

    public event Action<ItemData> OnMissionAssigned;
    public event Action<ItemData> OnMissionDisplayed;
    public event Action<ItemData> OnMissionCompleted;
    public event Action<string> OnHintAssigned;
    public event Action OnOptionalMissionUnlocked;

    public void NotifyMissionDisplayed(ItemData item) => OnMissionDisplayed?.Invoke(item);

    public void PauseMissionAssignment() => _missionsPaused = true;

    public void ResumeMissionAssignment() => _missionsPaused = false;

    // 미할당 미션 중 가장 이른 것 하나만 즉시 배분 (튜토리얼 종료 직후 호출용)
    public void ForceAssignNext()
    {
        for (int i = 0; i < _missionPool.Length; i++)
        {
            if (_assigned[i] || i == _bonusIdx)
                continue;
            _assigned[i] = true;
            string preKey = StringTableManager.Instance.GetPreMonologueKey(
                _missionPool[i].itemName
            );
            if (!string.IsNullOrEmpty(preKey))
                OnHintAssigned?.Invoke(preKey);
            OnMissionAssigned?.Invoke(_missionPool[i]);
            return;
        }
    }

    private float _elapsedTime;
    private bool _missionsPaused;
    private ItemData[] _missionPool;
    private float[] _unlockTimes;
    private bool[] _assigned;
    private bool[] _completed;
    private bool[] _hintTriggered;
    private int _completedCount;
    private bool _optionalUnlocked;
    private bool _optionalCompleted;

    // 보너스 미션은 _missionPool에 포함, 인덱스만 기록
    private int _bonusIdx = -1;
    private bool _bonusTriggerFired;

    public bool OnAllMissionCompleted => _completedCount >= _missionPool.Length;

    // 보너스 제외, 아직 unlockTime에 도달하지 않아 할당되지 않은 일반 미션이 있는지
    public bool HasUnassignedMissions
    {
        get
        {
            for (int i = 0; i < _missionPool.Length; i++)
                if (!_assigned[i] && i != _bonusIdx)
                    return true;
            return false;
        }
    }

    public bool IsOptionalUnlocked => _optionalUnlocked;
    public bool IsOptionalCompleted => _optionalCompleted;
    public ItemData OptionalMission => _optionalMission;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildMissionPool();

        _assigned = new bool[_missionPool.Length];
        _completed = new bool[_missionPool.Length];
        _hintTriggered = new bool[_hints?.Length ?? 0];
    }

    // A·B 중 하나를 추가 미션 후보 그룹으로 결정 (인덱스 0 또는 1)
    private const int BONUS_GROUP_COUNT = 2;

    // 배분 규칙은 MissionPoolBuilder (EditMode 테스트로 검증), 여기서는 결과만 받는다
    private void BuildMissionPool()
    {
        int seed = Environment.TickCount;
#if UNITY_EDITOR
        // 재현용: Tools > Mission Seed 창에서 켠 경우에만 덮어쓴다
        if (UnityEditor.EditorPrefs.GetBool(SEED_OVERRIDE_ENABLED_KEY, false))
        {
            seed = UnityEditor.EditorPrefs.GetInt(SEED_OVERRIDE_VALUE_KEY, 0);
            Debug.LogWarning($"[MissionManager] 미션 시드 덮어쓰기 사용 중: {seed}", this);
        }
        UnityEditor.EditorPrefs.SetInt(LAST_SEED_KEY, seed);
#endif
        Debug.Log($"[MissionManager] mission seed = {seed}", this);

        var pool = MissionPoolBuilder.Build(
            _fixedMissions,
            _fixedUnlockTimes,
            ToCandidateLists(_randomGroups),
            _randomUnlockTimes,
            BONUS_GROUP_COUNT,
            new System.Random(seed)
        );

        foreach (var warning in pool.Warnings)
            Debug.LogWarning($"[MissionManager] {warning}", this);

        _missionPool = pool.Items;
        _unlockTimes = pool.UnlockTimes;
        _bonusIdx = pool.BonusIndex;
    }

    private static IReadOnlyList<ItemData>[] ToCandidateLists(RandomMissionGroup[] groups)
    {
        if (groups == null)
            return null;

        var lists = new IReadOnlyList<ItemData>[groups.Length];
        for (int i = 0; i < groups.Length; i++)
            lists[i] = groups[i].candidates;
        return lists;
    }

#if UNITY_EDITOR
    // MissionSeedWindow와 공유하는 EditorPrefs 키
    public const string SEED_OVERRIDE_ENABLED_KEY = "HoneyIMHome.MissionSeed.OverrideEnabled";
    public const string SEED_OVERRIDE_VALUE_KEY = "HoneyIMHome.MissionSeed.OverrideValue";
    public const string LAST_SEED_KEY = "HoneyIMHome.MissionSeed.Last";

    // 빌더와 같은 개수 검사를 인스펙터 편집 시점에 바로 보여준다 (필드는 수정하지 않음)
    private void OnValidate()
    {
        var warnings = MissionPoolBuilder.CollectWarnings(
            _fixedMissions?.Length ?? 0,
            _fixedUnlockTimes?.Length ?? 0,
            MissionPoolBuilder.CountNonEmptyGroups(ToCandidateLists(_randomGroups)),
            _randomUnlockTimes?.Length ?? 0
        );

        foreach (var warning in warnings)
            Debug.LogWarning($"[MissionManager] {warning}", this);
    }
#endif

    private void Update()
    {
        if (_missionsPaused)
            return;

        _elapsedTime += Time.deltaTime;

        for (int i = 0; i < _missionPool.Length; i++)
        {
            if (!_assigned[i] && _elapsedTime >= _unlockTimes[i])
            {
                _assigned[i] = true;
                string preKey = StringTableManager.Instance.GetPreMonologueKey(
                    _missionPool[i].itemName
                );
                if (!string.IsNullOrEmpty(preKey))
                    OnHintAssigned?.Invoke(preKey);
                OnMissionAssigned?.Invoke(_missionPool[i]);
            }
        }

        if (_hints == null)
            return;

        for (int i = 0; i < _hints.Length; i++)
        {
            if (!_hintTriggered[i] && _elapsedTime >= _hints[i].unlockTime)
            {
                _hintTriggered[i] = true;
                OnHintAssigned?.Invoke(_hints[i].csvKey);
            }
        }
    }

    public void ReportCollected(string itemName)
    {
        for (int i = 0; i < _missionPool.Length; i++)
        {
            if (_assigned[i] && !_completed[i] && _missionPool[i].itemName == itemName)
            {
                _completed[i] = true;
                _completedCount++;
                OnMissionCompleted?.Invoke(_missionPool[i]);
                TryTriggerBonus();
                CheckOptionalUnlock();
                return;
            }
        }

        if (
            _optionalMission != null
            && _optionalUnlocked
            && !_optionalCompleted
            && _optionalMission.itemName == itemName
        )
        {
            _optionalCompleted = true;
            OnMissionCompleted?.Invoke(_optionalMission);
        }
    }

    private void TryTriggerBonus()
    {
        if (_bonusTriggerFired || _bonusIdx < 0)
            return;

        if (_completedCount >= _bonusTriggerCount)
        {
            _bonusTriggerFired = true;
            _unlockTimes[_bonusIdx] = _elapsedTime + _bonusDelay;
        }
    }

    private void CheckOptionalUnlock()
    {
        if (_completedCount >= _missionPool.Length && !_optionalUnlocked)
        {
            _optionalUnlocked = true;
            OnOptionalMissionUnlocked?.Invoke();
        }
    }

    public bool IsMissionAssigned(string itemName)
    {
        for (int i = 0; i < _missionPool.Length; i++)
            if (_missionPool[i].itemName == itemName)
                return _assigned[i];

        return false;
    }

#if UNITY_EDITOR
    public void DebugUnlockOptional()
    {
        if (_optionalMission == null || _optionalUnlocked)
            return;
        _optionalUnlocked = true;
        OnOptionalMissionUnlocked?.Invoke();
        NotifyMissionDisplayed(_optionalMission);
    }

    public void DebugCompleteAll()
    {
        // 보너스 제외 미할당 미션 즉시 할당
        for (int i = 0; i < _missionPool.Length; i++)
        {
            if (!_assigned[i] && i != _bonusIdx)
            {
                _assigned[i] = true;
                OnMissionAssigned?.Invoke(_missionPool[i]);
            }
        }
        // 일반 미션 완료 — 내부에서 TryTriggerBonus 호출됨
        for (int i = 0; i < _missionPool.Length; i++)
        {
            if (_assigned[i] && !_completed[i] && i != _bonusIdx)
                ReportCollected(_missionPool[i].itemName);
        }
        // 보너스 미션 강제 할당 후 완료
        if (_bonusIdx >= 0 && !_assigned[_bonusIdx])
        {
            _assigned[_bonusIdx] = true;
            OnMissionAssigned?.Invoke(_missionPool[_bonusIdx]);
        }
        if (_bonusIdx >= 0 && !_completed[_bonusIdx])
            ReportCollected(_missionPool[_bonusIdx].itemName);
    }
#endif
}
