using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ResultInputController : MonoBehaviour
{
    [SerializeField] private GameObject resultRoot;

    [SerializeField] private Button nextButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button stageMapButton;

    [SerializeField] private InputActionAsset inputActions;

    [SerializeField] private string playerMapName = "Player";
    [SerializeField] private string uiMapName = "UI";

    private InputActionMap playerMap;
    private InputActionMap uiMap;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void Awake()
    {
        playerMap = inputActions.FindActionMap(playerMapName, true);
        uiMap = inputActions.FindActionMap(uiMapName, true);
    }


    public void Open()
    {
        Debug.Log("リザルト画面の表示");

        // リザルト画面の表示
        resultRoot.SetActive(true);
        TimeUIManager.Instance.SetTimerRootFlg(false);
        TackleGaugeUI.Instance.SetVisible(false);
        PlayerHPGaugeUI.Instance.SetVisible(false);

        playerMap.Disable();
        uiMap.Enable();

        nextButton.Select();
    }

    public void Close()
    {
        uiMap.Disable();
        playerMap.Enable();
    }

    public void ResultClose()
    {
        Debug.Log("リザルト画面の非表示");
        resultRoot.SetActive(false);
    }
}
