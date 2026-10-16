using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class FollowCamera : MonoBehaviour
{
    [SerializeField]
    private PlayerMovement _player;

    [SerializeField]
    private float _scrollSensitivity = 5f;

    [SerializeField]
    private Vector3 _offset = new Vector3(0f, 0.05f, -0.35f);

    [Header("Reaction Cut")]
    [SerializeField]
    private Vector3 _reactionOffset = new Vector3(-0.04f, 0.012f, 0.005f);

    [SerializeField]
    private Vector3 _reactionLookAtOffset = new Vector3(0f, 0.015f, 0.01f);

    [SerializeField]
    private float _reactionInDuration = 0.15f;

    [SerializeField]
    private float _reactionOutDuration = 0.3f;

    [Header("Feedback System")]
    [SerializeField]
    private ProximityFeedback _proximity;

    [SerializeField]
    private float _maxShakeAmount = 0.008f;
    private float _shakeIntensity;

    private float _defaultDistance;
    private float _currentDistance;
    private Vector3 _activeOffset;
    private bool _isReacting;
    private bool _useLookAt;

    [SerializeField]
    private float _maxZoomOut = 2.5f;

    [Tooltip(
        "휠(Zoom 액션, <Mouse>/scroll/y) 값은 노치당 값이 레거시 Input.GetAxis(\"Mouse ScrollWheel\")보다 " +
        "훨씬 큰 단위(Windows 기준 노치당 약 ±120)로 들어온다. 이전 _scrollSensitivity 값이 그대로 쓰이도록 " +
        "같은 스케일(노치당 대략 ±0.1)로 나눠주는 계수 — 실제 플레이 체감이 이전과 다르면 여기서 조정."
    )]
    [SerializeField]
    private float _scrollUnitScale = 1200f;

    private InputAction _zoomAction;

    private void Start()
    {
        // PlayerInput이 OnEnable에서 액션 사본을 만든 뒤에 가져와야 DeactivateInput이 줌에도 적용된다
        if (_player != null && _player.TryGetComponent<PlayerInput>(out var playerInput))
            _zoomAction = playerInput.actions.FindAction("Zoom");
    }

    private float ReadScrollDelta()
    {
        if (_zoomAction == null || _scrollUnitScale == 0f)
            return 0f;

        return _zoomAction.ReadValue<float>() / _scrollUnitScale;
    }

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _defaultDistance = _offset.magnitude;
        _currentDistance = _defaultDistance;
        _activeOffset = _offset;
        GetComponent<Camera>().nearClipPlane = 0.01f;
    }

    private CancellationTokenSource _reactionCts;

    private void OnEnable()
    {
        if (_proximity != null)
        {
            _proximity.OnPanicChanged += HandlePanic;
            // 이벤트는 값이 바뀔 때만 오므로, 이미 패닉 상태에서 켜져도 흔들리도록 현재 값으로 맞춘다
            HandlePanic(_proximity.IsPanic);
        }
    }

    private void OnDisable()
    {
        if (_proximity != null)
            _proximity.OnPanicChanged -= HandlePanic;

        // 작업만 취소하면 _isReacting이 true로 남아 다시 켜졌을 때 줌·리액션이 막히므로 상태까지 되돌린다
        CancelReaction();
    }

    private void LateUpdate()
    {
        if (_player == null)
            return;

        // 일시정지·결과 화면에서는 GameManager가 PlayerInput을 꺼서 Zoom 액션 값도 0이 된다
        if (!_isReacting)
        {
            _currentDistance -= ReadScrollDelta() * _scrollSensitivity;
            _currentDistance = Mathf.Clamp(
                _currentDistance,
                _defaultDistance,
                _defaultDistance + _maxZoomOut
            );
            _activeOffset = _offset.normalized * _currentDistance;
        }

        // 일반 모드: 월드 Yaw/Pitch 기반
        Quaternion playerRot = Quaternion.Euler(0f, _player.Yaw, 0f);
        Quaternion fullRot = Quaternion.Euler(_player.Pitch, _player.Yaw, 0f);
        Vector3 normalPos = _player.transform.position + playerRot * _activeOffset;

        transform.position = normalPos;

        if (_useLookAt)
        {
            Vector3 lookTarget = _player.transform.position + playerRot * _reactionLookAtOffset;
            transform.LookAt(lookTarget);
        }
        else
        {
            transform.rotation = fullRot;
        }

        // 흔들림은 deltaTime이 아니라 매 프레임 난수라 timeScale 0에서도 계속되므로 직접 막는다 (일시정지·게임오버·클리어)
        if (Time.timeScale > 0f)
        {
            Vector3 shakeOffset = UnityEngine.Random.insideUnitCircle * (_maxShakeAmount * _shakeIntensity);
            transform.position += shakeOffset;
        }
    }

    private void HandlePanic(bool isPanic) => _shakeIntensity = isPanic ? 1f : 0f;

    public void TriggerReactionCut(float holdDuration = 0.8f)
    {
        if (_isReacting)
            return;
        ReactionCutAsync(holdDuration, CtsUtility.Renew(ref _reactionCts)).Forget();
    }

    public void CancelReaction()
    {
        CtsUtility.Cancel(ref _reactionCts);

        _activeOffset = _offset.normalized * _currentDistance;
        _useLookAt = false;
        _isReacting = false;
    }

    private async UniTaskVoid ReactionCutAsync(float holdDuration, CancellationToken ct)
    {
        try
        {
            _isReacting = true;
            _useLookAt = true;
            Vector3 startOffset = _activeOffset;

            float t = 0f;
            while (t < _reactionInDuration)
            {
                t += Time.deltaTime;
                _activeOffset = Vector3.Lerp(
                    startOffset,
                    _reactionOffset,
                    Mathf.Clamp01(t / _reactionInDuration)
                );
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _activeOffset = _reactionOffset;

            await UniTask.Delay(TimeSpan.FromSeconds(holdDuration), DelayType.DeltaTime, cancellationToken: ct);

            t = 0f;
            Vector3 returnTarget = _offset.normalized * _currentDistance;
            while (t < _reactionOutDuration)
            {
                t += Time.deltaTime;
                _activeOffset = Vector3.Lerp(
                    _reactionOffset,
                    returnTarget,
                    Mathf.Clamp01(t / _reactionOutDuration)
                );
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            _useLookAt = false;
            _isReacting = false;
        }
        catch (OperationCanceledException) { }
    }
}