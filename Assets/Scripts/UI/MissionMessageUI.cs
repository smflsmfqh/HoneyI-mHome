using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionMessageUI : MonoBehaviour
{
    [SerializeField]
    private Image _icon;

    [SerializeField]
    private TextMeshProUGUI _senderName;

    [SerializeField]
    private TextMeshProUGUI _message;

    [SerializeField]
    private RectTransform _panel;

    [SerializeField]
    private float _displayDuration = 7f;

    [SerializeField]
    private AngerSystem _angerSystem;

    [Header("오디오")]
    [SerializeField]
    private AudioClip _slideInSound;

    // 슬라이드 완료 직후 발화 — 미션 메시지일 때만 ItemData가 non-null
    public event Action<ItemData> OnSlidedIn;

    private readonly struct MessageData
    {
        public readonly string MsgKey; // csvKey — dequeue 시점에 StringTable 재조회
        public readonly ItemData Item; // 체크리스트·NotifyMissionDisplayed용 (분노·힌트는 null)
        public readonly System.Action OnSlidedInCallback;
        public readonly float DisplayDuration; // <=0이면 _displayDuration 사용

        public MessageData(
            string msgKey,
            ItemData item = null,
            System.Action onSlidedInCallback = null,
            float displayDuration = -1f
        )
        {
            MsgKey = msgKey;
            Item = item;
            OnSlidedInCallback = onSlidedInCallback;
            DisplayDuration = displayDuration;
        }
    }

    private float _panelWidth;
    private float _currentSlideDuration = 0.3f;
    private CancellationTokenSource _slideCts;
    private string _currentMsgKey;
    private readonly Queue<MessageData> _queue = new();

    public void SetSlideDuration(float duration) => _currentSlideDuration = duration;

    public void ClearQueue()
    {
        _queue.Clear();
        // Dispose는 해당 루프의 finally가 담당 — 같은 프레임의 Enqueue가 새 루프를 시작할 수 있도록 필드만 비움
        _slideCts?.Cancel();
        _slideCts = null;
        _currentMsgKey = null;
        _panel.anchoredPosition = new Vector2(-_panelWidth, _panel.anchoredPosition.y);
    }

    private void Awake()
    {
        _panelWidth = _panel.sizeDelta.x;
        _panel.anchoredPosition = new Vector2(-_panelWidth, _panel.anchoredPosition.y);
    }

    private void OnEnable()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionAssigned += HandleMissionAssigned;
            MissionManager.Instance.OnHintAssigned += HandleHint;
            MissionManager.Instance.OnOptionalMissionUnlocked += HandleMonologue;
        }
        if (_angerSystem != null)
            _angerSystem.OnMessasgeTriggered += HandleAngerMessage;
        StringTableManager.Instance.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionAssigned -= HandleMissionAssigned;
            MissionManager.Instance.OnHintAssigned -= HandleHint;
            MissionManager.Instance.OnOptionalMissionUnlocked -= HandleMonologue;
        }
        if (_angerSystem != null)
            _angerSystem.OnMessasgeTriggered -= HandleAngerMessage;
        StringTableManager.Instance.OnLanguageChanged -= HandleLanguageChanged;

        _slideCts?.Cancel();
        _slideCts = null;
    }

    // 언어 변경 시 현재 화면에 표시 중인 메시지를 즉시 재번역
    private void HandleLanguageChanged()
    {
        if (string.IsNullOrEmpty(_currentMsgKey))
            return;

        var (msg, sender) = StringTableManager.Instance.GetMessage(_currentMsgKey);
        _message.text = msg;
        _senderName.text = sender;
        if (!string.IsNullOrEmpty(sender))
        {
            string imageKey = StringTableManager.Instance.GetImageKeyBySender(sender);
            _icon.sprite = Resources.Load<Sprite>(imageKey);
        }
    }

    private void HandleMissionAssigned(ItemData itemData)
    {
        string key = $"MISSION_{itemData.itemName}";
        var (message, _) = StringTableManager.Instance.GetMessage(key);
        if (string.IsNullOrEmpty(message))
            return;
        Enqueue(new MessageData(key, itemData));
    }

    private void HandleHint(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;
        Enqueue(new MessageData(key));
    }

    private void HandleMonologue()
    {
        EnqueueIfExists("MONOLOGUE_HOME");
        EnqueueIfExists("MONOLOGUE_WAIT");

        var (flowerMsg, _) = StringTableManager.Instance.GetMessage("MONOLOGUE_FLOWER");
        if (!string.IsNullOrEmpty(flowerMsg))
            Enqueue(new MessageData("MONOLOGUE_FLOWER", MissionManager.Instance?.OptionalMission));

        EnqueueIfExists("MONOLOGUE_FLOWER_OPTIONAL");
    }

    private void EnqueueIfExists(string key)
    {
        var (msg, _) = StringTableManager.Instance.GetMessage(key);
        if (!string.IsNullOrEmpty(msg))
            Enqueue(new MessageData(key));
    }

    private void HandleAngerMessage(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;
        Enqueue(new MessageData(key));
    }

    public void EnqueueTutorialMessage(
        string csvKey,
        System.Action onSlidedIn = null,
        float displayDuration = 3f
    )
    {
        var (message, _) = StringTableManager.Instance.GetMessage(csvKey);
        if (string.IsNullOrEmpty(message))
            return;
        Enqueue(new MessageData(csvKey, null, onSlidedIn, displayDuration));
    }

    // 여러 메시지를 순서대로 한 번에 큐 맨 앞에 삽입 (연속 호출 시 순서 역전 방지)
    public void EnqueueFrontTutorialMessages(params string[] csvKeys)
    {
        var inserts = new List<MessageData>();
        foreach (var key in csvKeys)
        {
            var (message, _) = StringTableManager.Instance.GetMessage(key);
            if (!string.IsNullOrEmpty(message))
                inserts.Add(new MessageData(key, null, null, 3f));
        }
        if (inserts.Count == 0)
            return;

        var temp = new Queue<MessageData>(_queue);
        _queue.Clear();
        foreach (var item in inserts)
            _queue.Enqueue(item);
        while (temp.Count > 0)
            _queue.Enqueue(temp.Dequeue());

        StartLoopIfIdle();
    }

    private void Enqueue(MessageData data)
    {
        _queue.Enqueue(data);
        StartLoopIfIdle();
    }

    private void StartLoopIfIdle()
    {
        if (_slideCts != null)
            return;
        var cts = new CancellationTokenSource();
        _slideCts = cts;
        ProcessQueueAsync(cts).Forget();
    }

    private async UniTaskVoid ProcessQueueAsync(CancellationTokenSource cts)
    {
        var ct = cts.Token;
        try
        {
            while (_queue.Count > 0)
            {
                var data = _queue.Dequeue();

                // dequeue 시점에 현재 언어로 재조회
                var (message, sender) = StringTableManager.Instance.GetMessage(data.MsgKey);
                _message.text = message;
                _senderName.text = sender;
                _currentMsgKey = data.MsgKey;

                if (!string.IsNullOrEmpty(sender))
                {
                    string imageKey = StringTableManager.Instance.GetImageKeyBySender(sender);
                    _icon.sprite = Resources.Load<Sprite>(imageKey);
                }

                AudioManager.Instance?.PlaySFX(_slideInSound);
                await SlideAsync(-_panelWidth, 10f, _currentSlideDuration, ct);
                OnSlidedIn?.Invoke(data.Item);
                // 구독자가 ClearQueue를 호출했다면 이 회차의 나머지(표시 통지 등)를 실행하지 않음
                ct.ThrowIfCancellationRequested();
                data.OnSlidedInCallback?.Invoke();
                ct.ThrowIfCancellationRequested();
                if (data.Item != null)
                    MissionManager.Instance?.NotifyMissionDisplayed(data.Item);
                float waitTime = data.DisplayDuration > 0 ? data.DisplayDuration : _displayDuration;
                await UniTask.Delay(TimeSpan.FromSeconds(waitTime), DelayType.DeltaTime, cancellationToken: ct);
                await SlideAsync(0f, -_panelWidth, _currentSlideDuration, ct);
                _currentMsgKey = null;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // 자기 CTS만 정리 — 취소된 루프의 finally는 다음 프레임에 실행되므로, 그사이 시작된 새 루프의 CTS를 건드리지 않도록
            if (_slideCts == cts)
                _slideCts = null;
            cts.Dispose();
        }
    }

    private async UniTask SlideAsync(float fromX, float toX, float duration, CancellationToken ct)
    {
        float elapsed = 0f;
        float y = _panel.anchoredPosition.y;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), y);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        _panel.anchoredPosition = new Vector2(toX, y);
    }
}
