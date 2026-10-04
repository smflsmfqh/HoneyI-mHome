using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NPCMovement : MonoBehaviour
{
    public enum NpcType
    {
        Patrol,
        Runner,
    }

    [Header("NPC 타입")]
    [SerializeField]
    private NpcType _type = NpcType.Patrol;

    [Header("이동 범위")]
    [SerializeField]
    private float _rectWidth = 20f;

    [SerializeField]
    private float _rectDepth = 10f;

    [Header("Patrol 설정")]
    [SerializeField]
    private float _patrolSpeed = 2.5f;

    [SerializeField]
    private float _patrolWaitTime = 1f;

    [Header("Runner 설정")]
    [SerializeField]
    private float _runSpeed = 5f;

    [SerializeField]
    private float _runWaitTime = 0.5f;

    [Header("NavMesh 영역 설정")]
    [SerializeField]
    private string[] _excludeAreas = { "Road", "Not Walkable" };

    // --- Stuck 판별 필드 ---
    private float _stuckTimer;
    private const float k_stuckThreshold = 1.5f;
    private const float k_stuckSpeedSq = 0.01f;

    // -----------------------

    private NavMeshAgent _agent;
    private Animator _animator;

    private Vector3 _origin;
    private int _areaMask;
    private float _waitTimer;
    private bool _waiting;
    private bool _initialized;
    private bool _externallyPaused;
    private float _animVert;
    private float _currentWaitTime;

    public float MoveBlend => _animVert;

    private static readonly int k_HorID = Animator.StringToHash("Hor");
    private static readonly int k_VertID = Animator.StringToHash("Vert");
    private static readonly int k_StateID = Animator.StringToHash("State");

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (!_initialized)
        {
            _origin = transform.position;
            Setup();
        }
    }

    private void Update()
    {
        if (!_initialized)
            return;
        UpdateMovement();
        UpdateAnimator();
    }

    public void SetupPatrol(
        Vector3 zoneCenter,
        float rectWidth = 20f,
        float rectDepth = 10f,
        float speed = 2.5f,
        float waitTime = 1f
    )
    {
        _origin = zoneCenter;
        _type = NpcType.Patrol;
        _rectWidth = rectWidth;
        _rectDepth = rectDepth;
        _patrolSpeed = speed;
        _patrolWaitTime = waitTime;
        Setup();
    }

    public void SetupRunner(
        Vector3 zoneCenter,
        float rectWidth = 20f,
        float rectDepth = 10f,
        float speed = 5f,
        float waitTime = 0.5f
    )
    {
        _origin = zoneCenter;
        _type = NpcType.Runner;
        _rectWidth = rectWidth;
        _rectDepth = rectDepth;
        _runSpeed = speed;
        _runWaitTime = waitTime;
        Setup();
    }

    private void Setup()
    {
        _initialized = true;
        _areaMask = BuildAreaMask();

        _agent.areaMask = _areaMask;
        _agent.stoppingDistance = 0.5f; // 약간의 여유 확보
        _agent.speed = _type == NpcType.Patrol ? _patrolSpeed : _runSpeed;
        _currentWaitTime = _type == NpcType.Patrol ? _patrolWaitTime : _runWaitTime;

        SetNextRandomDestination();

        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        _agent.avoidancePriority = Random.Range(0, 100);
        _agent.baseOffset += _type == NpcType.Runner ? 0.02f : 0f;
    }

    private int BuildAreaMask()
    {
        int mask = NavMesh.AllAreas;
        foreach (var areaName in _excludeAreas)
        {
            int area = NavMesh.GetAreaFromName(areaName);
            if (area >= 0)
                mask &= ~(1 << area);
        }
        return mask;
    }

    private void SetNextRandomDestination()
    {
        float randomX = Random.Range(-_rectWidth * 0.5f, _rectWidth * 0.5f);
        float randomZ = Random.Range(-_rectDepth * 0.5f, _rectDepth * 0.5f);
        Vector3 targetPos = _origin + new Vector3(randomX, 0f, randomZ);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 5f, _areaMask))
        {
            _agent.SetDestination(hit.position);
            _agent.isStopped = false;
        }
        else
        {
            _waiting = true;
            _waitTimer = _currentWaitTime;
        }
    }

    /// <summary>
    /// CrosswalkWaitZone 등 외부에서 NPC를 일시정지/해제.
    /// true면 내부 이동 로직 전체를 스킵해 CheckStuck이 덮어쓰지 않음.
    /// </summary>
    public void SetExternalPause(bool paused)
    {
        _externallyPaused = paused;
        _agent.isStopped = paused;

        // 해제 시 경로가 없으면 새 목적지를 잡아줌
        // (대기 중 기존 목적지에 도달해 경로가 소멸된 경우 대비)
        if (!paused && !_agent.hasPath && !_waiting)
            SetNextRandomDestination();
    }

    private void UpdateMovement()
    {
        if (_externallyPaused)
            return;

        CheckStuck();

        if (_waiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _waiting = false;
                SetNextRandomDestination();
            }
            return;
        }

        if (HasArrived())
        {
            _agent.isStopped = true;
            _waiting = true;
            _waitTimer = _currentWaitTime;
        }
    }

    private void CheckStuck()
    {
        if (_waiting)
        {
            _stuckTimer = 0f;
            return;
        }

        if (_agent.velocity.sqrMagnitude < k_stuckSpeedSq)
            _stuckTimer += Time.deltaTime;
        else
            _stuckTimer = 0f;

        if (_stuckTimer >= k_stuckThreshold)
        {
            _stuckTimer = 0f;
            SetNextRandomDestination();
        }
    }

    private void UpdateAnimator()
    {
        var moving = !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.01f;
        _animVert = Mathf.MoveTowards(_animVert, moving ? 1f : 0f, Time.deltaTime * 4.5f);

        _animator.SetFloat(k_HorID, 0f);
        _animator.SetFloat(k_VertID, _animVert);
        _animator.SetFloat(k_StateID, _type == NpcType.Runner ? 1f : 0f);
    }

    private bool HasArrived()
    {
        return !_agent.pathPending
            && _agent.hasPath
            && _agent.remainingDistance <= _agent.stoppingDistance;
    }
}
