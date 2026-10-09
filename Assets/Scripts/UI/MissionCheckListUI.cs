using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MissionCheckListUI : MonoBehaviour
{
    [SerializeField]
    private GameObject _listPanel;

    [SerializeField]
    private GameObject _entryPrefab;

    [SerializeField]
    private Transform _entryContainer;

    [SerializeField]
    private PlayerController _playerController;

    [SerializeField]
    private MissionMessageUI _missionMessageUI;

    private readonly List<MissionEntryUI> _entries = new();
    private bool _isOpen = false;
    private CancellationTokenSource _peekCts;

    private void Awake()
    {
        _listPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionCompleted += HandleMissionCompleted;
        if (_playerController != null)
            _playerController.OnMissionTogglePressed += ToggleList;
        if (_missionMessageUI != null)
            _missionMessageUI.OnSlidedIn += HandleSlidedIn;
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
        if (_playerController != null)
            _playerController.OnMissionTogglePressed -= ToggleList;
        if (_missionMessageUI != null)
            _missionMessageUI.OnSlidedIn -= HandleSlidedIn;

        _peekCts?.Cancel();
        _peekCts?.Dispose();
        _peekCts = null;
    }

    // 미션 메시지가 슬라이딩 인 될 때 항목 추가 + 리스트 잠깐 표시
    private void HandleSlidedIn(ItemData item)
    {
        if (item == null)
            return; // 분노·힌트·독백 메시지는 무시
        AddEntry(item);
        Peek();
    }

    private void AddEntry(ItemData item)
    {
        var go = Instantiate(_entryPrefab, _entryContainer);
        var entry = go.GetComponent<MissionEntryUI>();
        entry.Init(item);
        _entries.Add(entry);
    }

    private void HandleMissionCompleted(ItemData item)
    {
        var entry = _entries.Find(e => e.ItemName == item.itemName);
        entry?.Complete();
        Peek();
    }

    public void Peek()
    {
        _peekCts?.Cancel();
        _peekCts?.Dispose();
        _peekCts = new CancellationTokenSource();
        PeekAsync(_peekCts.Token).Forget();
    }

    private void ToggleList()
    {
        _isOpen = !_isOpen;
        _listPanel.SetActive(_isOpen);
    }

    private async UniTaskVoid PeekAsync(CancellationToken ct)
    {
        try
        {
            _listPanel.SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(3f), DelayType.DeltaTime, cancellationToken: ct);
            if (!_isOpen)
                _listPanel.SetActive(false);
        }
        catch (OperationCanceledException) { }
    }
}
