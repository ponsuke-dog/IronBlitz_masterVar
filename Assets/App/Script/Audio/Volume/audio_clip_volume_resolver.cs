using UnityEngine;

/// <summary>
/// AudioClipごとの音量補正値を取得するための補助クラス。
/// 
/// 【役割】
/// - AudioClipVolumeDatabaseを保持する
/// - AudioClipから補正倍率を取得する
/// - 未設定時でも安全に1.0を返す
/// 
/// 【使い方】
/// - AudioSystem Prefab内に配置する
/// - BgmPlayer / UiPlayer / SePoolPlayerから参照して使う
/// </summary>
public class AudioClipVolumeResolver : MonoBehaviour
{
    [Header("AudioClip音量補正データベース")]
    [SerializeField] private AudioClipVolumeDatabase volumeDatabase;

    [Header("デバッグ")]
    [SerializeField] private bool enableDebugLog = false;

    /// <summary>
    /// AudioClipごとの音量補正倍率を取得します。
    /// Database未設定、またはClip未登録の場合は1.0を返します。
    /// </summary>
    public float GetVolumeScale(AudioClip audioClip)
    {
        if (audioClip == null)
        {
            return 1.0f;
        }

        if (volumeDatabase == null)
        {
            return 1.0f;
        }

        float volumeScale = volumeDatabase.GetVolumeScale(audioClip);

        if (enableDebugLog)
        {
            Debug.Log($"[AudioClipVolumeResolver] Clip = {audioClip.name}, Scale = {volumeScale}");
        }

        return volumeScale;
    }
}
