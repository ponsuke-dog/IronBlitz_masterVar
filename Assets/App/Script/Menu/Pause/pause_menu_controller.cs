using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ポーズメニュー画面を制御するクラス。
/// Resume / Config / Restart / Return などのボタン処理を担当する。
/// 
/// 【追加仕様】
/// - Continue / Resume はSEを鳴らさずにメニューを閉じる。
/// - Config / Retry / StageSelect / Title はEnter SEを鳴らす。
/// - Retry / StageSelect / Title は一度押したらボタンを無効化し、連打を防ぐ。
/// - Scene遷移処理が二重に呼ばれないようにする。
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuManager menuManager;

    [Header("Root")]
    [SerializeField] private GameObject rootObject;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button configButton;
    [SerializeField] private Button restartStageButton;
    [SerializeField] private Button returnStageSelectButton;
    [SerializeField] private Button returnTitleButton;

    [Header("Scene Data")]
    [SerializeField] private SceneData restartScene;
    [SerializeField] private SceneData stageSelectScene;
    [SerializeField] private SceneData titleScene;

    [Header("SE")]
    [Tooltip("Resume以外のポーズメニューボタン決定時にSEを鳴らします。")]
    [SerializeField] private bool playEnterSe = true;

    [Header("Click Guard")]
    [Tooltip("ボタン連打を防止します。")]
    [SerializeField] private bool useClickGuard = true;

    [Tooltip("Configなど、シーン遷移しないボタンの短時間連打を防ぐ秒数です。")]
    [SerializeField] private float menuActionLockDuration = 0.2f;

    private bool isMenuActionLocked;
    private bool isSceneTransitionRequested;
    private float menuActionUnlockTime;

    private void Awake()
    {
        RegisterButtonEvents();
    }

    private void Update()
    {
        UpdateMenuActionLock();
    }

    /// <summary>
    /// ボタンイベントを登録する。
    /// 二重登録を避けるため、一度RemoveしてからAddする。
    /// </summary>
    private void RegisterButtonEvents()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(OnClickResume);
            resumeButton.onClick.AddListener(OnClickResume);
        }

        if (configButton != null)
        {
            configButton.onClick.RemoveListener(OnClickConfig);
            configButton.onClick.AddListener(OnClickConfig);
        }

        if (restartStageButton != null)
        {
            restartStageButton.onClick.RemoveListener(OnClickRestartStage);
            restartStageButton.onClick.AddListener(OnClickRestartStage);
        }

        if (returnStageSelectButton != null)
        {
            returnStageSelectButton.onClick.RemoveListener(OnClickReturnStageSelect);
            returnStageSelectButton.onClick.AddListener(OnClickReturnStageSelect);
        }

        if (returnTitleButton != null)
        {
            returnTitleButton.onClick.RemoveListener(OnClickReturnTitle);
            returnTitleButton.onClick.AddListener(OnClickReturnTitle);
        }
    }

    /// <summary>
    /// ポーズメニューを表示する。
    /// 表示時に連打防止状態をリセットする。
    /// </summary>
    public void Open()
    {
        isMenuActionLocked = false;
        isSceneTransitionRequested = false;
        menuActionUnlockTime = 0.0f;

        SetButtonsInteractable(true);

        if (rootObject != null)
        {
            rootObject.SetActive(true);
        }

        if (EventSystem.current != null && resumeButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    /// <summary>
    /// ポーズメニューを非表示にする。
    /// </summary>
    public void Close()
    {
        if (rootObject != null)
        {
            rootObject.SetActive(false);
        }
    }

    /// <summary>
    /// Continue / Resumeボタン。
    /// 仕様によりSEは鳴らさず、メニューだけ閉じる。
    /// </summary>
    private void OnClickResume()
    {
        if (TryLockMenuAction() == false)
        {
            return;
        }

        if (menuManager != null)
        {
            menuManager.CloseAllMenus();
        }
    }

    /// <summary>
    /// Configボタン。
    /// </summary>
    private void OnClickConfig()
    {
        if (TryLockMenuAction() == false)
        {
            return;
        }

        PlayEnterSe();

        if (menuManager != null)
        {
            menuManager.OpenConfigMenu();
        }
    }

    /// <summary>
    /// ステージリトライボタン。
    /// </summary>
    private void OnClickRestartStage()
    {
        ChangeScene(restartScene);
    }

    /// <summary>
    /// ステージセレクトへ戻るボタン。
    /// </summary>
    private void OnClickReturnStageSelect()
    {
        ChangeScene(stageSelectScene);
    }

    /// <summary>
    /// タイトルへ戻るボタン。
    /// </summary>
    private void OnClickReturnTitle()
    {
        ChangeScene(titleScene);
    }

    /// <summary>
    /// SceneChangeManager経由でシーン遷移する。
    /// 一度呼ばれた後は再実行しない。
    /// </summary>
    private void ChangeScene(SceneData sceneData)
    {
        if (TryStartSceneTransition() == false)
        {
            return;
        }

        if (sceneData == null)
        {
            Debug.LogWarning("SceneData が設定されていません。");
            UnlockSceneTransition();
            return;
        }

        if (SceneChangeManager.Instance == null)
        {
            Debug.LogWarning("SceneChangeManager がシーン内に存在しません。");
            UnlockSceneTransition();
            return;
        }

        PlayEnterSe();

        // シーン遷移中に再度押されないよう、全ボタンを無効化します。
        SetButtonsInteractable(false);

        Time.timeScale = 1.0f;
        SceneChangeManager.Instance.ChangeScene(sceneData);
    }

    /// <summary>
    /// メニュー内ボタンの短時間連打を防ぐ。
    /// </summary>
    private bool TryLockMenuAction()
    {
        if (useClickGuard == false)
        {
            return true;
        }

        if (isMenuActionLocked)
        {
            return false;
        }

        isMenuActionLocked = true;
        menuActionUnlockTime = Time.unscaledTime + menuActionLockDuration;

        return true;
    }

    /// <summary>
    /// シーン遷移ボタンの二重実行を防ぐ。
    /// </summary>
    private bool TryStartSceneTransition()
    {
        if (useClickGuard == false)
        {
            return true;
        }

        if (isSceneTransitionRequested)
        {
            return false;
        }

        isSceneTransitionRequested = true;
        isMenuActionLocked = true;

        return true;
    }

    /// <summary>
    /// シーン遷移開始前に失敗した場合だけロックを戻す。
    /// </summary>
    private void UnlockSceneTransition()
    {
        isSceneTransitionRequested = false;
        isMenuActionLocked = false;
        SetButtonsInteractable(true);
    }

    /// <summary>
    /// メニュー操作ロックの解除時間を更新する。
    /// </summary>
    private void UpdateMenuActionLock()
    {
        if (isSceneTransitionRequested)
        {
            return;
        }

        if (isMenuActionLocked == false)
        {
            return;
        }

        if (Time.unscaledTime < menuActionUnlockTime)
        {
            return;
        }

        isMenuActionLocked = false;
    }

    /// <summary>
    /// ポーズメニュー内の全ボタンの入力可否を切り替える。
    /// </summary>
    private void SetButtonsInteractable(bool isInteractable)
    {
        if (resumeButton != null)
        {
            resumeButton.interactable = isInteractable;
        }

        if (configButton != null)
        {
            configButton.interactable = isInteractable;
        }

        if (restartStageButton != null)
        {
            restartStageButton.interactable = isInteractable;
        }

        if (returnStageSelectButton != null)
        {
            returnStageSelectButton.interactable = isInteractable;
        }

        if (returnTitleButton != null)
        {
            returnTitleButton.interactable = isInteractable;
        }
    }

    /// <summary>
    /// ボタン決定SEを再生する。
    /// MenuUiSeControllerが存在しない場合は何もしない。
    /// </summary>
    private void PlayEnterSe()
    {
        if (playEnterSe == false)
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