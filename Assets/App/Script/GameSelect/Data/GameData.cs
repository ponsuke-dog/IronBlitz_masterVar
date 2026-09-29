using UnityEngine;

public static class GameData
{
    public static StageData CurrentStage;

    public static void SetCurrentStageData(StageData data)
    {
        CurrentStage = data;
    }
}