using System;

namespace SlimeBreaker
{
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 1;
        public int gold;
        public int damageRank;
        public int healthRank;
        public int speedRank;
        public float bestRunTime;
    }
}
