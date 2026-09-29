using UnityEngine;

/// <summary>
/// AudioSource に AudioData を適用する共通処理を持つ基底クラス
/// </summary>
public abstract class AudioSourcePlayer : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] protected AudioSource audioSource;

    protected virtual void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    /// <summary>
    /// AudioData の内容を AudioSource に反映する
    /// </summary>
    protected void ApplyAudioData(AudioData audioData)
    {
        if (audioData == null)
        {
            Debug.LogWarning("AudioData is null.");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogWarning("AudioSource is not assigned.");
            return;
        }

        audioSource.clip = audioData.Clip;
        audioSource.loop = audioData.Loop;
        audioSource.outputAudioMixerGroup = audioData.OutputMixerGroup;

        if (audioData.Use3DSound)
        {
            audioSource.spatialBlend = audioData.SpatialBlend;
            audioSource.minDistance = audioData.MinDistance;
            audioSource.maxDistance = audioData.MaxDistance;
        }
        else
        {
            audioSource.spatialBlend = 0.0f;
        }
    }

    /// <summary>
    /// 再生中の音を停止する
    /// </summary>
    public virtual void StopAudio()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.Stop();
    }

    /// <summary>
    /// 現在再生中か確認する
    /// </summary>
    public bool IsPlaying()
    {
        if (audioSource == null)
        {
            return false;
        }

        return audioSource.isPlaying;
    }
}