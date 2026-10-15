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
            Assert.AreEqual(5, RunRules.PowerUpEvery);
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
        public void BossAttacksUseFixedDamageTiers()
        {
            Assert.AreEqual(50, BossCatalog.ContactDamageForFloor(10));
            Assert.AreEqual(50, BossCatalog.ContactDamageForFloor(100));
            Assert.AreEqual(100, BossCatalog.SkillDamage(false));
            Assert.AreEqual(200, BossCatalog.SkillDamage(true));
        }

        [Test]
        public void CombatMathWithoutRunReturnsBaseDamage()
        {
            int rolled = CombatMath.ApplyCrit(12, out bool crit);
            Assert.AreEqual(12, rolled);
            Assert.IsFalse(crit);
        }
    }
}
