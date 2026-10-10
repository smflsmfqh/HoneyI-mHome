using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private int _score;
    public int Score => _score;

    private bool _isPaused;
    private float _playTime;

    // 게임오버·클리어 이후 true — 결과 화면에서 추가 피해·중복 게임오버를 막는 데 사용
    public bool IsGameEnded { get; private set; }
    public float PlayTime => _playTime;

    [SerializeField]
    private PlayerHealth _playerHealth;

    private UnityEngine.InputSystem.PlayerInput _playerInput;

    [SerializeField]
    private AngerSystem _angerSystem;

    [SerializeField]
    private MissionMessageUI _missionMessageUI;

    [SerializeField]
    private Language _language = Language.En;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _playerInput = _playerHealth.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        // AudioListener.pause는 전역 상태라 씬을 넘어 남을 수 있으므로 시작 시 초기화
        SetGameTimeStopped(false);
        Application.targetFrameRate = 60;
        _score = 0;

        if (PlayerPrefs.HasKey(TitleController.LanguagePrefKey))
            _language = (Language)PlayerPrefs.GetInt(TitleController.LanguagePrefKey);
        StringTableManager.Instance.SetLanguage(_language);
    }

    private void OnEnable()
    {
        _playerHealth.OnDied += GameOver;
    }

    private void OnDisable()
    {
        _playerHealth.OnDied -= GameOver;
    }

    private void Update()
    {
        if (!_isPaused)
            _playTime += Time.deltaTime;

#if UNITY_EDITOR
        if (UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
            MissionManager.Instance.DebugCompleteAll();
        if (UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame)
            MissionManager.Instance.DebugUnlockOptional();
#endif
    }

    public void AddScore(int amount)
    {
        _score += amount;
    }

    public void GameOver(CauseDeath cause)
    {
        IsGameEnded = true;
        // 원인과 관계없이 게임 시간을 정지 (결과 화면 UI는 unscaled로 동작)
        SetGameTimeStopped(true);

        _angerSystem.Pause();
        MissionManager.Instance.PauseMissionAssignment();
        _missionMessageUI?.ClearQueue();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // 체력 사망은 PlayerController.HandleDied에서도 끄지만, 분노·미션 실패 게임오버는 여기서만 끔
        if (_playerInput != null)
            _playerInput.DeactivateInput();

        foreach (var cat in FindObjectsByType<CatMovement>(FindObjectsSortMode.None))
            cat.SetExternalPause(true);

        UIManager.Instance.ShowGameOver(cause, Mathf.RoundToInt(_playTime));
    }

    public void GameClear()
    {
        IsGameEnded = true;
        SetGameTimeStopped(true);
        MissionManager.Instance.PauseMissionAssignment();
        _missionMessageUI?.ClearQueue();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (_playerInput != null)
            _playerInput.DeactivateInput();
        UIManager.Instance.ShowGameClear(Mathf.RoundToInt(_playTime));
    }

    public void GoToTitle()
    {
        SetGameTimeStopped(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("TitleScene");
    }

    public void Restart()
    {
        SetGameTimeStopped(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerPrefs.SetInt("SkipTutorial", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 게임 시간과 월드 소리를 함께 정지·재개 (BGM·UI 효과음은 AudioManager에서 ignoreListenerPause)
    private void SetGameTimeStopped(bool stopped)
    {
        Time.timeScale = stopped ? 0f : 1f;
        AudioListener.pause = stopped;
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        SetGameTimeStopped(_isPaused);
        Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isPaused;

        if (_isPaused)
        {
            if (_playerInput != null)
                _playerInput.DeactivateInput();
            UIManager.Instance.ShowPause();
        }
        else
        {
            if (_playerInput != null)
                _playerInput.ActivateInput();
            UIManager.Instance.HidePause();
        }
    }

    public void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
