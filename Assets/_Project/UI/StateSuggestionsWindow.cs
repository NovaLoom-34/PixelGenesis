using PG.Sim;

namespace PG.UI
{
    // H2: player-requested, read-only next-step advice based on the current simulation state.
    public sealed class StateSuggestionsWindow : WindowBase
    {
        readonly UiContext _ui;

        public StateSuggestionsWindow(UiContext ui) : base(ui.Host, Loc.T("ui.suggestions"))
        {
            _ui = ui;
            Rebuild();
        }

        protected override void Build()
        {
            var suggestions = StateSuggestionPolicy.Evaluate(Host.Sim);
            for (int i = 0; i < suggestions.Length; i++)
            {
                var suggestion = suggestions[i];
                Heading(suggestion.TitleKey);
                Line(Loc.T(suggestion.DetailKey));
                var action = Raw(Loc.T(suggestion.ActionKey), () => Apply(suggestion));
                action.AddToClassList("pg-button");
                Body.Add(action);
                if (suggestion.Kind != StateSuggestionKind.FoundCivilization)
                {
                    var tool = Raw(Loc.T("ui.suggestion.select_tool"), () => SelectTool(suggestion));
                    tool.AddToClassList("pg-button");
                    Body.Add(tool);
                }
            }
        }

        void SelectTool(StateSuggestion suggestion)
        {
            if (_ui.SelectPower == null || !_ui.SelectPower(suggestion.RecommendedPowerId))
                _ui.Toast?.Invoke(Loc.T("ui.suggestion.power_unavailable"));
        }

        void Apply(StateSuggestion suggestion)
        {
            var sim = Host.Sim;
            if (sim == null) return;
            switch (suggestion.Kind)
            {
                case StateSuggestionKind.Food:
                case StateSuggestionKind.Housing:
                    if (sim.Civ != null && sim.Civ.CityAlive(suggestion.Target))
                    {
                        _ui.GoTo((Unity.Mathematics.float2)sim.Civ.City(suggestion.Target).Center);
                        _ui.OpenMeta(MetaKind.City, suggestion.Target);
                    }
                    break;
                case StateSuggestionKind.War:
                    if (sim.Meta != null && suggestion.Target >= 0 && suggestion.Target < sim.Meta.Wars.Count)
                        _ui.OpenMeta(MetaKind.War, suggestion.Target);
                    break;
                case StateSuggestionKind.FoundCivilization:
                    if (_ui.SelectPower == null || !_ui.SelectPower("pw.spawn_human"))
                        _ui.Toast?.Invoke(Loc.T("ui.suggestion.power_unavailable"));
                    break;
                case StateSuggestionKind.Observe:
                    _ui.Windows.Show("history", () => new HistoryWindow(_ui));
                    break;
            }
        }
    }
}
