using UnityEngine;

public class ProximityPanelFeedbackUI : MonoBehaviour
{
    [SerializeField]
    private GameObject _panel;

    [SerializeField]
    private float _blinkInterval = 0.07f;

    [SerializeField]
    private ProximityFeedback _proximity;

    private bool _isInDanger;
    private float _blinkTimer;

    private void Awake()
    {
        if (_panel != null)
            _panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_proximity != null)
        {
            _proximity.OnIntensityChanged += HandleIntensity;
            // 이벤트는 값이 바뀔 때만 오므로, 이미 위험 범위 안에서 켜져도 패널이 뜨도록 현재 값으로 맞춘다
            HandleIntensity(_proximity.Intensity);
        }
    }

    private void OnDisable()
    {
        if (_proximity != null)
            _proximity.OnIntensityChanged -= HandleIntensity;
    }

    private void Update()
    {
        if (!_isInDanger)
            return;

        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= _blinkInterval)
        {
            _blinkTimer = 0f;
            _panel.SetActive(!_panel.activeSelf);
        }
    }

    private void HandleIntensity(float intensity)
    {
        // OnEnable에서 바로 호출되므로 미연결이면 씬 로드 직후 예외가 나지 않게 막는다
        if (_panel == null)
            return;

        _isInDanger = intensity > 0f;
        if (!_isInDanger)
        {
            _blinkTimer = 0f;
            _panel.SetActive(false);
        }
    }

    public void ForceHide()
    {
        _isInDanger = false;
        _blinkTimer = 0f;
        _panel.SetActive(false);
        enabled = false;
    }
}
