using UnityEngine;

[CreateAssetMenu(menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    [Header("ステージ情報")]
    [SerializeField] public int stageID = 0;
    [SerializeField]public string stageName;
    [SerializeField] public SceneData sceneData;

    public StagePreviewSet stagePreview;

    [Header("次のステージ")]
    [SerializeField] public SceneData NextStage;

    [Header("ステージ選択マップ")]
    [SerializeField] public SceneData StageMap;

    [Header("このステージのミッション")]
    [SerializeField] public StageMission stageMission;
}
