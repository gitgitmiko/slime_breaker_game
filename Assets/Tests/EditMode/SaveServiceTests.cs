using NUnit.Framework;

namespace SlimeBreaker.Tests
{
    public class SaveServiceTests
    {
        [Test]
        public void CorruptJsonFallsBackToDefault()
        {
            var data = SaveService.ParseOrDefault("{bukan json");
            Assert.AreEqual(0, data.gold);
            Assert.AreEqual(1, data.schemaVersion);
            Assert.AreEqual(0, data.damageRank);
        }

        [Test]
        public void EmptyJsonFallsBackToDefault()
        {
            var data = SaveService.ParseOrDefault("   ");
            Assert.AreEqual(0, data.gold);
            Assert.AreEqual(1, data.schemaVersion);
        }

        [Test]
        public void ValidJsonKeepsGold()
        {
            var data = SaveService.ParseOrDefault("{\"schemaVersion\":1,\"gold\":42,\"damageRank\":2,\"healthRank\":0,\"speedRank\":1,\"bestRunTime\":12.5}");
            Assert.AreEqual(42, data.gold);
            Assert.AreEqual(2, data.damageRank);
            Assert.AreEqual(1, data.speedRank);
            Assert.AreEqual(12.5f, data.bestRunTime, 0.01f);
        }
    }
}
