using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// タイトルUIと既存Configメニューをつなぐ橋渡しクラスです。
/// Configを閉じた後、PauseMenuではなくタイトルUIへ戻します。
/// </summary>
public sealed class TitleConfigMenuBridge : MonoBehaviour
{
    private enum BridgeState
    {
        Title,
        OpeningConfig,
        ConfigReady,
        ClosingConfig
    }

    [Header("Title")]
    [SerializeField] private TitleMenuController titleMenuController;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string uiActionMapName = "UI";

    [Header("Menu System")]
    [SerializeField] private GameObject menuSystemRoot;
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private ConfigMenuController configMenuController;

    [Header("Events")]
    [SerializeField] private UnityEvent onOpenConfig;
    [SerializeField] private UnityEvent onCloseConfig;

    private BridgeState currentState = BridgeState.Title;
    private Coroutine openRoutine;
    private Coroutine closeRoutine;

    public void OpenConfigFromTitle()
    {
        if (currentState != BridgeState.Title)
        {
            return;
        }

        RestoreUIActionMap();

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
        }

        openRoutine = StartCoroutine(OpenConfigFromTitleRoutine());
    }

    private IEnumerator OpenConfigFromTitleRoutine()
    {
        currentState = BridgeState.OpeningConfig;

        titleMenuController?.HideTitleMenuForConfig();

        if (menuSystemRoot == null || menuManager == null || configMenuController == null)
        {
            Debug.LogWarning("TitleConfigMenuBridge の参照設定が不足しています。");
            currentState = BridgeState.Title;
            yield break;
        }

        ClearSelection();
        RestoreUIActionMap();

        menuSystemRoot.SetActive(true);

        yield return null;

        RestoreUIActionMap();

        configMenuController.SetExternalCloseRequest(CloseConfigToTitle);
        menuManager.OpenConfigMenu();

        yield return null;

        RestoreUIActionMap();

        currentState = BridgeState.ConfigReady;
        onOpenConfig?.Invoke();

        openRoutine = null;
    }

    public void CloseConfigToTitle()
    {
        if (currentState != BridgeState.ConfigReady)
        {
            return;
        }

        RestoreUIActionMap();

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
        }

        closeRoutine = StartCoroutine(CloseConfigToTitleRoutine());
    }

    private IEnumerator CloseConfigToTitleRoutine()
    {
        currentState = BridgeState.ClosingConfig;

        ClearSelection();
        RestoreUIActionMap();

        if (configMenuController != null)
        {
            configMenuController.ClearExternalCloseRequest();
        }

        if (menuManager != null)
        {
            menuManager.CloseAllMenus();
        }

        yield return null;

        RestoreUIActionMap();

        if (menuSystemRoot != null)
        {
            menuSystemRoot.SetActive(false);
        }

        yield return null;

        RestoreUIActionMap();

        onCloseConfig?.Invoke();

        if (titleMenuController != null)
        {
            titleMenuController.RestoreUIActionMap();
            titleMenuController.ShowTitleMenu();
        }

        currentState = BridgeState.Title;
        closeRoutine = null;
    }

    private void RestoreUIActionMap()
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

    private void ClearSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}