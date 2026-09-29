using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DataBase/StageDataBase")]
public class StageDataBase : ScriptableObject
{
    public List<StageData> stages;
}
