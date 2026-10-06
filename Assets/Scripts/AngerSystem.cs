using System;
using UnityEngine;

public class AngerSystem : MonoBehaviour
{
    [SerializeField]
    private float _angerPerSecond = 1.5f;

    [SerializeField]
    private float _missionReduction = 20f;

    [SerializeField]
    private float _maxAnger = 200f;

    private readonly string[] _introKeys = { "ANGER_0", "ANGER_1", "ANGER_2" };

    private float[] _thresholds = { 10f, 25f, 50f, 75f, 90f };

    private float _currentAnger;
    private bool[] _triggered;
    private bool _gameOverTriggered;
    private bool _paused;

    public void Pause()  => _paused = true;
    public void Resume() => _paused = false;
    private CauseDeath cause = CauseDeath.Anger;

    public float Anger => _currentAnger;
    public float AngerPerSecond => _angerPerSecond;
    public bool IntroQueued { get; private set; }
    public event Action<float> OnAngerChanged;
    public event Action<string> OnMessasgeTriggered;

    private void Start()
    {
        _triggered = new bool[_thresholds.Length];
        MissionManager.Instance.OnMissionCompleted += HandleMissionCompleted;

        foreach (var key in _introKeys)
        {
            var (msg, _) = StringTableManager.Instance.GetMessage(key);
            if (!string.IsNullOrEmpty(msg))
                OnMessasgeTriggered?.Invoke(key);
        }

        IntroQueued = true;
    }

    private void OnDisable()
    {
        if (MissionManager.Instance != null)
        {
            MissionManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
        }
    }

    private void Update()
    {
        if (_gameOverTriggered || _paused)
            return;

        _currentAnger = Mathf.Min(_currentAnger + _angerPerSecond * Time.deltaTime, _maxAnger);
        float percent = _currentAnger / _maxAnger * 100f;

        for (int i = 0; i < _thresholds.Length; i++)
        {
            if (!_triggered[i] && percent >= _thresholds[i])
            {
                _triggered[i] = true;
                OnMessasgeTriggered?.Invoke($"ANGER_{(int)_thresholds[i]}");
            }
        }

        OnAngerChanged?.Invoke(_currentAnger / _maxAnger);

        if (_currentAnger >= _maxAnger)
        {
            _gameOverTriggered = true;
            GameManager.Instance.GameOver(cause);
        }
    }

    private void HandleMissionCompleted(ItemData _)
    {
        _currentAnger = Mathf.Max(0f, _currentAnger - _missionReduction);
    }
}
