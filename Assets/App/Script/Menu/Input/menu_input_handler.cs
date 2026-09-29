using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GameJam_InputSystem を使って、Menu入力とActionMap切り替えを管理するクラス。
/// 
/// メニューを開いた時:
/// Player ActionMap を無効化し、UI ActionMap を有効化する。
/// 
/// メニューを閉じた時:
/// UI ActionMap を無効化し、Player ActionMap を有効化する。
/// 
/// 【追加仕様】
/// - Config表示中のみ、UI/CategoryMoveでカテゴリを切り替える
/// - Keyboard: Q / E
/// - Gamepad: LB / RB
/// 
/// 【修正内容】
/// - Back入力時に即座にActionMapを切り替えない
/// - InputSystemUIInputModuleとの競合を避けるため、Back処理を1フレーム遅延する
/// </summary>
public class MenuInputHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private ConfigMenuController configMenuController;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Action Map Names")]
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string uiActionMapName = "UI";

    [Header("Action Names")]
    [SerializeField] private string menuActionName = "Menu";
    [SerializeField] private string backActionName = "Back";
    [SerializeField] private string categoryMoveActionName = "CategoryMove";

    private InputActionMap playerActionMap;
    private InputActionMap uiActionMap;

    private InputAction menuAction;
    private InputAction backAction;
    private InputAction categoryMoveAction;

    private Coroutine backDelayCoroutine;
    private bool isBackRequestQueued;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("[MenuInputHandler] Input Actions が未設定です。");
            enabled = false;
            return;
        }

        playerActionMap = inputActions.FindActionMap(playerActionMapName, true);
        uiActionMap = inputActions.FindActionMap(uiActionMapName, true);

        menuAction = playerActionMap.FindAction(menuActionName, true);
        backAction = uiActionMap.FindAction(backActionName, true);

        // CategoryMoveは追加Actionなので、未追加でも即エラー停止しないようにfalseで探す。
        categoryMoveAction = uiActionMap.FindAction(categoryMoveActionName, false);

        if (categoryMoveAction == null)
        {
            Debug.LogWarning(
                $"[MenuInputHandler] UI ActionMapに {categoryMoveActionName} が見つかりません。Q/E・LB/RBカテゴリ切替を使う場合はInput Actionsへ追加してください。");
        }

        // 初期状態ではPlayer入力から開始するため、UI側は後で状態に応じて切り替える。
        playerActionMap.Disable();
    }

    private void OnEnable()
    {
        if (menuAction != null)
        {
            menuAction.performed += OnMenuPerformed;
        }

        if (backAction != null)
        {
            backAction.performed += OnBackPerformed;
        }

        if (categoryMoveAction != null)
        {
            categoryMoveAction.performed += OnCategoryMovePerformed;
        }

        if (menuManager != null)
        {
            menuManager.OnMenuStateChanged += OnMenuStateChanged;
        }

        EnablePlayerInput();
    }

    private void OnDisable()
    {
        if (menuAction != null)
        {
            menuAction.performed -= OnMenuPerformed;
        }

        if (backAction != null)
        {
            backAction.performed -= OnBackPerformed;
        }

        if (categoryMoveAction != null)
        {
            categoryMoveAction.performed -= OnCategoryMovePerformed;
        }

        if (menuManager != null)
        {
            menuManager.OnMenuStateChanged -= OnMenuStateChanged;
        }

        if (backDelayCoroutine != null)
        {
            StopCoroutine(backDelayCoroutine);
            backDelayCoroutine = null;
        }

        isBackRequestQueued = false;
    }

    /// <summary>
    /// Player操作中のMenu入力。
    /// </summary>
    private void OnMenuPerformed(InputAction.CallbackContext context)
    {
        if (menuManager == null)
        {
            return;
        }

        if (menuManager.IsMenuOpen)
        {
            return;
        }

        menuManager.OpenPauseMenu();
    }

    /// <summary>
    /// UI操作中のBack入力。
    /// 
    /// InputSystemUIInputModuleも同じCancel入力を処理するため、
    /// このコールバック内で即座にActionMapを切り替えると、
    /// Input System側でIndexOutOfRangeExceptionが出る場合がある。
    /// 
    /// そのため、Back処理は1フレーム遅らせて実行する。
    /// </summary>
    private void OnBackPerformed(InputAction.CallbackContext context)
    {
        if (isBackRequestQueued)
        {
            return;
        }

        isBackRequestQueued = true;
        backDelayCoroutine = StartCoroutine(HandleBackAfterInputSystemCallback());
    }

    /// <summary>
    /// Input System側のperformed callback処理が終わった後にBack処理を行う。
    /// 
    /// UI ActionMapのDisableを入力コールバック中に実行しないことで、
    /// InputSystemUIInputModule側のCancel処理との競合を避ける。
    /// </summary>
    private IEnumerator HandleBackAfterInputSystemCallback()
    {
        yield return null;

        isBackRequestQueued = false;
        backDelayCoroutine = null;

        if (menuManager == null)
        {
            yield break;
        }

        menuManager.HandleBack();
    }

    /// <summary>
    /// Config表示中のカテゴリ移動入力。
    /// 
    /// CategoryMoveはAxis値として受け取る。
    /// -1なら前カテゴリ、+1なら次カテゴリ。
    /// </summary>
    private void OnCategoryMovePerformed(InputAction.CallbackContext context)
    {
        if (menuManager == null)
        {
            return;
        }

        if (menuManager.CurrentState != MenuManager.MenuState.Config)
        {
            return;
        }

        if (configMenuController == null)
        {
            return;
        }

        float moveValue = context.ReadValue<float>();

        if (Mathf.Abs(moveValue) < 0.5f)
        {
            return;
        }

        int direction = moveValue > 0.0f ? 1 : -1;
        configMenuController.MoveCategoryByDirection(direction);
    }

    /// <summary>
    /// MenuManagerの状態変更に合わせてActionMapを切り替える。
    /// Button操作 / キー操作 / マウス操作のどれでも同期される。
    /// </summary>
    private void OnMenuStateChanged(MenuManager.MenuState state)
    {
        if (state == MenuManager.MenuState.Closed)
        {
            EnablePlayerInput();
            return;
        }

        EnableUiInput();
    }

    /// <summary>
    /// Player操作用ActionMapを有効化する。
    /// </summary>
    private void EnablePlayerInput()
    {
        if (uiActionMap != null)
        {
            uiActionMap.Disable();
        }

        if (playerActionMap != null)
        {
            playerActionMap.Enable();
        }
    }

    /// <summary>
    /// UI操作用ActionMapを有効化する。
    /// </summary>
    private void EnableUiInput()
    {
        if (playerActionMap != null)
        {
            playerActionMap.Disable();
        }

        if (uiActionMap != null)
        {
            uiActionMap.Enable();
        }
    }
}