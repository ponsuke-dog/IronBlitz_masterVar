using UnityEngine;

/// <summary>
/// UI音再生専用プレイヤー。
/// 
/// 【役割】
/// - AudioDatabaseに登録されたUIカテゴリのAudioDataを再生する
/// - AudioClipごとの音量補正倍率を反映する
/// - PlayOneShotを使い、UI SEを多重再生できるようにする
/// 
/// 【注意】
/// - PlayOneShot方式では、短いUI SEを重ねて鳴らせます。
/// - 個別ハンドル停止は厳密な個別停止ではなく、現在記録している最後のUI音に対する互換処理として残します。
/// </summary>
public class UiPlayer : AudioSourcePlayer
{
    [Header("Database")]
    [SerializeField] private AudioDatabase audioDatabase;

    [Header("Clip Volume")]
    [SerializeField] private AudioClipVolumeResolver clipVolumeResolver;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private string currentAudioId;
    private int currentHandle = -1;

    /// <summary>
    /// 指定IDのUI音を再生する。
    /// 従来互換用。ハンドルは返さない。
    /// </summary>
    public void PlayUi(string audioId)
    {
        PlayUiInternal(audioId, -1);
    }

    /// <summary>
    /// 指定IDのUI音を再生し、個別停止用ハンドルを返す。
    /// </summary>
    public int PlayUiWithHandle(string audioId)
    {
        int handle = AudioPlaybackHandleGenerator.Create();

        bool played = PlayUiInternal(audioId, handle);

        if (!played)
        {
            return -1;
        }

        return handle;
    }

    /// <summary>
    /// UI音を再生する内部処理。
    /// PlayOneShotを使うことで、直前のUI音を止めずに重ねて再生する。
    /// </summary>
    private bool PlayUiInternal(string audioId, int handle)
    {
        if (audioDatabase == null)
        {
            Debug.LogWarning("[UiPlayer] AudioDatabase is not assigned.");
            return false;
        }

        if (audioSource == null)
        {
            Debug.LogWarning("[UiPlayer] AudioSource is not assigned.");
            return false;
        }

        AudioData data = audioDatabase.GetById(audioId);

        if (data == null || data.Category != AudioCategory.UI)
        {
            Debug.LogWarning($"[UiPlayer] UI sound not found. audioId: {audioId}");
            return false;
        }

        if (data.IsValid() == false)
        {
            Debug.LogWarning($"[UiPlayer] UI sound data is invalid. audioId: {audioId}");
            return false;
        }

        currentAudioId = audioId;
        currentHandle = handle;

        // AudioDataのMixerGroupなどをAudioSourceへ反映します。
        ApplyAudioData(data);

        float clipVolumeScale = GetClipVolumeScale(data.Clip);

        // PlayOneShotの第2引数で素材ごとの音量補正を反映します。
        // AudioSource.volume側は1.0に固定し、UI全体の音量はAudioMixerで制御します。
        audioSource.volume = 1.0f;

        // audioSource.clipを差し替えてPlayするのではなく、PlayOneShotで多重再生します。
        audioSource.PlayOneShot(data.Clip, clipVolumeScale);

        if (enableDebugLog)
        {
            string clipName = data.Clip != null ? data.Clip.name : "None";
            Debug.Log($"[UiPlayer] PlayOneShot UI. AudioId = {audioId}, Clip = {clipName}, ClipScale = {clipVolumeScale}");
        }

        return true;
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

    /// <summary>
    /// 現在のUI音を停止する。
    /// PlayOneShotで鳴っている音もAudioSource単位で停止する。
    /// </summary>
    public new void StopAudio()
    {
        currentAudioId = null;
        currentHandle = -1;

        base.StopAudio();
    }

    /// <summary>
    /// 指定IDのUI音が最後に再生されたUI音なら停止する。
    /// PlayOneShot方式のため、厳密な個別停止ではなく互換用。
    /// </summary>
    public void StopUi(string audioId)
    {
        if (string.IsNullOrEmpty(audioId))
        {
            return;
        }

        if (currentAudioId != audioId)
        {
            return;
        }

        StopAudio();
    }

    /// <summary>
    /// 指定ハンドルのUI音が最後に再生されたUI音なら停止する。
    /// PlayOneShot方式のため、厳密な個別停止ではなく互換用。
    /// </summary>
    public void StopUi(int handle)
    {
        if (handle < 0)
        {
            return;
        }

        if (currentHandle != handle)
        {
            return;
        }

        StopAudio();
    }
}