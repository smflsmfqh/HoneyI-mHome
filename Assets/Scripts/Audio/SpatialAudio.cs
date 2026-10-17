using UnityEngine;

public static class SpatialAudio
{
    private static readonly AnimationCurve _noRolloff = AnimationCurve.Constant(0f, 1f, 1f);

    // 방향(패닝)만 3D로 처리하고, 거리 감쇠는 호출 측에서 volume으로 직접 계산한다.
    public static void ApplyDirectionalOnly(AudioSource source)
    {
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _noRolloff);
    }

    // 근접 음량: panicRadius 안쪽은 1, hearRadius 바깥은 0, 그 사이는 선형 감쇠.
    public static float ProximityVolume(float distance, float panicRadius, float hearRadius)
    {
        float range = hearRadius - panicRadius;
        if (range <= 0f)
            return distance <= hearRadius ? 1f : 0f;

        return 1f - Mathf.Clamp01((distance - panicRadius) / range);
    }
}
