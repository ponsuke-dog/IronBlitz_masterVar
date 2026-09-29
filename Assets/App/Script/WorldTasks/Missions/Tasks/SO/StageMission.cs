using UnityEngine;

[CreateAssetMenu(menuName = "Mission/StageMission")]
public class StageMission : ScriptableObject
{

    [Header("ステージの大目標")]
    [SerializeField] public MainMissionPreset MainMissionPreset;
    bool StageClearFlg = false;

    [Header("ステージのサブ目標")]
    [SerializeField] public SubMissionPreset SubMissionPreset1;
    [SerializeField] public SubMissionPreset SubMissionPreset2;
    [SerializeField] public SubMissionPreset SubMissionPreset3;

    public void ClearStage()
    {
        StageClearFlg = true;
    }
}
