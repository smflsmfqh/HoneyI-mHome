using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public enum MoveMode
    {
        Move,
        Sprint,
        Roll,
        Crouch,
        None,
    }

    [Header("PlayerHealth")]
    [SerializeField]
    private PlayerHealth _playerHealth;

    [Header("Animation")]
    [SerializeField]
    private Animator _animator;

    private int _currentFace; // 0 = 아직 재생 전

    // 표정 레이어(1) 상태
    private static readonly int FACE_BLINK_ID = Animator.StringToHash("Eyes_Blink");
    private static readonly int FACE_CRY_ID = Animator.StringToHash("Eyes_Cry");
    private static readonly int FACE_DEAD_ID = Animator.StringToHash("Eyes_Dead");
    private static readonly int FACE_HAPPY_ID = Animator.StringToHash("Eyes_Happy");
    private static readonly int FACE_SHRINK_ID = Animator.StringToHash("Eyes_Shrink");
    private static readonly int FACE_EXCITED_ID = Animator.StringToHash("Eyes_Excited");
    private static readonly int FACE_TRAUMA_ID = Animator.StringToHash("Eyes_Trauma");

    [Header("Movement Speed")]
    [SerializeField]
    private float _moveSpeed = 0.8f;

    [Header("Jump Field")]
    [SerializeField]
    private float _jumpForce = 2.2f;
    private readonly HashSet<Collider> _groundContacts = new();
    private bool _isGrounded => _groundContacts.Count > 0;

    [Header("Player Rotation")]
    [SerializeField]
    private float _mouseSensitivity = 0.15f;

    [SerializeField]
    private float _minPitch = -20f;

    [SerializeField]
    private float _maxPitch = 10f;

    [Header("Roll Field")]
    private bool _isRolling = false;
    private float _lastRollTime = -999f;
    private Vector3 _rollDirection;
    private CancellationTokenSource _rollCts;

    [SerializeField]
    private float _rollSpeed = 2f;

    [SerializeField]
    private float _rollDuration = 0.7f;

    [SerializeField]
    private float _rollCoolDown = 5f;

    private float _standHeight;

    [Header("Crouch Field")]
    private float _crouchHeight;

    [SerializeField]
    private float _crouchSpeed = 0.5f;

    [SerializeField]
    private bool _isCrouching = false;

    private Vector3 _prevColliderCenter;
    private bool? _crouchShapeApplied; // 마지막으로 적용한 콜라이더 형태 (null = 아직 적용 전)

    [Header("Sprint Field")]
    [SerializeField]
    private float _sprintSpeed = 1.7f;

    [SerializeField]
    private float _sprintTotalTime = 7f;
    private float _sprintDuration;
    private bool _isSprint = false;

    [Header("노멀법선 field")]
    [SerializeField]
    private float yNormal = 0.2f;

    private Rigidbody _rb;
    private CapsuleCollider _collider;
    private PlayerController _playerController;
    private Vector2 _inputDir;
    private float _currentSpeed;
    private float _boostSpeed;
    private MoveMode _mode = MoveMode.Move;
    private bool _isSpeedBoost = false;
    private CancellationTokenSource _speedBoostCts;
    private float _hangoverMultiplier = 1f;
    private readonly List<Vector3> _contactNormals = new();
    private readonly ContactPoint[] _contactBuffer = new ContactPoint[8];

    // 값이 바뀔 때만 이벤트를 보내기 위한 직전 전송값 (-1 = 아직 전송 전)
    private float _lastSprintRatio = -1f;
    private float _lastRollRatio = -1f;

    private static readonly int SPEED_ID = Animator.StringToHash("Speed");
    private static readonly int IS_GROUNDED_ID = Animator.StringToHash("IsGrounded");
    private static readonly int IS_ROLLING_ID = Animator.StringToHash("IsRolling");
    private static readonly int IS_CROUCHING_ID = Animator.StringToHash("isCrouching");

    // --- 이벤트 관련 필드 ---
    public event Action<float> OnSprintChanged;
    public event Action<bool> OnSprintActive;
    public event Action<float> OnRollChanged;
    public event Action<bool> OnRollActive;

    // --- 외부 호출 가능 속성 필드 ---
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public bool IsCrouching => _isCrouching;
    public bool IsGrounded => _isGrounded;

    public Vector2 InputDir => _inputDir;
    public float SprintRatio => _sprintDuration / _sprintTotalTime;
    public float RollRatio => Mathf.Clamp01((Time.time - _lastRollTime) / _rollCoolDown);

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _collider = GetComponent<CapsuleCollider>();
        foreach (var c in GetComponentsInChildren<Collider>(true))
            if (c != _collider && !c.isTrigger)
                Physics.IgnoreCollision(_collider, c);
        _collider.sharedMaterial = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };
        _animator = GetComponent<Animator>();
        _playerController = GetComponent<PlayerController>();

        if (_animator != null)
            _animator.applyRootMotion = false;

        _standHeight = _collider.height;
        _crouchHeight = _standHeight * 0.5f;
        _prevColliderCenter = _collider.center;
        _sprintDuration = _sprintTotalTime;
        Yaw = transform.eulerAngles.y;
    }

    // 형태가 바뀔 때만 콜라이더를 갱신 — Roll은 호출하지 않으므로 직전 형태를 유지
    private void ApplyColliderShape(bool crouched)
    {
        if (_crouchShapeApplied == crouched)
            return;
        _crouchShapeApplied = crouched;

        if (crouched)
        {
            _collider.height = _crouchHeight;
            _collider.center = new Vector3(
                _prevColliderCenter.x,
                _prevColliderCenter.y - (_standHeight - _crouchHeight) / 2f,
                _prevColliderCenter.z
            );
        }
        else
        {
            _collider.height = _standHeight;
            _collider.center = _prevColliderCenter;
        }
    }

    private void Start()
    {
        RaiseSprintChanged();
        OnSprintActive?.Invoke(_isSprint);
        RaiseRollChanged(1f);
        OnRollActive?.Invoke(false);
    }

    private void OnDisable()
    {
        CancelCts(ref _rollCts);
        CancelCts(ref _speedBoostCts);
    }

    private static void CancelCts(ref CancellationTokenSource cts)
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private void FixedUpdate()
    {
        _rb.MoveRotation(Quaternion.Euler(0f, Yaw, 0f));

        // 우선순위: Roll > Crouch > Sprint > Move
        // SpeedBoost는 _currentSpeed만 덮어쓰므로 모드 결정에서 제외 — 콜라이더 관리가 끊기지 않도록
        if (_isRolling)
            _mode = MoveMode.Roll;
        else if (_isCrouching)
            _mode = MoveMode.Crouch;
        else if (_isSprint && _sprintDuration > 0)
            _mode = MoveMode.Sprint;
        else
            _mode = MoveMode.Move;

        switch (_mode)
        {
            case MoveMode.Move:
                {
                    _currentSpeed = _moveSpeed * _hangoverMultiplier;
                    ApplyColliderShape(crouched: false);

                    _sprintDuration += Time.fixedDeltaTime;
                    if (_sprintDuration >= _sprintTotalTime)
                        _sprintDuration = _sprintTotalTime;

                    RaiseSprintChanged();
                }
                break;
            case MoveMode.Sprint:
                {
                    _currentSpeed = (_moveSpeed + _sprintSpeed) * _hangoverMultiplier;
                    // 앉기를 뗀 직후 바로 Sprint로 들어와도 서 있는 높이로 복원
                    ApplyColliderShape(crouched: false);
                    _sprintDuration -= Time.fixedDeltaTime;

                    if (_sprintDuration <= 0)
                    {
                        _isSprint = false;
                        _sprintDuration = 0f;
                        OnSprintActive?.Invoke(_isSprint);
                    }
                    RaiseSprintChanged();
                }
                break;
            case MoveMode.Roll:
            {
                Vector3 rollVelocity = _rollDirection * _rollSpeed;
                rollVelocity.y = _rb.linearVelocity.y;
                _rb.linearVelocity = rollVelocity;
                break;
            }
            case MoveMode.Crouch:
                {
                    _currentSpeed = _crouchSpeed * _hangoverMultiplier;
                    ApplyColliderShape(crouched: true);
                }
                break;
        }

        RaiseRollChanged(RollRatio);

        if (_isSpeedBoost)
        {
            _currentSpeed = _boostSpeed;
            if (_isSprint && _sprintDuration > 0)
            {
                _currentSpeed += _sprintSpeed;
                _sprintDuration -= Time.fixedDeltaTime;
                if (_sprintDuration <= 0)
                {
                    _isSprint = false;
                    _sprintDuration = 0f;
                }
                RaiseSprintChanged();
            }
        }

        bool hasInput = _inputDir.sqrMagnitude > 0.01f;

        // 입력이 없을 때 XZ 이동을 물리 엔진이 덮어쓰지 못하도록 constraint로 고정
        if (!hasInput && _mode != MoveMode.Roll)
            _rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        else
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (_mode != MoveMode.Roll)
        {
            Vector3 move =
                (transform.forward * _inputDir.y + transform.right * _inputDir.x) * _currentSpeed;

            // 속도 부스트 중 벽 충돌 노멀 방향으로 이동 성분을 제거해 끼임 방지
            if (_isSpeedBoost)
            {
                foreach (var normal in _contactNormals)
                {
                    if (Vector3.Dot(move, normal) < 0f)
                        move = Vector3.ProjectOnPlane(move, normal);
                }
            }

            move.y = _rb.linearVelocity.y;
            _rb.linearVelocity = move;
        }

        _contactNormals.Clear();

        if (_mode != MoveMode.Roll && _isGrounded && _rb.linearVelocity.y <= 0f)
        {
            var v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
        }

        float horizSpeed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
        _animator.SetFloat(SPEED_ID, horizSpeed);
        _animator.SetBool(IS_GROUNDED_ID, _isGrounded);
        _animator.SetBool(IS_ROLLING_ID, _isRolling);
        _animator.SetBool(IS_CROUCHING_ID, _isCrouching);
    }

    private void RaiseSprintChanged()
    {
        float ratio = SprintRatio;
        if (Mathf.Approximately(ratio, _lastSprintRatio))
            return;
        _lastSprintRatio = ratio;
        OnSprintChanged?.Invoke(ratio);
    }

    private void RaiseRollChanged(float ratio)
    {
        if (Mathf.Approximately(ratio, _lastRollRatio))
            return;
        _lastRollRatio = ratio;
        OnRollChanged?.Invoke(ratio);
    }

    private void PlayFace(int stateHash)
    {
        if (_currentFace == stateHash || _animator == null)
            return;

        _currentFace = stateHash;
        _animator.Play(stateHash, 1);
    }

    public void ResetFace() => PlayFace(FACE_BLINK_ID);

    public void SetFaceDamaged() => PlayFace(FACE_CRY_ID);

    public void SetFaceDead() => PlayFace(FACE_DEAD_ID);

    public void SetFaceHappy() => PlayFace(FACE_HAPPY_ID);

    public void SetFaceShrink() => PlayFace(FACE_SHRINK_ID);

    public void SetFaceExcited() => PlayFace(FACE_EXCITED_ID);

    public void SetFaceTrauma() => PlayFace(FACE_TRAUMA_ID);

    private void OnMove(InputValue value)
    {
        _inputDir = value.Get<Vector2>();
    }

    private void OnSprint(InputValue value)
    {
        if (value.isPressed)
        {
            _isSprint = true;
            OnSprintActive?.Invoke(_isSprint);
        }
        else
        {
            _isSprint = false;
            OnSprintActive?.Invoke(_isSprint);
        }
    }

    private void OnLook(InputValue value)
    {
        Vector2 delta = value.Get<Vector2>() * _mouseSensitivity;
        Yaw += delta.x;
        Pitch = Mathf.Clamp(Pitch - delta.y, _minPitch, _maxPitch);
    }

    private void OnJump(InputValue value)
    {
        if (value.isPressed && _isGrounded)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _playerController?.PlayJumpSound();
        }
    }

    private void OnCrouch(InputValue value)
    {
        _isCrouching = value.isPressed;
    }

    private void OnRoll(InputValue value)
    {
        if (!value.isPressed || _isRolling)
            return;

        if (Time.time - _lastRollTime < _rollCoolDown)
            return;

        _rollDirection =
            (_inputDir.magnitude > 0.1f)
                ? (transform.forward * _inputDir.y + transform.right * _inputDir.x).normalized
                : transform.forward;
        _playerController?.PlayRollSound();
        CancelCts(ref _rollCts);
        _rollCts = new CancellationTokenSource();
        RollAsync(_rollCts.Token).Forget();
    }

    public void CancelRoll()
    {
        if (!_isRolling)
            return;
        CancelCts(ref _rollCts);
        _isRolling = false;
    }

    private async UniTaskVoid RollAsync(CancellationToken ct)
    {
        _isRolling = true;
        OnRollActive?.Invoke(true);
        _lastRollTime = Time.time;
        _playerHealth.SetInvincible(_rollDuration, "roll");

        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_rollDuration), DelayType.DeltaTime, cancellationToken: ct);
            _isRolling = false;
            OnRollActive?.Invoke(false);
        }
        catch (OperationCanceledException) { }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsGroundContact(collision))
            _groundContacts.Add(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        // 속도 부스트 중 벽면 노멀 수집 (바닥 제외)
        if (!_isSpeedBoost) return;
        int count = collision.GetContacts(_contactBuffer);
        for (int i = 0; i < count; i++)
        {
            if (_contactBuffer[i].normal.y < 0.7f)
                _contactNormals.Add(_contactBuffer[i].normal);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        _groundContacts.Remove(collision.collider);
    }

    private bool IsGroundContact(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Ground") && !collision.gameObject.CompareTag("Road"))
        {
            return false;
        }

        int count = collision.GetContacts(_contactBuffer);
        for (int i = 0; i < count; i++)
        {
            if (_contactBuffer[i].normal.y > yNormal)
                return true;
        }

        return false;
    }

    public void SetHangoverDebuff(float multiplier)
    {
        _hangoverMultiplier = multiplier;
    }

    public void SetSpeedBoost(float speed, float sec)
    {
        CancelCts(ref _speedBoostCts);
        _speedBoostCts = new CancellationTokenSource();
        SpeedBoostAsync(speed, sec, _speedBoostCts.Token).Forget();
    }

    private async UniTaskVoid SpeedBoostAsync(float speed, float sec, CancellationToken ct)
    {
        _isSpeedBoost = true;
        _boostSpeed = speed;
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(sec), DelayType.DeltaTime, cancellationToken: ct);
            _isSpeedBoost = false;
            _currentSpeed = _moveSpeed;
        }
        catch (OperationCanceledException) { }
    }
}
