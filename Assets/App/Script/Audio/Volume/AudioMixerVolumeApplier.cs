using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioVolumeDataをAudioMixerへ反映するクラス。
/// 
/// 【追加仕様】
/// - BGM / SE / UI に基準Boost dBを加算できる
/// - Boost用MixerGroupを作らず、既存MixerGroupの音量を底上げする
/// - Clipごとの音量差は AudioClipVolumeDatabase 側で下げて調整する
/// </summary>
public class AudioMixerVolumeApplier : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Config")]
    [SerializeField] private AudioVolumeConfig volumeConfig;

    [Header("Base Boost Db")]
    [Tooltip("BGM全体の基準音量を底上げするdB値です。元音量が小さい場合は6〜12程度から確認します。")]
    [SerializeField] private float bgmBaseBoostDb = 0.0f;

    [Tooltip("SE全体の基準音量を底上げするdB値です。元音量が小さい場合は6〜12程度から確認します。")]
    [SerializeField] private float seBaseBoostDb = 0.0f;

    [Tooltip("UI音全体の基準音量を底上げするdB値です。元音量が小さい場合は3〜6程度から確認します。")]
    [SerializeField] private float uiBaseBoostDb = 0.0f;

    [Header("Limit")]
    [Tooltip("Mixerへ設定する最大dB値です。上げすぎによる音割れを防ぐための上限です。")]
    [SerializeField] private float maxDb = 20.0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    /// <summary>
    /// 音量データをAudioMixerへ反映する。
    /// </summary>
    public void Apply(AudioVolumeData data)
    {
        if (audioMixer == null || volumeConfig == null || data == null)
        {
            Debug.LogWarning("[AudioMixerVolumeApplier] reference is missing.");
            return;
        }

        // Masterは全体の最終音量なので、基本的にはBoostしません。
        SetVolume(volumeConfig.MasterVolumeParameter, data.masterVolume, 0.0f, "Master");

        // BGM / SE / UI は既存MixerGroupを底上げするため、カテゴリごとのBoost値を足します。
        SetVolume(volumeConfig.BgmVolumeParameter, data.bgmVolume, bgmBaseBoostDb, "BGM");
        SetVolume(volumeConfig.SeVolumeParameter, data.seVolume, seBaseBoostDb, "SE");
        SetVolume(volumeConfig.UiVolumeParameter, data.uiVolume, uiBaseBoostDb, "UI");
    }

    /// <summary>
    /// 0.0f〜1.0fの音量をdBへ変換し、Boost値を加算してMixerへ設定する。
    /// </summary>
    private void SetVolume(string parameterName, float normalizedVolume, float baseBoostDb, string label)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            Debug.LogWarning($"[AudioMixerVolumeApplier] {label} parameterName is empty.");
            return;
        }

        float clampedVolume = Mathf.Clamp01(normalizedVolume);
        float dbVolume = ConvertToDecibel(clampedVolume);

        // 音量0のときは完全にミュート扱いにしたいので、Boostを足さずMinDbのままにします。
        if (clampedVolume > 0.0001f)
        {
            dbVolume += baseBoostDb;
        }

        dbVolume = Mathf.Clamp(dbVolume, volumeConfig.MinDb, maxDb);

        bool result = audioMixer.SetFloat(parameterName, dbVolume);

        if (enableDebugLog)
        {
            Debug.Log(
                $"[AudioMixerVolumeApplier] {label} Param={parameterName}, Volume={clampedVolume}, Boost={baseBoostDb}dB, Result={dbVolume}dB, SetResult={result}");
        }
    }

    /// <summary>
    /// 正規化音量をdBへ変換する。
    /// </summary>
    private float ConvertToDecibel(float normalizedVolume)
    {
        if (normalizedVolume <= 0.0001f)
        {
            return volumeConfig.MinDb;
        }

        return Mathf.Log10(normalizedVolume) * 20.0f;
    }
}