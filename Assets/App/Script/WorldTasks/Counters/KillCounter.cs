using System.Collections.Generic;
using UnityEngine;

public class KillCounter
{
    private Dictionary<EnemyData, int> killCount = new();
    public int TotalKillCount { get; private set; }

    public void AddKillCount(EnemyData enemy)
    {
        TotalKillCount++;

        if (!killCount.ContainsKey(enemy))
        {
            killCount[enemy] = 0;
        }

        killCount[enemy]++;
    }

    public int GetKillCount(EnemyData enemy)
    {
        if (killCount.TryGetValue(enemy, out int count))
        {
            return count;
        }
        return 0;
    }
}
