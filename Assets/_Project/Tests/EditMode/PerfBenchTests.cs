using System.Globalization;
using NUnit.Framework;
using PG.EditorTools;

namespace PG.Tests.EditMode
{
    public sealed class PerfBenchTests
    {
        [Test]
        public void InvalidTargetUsesFallbackAndPositiveTargetIsAccepted()
        {
            Assert.AreEqual(5000, PerfBench.ParsePositiveInt(null, 5000));
            Assert.AreEqual(5000, PerfBench.ParsePositiveInt("0", 5000));
            Assert.AreEqual(1200, PerfBench.ParsePositiveInt("1200", 5000));
        }

        [Test]
        public void NonNegativeAllocationThresholdAcceptsZeroAndRejectsInvalidValues()
        {
            Assert.AreEqual(0, PerfBench.ParseNonNegativeInt("0", 64));
            Assert.AreEqual(12, PerfBench.ParseNonNegativeInt("12", 64));
            Assert.AreEqual(64, PerfBench.ParseNonNegativeInt("-1", 64));
            Assert.AreEqual(64, PerfBench.ParseNonNegativeInt("NaN", 64));
        }

        [Test]
        public void BudgetParsingIsInvariantAndRejectsNonFiniteValues()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.AreEqual(8.5d, PerfBench.ParsePositiveDouble("8.5", 10d));
                Assert.AreEqual(10d, PerfBench.ParsePositiveDouble("8,5", 10d));
                Assert.AreEqual(10d, PerfBench.ParsePositiveDouble("NaN", 10d));
                Assert.AreEqual(10d, PerfBench.ParsePositiveDouble("Infinity", 10d));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
