using UnityEngine;

/// <summary>
/// カメラコンフィグの実行中キャッシュとPlayerPrefs保存を管理する静的クラス。
/// 
/// 【目的】
/// - ビルド後のシーン遷移でも設定値を安定して保持する
/// - ScriptableObjectの実行時変更に依存しない
/// - PlayerPrefsを正式な保存元にする
/// 
/// 【重要】
/// - CameraControlSettingsは保存元ではなく、実行中に値を入れる受け皿として扱う
/// - タイトル→ゲーム遷移時も、このRuntimeStore経由で値を再反映する
/// </summary>
public static class CameraConfigRuntimeStore
{
    private const string KeyMouseHorizontalSensitivity = "CameraConfig.MouseHorizontalSensitivity";
    private const string KeyMouseVerticalSensitivity = "CameraConfig.MouseVerticalSensitivity";

    private const string KeyInvertMouseX = "CameraConfig.InvertMouseX";
    private const string KeyInvertMouseY = "CameraConfig.InvertMouseY";

    private const string KeyControllerHorizontalSpeed = "CameraConfig.ControllerHorizontalSpeed";
    private const string KeyControllerVerticalSpeed = "CameraConfig.ControllerVerticalSpeed";

    private const string KeyInvertControllerX = "CameraConfig.InvertControllerX";
    private const string KeyInvertControllerY = "CameraConfig.InvertControllerY";

    private const string KeyUseControllerAcceleration = "CameraConfig.UseControllerAcceleration";
    private const string KeyControllerAcceleration = "CameraConfig.ControllerAcceleration";
    private const string KeyControllerDeceleration = "CameraConfig.ControllerDeceleration";

    private static bool isLoaded;

    private static float mouseHorizontalSensitivity;
    private static float mouseVerticalSensitivity;

    private static bool invertMouseX;
    private static bool invertMouseY;

    private static float controllerHorizontalSpeed;
    private static float controllerVerticalSpeed;

    private static bool invertControllerX;
    private static bool invertControllerY;

    private static bool useControllerAcceleration;
    private static float controllerAcceleration;
    private static float controllerDeceleration;

