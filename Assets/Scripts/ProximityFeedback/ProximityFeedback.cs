using System;
using System.Collections.Generic;
using UnityEngine;

public class ProximityFeedback : MonoBehaviour
{
    [Header("감지 범위")]
    [SerializeField]
    private float _dangerRadius = 4f;

    [SerializeField]
    private float _panicRadius = 1f;

    public event Action<float> OnIntensityChanged;
    public event Action<bool> OnPanicChanged;

    public float DangerRadius => _dangerRadius;
    public float PanicRadius => _panicRadius;
    public float Intensity { get; private set; }

    // 패닉(panicRadius 안) 판정은 여기서만 한다 — 구독자는 Intensity를 직접 비교하지 않는다
    public bool IsPanic => IsPanicIntensity(Intensity);

    private readonly List<Transform> _dangers = new();

    public void RegisterDanger(Transform t)
    {
        if (t == null || _dangers.Contains(t))
            return;
        _dangers.Add(t);
    }

    public void UnregisterDanger(Transform t) => _dangers.Remove(t);

    private void Update()
    {
        float minDist = CalcMinDist();
        float newIntensity = CalcIntensity(minDist);

        bool wasInDanger = Intensity > 0f;
        bool isInDanger  = newIntensity > 0f;

        // 0.995 → 1.0처럼 변화량이 작아도 위험·패닉 경계를 넘으면 구독자가 경계 값을 받도록 반드시 발행한다
        bool wasPanic = IsPanic;
        bool isPanic  = IsPanicIntensity(newIntensity);

        if (wasInDanger == isInDanger && wasPanic == isPanic && Mathf.Abs(newIntensity - Intensity) < 0.01f)
            return;

        Intensity = newIntensity;
        OnIntensityChanged?.Invoke(Intensity);

        if (wasPanic != isPanic)
            OnPanicChanged?.Invoke(isPanic);
    }

    // panicRadius 안에서는 CalcIntensity가 정확히 1을 반환한다
    private static bool IsPanicIntensity(float intensity) => intensity >= 1f;

    private float CalcMinDist()
    {
        float minDistSq = float.MaxValue;
        Vector3 myPos = transform.position;

        for (int i = _dangers.Count - 1; i >= 0; i--)
        {
            Transform t = _dangers[i];
            if (t == null) // Unregister 없이 파괴된 경우
            {
                _dangers.RemoveAt(i);
                continue;
            }

            float dSq = (myPos - t.position).sqrMagnitude;
            if (dSq < minDistSq)
                minDistSq = dSq;
        }

        return minDistSq == float.MaxValue ? float.MaxValue : Mathf.Sqrt(minDistSq);
    }

    private float CalcIntensity(float minDist)
    {
        if (minDist == float.MaxValue)
            return 0f;

        return 1f - Mathf.Clamp01((minDist - _panicRadius) / (_dangerRadius - _panicRadius));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _panicRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _dangerRadius);
    }
}
