using PG.Content;
using PG.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    public sealed class ScenarioWindow : WindowBase
    {
        int _lastYear = -1;

        public ScenarioWindow(IGameHost host) : base(host, Loc.T("ui.scenario_objectives")) { Rebuild(); }

        protected override void Build()
        {
            var sim = Host.Sim;
            if (sim == null || string.IsNullOrEmpty(sim.ScenarioId) || !Host.Content.Scenarios.TryGet(sim.ScenarioId, out var scenario))
            {
                Line(Loc.T("ui.scenario_sandbox_status"));
                return;
            }

            string name = Loc.Language == "en" && !string.IsNullOrEmpty(scenario.NameEn) ? scenario.NameEn : Loc.Name(scenario);
            string description = Loc.Language == "en" && !string.IsNullOrEmpty(scenario.DescriptionEn)
                ? scenario.DescriptionEn : Loc.T(scenario.Description);
            Heading(name);
            Line(description);

            var result = ScenarioRules.Evaluate(sim, scenario);
            string status = result.Status == ScenarioStatus.Won ? Loc.T("ui.scenario_won")
                : result.Status == ScenarioStatus.Lost ? Loc.T("ui.scenario_lost") : Loc.T("ui.scenario_active");
            Line(status);
            for (int i = 0; i < result.Goals.Length; i++)
            {
                var goal = result.Goals[i];
                var line = Line(Loc.F("ui.scenario_goal_fmt", Loc.T(goal.TextKey), goal.Current, goal.Target));
                if (goal.Complete) line.AddToClassList("pg-scenario-goal--complete");
            }
            if (result.Failures.Length > 0)
            {
                Heading(Loc.T("ui.scenario_failure_conditions"));
                for (int i = 0; i < result.Failures.Length; i++)
                {
                    var failure = result.Failures[i];
                    string comparison = failure.Operator == "atMost"
                        ? Loc.T("ui.scenario_at_most") : Loc.T("ui.scenario_at_least");
                    var line = Line(Loc.F("ui.scenario_failure_fmt", Loc.T(failure.TextKey), comparison, failure.Target, failure.Current));
                    if (failure.Complete) line.AddToClassList("pg-scenario-failure--triggered");
                }
            }
            _lastYear = sim.Clock.Year;
        }

        protected override void Refresh()
        {
            if (Host?.Sim == null || Host.Sim.Clock.Year == _lastYear) return;
            Rebuild();
        }
    }
}
