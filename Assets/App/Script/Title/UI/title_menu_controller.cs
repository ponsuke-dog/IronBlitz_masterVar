using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// タイトル画面の状態遷移を管理します。
/// PressAnyButton表示からタイトルメニュー表示への切り替えを担当します。
/// 
/// 【追加仕様】
/// - PressAnyButton入力を受け付けた瞬間に、MenuUiSeController経由でPressAny SEを再生する。
/// - GameStartボタン用に、連打防止とGameStart SE再生を行う公開メソッドを追加する。
/// </summary>
public sealed class TitleMenuController : MonoBehaviour
{
    private enum TitleState
    {
        PressAnyButton,
        MainMenu,
        Config
    }

    [Header("Root Objects")]
    [SerializeField] private GameObject pressAnyButtonRoot;
    [SerializeField] private GameObject titleMenuRoot;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string uiActionMapName = "UI";

    [Header("First Select")]
    [SerializeField] private Selectable firstSelectedButton;

    [Header("Behavior")]
    [SerializeField] private bool showPressAnyButtonOnStart = true;

    [Header("SE")]
    [Tooltip("PressAnyButton入力を受け付けた時にSEを鳴らします。")]
    [SerializeField] private bool playPressAnySe = true;

    [Tooltip("GameStartボタンを押した時にSEを鳴らします。")]
    [SerializeField] private bool playGameStartSe = true;

    [Header("Menu Input Guard")]
    [SerializeField] private CanvasGroup titleMenuCanvasGroup;
    [SerializeField] private float menuEnableDelay = 0.2f;

    [Header("Game Start Guard")]
    [Tooltip("ゲームスタートボタンの連打を防止します。")]
    [SerializeField] private bool useGameStartGuard = true;

    [Tooltip("ゲームスタート時に無効化したいボタンを設定します。Start / Config / Exitなどを入れてください。")]
    [SerializeField] private Button[] titleMenuButtonsToDisable;

    private TitleState currentState;
    private Selectable lastSelectedButton;
    private Coroutine selectRoutine;

    private bool isPressAnyAccepted;
    private bool isGameStartRequested;

    private void Awake()
    {
        RestoreUIActionMap();
        ChangeState(showPressAnyButtonOnStart ? TitleState.PressAnyButton : TitleState.MainMenu);
    }

    private void OnEnable()
    {
        RestoreUIActionMap();
    }

    private void Update()
    {
        if (currentState != TitleState.PressAnyButton)
        {
            return;
        }

        if (isPressAnyAccepted)
        {
            return;
        }

        if (WasAnyButtonPressed())
        {
            Debug.Log("PressAnyButton");

            isPressAnyAccepted = true;

            // PressAnyButton入力を受け付けた瞬間にSEを鳴らします。
            PlayPressAnySe();

            ShowTitleMenu(firstSelectedButton);
        }
    }

    private IEnumerator EnableMenuAfterInputRelease(Selectable selectable)
    {
        // メニュー表示直後はButtonがSubmit/Clickを受けないようにします。
        SetTitleMenuInteractable(false);

        EventSystem.current?.SetSelectedGameObject(null);

        // PressAnyButtonに使った入力が完全に解放されるまで待ちます。
        while (IsAnyInputPressed())
        {
            yield return null;
        }

        // 同一フレーム入力の残りを吸収します。
        yield return new WaitForSecondsRealtime(menuEnableDelay);

        RestoreUIActionMap();

        SetTitleMenuInteractable(true);

        if (selectable != null && EventSystem.current != null)
        {
            Canvas.ForceUpdateCanvases();

            EventSystem.current.SetSelectedGameObject(null);
            yield return null;

            selectable.Select();
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        selectRoutine = null;
    }

    private void SetTitleMenuInteractable(bool isInteractable)
    {
        if (titleMenuCanvasGroup == null)
        {
            return;
        }

        titleMenuCanvasGroup.interactable = isInteractable;
        titleMenuCanvasGroup.blocksRaycasts = isInteractable;
        titleMenuCanvasGroup.alpha = 1.0f;
    }

    private bool IsAnyInputPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
        {
            return true;
        }

        if (Mouse.current != null &&
            (Mouse.current.leftButton.isPressed ||
             Mouse.current.rightButton.isPressed ||
             Mouse.current.middleButton.isPressed))
        {
            return true;
        }

        if (Gamepad.current != null &&
            (Gamepad.current.buttonSouth.isPressed ||
             Gamepad.current.buttonEast.isPressed ||
             Gamepad.current.buttonWest.isPressed ||
             Gamepad.current.buttonNorth.isPressed ||
             Gamepad.current.startButton.isPressed ||
             Gamepad.current.selectButton.isPressed ||
             Gamepad.current.leftShoulder.isPressed ||
             Gamepad.current.rightShoulder.isPressed))
        {
            return true;
        }

        return false;
    }

