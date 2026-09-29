using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StageSelectManager : MonoBehaviour
{
    public static StageSelectManager Instance { get; private set; }

    [SerializeField] private Button StageButtonPrefab;
    [SerializeField] private Button BossStageButtonPrefab;
    [SerializeField] Transform buttonParent;

    [SerializeField] private SelectInputController inputController;

    private List<Button> currentButtons = new();

    private WorldData currentWorldData;
    private GameObject previewSelected;
    private GameObject currentSelected;

    [SerializeField] private Image previewA;
    [SerializeField] private Image previewB;
    [SerializeField] private UIAnimationController animationController;

    [SerializeField] private float previewFadeTime = 0.5f;

    private CanvasGroup groupA;
    private CanvasGroup groupB;

    private bool useA = true;
    private Coroutine slideShowRoutine;


    private void Awake()
    {
        // シングルトン
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void Start()
    {
        inputController.Open();
        SaveSystem.Load();
        Time.timeScale = 1f;
    }
    public void SetUP(WorldData world)
    {
        StageSlectScrool.Instance.ResetScroll();
        currentWorldData = world;
        CreateStageButtons();

        StageSlectScrool.Instance.ButtonGet();
    }

    private void Update()
    {

        currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != previewSelected)
        {
            previewSelected = currentSelected;
            if (currentSelected != null)
            {

                int index = inputController.GetButtonNum();

                if (currentWorldData == null)
                {
                    return;
                }
                if (index < 0 || index >= currentWorldData.stages.Length)
                {
                    return;
                }

                UpdateMissionUI(index);
            }
        }



    }
    private void UpdateMissionUI(int index)
    {
        int id = index + (5 * currentWorldData.WorldID);
        StageData stage = currentWorldData.stages[index];

        // 初回セットアップ
        if (groupA == null)
        {
            groupA = previewA.GetComponent<CanvasGroup>();
            if (groupA == null) groupA = previewA.gameObject.AddComponent<CanvasGroup>();

            groupB = previewB.GetComponent<CanvasGroup>();
            if (groupB == null) groupB = previewB.gameObject.AddComponent<CanvasGroup>();

            groupA.alpha = 1f;
            groupB.alpha = 0f;
        }

        // 前のスライドショーを停止
        if (slideShowRoutine != null)
            StopCoroutine(slideShowRoutine);

        // 新しいステージの画像スライドショー開始
        slideShowRoutine = StartCoroutine(PreviewSlideShow(stage.stagePreview.previewSprites));

        SelectMissionManager.Instance.DrawMainMissionUIs(stage.stageMission.MainMissionPreset);
        SelectMissionManager.Instance.DrawSubMissionUIs(stage.stageMission.SubMissionPreset1, 0);
        SelectMissionManager.Instance.DrawSubMissionUIs(stage.stageMission.SubMissionPreset2, 1);
        SelectMissionManager.Instance.DrawSubMissionUIs(stage.stageMission.SubMissionPreset3, 2);
        SelectMissionManager.Instance.CheakMissionClearStar(id);
        StageBestTimeViewr.Instance.StageTimerView(id);

        GameData.SetCurrentStageData(stage);
    }

    private void CreateStageButtons()
    {

        // ボタンのリセット
        foreach (var button in currentButtons)
        {
            DestroyImmediate(button.gameObject);
        }
        currentButtons.Clear();

        var stages = currentWorldData.stages;

        for (int i = 0; i < stages.Length; i++)
        {
            // 今現在のステージ参照
            var stage = stages[i];

            if (!SaveSystem.GetUnlock(stage.stageID))
            {
                continue;
            }

            // 生成されるボタンがステージの最後ならボス、以外なら各ステージ
            Button prefab = (i == stages.Length - 1) ? BossStageButtonPrefab : StageButtonPrefab;

            Button button = Instantiate(prefab, buttonParent);

            button.GetComponentInChildren<TMPro.TMP_Text>().text = stage.stageName;

            var sceneData = stage.sceneData;

            button.onClick.AddListener(() => { LoadScene(sceneData); });

            currentButtons.Add(button);

        }

        StageSlectScrool.Instance.ButtonGet();

        inputController.SetStageButtons(currentButtons);

        if (currentButtons.Count > 0)
        {
            currentButtons[0].Select();
        }
    }

    public void LoadScene(SceneData data)
    {
        Debug.Log($"シーン遷移 {data} へ");
        InputClose();
        SceneChangeManager.Instance.ChangeScene(data);
    }

    public void StagetoWorld()
    {
        SelectAnimationManager.Instance.StagetoWorld();
        inputController.OpenWorldSelect();
    }
    private IEnumerator PreviewSlideShow(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
            yield break;

        int index = 0;

        while (true)
        {
            Sprite nextSprite = sprites[index];

            // クロスフェード
            if (useA)
            {
                previewB.sprite = nextSprite;
                yield return animationController.CrossFade(groupA, groupB, previewFadeTime);
            }
            else
            {
                previewA.sprite = nextSprite;
                yield return animationController.CrossFade(groupB, groupA, previewFadeTime);
            }

            useA = !useA;

            // 次の画像へ
            index = (index + 1) % sprites.Length;

            // 次の画像までの待ち時間
            yield return new WaitForSecondsRealtime(2f);
        }
    }


    public void InputClose()
    {
        inputController.Close();
    } 
    public void InputOpen()
    {
        inputController.Open();
    }
}