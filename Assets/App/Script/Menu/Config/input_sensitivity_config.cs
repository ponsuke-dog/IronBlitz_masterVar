using UnityEngine;

/// <summary>
/// 入力感度の初期値・保存キー・範囲を管理する設定データ。
/// </summary>
[CreateAssetMenu(
    fileName = "InputSensitivityConfig",
    menuName = "Game/Menu/Input Sensitivity Config"
)]
public class InputSensitivityConfig : ScriptableObject
{
    [Header("PlayerPrefs Keys")]
    public string mouseSensitivityKey = "Input_MouseSensitivity";
    public string controllerSensitivityKey = "Input_ControllerSensitivity";

    [Header("Default Values")]
    [Range(0.1f, 10.0f)] public float defaultMouseSensitivity = 1.0f;
    [Range(0.1f, 10.0f)] public float defaultControllerSensitivity = 1.0f;

    [Header("Value Range")]
    public float minSensitivity = 0.1f;
    public float maxSensitivity = 10.0f;
}
