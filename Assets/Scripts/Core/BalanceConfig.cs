using System;
using UnityEngine;

namespace SlimeBreaker
{
    [Serializable]
    public class SpawnPhase
    {
        public float untilSeconds = 60f;
        public float interval = 2.2f;
        public int normal = 1;
        public int fast;
        public int tank;
    }

    [CreateAssetMenu(fileName = "Balance", menuName = "Slime Breaker/Balance")]
    public class BalanceConfig : ScriptableObject
    {
        public int baseDamage = 12;
        public float baseMoveSpeed = 5.2f;
        public int baseHealth = 100;
        public float baseAttackCooldown = 0.48f;
        public float baseAttackRange = 2.15f;
        public float baseDashCooldown = 1.25f;
        public float dashDuration = 0.16f;
        public float dashSpeed = 16f;
        public float bossTime = 300f;
        public int maxAliveEnemies = 22;
        public int victoryGold = 40;
        public int defeatGold = 12;
        public int goldPerLevel = 4;
        public int permanentBaseCost = 30;
        public SpawnPhase[] phases;
    }
}
