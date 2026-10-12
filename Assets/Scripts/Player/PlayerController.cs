using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

    // 바닥·건물·트리거 구역까지 함께 잡히므로 아이템이 잘려 나가지 않도록 넉넉하게 잡는다
    private readonly Collider[] _interactHits = new Collider[64];

    private readonly Dictionary<EffectParticleType, CancellationTokenSource> _particleCts = new();

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

        foreach (var cts in _particleCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _particleCts.Clear();
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

        if (_particleCts.TryGetValue(type, out var prev))
        {
            prev.Cancel();
            prev.Dispose();
        }

        particle.gameObject.SetActive(true);
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particle.Play();

        var cts = new CancellationTokenSource();
        _particleCts[type] = cts;
        DeactivateParticleAfterAsync(particle, duration, cts.Token).Forget();
    }

    private async UniTaskVoid DeactivateParticleAfterAsync(
        ParticleSystem particle,
        float duration,
        CancellationToken ct
    )
    {
        try
        {
            if (duration > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(duration), DelayType.DeltaTime, cancellationToken: ct);
            else
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitWhile(() => particle.isPlaying, cancellationToken: ct);
            }

            particle.Stop();
            particle.gameObject.SetActive(false);
        }
        catch (OperationCanceledException) { }
    }
}
