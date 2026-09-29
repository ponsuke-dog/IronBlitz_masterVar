using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// カメラ設定ページのUI制御クラス。
/// 
/// 【役割】
/// - 保存済みカメラ設定をUIへ反映する
/// - UI操作時にCameraControlSettingsへ反映して保存する
/// 
/// 【ビルド対応の重要点】
/// - ScriptableObjectの初期値を保存済みデータとして信用しない
/// - OnEnable時に必ずSaveLoader.Load()を呼ぶ
/// - UI反映にはSetValueWithoutNotify / SetIsOnWithoutNotifyを使う
/// - UI反映後にイベント登録する
/// - シーン遷移直後の他システム初期化対策として、1フレーム後にも再反映する
/// </summary>
public class CameraConfigPageController : MonoBehaviour
{
    [Header("設定データ")]
    [SerializeField] private CameraControlSettings cameraSetting;

    [Header("保存・読み込み")]
    [SerializeField] private CameraControlSettingsSaveLoader saveLoader;

    [Header("Mouse")]
    [SerializeField] private Slider mouseHorizontalSensitivitySlider;
    [SerializeField] private Slider mouseVerticalSensitivitySlider;
    [SerializeField] private Toggle invertMouseXToggle;
    [SerializeField] private Toggle invertMouseYToggle;

    [Header("Controller")]
    [SerializeField] private Slider controllerHorizontalSpeedSlider;
    [SerializeField] private Slider controllerVerticalSpeedSlider;
    [SerializeField] private Toggle invertControllerXToggle;
    [SerializeField] private Toggle invertControllerYToggle;

    [Header("Controller Acceleration")]
    [SerializeField] private Toggle useControllerAccelerationToggle;
    [SerializeField] private Slider controllerAccelerationSlider;
    [SerializeField] private Slider controllerDecelerationSlider;

    [Header("ビルド対策")]
    [SerializeField] private bool reapplyAfterOneFrame = true;

    [Header("デバッグログ")]
    [SerializeField] private bool enableDebugLog = true;

    private bool isApplyingUi;
    private bool isEventRegistered;
    private Coroutine reapplyCoroutine;

    private void OnEnable()
    {
        if (!ValidateReferences())
        {
            return;
        }

        // 二重登録防止。
        UnregisterEvents();

        // ビルド後でも、画面を開くたびに保存値をCameraControlSettingsへ戻す。
        saveLoader.Load();

        // 保存値をUIへ反映する。
        ApplySettingToUi();

        // UI反映後にイベント登録する。
        RegisterEvents();

        // タイトル→ゲーム遷移時、他システムのStart/Awakeが後から値を触る場合への保険。
        if (reapplyAfterOneFrame)
        {
            if (reapplyCoroutine != null)
            {
                StopCoroutine(reapplyCoroutine);
            }

            reapplyCoroutine = StartCoroutine(ReapplyAfterOneFrame());
        }
    }

    private void OnDisable()
    {
        if (reapplyCoroutine != null)
        {
            StopCoroutine(reapplyCoroutine);
            reapplyCoroutine = null;
        }

        UnregisterEvents();
    }

    /// <summary>
    /// シーン遷移直後の初期化順対策。
    /// 1フレーム待ってから再度保存値をUIへ反映する。
    /// </summary>
    private IEnumerator ReapplyAfterOneFrame()
    {
        yield return null;

        if (!isActiveAndEnabled)
        {
            yield break;
        }

        UnregisterEvents();

        saveLoader.Load();
        ApplySettingToUi();

        RegisterEvents();

        if (enableDebugLog)
        {
            Debug.Log("[CameraConfig] ReapplyAfterOneFrame 完了");
        }

        reapplyCoroutine = null;
    }

