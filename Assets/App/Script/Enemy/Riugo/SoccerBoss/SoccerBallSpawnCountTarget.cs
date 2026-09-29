using UnityEngine;

public class SoccerBallSpawnCountTarget : MonoBehaviour
{
    private SoccerBallSpawnLimitManager manager;
    private bool registered;

    private void Start()
    {
        RegisterToManager(SoccerBallSpawnLimitManager.Instance);
    }
    private void OnDestroy()
    {
        Unregister();
    }

    public void RegisterToManager(SoccerBallSpawnLimitManager targetManager)
    {
        if (registered)
            return;

        manager = targetManager;

        if (manager == null)
        {
            Debug.LogWarning($"{name}: SoccerSpawnLimitManager Ç™SceneÇ…Ç†ÇËÇ‹ÇπÇÒÅB");
            return;
        }

        manager.Register(gameObject);
        registered = true;
    }

    private void Unregister()
    {
        if (!registered)
            return;

        if (manager != null)
            manager.Unregister(gameObject);

        registered = false;
    }
}