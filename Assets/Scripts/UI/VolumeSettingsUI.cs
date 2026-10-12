using UnityEngine;
using UnityEngine.UI;

public class VolumeSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("언어 선택")]
    [SerializeField] private Button _koreanButton;
    [SerializeField] private Button _englishButton;

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
        if (_mgr == null) return;
        _bgmSlider.SetValueWithoutNotify(_mgr.BGMVolume);
        _sfxSlider.SetValueWithoutNotify(_mgr.SFXVolume);
    }

    private void OnDestroy()
    {
        if (_mgr == null) return;
        _bgmSlider.onValueChanged.RemoveListener(_mgr.SetBGMVolume);
        _sfxSlider.onValueChanged.RemoveListener(_mgr.SetSFXVolume);
    }
}
