using UnityEngine;
using UnityEngine.UI;

public class RollUI : MonoBehaviour
{
    [SerializeField]
    private Image _fillImage;

    [SerializeField]
    private Image _backgroundImage;

    [SerializeField]
    private PlayerMovement _playerMovement;

    [SerializeField]
    private float _inactiveAlpha = 0.3f;
    private const float ACTIVE_ALPHA = 1f;
    private bool _isRolling;

    private void OnEnable()
    {
        _playerMovement.OnRollChanged += UpdateCircleBar;
        _playerMovement.OnRollActive += UpdateAlpha;
        // 이벤트는 값이 바뀔 때만 오므로, 꺼져 있던 동안의 값을 켜질 때 한 번 맞춘다
        UpdateCircleBar(_playerMovement.RollRatio);
    }

    private void OnDisable()
    {
        _playerMovement.OnRollChanged -= UpdateCircleBar;
        _playerMovement.OnRollActive -= UpdateAlpha;
    }

    private void UpdateCircleBar(float ratio)
    {
        _fillImage.fillAmount = ratio;
        ApplyAlpha(!_isRolling && ratio >= 1f);
    }

    private void UpdateAlpha(bool isActive)
    {
        _isRolling = isActive;
        ApplyAlpha(!isActive && _fillImage.fillAmount >= 1f);
    }

    private void ApplyAlpha(bool isReady)
    {
        Color fillColor = _fillImage.color;
        Color backColor = _backgroundImage.color;

        fillColor.a = isReady ? ACTIVE_ALPHA : _inactiveAlpha;
        backColor.a = isReady ? ACTIVE_ALPHA : _inactiveAlpha;

        _fillImage.color = fillColor;
        _backgroundImage.color = backColor;
    }
}
