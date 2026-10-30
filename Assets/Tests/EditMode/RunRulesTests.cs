using NUnit.Framework;
using UnityEngine;
using SlimeBreaker;

namespace SlimeBreaker.Tests
{
    public class RunRulesTests
    {
        [Test]
        public void HeroCapIs999AndRunHas100Floors()
        {
            Assert.AreEqual(999, RunRules.MaxHeroLevel);
            Assert.AreEqual(100, RunRules.MaxFloor);
            Assert.AreEqual(20, RunRules.PowerUpEvery);
            Assert.AreEqual(5, Difficulty.For(DifficultyLevel.Easy).powerUpEvery);
            Assert.AreEqual(10, Difficulty.For(DifficultyLevel.Normal).powerUpEvery);
            Assert.AreEqual(20, Difficulty.For(DifficultyLevel.Hard).powerUpEvery);
            Assert.AreEqual(30, GraphicsService.For(GraphicsService.Medium).targetFps);
            Assert.AreEqual(2f / 3f, GraphicsService.For(GraphicsService.Low).RenderScale(1080f, 0.55f), 0.001f);
            Assert.AreEqual(1f, GraphicsService.For(GraphicsService.High).RenderScale(720f, 0.55f), 0.001f);
            Assert.AreEqual(10, RunRules.BossEvery);
        }

        [Test]
        public void BossFloorsMatchTenTwentyOneHundred()
        {
            Assert.IsFalse(RunRules.IsBossFloor(1));
            Assert.IsFalse(RunRules.IsBossFloor(5));
            Assert.IsTrue(RunRules.IsBossFloor(10));
            Assert.IsTrue(RunRules.IsBossFloor(20));
            Assert.IsTrue(RunRules.IsBossFloor(100));
            Assert.IsFalse(RunRules.IsBossFloor(101));
        }

        [Test]
        public void FloorTenBossHasTenThousandHpAndOneEnrageSkill()
        {
            Assert.AreEqual(BossKind.Rock, BossCatalog.KindAtFloor(10));
            Assert.AreEqual(10000, BossCatalog.HealthForFloor(10));
            Assert.AreEqual(1, BossCatalog.EnrageSkillCount(10));
        }

        [Test]
        public void FloorTwentyBossHasTwentyThousandHpAndTwoEnrageSkills()
        {
            Assert.AreEqual(BossKind.Flame, BossCatalog.KindAtFloor(20));
            Assert.AreEqual(20000, BossCatalog.HealthForFloor(20));
            Assert.AreEqual(2, BossCatalog.EnrageSkillCount(20));
        }

        [Test]
        public void FloorOneHundredBossIsRoyalWithTenSkills()
        {
            Assert.AreEqual(BossKind.Royal, BossCatalog.KindAtFloor(100));
            Assert.AreEqual(100000, BossCatalog.HealthForFloor(100));
            Assert.AreEqual(10, BossCatalog.EnrageSkillCount(100));
            Assert.IsTrue(BossCatalog.IsFinal(BossKind.Royal));
        }

        [Test]
        public void HeroLevelDoesNotPickABossKind()
        {
            Assert.AreEqual(BossKind.None, BossCatalog.KindAtFloor(9));
            Assert.AreEqual(BossKind.None, BossCatalog.KindAtFloor(11));
        }

        [Test]
        public void DefaultCritStatsAreSensible()
        {
            var stats = new RunStats();
            Assert.AreEqual(0f, stats.critChance, 0.001f);
            Assert.AreEqual(1.5f, stats.critMultiplier, 0.001f);
            Assert.IsFalse(stats.HasCrit);
        }

