using System.IO;
using UnityEngine;

public static class SaveSystem
{

    private static bool initialized = false;

    // セーブする場所
    private static string saveDirectory => Path.Combine(Application.dataPath, "../Save");

    // セーブファイル
    private static string SavePath => Path.Combine(saveDirectory, "save.json"); 

    private static StageDataBase stageDataBase;

    private static SaveData currentData;

    public static int StageCount => stageDataBase.stages.Count;

    private static void Initialize()
    {
        // 既に初期化されていたらもうやらせない
        if (initialized)
        {
            return;
        }

        stageDataBase = Resources.Load<StageDataBase>("StageDataBase/StageDataBase");
 
        if (stageDataBase == null)
        {
            Debug.LogError("StageDataBase not found");
        }

        // フォルダ生成
        if (!Directory.Exists(saveDirectory))
        {
            // 既にディレクトリが作られているならそこが参照されるので複数回呼び出されても大丈夫
            Directory.CreateDirectory(saveDirectory);

            Debug.Log("Create Save Directoy");
        }
        initialized = true;
    }


    public static void Save(SaveData data)
    {
        Initialize();

        currentData = data;

        string json = JsonUtility.ToJson(data, true);

        File.WriteAllText(SavePath, json);

        Debug.Log("Save Complete");

    }

    public static SaveData Load()
    {
        Initialize();


        if (currentData != null)
        {
            return currentData;
        }

        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);


               
            currentData = JsonUtility.FromJson<SaveData>(json);

            ResizeStageArray(currentData);

            Save(currentData);

            return currentData;
        }

        // 初回時は自動的にセーブさせる
        currentData = CreateNewSaveData();

        Save(currentData);

        return currentData;
    }

    public static SaveData CreateNewSaveData()
    {
        SaveData data = new SaveData();

            
        data.stages = new StageSaveData[StageCount];

        for (int i = 0; i < StageCount; i++)
        {
            data.stages[i] = new StageSaveData();

            if (i == 0)
            {
                data.stages[i].isUnlock = true;
            }
        }
        return data;
    }

    private static void ResizeStageArray(SaveData data)
    {
        // セーブ内容が無かったら生成する
        if (data.stages == null)
        {
            data.stages = new StageSaveData[StageCount];

            // セーブ内容を生成
            for (int i = 0; i < StageCount; i++)
            {
                data.stages[i] = new StageSaveData();
            }
        }

        // ステージ数が変わらなかったらそのまま
        if (data.stages.Length == StageCount)
        {
            return;
        }

        int oldLength = data.stages.Length;

        System.Array.Resize(ref data.stages, StageCount);

        // 足りない分を生成させる
        for (int i = oldLength; i < StageCount; i++)
        {
            data.stages[i] = new StageSaveData();
        }

    }

    public static bool GetStageClear(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return false;
        }
        return Load().stages[stageID].isMainClear;
    }
    
    public static bool GetSubMission1Clear(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return false;
        }
        return Load().stages[stageID].SubMission1;
    }
    public static bool GetSubMission2Clear(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return false;
        }
        return Load().stages[stageID].SubMission2;
    }
    public static bool GetSubMission3Clear(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return false;
        }
        return Load().stages[stageID].SubMission3;
    }

    public static bool GetUnlock(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return false;
        }
        return Load().stages[stageID].isUnlock;
    }
    public static float GetClearBestTime(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return 0;
        }
        return Load().stages[stageID].ClearBestTime;
    }
    public static StageSaveData GetStageData(int stageID)
    {
        if (!IsValidStageID(stageID))
        {
            Debug.LogError($"存在しないステージIDじゃ! {stageID}");
            return null;
        }
        return Load().stages[stageID];
    }
    private static bool IsValidStageID(int stageID)
    {
        // 存在しているステージIDかどうかのチェック
        return stageID >= 0 && stageID < StageCount;
    }

    private static void DeleteSave()
    {
        Initialize();

        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }

        currentData = null;

        Debug.Log("Save Deleted");
    }

    public static void ResetSave()
    {
        // セーブデータの削除
        DeleteSave();

        // 削除後に新しくセーブデータを生成させる
        SaveData newData = CreateNewSaveData();

        Save(newData);
    }

    // スロットとか、別のセーブファイル作るならこっち

    //public static void Save(SaveData data, string fileName)
    //{
    //    Initialize();

    //    string json = JsonUtility.ToJson(data, true);

    //    File.WriteAllText(GetSavePath(fileName), json);

    //    Debug.Log("Save Complete");

    //}

    //public static SaveData Load(string fileName)
    //{
    //    Initialize();

    //    string path = GetSavePath(fileName);

    //    if (File.Exists(path))
    //    {
    //        string json = File.ReadAllText(path);

    //        return JsonUtility.FromJson<SaveData>(json);
    //    }

    //    // 初回時は自動的にセーブさせる
    //    SaveData data = CreateNewSaveData();

    //    Save(data, fileName);

    //    return data;
    //}

    //private static string GetSavePath(string filename)
    //{
    //    return Path.Combine(saveDirectory,filename + ".json");
    //}
}

