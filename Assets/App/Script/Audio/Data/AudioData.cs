using System;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 1つのオーディオ定義データ
/// </summary>
[Serializable]
public class AudioData
{
    [Header("Basic")]
    [SerializeField] private string audioId;
    [SerializeField] private AudioCategory category;
    [SerializeField] private AudioClip clip;

    [Header("Playback")]
    [SerializeField] private bool loop = false;

    [Header("Output")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [Header("3D Settings")]
    [SerializeField] private bool use3DSound = false;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float spatialBlend = 1.0f;

    [SerializeField] private float minDistance = 1.0f;
    [SerializeField] private float maxDistance = 300.0f;

    public string AudioId => audioId;
    public AudioCategory Category => category;
    public AudioClip Clip => clip;
    public bool Loop => loop;
    public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
    public bool Use3DSound => use3DSound;
    public float SpatialBlend => spatialBlend;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;

    /// <summary>
    /// AudioDataとして有効か確認する
    /// </summary>
    public bool IsValid()
    {
        return string.IsNullOrEmpty(audioId) == false && clip != null;
    }
}