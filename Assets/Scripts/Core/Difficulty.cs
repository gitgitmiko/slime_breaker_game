using UnityEngine;

namespace SlimeBreaker
{
    public enum DifficultyLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>Multipliers for one difficulty. Hard is the original balance (everything 1, drops 5%).</summary>
    public struct DifficultyProfile
    {
        public float monsterHealth;
        public float monsterDamage;
        /// <summary>Multiplier on the time between spawns (above 1 = fewer monsters).</summary>
        public float spawnInterval;
        public float bossHealth;
        public float bossDamage;
        /// <summary>Hero starting max HP.</summary>
        public float heroHealth;
        /// <summary>Multiplier on the percentage a level-up card adds.</summary>
        public float upgradeBonus;
        /// <summary>Chance that a slime drops an item.</summary>
        public float dropChance;
        public float xp;
    }

    /// <summary>
    /// The difficulty of the current run. It is chosen in the main menu, stored in the run save (so Continue keeps the
    /// difficulty the run started on) and read by the systems that scale monsters, bosses, level-ups and loot.
    /// Hard is the default in code so tests and tools see the original numbers unless a run sets another level.
    /// </summary>
    public static class Difficulty
    {
        public static DifficultyLevel Level { get; private set; } = DifficultyLevel.Hard;

        public static void Set(DifficultyLevel level)
        {
            Level = (DifficultyLevel)Mathf.Clamp((int)level, 0, 2);
        }

        public static void Set(int level) => Set((DifficultyLevel)level);

        public static DifficultyProfile Profile => For(Level);

        public static DifficultyProfile For(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy:
                    return new DifficultyProfile
                    {
                        monsterHealth = 0.7f, monsterDamage = 0.5f, spawnInterval = 1.35f, bossHealth = 0.5f, bossDamage = 0.5f,
                        heroHealth = 1.5f, upgradeBonus = 3f, dropChance = 0.15f, xp = 1.5f
                    };
                case DifficultyLevel.Normal:
                    return new DifficultyProfile
                    {
                        monsterHealth = 0.85f, monsterDamage = 0.75f, spawnInterval = 1.15f, bossHealth = 0.75f, bossDamage = 0.75f,
                        heroHealth = 1.25f, upgradeBonus = 2f, dropChance = 0.08f, xp = 1.25f
                    };
                default:
                    return new DifficultyProfile
                    {
                        monsterHealth = 1f, monsterDamage = 1f, spawnInterval = 1f, bossHealth = 1f, bossDamage = 1f,
                        heroHealth = 1f, upgradeBonus = 1f, dropChance = 0.05f, xp = 1f
                    };
            }
        }

        public static string Label(DifficultyLevel level) => level.ToString();

        /// <summary>One line for the menu: what changes compared with Hard.</summary>
        public static string Summary(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy:
                    return "Damage monster 50% · HP hero +50% · kartu ×3 · drop 15%";
                case DifficultyLevel.Normal:
                    return "Damage monster 75% · HP hero +25% · kartu ×2 · drop 8%";
                default:
                    return "Seimbang seperti aslinya: tantangan penuh";
            }
        }

        public static int Scale(int value, float multiplier) => Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
    }
}
