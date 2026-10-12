// 여러 스크립트가 같은 문자열을 써야 하는 키·이름 모음.
// PlayerPrefs 키 값은 기존 저장 데이터와 호환되도록 바꾸지 않는다.
public static class PrefKeys
{
    public const string LANGUAGE = "SelectedLanguage";
    public const string SKIP_TUTORIAL = "SkipTutorial";
    public const string BGM_VOLUME = "Vol_BGM";
    public const string SFX_VOLUME = "Vol_SFX";
}

public static class SceneNames
{
    public const string TITLE = "TitleScene";
}

// AudioMixer에 Expose한 파라미터 이름
public static class MixerParams
{
    public const string BGM = "BGMVolume";
    public const string SFX = "SFXVolume";
}

public static class ItemNames
{
    public const string ENERGY_DRINK = "ENERGYDRINK";
}
