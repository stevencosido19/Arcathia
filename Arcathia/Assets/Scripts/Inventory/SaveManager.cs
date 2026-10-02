using UnityEngine;
using System.IO;

public static class SaveManager
{
    private static string SavePath => Application.persistentDataPath + "/playerData.json";

    public static PlayerSaveData LoadData()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<PlayerSaveData>(json);
        }
        return new PlayerSaveData(); // Returns default data (1000 currency)
    }

    public static void SaveData(PlayerSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }
}