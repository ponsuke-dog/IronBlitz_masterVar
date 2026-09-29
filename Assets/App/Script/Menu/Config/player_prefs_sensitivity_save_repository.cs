using UnityEngine;

/// <summary>
/// PlayerPrefsを使って入力感度を保存・読み込み・削除するクラス。
/// </summary>
public class PlayerPrefsSensitivitySaveRepository : MonoBehaviour
{
    [SerializeField] private InputSensitivityConfig config;

    /// <summary>
    /// 保存済みの感度を読み込む。
    /// 未保存の場合は設定ファイルの初期値を使用する。
    /// </summary>
    public InputSensitivityData Load()
    {
        float mouseSensitivity = PlayerPrefs.GetFloat(
            config.mouseSensitivityKey,
            config.defaultMouseSensitivity
        );

        float controllerSensitivity = PlayerPrefs.GetFloat(
            config.controllerSensitivityKey,
            config.defaultControllerSensitivity
        );

        return new InputSensitivityData(mouseSensitivity, controllerSensitivity);
    }

    /// <summary>
    /// 感度を保存する。
    /// </summary>
    public void Save(InputSensitivityData data)
    {
        PlayerPrefs.SetFloat(config.mouseSensitivityKey, data.mouseSensitivity);
        PlayerPrefs.SetFloat(config.controllerSensitivityKey, data.controllerSensitivity);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 保存済みの感度を削除する。
    /// </summary>
    public void Delete()
    {
        PlayerPrefs.DeleteKey(config.mouseSensitivityKey);
        PlayerPrefs.DeleteKey(config.controllerSensitivityKey);
        PlayerPrefs.Save();
    }
}