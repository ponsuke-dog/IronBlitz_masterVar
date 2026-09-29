using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Config画面の状態を一元管理するクラス。
/// Category選択状態と項目調整状態を切り替え、
/// マウス / キーボード / ゲームパッド操作で状態が崩れないようにします。
/// 
/// 【現在の仕様】
/// - カテゴリボタンは常に操作可能にする
/// - カテゴリ切り替え後は、そのカテゴリの一番上の項目を選択する
/// - 各カテゴリ内Backボタンは使用しない
/// - 各カテゴリ内Resetボタンは使用しない
/// - 共通BackボタンでConfig画面から一回で抜ける
/// </summary>
public class ConfigFocusStateController : MonoBehaviour
{
    public enum ConfigCategory
    {
        Audio,
        Mouse,
        Controller
    }

    [Header("Category Area")]
    [SerializeField] private CanvasGroup categoryCanvasGroup;

    [Header("Item Areas")]
    [SerializeField] private CanvasGroup audioPageCanvasGroup;
    [SerializeField] private CanvasGroup mousePageCanvasGroup;
    [SerializeField] private CanvasGroup controllerPageCanvasGroup;

    [Header("Category Buttons")]
    [SerializeField] private Selectable audioTabButton;
    [SerializeField] private Selectable mouseTabButton;
    [SerializeField] private Selectable controllerTabButton;
    [SerializeField] private Selectable backButton;

    [Header("Audio Items")]
    [SerializeField] private Selectable masterSlider;
    [SerializeField] private Selectable bgmSlider;
    [SerializeField] private Selectable seSlider;
    [SerializeField] private Selectable uiSlider;

    [Header("Mouse Items")]
    [SerializeField] private Selectable mouseSensitivitySlider;

    [Header("Controller Items")]
    [SerializeField] private Selectable controllerSensitivitySlider;

    private ConfigCategory currentCategory;
    private bool isEditingItem;

    public bool IsEditingItem => isEditingItem;

    /// <summary>
    /// カテゴリを選択状態にします。
    /// </summary>
    public void SelectCategory(ConfigCategory category)
    {
        currentCategory = category;
        isEditingItem = false;

        // 上部カテゴリボタンと共通Backボタンは常に使えるようにします。
        SetCategoryAreaEnabled(true);

        // 一旦すべての項目エリアを操作不可にします。
        SetAllItemAreasEnabled(false);

        Select(GetCategorySelectable(category));
    }

    /// <summary>
    /// 現在カテゴリの一番上の調整項目を選択します。
    /// </summary>
    public void EnterCurrentCategoryItems()
    {
        isEditingItem = true;

        // 新仕様ではAudio / Mouse / Controller / Backを常に操作可能にします。
        SetCategoryAreaEnabled(true);

        SetAllItemAreasEnabled(false);

        switch (currentCategory)
        {
            case ConfigCategory.Audio:
                SetCanvasGroupEnabled(audioPageCanvasGroup, true);
                SetAudioItemsInteractable(true);
                Select(masterSlider);
                break;

            case ConfigCategory.Mouse:
                SetCanvasGroupEnabled(mousePageCanvasGroup, true);
                SetMouseItemsInteractable(true);
                Select(mouseSensitivitySlider);
                break;

            case ConfigCategory.Controller:
                SetCanvasGroupEnabled(controllerPageCanvasGroup, true);
                SetControllerItemsInteractable(true);
                Select(controllerSensitivitySlider);
                break;
        }
    }

    /// <summary>
    /// 旧仕様用のBack処理。
    /// 現在はConfigMenuController側でBack一回退出にしているため、基本的には使いません。
    /// </summary>
    public bool HandleBack()
    {
        return false;
    }

    private Selectable GetCategorySelectable(ConfigCategory category)
    {
        switch (category)
        {
            case ConfigCategory.Audio:
                return audioTabButton;

            case ConfigCategory.Mouse:
                return mouseTabButton;

            case ConfigCategory.Controller:
                return controllerTabButton;

            default:
                return audioTabButton;
        }
    }

    private void SetCategoryAreaEnabled(bool enabled)
    {
        SetCanvasGroupEnabled(categoryCanvasGroup, enabled);

        SetSelectableInteractable(audioTabButton, enabled);
        SetSelectableInteractable(mouseTabButton, enabled);
        SetSelectableInteractable(controllerTabButton, enabled);
        SetSelectableInteractable(backButton, enabled);
    }

    private void SetAllItemAreasEnabled(bool enabled)
    {
        SetCanvasGroupEnabled(audioPageCanvasGroup, enabled);
        SetCanvasGroupEnabled(mousePageCanvasGroup, enabled);
        SetCanvasGroupEnabled(controllerPageCanvasGroup, enabled);

        SetAudioItemsInteractable(enabled);
        SetMouseItemsInteractable(enabled);
        SetControllerItemsInteractable(enabled);
    }

    private void SetAudioItemsInteractable(bool enabled)
    {
        SetSelectableInteractable(masterSlider, enabled);
        SetSelectableInteractable(bgmSlider, enabled);
        SetSelectableInteractable(seSlider, enabled);
        SetSelectableInteractable(uiSlider, enabled);

    }

    private void SetMouseItemsInteractable(bool enabled)
    {
        SetSelectableInteractable(mouseSensitivitySlider, enabled);
    }

    private void SetControllerItemsInteractable(bool enabled)
    {
        SetSelectableInteractable(controllerSensitivitySlider, enabled);
    }

    private void SetSelectableInteractable(Selectable selectable, bool enabled)
    {
        if (selectable == null)
        {
            return;
        }

        selectable.interactable = enabled;
    }

    private void SetCanvasGroupEnabled(CanvasGroup canvasGroup, bool enabled)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void Select(Selectable selectable)
    {
        if (selectable == null)
        {
            return;
        }

        if (EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }
}