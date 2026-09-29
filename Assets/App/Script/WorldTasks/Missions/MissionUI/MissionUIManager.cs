using System.Collections.Generic;
using UnityEngine;
using static SubMissionPreset;

public class MissionUIManager : MonoBehaviour
{
    public static MissionUIManager Instance { get; private set; }

    [Header("オンオフ切り替えをするGameObject")]
    [SerializeField] GameObject root;

    [Header("登録するミッションUIの１行")]
    [SerializeField] private MissionUILine MainUI;
    [SerializeField] private List<MissionUILine> UIs;


    private void Awake()
    {
        // 生成時に他にMissionUIManagerがいるのなら自身を削除
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    public void InitializeMainMission(MainMissionRunTime mission)
    {
        switch (mission.presetMain.presetType)
        {
            case MainMissionPreset.StageMission.Goal:
                MainUI.Initialize("ゴールを目指せ！");
                MainUI.UpdateText();
                break;
            case MainMissionPreset.StageMission.Kill:
                if (mission.presetMain.killType == MainMissionPreset.KillConditionType.SpecificEnemy)
                {
                    if (mission.presetMain.KillObject != null)
                    {
                        MainUI.Initialize("{2}を{1}体倒せ! ({0}/{1})");
                        MainUI.UpdateText(0, mission.presetMain.KillCount, mission.presetMain.KillObject.GroupName);
                    }
                    else
                    {
                        MainUI.Initialize("エネミーがNullです");
                        MainUI.UpdateText();
                    }
                }
                else
                {
                    MainUI.Initialize("全て倒せ! ({0}/{1})");
                    MainUI.UpdateText(0, StageClearManager.Instance.GetTotalEnemyCount());
                }
                break;
            case MainMissionPreset.StageMission.Collect:
                if (mission.presetMain.CollectObject != null)
                {
                    MainUI.Initialize("{2}を{1}個集めろ! ({0}/{1})");
                    MainUI.UpdateText(0, mission.presetMain.CollectCount, mission.presetMain.CollectObject.name);
                }
                else
                {
                    MainUI.Initialize("収集物がNullです。");
                    MainUI.UpdateText();
                }
                break;
        }
    }
    public void InitializeSubMiisions(SubMissionRunTime mission, int uiIndex)
    {
  

        switch (mission.presetSub.presetType)
        {
            case SubMissionPreset.MissionType.ClearTime:
                int minutes = mission.presetSub.TimeCount / 60;
                int seconds = mission.presetSub.TimeCount % 60;

                UIs[uiIndex].Initialize("{0}:{1}以内にクリア!");
                UIs[uiIndex].UpdateText($"{ minutes: 00}",$"{ seconds: 00}");
                break;
            case SubMissionPreset.MissionType.KillCount:
                if (mission.presetSub.killType == SubMissionPreset.KillConditionType.SpecificEnemy)
                {
                    UIs[uiIndex].Initialize("{2}を{1}体倒せ! ({0}/{1})");
                    UIs[uiIndex].UpdateText(0, mission.presetSub.ObjectCount, mission.presetSub.EnemyObject.GroupName);
                }
                else
                {
                    UIs[uiIndex].Initialize("全て倒せ! ({0}/{1})");
                    UIs[uiIndex].UpdateText(0, StageClearManager.Instance.GetTotalEnemyCount());
                }
                break;
            case SubMissionPreset.MissionType.TackleCountLimit:
                UIs[uiIndex].Initialize("タックル{1}回までにクリア! ({0}/{1})");
                UIs[uiIndex].UpdateText(0, mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.TackleCountOverthan:
                UIs[uiIndex].Initialize("タックル{1}回以上でクリア! ({0}/{1})");
                UIs[uiIndex].UpdateText(0, mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountLimit:
                UIs[uiIndex].Initialize("ジャンプ{1}回までにクリア! ({0}/{1})");
                UIs[uiIndex].UpdateText(0, mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountOverthan:
                UIs[uiIndex].Initialize("ジャンプ{1}回以上でクリア! ({0}/{1})");
                UIs[uiIndex].UpdateText(0, mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountOverthan:
                UIs[uiIndex].Initialize("ブロックを{1}個壊せ! ({0}/{1})");
                UIs[uiIndex].UpdateText(0,mission.presetSub.ObjectCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountLimit:
                UIs[uiIndex].Initialize("ブロックを{1}個まで壊すな! ({0}/{1})");
                UIs[uiIndex].UpdateText(0,mission.presetSub.ObjectCount);
                break;
            case SubMissionPreset.MissionType.HPSaving:
                UIs[uiIndex].Initialize("HP {0}% 以上でクリア!");
                UIs[uiIndex].UpdateText(mission.presetSub.PlayerHP);
                break; 
        }
    }

    public void UpdateMainKillMissionUI(MainMissionRunTime mission, KillCounter killCounter)
    {
        if (mission.presetMain.killType == MainMissionPreset.KillConditionType.SpecificEnemy)
        {
            int current = CurrentMin(killCounter.GetKillCount(mission.presetMain.KillObject));
            MainUI.UpdateText(current, mission.presetMain.KillCount, mission.presetMain.KillObject.GroupName);
        }
        else
        {
            int current = CurrentMin(killCounter.TotalKillCount);

            MainUI.UpdateText(current, StageClearManager.Instance.GetTotalEnemyCount());
        }
    }

    public void UpdateKillMissionUI(SubMissionRunTime mission, KillCounter killCounter, int uiIndex)
    {
        if (mission.presetSub.killType == KillConditionType.SpecificEnemy)
        {
            int current = CurrentMin(killCounter.GetKillCount(mission.presetSub.EnemyObject));

            UIs[uiIndex].UpdateText(current, mission.presetSub.ObjectCount, mission.presetSub.EnemyObject.GroupName);

            if (mission.presetSub.ObjectCount <= current)
            {
                UIs[uiIndex].SetUIState(MissionUILine.UIState.Cleared);
            }
        }
        else
        {
            int current = CurrentMin(killCounter.TotalKillCount);

            UIs[uiIndex].UpdateText(current, StageClearManager.Instance.GetTotalEnemyCount());

            if (StageClearManager.Instance.GetTotalEnemyCount() <= current)
            {
                UIs[uiIndex].SetUIState(MissionUILine.UIState.Cleared);
            }
        }


    }
    public void UpdateJumpMissionUI(SubMissionRunTime mission, ActionCounter actionCounter, int uiIndex)
    {
        int current = CurrentMin(actionCounter.jumpCount);

        UIs[uiIndex].UpdateText(current, mission.presetSub.PlayerActionCount);

        if (mission.presetSub.PlayerActionCount < current)
        {
            UIs[uiIndex].SetUIState(MissionUILine.UIState.Failed);
        }
    }

    public void UpdateTackleMissionUI(SubMissionRunTime mission, ActionCounter actionCounter, int uiIndex)
    { 
        int current = CurrentMin(actionCounter.tackleCount);

        UIs[uiIndex].UpdateText(current, mission.presetSub.PlayerActionCount);

        if (mission.presetSub.PlayerActionCount < current)
        {
            UIs[uiIndex].SetUIState(MissionUILine.UIState.Failed);
        }
    }

    //public void UpdateCollectMissionUI(SubMissionRunTime mission, CollectionCounter collectionCounter,int uiIndex)
    //{
    //    int current = CurrentMin(collectionCounter.CollectCount);

    //    UIs[uiIndex].UpdateText(current, mission.presetSub.ObjectCount);

    //    if (mission.presetSub.ObjectCount <= current)
    //    {
    //        UIs[uiIndex].SetUIState(MissionUILine.UIState.Cleared);
    //    }
    //} 

    public void UpdateBreakBlockMissionUI(SubMissionRunTime mission, BreakBlockCounter breakBlockCounter,int uiIndex)
    {
        int current = CurrentMin(breakBlockCounter.BreakCount);

        UIs[uiIndex].UpdateText(current, mission.presetSub.ObjectCount);

        if (mission.presetSub.presetType == MissionType.BreakBlockCountLimit)
        {
            if (mission.presetSub.ObjectCount < current)
            {
                UIs[uiIndex].SetUIState(MissionUILine.UIState.Failed);
            }
        }
        else
        {
            if (mission.presetSub.ObjectCount <= current)
            {
                UIs[uiIndex].SetUIState(MissionUILine.UIState.Cleared);
            }
        }
    }

    public void SetUIRootFlg(bool flg)
    {
        root.SetActive(flg);   
    }

    private int CurrentMin(int a)
    {
        return Mathf.Min(a, 99);
    }
}
