using System.Collections.Generic;
using UnityEngine;

public class ResultUIManager : MonoBehaviour
{
    public static ResultUIManager Instance { get; private set; }

    [Header("登録するミッションUIの１行")]
   // [SerializeField] private MissionUILine MainMissionUI;
    [SerializeField] private List<ResultUILine> SubMissionUIs;

    private void Awake()
    {
       // シングルトン化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

    }
    public void DrawSubMissionUIs(SubMissionRunTime mission,int uiIndex)
    {
        switch (mission.presetSub.presetType)
        {
            case SubMissionPreset.MissionType.ClearTime:
                int minutes = mission.presetSub.TimeCount / 60;
                int seconds = mission.presetSub.TimeCount % 60;

                SubMissionUIs[uiIndex].Initialize("{0}:{1}以内にクリア!");
                SubMissionUIs[uiIndex].UpdateText($"{minutes: 00}", $"{seconds: 00}");
                break;
            case SubMissionPreset.MissionType.KillCount:
                if (mission.presetSub.killType == SubMissionPreset.KillConditionType.SpecificEnemy)
                {
                    SubMissionUIs[uiIndex].Initialize("{1}を{0}体倒せ!");
                    SubMissionUIs[uiIndex].UpdateText(mission.presetSub.ObjectCount, mission.presetSub.EnemyObject.GroupName);
                }
                else
                {
                    SubMissionUIs[uiIndex].Initialize("全て倒せ!");
                    SubMissionUIs[uiIndex].UpdateText();
                }
                break;
            case SubMissionPreset.MissionType.TackleCountLimit:
                SubMissionUIs[uiIndex].Initialize("タックル{0}回までにクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.TackleCountOverthan:
                SubMissionUIs[uiIndex].Initialize("タックル{0}回以上でクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountLimit:
                SubMissionUIs[uiIndex].Initialize("ジャンプ{0}回までにクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountOverthan:
                SubMissionUIs[uiIndex].Initialize("ジャンプ{0}回以上でクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.PlayerActionCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountOverthan:
                SubMissionUIs[uiIndex].Initialize("ブロックを{0}個壊せ!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.ObjectCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountLimit:
                SubMissionUIs[uiIndex].Initialize("ブロックを{0}個まで壊すな!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.ObjectCount);
                break;
            case SubMissionPreset.MissionType.HPSaving:
                SubMissionUIs[uiIndex].Initialize("HP {0}% 以上でクリア!");
                SubMissionUIs[uiIndex].UpdateText(mission.presetSub.PlayerHP);
                break;
        }
        if (mission.isClear)
        {
            SubMissionUIs[uiIndex].SetClear(true);
        }
        else
        {
            SubMissionUIs[uiIndex].SetClear(false);
        }
        ResultTimeViewr.Instance.ResultTimerView();
    }

    public void CallResult()
    {
    
        // ミッション表示を消す (リザルト画面で見せるため)
        MissionUIManager.Instance.SetUIRootFlg(false);
    }
}
