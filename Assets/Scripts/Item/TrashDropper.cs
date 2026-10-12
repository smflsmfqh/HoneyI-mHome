using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class TrashDropper : MonoBehaviour
{
    [Header("드랍 아이템 테이블")]
    [SerializeField]
    private ItemData[] _dropTable;

    [Header("드랍 타이밍")]
    [SerializeField]
    private float _intervalMin = 20f;

    [SerializeField]
    private float _intervalMax = 60f;

    [Header("드랍 반경")]
    [SerializeField]
    private float _radiusMin = 0.5f;

    [SerializeField]
    private float _radiusMax = 2.5f;

    [Header("위치 검증")]
    [SerializeField]
    private LayerMask _groundMask;

    [SerializeField]
    private LayerMask _obstacleMask;

    [SerializeField]
    private float _itemCheckRadius = 0.25f;

    [SerializeField]
    private int _maxRetries = 15;

    private CancellationTokenSource _dropCts;

    private void Start()
    {
        if (_dropTable == null || _dropTable.Length == 0)
        {
            enabled = false;
            return;
        }

        if (TrashDropManager.Instance == null)
        {
            enabled = false;
            return;
        }

        DropLoopAsync(CtsUtility.Renew(ref _dropCts)).Forget();
    }

    private void OnDisable()
    {
        CtsUtility.Cancel(ref _dropCts);
    }

    private async UniTaskVoid DropLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                float wait = Random.Range(_intervalMin, _intervalMax);
                await UniTask.Delay(TimeSpan.FromSeconds(wait), DelayType.DeltaTime, cancellationToken: ct);
                TrySpawn();
            }
        }
        catch (OperationCanceledException) { }
    }

    private void TrySpawn()
    {
        for (int i = 0; i < _maxRetries; i++)
        {
            if (TryGetValidPosition(out Vector3 pos))
            {
                ItemData data = _dropTable[Random.Range(0, _dropTable.Length)];
                TrashDropManager.Instance.SpawnItem(data, pos);
                return;
            }
        }
    }

    private bool TryGetValidPosition(out Vector3 result)
    {
        result = Vector3.zero;

        Vector2 flat = Random.insideUnitCircle;
        if (flat.sqrMagnitude < 0.001f)
            flat = Vector2.right;
        flat = flat.normalized * Random.Range(_radiusMin, _radiusMax);

        Vector3 origin = transform.position + new Vector3(flat.x, 4f, flat.y);
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, _groundMask))
            return false;

        Vector3 checkPos = hit.point + Vector3.up * _itemCheckRadius;
        if (Physics.CheckSphere(checkPos, _itemCheckRadius, _obstacleMask))
            return false;

        result = hit.point + Vector3.up * 0.01f;
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, _radiusMax);

        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, _radiusMin);
    }
#endif
}