        [Test]
        public void CritUpgradesRaiseChanceAndMultiplier()
        {
            var host = new UnityEngine.GameObject("UpgradeHost");
            try
            {
                var manager = host.AddComponent<UpgradeManager>();
                var stats = new RunStats();
                Assert.IsFalse(stats.HasCrit);
                var rate = UnityEngine.ScriptableObject.CreateInstance<UpgradeDefinition>();
                rate.kind = UpgradeKind.CritChance;
                rate.amount = 0.08f;
                manager.Apply(rate, stats);
                Assert.AreEqual(0.08f, stats.critChance, 0.001f);
                Assert.IsTrue(stats.HasCrit);
                var crit = UnityEngine.ScriptableObject.CreateInstance<UpgradeDefinition>();
                crit.kind = UpgradeKind.CritDamage;
                crit.amount = 0.3f;
                manager.Apply(crit, stats);
                Assert.AreEqual(1.8f, stats.critMultiplier, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UpgradeRarityChancesAndBonuses()
        {
            int[] counts = new int[4];
            const int samples = 100000;
            for (int i = 0; i < samples; i++)
                counts[(int)UpgradeRarities.FromRoll((i + 0.5f) / samples)]++;
            Assert.AreEqual(65f, counts[0] * 100f / samples, 0.01f, "Normal chance");
            Assert.AreEqual(20f, counts[1] * 100f / samples, 0.01f, "Rare chance");
            Assert.AreEqual(10f, counts[2] * 100f / samples, 0.01f, "Legend chance");
            Assert.AreEqual(5f, counts[3] * 100f / samples, 0.01f, "Ultra chance");
            Assert.AreEqual(0.001f, UpgradeRarities.Fraction(UpgradeRarity.Normal), 1e-6f);
            Assert.AreEqual(0.003f, UpgradeRarities.Fraction(UpgradeRarity.Rare), 1e-6f);
            Assert.AreEqual(0.005f, UpgradeRarities.Fraction(UpgradeRarity.Legend), 1e-6f);
            Assert.AreEqual(0.01f, UpgradeRarities.Fraction(UpgradeRarity.Ultra), 1e-6f);
            Assert.AreEqual("+0.3% magic damage", UpgradeRarities.Describe(UpgradeKind.Damage, UpgradeRarity.Rare));
            Assert.AreEqual("+1.0% critical chance", UpgradeRarities.Describe(UpgradeKind.CritChance, UpgradeRarity.Ultra));
        }

        [Test]
        public void RarityUpgradesAddTheirPercentToEveryStat()
        {
            var host = new UnityEngine.GameObject("UpgradeHost");
            try
            {
                var manager = host.AddComponent<UpgradeManager>();
                var stats = new RunStats { damage = 1000, maxHealth = 1000 };
                UpgradeOffer Offer(UpgradeKind kind, UpgradeRarity rarity)
                {
                    var def = UnityEngine.ScriptableObject.CreateInstance<UpgradeDefinition>();
                    def.kind = kind;
                    def.title = kind.ToString();
                    return new UpgradeOffer(def, rarity);
                }
                manager.Apply(Offer(UpgradeKind.Damage, UpgradeRarity.Ultra), stats);
                Assert.AreEqual(1010, stats.damage, "Ultra damage should add 1%");
                manager.Apply(Offer(UpgradeKind.Health, UpgradeRarity.Legend), stats);
                Assert.AreEqual(1005, stats.maxHealth, "Legend HP should add 0.5%");
                float range = stats.attackRange;
                manager.Apply(Offer(UpgradeKind.Range, UpgradeRarity.Rare), stats);
                Assert.AreEqual(range * 1.003f, stats.attackRange, 1e-5f);
                manager.Apply(Offer(UpgradeKind.Lifesteal, UpgradeRarity.Normal), stats);
                Assert.AreEqual(0.001f, stats.lifesteal, 1e-6f);
                manager.Apply(Offer(UpgradeKind.CritChance, UpgradeRarity.Ultra), stats);
                Assert.AreEqual(0.01f, stats.critChance, 1e-6f);
                float cooldown = stats.attackCooldown;
                manager.Apply(Offer(UpgradeKind.AttackRate, UpgradeRarity.Legend), stats);
                Assert.AreEqual(cooldown * 0.995f, stats.attackCooldown, 1e-6f);
                // Small Normal cards on a small integer stat still accumulate instead of rounding away.
                var small = new RunStats { damage = 32 };
                for (int i = 0; i < 40; i++)
                    manager.Apply(Offer(UpgradeKind.Damage, UpgradeRarity.Normal), small);
                Assert.AreEqual(33, small.damage, "40 Normal damage cards on 32 should reach 33");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ItemStatsFollowSlotRarityAndFloorBand()
        {
            Assert.AreEqual(1, ItemCatalog.Band(1));
            Assert.AreEqual(1, ItemCatalog.Band(10));
            Assert.AreEqual(2, ItemCatalog.Band(11));
            Assert.AreEqual(10, ItemCatalog.Band(100));
            Assert.AreEqual(100, ItemCatalog.Create(ItemSlot.Staff, UpgradeRarity.Normal, 1).damage);
            Assert.AreEqual(500, ItemCatalog.Create(ItemSlot.Staff, UpgradeRarity.Legend, 1).damage);
            Assert.AreEqual(1000, ItemCatalog.Create(ItemSlot.Staff, UpgradeRarity.Ultra, 1).damage, "best staff on floors 1-10 (level 1) = +1000");
            Assert.AreEqual(2000, ItemCatalog.Create(ItemSlot.Staff, UpgradeRarity.Ultra, ItemCatalog.Band(11)).damage, "best staff on floors 11-20 (level 2) = +2000");
            Assert.AreEqual(2, ItemCatalog.Roll(() => 0.99f, 15).band, "an item dropped on floor 15 is level 2");
            Assert.AreEqual(1, ItemCatalog.Roll(() => 0.5f, 10).band, "an item dropped on floor 10 is level 1");
            var rng1 = new System.Random(3);
            for (int i = 0; i < 2000; i++)
            {
                var item = ItemCatalog.Roll(() => (float)rng1.NextDouble(), 1 + i % 10);
                Assert.LessOrEqual(item.damage, 1000, "level 1 item damage must not exceed 1000");
            }
            var hat = ItemCatalog.Create(ItemSlot.Hat, UpgradeRarity.Rare, 3);
            Assert.Greater(hat.health, 0);
            Assert.AreEqual(0, hat.damage);
            var armor = ItemCatalog.Create(ItemSlot.Armor, UpgradeRarity.Legend, 1);
            Assert.Greater(armor.health, 0, "Baju gives HP");
            Assert.Greater(armor.defense, 0f, "Baju gives DEF");
            Assert.Greater(ItemCatalog.Create(ItemSlot.Pants, UpgradeRarity.Normal, 1).health, 0);
            var boots = ItemCatalog.Create(ItemSlot.Boots, UpgradeRarity.Ultra, 1);
            Assert.Greater(boots.speed, 0f, "Sepatu gives move speed");
            Assert.AreEqual(0, boots.health);
            for (int r = 1; r < 4; r++)
            {
                foreach (ItemSlot slot in System.Enum.GetValues(typeof(ItemSlot)))
                    Assert.Greater(ItemCatalog.Create(slot, (UpgradeRarity)r, 1).Power, ItemCatalog.Create(slot, (UpgradeRarity)(r - 1), 1).Power, slot + " rarity order");
            }
        }

        [Test]
        public void BossDropsFourItemsWithAtLeastOneUltra()
        {
            var rng = new System.Random(7);
            for (int run = 0; run < 200; run++)
            {
                var drops = ItemCatalog.RollBossDrops(() => (float)rng.NextDouble(), 10 * (1 + run % 10));
                Assert.AreEqual(4, drops.Count);
                Assert.IsTrue(drops.Exists(item => item.Rarity == UpgradeRarity.Ultra), "boss loot without an Ultra");
            }
            int[] rarities = new int[4];
            for (int i = 0; i < 20000; i++)
                rarities[ItemCatalog.Roll(() => (float)rng.NextDouble(), 5).rarity]++;
            Assert.AreEqual(65f, rarities[0] / 200f, 1.5f);
            Assert.AreEqual(20f, rarities[1] / 200f, 1.5f);
            Assert.AreEqual(10f, rarities[2] / 200f, 1.5f);
            Assert.AreEqual(5f, rarities[3] / 200f, 1.5f);
        }

        [Test]
        public void SavedVolumeLevelsAreClampedAndDefaultToFull()
        {
            var fresh = SaveService.ParseOrDefault("{\"schemaVersion\":2,\"gold\":5}");
            Assert.AreEqual(1f, fresh.musicLevel, 0.0001f, "old saves keep full volume");
            Assert.AreEqual(1f, fresh.sfxLevel, 0.0001f);
            var wild = SaveService.ParseOrDefault("{\"schemaVersion\":2,\"musicLevel\":4,\"sfxLevel\":-2}");
            Assert.AreEqual(1f, wild.musicLevel, 0.0001f);
            Assert.AreEqual(0f, wild.sfxLevel, 0.0001f);
            var quiet = SaveService.ParseOrDefault("{\"schemaVersion\":2,\"musicLevel\":0.3,\"sfxLevel\":0.6}");
            Assert.AreEqual(0.3f, quiet.musicLevel, 0.0001f);
            Assert.AreEqual(0.6f, quiet.sfxLevel, 0.0001f);
        }

        [Test]
        public void DifficultyLevelsScaleMonstersBossesUpgradesAndDrops()
        {
            try
            {
                Difficulty.Set(DifficultyLevel.Hard);
                Assert.AreEqual(10000, BossCatalog.HealthForFloor(10), "Hard keeps the original boss HP");
                Assert.AreEqual(50, BossCatalog.ContactDamageForFloor(1));
                Assert.AreEqual("1.0%", UpgradeRarities.Percent(UpgradeRarity.Ultra));
                Assert.AreEqual(0.05f, ItemCatalog.DropChanceNow, 1e-6f);

                Difficulty.Set(DifficultyLevel.Normal);
                Assert.AreEqual(7500, BossCatalog.HealthForFloor(10));
                Assert.AreEqual(38, BossCatalog.ContactDamageForFloor(1));
                Assert.AreEqual("2.0%", UpgradeRarities.Percent(UpgradeRarity.Ultra));
                Assert.AreEqual(0.02f, UpgradeRarities.Fraction(UpgradeRarity.Ultra), 1e-6f);
                Assert.AreEqual(0.08f, ItemCatalog.DropChanceNow, 1e-6f);

                Difficulty.Set(DifficultyLevel.Easy);
                Assert.AreEqual(5000, BossCatalog.HealthForFloor(10));
                Assert.AreEqual(25, BossCatalog.ContactDamageForFloor(1));
                Assert.AreEqual("3.0%", UpgradeRarities.Percent(UpgradeRarity.Ultra));
                Assert.AreEqual(0.15f, ItemCatalog.DropChanceNow, 1e-6f);

                var easy = Difficulty.For(DifficultyLevel.Easy);
                var normal = Difficulty.For(DifficultyLevel.Normal);
                var hard = Difficulty.For(DifficultyLevel.Hard);
                Assert.Less(easy.monsterDamage, normal.monsterDamage);
                Assert.Less(normal.monsterDamage, hard.monsterDamage);
                Assert.Less(easy.monsterHealth, normal.monsterHealth);
                Assert.Greater(easy.heroHealth, normal.heroHealth);
                Assert.Greater(normal.heroHealth, hard.heroHealth);
                Assert.Greater(easy.upgradeBonus, normal.upgradeBonus);
                Assert.Greater(easy.dropChance, normal.dropChance);
                Assert.Greater(easy.xp, normal.xp);
                Assert.Greater(easy.spawnInterval, normal.spawnInterval);
                Assert.AreEqual(1, Difficulty.Scale(1, 0.1f), "scaled values never drop below 1");
            }
            finally
            {
                Difficulty.Set(DifficultyLevel.Hard);
            }
        }

        [Test]
        public void DifficultyIsSavedWithTheRunAndOldSavesStayHard()
        {
            var fresh = SaveService.ParseOrDefault("{\"schemaVersion\":2,\"gold\":5}");
            Assert.AreEqual((int)DifficultyLevel.Normal, fresh.difficulty, "new installs start on Normal");
            var wild = SaveService.ParseOrDefault("{\"schemaVersion\":2,\"difficulty\":9}");
            Assert.AreEqual(2, wild.difficulty);
            var run = JsonUtility.FromJson<RunSave>("{\"level\":3}");
            Assert.AreEqual((int)DifficultyLevel.Hard, run.difficulty, "runs saved before difficulties existed were Hard");
        }

        [Test]
        public void CriticalUpgradeAloneUnlocksCrits()
        {
            var host = new UnityEngine.GameObject("UpgradeHost");
            try
            {
                var manager = host.AddComponent<UpgradeManager>();
                var stats = new RunStats();
                var crit = UnityEngine.ScriptableObject.CreateInstance<UpgradeDefinition>();
                crit.kind = UpgradeKind.CritDamage;
                crit.amount = 0.3f;
                manager.Apply(crit, stats);
                Assert.IsTrue(stats.HasCrit);
                Assert.AreEqual(0.08f, stats.critChance, 0.001f);
                Assert.AreEqual(1.8f, stats.critMultiplier, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RockBossCapeIsRedAndCrownIsGold()
        {
            var cape = BossCatalog.CapeColor(BossKind.Rock);
            Assert.Greater(cape.r, 0.6f);
            Assert.Less(cape.g, 0.2f);
            Assert.Less(cape.b, 0.2f);
            Assert.Greater(BossCatalog.CrownColor.r, 0.9f);
            Assert.Greater(BossCatalog.CrownColor.g, 0.65f);
            Assert.AreEqual(BossCatalog.CapeColor(BossKind.Rock),
                BossCatalog.ColorForMaterial("MAT_king_cape", BossKind.Rock, UnityEngine.Color.gray));
            Assert.AreEqual(BossCatalog.CrownColor,
                BossCatalog.ColorForMaterial("MAT_gold.001", BossKind.Rock, UnityEngine.Color.gray));
        }

        [Test]
        public void BiomeBandsMatchTenFloorThemes()
        {
            Assert.AreEqual(Biome.Forest, ArenaTheme.BiomeAtFloor(1));
            Assert.AreEqual(Biome.Forest, ArenaTheme.BiomeAtFloor(10));
            Assert.AreEqual(Biome.Desert, ArenaTheme.BiomeAtFloor(11));
            Assert.AreEqual(Biome.Desert, ArenaTheme.BiomeAtFloor(20));
            Assert.AreEqual(Biome.Snow, ArenaTheme.BiomeAtFloor(21));
            Assert.AreEqual(Biome.Thunder, ArenaTheme.BiomeAtFloor(40));
            Assert.AreEqual(Biome.Swamp, ArenaTheme.BiomeAtFloor(41));
            Assert.AreEqual(Biome.Shadow, ArenaTheme.BiomeAtFloor(60));
            Assert.AreEqual(Biome.Volcano, ArenaTheme.BiomeAtFloor(70));
            Assert.AreEqual(Biome.Storm, ArenaTheme.BiomeAtFloor(71));
            Assert.AreEqual(Biome.Crystal, ArenaTheme.BiomeAtFloor(90));
            Assert.AreEqual(Biome.Royal, ArenaTheme.BiomeAtFloor(100));
        }

        [Test]
        public void FloorThemeRotatesWeatherAndTimeOfDay()
        {
            var floor1 = ArenaTheme.ForFloor(1, 42);
            var floor2 = ArenaTheme.ForFloor(2, 42, floor1);
            Assert.AreEqual(Biome.Forest, floor1.biome);
            Assert.AreEqual(Weather.Clear, floor1.weather);
            Assert.AreEqual(TimeOfDay.Day, floor1.time);
            Assert.AreEqual(Biome.Forest, floor2.biome);
            Assert.AreNotEqual(floor1.weather, floor2.weather);
            Assert.AreNotEqual(floor1.seed, floor2.seed);
            Assert.AreEqual(floor2.weather, ArenaTheme.ForFloor(2, 42, floor1).weather, "same run seed must give the same theme");
            Assert.AreEqual(floor2.time, ArenaTheme.ForFloor(2, 42, floor1).time, "same run seed must give the same theme");
            var desert = ArenaTheme.ForFloor(15, 42);
            Assert.AreEqual(Biome.Desert, desert.biome);
        }

        [Test]
        public void RainFallsByDayAndByNight()
        {
            int rainyDays = 0;
            int rainyNights = 0;
            for (int run = 1; run <= 60; run++)
            {
                var previous = ArenaTheme.ForFloor(1, run);
                for (int floor = 2; floor <= 100; floor++)
                {
                    var theme = ArenaTheme.ForFloor(floor, run, previous);
                    if (previous.biome == theme.biome && ArenaTheme.WeathersFor(theme.biome).Length > 1)
                        Assert.AreNotEqual(previous.weather, theme.weather, "weather repeated on floor " + floor);
                    if (theme.weather == Weather.Rain)
                    {
                        if (theme.IsNight)
                            rainyNights++;
                        else
                            rainyDays++;
                    }
                    previous = theme;
                }
            }
            Assert.Greater(rainyDays, 50, "rain should also fall during the day");
            Assert.Greater(rainyNights, 30, "rain should still fall at night too");
        }

        [Test]
        public void KillQuotaIsTenTimesTheFloor()
        {
            Assert.AreEqual(10, RunRules.KillQuota(1));
            Assert.AreEqual(20, RunRules.KillQuota(2));
            Assert.AreEqual(80, RunRules.KillQuota(8));
            Assert.AreEqual(90, RunRules.KillQuota(9));
            Assert.AreEqual(0, RunRules.KillQuota(10));
            Assert.AreEqual(110, RunRules.KillQuota(11));
        }

        [Test]
        public void SlimeAndBossDamageGrowEveryFloor()
        {
            Assert.AreEqual(8, RunRules.SlimeContactDamage(8, 1));
            Assert.AreEqual(9, RunRules.SlimeContactDamage(8, 2));
            Assert.AreEqual(17, RunRules.SlimeContactDamage(8, 10));
            Assert.AreEqual(50, BossCatalog.ContactDamageForFloor(1));
            Assert.AreEqual(50 + 9 * RunRules.BossDamagePerFloor, BossCatalog.ContactDamageForFloor(10));
            Assert.AreEqual(50 + 99 * RunRules.BossDamagePerFloor, BossCatalog.ContactDamageForFloor(100));
            Assert.Greater(BossCatalog.SkillDamage(20, false), BossCatalog.SkillDamage(10, false));
            Assert.Greater(BossCatalog.SkillDamage(20, true), BossCatalog.SkillDamage(10, true));
        }

        [Test]
        public void SlimeHealthGrowsEveryFloor()
        {
            Assert.AreEqual(24, RunRules.SlimeMaxHealth(24, 1));
            Assert.Greater(RunRules.SlimeMaxHealth(24, 2), RunRules.SlimeMaxHealth(24, 1));
            Assert.AreEqual(UnityEngine.Mathf.RoundToInt(24 * 1.9f), RunRules.SlimeMaxHealth(24, 10));
            Assert.Greater(RunRules.SlimeMaxHealth(24, 20), RunRules.SlimeMaxHealth(24, 10));
        }

        [Test]
        public void LevelUpAddsTenBasicAttack()
        {
            var stats = new RunStats { damage = 12, level = 1 };
            Assert.IsTrue(stats.TryLevelUp());
            Assert.AreEqual(2, stats.level);
            Assert.AreEqual(22, stats.damage);
            Assert.AreEqual(10, RunRules.HeroDamagePerLevel);
            stats.level = RunRules.MaxHeroLevel;
            Assert.IsFalse(stats.TryLevelUp());
            Assert.AreEqual(RunRules.MaxHeroLevel, stats.level);
            Assert.AreEqual(22, stats.damage);
        }

        [Test]
        public void CombatMathWithoutRunReturnsBaseDamage()
        {
            int rolled = CombatMath.ApplyCrit(12, out bool crit);
            Assert.AreEqual(12, rolled);
            Assert.IsFalse(crit);
        }
    
        [Test]
        public void SkillsScaleToLevelTenThenMax()
        {
            var skill = UnityEngine.ScriptableObject.CreateInstance<SkillDefinition>();
            skill.kind = SkillKind.LightningChain;
            skill.baseDamage = 10;
            skill.cooldown = 2f;
            skill.radius = 1.5f;
            skill.count = 3;
            Assert.AreEqual(10, skill.maxLevel);
            Assert.AreEqual(10, SkillDefinition.MaxSkillLevel);
            Assert.IsFalse(skill.IsMax(9));
            Assert.IsTrue(skill.IsMax(10));
            Assert.AreEqual(10, skill.DamageAt(1));
            Assert.AreEqual(37, skill.DamageAt(10));
            Assert.AreEqual(3, skill.CountAt(1));
            Assert.AreEqual(8, skill.CountAt(10));
            Assert.Less(skill.CooldownAt(10), skill.CooldownAt(5));
            Assert.Less(skill.CooldownAt(5), skill.CooldownAt(1));
            Assert.Greater(skill.RadiusAt(10), skill.RadiusAt(1) * 1.6f);
            // Past the cap nothing grows further.
            Assert.AreEqual(skill.DamageAt(10), skill.DamageAt(14));
            for (int level = 2; level <= 10; level++)
                Assert.GreaterOrEqual(skill.DamageAt(level), skill.DamageAt(level - 1));
            UnityEngine.Object.DestroyImmediate(skill);
        }
    
        [Test]
        public void BossHpUsesIndonesianThousands()
        {
            Assert.AreEqual("10.000", UIController.Thousands(10000));
            Assert.AreEqual("7.250", UIController.Thousands(7250));
            Assert.AreEqual("999", UIController.Thousands(999));
            Assert.AreEqual("0", UIController.Thousands(0));
            Assert.AreEqual("100.000", UIController.Thousands(100000));
            Assert.AreEqual("1.234.567", UIController.Thousands(1234567));
        }

        [Test]
        public void ItemsSellForOneThreeSixAndTenGold()
        {
            Assert.AreEqual(1, ItemCatalog.Create(ItemSlot.Hat, UpgradeRarity.Normal, 1).SellValue);
            Assert.AreEqual(3, ItemCatalog.Create(ItemSlot.Hat, UpgradeRarity.Rare, 4).SellValue);
            Assert.AreEqual(6, ItemCatalog.Create(ItemSlot.Staff, UpgradeRarity.Legend, 2).SellValue);
            Assert.AreEqual(10, ItemCatalog.Create(ItemSlot.Boots, UpgradeRarity.Ultra, 10).SellValue);
        }
    }
}
