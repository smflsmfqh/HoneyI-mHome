using System;
using System.Collections;
using System.Collections.Generic;
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

    private string _currentFace = string.Empty;

    [Header("Movement Speed")]
    [SerializeField]
    private float _moveSpeed = 0.8f;

    [Header("Jump Field")]
    [SerializeField]
    private float _jumpForce = 2.5f;
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
    private Coroutine _rollCoroutine;

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
    private Coroutine _speedCo;
    private float _hangoverMultiplier = 1f;
    private readonly List<Vector3> _contactNormals = new();

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

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _collider = GetComponent<CapsuleCollider>();
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

    private void Start()
    {
        OnSprintChanged?.Invoke(_sprintDuration / _sprintTotalTime);
        OnSprintActive?.Invoke(_isSprint);
        OnRollChanged?.Invoke(1f);
        OnRollActive?.Invoke(false);
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
                    _collider.height = _standHeight;
                    _collider.center = _prevColliderCenter;

                    _sprintDuration += Time.fixedDeltaTime;
                    if (_sprintDuration >= _sprintTotalTime)
                        _sprintDuration = _sprintTotalTime;

                    OnSprintChanged?.Invoke(_sprintDuration / _sprintTotalTime);
                }
                break;
            case MoveMode.Sprint:
                {
                    _currentSpeed = (_moveSpeed + _sprintSpeed) * _hangoverMultiplier;
                    // 앉기를 뗀 직후 바로 Sprint로 들어와도 서 있는 높이로 복원
                    _collider.height = _standHeight;
                    _collider.center = _prevColliderCenter;
                    _sprintDuration -= Time.fixedDeltaTime;

                    if (_sprintDuration <= 0)
                    {
                        _isSprint = false;
                        _sprintDuration = 0f;
                        OnSprintActive?.Invoke(_isSprint);
                    }
                    OnSprintChanged?.Invoke(_sprintDuration / _sprintTotalTime);
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
                    _collider.height = _crouchHeight;
                    _collider.center = new Vector3(
                        _prevColliderCenter.x,
                        _prevColliderCenter.y - (_standHeight - _crouchHeight) / 2f,
                        _prevColliderCenter.z
                    );
                }
                break;
        }

        float rollRatio = Mathf.Clamp01((Time.time - _lastRollTime) / _rollCoolDown);
        OnRollChanged?.Invoke(rollRatio);

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
                OnSprintChanged?.Invoke(_sprintDuration / _sprintTotalTime);
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
        _animator.SetFloat("Speed", horizSpeed);
        _animator.SetBool("IsGrounded", _isGrounded);
        _animator.SetBool("IsRolling", _isRolling);
        _animator.SetBool("isCrouching", _isCrouching);
    }

    public void PlayFace(string stateName)
    {
        if (_currentFace == stateName || _animator == null)
            return;

        _currentFace = stateName;
        _animator.Play(stateName, 1);
    }

    public void ResetFace() => PlayFace("Eyes_Blink");

    public void SetFaceDamaged() => PlayFace("Eyes_Cry");

    public void SetFaceDead() => PlayFace("Eyes_Dead");

    public void SetFaceHappy() => PlayFace("Eyes_Happy");

    public void SetFaceShrink() => PlayFace("Eyes_Shrink");

    public void SetFaceExcited() => PlayFace("Eyes_Excited");

    public void SetFaceTrauma() => PlayFace("Eyes_Trauma");

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
        _rollCoroutine = StartCoroutine(RollCoroutine());
    }

    public void CancelRoll()
    {
        if (_rollCoroutine == null)
            return;
        StopCoroutine(_rollCoroutine);
        _rollCoroutine = null;
        _isRolling = false;
    }

    private IEnumerator RollCoroutine()
    {
        _isRolling = true;
        OnRollActive?.Invoke(true);
        _lastRollTime = Time.time;
        _playerHealth.SetInvincible(_rollDuration, "roll");

        yield return new WaitForSeconds(_rollDuration);

        _rollCoroutine = null;
        _isRolling = false;
        OnRollActive?.Invoke(false);
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
        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y < 0.7f)
                _contactNormals.Add(contact.normal);
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

        foreach (var c in collision.contacts)
        {
            if (c.normal.y > yNormal)
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
        if (_speedCo != null)
            StopCoroutine(_speedCo);

        _speedCo = StartCoroutine(SpeedBoostCoroutine(speed, sec));
    }

    private IEnumerator SpeedBoostCoroutine(float speed, float sec)
    {
        _isSpeedBoost = true;
        _boostSpeed = speed;
        yield return new WaitForSeconds(sec);

        _isSpeedBoost = false;
        _currentSpeed = _moveSpeed;
    }
}
