using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SEを複数同時再生するためのプール型プレイヤー。
/// audioId単位の停止と、ハンドル単位の個別停止に対応する。
/// 
/// 【追加仕様】
/// - AudioClipごとの音量補正倍率を反映する
/// - AudioMixer側のSE音量とは別に、素材単位の音量差を補正する
/// - 同じSEが短時間に重なりすぎて大音量になることを防ぐ
/// - Audio IDごとに、最小再生間隔と最大同時再生数を設定できる
/// </summary>
public class SePoolPlayer : MonoBehaviour
{
    [Serializable]
    private class SePlaybackLimitEntry
    {
        [Header("Audio ID")]
        public string audioId;

        [Header("この制限を使用する")]
        public bool enabled = true;

        [Header("同じAudio IDの最小再生間隔")]
        [Min(0.0f)]
        public float minInterval = 0.03f;

        [Header("同じAudio IDの最大同時再生数")]
        [Min(0)]
        public int maxSameSoundCount = 3;
    }

    [Header("Database")]
    [SerializeField] private AudioDatabase audioDatabase;

    [Header("Clip Volume")]
    [SerializeField] private AudioClipVolumeResolver clipVolumeResolver;

    [Header("Pool Settings")]
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private int maxPoolSize = 30;

    [Header("Playback Limit")]
    [Tooltip("同じSEが重なりすぎることを防ぐ場合はONにします。")]
    [SerializeField] private bool usePlaybackLimit = true;

    [Tooltip("個別設定がないAudio IDに使う最小再生間隔です。0なら間隔制限なしです。")]
    [SerializeField] private float defaultMinInterval = 0.0f;

    [Tooltip("個別設定がないAudio IDに使う最大同時再生数です。0なら同時再生数制限なしです。")]
    [SerializeField] private int defaultMaxSameSoundCount = 8;

    [Tooltip("Audio IDごとの再生制限設定です。弾の発射音などをここに登録してください。")]
    [SerializeField] private List<SePlaybackLimitEntry> playbackLimitEntries = new List<SePlaybackLimitEntry>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private readonly List<AudioSource> audioSources = new List<AudioSource>();

    /// <summary>
    /// AudioSourceごとの再生中audioId。
    /// </summary>
    private readonly Dictionary<AudioSource, string> playingAudioIds =
        new Dictionary<AudioSource, string>();

    /// <summary>
    /// AudioSourceごとの再生ハンドル。
    /// 同じaudioIdを複数再生した時に1つだけ止めるために使う。
    /// </summary>
    private readonly Dictionary<AudioSource, int> playingHandles =
        new Dictionary<AudioSource, int>();

    /// <summary>
    /// AudioSourceごとの自動解放Coroutine。
    /// 個別停止時にCoroutineも止めるために保持する。
    /// </summary>
    private readonly Dictionary<AudioSource, Coroutine> releaseCoroutines =
        new Dictionary<AudioSource, Coroutine>();

    /// <summary>
    /// Audio IDごとの制限設定。
    /// </summary>
    private readonly Dictionary<string, SePlaybackLimitEntry> playbackLimitDictionary =
        new Dictionary<string, SePlaybackLimitEntry>();

    /// <summary>
    /// Audio IDごとの最後の再生時刻。
    /// </summary>
    private readonly Dictionary<string, float> lastPlayTimes =
        new Dictionary<string, float>();

    private void Awake()
    {
        BuildPlaybackLimitDictionary();
        CreateInitialPool();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            BuildPlaybackLimitDictionary();
        }
    }
