using UnityEngine;

/// <summary>
/// BGM再生専用プレイヤー。
/// 
/// 【追加仕様】
/// - AudioClipごとの音量補正倍率を反映する
/// - AudioMixer側のBGM音量とは別に、素材単位の音量差を補正する
/// </summary>
public class BgmPlayer : AudioSourcePlayer
{
    [Header("Database")]
    [SerializeField] private AudioDatabase audioDatabase;

    [Header("Clip Volume")]
    [SerializeField] private AudioClipVolumeResolver clipVolumeResolver;

    /// <summary>
    /// 指定IDのBGMを再生する。
    /// </summary>
    public void PlayBgm(string audioId)
    {
        if (audioDatabase == null)
        {
            Debug.LogWarning("AudioDatabase is not assigned.");
            return;
        }

        AudioData data = audioDatabase.GetById(audioId);

        if (data == null || data.Category != AudioCategory.BGM)
        {
            Debug.LogWarning($"BGM not found. audioId: {audioId}");
            return;
        }

        if (data.IsValid() == false)
        {
            Debug.LogWarning($"BGM data is invalid. audioId: {audioId}");
            return;
        }

        ApplyAudioData(data);

        // AudioSourcePlayer側で設定されたvolumeを基準に、
        // AudioClipごとの補正倍率を掛けます。
        ApplyClipVolumeScale(data.Clip);

        audioSource.Play();
    }

    /// <summary>
    /// AudioClipごとの音量補正倍率をAudioSourceへ反映する。
    /// 
    /// AudioMixer側でBGMカテゴリ音量を管理しているため、
    /// AudioSource.volumeには素材単位の補正値を直接設定します。
    /// </summary>
    private void ApplyClipVolumeScale(AudioClip audioClip)
    {
        if (audioSource == null)
        {
            return;
        }

        float clipVolumeScale = GetClipVolumeScale(audioClip);

        // 乗算ではなく代入にすることで、再生のたびに音量が累積変化するのを防ぎます。
        audioSource.volume = clipVolumeScale;

        string clipName = audioClip != null ? audioClip.name : "None";
        Debug.Log($"[BgmPlayer] Clip = {clipName}, ClipScale = {clipVolumeScale}, SourceVolume = {audioSource.volume}");
    }

    /// <summary>
    /// AudioClipごとの音量補正倍率を取得する。
    /// </summary>
    private float GetClipVolumeScale(AudioClip audioClip)
    {
        if (clipVolumeResolver == null)
        {
            return 1.0f;
        }

        return clipVolumeResolver.GetVolumeScale(audioClip);
    }
}