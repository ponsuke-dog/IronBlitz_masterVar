using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>
/// InputActionAsset内の有効なActionMapを監視して、マウスカーソル表示を制御するクラス。
///
/// 【仕様】
/// - Player ActionMapが有効な時はカーソル非表示
/// - UI / Scene ActionMapが有効な時は、マウス移動を検知したらカーソル表示
/// - UI中でもキーボード / コントローラー入力があったらカーソル非表示
/// - UI中で一定時間マウス操作が無ければカーソル非表示
/// - シーン遷移直後にも数フレーム再確認し、ステージ開始時のカーソル表示残りを防ぐ
/// </summary>
public class MouseCursorActionMapWatcher : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActionAsset;

    [Header("Gameplay Action Maps")]
    [SerializeField] private string[] gameplayActionMapNames = { "Player" };

    [Header("UI Action Maps")]
    [SerializeField] private string[] uiActionMapNames = { "UI", "Scene" };

    [Header("Mouse Cursor")]
    [SerializeField] private float mouseMoveThreshold = 0.1f;

    [Tooltip("UI中にマウス操作が無い場合、何秒後にカーソルを非表示にするか")]
    [SerializeField] private float autoHideDelay = 3.0f;

    [Header("Other Device Input")]
    [SerializeField] private bool hideOnKeyboardInput = true;
    [SerializeField] private bool hideOnGamepadInput = true;

    [Tooltip("コントローラーのスティック入力を検知するしきい値")]
    [SerializeField] private float gamepadStickThreshold = 0.25f;

    [Header("Gameplay Cursor Lock")]
    [SerializeField] private CursorLockMode gameplayLockMode = CursorLockMode.None;

    [Header("Scene Transition Fix")]
    [Tooltip("ONにするとシーン遷移後、数フレームにわたってカーソル状態を再適用します。")]
    [SerializeField] private bool applyAfterSceneLoaded = true;

    [Tooltip("シーン遷移後に何フレーム再確認するか。ステージ開始直後にカーソルが残る場合は大きめにします。")]
    [SerializeField] private int sceneLoadedReapplyFrameCount = 10;

    [Tooltip("LateUpdateでもPlayer状態を確認します。他のStart/Update処理でカーソル表示に戻される場合の対策です。")]
    [SerializeField] private bool applyGameplayCursorInLateUpdate = true;

    [Header("Life Time")]
    [Tooltip("シーンをまたいでこの制御を維持します。複数シーンで使う場合はON推奨です。")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    private static MouseCursorActionMapWatcher instance;

    private bool wasUiMode;
    private float lastMouseMoveTime;
    private string lastCursorState = string.Empty;
    private Coroutine sceneLoadedApplyCoroutine;

    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        lastMouseMoveTime = Time.unscaledTime;

        // 起動直後に一度評価します。
        RefreshCursorState("Awake");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 有効化直後にも評価します。
        RefreshCursorState("OnEnable");
    }

    private void Start()
    {
        // Start時点でもう一度評価します。
        RefreshCursorState("Start");

        // ActionMapの有効化が遅れるケースに備えて、数フレーム再評価します。
        StartSceneLoadedReapply("StartDelayed");
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (sceneLoadedApplyCoroutine != null)
        {
            StopCoroutine(sceneLoadedApplyCoroutine);
            sceneLoadedApplyCoroutine = null;
        }
    }

    private void Update()
    {
        RefreshCursorState("Update");
    }

    private void LateUpdate()
    {
        if (applyGameplayCursorInLateUpdate == false)
        {
            return;
        }

        // 他のスクリプトがUpdate中にCursor.visibleをtrueへ戻した場合でも、
        // Player ActionMapが有効ならLateUpdateで最後に非表示へ戻します。
        if (IsGameplayMode())
        {
            ApplyGameplayCursor("LateUpdate Gameplay");
        }
    }

    /// <summary>
    /// シーン読み込み完了時の処理。
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[MouseCursorActionMapWatcher] Scene Loaded: {scene.name}");
        }

        if (applyAfterSceneLoaded)
        {
            StartSceneLoadedReapply("SceneLoaded");
        }
    }

    /// <summary>
    /// シーン遷移直後の数フレーム、カーソル状態を再評価する。
    /// </summary>
    private void StartSceneLoadedReapply(string reason)
    {
        if (sceneLoadedApplyCoroutine != null)
        {
            StopCoroutine(sceneLoadedApplyCoroutine);
        }

        sceneLoadedApplyCoroutine = StartCoroutine(ReapplyCursorForFrames(reason));
    }

    private IEnumerator ReapplyCursorForFrames(string reason)
    {
        int frameCount = Mathf.Max(1, sceneLoadedReapplyFrameCount);

        for (int i = 0; i < frameCount; i++)
        {
            RefreshCursorState($"{reason} Frame {i + 1}");

            yield return null;
        }

        sceneLoadedApplyCoroutine = null;
    }

    /// <summary>
    /// 現在のActionMap状態に応じてカーソル状態を更新する。
    /// </summary>
    private void RefreshCursorState(string reason)
    {
        if (inputActionAsset == null)
        {
            return;
        }

        bool isGameplayMapActive = IsGameplayMode();
        bool isUiMapActive = IsUiMode();

        // Playerが有効な時はゲーム中扱いにして最優先で非表示
        if (isGameplayMapActive)
        {
            ApplyGameplayCursor(reason);
            wasUiMode = false;
            return;
        }

        // UI系ActionMapが有効な時はUI中扱い
        if (isUiMapActive)
        {
            ApplyUiCursor(wasUiMode == false, reason);
            wasUiMode = true;
            return;
        }

        wasUiMode = false;
    }

    /// <summary>
    /// ゲームプレイ用ActionMapが有効か確認する。
    /// </summary>
    private bool IsGameplayMode()
    {
        return IsAnyActionMapEnabled(gameplayActionMapNames);
    }

    /// <summary>
    /// UI用ActionMapが有効か確認する。
    /// </summary>
    private bool IsUiMode()
    {
        return IsAnyActionMapEnabled(uiActionMapNames);
    }

    /// <summary>
    /// 指定したActionMapのいずれかが有効か確認する。
    /// </summary>
    private bool IsAnyActionMapEnabled(string[] actionMapNames)
    {
        if (inputActionAsset == null || actionMapNames == null)
        {
            return false;
        }

        for (int i = 0; i < actionMapNames.Length; i++)
        {
            if (string.IsNullOrEmpty(actionMapNames[i]))
            {
                continue;
            }

            InputActionMap actionMap = inputActionAsset.FindActionMap(actionMapNames[i], false);

            if (actionMap != null && actionMap.enabled)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ゲーム中のカーソル状態を適用する。
    /// </summary>
    private void ApplyGameplayCursor(string reason)
    {
        HideCursor(gameplayLockMode, $"Gameplay: {reason}");
    }

    /// <summary>
    /// UI中のカーソル状態を適用する。
    /// </summary>
    private void ApplyUiCursor(bool justChangedToUi, string reason)
    {
        Cursor.lockState = CursorLockMode.None;

        if (justChangedToUi)
        {
            // UIへ切り替わった直後は、マウスを動かすまで非表示
            HideCursor(CursorLockMode.None, $"Changed To UI: {reason}");
            lastMouseMoveTime = Time.unscaledTime;
            return;
        }

        // キーボード / コントローラー入力があれば非表示
        if (WasOtherDeviceInputThisFrame())
        {
            HideCursor(CursorLockMode.None, "Keyboard Or Gamepad Input");
            return;
        }

        // マウス移動を検知したら表示
        if (WasMouseMovedThisFrame())
        {
            ShowCursor("Mouse Move");
            lastMouseMoveTime = Time.unscaledTime;
            return;
        }

        // 一定時間マウス移動が無ければ非表示
        if (Cursor.visible && Time.unscaledTime - lastMouseMoveTime >= autoHideDelay)
        {
            HideCursor(CursorLockMode.None, "Auto Hide");
        }
    }

    /// <summary>
    /// マウス移動を検知する。
    /// </summary>
    private bool WasMouseMovedThisFrame()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        return mouseDelta.sqrMagnitude > mouseMoveThreshold * mouseMoveThreshold;
    }

    /// <summary>
    /// マウス以外の入力を検知する。
    /// </summary>
    private bool WasOtherDeviceInputThisFrame()
    {
        if (hideOnKeyboardInput && WasKeyboardInputThisFrame())
        {
            return true;
        }

        if (hideOnGamepadInput && WasGamepadInputThisFrame())
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// キーボード入力を検知する。
    /// </summary>
    private bool WasKeyboardInputThisFrame()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.anyKey.wasPressedThisFrame;
    }

    /// <summary>
    /// コントローラー入力を検知する。
    /// ボタン、D-Pad、スティック入力を対象にする。
    /// </summary>
    private bool WasGamepadInputThisFrame()
    {
        if (Gamepad.all.Count <= 0)
        {
            return false;
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad gamepad = Gamepad.all[i];

            if (gamepad == null)
            {
                continue;
            }

            foreach (InputControl control in gamepad.allControls)
            {
                if (control is ButtonControl buttonControl && buttonControl.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (gamepad.leftStick.ReadValue().sqrMagnitude > gamepadStickThreshold * gamepadStickThreshold)
            {
                return true;
            }

            if (gamepad.rightStick.ReadValue().sqrMagnitude > gamepadStickThreshold * gamepadStickThreshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// カーソルを表示する。
    /// </summary>
    private void ShowCursor(string reason)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DebugCursorState("Show", reason);
    }

    /// <summary>
    /// カーソルを非表示にする。
    /// </summary>
    private void HideCursor(CursorLockMode lockMode, string reason)
    {
        Cursor.lockState = lockMode;
        Cursor.visible = false;

        DebugCursorState("Hide", reason);
    }

    /// <summary>
    /// カーソル状態のログを出す。
    /// </summary>
    private void DebugCursorState(string state, string reason)
    {
        if (enableDebugLog == false)
        {
            return;
        }

        string nextState = $"{state}:{reason}";

        if (lastCursorState == nextState)
        {
            return;
        }

        lastCursorState = nextState;

        Debug.Log($"[MouseCursorActionMapWatcher] Cursor {state}. Reason = {reason}");
    }
}