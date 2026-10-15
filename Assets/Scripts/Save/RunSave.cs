using System;

namespace SlimeBreaker
{
    /// <summary>Snapshot of an in-progress run so the player can continue after closing the game.</summary>
    [Serializable]
    public class RunSave
    {
        public int schemaVersion = 1;
        public int level = 1;
        public int stage = 1;
        public int runSeed;
        public float elapsed;
        public int xp;
        public int hp = 100;
        public RunStats stats = new RunStats();
        public int[] skillKinds = Array.Empty<int>();
        public int[] skillLevels = Array.Empty<int>();
        public bool intermission;
        public int biome;
        public int weather;
        public int time;
        public int themeSeed;

        public ArenaTheme ToTheme()
        {
            var biomeValue = (Biome)UnityEngine.Mathf.Clamp(biome, 0, 2);
            var weatherValue = (Weather)UnityEngine.Mathf.Clamp(weather, 0, 3);
            var timeValue = (TimeOfDay)UnityEngine.Mathf.Clamp(time, 0, 1);
            return new ArenaTheme(biomeValue, weatherValue, timeValue, themeSeed != 0 ? themeSeed : runSeed);
        }
    }
}
