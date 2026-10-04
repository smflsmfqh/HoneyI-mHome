using UnityEngine;

public class FootStompCollider : MonoBehaviour
{
    [SerializeField]
    private CauseDeath _cause = CauseDeath.NPC;

    [SerializeField]
    private float _damage = 25f;

    [Header("발소리")]
    [SerializeField]
    private AudioSource _audioSource;

    [SerializeField]
    private AudioClip _footstepClip;

    [SerializeField]
    private float _hearRadius = 6f;

    private Transform _playerTransform;
    private ProximityFeedback _proximity;
    private NPCMovement _npc;

    private void Start()
    {
        _npc = GetComponent<NPCMovement>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _proximity = player.GetComponent<ProximityFeedback>();
        }

        if (_audioSource != null && _footstepClip != null)
        {
            _audioSource.clip = _footstepClip;
            SpatialAudio.ApplyDirectionalOnly(_audioSource);
            _audioSource.loop = true;
            _audioSource.volume = 0f;
            _audioSource.Play();
        }
    }

    private void Update()
    {
        UpdateFootVolume();
    }

    private void UpdateFootVolume()
    {
        if (_audioSource == null || _playerTransform == null)
            return;

        float panicRadius = _proximity != null ? _proximity.PanicRadius : 1f;
        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        float t = Mathf.Clamp01((dist - panicRadius) / (_hearRadius - panicRadius));
        float moveBlend = _npc != null ? _npc.MoveBlend : 1f;
        _audioSource.volume = (1f - t) * moveBlend;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (!other.TryGetComponent<PlayerHealth>(out var health))
            return;

        health.TakeDamage(_damage, _cause);
    }
}
