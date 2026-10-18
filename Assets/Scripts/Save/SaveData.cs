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
        /// <summary>Music volume 0..1 (BGM). Default full; the mix itself is already balanced.</summary>
        public float musicLevel = 1f;
        /// <summary>Game sound volume 0..1 (effects and ambience).</summary>
        public float sfxLevel = 1f;
    }
}
