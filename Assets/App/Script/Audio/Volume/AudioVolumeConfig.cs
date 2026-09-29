using UnityEngine;

/// <summary>
/// Audio音量設定用ScriptableObject
/// Exposed Parameter名、保存キー、初期値を管理する
/// </summary>
[CreateAssetMenu(fileName = "audio_volume_config", menuName = "GameJam/Audio/Audio Volume Config")]
public class AudioVolumeConfig : ScriptableObject
{
    [Header("Exposed Parameter Names")]
    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [SerializeField] private string bgmVolumeParameter = "BgmVolume";
    [SerializeField] private string seVolumeParameter = "SeVolume";
    [SerializeField] private string uiVolumeParameter = "UiVolume";

    [Header("PlayerPrefs Keys")]
    [SerializeField] private string masterVolumeKey = "Audio_MasterVolume";
    [SerializeField] private string bgmVolumeKey = "Audio_BgmVolume";
    [SerializeField] private string seVolumeKey = "Audio_SeVolume";
    [SerializeField] private string uiVolumeKey = "Audio_UiVolume";

    [Header("Default Volumes")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float defaultMasterVolume = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float defaultBgmVolume = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float defaultSeVolume = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float defaultUiVolume = 1.0f;

    [Header("Mixer Settings")]
    [SerializeField] private float minDb = -80.0f;

    public string MasterVolumeParameter => masterVolumeParameter;
    public string BgmVolumeParameter => bgmVolumeParameter;
    public string SeVolumeParameter => seVolumeParameter;
    public string UiVolumeParameter => uiVolumeParameter;

    public string MasterVolumeKey => masterVolumeKey;
    public string BgmVolumeKey => bgmVolumeKey;
    public string SeVolumeKey => seVolumeKey;
    public string UiVolumeKey => uiVolumeKey;

    public float DefaultMasterVolume => defaultMasterVolume;
    public float DefaultBgmVolume => defaultBgmVolume;
    public float DefaultSeVolume => defaultSeVolume;
    public float DefaultUiVolume => defaultUiVolume;

    public float MinDb => minDb;

    public AudioVolumeData CreateDefaultVolumeData()
    {
        return new AudioVolumeData(
            defaultMasterVolume,
            defaultBgmVolume,
            defaultSeVolume,
            defaultUiVolume
        );
    }
}