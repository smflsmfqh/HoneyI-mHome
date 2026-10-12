using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("오디오")]
    [SerializeField]
    private AudioClip _pickupSound;

    [SerializeField]
    private AudioClip _jumpSound;

    [SerializeField]
    private AudioClip _rollSound;

    [SerializeField]
    private ParticleSystem healParticle;

    [SerializeField]
    private ParticleSystem speedBoostParticle;

    [SerializeField]
    private ParticleSystem invincibleParticle;
    public event Action OnMissionTogglePressed;
    private PlayerHealth _ph;
    private PlayerMovement _pm;
    private PlayerInput _pi;

    private readonly Collider[] _interactHits = new Collider[16];

    private Dictionary<EffectParticleType, Coroutine> _particleCorutine =
        new Dictionary<EffectParticleType, Coroutine>();

    private void Awake()
    {
        _ph = GetComponent<PlayerHealth>();
        _pm = GetComponent<PlayerMovement>();
        _pi = GetComponent<PlayerInput>();

        if (healParticle != null)
            healParticle.gameObject.SetActive(false);
        if (speedBoostParticle != null)
            speedBoostParticle.gameObject.SetActive(false);
        if (invincibleParticle != null)
            invincibleParticle.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _ph.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        _ph.OnDied -= HandleDied;
    }

    private void OnInteract(InputValue value)
    {
        if (!value.isPressed)
            return;

        // 픽업 가능한 후보 중 가장 가까운 것 하나만 줍는다
        int count = Physics.OverlapSphereNonAlloc(transform.position, 1f, _interactHits);
        IInteractive closest = null;
        float closestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var hit = _interactHits[i];
            if (!hit.TryGetComponent<IInteractive>(out var item) || !item.CanInteract)
                continue;

            float sqr = (hit.transform.position - transform.position).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closest = item;
            }
        }

        closest?.Interact(this);
    }

    private void OnMissionToggle(InputValue value)
    {
        if (value.isPressed)
            OnMissionTogglePressed?.Invoke();
    }

    private void OnPause(InputValue value)
    {
        if (value.isPressed)
            GameManager.Instance.TogglePause();
    }

    public void Heal(int amount)
    {
        _ph.Heal(amount);
    }

    public void SetInvincible(float sec, string source = "unknown")
    {
        _ph.SetInvincible(sec, source);
    }

    public void SetSpeedBoost(float speed, float sec)
    {
        _pm.SetSpeedBoost(speed, sec);
    }

    private void HandleDied(CauseDeath _)
    {
        _pi.DeactivateInput();
    }

    public void PlayPickupSound() => AudioManager.Instance?.PlaySFX(_pickupSound);

    public void PlayJumpSound() => AudioManager.Instance?.PlaySFX(_jumpSound);

    public void PlayRollSound() => AudioManager.Instance?.PlaySFX(_rollSound);

    public void PlayEffect(EffectParticleType type, float duration = 0f)
    {
        ParticleSystem particle = null;
        switch (type)
        {
            case EffectParticleType.HealParticle:
                particle = healParticle;
                break;
            case EffectParticleType.SpeedBoostParticle:
                particle = speedBoostParticle;
                break;
            case EffectParticleType.InvincibleParticle:
                particle = invincibleParticle;
                break;
        }

        if (particle == null)
            return;

        if (_particleCorutine.TryGetValue(type, out var prev) && prev != null)
        {
            StopCoroutine(prev);
        }

        particle.gameObject.SetActive(true);
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particle.Play();

        _particleCorutine[type] = StartCoroutine(DeactivateParticleAfter(particle, type, duration));
    }

    private IEnumerator DeactivateParticleAfter(
        ParticleSystem particle,
        EffectParticleType type,
        float duration
    )
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);
        else
        {
            yield return null;
            yield return new WaitWhile(() => particle.isPlaying);
        }

        particle.Stop();
        particle.gameObject.SetActive(false);
        _particleCorutine[type] = null;
    }
}
