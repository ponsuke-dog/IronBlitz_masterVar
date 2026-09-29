using System;

/// <summary>
/// 入力感度の値を保持するデータクラス。
/// </summary>
[Serializable]
public class InputSensitivityData
{
    public float mouseSensitivity;
    public float controllerSensitivity;

    public InputSensitivityData(float mouseSensitivity, float controllerSensitivity)
    {
        this.mouseSensitivity = mouseSensitivity;
        this.controllerSensitivity = controllerSensitivity;
    }
}
