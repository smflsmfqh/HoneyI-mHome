using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private int _score;
    public int Score => _score;

    private bool _isPaused;
    private float _playTime;
    public float PlayTime => _playTime;

    [SerializeField]
    private PlayerHealth _playerHealth;

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
        if (cause == CauseDeath.Anger)
            Time.timeScale = 0f;

        _angerSystem.Pause();
        MissionManager.Instance.PauseMissionAssignment();
        _missionMessageUI?.ClearQueue();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // 체력 사망은 PlayerController.HandleDied에서도 끄지만, 분노·미션 실패 게임오버는 여기서만 끔
        _playerHealth.GetComponent<UnityEngine.InputSystem.PlayerInput>()?.DeactivateInput();

        foreach (var cat in FindObjectsByType<CatMovement>(FindObjectsSortMode.None))
            cat.SetExternalPause(true);

        UIManager.Instance.ShowGameOver(cause, Mathf.RoundToInt(_playTime));
    }

    public void GameClear()
    {
        Time.timeScale = 0f;
        MissionManager.Instance.PauseMissionAssignment();
        _missionMessageUI?.ClearQueue();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _playerHealth.GetComponent<UnityEngine.InputSystem.PlayerInput>()?.DeactivateInput();
        UIManager.Instance.ShowGameClear(Mathf.RoundToInt(_playTime));
    }

    public void GoToTitle()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("TitleScene");
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        PlayerPrefs.SetInt("SkipTutorial", 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;
        Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _isPaused;

        var playerInput = _playerHealth.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (_isPaused)
        {
            playerInput?.DeactivateInput();
            UIManager.Instance.ShowPause();
        }
        else
        {
            playerInput?.ActivateInput();
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
