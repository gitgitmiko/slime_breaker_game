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
        /// <summary>0 = Low, 1 = Medium, 2 = High. Default High for new installs.</summary>
        public int graphicsLevel = 2;
        /// <summary>False = fixed joystick (default); true = free/floating joystick.</summary>
        public bool freeJoystick;
    }
}
