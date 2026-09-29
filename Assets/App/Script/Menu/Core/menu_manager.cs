using System;
using UnityEngine;

/// <summary>
/// メニュー全体の表示状態を管理するクラス。
/// PauseMenu と ConfigMenu の切り替えを担当する。
/// 
/// 【追加仕様】
/// - PauseMenu から ConfigMenu へ移動する時、Enter SEを再生する。
/// - InspectorのButton OnClick設定に依存せず、コード側でSEを鳴らす。
/// </summary>
public class MenuManager : MonoBehaviour
{
    public enum MenuState
    {
        Closed,
        Pause,
        Config
    }

    [Header("Menu Controllers")]
    [SerializeField] private PauseMenuController pauseMenuController;
    [SerializeField] private ConfigMenuController configMenuController;

    [Header("Canvas")]
    [SerializeField] private Canvas menuCanvas;

    [Header("SE")]
    [Tooltip("PauseMenuからConfigMenuへ移動する時にEnter SEを鳴らします。")]
    [SerializeField] private bool playEnterSeOnOpenConfig = true;

    private MenuState currentState = MenuState.Closed;

    /// <summary>
    /// 外部からBack入力を処理したい場合に使用します。
    /// タイトル画面からConfigを開いた時だけ登録します。
    /// </summary>
    private Func<bool> externalBackHandler;

    public event Action<MenuState> OnMenuStateChanged;

    public bool IsMenuOpen => currentState != MenuState.Closed;
    public MenuState CurrentState => currentState;

    private void Start()
    {
        if (menuCanvas != null)
        {
            menuCanvas.gameObject.SetActive(true);
        }

        CloseAllMenus();
    }

    public void SetExternalBackHandler(Func<bool> handler)
    {
        externalBackHandler = handler;
    }

    public void ClearExternalBackHandler()
    {
        externalBackHandler = null;
    }

    public void OpenPauseMenu()
    {
        SetState(MenuState.Pause);
    }

    public void OpenConfigMenu()
    {
        // PauseからConfigへ移動する時だけ、決定音としてEnter SEを鳴らす。
        if (currentState == MenuState.Pause)
        {
            PlayEnterSe();
        }

        SetState(MenuState.Config);
    }

    public void CloseAllMenus()
    {
        SetState(MenuState.Closed);
    }

    /// <summary>
    /// Back入力時の戻り先を現在のメニュー状態から判断します。
    /// </summary>
    public void HandleBack()
    {
        // タイトル画面など、外部側でBackを処理したい場合は最優先します。
        if (externalBackHandler != null && externalBackHandler.Invoke())
        {
            return;
        }

        switch (currentState)
        {
            case MenuState.Config:
                if (configMenuController != null && configMenuController.HandleBack())
                {
                    return;
                }

                OpenPauseMenu();
                break;

            case MenuState.Pause:
                CloseAllMenus();
                break;

            case MenuState.Closed:
                break;
        }
    }

    private void SetState(MenuState nextState)
    {
        currentState = nextState;

        switch (currentState)
        {
            case MenuState.Closed:
                if (pauseMenuController != null)
                {
                    pauseMenuController.Close();
                }

                if (configMenuController != null)
                {
                    configMenuController.Close();
                }

                Time.timeScale = 1.0f;
                break;

            case MenuState.Pause:
                if (pauseMenuController != null)
                {
                    pauseMenuController.Open();
                }

                if (configMenuController != null)
                {
                    configMenuController.Close();
                }

                Time.timeScale = 0.0f;
                break;

            case MenuState.Config:
                if (pauseMenuController != null)
                {
                    pauseMenuController.Close();
                }

                if (configMenuController != null)
                {
                    configMenuController.Open();
                }

                Time.timeScale = 0.0f;
                break;
        }

        OnMenuStateChanged?.Invoke(currentState);
    }

    /// <summary>
    /// 決定SEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlayEnterSe()
    {
        if (playEnterSeOnOpenConfig == false)
        {
            return;
        }

        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.Enter);
    }
}