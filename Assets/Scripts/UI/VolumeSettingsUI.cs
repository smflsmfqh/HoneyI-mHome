using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VolumeSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("언어 선택")]
    [SerializeField] private Button _koreanButton;
    [SerializeField] private Button _englishButton;

    // 패널이 보이는 동안만 켜지는 Esc 닫기 액션. 일시정지 중엔 PlayerInput이 꺼져 있고 타이틀엔 PlayerInput이 없어 독립 액션으로 둔다
    [Header("닫기 입력")]
    [SerializeField]
    private InputAction _closeAction = new InputAction(
        name: "CloseVolumeSettings",
        type: InputActionType.Button,
        binding: "<Keyboard>/escape"
    );

    // 패널을 여닫는 쪽(UIManager·TitleController)이 구독해 패널을 닫는다
    public event Action CloseRequested;

    private VolumeSettingsManager _mgr;

    private void Start()
    {
        _mgr = VolumeSettingsManager.Instance;
        if (_mgr == null) return;

        _bgmSlider.minValue = 0f;
        _bgmSlider.maxValue = 1f;
        _sfxSlider.minValue = 0f;
        _sfxSlider.maxValue = 1f;

        _bgmSlider.value = _mgr.BGMVolume;
        _sfxSlider.value = _mgr.SFXVolume;

        _bgmSlider.onValueChanged.AddListener(_mgr.SetBGMVolume);
        _sfxSlider.onValueChanged.AddListener(_mgr.SetSFXVolume);

        _koreanButton?.onClick.AddListener(() => SelectLanguage(Language.Ko));
        _englishButton?.onClick.AddListener(() => SelectLanguage(Language.En));
    }

    private void SelectLanguage(Language language)
    {
        PlayerPrefs.SetInt(PrefKeys.LANGUAGE, (int)language);
        PlayerPrefs.Save();
        StringTableManager.Instance.SetLanguage(language);
    }

    // 패널이 다시 열릴 때 저장된 값과 슬라이더를 동기화
    private void OnEnable()
    {
        _closeAction.performed += OnClosePerformed;
        _closeAction.Enable();

        if (_mgr == null) return;
        _bgmSlider.SetValueWithoutNotify(_mgr.BGMVolume);
        _sfxSlider.SetValueWithoutNotify(_mgr.SFXVolume);
    }

    private void OnDisable()
    {
        _closeAction.performed -= OnClosePerformed;
        _closeAction.Disable();
    }

    private void OnClosePerformed(InputAction.CallbackContext ctx) => CloseRequested?.Invoke();

    private void OnDestroy()
    {
        if (_mgr == null) return;
        _bgmSlider.onValueChanged.RemoveListener(_mgr.SetBGMVolume);
        _sfxSlider.onValueChanged.RemoveListener(_mgr.SetSFXVolume);
    }
}
