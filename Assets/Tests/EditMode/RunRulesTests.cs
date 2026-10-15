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
            Assert.AreEqual(0.08f, stats.critChance, 0.001f);
            Assert.AreEqual(1.5f, stats.critMultiplier, 0.001f);
        }

        [Test]
        public void CritUpgradesRaiseChanceAndMultiplier()
        {
            var host = new UnityEngine.GameObject("UpgradeHost");
            try
            {
                var manager = host.AddComponent<UpgradeManager>();
                var stats = new RunStats();
                var rate = UnityEngine.ScriptableObject.CreateInstance<UpgradeDefinition>();
                rate.kind = UpgradeKind.CritChance;
                rate.amount = 0.08f;
                manager.Apply(rate, stats);
                Assert.AreEqual(0.16f, stats.critChance, 0.001f);
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
        public void CombatMathWithoutRunReturnsBaseDamage()
        {
            int rolled = CombatMath.ApplyCrit(12, out bool crit);
            Assert.AreEqual(12, rolled);
            Assert.IsFalse(crit);
        }
    }
}
