using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DataBase/SceneDataBase")]
public class SceneDataBase : ScriptableObject
{
    [SerializeField]
    private SceneData[] scenes;

    private Dictionary<string, SceneData> map;

    private void OnEnable()
    {
        map = new Dictionary<string, SceneData>();

        foreach (var scene in scenes)
        {
            if (scene == null)
            {
                continue;
            }

            map[scene.sceneName] = scene;
        }
    }

    public SceneData GetScene(string sceneName)
    {
        if (map.TryGetValue(sceneName,out var data))
        {
            return data;
        }
        Debug.Log($"SceneData not found : {sceneName}");
        return null;
    }
}
