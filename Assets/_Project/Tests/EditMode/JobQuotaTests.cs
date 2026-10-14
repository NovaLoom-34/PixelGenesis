using NUnit.Framework;
using PG.Sim;

namespace PG.Tests.EditMode
{
    public sealed class JobQuotaTests
    {
        [Test]
        public void WoodShortageRaisesLumberjackQuotaAndStockRecoveryRemovesIt()
        {
            Assert.That(CityEconomySystem.LumberjackQuota(20, 100, 100), Is.EqualTo(0),
                "No wood shortage should leave no lumberjack demand.");
            Assert.That(CityEconomySystem.LumberjackQuota(20, 80, 100), Is.EqualTo(1));
            Assert.That(CityEconomySystem.LumberjackQuota(20, 0, 200), Is.EqualTo(5),
                "A large shortage should scale workers up to the population-based limit.");
        }

        [Test]
        public void SmallCityQuotaUsesTwoWorkersAsItsPopulationBasedCap()
        {
            Assert.That(CityEconomySystem.LumberjackQuota(4, 0, 200), Is.EqualTo(2));
            Assert.That(CityEconomySystem.LumberjackQuota(4, 199, 200), Is.EqualTo(1),
                "The small-city cap permits up to two workers as the shortage grows.");
        }
    }
}
