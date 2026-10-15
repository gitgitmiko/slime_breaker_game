namespace SlimeBreaker
{
    /// <summary>
    /// Shared run limits: 100 floors, hero levels to 999, a power-up every five
    /// hero levels, a biome every ten floors, and a boss every ten floors.
    /// </summary>
    public static class RunRules
    {
        public const int MaxHeroLevel = 999;
        public const int MaxFloor = 100;
        public const int BossEvery = 10;
        public const int PowerUpEvery = 5;
        public const int BossHealthPerTier = 10000;
        public const float BossEnrageHealth = 0.3f;
        public const int BossBasicDamage = 50;
        public const int BossSkillDamage = 100;
        public const int BossEnrageDamage = 200;
        public const int KillsPerFloorStep = 10;
        public const int HeroDamagePerLevel = 10;
        public const int SlimeDamagePerFloor = 1;
        public const int BossDamagePerFloor = 3;
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

        /// <summary>Regular slimes to clear before the floor advances. Floor N needs N×10 kills. Boss floors skip this quota.</summary>
        public static int KillQuota(int floor)
        {
            if (IsBossFloor(floor))
                return 0;
            return UnityEngine.Mathf.Max(1, floor) * KillsPerFloorStep;
        }

        public static int SlimeContactDamage(int baseDamage, int floor)
        {
            return UnityEngine.Mathf.Max(1, baseDamage + UnityEngine.Mathf.Max(0, floor - 1) * SlimeDamagePerFloor);
        }

        public static int BossContactDamage(int floor)
        {
            return BossBasicDamage + UnityEngine.Mathf.Max(0, floor - 1) * BossDamagePerFloor;
        }

        public static int BossSkillDamageAt(int floor, bool enraged)
        {
            int baseline = enraged ? BossEnrageDamage : BossSkillDamage;
            return baseline + UnityEngine.Mathf.Max(0, floor - 1) * BossDamagePerFloor;
        }
    }
}
