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
        public int floor = 1;
        public int floorKills;
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
            int floorValue = UnityEngine.Mathf.Max(1, floor > 0 ? floor : stage);
            int seed = themeSeed != 0 ? themeSeed : (runSeed != 0 ? runSeed : 1);
            return ArenaTheme.ForFloor(floorValue, seed);
        }
    }
}
