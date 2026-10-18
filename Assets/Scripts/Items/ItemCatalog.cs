using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SlimeBreaker
{
    public enum ItemSlot
    {
        Staff,
        Hat,
        Armor,
        Pants,
        Boots
    }

    /// <summary>One piece of equipment dropped by a monster. Serializable so it rides along in the run save.</summary>
    [Serializable]
    public class ItemData
    {
        public int id;
        public int slot;
        public int rarity;
        /// <summary>Floor band the item dropped in (floors 1–10 = 1, 11–20 = 2, …).</summary>
        public int band = 1;
        public int damage;
        public int health;
        public float defense;
        public float speed;

        public ItemSlot Slot => (ItemSlot)slot;
        public UpgradeRarity Rarity => (UpgradeRarity)rarity;
        public string Name => ItemCatalog.Name(Slot, Rarity);
        public string Stats => ItemCatalog.Describe(this);
        /// <summary>Single number for "is this better than what I wear" (same slot only).</summary>
        public float Power => damage + health + defense * 10000f + speed * 10000f;
    }

    /// <summary>
    /// Equipment rules. Staff = basic-attack damage, Hat = HP, Armor = HP + DEF, Pants = HP, Boots = move speed.
    /// Rarity chances match the level-up cards (65 / 20 / 10 / 5%). Stats scale with the floor band the item dropped
    /// in (item level = band): the best staff (Ultra) gives +1000 ATK on floors 1-10 (level 1), +2000 on floors 11-20 (level 2), and so on.
    /// </summary>
    public static class ItemCatalog
    {
        public const int SlotCount = 5;
        /// <summary>Hard-difficulty slime drop chance; see <see cref="DropChanceNow"/> for the current run.</summary>
        public const float DropChance = 0.05f;
        public static float DropChanceNow => Difficulty.Profile.dropChance;
        public const int BossDrops = 4;

        //                                          Normal  Rare  Legend  Ultra   (per floor band)
        static readonly int[] StaffDamage = { 100, 250, 500, 1000 };
        static readonly int[] HatHealth = { 20, 50, 120, 250 };
        static readonly int[] ArmorHealth = { 30, 75, 180, 360 };
        static readonly int[] PantsHealth = { 15, 40, 100, 200 };
        static readonly float[] ArmorDefense = { 0.01f, 0.02f, 0.035f, 0.05f };
        static readonly float[] BootsSpeed = { 0.02f, 0.04f, 0.06f, 0.09f };
        // DEF and speed grow gently per band so they cannot run away (total DEF is capped in RunStats).
        const float DefensePerBand = 0.0025f;
        const float SpeedPerBand = 0.003f;

        static readonly string[,] Names =
        {
            { "Oak Staff", "Sapphire Staff", "Arcane Staff", "Solar Staff" },
            { "Cloth Hat", "Starry Hat", "Witch Hat", "Celestial Hat" },
            { "Cotton Shirt", "Silk Robe", "Mystic Robe", "Astral Robe" },
            { "Linen Shorts", "Woven Shorts", "Runed Shorts", "Astral Shorts" },
            { "Leather Boots", "Swift Boots", "Wind Boots", "Comet Boots" }
        };

        public static readonly string[] SlotLabels = { "Staff", "Topi", "Baju", "Celana", "Sepatu" };

        public static int Band(int floor) => Mathf.Clamp((Mathf.Max(1, floor) + 9) / 10, 1, 10);

        public static string Name(ItemSlot slot, UpgradeRarity rarity) => Names[(int)slot, (int)rarity];

        public static ItemData Create(ItemSlot slot, UpgradeRarity rarity, int band)
        {
            band = Mathf.Clamp(band, 1, 10);
            int r = (int)rarity;
            var item = new ItemData { slot = (int)slot, rarity = r, band = band };
            switch (slot)
            {
                case ItemSlot.Staff:
                    item.damage = StaffDamage[r] * band;
                    break;
                case ItemSlot.Hat:
                    item.health = HatHealth[r] * band;
                    break;
                case ItemSlot.Armor:
                    item.health = ArmorHealth[r] * band;
                    item.defense = ArmorDefense[r] + DefensePerBand * (band - 1);
                    break;
                case ItemSlot.Pants:
                    item.health = PantsHealth[r] * band;
                    break;
                case ItemSlot.Boots:
                    item.speed = BootsSpeed[r] + SpeedPerBand * (band - 1);
                    break;
            }
            return item;
        }

        /// <summary>A random item for this floor; <paramref name="random"/> returns values in [0, 1).</summary>
        public static ItemData Roll(Func<float> random, int floor, UpgradeRarity? forced = null)
        {
            var slot = (ItemSlot)Mathf.Clamp(Mathf.FloorToInt(random() * SlotCount), 0, SlotCount - 1);
            var rarity = forced ?? UpgradeRarities.FromRoll(random());
            return Create(slot, rarity, Band(floor));
        }

        /// <summary>Boss loot: four items, at least one of them Ultra.</summary>
        public static List<ItemData> RollBossDrops(Func<float> random, int floor)
        {
            var drops = new List<ItemData>(BossDrops);
            int ultra = Mathf.Clamp(Mathf.FloorToInt(random() * BossDrops), 0, BossDrops - 1);
            for (int i = 0; i < BossDrops; i++)
                drops.Add(Roll(random, floor, i == ultra ? UpgradeRarity.Ultra : (UpgradeRarity?)null));
            return drops;
        }

        static string Pct(float fraction) => (fraction * 100f).ToString("0.0#", CultureInfo.InvariantCulture) + "%";

        public static string Describe(ItemData item)
        {
            if (item == null)
                return string.Empty;
            switch (item.Slot)
            {
                case ItemSlot.Staff:
                    return "+" + UIController.Thousands(item.damage) + " ATK";
                case ItemSlot.Armor:
                    return "+" + UIController.Thousands(item.health) + " HP  +" + Pct(item.defense) + " DEF";
                case ItemSlot.Boots:
                    return "+" + Pct(item.speed) + " MOVE";
                default:
                    return "+" + UIController.Thousands(item.health) + " HP";
            }
        }
    }
}
