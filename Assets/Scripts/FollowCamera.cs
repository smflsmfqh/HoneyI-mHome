using System.Collections;
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
        "New Input System의 Mouse.scroll은 노치당 값이 레거시 Input.GetAxis(\"Mouse ScrollWheel\")보다 " +
        "훨씬 큰 단위(Windows 기준 노치당 약 ±120)로 들어온다. 이전 _scrollSensitivity 값이 그대로 쓰이도록 " +
        "같은 스케일(노치당 대략 ±0.1)로 나눠주는 계수 — 실제 플레이 체감이 이전과 다르면 여기서 조정."
    )]
    [SerializeField]
    private float _scrollUnitScale = 1200f;

    public void SetShakeIntensity(float intensity) => _shakeIntensity = intensity;

    private float ReadScrollDelta()
    {
        if (Mouse.current == null || _scrollUnitScale == 0f)
            return 0f;

        return Mouse.current.scroll.ReadValue().y / _scrollUnitScale;
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

    private void Start()
    {
    }

    private void LateUpdate()
    {
        if (_player == null)
            return;

        

        // 일시정지·결과 화면(timeScale 0)에서는 휠 줌 무시 — 휠은 PlayerInput을 거치지 않아 입력 비활성화로 막히지 않음
        if (!_isReacting && Time.timeScale > 0f)
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

        Vector3 shakeOffset = Random.insideUnitCircle * (_maxShakeAmount * _shakeIntensity);
        transform.position += shakeOffset;
    }

    public void TriggerReactionCut(float holdDuration = 0.8f)
    {
        if (_isReacting)
            return;
        StartCoroutine(ReactionCutCoroutine(holdDuration));
    }

    public void CancelReaction()
    {
        StopAllCoroutines();

        _activeOffset = _offset.normalized * _currentDistance;
        _useLookAt = false;
        _isReacting = false;
    }

    private IEnumerator ReactionCutCoroutine(float holdDuration)
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
            yield return null;
        }

        _activeOffset = _reactionOffset;

        yield return new WaitForSeconds(holdDuration);

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
            yield return null;
        }

        _useLookAt = false;
        _isReacting = false;
    }
}