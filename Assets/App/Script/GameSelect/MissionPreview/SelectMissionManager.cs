using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class SelectMissionManager : MonoBehaviour
{
   
    [SerializeField] private List<SelectMissionUILine> SubMissionUIs;
    [SerializeField] private SelectMissionUILine MainMissionUI;
    public static SelectMissionManager Instance { get; private set; }
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

    public void DrawMainMissionUIs(MainMissionPreset mission)
    {
        switch (mission.presetType)
        {
            case MainMissionPreset.StageMission.Goal:
                MainMissionUI.Initialize("ゴールを目指せ！");
                MainMissionUI.UpdateText();
                break;
            case MainMissionPreset.StageMission.Kill:
                if (mission.killType == MainMissionPreset.KillConditionType.SpecificEnemy)
                {
                    if (mission.KillObject != null)
                    {
                        MainMissionUI.Initialize("{1}を{0}体倒せ!");
                        MainMissionUI.UpdateText(mission.KillCount, mission.KillObject.GroupName);
                    }
                    else
                    {
                        MainMissionUI.Initialize("エネミーがNullです");
                        MainMissionUI.UpdateText();
                    }
                }
                else
                {
                    MainMissionUI.Initialize("全て倒せ!");
                    MainMissionUI.UpdateText();
                }
                break;
            case MainMissionPreset.StageMission.Collect:
                if (mission.CollectObject != null)
                {
                    MainMissionUI.Initialize("{1}を{0}個集めろ!");
                    MainMissionUI.UpdateText(mission.CollectCount, mission .CollectObject.name);
                }
                else
                {
                    MainMissionUI.Initialize("収集物がNullです。");
                    MainMissionUI.UpdateText();
                }
                break;
        }

    }

    public void DrawSubMissionUIs(SubMissionPreset mission, int uiIndex)
    {
        switch (mission.presetType)
        {
            case SubMissionPreset.MissionType.ClearTime:
                int minutes = mission.TimeCount / 60;
                int seconds = mission.TimeCount % 60;

                SubMissionUIs[uiIndex].Initialize("{0}:{1}以内にクリア!");
                SubMissionUIs[uiIndex].UpdateText($"{minutes: 00}", $"{seconds: 00}");
                break;
            case SubMissionPreset.MissionType.KillCount:
                if (mission.killType == SubMissionPreset.KillConditionType.SpecificEnemy)
                {
                    SubMissionUIs[uiIndex].Initialize("{1}を{0}体倒せ!");
                    SubMissionUIs[uiIndex].UpdateText(mission.ObjectCount, mission.EnemyObject.GroupName);
                }
                else
                {
                    SubMissionUIs[uiIndex].Initialize("全て倒せ!");
                    SubMissionUIs[uiIndex].UpdateText();
                }
                break;
            case SubMissionPreset.MissionType.TackleCountLimit:
                SubMissionUIs[uiIndex].Initialize("タックル{0}回までにクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.TackleCountOverthan:
                SubMissionUIs[uiIndex].Initialize("タックル{0}回以上でクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountLimit:
                SubMissionUIs[uiIndex].Initialize("ジャンプ{0}回までにクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountOverthan:
                SubMissionUIs[uiIndex].Initialize("ジャンプ{0}回以上でクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountOverthan:
                SubMissionUIs[uiIndex].Initialize("ブロックを{0}個壊せ!");
                SubMissionUIs[uiIndex].UpdateText(mission.ObjectCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountLimit:
                SubMissionUIs[uiIndex].Initialize("ブロックを{0}個まで壊すな!");
                SubMissionUIs[uiIndex].UpdateText(mission.ObjectCount);
                break;
            case SubMissionPreset.MissionType.HPSaving:
                SubMissionUIs[uiIndex].Initialize("HP {0}% 以上でクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.PlayerHP);
                break;
        }

    }

    public void DrawBestTime(int stageID)
    {
        StageBestTimeViewr.Instance.StageTimerView(stageID);
    }

    public void CheakMissionClearStar(int stageID)
    {
        SubMissionUIs[0].SetClear(SaveSystem.GetSubMission1Clear(stageID));
        SubMissionUIs[1].SetClear(SaveSystem.GetSubMission2Clear(stageID));
        SubMissionUIs[2].SetClear(SaveSystem.GetSubMission3Clear(stageID));        
    }
}
