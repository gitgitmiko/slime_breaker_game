namespace SlimeBreaker
{
    /// <summary>
    /// Shared run limits: one large map, 100 floors, hero levels to 999,
    /// a power-up every five hero levels, and a boss every ten floors.
    /// </summary>
    public static class RunRules
    {
        public const int MaxHeroLevel = 999;
        public const int MaxFloor = 100;
        public const int BossEvery = 10;
        public const int PowerUpEvery = 5;
        public const int BossHealthPerTier = 10000;
        public const float BossEnrageHealth = 0.3f;
        public const float ArenaRadius = 22f;

        public static bool IsBossFloor(int floor)
        {
            return floor > 0 && floor <= MaxFloor && floor % BossEvery == 0;
        }

        public static int BossTier(int floor)
        {
            if (!IsBossFloor(floor))
                return 0;
            return floor / BossEvery;
        }

        /// <summary>Regular slimes to clear before the floor advances. Boss floors skip this quota.</summary>
        public static int KillQuota(int floor)
        {
            if (IsBossFloor(floor))
                return 0;
            return 8 + UnityEngine.Mathf.Min(floor, 16);
        }
    }
}
