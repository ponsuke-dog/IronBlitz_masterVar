using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// コンフィグメニュー全体を制御するクラス。
/// Audio / Mouse / Controller ページの切り替えを担当します。
/// 
/// 【仕様】
/// - カテゴリボタンは横配置で常に表示する
/// - カテゴリを切り替えたら、そのカテゴリの一番上の調整項目を選択する
/// - Q / E、ゲームパッドLB / RBでカテゴリを切り替える
/// - 各カテゴリ内Backボタンは使用しない
/// - 共通BackボタンでConfig画面から一回で抜ける
/// - 現在開いているカテゴリタブは、調整項目選択中でも強調表示し続ける
/// 
/// 【追加仕様】
/// - カテゴリが実際に切り替わった時、CategorySelect SEを再生する。
/// - カテゴリボタンクリック、Q/E、LB/RBのどちらでも同じSEを再生する。
/// - Config画面を開いた直後の初期表示ではSEを鳴らさない。
/// </summary>
public class ConfigMenuController : MonoBehaviour
{
    private enum ConfigPageType
    {
        Audio,
        Mouse,
        Controller
    }

    [Header("References")]
    [SerializeField] private MenuManager menuManager;

    [Header("Root")]
    [SerializeField] private GameObject rootObject;

    [Header("Page Roots")]
    [SerializeField] private GameObject audioPageRoot;
    [SerializeField] private GameObject mousePageRoot;
    [SerializeField] private GameObject controllerPageRoot;

    [Header("Buttons")]
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button mouseTabButton;
    [SerializeField] private Button controllerTabButton;
    [SerializeField] private Button backButton;

    [Header("Tab Sprites - Audio")]
    [SerializeField] private Sprite audioTabSelectedSprite;
    [SerializeField] private Sprite audioTabUnselectedSprite;

    [Header("Tab Sprites - Mouse")]
    [SerializeField] private Sprite mouseTabSelectedSprite;
    [SerializeField] private Sprite mouseTabUnselectedSprite;

    [Header("Tab Sprites - Controller")]
    [SerializeField] private Sprite controllerTabSelectedSprite;
    [SerializeField] private Sprite controllerTabUnselectedSprite;

    [Header("Focus")]
    [SerializeField] private ConfigFocusStateController focusStateController;

    [Header("SE")]
    [Tooltip("カテゴリ切り替え時にSEを鳴らします。")]
    [SerializeField] private bool playCategorySelectSe = true;

    [Tooltip("Config画面から戻る時にBack SEを鳴らします。")]
    [SerializeField] private bool playBackSe = true;

    private Action externalCloseRequest;

    private ConfigPageType currentPageType = ConfigPageType.Audio;

    private void Awake()
    {
        RegisterButtonEvents();
    }

