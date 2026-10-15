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
        public float attackRange = 2.15f;
        public float dashCooldown = 1.25f;
        public float xpMultiplier = 1f;
        public float damageReduction;
        public int level = 1;
    }
}
