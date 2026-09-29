using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ポーズ画面のガイドパネルを制御するクラス。
/// 
/// 【役割】
/// - 複数のガイドページを切り替える
/// - 左右矢印ボタンでページを切り替える
/// - キーボード / コントローラーの左右入力でページを切り替える
/// - 一定時間経過で自動的に次のページへ切り替える
/// - ページ切り替え時にフェード + スライドアニメーションを行う
/// 
/// 【追加仕様】
/// - 手動操作によるページ切り替え時のみ、MenuUiSeController経由でページ切り替えSEを再生する
/// - 自動切り替え時はデフォルトではSEを鳴らさない
/// 
/// 【注意】
/// - ポーズ中はTime.timeScaleが0になるため、時間経過はTime.unscaledDeltaTimeで処理する
/// - 入力ActionはInputActionReferenceで直接指定する
/// - DOTween等の外部ライブラリは使わず、Coroutineで制御する
/// </summary>
public class PauseGuidePanelController : MonoBehaviour
{
    [Header("ガイドページ")]
    [SerializeField] private List<GameObject> guidePages = new List<GameObject>();

    [Header("矢印ボタン")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Input System")]
    [SerializeField] private InputActionReference guidePageMoveAction;

    [Header("自動切り替え")]
    [SerializeField] private bool autoSwitchEnabled = true;
    [SerializeField] private float autoSwitchInterval = 5.0f;

    [Header("SE")]
    [Tooltip("矢印クリック、キー入力、コントローラー入力など、手動ページ切り替え時にSEを鳴らします。")]
    [SerializeField] private bool playSeOnManualPageMove = true;

    [Tooltip("自動ページ切り替え時にもSEを鳴らす場合はONにします。通常はOFF推奨です。")]
    [SerializeField] private bool playSeOnAutoSwitch = false;

    [Header("入力設定")]
    [SerializeField] private float inputThreshold = 0.5f;
    [SerializeField] private float inputCooldown = 0.25f;

    [Header("ページ切り替えアニメーション")]
    [SerializeField] private bool usePageAnimation = true;
    [SerializeField] private float transitionDuration = 0.25f;
    [SerializeField] private float slideDistance = 80.0f;

    [Header("開いた時の設定")]
    [SerializeField] private bool resetPageOnEnable = true;

    [Header("デバッグ")]
    [SerializeField] private bool enableDebugLog = false;

    private int currentPageIndex;
    private float autoSwitchTimer;
    private float inputCooldownTimer;

    private bool wasGuidePageMoveActionEnabled;
    private bool isInputHolding;
    private bool isAnimating;

    private Coroutine transitionCoroutine;

    private void Awake()
    {
        RegisterButtonEvents();
    }

    private void OnEnable()
    {
        if (resetPageOnEnable)
        {
            currentPageIndex = 0;
        }

        autoSwitchTimer = 0.0f;
        inputCooldownTimer = 0.0f;
        isInputHolding = false;
        isAnimating = false;

        EnableGuidePageMoveAction();
        ApplyPageImmediately();
    }

    private void OnDisable()
    {
        DisableGuidePageMoveActionIfNeeded();

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        isAnimating = false;
    }

    private void OnDestroy()
    {
        UnregisterButtonEvents();
    }

    private void Update()
    {
        UpdateInputCooldown();
        HandleGuidePageMoveInput();
        HandleAutoSwitch();
    }

    /// <summary>
    /// 左右矢印ボタンのイベントを登録する。
    /// 手動クリック扱いにしたいため、SE付きのメソッドを登録する。
    /// </summary>
    private void RegisterButtonEvents()
    {
        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(ShowPreviousPage);
            previousButton.onClick.RemoveListener(ShowPreviousPageByUser);
            previousButton.onClick.AddListener(ShowPreviousPageByUser);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextPage);
            nextButton.onClick.RemoveListener(ShowNextPageByUser);
            nextButton.onClick.AddListener(ShowNextPageByUser);
        }
    }

    /// <summary>
    /// 左右矢印ボタンのイベントを解除する。
    /// </summary>
    private void UnregisterButtonEvents()
    {
        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(ShowPreviousPage);
            previousButton.onClick.RemoveListener(ShowPreviousPageByUser);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextPage);
            nextButton.onClick.RemoveListener(ShowNextPageByUser);
        }
    }

    /// <summary>
    /// ガイドページ移動Actionを有効化する。
    /// </summary>
    private void EnableGuidePageMoveAction()
    {
        if (guidePageMoveAction == null || guidePageMoveAction.action == null)
        {
            Debug.LogWarning("[PauseGuide] Guide Page Move Action が未設定です。キー / コントローラー入力でのページ切替は無効です。");
            return;
        }

        wasGuidePageMoveActionEnabled = guidePageMoveAction.action.enabled;

        if (!guidePageMoveAction.action.enabled)
        {
            guidePageMoveAction.action.Enable();
        }

        if (enableDebugLog)
        {
            Debug.Log("[PauseGuide] GuidePageMove Action Enabled");
        }
    }

    /// <summary>
    /// このクラスが有効化したActionだけを無効化する。
    /// </summary>
    private void DisableGuidePageMoveActionIfNeeded()
    {
        if (guidePageMoveAction == null || guidePageMoveAction.action == null)
        {
            return;
        }

        if (!wasGuidePageMoveActionEnabled)
        {
            guidePageMoveAction.action.Disable();
        }
    }

    /// <summary>
    /// 入力クールダウンを更新する。
    /// </summary>
    private void UpdateInputCooldown()
    {
        if (inputCooldownTimer <= 0.0f)
        {
            return;
        }

        inputCooldownTimer -= Time.unscaledDeltaTime;
    }

    /// <summary>
    /// ガイドページ移動入力を処理する。
    /// キー / コントローラー操作は手動切り替え扱いにする。
    /// </summary>
    private void HandleGuidePageMoveInput()
    {
        if (isAnimating)
        {
            return;
        }

        if (guidePageMoveAction == null || guidePageMoveAction.action == null)
        {
            return;
        }

        float moveValue = guidePageMoveAction.action.ReadValue<float>();

        if (Mathf.Abs(moveValue) < inputThreshold)
        {
            isInputHolding = false;
            return;
        }

        if (isInputHolding)
        {
            return;
        }

        if (inputCooldownTimer > 0.0f)
        {
            return;
        }

        if (moveValue > 0.0f)
        {
            ShowNextPageByUser();
        }
        else
        {
            ShowPreviousPageByUser();
        }

        isInputHolding = true;
        inputCooldownTimer = inputCooldown;
    }

    /// <summary>
    /// 一定時間経過で自動的に次のページへ切り替える。
    /// </summary>
    private void HandleAutoSwitch()
    {
        if (!autoSwitchEnabled)
        {
            return;
        }

        if (isAnimating)
        {
            return;
        }

        if (guidePages == null || guidePages.Count <= 1)
        {
            return;
        }

        autoSwitchTimer += Time.unscaledDeltaTime;

        if (autoSwitchTimer < autoSwitchInterval)
        {
            return;
        }

        if (playSeOnAutoSwitch)
        {
            ShowNextPageInternal(true);
        }
        else
        {
            ShowNextPage();
        }
    }

    /// <summary>
    /// 前のページを表示する。
    /// 自動処理や外部処理から呼ばれてもSEは鳴らさない。
    /// </summary>
    public void ShowPreviousPage()
    {
        ShowPreviousPageInternal(false);
    }

    /// <summary>
    /// 次のページを表示する。
    /// 自動処理や外部処理から呼ばれてもSEは鳴らさない。
    /// </summary>
    public void ShowNextPage()
    {
        ShowNextPageInternal(false);
    }

    /// <summary>
    /// ユーザー操作として前のページを表示する。
    /// 矢印クリックや手動入力用。
    /// </summary>
    public void ShowPreviousPageByUser()
    {
        ShowPreviousPageInternal(playSeOnManualPageMove);
    }

    /// <summary>
    /// ユーザー操作として次のページを表示する。
    /// 矢印クリックや手動入力用。
    /// </summary>
    public void ShowNextPageByUser()
    {
        ShowNextPageInternal(playSeOnManualPageMove);
    }

    /// <summary>
    /// 前のページを表示する内部処理。
    /// </summary>
    private void ShowPreviousPageInternal(bool playSe)
    {
        if (guidePages == null || guidePages.Count == 0)
        {
            return;
        }

        int nextPageIndex = currentPageIndex - 1;

        if (nextPageIndex < 0)
        {
            nextPageIndex = guidePages.Count - 1;
        }

        bool changed = ChangePage(nextPageIndex, -1);

        if (changed && playSe)
        {
            PlaySwitchPageSe();
        }
    }

    /// <summary>
    /// 次のページを表示する内部処理。
    /// </summary>
    private void ShowNextPageInternal(bool playSe)
    {
        if (guidePages == null || guidePages.Count == 0)
        {
            return;
        }

        int nextPageIndex = currentPageIndex + 1;

        if (nextPageIndex >= guidePages.Count)
        {
            nextPageIndex = 0;
        }

        bool changed = ChangePage(nextPageIndex, 1);

        if (changed && playSe)
        {
            PlaySwitchPageSe();
        }
    }

    /// <summary>
    /// 指定ページへ切り替える。
    /// 
    /// direction:
    /// -1 = 前方向
    ///  1 = 次方向
    /// 
    /// 戻り値:
    /// true  = ページ切り替えが開始または完了した
    /// false = 切り替えできなかった
    /// </summary>
    private bool ChangePage(int nextPageIndex, int direction)
    {
        if (nextPageIndex == currentPageIndex)
        {
            return false;
        }

        if (isAnimating)
        {
            return false;
        }

        ResetAutoSwitchTimer();

        if (!usePageAnimation || transitionDuration <= 0.0f)
        {
            currentPageIndex = nextPageIndex;
            ApplyPageImmediately();
            return true;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(AnimatePageTransition(currentPageIndex, nextPageIndex, direction));
        return true;
    }

    /// <summary>
    /// ページ切り替えアニメーションを実行する。
    /// </summary>
    private IEnumerator AnimatePageTransition(int fromIndex, int toIndex, int direction)
    {
        isAnimating = true;

        GameObject fromPage = guidePages[fromIndex];
        GameObject toPage = guidePages[toIndex];

        if (fromPage == null || toPage == null)
        {
            currentPageIndex = toIndex;
            ApplyPageImmediately();
            isAnimating = false;
            transitionCoroutine = null;
            yield break;
        }

        CanvasGroup fromCanvasGroup = GetOrAddCanvasGroup(fromPage);
        CanvasGroup toCanvasGroup = GetOrAddCanvasGroup(toPage);

        RectTransform fromRectTransform = fromPage.GetComponent<RectTransform>();
        RectTransform toRectTransform = toPage.GetComponent<RectTransform>();

        Vector2 basePosition = Vector2.zero;

        if (fromRectTransform != null)
        {
            basePosition = fromRectTransform.anchoredPosition;
        }
        else if (toRectTransform != null)
        {
            basePosition = toRectTransform.anchoredPosition;
        }

        Vector2 fromStartPosition = basePosition;
        Vector2 fromEndPosition = basePosition + new Vector2(-direction * slideDistance, 0.0f);

        Vector2 toStartPosition = basePosition + new Vector2(direction * slideDistance, 0.0f);
        Vector2 toEndPosition = basePosition;

        // 切り替え対象2ページだけ表示状態にする。
        fromPage.SetActive(true);
        toPage.SetActive(true);

        fromCanvasGroup.alpha = 1.0f;
        fromCanvasGroup.blocksRaycasts = false;
        fromCanvasGroup.interactable = false;

        toCanvasGroup.alpha = 0.0f;
        toCanvasGroup.blocksRaycasts = false;
        toCanvasGroup.interactable = false;

        if (fromRectTransform != null)
        {
            fromRectTransform.anchoredPosition = fromStartPosition;
        }

        if (toRectTransform != null)
        {
            toRectTransform.anchoredPosition = toStartPosition;
        }

        float elapsedTime = 0.0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float normalizedTime = Mathf.Clamp01(elapsedTime / transitionDuration);
            float easedTime = EaseOutCubic(normalizedTime);

            fromCanvasGroup.alpha = Mathf.Lerp(1.0f, 0.0f, easedTime);
            toCanvasGroup.alpha = Mathf.Lerp(0.0f, 1.0f, easedTime);

            if (fromRectTransform != null)
            {
                fromRectTransform.anchoredPosition = Vector2.Lerp(fromStartPosition, fromEndPosition, easedTime);
            }

            if (toRectTransform != null)
            {
                toRectTransform.anchoredPosition = Vector2.Lerp(toStartPosition, toEndPosition, easedTime);
            }

            yield return null;
        }

        currentPageIndex = toIndex;

        // 最終状態を確定する。
        ApplyPageImmediately();

        if (enableDebugLog)
        {
            Debug.Log($"[PauseGuide] Page Changed = {currentPageIndex}");
        }

        isAnimating = false;
        transitionCoroutine = null;
    }

    /// <summary>
    /// 現在ページだけを即時表示する。
    /// アニメーションを使わない場合や、初期表示時に使用する。
    /// </summary>
    private void ApplyPageImmediately()
    {
        if (guidePages == null || guidePages.Count == 0)
        {
            return;
        }

        for (int i = 0; i < guidePages.Count; i++)
        {
            GameObject page = guidePages[i];

            if (page == null)
            {
                continue;
            }

            bool isCurrentPage = i == currentPageIndex;

            page.SetActive(isCurrentPage);

            CanvasGroup canvasGroup = GetOrAddCanvasGroup(page);
            canvasGroup.alpha = isCurrentPage ? 1.0f : 0.0f;
            canvasGroup.interactable = isCurrentPage;
            canvasGroup.blocksRaycasts = isCurrentPage;

            RectTransform rectTransform = page.GetComponent<RectTransform>();

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// CanvasGroupを取得する。
    /// 無い場合は自動で追加する。
    /// </summary>
    private CanvasGroup GetOrAddCanvasGroup(GameObject targetObject)
    {
        CanvasGroup canvasGroup = targetObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = targetObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    /// <summary>
    /// 自動切り替えタイマーをリセットする。
    /// 手動切り替え直後にすぐ自動切り替えされるのを防ぐ。
    /// </summary>
    private void ResetAutoSwitchTimer()
    {
        autoSwitchTimer = 0.0f;
    }

    /// <summary>
    /// ページ切り替えSEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlaySwitchPageSe()
    {
        if (MenuUiSeController.Instance == null)
        {
            return;
        }

        MenuUiSeController.Instance.Play(MenuUiSeController.MenuUiSeType.GuideSlide);
    }

    /// <summary>
    /// Cubic easing。
    /// 終わり側でゆっくり止まる動きにする。
    /// </summary>
    private float EaseOutCubic(float value)
    {
        float inverseValue = 1.0f - value;
        return 1.0f - inverseValue * inverseValue * inverseValue;
    }
}