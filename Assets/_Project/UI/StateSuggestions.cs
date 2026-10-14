using System.Collections.Generic;
using PG.Sim;

namespace PG.UI
{
    public enum StateSuggestionKind : byte { Food, Housing, War, FoundCivilization, Observe }

    public readonly struct StateSuggestion
    {
        public readonly StateSuggestionKind Kind;
        public readonly int Target;

        public StateSuggestion(StateSuggestionKind kind, int target = -1)
        {
            Kind = kind;
            Target = target;
        }

        public string TitleKey => "ui.suggestion." + Kind.ToString().ToLowerInvariant();
        public string DetailKey => TitleKey + ".detail";
        public string ActionKey => "ui.suggestion.open";
        public string RecommendedPowerId
        {
            get
            {
                switch (Kind)
                {
                    case StateSuggestionKind.Food: return "pw.fertile_rain";
                    case StateSuggestionKind.Housing: return "pw.inspect";
                    case StateSuggestionKind.War: return "pw.history";
                    case StateSuggestionKind.FoundCivilization: return "pw.spawn_human";
                    default: return "pw.stats";
                }
            }
        }
    }

    public readonly struct StateSuggestionSignals
    {
        public readonly bool HasCivilization;
        public readonly int FoodCity, HousingCity, ActiveWar;

        public StateSuggestionSignals(bool hasCivilization, int foodCity, int housingCity, int activeWar)
        {
            HasCivilization = hasCivilization;
            FoodCity = foodCity;
            HousingCity = housingCity;
            ActiveWar = activeWar;
        }
    }

    public static class StateSuggestionPolicy
    {
        const int MaxSuggestions = 3;

        // Pure ordering policy: urgent city needs first, current conflict next, then a useful fallback.
        public static StateSuggestion[] Rank(StateSuggestionSignals signals)
        {
            var result = new List<StateSuggestion>(MaxSuggestions);
            if (signals.FoodCity >= 0) result.Add(new StateSuggestion(StateSuggestionKind.Food, signals.FoodCity));
            if (signals.HousingCity >= 0) result.Add(new StateSuggestion(StateSuggestionKind.Housing, signals.HousingCity));
            if (signals.ActiveWar >= 0) result.Add(new StateSuggestion(StateSuggestionKind.War, signals.ActiveWar));
            if (result.Count == 0)
                result.Add(new StateSuggestion(signals.HasCivilization ? StateSuggestionKind.Observe : StateSuggestionKind.FoundCivilization));
            if (result.Count > MaxSuggestions) result.RemoveRange(MaxSuggestions, result.Count - MaxSuggestions);
            return result.ToArray();
        }

        public static StateSuggestion[] Evaluate(SimWorld sim)
        {
            if (sim == null) return new StateSuggestion[0];
            bool hasCivilization = false;
            var units = sim.Units.Store;
            for (int k = 0; k < units.Alive.Length; k++)
            {
                int i = units.Alive[k];
                if (units.State[i] == UnitStore.StateAlive && units.SpeciesOf(i).IsCiv)
                { hasCivilization = true; break; }
            }

            int foodCity = -1, housingCity = -1;
            float highestFoodSeverity = float.MinValue, worstHousing = 0f;
            var civ = sim.Civ;
            if (civ != null)
            {
                for (int c = 0; c < civ.CityCount; c++)
                {
                    if (!civ.CityAlive(c)) continue;
                    ref var city = ref civ.City(c);
                    if (city.Population <= 0) continue;
                    int food = civ.FoodTotal(c);
                    float foodRatio = (float)food / (city.Population * 3f);
                    if (city.FamineMonths > 0 || foodRatio < 0.35f)
                    {
                        float severity = city.FamineMonths * 2f + (1f - foodRatio);
                        if (severity > highestFoodSeverity) { highestFoodSeverity = severity; foodCity = c; }
                    }
                    if (city.Housing < city.Population)
                    {
                        float shortage = (float)(city.Population - city.Housing) / city.Population;
                        if (shortage > worstHousing) { worstHousing = shortage; housingCity = c; }
                    }
                }
            }

            int activeWar = -1;
            if (sim.Meta != null)
            {
                long newest = long.MinValue;
                for (int w = 0; w < sim.Meta.Wars.Count; w++)
                {
                    if (!sim.Meta.Wars.IsAlive(w)) continue;
                    var war = sim.Meta.Wars.Items[w];
                    if (war.Result != WarResult.Ongoing || war.StartTick < newest) continue;
                    newest = war.StartTick;
                    activeWar = w;
                }
            }
            return Rank(new StateSuggestionSignals(hasCivilization, foodCity, housingCity, activeWar));
        }
    }
}
