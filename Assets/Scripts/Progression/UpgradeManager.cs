using System.Collections.Generic;
using UnityEngine;

namespace SlimeBreaker
{
    public class UpgradeManager : MonoBehaviour
    {
        public UpgradeDefinition[] pool;

        /// <summary>Three different upgrade kinds, each with its own rolled rarity.</summary>
        public UpgradeOffer[] RollThree()
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
            var result = new UpgradeOffer[count];
            for (int i = 0; i < count; i++)
                result[i] = new UpgradeOffer(source[i], UpgradeRarities.FromRoll(Random.value));
            return result;
        }

        /// <summary>Apply a rarity card: every kind grows by the rarity's percentage (0.1 / 0.3 / 0.5 / 1%).</summary>
        public void Apply(UpgradeOffer offer, RunStats stats)
        {
            if (!offer.IsValid || stats == null)
                return;
            float p = offer.Fraction;
            switch (offer.definition.kind)
            {
                case UpgradeKind.Damage:
                {
                    // Integer damage keeps the fractional part so many small Normal cards still add up.
                    float exact = (stats.damage + stats.damageCarry) * (1f + p);
                    stats.damage = Mathf.Max(1, Mathf.FloorToInt(exact));
                    stats.damageCarry = exact - stats.damage;
                    break;
                }
                case UpgradeKind.Health:
                {
                    float exact = (stats.maxHealth + stats.healthCarry) * (1f + p);
                    int before = stats.maxHealth;
                    stats.maxHealth = Mathf.Max(1, Mathf.FloorToInt(exact));
                    stats.healthCarry = exact - stats.maxHealth;
                    PlayerHealth.Instance?.SetMaxHealth(stats.maxHealth, stats.maxHealth - before);
                    break;
                }
                case UpgradeKind.Speed:
                    stats.moveSpeed *= 1f + p;
                    break;
                case UpgradeKind.AttackRate:
                    stats.attackCooldown = Mathf.Max(0.18f, stats.attackCooldown * (1f - p));
                    break;
                case UpgradeKind.Dash:
                    stats.dashCooldown = Mathf.Max(0.35f, stats.dashCooldown * (1f - p));
                    break;
                case UpgradeKind.Range:
                    stats.attackRange *= 1f + p;
                    break;
                case UpgradeKind.Xp:
                    stats.xpMultiplier += p;
                    break;
                case UpgradeKind.Armor:
                    stats.damageReduction = Mathf.Clamp(stats.damageReduction + p, 0f, 0.6f);
                    break;
                case UpgradeKind.Lifesteal:
                    stats.lifesteal = Mathf.Clamp(stats.lifesteal + p, 0f, 0.4f);
                    break;
                case UpgradeKind.CritChance:
                    stats.critChance = Mathf.Clamp(stats.critChance + p, 0f, 0.7f);
                    break;
                case UpgradeKind.CritDamage:
                    stats.critMultiplier = Mathf.Clamp(stats.critMultiplier + p, 1.1f, 3.5f);
                    // Taking Critical without Crit Rate still unlocks crits, with a modest base chance.
                    if (stats.critChance <= 0f)
                        stats.critChance = 0.08f;
                    break;
            }
        }

        /// <summary>Legacy flat application using the definition's own amount (tools and tests).</summary>
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
                case UpgradeKind.Lifesteal:
                    stats.lifesteal = Mathf.Clamp(stats.lifesteal + upgrade.amount, 0f, 0.4f);
                    break;
                case UpgradeKind.CritChance:
                    stats.critChance = Mathf.Clamp(stats.critChance + upgrade.amount, 0f, 0.7f);
                    break;
                case UpgradeKind.CritDamage:
                    stats.critMultiplier = Mathf.Clamp(stats.critMultiplier + upgrade.amount, 1.1f, 3.5f);
                    // Taking Critical without Crit Rate still unlocks crits, with a modest base chance.
                    if (stats.critChance <= 0f)
                        stats.critChance = 0.08f;
                    break;
            }
        }
    }
}