#endif

    /// <summary>
    /// Inspectorで設定した再生制限を検索用Dictionaryへ変換する。
    /// </summary>
    private void BuildPlaybackLimitDictionary()
    {
        playbackLimitDictionary.Clear();

        if (playbackLimitEntries == null)
        {
            return;
        }

        for (int i = 0; i < playbackLimitEntries.Count; i++)
        {
            SePlaybackLimitEntry entry = playbackLimitEntries[i];

            if (entry == null || string.IsNullOrEmpty(entry.audioId))
            {
                continue;
            }

            playbackLimitDictionary[entry.audioId] = entry;
        }
    }

    private void CreateInitialPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateAudioSource();
        }
    }

    private AudioSource CreateAudioSource()
    {
        GameObject sourceObject = new GameObject($"SeAudioSource_{audioSources.Count}");
        sourceObject.transform.SetParent(transform);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1.0f;

        audioSources.Add(source);
        return source;
    }

    /// <summary>
    /// 指定IDのSEを指定位置で再生する。
    /// 従来互換用。ハンドルは返さない。
    /// </summary>
    public void PlaySe(string audioId, Vector3 position)
    {
        PlaySeInternal(audioId, position, -1);
    }

    /// <summary>
    /// 指定IDのSEを指定位置で再生し、個別停止用ハンドルを返す。
    /// </summary>
    public int PlaySeWithHandle(string audioId, Vector3 position)
    {
        int handle = AudioPlaybackHandleGenerator.Create();

        bool played = PlaySeInternal(audioId, position, handle);

        if (!played)
        {
            return -1;
        }

        return handle;
    }

    /// <summary>
    /// SE再生の内部処理。
    /// </summary>
    private bool PlaySeInternal(string audioId, Vector3 position, int handle)
    {
        if (audioDatabase == null)
        {
            Debug.LogWarning("AudioDatabase is not assigned.");
            return false;
        }

        AudioData data = audioDatabase.GetById(audioId);

        if (data == null || data.Category != AudioCategory.SE)
        {
            Debug.LogWarning($"SE not found. audioId: {audioId}");
            return false;
        }

        if (data.IsValid() == false)
        {
            Debug.LogWarning($"SE data is invalid. audioId: {audioId}");
            return false;
        }

        // 同じSEが短時間に重なりすぎないように制限する。
        if (CanPlayByLimit(audioId) == false)
        {
            return false;
        }

        AudioSource source = GetAvailableAudioSource();

        if (source == null)
        {
            Debug.LogWarning("No available SE AudioSource.");
            return false;
        }

        StopReleaseCoroutine(source);

        ApplyAudioData(source, data, position);

        playingAudioIds[source] = audioId;
        playingHandles[source] = handle;

        source.Play();

        NotifyPlaybackStarted(audioId);

        if (data.Loop == false)
        {
            Coroutine coroutine = StartCoroutine(
                ReleaseAfterPlayback(source, data.Clip.length)
            );

            releaseCoroutines[source] = coroutine;
        }

        return true;
    }

    /// <summary>
    /// 再生制限により、指定Audio IDを今再生できるか確認する。
    /// </summary>
    private bool CanPlayByLimit(string audioId)
    {
        if (usePlaybackLimit == false)
        {
            return true;
        }

        if (string.IsNullOrEmpty(audioId))
        {
            return false;
        }

        CleanupStoppedSources();

        float currentTime = Time.unscaledTime;

        float minInterval = defaultMinInterval;
        int maxSameSoundCount = defaultMaxSameSoundCount;

        if (playbackLimitDictionary.TryGetValue(audioId, out SePlaybackLimitEntry entry))
        {
            if (entry != null && entry.enabled == false)
            {
                return true;
            }

            if (entry != null)
            {
                minInterval = entry.minInterval;
                maxSameSoundCount = entry.maxSameSoundCount;
            }
        }

        // 最小再生間隔チェック。
        // 同じAudio IDが短時間に連続再生された場合は間引く。
        if (minInterval > 0.0f &&
            lastPlayTimes.TryGetValue(audioId, out float lastPlayTime) &&
            currentTime - lastPlayTime < minInterval)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[SePoolPlayer] Skip SE by MinInterval. AudioId = {audioId}");
            }

            return false;
        }

        // 最大同時再生数チェック。
        // 0以下の場合は同時再生数制限なし。
        if (maxSameSoundCount > 0)
        {
            int sameSoundCount = CountPlayingSameAudioId(audioId);

            if (sameSoundCount >= maxSameSoundCount)
            {
                if (enableDebugLog)
                {
                    Debug.Log($"[SePoolPlayer] Skip SE by SameSoundCount. AudioId = {audioId}, Count = {sameSoundCount}");
                }

                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 指定Audio IDの再生開始を記録する。
    /// </summary>
    private void NotifyPlaybackStarted(string audioId)
    {
        if (string.IsNullOrEmpty(audioId))
        {
            return;
        }

        lastPlayTimes[audioId] = Time.unscaledTime;
    }

    /// <summary>
    /// 現在再生中の同じAudio ID数を数える。
    /// </summary>
    private int CountPlayingSameAudioId(string audioId)
    {
        int count = 0;

        foreach (KeyValuePair<AudioSource, string> pair in playingAudioIds)
        {
            if (pair.Key == null)
            {
                continue;
            }

            if (pair.Key.isPlaying == false)
            {
                continue;
            }

            if (pair.Value == audioId)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 指定IDのSEをすべて停止する。
    /// 同じaudioIdが複数同時再生されている場合は全て止める。
    /// </summary>
    public void StopSe(string audioId)
    {
        if (string.IsNullOrEmpty(audioId))
        {
            return;
        }

        List<AudioSource> stopTargets = new List<AudioSource>();

        foreach (KeyValuePair<AudioSource, string> pair in playingAudioIds)
        {
            if (pair.Value == audioId)
            {
                stopTargets.Add(pair.Key);
            }
        }

        for (int i = 0; i < stopTargets.Count; i++)
        {
            StopSource(stopTargets[i]);
        }
    }

    /// <summary>
    /// 指定ハンドルのSEを1つだけ停止する。
    /// </summary>
    public void StopSe(int handle)
    {
        if (handle < 0)
        {
            return;
        }

        AudioSource targetSource = null;

        foreach (KeyValuePair<AudioSource, int> pair in playingHandles)
        {
            if (pair.Value == handle)
            {
                targetSource = pair.Key;
                break;
            }
        }

        if (targetSource == null)
        {
            return;
        }

        StopSource(targetSource);
    }

    private AudioSource GetAvailableAudioSource()
    {
        CleanupStoppedSources();

        for (int i = 0; i < audioSources.Count; i++)
        {
            if (audioSources[i].isPlaying == false)
            {
                return audioSources[i];
            }
        }

        if (audioSources.Count < maxPoolSize)
        {
            return CreateAudioSource();
        }

        return null;
    }

    private void ApplyAudioData(AudioSource source, AudioData data, Vector3 position)
    {
        source.transform.position = position;
        source.clip = data.Clip;
        source.loop = data.Loop;
        source.outputAudioMixerGroup = data.OutputMixerGroup;

        // プールされたAudioSourceは前回再生時のvolumeが残る可能性があるため、
        // 毎回1.0を基準にAudioClipごとの補正倍率を設定します。
        float clipVolumeScale = GetClipVolumeScale(data.Clip);
        source.volume = clipVolumeScale;

        if (enableDebugLog)
        {
            string clipName = data.Clip != null ? data.Clip.name : "None";
            Debug.Log($"[SePoolPlayer] Play SE. Clip = {clipName}, ClipScale = {clipVolumeScale}, SourceVolume = {source.volume}");
        }

        if (data.Use3DSound)
        {
            source.spatialBlend = data.SpatialBlend;
            source.minDistance = data.MinDistance;
            source.maxDistance = data.MaxDistance;
        }
        else
        {
            source.spatialBlend = 0.0f;
        }
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

    private IEnumerator ReleaseAfterPlayback(AudioSource source, float clipLength)
    {
        yield return new WaitForSeconds(clipLength);

        if (source == null)
        {
            yield break;
        }

        StopSource(source);
    }

    /// <summary>
    /// 指定AudioSourceを停止し、再生情報をクリアする。
    /// </summary>
    private void StopSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        StopReleaseCoroutine(source);

        source.Stop();
        source.clip = null;

        playingAudioIds.Remove(source);
        playingHandles.Remove(source);
    }

    /// <summary>
    /// 自動解放Coroutineを停止する。
    /// </summary>
    private void StopReleaseCoroutine(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        if (releaseCoroutines.TryGetValue(source, out Coroutine coroutine))
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }

            releaseCoroutines.Remove(source);
        }
    }

    /// <summary>
    /// 再生終了済みAudioSourceの管理情報を掃除する。
    /// </summary>
    private void CleanupStoppedSources()
    {
        List<AudioSource> stoppedSources = new List<AudioSource>();

        foreach (KeyValuePair<AudioSource, string> pair in playingAudioIds)
        {
            if (pair.Key == null || pair.Key.isPlaying == false)
            {
                stoppedSources.Add(pair.Key);
            }
        }

        for (int i = 0; i < stoppedSources.Count; i++)
        {
            AudioSource source = stoppedSources[i];

            if (source != null)
            {
                StopReleaseCoroutine(source);
                source.clip = null;
            }

            playingAudioIds.Remove(source);
            playingHandles.Remove(source);
        }
    }

    /// <summary>
    /// 再生中のSEをすべて停止する。
    /// </summary>
    public void StopAllSe()
    {
        for (int i = 0; i < audioSources.Count; i++)
        {
            StopSource(audioSources[i]);
        }

        playingAudioIds.Clear();
        playingHandles.Clear();
        releaseCoroutines.Clear();
    }
}