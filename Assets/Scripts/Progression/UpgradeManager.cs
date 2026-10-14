using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    public class UpgradeManager : MonoBehaviour
    {
        public UpgradeDefinition[] pool;

        public UpgradeDefinition[] RollThree()
        {
            var source = new List<UpgradeDefinition>();
            if (pool != null)
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i] != null)
                        source.Add(pool[i]);
                }
            }
            for (int i = source.Count - 1; i > 0; i--)
            {
                int swap = Random.Range(0, i + 1);
                (source[i], source[swap]) = (source[swap], source[i]);
            }
            int count = Mathf.Min(3, source.Count);
            var result = new UpgradeDefinition[count];
            for (int i = 0; i < count; i++)
                result[i] = source[i];
            return result;
        }

        public void Apply(UpgradeDefinition upgrade, RunStats stats)
        {
            if (upgrade == null || stats == null)
                return;
            switch (upgrade.kind)
            {
                case UpgradeKind.Damage:
                    stats.damage = Mathf.Max(1, Mathf.RoundToInt(stats.damage * (1f + upgrade.amount)));
                    break;
                case UpgradeKind.Speed:
                    stats.moveSpeed *= 1f + upgrade.amount;
                    break;
                case UpgradeKind.Health:
                    stats.maxHealth += Mathf.RoundToInt(upgrade.amount);
                    PlayerHealth.Instance?.SetMaxHealth(stats.maxHealth, Mathf.RoundToInt(upgrade.amount));
                    break;
                case UpgradeKind.AttackRate:
                    stats.attackCooldown = Mathf.Max(0.18f, stats.attackCooldown * (1f - upgrade.amount));
                    break;
                case UpgradeKind.Dash:
                    stats.dashCooldown = Mathf.Max(0.35f, stats.dashCooldown * (1f - upgrade.amount));
                    break;
                case UpgradeKind.Range:
                    stats.attackRange *= 1f + upgrade.amount;
                    break;
                case UpgradeKind.Xp:
                    stats.xpMultiplier += upgrade.amount;
                    break;
                case UpgradeKind.Armor:
                    stats.damageReduction = Mathf.Clamp(stats.damageReduction + upgrade.amount, 0f, 0.6f);
                    break;
            }
        }
    }
}
