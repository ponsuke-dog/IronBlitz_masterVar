using UnityEngine;

[CreateAssetMenu(
    fileName = "CameraControlSettings",
    menuName = "Game/Settings/Camera Control Settings")]
public class CameraControlSettings : ScriptableObject
{
    #region 設定画面 / マウス

    [Header("設定画面 / マウス")]

    [Tooltip("マウス横感度。設定画面に出す想定の値です。1〜100で扱います。")]
    [Range(1f, 100f)]
    public float mouseHorizontalSensitivity = 50f;

    [Tooltip("マウス縦感度。設定画面に出す想定の値です。1〜100で扱います。")]
    [Range(1f, 100f)]
    public float mouseVerticalSensitivity = 50f;

    [Tooltip("ONならマウス横入力を反転します。")]
    public bool invertMouseX = false;

    [Tooltip("ONならマウス縦入力を反転します。")]
    public bool invertMouseY = false;

    #endregion

    #region 設定画面 / コントローラー

    [Header("設定画面 / コントローラー")]

    [Tooltip("コントローラー横回転速度。設定画面に出す想定の値です。1〜100で扱います。")]
    [Range(1f, 100f)]
    public float controllerHorizontalSpeed = 50f;

    [Tooltip("コントローラー縦回転速度。設定画面に出す想定の値です。1〜100で扱います。")]
    [Range(1f, 100f)]
    public float controllerVerticalSpeed = 50f;

    [Tooltip("ONならコントローラー横入力を反転します。")]
    public bool invertControllerX = false;

    [Tooltip("ONならコントローラー縦入力を反転します。")]
    public bool invertControllerY = false;

    [Header("設定画面 / コントローラー加減速")]

    [Tooltip("ONならコントローラー入力に加速/減速をかけます。OFFなら入力に即応します。")]
    public bool useControllerAcceleration = true;

    [Tooltip("入力を倒した時、目標速度へ近づく速さです。設定画面に出す想定の値です。1〜100で扱います。")]
    [Range(1f, 100f)]
    public float controllerAcceleration = 50f;

    [Tooltip("入力を離した時、速度が0へ戻る速さです。設定画面に出す想定の値です。1〜100で扱います。")]
    [Range(1f, 100f)]
    public float controllerDeceleration = 50f;

    #endregion

    #region 内部調整 / マウス変換レンジ

    [Header("内部調整 / マウス変換レンジ")]

    [Tooltip("マウス感度1の時の、Mouse Delta 1単位あたりの回転角度です。")]
    public float minMouseDegreesPerInput = 0.01f;

    [Tooltip("マウス感度100の時の、Mouse Delta 1単位あたりの回転角度です。")]
    public float maxMouseDegreesPerInput = 0.18f;

    [Tooltip("マウス入力のデッドゾーンです。微小な揺れで勝手に回る場合に上げます。")]
    public float mouseDeadZone = 0.001f;

    [Tooltip("ONならマウス入力の最大値を制限します。極端なマウス移動で暴れる場合に使います。")]
    public bool limitMouseInputMagnitude = true;

    [Tooltip("マウス入力の最大値です。")]
    public float maxMouseInputMagnitude = 120f;

    [Tooltip("ONならマウス入力にも滑らか処理をかけます。基本OFF推奨です。")]
    public bool smoothMouseInput = false;

    [Tooltip("マウス入力の滑らか時間です。使う場合もかなり小さめ推奨です。")]
    public float mouseSmoothTime = 0.01f;

    #endregion

    #region 内部調整 / コントローラー変換レンジ

    [Header("内部調整 / コントローラー変換レンジ")]

    [Tooltip("コントローラー速度1の時の回転速度です。単位は度/秒です。")]
    public float minControllerSpeed = 30f;

    [Tooltip("コントローラー速度100の時の回転速度です。単位は度/秒です。")]
    public float maxControllerSpeed = 520f;

