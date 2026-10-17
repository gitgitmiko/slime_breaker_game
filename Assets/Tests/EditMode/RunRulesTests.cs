using NUnit.Framework;
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
        public void FloorTenBossHasTenMillionHpAndOneEnrageSkill()
        {
            Assert.AreEqual(BossKind.Rock, BossCatalog.KindAtFloor(10));
            Assert.AreEqual(10000000, BossCatalog.HealthForFloor(10));
            Assert.AreEqual(1, BossCatalog.EnrageSkillCount(10));
        }

        [Test]
        public void FloorTwentyBossHasTwentyMillionHpAndTwoEnrageSkills()
        {
            Assert.AreEqual(BossKind.Flame, BossCatalog.KindAtFloor(20));
            Assert.AreEqual(20000000, BossCatalog.HealthForFloor(20));
            Assert.AreEqual(2, BossCatalog.EnrageSkillCount(20));
        }

        [Test]
        public void FloorOneHundredBossIsRoyalWithTenSkills()
        {
            Assert.AreEqual(BossKind.Royal, BossCatalog.KindAtFloor(100));
            Assert.AreEqual(100000000, BossCatalog.HealthForFloor(100));
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
            Assert.AreEqual(TimeOfDay.Night, floor2.time);
            Assert.AreNotEqual(floor1.weather, floor2.weather);
            Assert.AreNotEqual(floor1.seed, floor2.seed);
            var desert = ArenaTheme.ForFloor(15, 42);
            Assert.AreEqual(Biome.Desert, desert.biome);
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
    }
}
