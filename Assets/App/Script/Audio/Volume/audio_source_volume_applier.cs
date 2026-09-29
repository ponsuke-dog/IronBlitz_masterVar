using UnityEngine;

/// <summary>
/// AudioSourceに対してAudioClipごとの音量補正を適用するクラス。
/// 
/// 【役割】
/// - AudioSourceに設定されているAudioClipを確認する
/// - AudioClipVolumeDatabaseから補正値を取得する
/// - AudioSource.volumeへ補正済み音量を反映する
/// 
/// 【用途】
/// - BGM用AudioSource
/// - 環境音用AudioSource
/// - Scene上に直接配置しているAudioSource
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioSourceVolumeApplier : MonoBehaviour
{
    [Header("音量補正データベース")]
    [SerializeField] private AudioClipVolumeDatabase volumeDatabase;

    [Header("基準音量")]
    [SerializeField, Range(0.0f, 1.0f)] private float baseVolume = 1.0f;

    [Header("実行設定")]
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;

    [Header("デバッグ")]
    [SerializeField] private bool enableDebugLog = false;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (applyOnAwake)
        {
            ApplyVolume();
        }
    }

    private void OnEnable()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (applyOnEnable)
        {
            ApplyVolume();
        }
    }

    /// <summary>
    /// AudioSourceに現在設定されているAudioClipの音量補正を反映する。
    /// </summary>
    public void ApplyVolume()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            return;
        }

        float clipVolumeScale = 1.0f;

        if (volumeDatabase != null)
        {
            clipVolumeScale = volumeDatabase.GetVolumeScale(audioSource.clip);
        }

        audioSource.volume = baseVolume * clipVolumeScale;

        if (enableDebugLog)
        {
            string clipName = audioSource.clip != null
                ? audioSource.clip.name
                : "None";

            Debug.Log($"[AudioSourceVolumeApplier] Clip = {clipName}, Base = {baseVolume}, Scale = {clipVolumeScale}, Result = {audioSource.volume}");
        }
    }

    /// <summary>
    /// 基準音量を変更して、補正音量を再適用する。
    /// </summary>
    public void SetBaseVolume(float volume)
    {
        baseVolume = Mathf.Clamp01(volume);
        ApplyVolume();
    }

    /// <summary>
    /// Inspector上で値を変更した時にも反映しやすくする。
    /// </summary>
    private void OnValidate()
    {
        baseVolume = Mathf.Clamp01(baseVolume);

        if (!Application.isPlaying)
        {
            return;
        }

        ApplyVolume();
    }
}
