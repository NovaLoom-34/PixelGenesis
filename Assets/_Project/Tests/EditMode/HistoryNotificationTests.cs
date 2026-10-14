using NUnit.Framework;
using PG.UI;

namespace PG.Tests.EditMode
{
    public sealed class HistoryNotificationTests
    {
        [Test]
        public void NotificationThresholdUsesInclusiveImportanceAndSupportedBounds()
        {
            Assert.That(HistoryNotificationPolicy.NormalizeThreshold(0), Is.EqualTo(20));
            Assert.That(HistoryNotificationPolicy.NormalizeThreshold(60), Is.EqualTo(60));
            Assert.That(HistoryNotificationPolicy.NormalizeThreshold(120), Is.EqualTo(100));
            Assert.That(HistoryNotificationPolicy.ShouldNotify(59, 60), Is.False);
            Assert.That(HistoryNotificationPolicy.ShouldNotify(60, 60), Is.True);
            Assert.That(HistoryNotificationPolicy.ShouldNotify(100, 100), Is.True);
        }

        [Test]
        public void WatchedKingdomsAreScopedToWorldAndAlwaysNotifyTheirHistory()
        {
            var profile = new Profile();
            Assert.That(profile.ToggleKingdomWatch(42, 192, 192, 901), Is.True);
            Assert.That(profile.IsKingdomWatched(42, 192, 192, 901), Is.True);
            Assert.That(profile.IsKingdomWatched(42, 256, 256, 901), Is.False);
            Assert.That(profile.WatchesKingdomEvent(42, 192, 192, 0, 901, 0), Is.True);
            Assert.That(HistoryNotificationPolicy.ShouldNotify(20, 100,
                profile.WatchesKingdomEvent(42, 192, 192, 901, 0, 0)), Is.True);
            Assert.That(profile.ToggleKingdomWatch(42, 192, 192, 901), Is.False);
            Assert.That(profile.IsKingdomWatched(42, 192, 192, 901), Is.False);
        }
    }
}
