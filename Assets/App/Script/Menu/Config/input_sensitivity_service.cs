using UnityEngine;

/// <summary>
/// 入力感度の現在値管理・保存・リセットを担当するサービスクラス。
/// </summary>
public class InputSensitivityService : MonoBehaviour
{
    [SerializeField] private InputSensitivityConfig config;
    [SerializeField] private PlayerPrefsSensitivitySaveRepository saveRepository;

    public InputSensitivityData CurrentSensitivityData { get; private set; }

    private void Awake()
    {
        Load();
    }

    /// <summary>
    /// 保存済みの感度を読み込む。
    /// </summary>
    public InputSensitivityData Load()
    {
        CurrentSensitivityData = saveRepository.Load();

        CurrentSensitivityData.mouseSensitivity = ClampSensitivity(
            CurrentSensitivityData.mouseSensitivity
        );

        CurrentSensitivityData.controllerSensitivity = ClampSensitivity(
            CurrentSensitivityData.controllerSensitivity
        );

        // InputSensitivityService.Load() の中に一時追加
        Debug.Log($"Load Mouse: {CurrentSensitivityData.mouseSensitivity}");
        Debug.Log($"Load Controller: {CurrentSensitivityData.controllerSensitivity}");

        return CurrentSensitivityData;
    }

    /// <summary>
    /// マウス感度を設定して保存する。
    /// </summary>
    public void SetMouseSensitivity(float value)
    {
        CurrentSensitivityData.mouseSensitivity = ClampSensitivity(value);
        saveRepository.Save(CurrentSensitivityData);
    }

    /// <summary>
    /// コントローラー感度を設定して保存する。
    /// </summary>
    public void SetControllerSensitivity(float value)
    {
        CurrentSensitivityData.controllerSensitivity = ClampSensitivity(value);
        saveRepository.Save(CurrentSensitivityData);
    }

    /// <summary>
    /// 感度設定を初期値に戻す。
    /// </summary>
    public void ResetSensitivity()
    {
        saveRepository.Delete();

        CurrentSensitivityData = new InputSensitivityData(
            config.defaultMouseSensitivity,
            config.defaultControllerSensitivity
        );

        saveRepository.Save(CurrentSensitivityData);
    }

    /// <summary>
    /// 感度値を設定範囲内に丸める。
    /// </summary>
    private float ClampSensitivity(float value)
    {
        return Mathf.Clamp(value, config.minSensitivity, config.maxSensitivity);
    }
}
