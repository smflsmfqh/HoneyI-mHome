using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-1)]
public class NPCSpawnManager : MonoBehaviour
{
    [SerializeField]
    private List<NPCMovement> _npcPrefabs;

    [SerializeField]
    private NPCSpawnZone[] _spawnZones;

    [SerializeField]
    private Transform _player;

    [SerializeField]
    private NPCVisualData _visualData;

    private Dictionary<NPCSpawnZone, Queue<NPCMovement>> _zonePools = new();
    private Dictionary<NPCSpawnZone, List<NPCMovement>> _zoneActives = new();

    [SerializeField]
    private int _minNpcsPerZone = 5;

    [SerializeField]
    private int _maxNpcsPerZone = 8;

    [SerializeField]
    private float _patrolPercent = 0.7f;

    private void Start()
    {
        foreach (var zone in _spawnZones)
        {
            InitZonePool(zone);
            SpawnInZone(zone);
        }
    }

    private void InitZonePool(NPCSpawnZone zone)
    {
        if (_npcPrefabs == null || _npcPrefabs.Count == 0)
            return;
        var pool = new Queue<NPCMovement>();
        int npcsPerZone = Random.Range(_minNpcsPerZone, _maxNpcsPerZone + 1);

        for (int i = 0; i < npcsPerZone; i++)
        {
            var prefab = _npcPrefabs[Random.Range(0, _npcPrefabs.Count)];
            var npc = Instantiate(prefab);
            npc.gameObject.SetActive(false);
            pool.Enqueue(npc);
        }

        _zonePools[zone] = pool;
        _zoneActives[zone] = new List<NPCMovement>();
    }

    private void SpawnInZone(NPCSpawnZone zone)
    {
        if (_zonePools == null || _zonePools.Count == 0)
            return;

        int walkable = 1 << NavMesh.GetAreaFromName("Walkable");
        var pool = _zonePools[zone];
        var actives = _zoneActives[zone];

        while (pool.Count > 0)
        {
            if (!zone.TryGetSpawnPoint(walkable, out Vector3 spawnPos))
                break;

            var npc = pool.Dequeue();
            npc.transform.position = spawnPos;
            npc.gameObject.SetActive(true);

            if (_visualData != null)
            {
                var customizer = npc.GetComponent<NPCVisualizeCustomizer>();

                if (customizer != null)
                {
                    customizer.ApplyRandomVisuals(_visualData);
                }
            }

            if (Random.value < _patrolPercent)
                npc.SetupPatrol(spawnPos, zone.Size.x, zone.Size.z);
            else
                npc.SetupRunner(spawnPos, zone.Size.x, zone.Size.z);

            actives.Add(npc);
        }
    }
}
