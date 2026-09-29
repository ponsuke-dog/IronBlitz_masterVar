using UnityEngine;

[System.Serializable]
public class SubMissionPreset
{
    public SubMissionPreset Instance { get; private set; }
    public enum MissionType
    {
        ClearTime,
        KillCount,
        CollectCount,
        TackleCountLimit,
        TackleCountOverthan,
        JumpCountLimit,
        JumpCountOverthan,
        BreakBlockCountLimit,
        BreakBlockCountOverthan,
        HPSaving,
    }

    // KillCountŽž‚Ì”»•Ê
    public enum KillConditionType
    {
        ALLEnemy,       // ‘S‚Ä‚Ì“G
        SpecificEnemy,  // “Á’è‚Ì“G
    }

    public MissionType presetType = MissionType.ClearTime;
    public KillConditionType killType = KillConditionType.ALLEnemy;

    public int TimeCount = 0;
    public int ObjectCount = 0;
    public int PlayerActionCount = 0;
    public float PlayerHP = 0f;
    public GameObject CollectObject;
    public EnemyData EnemyObject;
}
