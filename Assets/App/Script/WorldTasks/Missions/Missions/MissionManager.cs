using System.Collections.Generic;
using UnityEngine;
using static SubMissionPreset;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }


    private List<SubMissionRunTime> SubMissions;

    [SerializeField] private StageMission stageData;

    private KillCounter killCounter;
    private ActionCounter actionCounter;
    private BreakBlockCounter breakCounter;



    private void Awake()
    {
        // 生成時に他にMissionManagerがいるのなら自身を削除
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;


    }

    private void Start()
    {
        // Awakeより後
        // ミッションの生成
        MissionCreate();
    }

    void MissionCreate()
    {

        SubMissions = new List<SubMissionRunTime>();

        if (stageData.SubMissionPreset1 != null)
        {
            SubMissions.Add(new SubMissionRunTime(stageData.SubMissionPreset1));
        }
        if (stageData.SubMissionPreset2 != null)
        {
            SubMissions.Add(new SubMissionRunTime(stageData.SubMissionPreset2));
        }
        if (stageData.SubMissionPreset3 != null)
        {
            SubMissions.Add(new SubMissionRunTime(stageData.SubMissionPreset3));
        }

        killCounter = new KillCounter();

        actionCounter = new ActionCounter();

        breakCounter = new BreakBlockCounter();

        int uiIndex = 0;
        foreach (var mission in SubMissions)
        {
            MissionUIManager.Instance.InitializeSubMiisions(mission, uiIndex);
            uiIndex++;
        }
    }

    public void CheakSubMissionClear()
    {
        foreach (var mission in SubMissions)
        {
            if (mission.isClear)
            {
                continue;
            }

            switch (mission.presetSub.presetType)
            {
                case SubMissionPreset.MissionType.ClearTime:
                    if (TimeUIManager.Instance.MaxTimer - mission.presetSub.TimeCount <= TimeUIManager.Instance.CurrentTime)
                    {
                        Clear(mission);
                        Debug.Log("TimeClear");
                    }
                    break;
                case SubMissionPreset.MissionType.KillCount:
                    if (mission.presetSub.killType == SubMissionPreset.KillConditionType.SpecificEnemy)
                    {
                        // 特定の敵を倒すパターン
                        if (mission.presetSub.ObjectCount <= killCounter.GetKillCount(mission.presetSub.EnemyObject))
                        {
                            Clear(mission);
                            Debug.Log("SpecifcKillClear");
                        }
                    }
                    else
                    {
                        // 全ての敵を倒すパターン
                        if (StageClearManager.Instance.GetTotalEnemyCount() <= killCounter.TotalKillCount)
                        {
                            Clear(mission);
                            Debug.Log("ALLKillClear");
                        }
                    }
                    break;
                case SubMissionPreset.MissionType.HPSaving:
                    if (mission.presetSub.PlayerHP <= PlayerHPManager.Instance.CurrentHP)
                    {
                        Clear(mission);
                        Debug.Log("HPSaveClear");
                    }
                    break;
                case SubMissionPreset.MissionType.TackleCountLimit:
                    if (mission.presetSub.PlayerActionCount >= actionCounter.tackleCount)
                    {
                        Clear(mission);
                    }
                    break;
                case SubMissionPreset.MissionType.TackleCountOverthan:
                    if (mission.presetSub.PlayerActionCount <= actionCounter.tackleCount)
                    {
                        Clear(mission);
                    }
                    break;
                case SubMissionPreset.MissionType.JumpCountLimit:
                    if (mission.presetSub.PlayerActionCount >= actionCounter.jumpCount)
                    {
                        Clear(mission);
                    }
                    break;
                case SubMissionPreset.MissionType.JumpCountOverthan:
                    if (mission.presetSub.PlayerActionCount <= actionCounter.jumpCount)
                    {
                        Clear(mission);
                    }
                    break;
                case SubMissionPreset.MissionType.BreakBlockCountOverthan:
                    if (mission.presetSub.ObjectCount <= breakCounter.BreakCount)
                    {
                        Clear(mission);
                    }
                    break;
                case SubMissionPreset.MissionType.BreakBlockCountLimit:
                    if (mission.presetSub.ObjectCount >= breakCounter.BreakCount)
                    {
                        Clear(mission);
                    }
                    break;
            }
        }
    }


    public void AddKill(EnemyData data)
    {
        Debug.Log("AddKill");
        killCounter.AddKillCount(data);

        int uiIndex = 0;


        foreach (var mission in SubMissions)
        {
            if (mission.presetSub.presetType == MissionType.KillCount)
            {
                MissionUIManager.Instance.UpdateKillMissionUI(mission, killCounter, uiIndex);
            }
            uiIndex++;
        }

        if (StageClearManager.Instance.GetMainMission().presetMain.presetType == MainMissionPreset.StageMission.Kill)
        {
            MissionUIManager.Instance.UpdateMainKillMissionUI(StageClearManager.Instance.GetMainMission(), killCounter);
        }

        StageClearManager.Instance.CheakMainMissionClear();
    }

    public bool GetSubMissionClearFlg(int ID)
    {
        return SubMissions[ID].isClear;
    }
    public void AddTotalEnemy()
    {
        StageClearManager.Instance.AddTotalEnemy();
    }

    public void AddTackleCount()
    {
        actionCounter.AddTackleCount();

        int uiIndex = 0;


        foreach (var mission in SubMissions)
        {
            if (mission.presetSub.presetType == MissionType.TackleCountLimit || mission.presetSub.presetType == MissionType.TackleCountOverthan)
            {
                MissionUIManager.Instance.UpdateTackleMissionUI(mission, actionCounter, uiIndex);
            }
            uiIndex++;
        }
    }

    public void AddJumpCount()
    {
        actionCounter.AddJumpCount();
        int uiIndex = 0;


        foreach (var mission in SubMissions)
        {
            if (mission.presetSub.presetType == MissionType.JumpCountLimit || mission.presetSub.presetType == MissionType.JumpCountOverthan)
            {
                MissionUIManager.Instance.UpdateJumpMissionUI(mission, actionCounter, uiIndex);
            }
            uiIndex++;
        }
    }
    public void AddBreakCount()
    {
        breakCounter.AddBreakBlockCounter();

        int uiIndex = 0;

        foreach (var mission in SubMissions)
        {
            if (mission.presetSub.presetType == MissionType.BreakBlockCountOverthan || mission.presetSub.presetType == MissionType.BreakBlockCountLimit)
            {
                MissionUIManager.Instance.UpdateBreakBlockMissionUI(mission, breakCounter, uiIndex);
            }
            uiIndex++;
        }
    }

    public void DrawResultUISubMissions()
    {
        int uiIndex = 0;
        foreach (var mission in SubMissions)
        {
            ResultUIManager.Instance.DrawSubMissionUIs(mission, uiIndex);
            uiIndex++;    
        }
    }
    public KillCounter GetKillCounter()
    {// stageClearManagerで使用
        return killCounter;
    }


    private void Clear(SubMissionRunTime mission)
    {
        mission.isClear = true;
    } 
   
   

   
}