    public void ShowTitleMenu(Selectable selectTarget = null)
    {
        RestoreUIActionMap();

        ChangeState(TitleState.MainMenu);

        Selectable target = selectTarget != null ? selectTarget : lastSelectedButton;

        if (target == null)
        {
            target = firstSelectedButton;
        }

        if (selectRoutine != null)
        {
            StopCoroutine(selectRoutine);
        }

        selectRoutine = StartCoroutine(EnableMenuAfterInputRelease(target));
    }

    public void HideTitleMenuForConfig()
    {
        RestoreUIActionMap();

        lastSelectedButton = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>()
            : null;

        ClearCurrentSelection();
        ChangeState(TitleState.Config);
    }

    /// <summary>
    /// タイトルUIで使用するUI ActionMapを有効化します。
    /// Player用ActionMapへ戻されていた場合の復帰用です。
    /// </summary>
    public void RestoreUIActionMap()
    {
        if (inputActions == null)
        {
            return;
        }

        foreach (InputActionMap map in inputActions.actionMaps)
        {
            map.Disable();
        }

        InputActionMap uiMap = inputActions.FindActionMap(uiActionMapName, false);

        if (uiMap != null)
        {
            uiMap.Enable();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.enabled = true;
            EventSystem.current.sendNavigationEvents = true;
        }
    }

    private void ChangeState(TitleState nextState)
    {
        currentState = nextState;

        if (pressAnyButtonRoot != null)
        {
            pressAnyButtonRoot.SetActive(currentState == TitleState.PressAnyButton);
        }

        if (titleMenuRoot != null)
        {
            titleMenuRoot.SetActive(currentState == TitleState.MainMenu);
        }
    }

    private void RequestSelectButton(Selectable selectable)
    {
        if (selectRoutine != null)
        {
            StopCoroutine(selectRoutine);
        }

        selectRoutine = StartCoroutine(SelectButtonNextFrame(selectable));
    }

    private IEnumerator SelectButtonNextFrame(Selectable selectable)
    {
        // PressAnyButtonで押した入力がButton決定に流れないように数フレーム待ちます。
        yield return null;
        yield return null;

        // キーボード・マウス・ゲームパッドの入力が離されるまで待ちます。
        while (IsAnyInputPressed())
        {
            yield return null;
        }

        RestoreUIActionMap();

        if (selectable == null || EventSystem.current == null)
        {
            yield break;
        }

        Canvas.ForceUpdateCanvases();

        EventSystem.current.SetSelectedGameObject(null);

        yield return null;

        if (selectable.gameObject.activeInHierarchy && selectable.interactable)
        {
            selectable.Select();
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        selectRoutine = null;
    }

    private void ClearCurrentSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private bool WasAnyButtonPressed()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame ||
             Mouse.current.rightButton.wasPressedThisFrame ||
             Mouse.current.middleButton.wasPressedThisFrame))
        {
            return true;
        }

        if (Gamepad.current != null &&
            (Gamepad.current.buttonSouth.wasPressedThisFrame ||
             Gamepad.current.buttonEast.wasPressedThisFrame ||
             Gamepad.current.buttonWest.wasPressedThisFrame ||
             Gamepad.current.buttonNorth.wasPressedThisFrame ||
             Gamepad.current.startButton.wasPressedThisFrame ||
             Gamepad.current.selectButton.wasPressedThisFrame))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// PressAnyButton用SEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlayPressAnySe()
    {
        if (playPressAnySe == false)
        {
            return;
        }

        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.PressAny);
    }

    /// <summary>
    /// GameStartボタン用。
    /// ButtonのOnClickの一番上に登録してください。
    /// 
    /// 【役割】
    /// - GameStart SEを1回だけ鳴らす
    /// - タイトルメニューのボタンを無効化して連打を防ぐ
    /// - その後、OnClickに登録されている本来のゲーム開始処理を実行させる
    /// </summary>
    public void LockTitleMenuAndPlayGameStartSe()
    {
        if (useGameStartGuard && isGameStartRequested)
        {
            return;
        }

        isGameStartRequested = true;

        DisableTitleMenuButtons();

        PlayGameStartSe();
    }

    /// <summary>
    /// GameStart用SEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlayGameStartSe()
    {
        if (playGameStartSe == false)
        {
            return;
        }

        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.GameStart);
    }

    /// <summary>
    /// タイトルメニュー内のボタンを無効化して、連打やSubmit連続入力を防ぐ。
    /// </summary>
    private void DisableTitleMenuButtons()
    {
        if (titleMenuCanvasGroup != null)
        {
            titleMenuCanvasGroup.interactable = false;
            titleMenuCanvasGroup.blocksRaycasts = false;
        }

        if (titleMenuButtonsToDisable == null)
        {
            return;
        }

        for (int i = 0; i < titleMenuButtonsToDisable.Length; i++)
        {
            if (titleMenuButtonsToDisable[i] != null)
            {
                titleMenuButtonsToDisable[i].interactable = false;
            }
        }
    }
}