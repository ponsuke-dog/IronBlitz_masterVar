using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
[CreateAssetMenu(menuName = "SceneData/SceneData")]
public class SceneData : ScriptableObject
{
#if UNITY_EDITOR
    [Header("シーンアセット")]
    [Tooltip("対応するシーンを入れる")]
    public SceneAsset sceneAsset;
#endif
    [Tooltip("自動入力されるが、一応の枠")]
    public string sceneName;

    [Header("このシーンから出る時の暗転フェード")]
    [SerializeField] public FadePreset ExitFade;

    [Header("このシーンに入る時の明転フェード")]
    [SerializeField] public FadePreset EnterFade;
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sceneAsset!=null)
        {
            sceneName = sceneAsset.name;
        }
    }
#endif
}