    /// <summary>
    /// 必要な参照が設定されているか確認する。
    /// </summary>
    private bool ValidateReferences()
    {
        if (cameraSetting == null)
        {
            Debug.LogError("[CameraConfig] CameraControlSettingsが未設定です。");
            return false;
        }

        if (saveLoader == null)
        {
            Debug.LogError("[CameraConfig] CameraControlSettingsSaveLoaderが未設定です。");
            return false;
        }

        if (mouseHorizontalSensitivitySlider == null)
        {
            Debug.LogError("[CameraConfig] Mouse Horizontal Sliderが未設定です。");
            return false;
        }

        if (mouseVerticalSensitivitySlider == null)
        {
            Debug.LogError("[CameraConfig] Mouse Vertical Sliderが未設定です。");
            return false;
        }

        if (invertMouseXToggle == null)
        {
            Debug.LogError("[CameraConfig] Invert Mouse X Toggleが未設定です。");
            return false;
        }

        if (invertMouseYToggle == null)
        {
            Debug.LogError("[CameraConfig] Invert Mouse Y Toggleが未設定です。");
            return false;
        }

        if (controllerHorizontalSpeedSlider == null)
        {
            Debug.LogError("[CameraConfig] Controller Horizontal Sliderが未設定です。");
            return false;
        }

        if (controllerVerticalSpeedSlider == null)
        {
            Debug.LogError("[CameraConfig] Controller Vertical Sliderが未設定です。");
            return false;
        }

        if (invertControllerXToggle == null)
        {
            Debug.LogError("[CameraConfig] Invert Controller X Toggleが未設定です。");
            return false;
        }

        if (invertControllerYToggle == null)
        {
            Debug.LogError("[CameraConfig] Invert Controller Y Toggleが未設定です。");
            return false;
        }

        if (useControllerAccelerationToggle == null)
        {
            Debug.LogError("[CameraConfig] Use Controller Acceleration Toggleが未設定です。");
            return false;
        }

        if (controllerAccelerationSlider == null)
        {
            Debug.LogError("[CameraConfig] Controller Acceleration Sliderが未設定です。");
            return false;
        }

        if (controllerDecelerationSlider == null)
        {
            Debug.LogError("[CameraConfig] Controller Deceleration Sliderが未設定です。");
            return false;
        }

        return true;
    }

    /// <summary>
    /// CameraControlSettingsの値をUIへ反映する。
    /// </summary>
    private void ApplySettingToUi()
    {
        isApplyingUi = true;

        mouseHorizontalSensitivitySlider.SetValueWithoutNotify(
            cameraSetting.mouseHorizontalSensitivity);

        mouseVerticalSensitivitySlider.SetValueWithoutNotify(
            cameraSetting.mouseVerticalSensitivity);

        invertMouseXToggle.SetIsOnWithoutNotify(
            cameraSetting.invertMouseX);

        invertMouseYToggle.SetIsOnWithoutNotify(
            cameraSetting.invertMouseY);

        controllerHorizontalSpeedSlider.SetValueWithoutNotify(
            cameraSetting.controllerHorizontalSpeed);

        controllerVerticalSpeedSlider.SetValueWithoutNotify(
            cameraSetting.controllerVerticalSpeed);

        invertControllerXToggle.SetIsOnWithoutNotify(
            cameraSetting.invertControllerX);

        invertControllerYToggle.SetIsOnWithoutNotify(
            cameraSetting.invertControllerY);

        useControllerAccelerationToggle.SetIsOnWithoutNotify(
            cameraSetting.useControllerAcceleration);

        controllerAccelerationSlider.SetValueWithoutNotify(
            cameraSetting.controllerAcceleration);

        controllerDecelerationSlider.SetValueWithoutNotify(
            cameraSetting.controllerDeceleration);

        if (enableDebugLog)
        {
            Debug.Log("[CameraConfig] ApplySettingToUi");
            Debug.Log($"[CameraConfig] APPLY UI MouseHorizontal = {cameraSetting.mouseHorizontalSensitivity}");
        }

        isApplyingUi = false;
    }

