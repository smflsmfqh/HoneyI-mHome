using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ShopBuilding : MonoBehaviour
{
    [SerializeField]
    private ShopData _shopData;

    [SerializeField]
    private LayerMask _groundMask;

    [SerializeField]
    private float _spreadHalfAngle = 30f;

    [SerializeField]
    private float _dropDistanceMin = 0.4f;

    [SerializeField]
    private float _dropDistanceMax = 0.8f;

    [Header("파티클")]
    [SerializeField]
    private ParticleSystem _spotParticle;

    [SerializeField]
    private ParticleSystem _groundParticlePrefab;

    [Header("드랍 쿨타임")]
    [SerializeField]
    private float _cooldown = 10f;

    [SerializeField]
    private float _foodCooldown = 5f;

    [SerializeField]
    private CooldownBarUI _cooldownBar;

    private bool _isCooldown;

    public float ItemCoolDown => _cooldown;

    private List<ItemData> _remainingItems = new();
    private readonly List<ItemData> _dropCandidates = new();
    private Vector3 _spawnPos;
    private CancellationTokenSource _cooldownCts;

    private void Start()
    {
        if (_shopData.dropItems != null)
            _remainingItems = new List<ItemData>(_shopData.dropItems);

        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionAssigned += HandleMissionAssigned;
            MissionManager.Instance.OnMissionDisplayed += HandleMissionDisplayed;
        }

        if (_spotParticle != null)
        {
            _spotParticle.transform.position = GetParticlePosition();
            if (HasFoodItem())
                PlayParticle();
            else
                StopParticle();
        }
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionAssigned -= HandleMissionAssigned;
            MissionManager.Instance.OnMissionDisplayed -= HandleMissionDisplayed;
        }

        _cooldownCts?.Cancel();
        _cooldownCts?.Dispose();
        _cooldownCts = null;
    }

    private void HandleMissionAssigned(ItemData data)
    {
        if (!_remainingItems.Exists(item => item.itemName == data.itemName))
            return;

        if (_spotParticle != null && _isCooldown)
            StopParticle();
    }

    private void HandleMissionDisplayed(ItemData data)
    {
        if (!_remainingItems.Exists(item => item.itemName == data.itemName))
            return;

        if (_spotParticle != null && !_isCooldown)
            PlayParticle();

        if (TryGetComponent(out MinimapMarker marker))
            MinimapUI.Instance?.PingMarker(marker);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (_isCooldown)
            return;

        if (_remainingItems.Count == 0)
            return;

        // 아직 부여되지 않은 미션 아이템은 후보에서 빼고 뽑는다
        _dropCandidates.Clear();
        foreach (var item in _remainingItems)
        {
            if (IsDroppable(item))
                _dropCandidates.Add(item);
        }

        if (_dropCandidates.Count == 0)
            return;

        ItemData selected = _dropCandidates[UnityEngine.Random.Range(0, _dropCandidates.Count)];

        _remainingItems.Remove(selected);

        _spawnPos = GetFrontSpawnPosition();
        _spawnPos.y += selected.spawnHeightOffset;
        var rot = Quaternion.Euler(selected.spawnRotation);
        var dropped = Instantiate(selected.dropPrefab, _spawnPos, rot);
        if (_groundParticlePrefab != null)
            Instantiate(_groundParticlePrefab, dropped.transform);

        if (_spotParticle != null)
            StopParticle();

        if (TryGetComponent(out MinimapMarker marker))
            MinimapUI.Instance?.StopPingMarker(marker);

        _cooldownCts?.Cancel();
        _cooldownCts?.Dispose();
        _cooldownCts = new CancellationTokenSource();
        float cooldown = selected.category == ItemCategory.Mission ? _cooldown : _foodCooldown;
        CooldownAsync(cooldown, selected, _cooldownCts.Token).Forget();
    }

    private async UniTaskVoid CooldownAsync(float cooldown, ItemData dropped, CancellationToken ct)
    {
        try
        {
            _isCooldown = true;
            _cooldownBar?.Show();

            float elapsed = 0f;
            while (elapsed < cooldown)
            {
                elapsed += Time.deltaTime;
                _cooldownBar?.SetFill(elapsed / cooldown);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _cooldownBar?.Hide();
            _isCooldown = false;

            if (dropped.category != ItemCategory.Mission)
                _remainingItems.Add(dropped);

            if (HasAvailableItem() && _spotParticle != null)
                PlayParticle();
        }
        catch (OperationCanceledException) { }
    }

    private bool HasFoodItem()
    {
        return _remainingItems.Exists(item => item.category != ItemCategory.Mission);
    }

    private bool HasAvailableItem()
    {
        foreach (var item in _remainingItems)
        {
            if (IsDroppable(item))
                return true;
        }

        return false;
    }

    private static bool IsDroppable(ItemData item)
    {
        if (item.category != ItemCategory.Mission)
            return true;

        return MissionManager.Instance != null
            && MissionManager.Instance.IsMissionAssigned(item.itemName);
    }

    private void PlayParticle()
    {
        _spotParticle.gameObject.SetActive(true);
        _spotParticle.Play();
    }

    private void StopParticle()
    {
        _spotParticle.gameObject.SetActive(false);
    }

    private Vector3 GetFrontSpawnPosition()
    {
        var col = GetComponent<BoxCollider>();
        Vector3 center = col != null ? transform.TransformPoint(col.center) : transform.position;

        Vector3 outDir = center - transform.position;
        outDir.y = 0f;
        outDir = outDir.sqrMagnitude > 0.001f ? outDir.normalized : transform.forward;

        float angle = UnityEngine.Random.Range(-_spreadHalfAngle, _spreadHalfAngle);
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * outDir;
        Vector3 horizontal = center + dir * UnityEngine.Random.Range(_dropDistanceMin, _dropDistanceMax);

        Vector3 origin = new Vector3(horizontal.x, center.y + 3f, horizontal.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, _groundMask))
            return hit.point + Vector3.up * 0.01f;

        return new Vector3(horizontal.x, 0.01f, horizontal.z);
    }

    private Vector3 GetParticlePosition()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null)
            return transform.position;

        Vector3 center = transform.TransformPoint(col.center);
        Vector3 outDir = center - transform.position;
        outDir.y = 0f;
        outDir = outDir.sqrMagnitude > 0.001f ? outDir.normalized : transform.forward;

        Vector3 scale = transform.lossyScale;
        float edgeDist =
            Mathf.Abs(Vector3.Dot(transform.right, outDir)) * col.size.x * Mathf.Abs(scale.x) * 0.5f
            + Mathf.Abs(Vector3.Dot(transform.forward, outDir))
                * col.size.z
                * Mathf.Abs(scale.z)
                * 0.5f;
        return center + outDir * edgeDist;
    }
}
