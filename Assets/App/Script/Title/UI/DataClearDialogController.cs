using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// セーブデータ削除確認ダイアログを制御します。
/// 「はい」でSaveSystem.ResetSave()を実行し、「いいえ」またはCancel入力で何もせずタイトルメニューへ戻します。
/// </summary>
public sealed class DataClearDialogController : MonoBehaviour
{
    [Header("Root Objects")]
    [SerializeField] private GameObject dialogRoot;
    [SerializeField] private GameObject mainMenuRoot;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string uiActionMapName = "UI";
    [SerializeField] private string cancelActionName = "Back";

    [Header("First Select")]
    [SerializeField] private Selectable firstSelectedButton;
    [SerializeField] private Selectable returnSelectedButton;

    private InputAction cancelAction;
    private bool isOpen;

    private void Awake()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap uiMap = inputActions.FindActionMap(uiActionMapName, false);

        if (uiMap != null)
        {
            cancelAction = uiMap.FindAction(cancelActionName, false);
        }
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            CancelDialog();
        }
    }

    /// <summary>
    /// 確認ダイアログを開きます。
    /// </summary>
    public void OpenDialog()
    {
        isOpen = true;

        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(false);
        }

        if (dialogRoot != null)
        {
            dialogRoot.SetActive(true);
        }

        SelectButton(firstSelectedButton);
    }

    /// <summary>
    /// 何もせず確認ダイアログを閉じます。
    /// </summary>
    public void CancelDialog()
    {
        CloseDialog();
    }

    /// <summary>
    /// セーブデータを初期化して確認ダイアログを閉じます。
    /// </summary>
    public void ExecuteDataClear()
    {
        SaveSystem.ResetSave();
        Debug.Log("セーブデータを初期化しました。");

        CloseDialog();
    }

    private void CloseDialog()
    {
        isOpen = false;

        if (dialogRoot != null)
        {
            dialogRoot.SetActive(false);
        }

        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(true);
        }

        SelectButton(returnSelectedButton);
    }

    private void SelectButton(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }
}