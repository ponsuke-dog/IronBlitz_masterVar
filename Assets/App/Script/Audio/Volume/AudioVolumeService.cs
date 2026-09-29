using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Audio音量の状態管理、保存、Mixer反映を担当するService。
/// UIが無い状態でもPrefab内に保持しておく。
/// 
/// 【追加仕様】
/// - ゲーム開始直後からAudioMixerへ確実に音量を反映する
/// - Awakeだけでなく、Startと1フレーム後にも再反映する
/// - Config画面を開かなくてもBase Boost Dbが反映されるようにする
/// </summary>
public class AudioVolumeService : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AudioVolumeConfig volumeConfig;
    [SerializeField] private AudioMixerVolumeApplier volumeApplier;
    [SerializeField] private PlayerPrefsVolumeSaveRepository saveRepository;

    [Header("Startup Apply")]
    [Tooltip("起動直後にStartでも再反映します。AudioMixer値が他の初期化で上書きされる場合の対策です。")]
    [SerializeField] private bool applyOnStart = true;

    [Tooltip("起動直後に1フレーム待ってから再反映します。Configを開くまで音量が反映されない場合の対策です。")]
    [SerializeField] private bool applyOneFrameAfterStart = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = true;

    public event Action<AudioVolumeData> OnVolumeChanged;

    public AudioVolumeData CurrentVolumeData { get; private set; }

    private Coroutine delayedApplyCoroutine;

    private void Awake()
    {
        // 保存済み音量を読み込み、まず一度Mixerへ反映します。
        LoadVolumes();
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ReapplyCurrentVolumes("Start");
        }

        if (applyOneFrameAfterStart)
        {
            delayedApplyCoroutine = StartCoroutine(ReapplyAfterOneFrame());
        }
    }

    private void OnDisable()
    {
        if (delayedApplyCoroutine != null)
        {
            StopCoroutine(delayedApplyCoroutine);
            delayedApplyCoroutine = null;
        }
    }

    /// <summary>
    /// 保存済み音量を読み込み、Mixerへ反映する。
    /// </summary>
    public void LoadVolumes()
    {
        if (saveRepository != null)
        {
            CurrentVolumeData = saveRepository.Load();
        }
        else if (volumeConfig != null)
        {
            CurrentVolumeData = volumeConfig.CreateDefaultVolumeData();
        }
        else
        {
            CurrentVolumeData = new AudioVolumeData(1.0f, 1.0f, 1.0f, 1.0f);
        }

        ApplyAndNotify("LoadVolumes");
    }

    public void SetMasterVolume(float value)
    {
        CurrentVolumeData.masterVolume = Mathf.Clamp01(value);
        SaveAndApply("SetMasterVolume");
    }

    public void SetBgmVolume(float value)
    {
        CurrentVolumeData.bgmVolume = Mathf.Clamp01(value);
        SaveAndApply("SetBgmVolume");
    }

    public void SetSeVolume(float value)
    {
        CurrentVolumeData.seVolume = Mathf.Clamp01(value);
        SaveAndApply("SetSeVolume");
    }

    public void SetUiVolume(float value)
    {
        CurrentVolumeData.uiVolume = Mathf.Clamp01(value);
        SaveAndApply("SetUiVolume");
    }

    /// <summary>
    /// 音量を初期値へ戻す。
    /// </summary>
    public void ResetVolumes()
    {
        if (volumeConfig != null)
        {
            CurrentVolumeData = volumeConfig.CreateDefaultVolumeData();
        }
        else
        {
            CurrentVolumeData = new AudioVolumeData(1.0f, 1.0f, 1.0f, 1.0f);
        }

        SaveAndApply("ResetVolumes");
    }

    /// <summary>
    /// 現在保持している音量値を、保存せずにMixerへ再反映する。
    /// ゲーム開始直後やシーン遷移後の再適用に使う。
    /// </summary>
    public void ReapplyCurrentVolumes(string reason = "Manual")
    {
        if (CurrentVolumeData == null)
        {
            LoadVolumes();
            return;
        }

        ApplyAndNotify(reason);
    }

    /// <summary>
    /// 1フレーム待ってから現在の音量を再反映する。
    /// 他の初期化処理がMixer値を上書きする場合の対策。
    /// </summary>
    private IEnumerator ReapplyAfterOneFrame()
    {
        yield return null;

        ReapplyCurrentVolumes("OneFrameAfterStart");

        delayedApplyCoroutine = null;
    }

    /// <summary>
    /// 現在の音量を保存して反映する。
    /// </summary>
    private void SaveAndApply(string reason)
    {
        if (saveRepository != null)
        {
            saveRepository.Save(CurrentVolumeData);
        }

        ApplyAndNotify(reason);
    }

    /// <summary>
    /// Mixer反映と変更通知を行う。
    /// </summary>
    private void ApplyAndNotify(string reason)
    {
        if (volumeApplier != null)
        {
            volumeApplier.Apply(CurrentVolumeData);
        }
        else
        {
            Debug.LogWarning("[AudioVolumeService] AudioMixerVolumeApplierが未設定です。");
        }

        if (enableDebugLog && CurrentVolumeData != null)
        {
            Debug.Log(
                $"[AudioVolumeService] Apply Reason = {reason}, " +
                $"Master = {CurrentVolumeData.masterVolume}, " +
                $"BGM = {CurrentVolumeData.bgmVolume}, " +
                $"SE = {CurrentVolumeData.seVolume}, " +
                $"UI = {CurrentVolumeData.uiVolume}");
        }

        OnVolumeChanged?.Invoke(CurrentVolumeData);
    }
}