    /// <summary>
    /// ボタンイベントを登録する。
    /// 二重登録を避けるため、一度RemoveしてからAddする。
    /// </summary>
    private void RegisterButtonEvents()
    {
        if (audioTabButton != null)
        {
            audioTabButton.onClick.RemoveListener(OnClickAudioCategory);
            audioTabButton.onClick.AddListener(OnClickAudioCategory);
        }

        if (mouseTabButton != null)
        {
            mouseTabButton.onClick.RemoveListener(OnClickMouseCategory);
            mouseTabButton.onClick.AddListener(OnClickMouseCategory);
        }

        if (controllerTabButton != null)
        {
            controllerTabButton.onClick.RemoveListener(OnClickControllerCategory);
            controllerTabButton.onClick.AddListener(OnClickControllerCategory);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnClickBack);
            backButton.onClick.AddListener(OnClickBack);
        }
    }

    /// <summary>
    /// タイトル画面など、MenuManager以外からConfigを開いた場合の閉じる処理を登録する。
    /// </summary>
    public void SetExternalCloseRequest(Action closeRequest)
    {
        externalCloseRequest = closeRequest;
    }

    /// <summary>
    /// 外部の閉じる処理を解除する。
    /// </summary>
    public void ClearExternalCloseRequest()
    {
        externalCloseRequest = null;
    }

    private void OnClickAudioCategory()
    {
        OnClickCategory(ConfigPageType.Audio);
    }

    private void OnClickMouseCategory()
    {
        OnClickCategory(ConfigPageType.Mouse);
    }

    private void OnClickControllerCategory()
    {
        OnClickCategory(ConfigPageType.Controller);
    }

    /// <summary>
    /// カテゴリボタンを押した時の処理。
    /// 別カテゴリへ切り替わった場合のみSEを鳴らす。
    /// </summary>
    private void OnClickCategory(ConfigPageType pageType)
    {
        bool changed = ChangePage(pageType);

        EnterCurrentCategoryItems();

        if (changed)
        {
            PlayCategorySelectSe();
        }
    }

    /// <summary>
    /// Config画面を開く。
    /// 開いた直後はAudioページから開始し、Audioの一番上の項目を選択する。
    /// 初期表示なのでカテゴリSEは鳴らさない。
    /// </summary>
    public void Open()
    {
        if (rootObject != null)
        {
            rootObject.SetActive(true);
        }

        ChangePage(ConfigPageType.Audio);
        EnterCurrentCategoryItems();
    }

    /// <summary>
    /// Config画面を閉じる。
    /// </summary>
    public void Close()
    {
        if (rootObject != null)
        {
            rootObject.SetActive(false);
        }
    }

    /// <summary>
    /// 入力からカテゴリを左右移動する。
    /// 
    /// direction:
    /// -1 = 前のカテゴリ
    ///  1 = 次のカテゴリ
    /// 
    /// Q / E、LB / RB入力から呼ばれる想定。
    /// </summary>
    public void MoveCategoryByDirection(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        ConfigPageType nextPageType = GetMovedPageType(direction);

        bool changed = ChangePage(nextPageType);

        EnterCurrentCategoryItems();

        if (changed)
        {
            PlayCategorySelectSe();
        }
    }

    /// <summary>
    /// 現在カテゴリから前後のカテゴリを取得する。
    /// Audio ←→ Mouse ←→ Controller をループさせる。
    /// </summary>
    private ConfigPageType GetMovedPageType(int direction)
    {
        int pageCount = Enum.GetValues(typeof(ConfigPageType)).Length;
        int currentIndex = (int)currentPageType;

        int nextIndex = currentIndex + direction;

        if (nextIndex < 0)
        {
            nextIndex = pageCount - 1;
        }
        else if (nextIndex >= pageCount)
        {
            nextIndex = 0;
        }

        return (ConfigPageType)nextIndex;
    }

    /// <summary>
    /// カテゴリページを切り替える。
    /// 
    /// 戻り値:
    /// true  = 表示カテゴリが実際に変わった
    /// false = 同じカテゴリだった
    /// </summary>
    private bool ChangePage(ConfigPageType pageType)
    {
        bool changed = currentPageType != pageType;

        currentPageType = pageType;

        if (audioPageRoot != null)
        {
            audioPageRoot.SetActive(pageType == ConfigPageType.Audio);
        }

        if (mousePageRoot != null)
        {
            mousePageRoot.SetActive(pageType == ConfigPageType.Mouse);
        }

        if (controllerPageRoot != null)
        {
            controllerPageRoot.SetActive(pageType == ConfigPageType.Controller);
        }

        if (focusStateController != null)
        {
            switch (pageType)
            {
                case ConfigPageType.Audio:
                    focusStateController.SelectCategory(ConfigFocusStateController.ConfigCategory.Audio);
                    break;

                case ConfigPageType.Mouse:
                    focusStateController.SelectCategory(ConfigFocusStateController.ConfigCategory.Mouse);
                    break;

                case ConfigPageType.Controller:
                    focusStateController.SelectCategory(ConfigFocusStateController.ConfigCategory.Controller);
                    break;
            }
        }

        // EventSystemの選択状態とは別に、現在カテゴリタブの通常表示Spriteを更新する。
        // これにより、スライダーやトグル選択中でもカテゴリタブの強調表示が残る。
        UpdateCategoryTabSprites();

        return changed;
    }

    /// <summary>
    /// 現在表示しているカテゴリの一番上の調整項目を選択する。
    /// </summary>
    private void EnterCurrentCategoryItems()
    {
        if (focusStateController == null)
        {
            return;
        }

        focusStateController.EnterCurrentCategoryItems();

        // スライダーへ選択が移った後も、現在カテゴリタブの強調表示を維持する。
        UpdateCategoryTabSprites();
    }

    /// <summary>
    /// 現在カテゴリに応じて、カテゴリタブの基本Spriteを更新する。
    /// </summary>
    private void UpdateCategoryTabSprites()
    {
        ApplyTabSprite(
            audioTabButton,
            currentPageType == ConfigPageType.Audio,
            audioTabSelectedSprite,
            audioTabUnselectedSprite);

        ApplyTabSprite(
            mouseTabButton,
            currentPageType == ConfigPageType.Mouse,
            mouseTabSelectedSprite,
            mouseTabUnselectedSprite);

        ApplyTabSprite(
            controllerTabButton,
            currentPageType == ConfigPageType.Controller,
            controllerTabSelectedSprite,
            controllerTabUnselectedSprite);
    }

    /// <summary>
    /// タブボタンの基本Spriteを反映する。
    /// </summary>
    private void ApplyTabSprite(
        Button targetButton,
        bool isCurrentCategory,
        Sprite selectedSprite,
        Sprite unselectedSprite)
    {
        if (targetButton == null)
        {
            return;
        }

        Image targetImage = targetButton.image;

        if (targetImage == null)
        {
            return;
        }

        Sprite nextSprite = isCurrentCategory
            ? selectedSprite
            : unselectedSprite;

        if (nextSprite == null)
        {
            return;
        }

        targetImage.overrideSprite = null;
        targetImage.sprite = nextSprite;
    }

    private void OnClickBack()
    {
        RequestBack();
    }

    /// <summary>
    /// Back入力を処理する。
    /// 今回の仕様では、項目編集中でもカテゴリ選択へ戻らず、
    /// Config画面から一回で抜ける。
    /// </summary>
    public bool HandleBack()
    {
        return RequestBack();
    }

    /// <summary>
    /// Config画面を閉じる。
    /// タイトルから開いている場合はタイトル側の閉じる処理を優先し、
    /// ゲーム中の場合はPauseMenuへ戻る。
    /// </summary>
    private bool RequestBack()
    {
        if (externalCloseRequest != null)
        {
            PlayBackSe();
            externalCloseRequest.Invoke();
            return true;
        }

        if (menuManager != null)
        {
            PlayBackSe();
            menuManager.OpenPauseMenu();
            return true;
        }

        return false;
    }

    /// <summary>
    /// カテゴリ切り替えSEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlayCategorySelectSe()
    {
        if (playCategorySelectSe == false)
        {
            return;
        }

        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.CategorySelect);
    }

    /// <summary>
    /// 戻るSEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlayBackSe()
    {
        if (playBackSe == false)
        {
            return;
        }

        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.Back);
    }
}