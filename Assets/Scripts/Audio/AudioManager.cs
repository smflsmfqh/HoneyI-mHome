using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField]
    private AudioSource _bgmSource;

    [SerializeField]
    private AudioSource _sfxSource;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 일시정지·결과 화면에서 AudioListener.pause로 월드 소리를 멈춰도 BGM·UI 효과음은 계속 재생
        _bgmSource.ignoreListenerPause = true;
        _sfxSource.ignoreListenerPause = true;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
            return;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
            return;
        _bgmSource.Stop();
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.Play();
    }

    public void StopBGM() => _bgmSource.Stop();

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;
        _sfxSource.PlayOneShot(clip);
    }
}
