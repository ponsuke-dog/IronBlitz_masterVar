using UnityEngine;

/// <summary>
/// Audioâπó ÇPlayerPrefsÇ÷ï€ë∂ÅEì«çûÇ∑ÇÈRepository
/// </summary>
public class PlayerPrefsVolumeSaveRepository : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private AudioVolumeConfig volumeConfig;

    /// <summary>
    /// ï€ë∂çœÇ›âπó Çì«Ç›çûÇﬁ
    /// ñ¢ï€ë∂ÇÃèÍçáÇÕConfigÇÃèâä˙ílÇégÇ§
    /// </summary>
    public AudioVolumeData Load()
    {
        if (volumeConfig == null)
        {
            Debug.LogWarning("AudioVolumeConfig is not assigned.");
            return new AudioVolumeData(1.0f, 1.0f, 1.0f, 1.0f);
        }

        float masterVolume = PlayerPrefs.GetFloat(volumeConfig.MasterVolumeKey, volumeConfig.DefaultMasterVolume);
        float bgmVolume = PlayerPrefs.GetFloat(volumeConfig.BgmVolumeKey, volumeConfig.DefaultBgmVolume);
        float seVolume = PlayerPrefs.GetFloat(volumeConfig.SeVolumeKey, volumeConfig.DefaultSeVolume);
        float uiVolume = PlayerPrefs.GetFloat(volumeConfig.UiVolumeKey, volumeConfig.DefaultUiVolume);

        return new AudioVolumeData(masterVolume, bgmVolume, seVolume, uiVolume);
    }

    /// <summary>
    /// âπó Çï€ë∂Ç∑ÇÈ
    /// </summary>
    public void Save(AudioVolumeData data)
    {
        if (volumeConfig == null || data == null)
        {
            return;
        }

        PlayerPrefs.SetFloat(volumeConfig.MasterVolumeKey, data.masterVolume);
        PlayerPrefs.SetFloat(volumeConfig.BgmVolumeKey, data.bgmVolume);
        PlayerPrefs.SetFloat(volumeConfig.SeVolumeKey, data.seVolume);
        PlayerPrefs.SetFloat(volumeConfig.UiVolumeKey, data.uiVolume);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// ï€ë∂çœÇ›âπó ÇçÌèúÇ∑ÇÈ
    /// </summary>
    public void Delete()
    {
        if (volumeConfig == null)
        {
            return;
        }

        PlayerPrefs.DeleteKey(volumeConfig.MasterVolumeKey);
        PlayerPrefs.DeleteKey(volumeConfig.BgmVolumeKey);
        PlayerPrefs.DeleteKey(volumeConfig.SeVolumeKey);
        PlayerPrefs.DeleteKey(volumeConfig.UiVolumeKey);
        PlayerPrefs.Save();
    }
}