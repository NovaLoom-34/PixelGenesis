using NUnit.Framework;
using PG.UI;

namespace PG.Tests.EditMode
{
    public sealed class WorldStatisticsTests
    {
        [Test]
        public void CivilizationCollapseWarningRequiresNoLivingCivsAndAPastKingdom()
        {
            Assert.That(StatsWindow.ShouldShowCivilizationCollapseWarning(0, 0, 1), Is.True);
            Assert.That(StatsWindow.ShouldShowCivilizationCollapseWarning(0, 0, 0), Is.False);
            Assert.That(StatsWindow.ShouldShowCivilizationCollapseWarning(1, 0, 1), Is.False);
            Assert.That(StatsWindow.ShouldShowCivilizationCollapseWarning(0, 1, 1), Is.False);
        }
    }
}
