using System.Globalization;
using UnityEngine;

namespace SlimeBreaker
{
    public enum UpgradeRarity
    {
        Normal,
        Rare,
        Legend,
        Ultra
    }

    /// <summary>
    /// Level-up card rarities. Every upgrade kind rolls one: Normal (green) +0.1%, Rare (blue) +0.3%,
    /// Legend (purple) +0.5%, Ultra (orange) +1%, with chances 65 / 20 / 10 / 5%.
    /// </summary>
    public static class UpgradeRarities
    {
        public static readonly int[] ChancePercent = { 65, 20, 10, 5 };
        public static readonly float[] BonusPercent = { 0.1f, 0.3f, 0.5f, 1f };

        public static string Name(UpgradeRarity rarity) => rarity switch
        {
            UpgradeRarity.Rare => "RARE",
            UpgradeRarity.Legend => "LEGEND",
            UpgradeRarity.Ultra => "ULTRA",
            _ => "NORMAL"
        };

        public static Color Color(UpgradeRarity rarity) => rarity switch
        {
            UpgradeRarity.Rare => new Color(0.25f, 0.55f, 1f),
            UpgradeRarity.Legend => new Color(0.66f, 0.36f, 1f),
            UpgradeRarity.Ultra => new Color(1f, 0.55f, 0.12f),
            _ => new Color(0.3f, 0.82f, 0.38f)
        };

        /// <summary>Bonus as a fraction (0.001 for +0.1%).</summary>
        public static float Fraction(UpgradeRarity rarity) => BonusPercent[(int)rarity] * Difficulty.Profile.upgradeBonus / 100f;

        /// <summary>Roll a rarity from a 0..1 value (UnityEngine.Random.value in play).</summary>
        public static UpgradeRarity FromRoll(float roll01)
        {
            float point = Mathf.Clamp01(roll01) * 100f;
            float edge = 0f;
            for (int i = 0; i < ChancePercent.Length; i++)
            {
                edge += ChancePercent[i];
                if (point < edge)
                    return (UpgradeRarity)i;
            }
            return UpgradeRarity.Normal;
        }

        public static string Percent(UpgradeRarity rarity) => (BonusPercent[(int)rarity] * Difficulty.Profile.upgradeBonus).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        public static string Describe(UpgradeKind kind, UpgradeRarity rarity)
        {
            string p = Percent(rarity);
            return kind switch
            {
                UpgradeKind.Damage => "+" + p + " magic damage",
                UpgradeKind.Speed => "+" + p + " move speed",
                UpgradeKind.Health => "+" + p + " max HP",
                UpgradeKind.AttackRate => "-" + p + " attack cooldown",
                UpgradeKind.Dash => "-" + p + " dash cooldown",
                UpgradeKind.Range => "+" + p + " spell range",
                UpgradeKind.Xp => "+" + p + " EXP gain",
                UpgradeKind.Armor => "-" + p + " damage taken",
                UpgradeKind.Lifesteal => "+" + p + " lifesteal",
                UpgradeKind.CritChance => "+" + p + " critical chance",
                UpgradeKind.CritDamage => "+" + p + " critical damage",
                _ => "+" + p
            };
        }
    }

    /// <summary>One level-up card: an upgrade kind plus its rolled rarity.</summary>
    public struct UpgradeOffer
    {
        public UpgradeDefinition definition;
        public UpgradeRarity rarity;

        public UpgradeOffer(UpgradeDefinition definition, UpgradeRarity rarity)
        {
            this.definition = definition;
            this.rarity = rarity;
        }

        public bool IsValid => definition != null;
        public float Fraction => UpgradeRarities.Fraction(rarity);
        public string Title => definition != null ? definition.title : string.Empty;
        public string Description => definition != null ? UpgradeRarities.Describe(definition.kind, rarity) : string.Empty;
    }
}
