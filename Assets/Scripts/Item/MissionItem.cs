using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MissionItem : MonoBehaviour, IInteractive
{
    [SerializeField]
    private ItemData _itemData;

    [SerializeField]
    private ParticleSystem _optionalParticle; // 선택 미션 입수 시 발동

    [HideInInspector]
    public bool IsPooled = false;

    [HideInInspector]
    public float LifeTime = 0f;
    public event Action<MissionItem> OnDeactivated;

    private PlayerMovement _playerMovement;
    private FollowCamera _camera;
    private CancellationTokenSource _lifeTimerCts;
    private MinimapMarker _minimapMarker;

    private void Awake()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;
        _playerMovement = player.GetComponent<PlayerMovement>();

        var mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCameraObj != null)
            _camera = mainCameraObj.GetComponent<FollowCamera>();

        if (_itemData != null)
            SetupMinimapMarker();

        if (
            _itemData != null
            && _itemData.category == ItemCategory.Optional
            && _optionalParticle != null
        )
            _optionalParticle.gameObject.SetActive(false);
    }

    public void Init(ItemData data)
    {
        _itemData = data;
        SetupMinimapMarker();
    }

    private void SetupMinimapMarker()
    {
        if (_itemData.category == ItemCategory.Food)
            return;

        if (!TryGetComponent(out _minimapMarker))
        {
            _minimapMarker = gameObject.AddComponent<MinimapMarker>();
            _minimapMarker.type = MinimapMarker.MarkerType.Item;
        }

        _minimapMarker.colorOverride = _itemData.category switch
        {
            ItemCategory.Mission => Color.red,
            ItemCategory.Optional => Color.yellow,
            _ => Color.magenta,
        };
        // AddComponent 시점의 OnEnable에서 기본 색으로 이미 등록됐으므로 다시 칠한다
        MinimapUI.Instance?.RefreshColor(_minimapMarker);

        if (_itemData.category == ItemCategory.Optional)
            _minimapMarker.enabled = false;
    }

    private void OnEnable()
    {
        if (IsPooled && LifeTime > 0f)
        {
            _lifeTimerCts = new CancellationTokenSource();
            AutoDeactivateAsync(_lifeTimerCts.Token).Forget();
        }

        if (
            _itemData != null
            && _itemData.category == ItemCategory.Optional
            && MissionManager.Instance != null
        )
            MissionManager.Instance.OnMissionDisplayed += HandleOptionalDisplayed;
    }

    private void OnDisable()
    {
        _lifeTimerCts?.Cancel();
        _lifeTimerCts?.Dispose();
        _lifeTimerCts = null;

        if (
            _itemData != null
            && _itemData.category == ItemCategory.Optional
            && MissionManager.Instance != null
        )
            MissionManager.Instance.OnMissionDisplayed -= HandleOptionalDisplayed;

        if (_optionalParticle != null)
            _optionalParticle.Stop();
    }

    private void HandleOptionalDisplayed(ItemData item)
    {
        if (item == null || item.itemName != _itemData.itemName)
            return;

        if (_minimapMarker != null)
        {
            _minimapMarker.enabled = true;
            MinimapUI.Instance?.PingMarker(_minimapMarker);
        }

        if (_optionalParticle != null)
        {
            _optionalParticle.gameObject.SetActive(true);
            _optionalParticle.Play();
        }

        MissionManager.Instance.OnMissionDisplayed -= HandleOptionalDisplayed;
    }

    private async UniTaskVoid AutoDeactivateAsync(CancellationToken ct)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(LifeTime), DelayType.DeltaTime, cancellationToken: ct);
            Deactivate();
        }
        catch (OperationCanceledException) { }
    }

    // Optional 미션은 독백(선택 미션 해제) 이후에만 픽업 가능
    public bool CanInteract =>
        _itemData.category != ItemCategory.Optional
        || MissionManager.Instance.IsOptionalUnlocked;

    public void Interact(PlayerController player)
    {
        if (!CanInteract)
            return;

        player.PlayPickupSound();

        foreach (var effect in _itemData.effects)
        {
            effect.Apply(player);
        }

        if (_itemData.category == ItemCategory.Mission)
        {
            MissionManager.Instance.ReportCollected(_itemData.itemName);
            _camera?.TriggerReactionCut(0.6f);
            _playerMovement.SetFaceHappy();
            GameManager.Instance.AddScore(ItemCategory.Mission);
        }
        else if (_itemData.category == ItemCategory.Food)
        {
            _camera?.TriggerReactionCut(0.6f);
            _playerMovement.SetFaceExcited();
            GameManager.Instance.AddScore(ItemCategory.Food);
        }
        else if (_itemData.category == ItemCategory.Optional)
        {
            MissionManager.Instance.ReportCollected(_itemData.itemName);
            GameManager.Instance.AddScore(ItemCategory.Optional);
        }

        if (IsPooled)
            Deactivate();
        else
            Destroy(gameObject);
    }

    public string GetItemName() => _itemData.itemName;

    private void Deactivate()
    {
        OnDeactivated?.Invoke(this);
        gameObject.SetActive(false);
    }
}
