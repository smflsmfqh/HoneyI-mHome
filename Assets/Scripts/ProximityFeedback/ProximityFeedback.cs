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

    [Header("카메라 흔들림")]
    [SerializeField]
    private FollowCamera _followCam;
    public event Action<float> OnIntensityChanged;

    public float DangerRadius => _dangerRadius;
    public float PanicRadius => _panicRadius;
    public float Intensity { get; private set; }

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

        _followCam?.SetShakeIntensity(minDist <= _panicRadius ? 1f : 0f);

        bool wasInDanger = Intensity > 0f;
        bool isInDanger  = newIntensity > 0f;

        if (wasInDanger == isInDanger && Mathf.Abs(newIntensity - Intensity) < 0.01f)
            return;

        Intensity = newIntensity;
        OnIntensityChanged?.Invoke(Intensity);
    }

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
