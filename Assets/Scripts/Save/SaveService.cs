using System;
using System.IO;
using UnityEngine;

namespace SlimeBreaker
{
    public static class SaveService
    {
        public const int CurrentSchema = 2;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "slimebreaker-save.json");

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return new SaveData();
                return ParseOrDefault(File.ReadAllText(SavePath));
            }
            catch (Exception error)
            {
                Debug.LogWarning("Save rusak, memakai data default. " + error.Message);
                return new SaveData();
            }
        }

        public static SaveData ParseOrDefault(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SaveData();

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch
            {
                return new SaveData();
            }

            if (data == null || data.schemaVersion <= 0)
                return new SaveData();

            data.gold = Mathf.Max(0, data.gold);
            data.damageRank = Mathf.Max(0, data.damageRank);
            data.healthRank = Mathf.Max(0, data.healthRank);
            data.speedRank = Mathf.Max(0, data.speedRank);
            data.bestRunTime = Mathf.Max(0f, data.bestRunTime);
            if (data.schemaVersion < 2)
                data.graphicsLevel = 2;
            data.graphicsLevel = Mathf.Clamp(data.graphicsLevel, 0, 2);
            data.musicLevel = Mathf.Clamp01(data.musicLevel);
            data.sfxLevel = Mathf.Clamp01(data.sfxLevel);
            data.difficulty = Mathf.Clamp(data.difficulty, 0, 2);
            data.bossesDefeated = Mathf.Clamp(data.bossesDefeated, 0, RunRules.MaxFloor / RunRules.BossEvery);
            return data;
        }

        public static void Save(SaveData data)
        {
            if (data == null)
                data = new SaveData();
            data.schemaVersion = CurrentSchema;
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data));
            if (File.Exists(SavePath))
                File.Delete(SavePath);
            File.Move(temporary, SavePath);
        }
    }
}
