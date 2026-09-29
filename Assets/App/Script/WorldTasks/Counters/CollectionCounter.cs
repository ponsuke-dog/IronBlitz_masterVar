using System.Collections.Generic;
using UnityEngine;

public class CollectionCounter : MonoBehaviour
{
   private Dictionary<EnemyData, int> CollectCount = new();

   // public int TotalCollectCount { get; private set; }

   //public void AddCollectionCount(EnemyData enemy)
   // {
   //     TotalCollectCount++;

   //     if (!CollectCount.ContainsKey(enemy))
   //     {
   //         killCount[enemy] = 0;
   //     }

   //     killCount[enemy]++;
   // }

   // public int GetKillCount(EnemyData enemy)
   // {
   //     if (killCount.TryGetValue(enemy, out int count))
   //     {
   //         return count;
   //     }
   //     return 0;
   // }
}
