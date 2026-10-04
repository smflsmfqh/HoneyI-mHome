using UnityEngine;

public static class SpatialAudio
{
    private static readonly AnimationCurve k_NoRolloff = AnimationCurve.Constant(0f, 1f, 1f);

    // 방향(패닝)만 3D로 처리하고, 거리 감쇠는 호출 측에서 volume으로 직접 계산한다.
    public static void ApplyDirectionalOnly(AudioSource source)
    {
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, k_NoRolloff);
    }
}
