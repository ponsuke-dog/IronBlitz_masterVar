using UnityEngine;

/// <summary>
/// CameraControlSettingsとCameraConfigRuntimeStoreをつなぐ保存・読み込みクラス。
/// 
/// 【役割】
/// - PlayerPrefsから読み込んだ値をCameraControlSettingsへ反映する
/// - CameraControlSettingsの変更内容をPlayerPrefsへ保存する
/// - ビルド後のシーン遷移でもScriptableObject初期値に戻らないようにする
/// </summary>
public class CameraControlSettingsSaveLoader : MonoBehaviour
{
    [Header("保存対象のカメラ設定")]
    [SerializeField] private CameraControlSettings settings;

    [Header("デバッグログ")]
    [SerializeField] private bool enableDebugLog = true;

    private void Awake()
    {
        Load();
    }

    private void OnEnable()
    {
        Load();
    }

    /// <summary>
    /// 保存済み設定を読み込み、CameraControlSettingsへ反映する。
    /// </summary>
    public void Load()
    {
        if (settings == null)
        {
            Debug.LogError("[CameraConfig] CameraControlSettingsが未設定です。");
            return;
        }

        CameraConfigRuntimeStore.Load(settings, enableDebugLog);
        CameraConfigRuntimeStore.ApplyTo(settings, enableDebugLog);

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] LOADER LOAD MouseHorizontal = {settings.mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// PlayerPrefsから強制再読み込みする。
    /// </summary>
    public void ForceReload()
    {
        if (settings == null)
        {
            Debug.LogError("[CameraConfig] CameraControlSettingsが未設定です。");
            return;
        }

        CameraConfigRuntimeStore.ForceReload(settings, enableDebugLog);
        CameraConfigRuntimeStore.ApplyTo(settings, enableDebugLog);

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] LOADER FORCE RELOAD MouseHorizontal = {settings.mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// CameraControlSettingsの現在値を保存する。
    /// </summary>
    public void Save()
    {
        if (settings == null)
        {
            Debug.LogError("[CameraConfig] CameraControlSettingsが未設定です。");
            return;
        }

        CameraConfigRuntimeStore.CaptureFrom(settings, enableDebugLog);
        CameraConfigRuntimeStore.Save(enableDebugLog);

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] LOADER SAVE MouseHorizontal = {settings.mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// CameraConfigの保存データを削除する。
    /// </summary>
    public void DeleteSaveData()
    {
        CameraConfigRuntimeStore.DeleteSaveData(enableDebugLog);

        if (settings != null)
        {
            CameraConfigRuntimeStore.Load(settings, enableDebugLog);
            CameraConfigRuntimeStore.ApplyTo(settings, enableDebugLog);
        }
    }
}