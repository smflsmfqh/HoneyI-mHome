using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class TitleController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject _titlePanel;
    [SerializeField] private GameObject _videoPanel;

    [Header("Video Settings")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private string _nextSceneName = "AntDemoScene";

    [Header("Buttons")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Button _languageButton;

    [Header("언어 선택")]
    [SerializeField] private GameObject _languagePanel;
    [SerializeField] private Button _koreanButton;
    [SerializeField] private Button _englishButton;
    [SerializeField] private Button _backButton;

    [Header("언어별 이미지")]
    [SerializeField] private Image _titleImage;
    [SerializeField] private Image _languagePanelImage;
    [SerializeField] private Sprite[] _titleSprites;          // [0]=En [1]=Ko
    [SerializeField] private Sprite[] _languagePanelSprites;  // [0]=En [1]=Ko

    [Header("오디오")]
    [SerializeField] private AudioClip _buttonClickSound;
    [SerializeField] private AudioClip _titleBGM;

    [Header("볼륨 설정")]
    [SerializeField] private GameObject _volumeSettingsPanel;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _closeVolumeButton;

    [Header("게임 종료")]
    [SerializeField] private Button _quitButton;

    private bool _sceneLoading = false;

    private void Awake()
    {
        if (_startButton != null)
            _startButton.onClick.AddListener(StartGameSequence);

        if (_skipButton != null)
            _skipButton.onClick.AddListener(SkipVideo);

        if (_languageButton != null)
            _languageButton.onClick.AddListener(ToggleLanguagePanel);

        if (_koreanButton != null)
            _koreanButton.onClick.AddListener(() => SelectLanguage(Language.Ko));

        if (_englishButton != null)
            _englishButton.onClick.AddListener(() => SelectLanguage(Language.En));

        if (_backButton != null)
            _backButton.onClick.AddListener(CloseLanguagePanel);

        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(() => { PlayButtonSound(); ShowVolumeSettings(); });

        if (_quitButton != null)
            _quitButton.onClick.AddListener(() => { PlayButtonSound(); Application.Quit(); });

        if (_closeVolumeButton != null)
            _closeVolumeButton.onClick.AddListener(() => { PlayButtonSound(); HideVolumeSettings(); });

        if (_titlePanel != null) _titlePanel.SetActive(true);
        if (_videoPanel != null) _videoPanel.SetActive(false);
        if (_languagePanel != null) _languagePanel.SetActive(false);
        if (_volumeSettingsPanel != null) _volumeSettingsPanel.SetActive(false);

        // 저장된 언어로 초기 이미지 설정
        Language savedLang = (Language)PlayerPrefs.GetInt(PrefKeys.LANGUAGE, (int)Language.En);
        StringTableManager.Instance.SetLanguage(savedLang);
        UpdateImages(savedLang);

    }

    private void Start()
    {
        AudioManager.Instance?.PlayBGM(_titleBGM);
    }

    private void Update()
    {
        if (_volumeSettingsPanel != null && _volumeSettingsPanel.activeSelf &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
            HideVolumeSettings();
    }

    private void ShowVolumeSettings()
    {
        if (_volumeSettingsPanel != null) _volumeSettingsPanel.SetActive(true);
    }

    private void HideVolumeSettings()
    {
        if (_volumeSettingsPanel != null) _volumeSettingsPanel.SetActive(false);
    }

    private void ToggleLanguagePanel()
    {
        PlayButtonSound();
        if (_languagePanel != null)
            _languagePanel.SetActive(!_languagePanel.activeSelf);
    }

    private void CloseLanguagePanel()
    {
        PlayButtonSound();
        if (_languagePanel != null)
            _languagePanel.SetActive(false);
    }

    private void SelectLanguage(Language language)
    {
        PlayButtonSound();
        PlayerPrefs.SetInt(PrefKeys.LANGUAGE, (int)language);
        PlayerPrefs.Save();
        StringTableManager.Instance.SetLanguage(language);
        UpdateImages(language);
        if (_languagePanel != null)
            _languagePanel.SetActive(false);
    }

    private void UpdateImages(Language language)
    {
        int idx = (int)language;
        if (_titleImage != null && _titleSprites != null && idx < _titleSprites.Length && _titleSprites[idx] != null)
            _titleImage.sprite = _titleSprites[idx];
        if (_languagePanelImage != null && _languagePanelSprites != null && idx < _languagePanelSprites.Length && _languagePanelSprites[idx] != null)
            _languagePanelImage.sprite = _languagePanelSprites[idx];
    }

    private void PlayButtonSound()
    {
        AudioManager.Instance?.PlaySFX(_buttonClickSound);
    }

    private void StartGameSequence()
    {
        PlayButtonSound();
        if (_videoPlayer != null && _videoPlayer.clip != null)
        {
            if (_titlePanel != null) _titlePanel.SetActive(false);
            if (_videoPanel != null) _videoPanel.SetActive(true);

            _videoPlayer.skipOnDrop = false;
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.Prepare();
        }
        else
        {
            SkipVideo();
        }
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnVideoPrepared;
        float duration = (float)vp.clip.length;
        vp.Play();
        StartCoroutine(WaitForVideoEnd(duration));
    }

    private IEnumerator WaitForVideoEnd(float clipDuration)
    {
        // 타임스탬프 버그로 clip.length가 0 반환할 경우 최소 5초 보장
        float waitTime = Mathf.Max(clipDuration, 5f);
        yield return new WaitForSeconds(waitTime);
        LoadGameScene();
    }

    private void SkipVideo()
    {
        PlayButtonSound();
        StopAllCoroutines();
        if (_videoPlayer != null)
            _videoPlayer.Stop();

        LoadGameScene();
    }

    private void LoadGameScene()
    {
        if (_sceneLoading) return;
        _sceneLoading = true;

        if (!string.IsNullOrEmpty(_nextSceneName))
            SceneManager.LoadScene(_nextSceneName);
    }
}
