using NUnit.Framework;
using PG.UI;

namespace PG.Tests.EditMode
{
    public sealed class RollingFrameWindowTests
    {
        [Test]
        public void ReportsAverageAndSlowestFrameWithoutCountingInvalidSamples()
        {
            var window = new RollingFrameWindow();
            window.AddSeconds(1f / 60f);
            window.AddSeconds(1f / 30f);
            window.AddSeconds(0f);
            window.AddSeconds(float.NaN);

            Assert.AreEqual(2, window.Count);
            Assert.AreEqual(25f, window.AverageMilliseconds, 0.01f);
            Assert.AreEqual(40f, window.AverageFramesPerSecond, 0.1f);
            Assert.AreEqual(1000f / 30f, window.SlowestMilliseconds, 0.01f);
        }

        [Test]
        public void EvictsFramesOlderThanTheFixedWindow()
        {
            var window = new RollingFrameWindow();
            for (int i = 0; i < RollingFrameWindow.Capacity; i++) window.AddSeconds(0.01f);
            window.AddSeconds(0.02f);

            Assert.AreEqual(RollingFrameWindow.Capacity, window.Count);
            Assert.AreEqual((RollingFrameWindow.Capacity - 1) * 10f + 20f, window.AverageMilliseconds * RollingFrameWindow.Capacity, 0.1f);
            Assert.AreEqual(20f, window.SlowestMilliseconds, 0.01f);
        }
    }
}
