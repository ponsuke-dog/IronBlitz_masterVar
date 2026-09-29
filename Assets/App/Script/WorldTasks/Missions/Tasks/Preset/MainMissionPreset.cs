using UnityEngine;

[System.Serializable]
public class MainMissionPreset
{
    public MainMissionPreset Instance { get; private set; }
    public enum StageMission
    {
        Goal,
        Kill,
        Collect,
    }

    // KillCountŽž‚Ì”»•Ê
    public enum KillConditionType
    {
        ALLEnemy,       // ‘S‚Ä‚Ì“G
        SpecificEnemy,  // “Á’è‚Ì“G
    }

    public StageMission presetType = StageMission.Goal;
    public KillConditionType killType = KillConditionType.ALLEnemy;

    public bool GoalFlg = false;
    public int KillCount = 0;
    public int CollectCount = 0;
    public GameObject CollectObject;
    public EnemyData KillObject;
}
