using UnityEngine;

/// <summary>
/// 3D SE再生専用プレイヤー
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class Se3DPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioDatabase audioDatabase;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// 指定IDの3D SEを指定位置で再生する
    /// </summary>
    public void PlaySe(string audioId, Vector3 playPosition)
    {
        AudioData data = audioDatabase.GetById(audioId);

        if (data == null || data.Category != AudioCategory.SE)
        {
            Debug.LogWarning($"SE not found. audioId: {audioId}");
            return;
        }

        if (data.IsValid() == false)
        {
            Debug.LogWarning($"SE data is invalid. audioId: {audioId}");
            return;
        }

        transform.position = playPosition;

        audioSource.clip = data.Clip;
        audioSource.loop = data.Loop;
        audioSource.outputAudioMixerGroup = data.OutputMixerGroup;
        audioSource.spatialBlend = data.Use3DSound ? data.SpatialBlend : 0.0f;
        audioSource.minDistance = data.MinDistance;
        audioSource.maxDistance = data.MaxDistance;

        audioSource.Play();
    }

    /// <summary>
    /// SEを停止する
    /// </summary>
    public void StopAudio()
    {
        audioSource.Stop();
    }
}