using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class StageClearManager : MonoBehaviour
{
    public static StageClearManager Instance { get; private set; }

    private int TotalEnemyCount;
    private bool ClearFlg = false;

    private MainMissionRunTime MainMission;

    private ResultInputController resultInputController;

    [SerializeField] StageData stageData;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        // シングルトン化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        resultInputController = FindFirstObjectByType<ResultInputController>();
        Time.timeScale = 1.0f;
        if (resultInputController == null)
        {
            Debug.Log("ResultInputControllerがnullです");
        }
    }

    private void Start()
    {
        if (stageData.stageMission.MainMissionPreset != null)
        {
            MainMission = new MainMissionRunTime(stageData.stageMission.MainMissionPreset);
        }
        MissionUIManager.Instance.InitializeMainMission(MainMission);

        // 念のため
        ClearFlg = false;
        MainMission.presetMain.GoalFlg = false;
        resultInputController.ResultClose();

        // ステージ情報の一時保存
        GameData.SetCurrentStageData(stageData);

        //// コイン
        //SaveData saveData = SaveSystem.Load();

        //StageSaveData stageSaveData = saveData.stages[stageData.stageID];

        //CoinCounter.Instance.Initialize(stageSaveData);
    }

    private void Update()
    {
        if (!ClearFlg)
        {
            return;
        }
    }

    public void CheakMainMissionClear()
    {

        switch (MainMission.presetMain.presetType)
        {
            case MainMissionPreset.StageMission.Goal:
                if (MainMission.presetMain.GoalFlg)
                {
                    Clear(MainMission);
                }
                break;
            case MainMissionPreset.StageMission.Kill:
                if (MainMission.presetMain.killType == MainMissionPreset.KillConditionType.SpecificEnemy)
                {
                    if (MainMission.presetMain.KillCount <= MissionManager.Instance.GetKillCounter().GetKillCount(MainMission.presetMain.KillObject))
                    {
                        Clear(MainMission);
                    }
                }
                else
                {
                    if (TotalEnemyCount <= MissionManager.Instance.GetKillCounter().TotalKillCount)
                    {
                        Clear(MainMission);
                    }
                }
                break;
            case MainMissionPreset.StageMission.Collect:
                break;
        }
    }

    public void StageClear()
    {
        // ここでシーン遷移関係のやつ

        // カウントストップ
        TimeUIManager.Instance.SetCountDownStart(false);

        MissionManager.Instance.CheakSubMissionClear();

        // セーブ
        SaveClearData();

        // リザルト生成はココ
        ResultUIManager.Instance.CallResult();

//        ResultUIManager.Instance.DrawResultMainMissionUI(MainMission);
        resultInputController.Open();
        ResultUIAnimation.Instance.ResultAnime();
        MissionManager.Instance.DrawResultUISubMissions();


        ClearFlg = true;



        Time.timeScale = 0;
    }

    public void AddTotalEnemy()
    {
        TotalEnemyCount++;
    }

    public int GetTotalEnemyCount()
    {
        return TotalEnemyCount;
    }

    public MainMissionRunTime GetMainMission()
    {
        return MainMission;
    }

    public void  ReachGoal()
    {

        MainMission.presetMain.GoalFlg = true;
        CheakMainMissionClear();
    }

    // 次のステージへ
    public void ChangeNextStage()
    {
        resultInputController.Close();

        var map = InputSystem.actions.FindActionMap("Player");
        map.Disable();

        SceneChangeManager.Instance.ChangeScene(stageData.NextStage);
    }

    // ステージマップへ
    public void ChangeStageMap()
    {
        resultInputController.Close();

        var map = InputSystem.actions.FindActionMap("Player");
        map.Disable();
        SceneChangeManager.Instance.ChangeScene(stageData.StageMap);
    }

    // リトライ
    public void Retry()
    {
        Debug.Log("retry pushed");
        resultInputController.Close();
        var map = InputSystem.actions.FindActionMap("Player");
        map.Disable();

        SceneChangeManager.Instance.ReloadCurrentScene();
    }

    public void SaveClearData()
    {
        // セーブデータをロードしてから比較する
        SaveData data = SaveSystem.Load();


        // ID参照
        int stageID = stageData.stageID;
        
        if (stageID < 0 || stageID >= data.stages.Length)
        {
            Debug.LogError("存在しないIDじゃボケ");
            return;
        }

        StageSaveData stage = data.stages[stageID];

        // 各項目に更新かけてから上書きセーブ ===============================

        stage.isMainClear = true;

        if (stageID + 1 < data.stages.Length)
        {
            data.stages[stageID + 1].isUnlock = true;
        }

        float stageLimitTime = TimeUIManager.Instance.MaxTimer;
        float clearTime = TimeUIManager.Instance.MaxTimer - TimeUIManager.Instance.CurrentTime;

       // stage.CoinsFlags = new List<bool>(CoinCounter.Instance.CoinsFalgs);

        if (data.stages[stageID].SubMission1 == false)
        {
            stage.SubMission1 = MissionManager.Instance.GetSubMissionClearFlg(0);
        }
        if (data.stages[stageID].SubMission2 == false)
        {
            stage.SubMission2 = MissionManager.Instance.GetSubMissionClearFlg(1);
        }
        if (data.stages[stageID].SubMission3 == false)
        {
            stage.SubMission3 = MissionManager.Instance.GetSubMissionClearFlg(2);
        }

        // ベストタイム更新
        if (stage.ClearBestTime == 0 || clearTime < stage.ClearBestTime)
        {
            Debug.Log("ベストタイム更新");
            stage.ClearBestTime = clearTime;
        }

        //==================================================================

        // ここでセーブ
        SaveSystem.Save(data);
        Debug.Log("SaveConplete");
    }
    private void Clear(MainMissionRunTime mission)
    {
        mission.isClear = true;
        StageClear();
    }
}
