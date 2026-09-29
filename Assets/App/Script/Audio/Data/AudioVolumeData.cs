using System;

/// <summary>
/// Audio音量データ
/// 0.0f から 1.0f の正規化値で保持する
/// </summary>
[Serializable]
public class AudioVolumeData
{
    public float masterVolume;
    public float bgmVolume;
    public float seVolume;
    public float uiVolume;

    public AudioVolumeData(float masterVolume, float bgmVolume, float seVolume, float uiVolume)
    {
        this.masterVolume = masterVolume;
        this.bgmVolume = bgmVolume;
        this.seVolume = seVolume;
        this.uiVolume = uiVolume;
    }

    public AudioVolumeData Clone()
    {
        return new AudioVolumeData(masterVolume, bgmVolume, seVolume, uiVolume);
    }
}