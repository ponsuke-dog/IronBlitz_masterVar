using UnityEngine;

/// <summary>
/// タイトルメニューの各ボタン処理を管理します。
/// </summary>
public sealed class TitleMenuActionRouter : MonoBehaviour
{
    [Header("Game Start")]
    [SerializeField] private SceneData gameSceneData;

    [Header("Config")]
    [SerializeField] private TitleConfigMenuBridge configMenuBridge;

    [Header("Data Clear")]
    [SerializeField] private DataClearDialogController dataClearDialogController;

    public void OnClickStartGame()
    {
        if (gameSceneData == null)
        {
            Debug.LogWarning("Game Scene Data が設定されていません。");
            return;
        }

        if (SceneChangeManager.Instance == null)
        {
            Debug.LogWarning("SceneChangeManager が存在しません。");
            return;
        }
        GameData.SetCurrentStageData(null);
        SceneChangeManager.Instance.ChangeScene(gameSceneData);
    }

    public void OnClickOption()
    {
        if (configMenuBridge == null)
        {
            Debug.LogWarning("TitleConfigMenuBridge が設定されていません。");
            return;
        }

        configMenuBridge.OpenConfigFromTitle();
    }

    public void OnClickDataClear()
    {
        if (dataClearDialogController == null)
        {
            Debug.LogWarning("DataClearDialogController が設定されていません。");
            return;
        }

        dataClearDialogController.OpenDialog();
    }

    public void OnClickExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}