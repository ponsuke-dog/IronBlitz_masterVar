using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject gameOverCanvas;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button titleButton;
    [SerializeField] private Button exitButton;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("シーン遷移先")]
    [SerializeField] SceneData titleSceneData;
    [SerializeField] SceneData stageSelectSceneData;

    [Header("アニメーションスクリプト")]
    [SerializeField] private UISlideAnimation gameOverAnimation;
    [SerializeField] private UISlideAnimation retryAnimation;
    [SerializeField] private UISlideAnimation titleAnimation;
    [SerializeField] private UISlideAnimation exitAnimation;

    private InputActionMap playerActionMap;
    private InputActionMap uiActionMap;

    private bool isGameOver;

    private void Awake()
    {
        Debug.Log("GameOverManager Awake");
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;


        playerActionMap = inputActions.FindActionMap("Player", true);
        uiActionMap = inputActions.FindActionMap("UI", true);

        isGameOver = false;
    }

    private void Start()
    {
        gameOverCanvas.SetActive(false);
    }

    public void ShowGameOver()
    {
        Debug.Log(gameOverCanvas);
        Debug.Log(retryButton);
        Debug.Log(playerActionMap);
        Debug.Log(uiActionMap);
        Debug.Log(EventSystem.current);

        if (isGameOver) return;

        isGameOver = true;

        gameOverCanvas.SetActive(true);

        retryButton.gameObject.SetActive(false);
        titleButton.gameObject.SetActive(false);
        exitButton.gameObject.SetActive(false);

        gameOverAnimation.OnAnimationFinished += PlayRetry;
        gameOverAnimation.Play();

        // UIモードへ切り替え
        // Player操作停止
        playerActionMap.Disable();

        // UI操作有効化
        uiActionMap.Enable();

        // 最初にRetryを選択
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(retryButton.gameObject);

        Time.timeScale = 0f;
    }

    public void Retry()
    {
        Time.timeScale = 1f;

        uiActionMap.Disable();
        playerActionMap.Enable();

        gameOverCanvas.SetActive(false);

        // 現在のシーンをリロード
       SceneChangeManager.Instance.ReloadCurrentScene();

    }

    public void BackToTitle()
    {
        Time.timeScale = 1f;

        uiActionMap.Disable();
        playerActionMap.Enable();

        SceneChangeManager.Instance.ChangeScene(titleSceneData);
    }

    //ステージセレクトシーンの遷移する
    public void BackToStageSelect()
    {
        Time.timeScale = 1f;

        uiActionMap.Disable();
        playerActionMap.Enable();

        // ステージセレクトシーンのシーンデータを取得して遷移
        SceneChangeManager.Instance.ChangeScene(stageSelectSceneData);
    }

    private void PlayRetry()
    {
        gameOverAnimation.OnAnimationFinished -= PlayRetry;

        retryButton.gameObject.SetActive(true);

        retryAnimation.OnAnimationFinished += PlayTitle;
        retryAnimation.Play();
    }

    private void PlayTitle()
    {
        retryAnimation.OnAnimationFinished -= PlayTitle;

        titleButton.gameObject.SetActive(true);

        titleAnimation.OnAnimationFinished += PlayExit;

        titleAnimation.Play();
    }

    private void PlayExit()
    {
        titleAnimation.OnAnimationFinished -= PlayExit;

        exitButton.gameObject.SetActive(true);

        exitAnimation.OnAnimationFinished += FinishAnimation;

        exitAnimation.Play();
    }

    private void FinishAnimation()
    {
        exitAnimation.OnAnimationFinished -= FinishAnimation;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

}