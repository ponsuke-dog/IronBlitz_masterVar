using UnityEngine;

public class ResultButtonHandller : MonoBehaviour
{
    public void Retry()
    {
        StageClearManager.Instance.Retry();
    }
    public void NextStage()
    {
        StageClearManager.Instance.ChangeNextStage();
    }
    public void StageMap()
    {
        StageClearManager.Instance.ChangeStageMap();
    }
}
