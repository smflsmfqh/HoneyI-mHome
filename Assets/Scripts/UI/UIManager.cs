using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField]
    private GameObject _hudPanel;

    [SerializeField]
    private GameObject _missionCheckListUI;

    // --- GameOver UI용 ---

    [SerializeField]
    private GameObject _gameOverPanel;

    [SerializeField]
    private TextMeshProUGUI _gameOverDescText;

    [SerializeField]
    private TextMeshProUGUI _gameOverScoreText;

    [SerializeField]
    private Button _restartButton1;

    [SerializeField]
    private Button _exitButton1;

    [SerializeField]
    private Button _backButton1;

    // --- GameClear UI용 ---
    [SerializeField]
    private GameObject _gameClearPanel;

    [SerializeField]
    private TextMeshProUGUI __scoreText;

    [SerializeField]
    private TextMeshProUGUI _clearTaglineText;

    [SerializeField]
    private Button _restartButton2;

    [SerializeField]
    private Button _exitButton2;

    [SerializeField]
    private Button _backButton2;

    // --- Pause UI용 ---
    [SerializeField]
    private GameObject _pausePanel;

    [SerializeField]
    private Button _resumeButton;

    [SerializeField]
    private Button _settingsButton;

    [SerializeField]
    private Button _exitButton3;

    [SerializeField]
    private Button _backButton3;

    [SerializeField]
    private TextMeshProUGUI _pauseExitText;

    // --- 볼륨 설정 패널 ---
    [SerializeField]
    private GameObject _volumeSettingsPanel;

    [SerializeField]
    private Button _closeVolumeButton;

    [SerializeField]
    private ProximityPanelFeedbackUI _proximityPanelFeedbackUI;

    [Header("히든 엔딩 영상")]
    [SerializeField]
    private GameObject _hiddenVideoPanel;

    [SerializeField]
    private VideoPlayer _hiddenVideoPlayer;

    [SerializeField]
    private Button _hiddenSkipButton;

    [Header("히든 엔딩 암전 연출")]
    [SerializeField]
    private GameObject _blackFadePanel;

    [SerializeField]
    private CanvasGroup _blackFadeCanvasGroup;

    [Header("SFX")]
    [SerializeField]
    private AudioClip _buttonClickSound;

    [Header("BGM")]
    [SerializeField]
    private AudioClip _gameplayBGM;

    [SerializeField]
    private AudioClip _gameOverBGM;

    [SerializeField]
    private AudioClip _clearBGM;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _restartButton1.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.Restart();
        });
        _restartButton2.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.Restart();
        });
        _exitButton1.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.Exit();
        });
        _exitButton2.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.Exit();
        });
        _backButton1?.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.GoToTitle();
        });
        _backButton2?.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.GoToTitle();
        });
        _resumeButton.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.TogglePause();
        });
        _settingsButton?.onClick.AddListener(() =>
        {
            PlayButtonSound();
            ShowVolumeSettings();
        });
        _exitButton3.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.Exit();
        });
        _backButton3?.onClick.AddListener(() =>
        {
            PlayButtonSound();
            GameManager.Instance.GoToTitle();
        });
        _closeVolumeButton?.onClick.AddListener(() =>
        {
            PlayButtonSound();
            HideVolumeSettings();
        });

        SetPanel(_gameOverPanel, false);
        SetPanel(_gameClearPanel, false);
        SetPanel(_pausePanel, false);
        SetPanel(_volumeSettingsPanel, false);
        SetPanel(_hiddenVideoPanel, false);
        SetPanel(_blackFadePanel, false);

        if (_hiddenSkipButton != null)
            _hiddenSkipButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                SkipHiddenVideo();
            });

        MissionManager.Instance.OnMissionDisplayed += EnableMissionCheckListUI;
        StringTableManager.Instance.OnLanguageChanged += RefreshLocalizedTexts;
    }

    private void PlayButtonSound()
    {
        AudioManager.Instance?.PlaySFX(_buttonClickSound);
    }

    private CancellationTokenSource _hiddenVideoCts;

    private void OnDisable()
    {
        CancelHiddenVideo();
    }

    private void CancelHiddenVideo()
    {
        _hiddenVideoCts?.Cancel();
        _hiddenVideoCts?.Dispose();
        _hiddenVideoCts = null;
    }

    private void OnDestroy()
    {
        if (MissionManager.Instance != null)
            MissionManager.Instance.OnMissionDisplayed -= EnableMissionCheckListUI;
        StringTableManager.Instance.OnLanguageChanged -= RefreshLocalizedTexts;
    }

    private void RefreshLocalizedTexts()
    {
        if (_pausePanel != null && _pausePanel.activeSelf && _pauseExitText != null)
            _pauseExitText.text = StringTableManager.Instance.GetMessage("PAUSE_EXIT").message;
    }

    private void EnableMissionCheckListUI(ItemData _)
    {
        SetPanel(_missionCheckListUI, true);
        MissionManager.Instance.OnMissionDisplayed -= EnableMissionCheckListUI;
    }

    private void Start()
    {
        ShowHUD();
        AudioManager.Instance?.PlayBGM(_gameplayBGM);
    }

    public void ShowGameOver(CauseDeath cause, int playSeconds)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioManager.Instance?.PlayBGM(_gameOverBGM);
        SetPanel(_hudPanel, false);
        SetPanel(_gameOverPanel, true);
        if (_proximityPanelFeedbackUI != null)
            _proximityPanelFeedbackUI.ForceHide();

        string deathMsg = StringTableManager.Instance.GetDeathMessage(cause);
        _gameOverDescText.text = deathMsg;

        if (_gameOverScoreText != null)
        {
            int min = playSeconds / 60;
            int sec = playSeconds % 60;
            string key = min > 0 ? "GAMEOVER_SCORE_MINSEC" : "GAMEOVER_SCORE_SEC";
            string fmt = StringTableManager.Instance.GetMessage(key).message;
            _gameOverScoreText.text = min > 0 ? string.Format(fmt, min, sec) : string.Format(fmt, sec);
        }
    }

    public void ShowGameClear(int angerSeconds)
    {
        Time.timeScale = 0f;
        if (_proximityPanelFeedbackUI != null)
            _proximityPanelFeedbackUI.ForceHide();

        if (
            MissionManager.Instance.IsOptionalCompleted
            && _hiddenVideoPanel != null
            && _hiddenVideoPlayer != null
            && _hiddenVideoPlayer.clip != null
        )
        {
            CancelHiddenVideo();
            _hiddenVideoCts = new CancellationTokenSource();
            PlayHiddenVideoThenClearAsync(angerSeconds, _hiddenVideoCts.Token).Forget();
        }
        else
        {
            ShowClearPanel(angerSeconds);
        }
    }

    private async UniTaskVoid PlayHiddenVideoThenClearAsync(int angerSeconds, CancellationToken ct)
    {
        try
        {
            // 1. 화면 서서히 암전 (1.2초)
            if (_blackFadePanel != null && _blackFadeCanvasGroup != null)
            {
                SetPanel(_blackFadePanel, true);
                _blackFadeCanvasGroup.alpha = 0f;
                float elapsed = 0f;
                const float FADE_DURATION = 1.2f;
                while (elapsed < FADE_DURATION)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _blackFadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / FADE_DURATION);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                _blackFadeCanvasGroup.alpha = 1f;
            }

            // 2. 비디오 패널 준비 (암전 상태에서 백그라운드 Prepare)
            SetPanel(_hudPanel, false);
            SetPanel(_hiddenVideoPanel, true);
            AudioManager.Instance?.PlayBGM(_clearBGM);

            _hiddenVideoPlayer.Prepare();

            // Prepare 타임아웃 5초
            float prepElapsed = 0f;
            while (!_hiddenVideoPlayer.isPrepared && prepElapsed < 5f)
            {
                prepElapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // 3. 암전 페이드 아웃 (0.5초) 하면서 영상 공개
            if (_blackFadePanel != null && _blackFadeCanvasGroup != null)
            {
                float elapsed = 0f;
                const float FADE_OUT_DURATION = 0.5f;
                while (elapsed < FADE_OUT_DURATION)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _blackFadeCanvasGroup.alpha = Mathf.Clamp01(1f - elapsed / FADE_OUT_DURATION);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                _blackFadeCanvasGroup.alpha = 0f;
                SetPanel(_blackFadePanel, false);
            }

            if (_hiddenVideoPlayer.isPrepared)
            {
                float duration = (float)_hiddenVideoPlayer.clip.length;
                _hiddenVideoPlayer.Play();
                await UniTask.Delay(TimeSpan.FromSeconds(duration), DelayType.Realtime, cancellationToken: ct);
            }

            _hiddenVideoPlayer.Stop();
            SetPanel(_hiddenVideoPanel, false);
            ShowClearPanel(angerSeconds);
        }
        catch (OperationCanceledException) { }
    }

    private void SkipHiddenVideo()
    {
        CancelHiddenVideo();
        _hiddenVideoPlayer.Stop();
        SetPanel(_hiddenVideoPanel, false);
        SetPanel(_blackFadePanel, false);

        // angerSeconds를 보존하지 못하므로 GameManager에서 직접 가져옴
        ShowClearPanel(Mathf.RoundToInt(GameManager.Instance.PlayTime));
    }

    private void ShowClearPanel(int angerSeconds)
    {
        SetPanel(_hudPanel, false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioManager.Instance?.PlayBGM(_clearBGM);
        SetPanel(_gameClearPanel, true);
        int min = angerSeconds / 60;
        int sec = angerSeconds % 60;
        string key = min > 0 ? "GAMECLEAR_SCORE_MINSEC" : "GAMECLEAR_SCORE_SEC";
        string fmt = StringTableManager.Instance.GetMessage(key).message;
        __scoreText.text = min > 0 ? string.Format(fmt, min, sec) : string.Format(fmt, sec);

        if (_clearTaglineText != null)
            _clearTaglineText.text = StringTableManager
                .Instance.GetMessage("GAMECLEAR_TAGLINE")
                .message;

    }

    private void Update()
    {
        if (IsVolumeSettingsOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
            HideVolumeSettings();
    }

    public void ShowHUD()
    {
        SetPanel(_hudPanel, true);
    }

    public void ShowPause()
    {
        if (_pauseExitText != null)
            _pauseExitText.text = StringTableManager.Instance.GetMessage("PAUSE_EXIT").message;
        SetPanel(_pausePanel, true);
    }

    public void HidePause()
    {
        HideVolumeSettings();
        SetPanel(_pausePanel, false);
    }

    public bool IsVolumeSettingsOpen =>
        _volumeSettingsPanel != null && _volumeSettingsPanel.activeSelf;

    public void ShowVolumeSettings() => SetPanel(_volumeSettingsPanel, true);

    public void HideVolumeSettings() => SetPanel(_volumeSettingsPanel, false);

    public void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }
}
