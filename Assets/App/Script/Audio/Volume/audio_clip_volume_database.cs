using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AudioClipごとの音量補正値を管理するデータベース。
/// 
/// 【役割】
/// - インポートしたAudioClipごとに個別の音量補正値を設定する
/// - Inspector上でAudioClipと補正倍率を編集できるようにする
/// - BGM / SE / UIなどのカテゴリ音量とは別に、素材単位の音量差を補正する
/// </summary>
[CreateAssetMenu(
    fileName = "audio_clip_volume_database",
    menuName = "GameJam/Audio/Audio Clip Volume Database")]
public class AudioClipVolumeDatabase : ScriptableObject
{
    /// <summary>
    /// AudioClip 1つ分の音量補正設定。
    /// </summary>
    [Serializable]
    public class AudioClipVolumeEntry
    {
        [Header("対象AudioClip")]
        public AudioClip audioClip;

        [Header("音量補正倍率")]
        [Min(0.0f)]
        public float volumeScale = 1.0f;

        [Header("メモ")]
        [TextArea(1, 3)]
        public string memo;
    }

    [Header("未登録AudioClipのデフォルト補正値")]
    [SerializeField, Min(0.0f)] private float defaultVolumeScale = 1.0f;

    [Header("AudioClipごとの音量補正")]
    [SerializeField] private List<AudioClipVolumeEntry> entries = new List<AudioClipVolumeEntry>();

    /// <summary>
    /// 指定AudioClipの音量補正値を取得します。
    /// 未登録の場合はdefaultVolumeScaleを返します。
    /// </summary>
    public float GetVolumeScale(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            return defaultVolumeScale;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AudioClipVolumeEntry entry = entries[i];

            if (entry == null)
            {
                continue;
            }

            if (entry.audioClip == audioClip)
            {
                return Mathf.Max(0.0f, entry.volumeScale);
            }
        }

        return defaultVolumeScale;
    }

    /// <summary>
    /// AudioClipが登録済みか確認します。
    /// </summary>
    public bool Contains(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AudioClipVolumeEntry entry = entries[i];

            if (entry == null)
            {
                continue;
            }

            if (entry.audioClip == audioClip)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Inspector上で値が変更された時に、不正な値を補正します。
    /// </summary>
    private void OnValidate()
    {
        defaultVolumeScale = Mathf.Max(0.0f, defaultVolumeScale);

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null)
            {
                continue;
            }

            entries[i].volumeScale = Mathf.Max(0.0f, entries[i].volumeScale);
        }
    }
}