    /// <summary>
    /// Unityの実行開始時にstatic状態を初期化する。
    /// Editor / Buildの差を減らすために明示的に初期化する。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        isLoaded = false;
    }

    /// <summary>
    /// PlayerPrefsから設定値を読み込む。
    /// すでに読み込み済みの場合は何もしない。
    /// </summary>
    public static void Load(CameraControlSettings defaultSettings, bool enableDebugLog)
    {
        if (isLoaded)
        {
            return;
        }

        if (defaultSettings == null)
        {
            Debug.LogError("[CameraConfig] defaultSettingsがnullのためLoadできません。");
            return;
        }

        mouseHorizontalSensitivity = PlayerPrefs.GetFloat(
            KeyMouseHorizontalSensitivity,
            defaultSettings.mouseHorizontalSensitivity);

        mouseVerticalSensitivity = PlayerPrefs.GetFloat(
            KeyMouseVerticalSensitivity,
            defaultSettings.mouseVerticalSensitivity);

        invertMouseX = PlayerPrefs.GetInt(
            KeyInvertMouseX,
            BoolToInt(defaultSettings.invertMouseX)) == 1;

        invertMouseY = PlayerPrefs.GetInt(
            KeyInvertMouseY,
            BoolToInt(defaultSettings.invertMouseY)) == 1;

        controllerHorizontalSpeed = PlayerPrefs.GetFloat(
            KeyControllerHorizontalSpeed,
            defaultSettings.controllerHorizontalSpeed);

        controllerVerticalSpeed = PlayerPrefs.GetFloat(
            KeyControllerVerticalSpeed,
            defaultSettings.controllerVerticalSpeed);

        invertControllerX = PlayerPrefs.GetInt(
            KeyInvertControllerX,
            BoolToInt(defaultSettings.invertControllerX)) == 1;

        invertControllerY = PlayerPrefs.GetInt(
            KeyInvertControllerY,
            BoolToInt(defaultSettings.invertControllerY)) == 1;

        useControllerAcceleration = PlayerPrefs.GetInt(
            KeyUseControllerAcceleration,
            BoolToInt(defaultSettings.useControllerAcceleration)) == 1;

        controllerAcceleration = PlayerPrefs.GetFloat(
            KeyControllerAcceleration,
            defaultSettings.controllerAcceleration);

        controllerDeceleration = PlayerPrefs.GetFloat(
            KeyControllerDeceleration,
            defaultSettings.controllerDeceleration);

        isLoaded = true;

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] RUNTIME LOAD MouseHorizontal = {mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// PlayerPrefsから強制的に再読み込みする。
    /// ビルド後の確認や、別システムがPlayerPrefsを書き換えていないか確認する時に使う。
    /// </summary>
    public static void ForceReload(CameraControlSettings defaultSettings, bool enableDebugLog)
    {
        isLoaded = false;
        Load(defaultSettings, enableDebugLog);
    }

    /// <summary>
    /// RuntimeStoreの値をCameraControlSettingsへ反映する。
    /// </summary>
    public static void ApplyTo(CameraControlSettings targetSettings, bool enableDebugLog)
    {
        if (targetSettings == null)
        {
            Debug.LogError("[CameraConfig] targetSettingsがnullのためApplyできません。");
            return;
        }

        targetSettings.mouseHorizontalSensitivity = mouseHorizontalSensitivity;
        targetSettings.mouseVerticalSensitivity = mouseVerticalSensitivity;

        targetSettings.invertMouseX = invertMouseX;
        targetSettings.invertMouseY = invertMouseY;

        targetSettings.controllerHorizontalSpeed = controllerHorizontalSpeed;
        targetSettings.controllerVerticalSpeed = controllerVerticalSpeed;

        targetSettings.invertControllerX = invertControllerX;
        targetSettings.invertControllerY = invertControllerY;

        targetSettings.useControllerAcceleration = useControllerAcceleration;
        targetSettings.controllerAcceleration = controllerAcceleration;
        targetSettings.controllerDeceleration = controllerDeceleration;

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] RUNTIME APPLY MouseHorizontal = {targetSettings.mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// CameraControlSettingsの現在値をRuntimeStoreへ取り込む。
    /// </summary>
    public static void CaptureFrom(CameraControlSettings sourceSettings, bool enableDebugLog)
    {
        if (sourceSettings == null)
        {
            Debug.LogError("[CameraConfig] sourceSettingsがnullのためCaptureできません。");
            return;
        }

        mouseHorizontalSensitivity = sourceSettings.mouseHorizontalSensitivity;
        mouseVerticalSensitivity = sourceSettings.mouseVerticalSensitivity;

        invertMouseX = sourceSettings.invertMouseX;
        invertMouseY = sourceSettings.invertMouseY;

        controllerHorizontalSpeed = sourceSettings.controllerHorizontalSpeed;
        controllerVerticalSpeed = sourceSettings.controllerVerticalSpeed;

        invertControllerX = sourceSettings.invertControllerX;
        invertControllerY = sourceSettings.invertControllerY;

        useControllerAcceleration = sourceSettings.useControllerAcceleration;
        controllerAcceleration = sourceSettings.controllerAcceleration;
        controllerDeceleration = sourceSettings.controllerDeceleration;

        isLoaded = true;

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] RUNTIME CAPTURE MouseHorizontal = {mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// RuntimeStoreの値をPlayerPrefsへ保存する。
    /// </summary>
    public static void Save(bool enableDebugLog)
    {
        PlayerPrefs.SetFloat(KeyMouseHorizontalSensitivity, mouseHorizontalSensitivity);
        PlayerPrefs.SetFloat(KeyMouseVerticalSensitivity, mouseVerticalSensitivity);

        PlayerPrefs.SetInt(KeyInvertMouseX, BoolToInt(invertMouseX));
        PlayerPrefs.SetInt(KeyInvertMouseY, BoolToInt(invertMouseY));

        PlayerPrefs.SetFloat(KeyControllerHorizontalSpeed, controllerHorizontalSpeed);
        PlayerPrefs.SetFloat(KeyControllerVerticalSpeed, controllerVerticalSpeed);

        PlayerPrefs.SetInt(KeyInvertControllerX, BoolToInt(invertControllerX));
        PlayerPrefs.SetInt(KeyInvertControllerY, BoolToInt(invertControllerY));

        PlayerPrefs.SetInt(KeyUseControllerAcceleration, BoolToInt(useControllerAcceleration));
        PlayerPrefs.SetFloat(KeyControllerAcceleration, controllerAcceleration);
        PlayerPrefs.SetFloat(KeyControllerDeceleration, controllerDeceleration);

        // ビルド後でも即時保存されるように明示的にSaveする。
        PlayerPrefs.Save();

        if (enableDebugLog)
        {
            Debug.Log($"[CameraConfig] RUNTIME SAVE MouseHorizontal = {mouseHorizontalSensitivity}");
        }
    }

    /// <summary>
    /// CameraConfig関連の保存データを削除する。
    /// </summary>
    public static void DeleteSaveData(bool enableDebugLog)
    {
        PlayerPrefs.DeleteKey(KeyMouseHorizontalSensitivity);
        PlayerPrefs.DeleteKey(KeyMouseVerticalSensitivity);

        PlayerPrefs.DeleteKey(KeyInvertMouseX);
        PlayerPrefs.DeleteKey(KeyInvertMouseY);

        PlayerPrefs.DeleteKey(KeyControllerHorizontalSpeed);
        PlayerPrefs.DeleteKey(KeyControllerVerticalSpeed);

        PlayerPrefs.DeleteKey(KeyInvertControllerX);
        PlayerPrefs.DeleteKey(KeyInvertControllerY);

        PlayerPrefs.DeleteKey(KeyUseControllerAcceleration);
        PlayerPrefs.DeleteKey(KeyControllerAcceleration);
        PlayerPrefs.DeleteKey(KeyControllerDeceleration);

        PlayerPrefs.Save();

        isLoaded = false;

        if (enableDebugLog)
        {
            Debug.Log("[CameraConfig] RUNTIME DELETE SAVE DATA");
        }
    }

    private static int BoolToInt(bool value)
    {
        return value ? 1 : 0;
    }
}
