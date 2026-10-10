using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField]
    private float k_maxHealth = 100f;
    private float _currentHealth;

    [SerializeField]
    private float _healthInterval = 2f;
    private float _healthTimer;

    [SerializeField]
    private float _addedHealth = 1f;

    [SerializeField]
    private FollowCamera _followCamera;

    [SerializeField]
    private PlayerMovement _playerMovement;

    [SerializeField]
    private AudioClip _damageSound;

    private const float k_damageInvincibleTime = 0.7f;
    public bool isDead = false;
    private bool _isInvincible = false;
    public bool IsInvincible => _isInvincible;
    private bool _tutorialInvincible = false;

    public void SetTutorialInvincible(bool invincible) => _tutorialInvincible = invincible;

    private Coroutine _invincibleCo;
    private Animator _animator;
    private CauseDeath _pendingCause;

    // --- 이벤트 관련 필드 ---
    public event Action<float> OnHealthChanged;
    public event Action<CauseDeath> OnDied;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        Reset();
    }

    private void Reset()
    {
        isDead = false;
        _healthTimer = 0f;
        _currentHealth = k_maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(_currentHealth / k_maxHealth);
    }

    private void Update()
    {
        // 게임오버·클리어 뒤에는 자동 회복 중지
        if (GameManager.Instance != null && GameManager.Instance.IsGameEnded)
            return;

        _healthTimer += Time.deltaTime;

        if (_healthTimer >= _healthInterval)
        {
            _healthTimer = 0f;
            _currentHealth += _addedHealth;
            if (_currentHealth >= k_maxHealth)
            {
                _currentHealth = k_maxHealth;
            }
            OnHealthChanged?.Invoke(_currentHealth / k_maxHealth);
        }
    }

    public void TakeDamage(float damage, CauseDeath causeDeath)
    {
        if (isDead || _isInvincible || _tutorialInvincible)
            return;
        // 게임오버·클리어 뒤에는 사람·차·고양이 피해를 일괄 차단
        if (GameManager.Instance != null && GameManager.Instance.IsGameEnded)
            return;

        _currentHealth -= damage;
        AudioManager.Instance?.PlaySFX(_damageSound);
        SetInvincible(k_damageInvincibleTime, "damage");

        if (_currentHealth <= 0)
        {
            _currentHealth = 0f;
        }
        OnHealthChanged?.Invoke(_currentHealth / k_maxHealth);

        if (_currentHealth <= 0f)
        {
            Die(causeDeath);
        }
        else
        {
            _followCamera?.TriggerReactionCut(0.6f);
            switch (causeDeath)
            {
                case CauseDeath.Car:
                    _playerMovement?.SetFaceShrink();
                    break;
                case CauseDeath.Cat:
                    _playerMovement?.SetFaceTrauma();
                    break;
                default:
                    _playerMovement?.SetFaceDamaged();
                    break;
            }
        }
    }

    public void Heal(int amount)
    {
        var prevH = _currentHealth;
        _currentHealth += amount;
        if (_currentHealth >= k_maxHealth)
        {
            _currentHealth = k_maxHealth;
        }
        OnHealthChanged?.Invoke(_currentHealth / k_maxHealth);
    }

    public void SetInvincible(float sec, string source = "unknown")
    {
        if (_invincibleCo != null)
            StopCoroutine(_invincibleCo);

        _invincibleCo = StartCoroutine(InvincibleCoroutine(sec, source));
    }

    private IEnumerator InvincibleCoroutine(float sec, string source)
    {
        _isInvincible = true;
        yield return new WaitForSeconds(sec);
        _isInvincible = false;
    }

    private void Die(CauseDeath cause)
    {
        if (isDead)
            return;
        _pendingCause = cause;
        isDead = true;
        _animator?.SetBool("IsDead", isDead);

        _followCamera?.TriggerReactionCut(2f);
        _playerMovement?.SetFaceDead();
    }

    public void OnDieAnimationEnd()
    {
        OnDied?.Invoke(_pendingCause);
    }
}
