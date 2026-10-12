using System.Collections.Generic;
using UnityEngine;

public class TrashDropManager : MonoBehaviour
{
    public static TrashDropManager Instance { get; private set; }

    [SerializeField]
    private float _itemLifeTime = 20f;

    [SerializeField]
    private ParticleSystem _groundParticlePrefab;

    private readonly Dictionary<ItemData, Stack<MissionItem>> _pool = new();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SpawnItem(ItemData data, Vector3 position)
    {
        MissionItem item = GetOrCreate(data);
        if (item == null)
        {
            Debug.LogWarning($"[TrashDropManager] {data.dropPrefab.name}에 MissionItem이 없어 드롭하지 않습니다.");
            return;
        }

        var rot = Quaternion.Euler(data.spawnRotation);
        item.transform.SetPositionAndRotation(position, rot);
        item.gameObject.SetActive(true);
    }

    private MissionItem GetOrCreate(ItemData data)
    {
        if (!_pool.TryGetValue(data, out var stack))
        {
            stack = new Stack<MissionItem>();
            _pool[data] = stack;
        }

        return stack.Count > 0 ? stack.Pop() : CreateItem(data);
    }

    private MissionItem CreateItem(ItemData data)
    {
        GameObject go = Instantiate(data.dropPrefab);
        MissionItem item = go.GetComponent<MissionItem>();

        if (item == null)
        {
            Destroy(go);
            return null;
        }

        item.Init(data);
        item.IsPooled = true;
        item.LifeTime = _itemLifeTime;
        item.OnDeactivated += (mi) => ReturnToPool(data, mi);

        if (_groundParticlePrefab != null)
            Instantiate(_groundParticlePrefab, go.transform);

        go.SetActive(false);
        return item;
    }

    private void ReturnToPool(ItemData data, MissionItem item)
    {
        if (!_pool.TryGetValue(data, out var stack))
        {
            stack = new Stack<MissionItem>();
            _pool[data] = stack;
        }
        stack.Push(item);
    }
}
