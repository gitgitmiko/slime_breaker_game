using System;

namespace SlimeBreaker
{
    [Serializable]
    public class RunStats
    {
        public int damage = 12;
        public float moveSpeed = 5.2f;
        public int maxHealth = 100;
        public float attackCooldown = 0.48f;
        public float attackRange = 4.4f;
        public float dashCooldown = 1.25f;
        public float xpMultiplier = 1f;
        public float damageReduction;
        public float lifesteal;
        /// <summary>Zero until the run takes Crit Rate or Critical. No crits before that.</summary>
        public float critChance;
        public float critMultiplier = 1.5f;
        public int level = 1;

        public bool HasCrit => critChance > 0.0001f;
        public float AttacksPerSecond => attackCooldown <= 0.01f ? 0f : 1f / attackCooldown;

        public bool TryLevelUp()
        {
            if (level >= RunRules.MaxHeroLevel)
                return false;
            level++;
            damage += RunRules.HeroDamagePerLevel;
            return true;
        }
    }
}
