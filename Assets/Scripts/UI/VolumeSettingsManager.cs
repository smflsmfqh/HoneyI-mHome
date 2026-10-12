using UnityEngine;
using UnityEngine.Audio;

public class VolumeSettingsManager : MonoBehaviour
{
    public static VolumeSettingsManager Instance { get; private set; }

    [SerializeField] private AudioMixer _mixer;

    public float BGMVolume { get; private set; }
    public float SFXVolume { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BGMVolume = PlayerPrefs.GetFloat(PrefKeys.BGMVolume, 1f);
        SFXVolume = PlayerPrefs.GetFloat(PrefKeys.SFXVolume, 1f);
    }

    private void Start()
    {
        _mixer?.SetFloat(MixerParams.BGM, LinearToDb(BGMVolume));
        _mixer?.SetFloat(MixerParams.SFX, LinearToDb(SFXVolume));
    }

    private static float LinearToDb(float linear) =>
        Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f;

    public void SetBGMVolume(float value)
    {
        BGMVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKeys.BGMVolume, BGMVolume);
        _mixer?.SetFloat(MixerParams.BGM, LinearToDb(BGMVolume));
    }

    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKeys.SFXVolume, SFXVolume);
        _mixer?.SetFloat(MixerParams.SFX, LinearToDb(SFXVolume));
    }
}
