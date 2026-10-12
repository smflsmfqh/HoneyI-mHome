using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class CatMovement : MonoBehaviour
{
    [Header("추격 대상")]
    [SerializeField]
    private PlayerHealth _player;
    private PlayerMovement _playerMovement;

    [Header("가시 범위")]
    [SerializeField]
    private float _detectionRadius = 8f;

    [SerializeField]
    private LayerMask _playerLayer;

    [Header("배회 설정")]
    [SerializeField]
    private float _wanderRadius = 10f;

    [SerializeField]
    private float _wanderSpeed = 2f;

    [SerializeField]
    private float _wanderWaitTime = 1f;

    [Header("추격 설정")]
    [SerializeField]
    private float _runSpeed = 4f;

    [SerializeField]
    private float _stopDistance = 1.5f;

    [Header("공격 설정")]
    [SerializeField]
    private float _attackRadius = 1.5f;

    [SerializeField]
    private float _attackCoolDown = 5f;
    private float _lastAttackTime = -999f;

    private bool _isAttacking = false;
    private CancellationTokenSource _attackCts;

    private Vector3 _lastSetDestination;
    private const float DESTINATION_UPDATE_DIST_SQ = 0.25f; // 0.5m 이상 이동 시 재계산

    [Header("야옹 소리")]
    [SerializeField]
    private AudioSource _audioSource;

    [SerializeField]
    private AudioClip[] _meowClips;

    [SerializeField]
    private float _meowIntervalMin = 2f;

    [SerializeField]
    private float _meowIntervalMax = 5f;

    private ProximityFeedback _proximity;

    [Header("NavMesh 영역 설정")]
    private NavMeshAgent _agent;
    private Animator _animator;
    private CatHeadDamage _headDamage;

    private enum State
    {
        Wander,
        Chase,
    }

    private State _state = State.Wander;
    private string _excludeArea = "Road";
    private int _walkableMask;
    private bool _isWaiting;
    private bool _isFound = false;
    private float _waitTimer;
    private bool _initialized;
    private bool _hasDestination = false;
    private int _currentAnim; // 0 = 강제 재생 필요

    private static readonly int EAT_ID = Animator.StringToHash("Eat");
    private static readonly int RUN_ID = Animator.StringToHash("Run");
    private static readonly int IDLE_ID = Animator.StringToHash("Idle_A");
    private static readonly int WALK_ID = Animator.StringToHash("Walk");
    private static readonly int IS_FOUND_ID = Animator.StringToHash("isFound");
    private CancellationTokenSource _meowCts;
    private bool _isTutorialCat;
    private bool _externallyPaused;
    private Vector3 _home; // 배회 기준점 (스폰 위치)

    public void MarkAsTutorialCat() => _isTutorialCat = true;

    public bool IsExternallyPaused => _externallyPaused;

    public void SetExternalPause(bool paused)
    {
        _externallyPaused = paused;
        if (_agent != null)
            _agent.isStopped = paused;
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _headDamage = GetComponentInChildren<CatHeadDamage>(true);
        if (_audioSource != null)
        {
            SpatialAudio.ApplyDirectionalOnly(_audioSource);
            _audioSource.volume = 0f;
        }
    }

    private void Start()
    {
        _home = transform.position;
        Setup();
        SetWanderTarget();
        if (_player != null)
        {
            _playerMovement = _player.GetComponent<PlayerMovement>();
            _proximity = _player.GetComponent<ProximityFeedback>();
        }
        if (_proximity != null)
            _proximity.RegisterDanger(transform);
        if (_audioSource != null && _meowClips != null && _meowClips.Length > 0)
            StartMeowLoop();
    }

    private void OnDisable()
    {
        CancelCts(ref _attackCts);
        CancelCts(ref _meowCts);
    }

    private static void CancelCts(ref CancellationTokenSource cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void OnDestroy()
    {
        if (_proximity != null)
            _proximity.UnregisterDanger(transform);
    }

    private void Update()
    {
        if (!_initialized || _externallyPaused)
            return;

        UpdateMeowVolume();

        bool detected =
            Physics.CheckSphere(transform.position, _detectionRadius, _playerLayer)
            && (_playerMovement == null || !_playerMovement.IsCrouching);

        if (detected && _state != State.Chase)
            EnterChase();
        if (!detected && _state != State.Wander)
            EnterWander();

        switch (_state)
        {
            case State.Chase:
                UpdateChase();
                break;
            case State.Wander:
                UpdateWander();
                break;
        }

        UpdateAnimation();
    }

    public void SetPlayer(PlayerHealth player)
    {
        _player = player;
        _playerMovement = player != null ? player.GetComponent<PlayerMovement>() : null;
    }

    private void Setup()
    {
        _initialized = true;
        int roadAreaIndex = NavMesh.GetAreaFromName(_excludeArea);
        _walkableMask =
            roadAreaIndex >= 0 ? NavMesh.AllAreas & ~(1 << roadAreaIndex) : NavMesh.AllAreas;

        _agent.areaMask = _walkableMask;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void EnterChase()
    {
        _state = State.Chase;
        _agent.speed = _runSpeed;
        _agent.stoppingDistance = _stopDistance;
        _agent.updateRotation = false;
        _hasDestination = false;
        _isFound = true;
        _isWaiting = false;
        _lastSetDestination = Vector3.positiveInfinity; // 진입 직후 즉시 경로 계산
    }

    private void EnterWander()
    {
        _state = State.Wander;
        _agent.speed = _wanderSpeed;
        _agent.stoppingDistance = 0f;
        _agent.updateRotation = true;

        _isFound = false;

        SetWanderTarget();
    }

    private void UpdateChase()
    {
        if (_player == null || !_agent.isOnNavMesh)
            return;

        Vector3 playerPos = _player.transform.position;
        if ((playerPos - _lastSetDestination).sqrMagnitude >= DESTINATION_UPDATE_DIST_SQ)
        {
            _agent.SetDestination(playerPos);
            _lastSetDestination = playerPos;
        }

        Vector3 dir = (_player.transform.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        TryAttack();
    }

    private void UpdateWander()
    {
        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _isWaiting = false;
                SetWanderTarget();
                return;
            }
        }

        if (
            _hasDestination
            && _agent.isOnNavMesh
            && !_agent.pathPending
            && _agent.remainingDistance <= _agent.stoppingDistance
        )
        {
            _hasDestination = false;
            _isWaiting = true;
            _waitTimer = _wanderWaitTime;
        }
    }

    private void SetWanderTarget()
    {
        int maxCount = 10;
        for (int i = 0; i < maxCount; i++)
        {
            Vector3 randomPos = _home + Random.insideUnitSphere * _wanderRadius;
            if (
                _agent.isOnNavMesh
                && NavMesh.SamplePosition(
                    randomPos,
                    out NavMeshHit hit,
                    _wanderRadius,
                    _walkableMask
                )
            )
            {
                _agent.SetDestination(hit.position);
                _hasDestination = true;
                return;
            }
        }
    }

    private void TryAttack()
    {
        if (_isAttacking)
            return;

        float dist = Vector3.Distance(transform.position, _player.transform.position);
        if (dist > _attackRadius)
            return;

        if (Time.time - _lastAttackTime < _attackCoolDown)
            return;

        _lastAttackTime = Time.time;
        CancelCts(ref _attackCts);
        _attackCts = new CancellationTokenSource();
        AttackAsync(_attackCts.Token).Forget();
    }

    private async UniTaskVoid AttackAsync(CancellationToken ct)
    {
        try
        {
            _isAttacking = true;
            _agent.isStopped = true;

            _animator.Play(EAT_ID);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);

            float clipLength = _animator.GetCurrentAnimatorStateInfo(0).length;

            await UniTask.Delay(TimeSpan.FromSeconds(clipLength * 0.5f), DelayType.DeltaTime, cancellationToken: ct);
            if (
                _headDamage != null
                && _player != null
                && Vector3.Distance(transform.position, _player.transform.position) <= _attackRadius
            )
                _headDamage.TryDamage(_player);

            await UniTask.Delay(TimeSpan.FromSeconds(clipLength * 0.5f), DelayType.DeltaTime, cancellationToken: ct);

            // 공격 도중 걸린 외부 일시정지(게임오버·튜토리얼)를 풀지 않도록
            _agent.isStopped = _externallyPaused;
            _currentAnim = 0;
            _isAttacking = false;
        }
        catch (OperationCanceledException) { }
    }

    private void UpdateAnimation()
    {
        if (_isAttacking)
            return;

        int targetMove;
        if (_state == State.Chase)
        {
            targetMove = RUN_ID;
        }
        else if (_isWaiting || _agent.velocity.magnitude < 0.1f)
        {
            targetMove = IDLE_ID;
        }
        else
            targetMove = WALK_ID;

        if (_currentAnim != targetMove)
        {
            _currentAnim = targetMove;
            _animator.Play(targetMove);
            _animator.SetBool(IS_FOUND_ID, _isFound);
        }
    }

    private void UpdateMeowVolume()
    {
        if (_audioSource == null || _player == null || _proximity == null)
            return;

        float dist = Vector3.Distance(transform.position, _player.transform.position);
        float t = Mathf.Clamp01((dist - _proximity.PanicRadius) / (_detectionRadius - _proximity.PanicRadius));
        _audioSource.volume = 1f - t;
    }

    private void StartMeowLoop()
    {
        CancelCts(ref _meowCts);
        _meowCts = new CancellationTokenSource();
        MeowLoopAsync(_meowCts.Token).Forget();
    }

    private async UniTaskVoid MeowLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                float wait = Random.Range(_meowIntervalMin, _meowIntervalMax);
                await UniTask.Delay(TimeSpan.FromSeconds(wait), DelayType.DeltaTime, cancellationToken: ct);
                var clip = _meowClips[Random.Range(0, _meowClips.Length)];
                _audioSource.PlayOneShot(clip);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
}