    [Tooltip("コントローラー加速度1の時の加速量です。単位は度/秒^2です。")]
    public float minControllerAcceleration = 100f;

    [Tooltip("コントローラー加速度100の時の加速量です。単位は度/秒^2です。")]
    public float maxControllerAcceleration = 3200f;

    [Tooltip("コントローラー減速度1の時の減速量です。単位は度/秒^2です。")]
    public float minControllerDeceleration = 100f;

    [Tooltip("コントローラー減速度100の時の減速量です。単位は度/秒^2です。")]
    public float maxControllerDeceleration = 3600f;

    [Tooltip("スティック入力のデッドゾーンです。微小な倒れを無視します。")]
    public float stickDeadZone = 0.12f;

    [Tooltip("スティック入力倍率です。基本は1.0です。")]
    public float stickInputScale = 1.0f;

    [Tooltip("ONならスティック入力の最大値を制限します。通常ON推奨です。")]
    public bool limitStickInputMagnitude = true;

    [Tooltip("スティック入力の最大値です。基本は1.0です。")]
    public float maxStickInputMagnitude = 1.0f;

    [Tooltip("ONならスティック入力そのものに滑らか処理をかけます。加速処理とは別です。")]
    public bool smoothStickInput = false;

    [Tooltip("スティック入力の滑らか時間です。")]
    public float stickSmoothTime = 0.04f;

    #endregion

    #region 内部調整 / カメラ角度制限

    [Header("内部調整 / カメラ角度制限")]

    [Tooltip("上方向に向ける最大角度です。")]
    public float maxPitch = 65f;

    [Tooltip("下方向に向ける最大角度です。")]
    public float minPitch = -35f;

    #endregion

    #region 計算用プロパティ

    public float MouseHorizontalDegreesPerInput
    {
        get
        {
            return Convert01ToRange(
                mouseHorizontalSensitivity,
                minMouseDegreesPerInput,
                maxMouseDegreesPerInput
            );
        }
    }

    public float MouseVerticalDegreesPerInput
    {
        get
        {
            return Convert01ToRange(
                mouseVerticalSensitivity,
                minMouseDegreesPerInput,
                maxMouseDegreesPerInput
            );
        }
    }

    public float ControllerHorizontalDegreesPerSecond
    {
        get
        {
            return Convert01ToRange(
                controllerHorizontalSpeed,
                minControllerSpeed,
                maxControllerSpeed
            );
        }
    }

    public float ControllerVerticalDegreesPerSecond
    {
        get
        {
            return Convert01ToRange(
                controllerVerticalSpeed,
                minControllerSpeed,
                maxControllerSpeed
            );
        }
    }

    public float ControllerAccelerationPerSecond
    {
        get
        {
            return Convert01ToRange(
                controllerAcceleration,
                minControllerAcceleration,
                maxControllerAcceleration
            );
        }
    }

    public float ControllerDecelerationPerSecond
    {
        get
        {
            return Convert01ToRange(
                controllerDeceleration,
                minControllerDeceleration,
                maxControllerDeceleration
            );
        }
    }

    private float Convert01ToRange(float userValue, float min, float max)
    {
        float t = Mathf.InverseLerp(1f, 100f, Mathf.Clamp(userValue, 1f, 100f));
        return Mathf.Lerp(min, max, t);
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (minMouseDegreesPerInput < 0.0001f)
            minMouseDegreesPerInput = 0.0001f;

        if (maxMouseDegreesPerInput < minMouseDegreesPerInput)
            maxMouseDegreesPerInput = minMouseDegreesPerInput;

        if (maxControllerSpeed < minControllerSpeed)
            maxControllerSpeed = minControllerSpeed;

        if (maxControllerAcceleration < minControllerAcceleration)
            maxControllerAcceleration = minControllerAcceleration;

        if (maxControllerDeceleration < minControllerDeceleration)
            maxControllerDeceleration = minControllerDeceleration;

        if (maxPitch < minPitch)
            maxPitch = minPitch;
    }
#endif
}