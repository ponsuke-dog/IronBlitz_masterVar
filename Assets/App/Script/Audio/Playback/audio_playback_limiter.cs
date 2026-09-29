using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 同じSEが短時間に重なりすぎることを防ぐための再生制限クラス。
/// 
/// 【用途】
/// - 弾の発射音など、同じSEが大量に同時再生される音を間引く
/// - Audio IDごとに最小再生間隔と同時再生数を設定する
/// </summary>
public class AudioPlaybackLimiter : MonoBehaviour
{
    [Serializable]
    private class LimitEntry
    {
        [Header("Audio ID")]
        public string audioId;

        [Header("同じAudio IDの最小再生間隔")]
        [Min(0.0f)]
        public float minInterval = 0.03f;

        [Header("同じAudio IDの最大同時再生数")]
        [Min(1)]
        public int maxSameSoundCount = 3;
    }

    [Header("Default Limit")]
    [SerializeField] private float defaultMinInterval = 0.0f;
    [SerializeField] private int defaultMaxSameSoundCount = 8;

    [Header("Limit Settings")]
    [SerializeField] private List<LimitEntry> limitEntries = new List<LimitEntry>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private readonly Dictionary<string, LimitEntry> limitDictionary = new Dictionary<string, LimitEntry>();
    private readonly Dictionary<string, float> lastPlayTimes = new Dictionary<string, float>();
    private readonly Dictionary<string, List<float>> activeEndTimes = new Dictionary<string, List<float>>();

    private void Awake()
    {
        BuildDictionary();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            BuildDictionary();
        }
    }
#endif

    /// <summary>
    /// Inspector設定を検索用Dictionaryに変換する。
    /// </summary>
    private void BuildDictionary()
    {
        limitDictionary.Clear();

        for (int i = 0; i < limitEntries.Count; i++)
        {
            LimitEntry entry = limitEntries[i];

            if (entry == null || string.IsNullOrEmpty(entry.audioId))
            {
                continue;
            }

            limitDictionary[entry.audioId] = entry;
        }
    }

    /// <summary>
    /// 指定Audio IDのSEを再生してよいか判定する。
    /// </summary>
    public bool CanPlay(string audioId, AudioClip audioClip)
    {
        if (string.IsNullOrEmpty(audioId))
        {
            return false;
        }

        float currentTime = Time.unscaledTime;

        LimitEntry entry = GetLimitEntry(audioId);

        float minInterval = entry != null ? entry.minInterval : defaultMinInterval;
        int maxSameSoundCount = entry != null ? entry.maxSameSoundCount : defaultMaxSameSoundCount;

        // 短時間の連続再生を間引く。
        if (minInterval > 0.0f &&
            lastPlayTimes.TryGetValue(audioId, out float lastPlayTime) &&
            currentTime - lastPlayTime < minInterval)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[AudioPlaybackLimiter] Skipped by interval. AudioId = {audioId}");
            }

            return false;
        }

        // 同時再生数を確認する。
        List<float> endTimes = GetEndTimeList(audioId);
        RemoveFinishedVoices(endTimes, currentTime);

        if (endTimes.Count >= maxSameSoundCount)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[AudioPlaybackLimiter] Skipped by count. AudioId = {audioId}, Count = {endTimes.Count}");
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 再生開始を記録する。
    /// </summary>
    public void NotifyPlay(string audioId, AudioClip audioClip)
    {
        if (string.IsNullOrEmpty(audioId))
        {
            return;
        }

        float currentTime = Time.unscaledTime;
        float clipLength = audioClip != null ? audioClip.length : 0.1f;

        lastPlayTimes[audioId] = currentTime;

        List<float> endTimes = GetEndTimeList(audioId);
        RemoveFinishedVoices(endTimes, currentTime);

        endTimes.Add(currentTime + clipLength);
    }

    private LimitEntry GetLimitEntry(string audioId)
    {
        limitDictionary.TryGetValue(audioId, out LimitEntry entry);
        return entry;
    }

    private List<float> GetEndTimeList(string audioId)
    {
        if (activeEndTimes.TryGetValue(audioId, out List<float> endTimes))
        {
            return endTimes;
        }

        endTimes = new List<float>();
        activeEndTimes[audioId] = endTimes;
        return endTimes;
    }

    private void RemoveFinishedVoices(List<float> endTimes, float currentTime)
    {
        for (int i = endTimes.Count - 1; i >= 0; i--)
        {
            if (endTimes[i] <= currentTime)
            {
                endTimes.RemoveAt(i);
            }
        }
    }
}
