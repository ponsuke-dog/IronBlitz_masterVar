using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オーディオ定義をまとめて管理するデータベース
/// </summary>
[CreateAssetMenu(fileName = "audio_database", menuName = "GameJam/Audio/Audio Database")]
public class AudioDatabase : ScriptableObject
{
    [Header("Audio List")]
    [SerializeField] private List<AudioData> audios = new List<AudioData>();

    public IReadOnlyList<AudioData> Audios => audios;

    /// <summary>
    /// audioIdに一致するAudioDataを取得する
    /// </summary>
    public AudioData GetById(string audioId)
    {
        for (int i = 0; i < audios.Count; i++)
        {
            if (audios[i] == null)
            {
                continue;
            }

            if (audios[i].AudioId == audioId)
            {
                return audios[i];
            }
        }

        return null;
    }
}