    /// <summary>
    /// UIイベントを登録する。
    /// </summary>
    private void RegisterEvents()
    {
        if (isEventRegistered)
        {
            return;
        }

        mouseHorizontalSensitivitySlider.onValueChanged.AddListener(
            OnMouseHorizontalSensitivityChanged);

        mouseVerticalSensitivitySlider.onValueChanged.AddListener(
            OnMouseVerticalSensitivityChanged);

        invertMouseXToggle.onValueChanged.AddListener(
            OnInvertMouseXChanged);

        invertMouseYToggle.onValueChanged.AddListener(
            OnInvertMouseYChanged);

        controllerHorizontalSpeedSlider.onValueChanged.AddListener(
            OnControllerHorizontalSpeedChanged);

        controllerVerticalSpeedSlider.onValueChanged.AddListener(
            OnControllerVerticalSpeedChanged);

        invertControllerXToggle.onValueChanged.AddListener(
            OnInvertControllerXChanged);

        invertControllerYToggle.onValueChanged.AddListener(
            OnInvertControllerYChanged);

        useControllerAccelerationToggle.onValueChanged.AddListener(
            OnUseControllerAccelerationChanged);

        controllerAccelerationSlider.onValueChanged.AddListener(
            OnControllerAccelerationChanged);

        controllerDecelerationSlider.onValueChanged.AddListener(
            OnControllerDecelerationChanged);

        isEventRegistered = true;
    }

    /// <summary>
    /// UIイベントを解除する。
    /// </summary>
    private void UnregisterEvents()
    {
        if (!isEventRegistered)
        {
            return;
        }

        mouseHorizontalSensitivitySlider.onValueChanged.RemoveListener(
            OnMouseHorizontalSensitivityChanged);

        mouseVerticalSensitivitySlider.onValueChanged.RemoveListener(
            OnMouseVerticalSensitivityChanged);

        invertMouseXToggle.onValueChanged.RemoveListener(
            OnInvertMouseXChanged);

        invertMouseYToggle.onValueChanged.RemoveListener(
            OnInvertMouseYChanged);

        controllerHorizontalSpeedSlider.onValueChanged.RemoveListener(
            OnControllerHorizontalSpeedChanged);

        controllerVerticalSpeedSlider.onValueChanged.RemoveListener(
            OnControllerVerticalSpeedChanged);

        invertControllerXToggle.onValueChanged.RemoveListener(
            OnInvertControllerXChanged);

        invertControllerYToggle.onValueChanged.RemoveListener(
            OnInvertControllerYChanged);

        useControllerAccelerationToggle.onValueChanged.RemoveListener(
            OnUseControllerAccelerationChanged);

        controllerAccelerationSlider.onValueChanged.RemoveListener(
            OnControllerAccelerationChanged);

        controllerDecelerationSlider.onValueChanged.RemoveListener(
            OnControllerDecelerationChanged);

        isEventRegistered = false;
    }

    private void OnMouseHorizontalSensitivityChanged(float value)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.mouseHorizontalSensitivity = value;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE MouseHorizontal = {value}");
        }
    }

    private void OnMouseVerticalSensitivityChanged(float value)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.mouseVerticalSensitivity = value;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE MouseVertical = {value}");
        }
    }

    private void OnInvertMouseXChanged(bool isOn)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.invertMouseX = isOn;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE InvertMouseX = {isOn}");
        }
    }

    private void OnInvertMouseYChanged(bool isOn)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.invertMouseY = isOn;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE InvertMouseY = {isOn}");
        }
    }

    private void OnControllerHorizontalSpeedChanged(float value)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.controllerHorizontalSpeed = value;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE ControllerHorizontal = {value}");
        }
    }

    private void OnControllerVerticalSpeedChanged(float value)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.controllerVerticalSpeed = value;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE ControllerVertical = {value}");
        }
    }

    private void OnInvertControllerXChanged(bool isOn)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.invertControllerX = isOn;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE InvertControllerX = {isOn}");
        }
    }

    private void OnInvertControllerYChanged(bool isOn)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.invertControllerY = isOn;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE InvertControllerY = {isOn}");
        }
    }

    private void OnUseControllerAccelerationChanged(bool isOn)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.useControllerAcceleration = isOn;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE UseControllerAcceleration = {isOn}");
        }
    }

    private void OnControllerAccelerationChanged(float value)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.controllerAcceleration = value;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE ControllerAcceleration = {value}");
        }
    }

    private void OnControllerDecelerationChanged(float value)
    {
        if (isApplyingUi)
        {
            return;
        }

        cameraSetting.controllerDeceleration = value;
        saveLoader.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] CHANGE ControllerDeceleration = {value}");
        }
    }
}