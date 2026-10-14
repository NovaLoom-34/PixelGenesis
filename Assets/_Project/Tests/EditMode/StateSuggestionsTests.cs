using NUnit.Framework;
using PG.UI;

namespace PG.Tests.EditMode
{
    public sealed class StateSuggestionsTests
    {
        [Test]
        public void CriticalFoodAndHousingNeedsAreRankedBeforeObservation()
        {
            var signals = new StateSuggestionSignals(true, 3, 7, 2);
            var suggestions = StateSuggestionPolicy.Rank(signals);

            Assert.That(suggestions.Length, Is.EqualTo(3));
            Assert.That(suggestions[0].Kind, Is.EqualTo(StateSuggestionKind.Food));
            Assert.That(suggestions[0].Target, Is.EqualTo(3));
            Assert.That(suggestions[0].RecommendedPowerId, Is.EqualTo("pw.fertile_rain"));
            Assert.That(suggestions[1].Kind, Is.EqualTo(StateSuggestionKind.Housing));
            Assert.That(suggestions[1].Target, Is.EqualTo(7));
            Assert.That(suggestions[1].RecommendedPowerId, Is.EqualTo("pw.inspect"));
            Assert.That(suggestions[2].Kind, Is.EqualTo(StateSuggestionKind.War));
            Assert.That(suggestions[2].Target, Is.EqualTo(2));
            Assert.That(suggestions[2].RecommendedPowerId, Is.EqualTo("pw.history"));
        }

        [Test]
        public void EmptyWorldSuggestsStartingACivilization()
        {
            var suggestions = StateSuggestionPolicy.Rank(new StateSuggestionSignals(false, -1, -1, -1));

            Assert.That(suggestions.Length, Is.EqualTo(1));
            Assert.That(suggestions[0].Kind, Is.EqualTo(StateSuggestionKind.FoundCivilization));
            Assert.That(suggestions[0].RecommendedPowerId, Is.EqualTo("pw.spawn_human"));
        }

        [Test]
        public void StableWorldSuggestsObservationRatherThanChangingIt()
        {
            var suggestions = StateSuggestionPolicy.Rank(new StateSuggestionSignals(true, -1, -1, -1));

            Assert.That(suggestions.Length, Is.EqualTo(1));
            Assert.That(suggestions[0].Kind, Is.EqualTo(StateSuggestionKind.Observe));
            Assert.That(suggestions[0].RecommendedPowerId, Is.EqualTo("pw.stats"));
        }
    }
}
