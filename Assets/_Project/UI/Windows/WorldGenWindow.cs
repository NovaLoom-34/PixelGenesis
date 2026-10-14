using System;
using System.Collections.Generic;
using System.Globalization;
using PG.Content;
using PG.World;
using PG.WorldGen;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 1.10.5: generation screen. Sliders bind to WorldGenSettings; a quarter-resolution preview follows edits.
    public sealed class WorldGenWindow
    {
        const float PreviewDelay = 0.3f;

        readonly IGameHost _host;
        readonly Action<string> _toast;
        readonly VisualElement _root;
        readonly DropdownField _template, _size;
        readonly DropdownField _scenario;
        readonly Toggle _spawnCivs;
        readonly TextField _seed, _image;
        readonly TextField _shareCode;
        readonly Label _scenarioDescription;
        Button _randomize;
        readonly Image _preview;
        readonly Label _memoryEstimate;
        readonly List<(Slider slider, Func<float> get, Action<float> set)> _sliders = new List<(Slider, Func<float>, Action<float>)>();
        readonly List<string> _templateIds = new List<string>();
        readonly List<string> _scenarioIds = new List<string>();
        WorldGenSettings _s = new WorldGenSettings();
        Texture2D _previewTex;
        float _previewDue = -1f;

        public WorldGenWindow(VisualElement layer, IGameHost host, Action<string> toast = null)
        {
            _host = host;
            _toast = toast;
            _root = new VisualElement();
            _root.AddToClassList("pg-window");
            _root.style.display = DisplayStyle.None;
            layer.Add(_root);

            var title = new Label(Loc.T("ui.new_world"));
            title.AddToClassList("pg-window-title");
            _root.Add(title);

            var body = new VisualElement();
            body.AddToClassList("pg-worldgen-body");
            _root.Add(body);

            var fields = new VisualElement();
            fields.AddToClassList("pg-worldgen-fields");
            body.Add(fields);

            var db = host.Content;
            var scenarioNames = new List<string> { Loc.T("ui.scenario_sandbox") };
            _scenarioIds.Add("");
            for (int i = 0; i < db.Scenarios.Count; i++)
            {
                var scenario = db.Scenarios[i];
                _scenarioIds.Add(scenario.Id);
                scenarioNames.Add(ScenarioName(scenario));
            }
            _scenario = new DropdownField(Loc.T("ui.scenario"), scenarioNames, 0);
            _scenario.RegisterValueChangedCallback(_ => OnScenarioChanged());
            fields.Add(_scenario);
            _scenarioDescription = new Label();
            _scenarioDescription.AddToClassList("pg-line");
            fields.Add(_scenarioDescription);
            _spawnCivs = new Toggle(Loc.T("ui.spawn_civs"));
            _spawnCivs.RegisterValueChangedCallback(e => _s.SpawnCivs = e.newValue);
            fields.Add(_spawnCivs);

            var names = new List<string>();
            for (int i = 0; i < db.WorldGenTemplates.Count; i++)
            {
                _templateIds.Add(db.WorldGenTemplates[i].Id);
                names.Add(Loc.Name(db.WorldGenTemplates[i]));
            }
            _template = new DropdownField(Loc.T("ui.template"), names, 0);
            _template.RegisterValueChangedCallback(_ => OnTemplateChanged());
            fields.Add(_template);

            var sizes = new List<string>();
            for (int i = 0; i < MapSizes.Count; i++)
            {
                var preset = (MapSizePreset)i;
                sizes.Add($"{Loc.T(MapSizes.LocKey(preset))} ({MapSizes.TilesFor(preset)})");
            }
            _size = new DropdownField(Loc.T("ui.size"), sizes, (int)MapSizePreset.Medium);
            _size.RegisterValueChangedCallback(_ =>
            {
                _s.Size = (MapSizePreset)_size.index;
                UpdateMemoryEstimate();
                SchedulePreview();
            });
            fields.Add(_size);

            _memoryEstimate = new Label();
            _memoryEstimate.AddToClassList("pg-line");
            _memoryEstimate.AddToClassList("pg-worldgen-estimate");
            fields.Add(_memoryEstimate);

            var seedRow = new VisualElement();
            seedRow.AddToClassList("pg-row");
            _seed = new TextField(Loc.T("ui.seed")) { style = { flexGrow = 1 } };
            _seed.RegisterValueChangedCallback(e =>
            {
                if (ulong.TryParse(e.newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed) && seed != 0)
                {
                    _s.Seed = seed;
                    SchedulePreview();
                }
            });
            seedRow.Add(_seed);
            _randomize = new Button(Randomize) { text = Loc.T("ui.randomize") };
            _randomize.AddToClassList("pg-button");
            seedRow.Add(_randomize);
            fields.Add(seedRow);

            var shareRow = new VisualElement();
            shareRow.AddToClassList("pg-row");
            _shareCode = new TextField(Loc.T("ui.world_share_code")) { style = { flexGrow = 1 } };
            shareRow.Add(_shareCode);
            var import = new Button(ImportShareCode) { text = Loc.T("ui.world_share_import") };
            import.AddToClassList("pg-button");
            shareRow.Add(import);
            fields.Add(shareRow);

            AddSlider(fields, "ui.land_ratio", 0.10f, 0.85f, () => _s.LandRatio, v => _s.LandRatio = v);
            AddSlider(fields, "ui.mountain_ratio", 0f, 0.25f, () => _s.MountainRatio, v => _s.MountainRatio = v);
            AddSlider(fields, "ui.hill_ratio", 0f, 0.30f, () => _s.HillRatio, v => _s.HillRatio = v);
            AddSlider(fields, "ui.roughness", 0f, 1f, () => _s.Roughness, v => _s.Roughness = v);
            AddSlider(fields, "ui.forest_density", 0f, 1f, () => _s.ForestDensity, v => _s.ForestDensity = v);
            AddSlider(fields, "ui.biome_variety", 0f, 1f, () => _s.BiomeVariety, v => _s.BiomeVariety = v);
            AddSlider(fields, "ui.ore_density", 0f, 1f, () => _s.OreDensity, v => _s.OreDensity = v);
            AddSlider(fields, "ui.temperature", 0f, 1f, () => _s.Temperature, v => _s.Temperature = v);
            AddSlider(fields, "ui.moisture", 0f, 1f, () => _s.Moisture, v => _s.Moisture = v);
            AddSlider(fields, "ui.river_density", 0f, 1f, () => _s.RiverDensity, v => _s.RiverDensity = v);

            _image = new TextField(Loc.T("ui.image_path"));
            _image.RegisterValueChangedCallback(e =>
            {
                _s.ImagePath = e.newValue;
                SchedulePreview();
            });
            fields.Add(_image);

            _preview = new Image { scaleMode = ScaleMode.ScaleToFit };
            _preview.AddToClassList("pg-preview");
            body.Add(_preview);

            var buttons = new VisualElement();
            buttons.AddToClassList("pg-row");
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8;
            _root.Add(buttons);
            var cancel = new Button(Close) { text = Loc.T("ui.cancel") };
            cancel.AddToClassList("pg-button");
            buttons.Add(cancel);
            var generate = new Button(Generate) { text = Loc.T("ui.generate") };
            generate.AddToClassList("pg-button");
            generate.AddToClassList("pg-button--selected");
            buttons.Add(generate);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public void Open()
        {
            if (_s.Seed == 0) _s.Seed = WorldGenerator.RandomSeed();
            SyncFields();
            _root.style.display = DisplayStyle.Flex;
            SchedulePreview();
        }

        public void Close()
        {
            _root.style.display = DisplayStyle.None;
            _previewDue = -1f;
        }

        public void Update()
        {
            if (!IsOpen || _previewDue < 0f || Time.unscaledTime < _previewDue) return;
            _previewDue = -1f;
            RenderPreview();
        }

        void AddSlider(VisualElement parent, string key, float min, float max, Func<float> get, Action<float> set)
        {
            var slider = new Slider(Loc.T(key), min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                SchedulePreview();
            });
            parent.Add(slider);
            _sliders.Add((slider, get, set));
        }

        void OnTemplateChanged()
        {
            int index = Mathf.Max(0, _template.index);
            _s.ApplyTemplateDefaults(_host.Content.WorldGenTemplates[index]);
            SyncFields();
            SchedulePreview();
        }

        void OnScenarioChanged()
        {
            int index = Mathf.Clamp(_scenario.index, 0, _scenarioIds.Count - 1);
            _s.ScenarioId = _scenarioIds[index];
            if (!string.IsNullOrEmpty(_s.ScenarioId) && _host.Content.Scenarios.TryGet(_s.ScenarioId, out var scenario))
            {
                if (_host.Content.WorldGenTemplates.TryGet(scenario.Template, out var template)) _s.ApplyTemplateDefaults(template);
                _s.Size = (MapSizePreset)Enum.Parse(typeof(MapSizePreset), scenario.Size, true);
                _s.Seed = scenario.Seed;
                _s.Temperature = scenario.Temperature;
                _s.Moisture = scenario.Moisture;
                _s.SpawnAnimals = scenario.SpawnAnimals;
                _s.ImagePath = null;
            }
            SyncFields();
            SchedulePreview();
        }

        void SyncFields()
        {
            int scenarioIndex = _scenarioIds.IndexOf(_s.ScenarioId ?? "");
            if (scenarioIndex < 0) scenarioIndex = 0;
            _scenario.SetValueWithoutNotify(_scenario.choices[scenarioIndex]);
            bool scenarioActive = scenarioIndex > 0;
            _spawnCivs.SetValueWithoutNotify(_s.SpawnCivs);
            _spawnCivs.SetEnabled(!scenarioActive);
            _scenarioDescription.text = scenarioActive && _host.Content.Scenarios.TryGet(_s.ScenarioId, out var scenario)
                ? (Loc.Language == "en" && !string.IsNullOrEmpty(scenario.DescriptionEn) ? scenario.DescriptionEn : Loc.T(scenario.Description))
                : Loc.T("ui.scenario_sandbox_status");
            int t = _templateIds.IndexOf(_s.Template);
            if (t >= 0) _template.SetValueWithoutNotify(_template.choices[t]);
            _template.SetEnabled(!scenarioActive);
            _size.SetValueWithoutNotify(_size.choices[(int)_s.Size]);
            _size.SetEnabled(!scenarioActive);
            _seed.SetValueWithoutNotify(_s.Seed.ToString(CultureInfo.InvariantCulture));
            _seed.SetEnabled(!scenarioActive);
            _randomize.SetEnabled(!scenarioActive);
            _image.SetValueWithoutNotify(_s.ImagePath ?? "");
            _image.style.display = _s.Template == "wgt.custom_image" ? DisplayStyle.Flex : DisplayStyle.None;
            _image.SetEnabled(!scenarioActive);
            foreach (var (slider, get, _) in _sliders) { slider.SetValueWithoutNotify(get()); slider.SetEnabled(!scenarioActive); }
            UpdateMemoryEstimate();
        }

        static string ScenarioName(ScenarioDef scenario) =>
            Loc.Language == "en" && !string.IsNullOrEmpty(scenario.NameEn) ? scenario.NameEn : Loc.Name(scenario);

        void UpdateMemoryEstimate()
        {
            int worldSize = MapSizes.TilesFor(_s.Size);
            int previewSize = WorldGenerator.SizeFor(new WorldGenSettings { Size = _s.Size, PreviewDivisor = 4 });
            long worldBytes = WorldGenerator.EstimatePeakWorkingSetBytes(_s.Size);
            long previewBytes = WorldGenerator.EstimatePeakWorkingSetBytes(_s.Size, 4);
            _memoryEstimate.text = Loc.F("ui.worldgen_memory_fmt", worldSize,
                Mathf.CeilToInt(worldBytes / (1024f * 1024f)), previewSize,
                Mathf.CeilToInt(previewBytes / (1024f * 1024f)));
        }

        // UI-side randomness (not simulation): picks a new seed and slider values within their ranges.
        void Randomize()
        {
            _s.Seed = WorldGenerator.RandomSeed();
            var rng = new System.Random((int)(_s.Seed & 0x7FFFFFFF));
            foreach (var (slider, _, set) in _sliders)
                set(slider.lowValue + (float)rng.NextDouble() * (slider.highValue - slider.lowValue));
            SyncFields();
            SchedulePreview();
        }

        void Generate()
        {
            Close();
            var settings = _s.Clone();
            _host.StartNewWorld(settings);
            _s.Seed = WorldGenerator.RandomSeed(); // next world starts from a fresh seed
        }

        void ImportShareCode()
        {
            if (!WorldShareCode.TryDecode(_shareCode.value, out var settings) || !_templateIds.Contains(settings.Template)
                || !_scenarioIds.Contains(settings.ScenarioId ?? ""))
            {
                _toast?.Invoke(Loc.T("ui.world_share_invalid"));
                return;
            }
            _s = settings;
            SyncFields();
            SchedulePreview();
            _toast?.Invoke(Loc.T("ui.world_share_imported"));
        }

        void SchedulePreview() => _previewDue = Time.unscaledTime + PreviewDelay;

        void RenderPreview()
        {
            var settings = _s.Clone();
            settings.PreviewDivisor = 4;
            WorldMap map = null;
            try
            {
                map = WorldGenerator.Generate(settings, _host.Content);
                if (_previewTex == null || _previewTex.width != map.Width)
                {
                    if (_previewTex != null) UnityEngine.Object.Destroy(_previewTex);
                    _previewTex = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                }
                _previewTex.SetPixels32(Colors(map, _host.Content));
                _previewTex.Apply(false);
                _preview.image = _previewTex;
            }
            catch (Exception e)
            {
                _preview.image = null;
                Debug.LogWarning($"[WorldGen] Preview failed: {e.Message}");
            }
            finally
            {
                map?.Dispose();
            }
        }

        static Color32[] Colors(WorldMap map, ContentDB db)
        {
            var pixels = new Color32[map.TileCount];
            for (int i = 0; i < pixels.Length; i++)
            {
                int v = map.Variant[i] & 3;
                byte b = map.Biome[i];
                pixels[i] = b != 0 ? db.Biomes[b - 1].ParsedColors[v] : db.Tiles[map.Ground[i]].ParsedColors[v];
            }
            return pixels;
        }
    }
}
