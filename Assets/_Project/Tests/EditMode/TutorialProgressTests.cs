using NUnit.Framework;
using PG.UI;

namespace PG.Tests.EditMode
{
    public sealed class TutorialProgressTests
    {
        [Test]
        public void CreatureAndCivilizationStepsRequireMatchingTabAndNewPopulation()
        {
            Assert.That(Tutorial.PopulationActionCompleted("creatures", "creatures", 120, 120), Is.False);
            Assert.That(Tutorial.PopulationActionCompleted("world", "creatures", 120, 121), Is.False);
            Assert.That(Tutorial.PopulationActionCompleted("creatures", "creatures", 120, 121), Is.True);
            Assert.That(Tutorial.PopulationActionCompleted("civ", "civ", 32, 40), Is.True);
        }

        [Test]
        public void CityAndLawStepsRequireActionsAfterTheirStepBegins()
        {
            Assert.That(Tutorial.CounterActionCompleted(4, 4), Is.False);
            Assert.That(Tutorial.CounterActionCompleted(4, 5), Is.True);
            Assert.That(Tutorial.CounterActionCompleted(2, 1), Is.False);
        }

        [Test]
        public void NatureStepRequiresApplyingANewPowerFromAnExpectedTab()
        {
            Assert.That(Tutorial.PowerActionCompleted("nature", 12, 12), Is.False);
            Assert.That(Tutorial.PowerActionCompleted("world", 12, 13), Is.False);
            Assert.That(Tutorial.PowerActionCompleted("destruction", 12, 13), Is.True);
        }
    }
}
