using UnityEngine;

namespace SlimeBreaker
{
    /// <summary>Hero damage rolls: crit chance and multiplier from run upgrades.</summary>
    public static class CombatMath
    {
        public static int ApplyCrit(int baseDamage, out bool crit)
        {
            crit = false;
            int amount = Mathf.Max(1, baseDamage);
            var stats = GameFlowController.Instance != null ? GameFlowController.Instance.Stats : null;
            if (stats == null)
                return amount;
            crit = Random.value < Mathf.Clamp01(stats.critChance);
            if (crit)
                amount = Mathf.Max(1, Mathf.RoundToInt(amount * Mathf.Max(1.1f, stats.critMultiplier)));
            return amount;
        }
    }
}
