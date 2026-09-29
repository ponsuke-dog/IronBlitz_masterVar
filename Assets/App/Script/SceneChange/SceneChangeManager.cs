using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// シーン遷移全体を管理する共通マネージャー
public class SceneChangeManager : MonoBehaviour
{
    public static SceneChangeManager Instance { get; private set; }

    [SerializeField] private SceneDataBase dataBase;

    private bool istransitioning = false;

    private SceneData CurrentSceneData
    {
        get
        {
            if (dataBase == null)
            {
                Debug.LogError("SceneDataBaseが未設定");
                return null;
            }
            // 現在開かれているシーンをシーンデータベースから検索して返す
            string current = SceneManager.GetActiveScene().name;
            return dataBase.GetScene(current);
        }
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (FadeManager.Instance != null && CurrentSceneData != null && CurrentSceneData.EnterFade != null)
        {
            // シーンの始めに黒くしておく
            FadeManager.Instance.SetBlack(CurrentSceneData.EnterFade);
        }
    }

    private IEnumerator Start()
    {
        yield return null;

        if (FadeManager.Instance != null)
        {            
            yield return FadeManager.Instance.FadeFromBlackRoutine(CurrentSceneData.EnterFade);
        }
    }
    public void ReloadCurrentScene()
    {
        if (CurrentSceneData == null)
        {
            Debug.LogError("現在のシーンがNull");
            return;
        }
        Debug.Log("リロード");

        ChangeScene(CurrentSceneData);
    }

    public void ChangeScene(SceneData scene)    // 基本はこっちを呼ぶ
    {
        // シーンデータ内のシーン名、フェード情報を使用
        ChangeScene(scene, scene.EnterFade, scene.ExitFade);
    }


    public void ChangeScene(SceneData scene, FadePreset EnterFade, FadePreset ExitFade) // 特別な個別設定したい場合のみこっちを直接呼ぶ
    {
        if (istransitioning) return;
        if (FadeManager.Instance == null) return;
        if (FadeManager.Instance.IsFading) return;

        StartCoroutine(ChangeSceneRoutine(scene,ExitFade));
    }

    private IEnumerator ChangeSceneRoutine(SceneData scene,FadePreset ExitFade)
    {
        istransitioning = true;
        //   pendingfadeinpreset = fadeInPreset;

        if (CurrentSceneData != null)
        {
            yield return FadeManager.Instance.FadeToBlackRoutine(ExitFade);
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene.sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

}
