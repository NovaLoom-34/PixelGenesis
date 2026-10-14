using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PG.Content
{
    public sealed class ScenarioConditionDef
    {
        public string Metric { get; set; }
        public string Operator { get; set; } = "atLeast";
        public int Target { get; set; }
        public string Text { get; set; }
    }

    // Optional challenge mode. The world recipe and objective data live in scenarios.json.
    public sealed class ScenarioDef : ContentDef
    {
        static readonly string[] Metrics = { "elapsedYears", "livingPopulation", "livingKingdoms", "livingCities", "alliances", "subspecies" };
        public string Description { get; set; }
        public string NameEn { get; set; }
        public string DescriptionEn { get; set; }
        public string Template { get; set; } = "wgt.continents";
        public string Size { get; set; } = "Small";
        public ulong Seed { get; set; }
        public float Temperature { get; set; } = 0.5f;
        public float Moisture { get; set; } = 0.5f;
        public bool SpawnAnimals { get; set; } = true;
        public string StartingSpecies { get; set; }
        public int StartingPopulation { get; set; }
        public bool RestrictPowers { get; set; }
        public string[] AllowedPowers { get; set; } = Array.Empty<string>();
        public ScenarioConditionDef[] Goals { get; set; } = Array.Empty<ScenarioConditionDef>();
        public ScenarioConditionDef[] Failures { get; set; } = Array.Empty<ScenarioConditionDef>();

        [JsonIgnore] public int TemplateIndex { get; private set; } = -1;
        [JsonIgnore] public int SpeciesIndex { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            TemplateIndex = db.WorldGenTemplates.IdOrDefault(Template);
            if (TemplateIndex < 0) errors.Add($"{Id}: unknown world template '{Template}'");
            if (Array.IndexOf(new[] { "Tiny", "Small", "Medium", "Large", "Huge", "Giant", "Titanic" }, Size) < 0)
                errors.Add($"{Id}: unknown map size '{Size}'");
            if (Seed == 0) errors.Add($"{Id}: seed must be non-zero");
            if (Temperature < 0f || Temperature > 1f) errors.Add($"{Id}: temperature must be between 0 and 1");
            if (Moisture < 0f || Moisture > 1f) errors.Add($"{Id}: moisture must be between 0 and 1");
            if (StartingPopulation < 0 || StartingPopulation > 100)
                errors.Add($"{Id}: startingPopulation must be between 0 and 100");
            if (StartingPopulation > 0)
            {
                SpeciesIndex = db.Species.IdOrDefault(StartingSpecies);
                if (SpeciesIndex < 0) errors.Add($"{Id}: unknown starting species '{StartingSpecies}'");
            }
            ValidateConditions(Goals, "goals", errors);
            ValidateConditions(Failures, "failures", errors);
            if (Goals == null || Goals.Length == 0) errors.Add($"{Id}: at least one goal is required");
            if (RestrictPowers)
            {
                foreach (var power in AllowedPowers ?? Array.Empty<string>())
                    if (!db.Powers.TryGet(power, out _)) errors.Add($"{Id}: unknown allowed power '{power}'");
            }
        }

        static void ValidateConditions(ScenarioConditionDef[] conditions, string path, List<string> errors)
        {
            if (conditions == null) return;
            for (int i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition == null) { errors.Add($"{path}[{i}]: condition is required"); continue; }
                if (Array.IndexOf(Metrics, condition.Metric) < 0) errors.Add($"{path}[{i}]: unknown metric '{condition.Metric}'");
                if (condition.Operator != "atLeast" && condition.Operator != "atMost")
                    errors.Add($"{path}[{i}]: operator must be 'atLeast' or 'atMost'");
                if (condition.Target < 0) errors.Add($"{path}[{i}]: target cannot be negative");
                if (string.IsNullOrWhiteSpace(condition.Text)) errors.Add($"{path}[{i}]: localized text key is required");
            }
        }
    }
}
