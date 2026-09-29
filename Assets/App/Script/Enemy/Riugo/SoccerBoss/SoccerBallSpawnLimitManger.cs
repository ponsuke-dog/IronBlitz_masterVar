using System.Collections.Generic;
using UnityEngine;

public class SoccerBallSpawnLimitManager : MonoBehaviour
{
    public static SoccerBallSpawnLimitManager Instance { get; private set; }

    [Header("Limit")]
    [SerializeField] private int maxCount = 3;

    [Header("Debug")]
    [SerializeField] private bool logRegister = true;

    private readonly HashSet<GameObject> spawnedObjects = new HashSet<GameObject>();

    public int CurrentCount
    {
        get
        {
            CleanupNullObjects();
            return spawnedObjects.Count;
        }
    }

    public int MaxCount => maxCount;

    public bool CanSpawn(int requestCount = 1)
    {
        CleanupNullObjects();

        return spawnedObjects.Count + Mathf.Max(1, requestCount) <= maxCount;
    }

    public int GetRemainingCount()
    {
        CleanupNullObjects();

        return Mathf.Max(0, maxCount - spawnedObjects.Count);
    }

    public bool TryReserveSpawnCount(int requestCount, out int allowedCount, bool spawnOnlyRemainingCount)
    {
        CleanupNullObjects();

        requestCount = Mathf.Max(1, requestCount);

        int remaining = GetRemainingCount();

        if (remaining <= 0)
        {
            allowedCount = 0;
            return false;
        }

        if (spawnOnlyRemainingCount)
        {
            allowedCount = Mathf.Min(requestCount, remaining);
            return allowedCount > 0;
        }

        if (requestCount > remaining)
        {
            allowedCount = 0;
            return false;
        }

        allowedCount = requestCount;
        return true;
    }

    public void Register(GameObject obj)
    {
        if (obj == null)
            return;

        CleanupNullObjects();

        spawnedObjects.Add(obj);

        if (logRegister)
        {
            Debug.Log(
                $"{name} Register: {obj.name} " +
                $"count:{spawnedObjects.Count}/{maxCount}"
            );
        }
    }

    public void Unregister(GameObject obj)
    {
        if (obj == null)
            return;

        spawnedObjects.Remove(obj);

        if (logRegister)
        {
            Debug.Log(
                $"{name} Unregister: {obj.name} " +
                $"count:{spawnedObjects.Count}/{maxCount}"
            );
        }
    }

    private void CleanupNullObjects()
    {
        spawnedObjects.RemoveWhere(obj => obj == null);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                $"{name}: SoccerSpawnLimitManager が複数あります。古い方を残し、このObjectを無効扱いにします。"
            );

            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}