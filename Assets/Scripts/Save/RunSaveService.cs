using System;
using System.IO;
using UnityEngine;

namespace SlimeBreaker
{
    public static class RunSaveService
    {
        public const int CurrentSchema = 1;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "slimebreaker-run.json");

        /// <summary>Set by the menu Continue button; the arena reads <see cref="PeekResume"/> then forgets it.</summary>
        public static bool ResumeRequested { get; set; }

        static RunSave cached;

        public static bool Exists()
        {
            try
            {
                return File.Exists(SavePath);
            }
            catch
            {
                return false;
            }
        }

        public static RunSave Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return null;
                var data = JsonUtility.FromJson<RunSave>(File.ReadAllText(SavePath));
                if (data == null || data.schemaVersion <= 0 || data.level <= 0)
                    return null;
                if (data.stats == null)
                    data.stats = new RunStats();
                data.level = Mathf.Max(1, data.level);
                data.stage = Mathf.Max(1, data.stage);
                data.floor = Mathf.Max(1, data.floor > 0 ? data.floor : data.stage);
                data.floorKills = Mathf.Max(0, data.floorKills);
                data.hp = Mathf.Max(0, data.hp);
                data.xp = Mathf.Max(0, data.xp);
                data.elapsed = Mathf.Max(0f, data.elapsed);
                data.skillKinds ??= Array.Empty<int>();
                data.skillLevels ??= Array.Empty<int>();
                return data;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Run save rusak. " + error.Message);
                return null;
            }
        }

        public static RunSave PeekResume()
        {
            if (!ResumeRequested)
                return null;
            cached ??= Load();
            return cached;
        }

        public static void FinishResume()
        {
            ResumeRequested = false;
            cached = null;
        }

        public static void Save(RunSave data)
        {
            if (data == null)
                return;
            data.schemaVersion = CurrentSchema;
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            var temporary = SavePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath))
                File.Delete(SavePath);
            File.Move(temporary, SavePath);
        }

        public static void Clear()
        {
            ResumeRequested = false;
            cached = null;
            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Gagal menghapus run save. " + error.Message);
            }
        }
    }
}
