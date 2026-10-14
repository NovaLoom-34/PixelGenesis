using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PG.Content;
using PG.Core;
using PG.Governor;
using PG.Persistence;
using PG.Powers;
using PG.Render;
using PG.Sim;
using PG.UI;
using PG.WorldGen;
using UnityEngine;

namespace PG.Boot
{
    // Wires every layer together: content -> world -> sim -> renderer/camera/UI. Scene: GameBootstrap.
    public sealed class GameBootstrap : MonoBehaviour, IGameHost
    {
        const int AutosaveEveryYears = 10;

        public WorldGenSettings DefaultWorld = new WorldGenSettings();
        public Color BackgroundColor = new Color32(12, 22, 40, 255);

        SimulationRunner _runner;
        UiRoot _ui;
        CameraController _camera;
        GovernorRunner _governor;
        int _lastYear;
        bool _autosaving;
        bool _undoBusy;
        UndoSnapshotHistory _undoHistory;

        public ContentDB Content { get; private set; }
        public SimWorld Sim { get; private set; }
        public WorldGenSettings CurrentWorldSettings { get; private set; }
        public PowerCommandQueue Commands { get; } = new PowerCommandQueue();
        public EditCommandQueue Edits { get; } = new EditCommandQueue();
        public MapRenderer Renderer { get; private set; }
        public OverlayRenderer Overlay { get; private set; }
        public AudioManager Audio { get; private set; }
        FeatureRenderer _features;
        NatureRenderer _nature;
        UnitRenderer _units;
        BuildingRenderer _buildings;
        PowerFxRenderer _powerFx;
        VehicleRenderer _vehicles;
        public int TicksLastSecond => _runner != null ? _runner.TicksLastSecond : 0;
        public bool CanUndo => !_undoBusy && _undoHistory != null && _undoHistory.CanUndo;

        public event Action WorldChanged;

        void Awake()
        {
            var args = CommandLine.Parse(Environment.GetCommandLineArgs());
            if (!LoadContent()) return;
            Loc.Init(Content);
            ApplyArgs(args);

            if (args.DeterminismTicks > 0)
            {
                if (DefaultWorld.Seed == 0) DefaultWorld.Seed = 42;
                bool ok = Diagnostics.DeterminismCheck(Content, DefaultWorld, args.DeterminismTicks);
                Finish(ok ? 0 : 1);
                return;
            }
            if (args.Headless)
            {
                if (DefaultWorld.Seed == 0) DefaultWorld.Seed = 42;
                Diagnostics.Headless(Content, DefaultWorld, args.Years);
                Finish(0);
                return;
            }

            SetupScene();
            StartNewWorld(DefaultWorld.Clone());
        }

        bool LoadContent()
        {
            try
            {
                Content = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), ModFolders());
                foreach (var w in ModManifest.Warnings) Debug.LogWarning("[Mods] " + w);
                return true;
            }
            catch (ContentLoadException e)
            {
                Debug.LogError(e.Message);
                UiRoot.ShowFatal("İçerik verisinde hata var / Content data has errors", e.Errors);
                if (Application.isBatchMode) Application.Quit(1);
                enabled = false;
                return false;
            }
        }

        // Bölüm 9.3: bundled StreamingAssets/Mods/<id> and user-installed <persistentData>/Mods/<id>; ContentDB orders them by manifest.
        static IReadOnlyList<string> ModFolders()
        {
            var mods = new List<string>();
            foreach (var root in new[] { Path.Combine(Application.streamingAssetsPath, "Mods"), Path.Combine(Application.persistentDataPath, "Mods") })
                if (PG.Content.ContentFiles.DirExists(root)) mods.AddRange(PG.Content.ContentFiles.Dirs(root));
            mods.Sort(StringComparer.Ordinal);
            return mods;
        }

        void ApplyArgs(CommandLine args)
        {
            if (args.Seed != 0) DefaultWorld.Seed = args.Seed;
            if (!string.IsNullOrEmpty(args.Template)) DefaultWorld.Template = args.Template;
            if (!string.IsNullOrEmpty(args.Size) && Enum.TryParse(args.Size, true, out MapSizePreset size)) DefaultWorld.Size = size;
        }

        static void Finish(int exitCode)
        {
            if (Application.isBatchMode) Application.Quit(exitCode);
        }

        void SetupScene()
        {
            _undoHistory = new UndoSnapshotHistory();
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 0f, -10f);
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            if (!cam.TryGetComponent(out _camera)) _camera = cam.gameObject.AddComponent<CameraController>();

            var mapGo = new GameObject("Map", typeof(MeshFilter), typeof(MeshRenderer));
            Renderer = mapGo.AddComponent<MapRenderer>();
            Renderer.ViewCamera = cam;
            _features = new GameObject("Features").AddComponent<FeatureRenderer>();
            _features.ViewCamera = cam;
            _nature = new GameObject("Nature").AddComponent<NatureRenderer>();
            _nature.ViewCamera = cam;
            _buildings = new GameObject("Buildings").AddComponent<BuildingRenderer>();
            _buildings.ViewCamera = cam;
            _powerFx = new GameObject("PowerFx").AddComponent<PowerFxRenderer>();
            _powerFx.ViewCamera = cam;
            _units = new GameObject("Units").AddComponent<UnitRenderer>();
            _units.ViewCamera = cam;
            _vehicles = new GameObject("Vehicles").AddComponent<VehicleRenderer>();
            _vehicles.ViewCamera = cam;
            Overlay = new GameObject("Overlay").AddComponent<OverlayRenderer>();
            Audio = new GameObject("Audio").AddComponent<AudioManager>();
            Audio.ViewCamera = cam;
            Audio.Lod = () => _camera != null ? _camera.Lod : 0;
            Audio.Init(Content);

            _runner = gameObject.AddComponent<SimulationRunner>();

            // Kingdom governors (LLM brains) run above the sim: they read state and write edit commands.
            var govSettings = GovernorSettings.Load();
            _governor = new GovernorRunner(govSettings, CreateBrain(govSettings));
            GovernorRunner.Active = _governor;
            SaveSystem.GovernorSectionWriter = () => GovernorRunner.Active?.Log.WriteBytes();
            SaveSystem.GovernorSectionImporter = raw => GovernorRunner.Active?.Log.ReadBytes(raw);
            SaveSystem.WorldRecipeSectionWriter = () => WorldGenSettingsCodec.Encode(CurrentWorldSettings);
            SaveSystem.WorldRecipeSectionImporter = raw =>
            {
                if (raw == null)
                {
                    CurrentWorldSettings = null; // Legacy saves have no portable generation recipe.
                    return;
                }
                if (WorldGenSettingsCodec.TryDecode(raw, out var settings)) CurrentWorldSettings = settings;
                else
                {
                    CurrentWorldSettings = null;
                    Debug.LogWarning("[Save] World-generation recipe is unreadable; sharing is unavailable for this save.");
                }
            };

            var uiGo = new GameObject("UI");
            _ui = uiGo.AddComponent<UiRoot>();
            _ui.Init(this, _camera);
        }

        // Headless runs and tests pass their own brain; the editor/game uses the HTTP brain unless disabled.
        static IKingdomBrain CreateBrain(GovernorSettings settings)
        {
            if (string.IsNullOrEmpty(settings.BaseUrl) || string.IsNullOrEmpty(settings.ModelName)) return ScriptedBrain.Wait();
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            return new GovernorBrain(GovernorClientFactory.Create(settings, web), settings);
        }

        public void StartNewWorld(WorldGenSettings settings)
        {
            var map = WorldGenerator.Generate(settings, Content);
            settings.ImagePath = settings.Template == "wgt.custom_image" ? null : settings.ImagePath;
            settings.PreviewDivisor = 1;
            CurrentWorldSettings = settings.Clone();
            var template = Content.WorldGenTemplates.TryGet(settings.Template, out var t) ? t : Content.WorldGenTemplates[0];
            string name = $"{Loc.Name(template)} #{settings.Seed % 100000}";
            var sim = new SimWorld(Content, map, new SimRandomProvider(settings.Seed), null, name);
            if (settings.SpawnAnimals) sim.SpawnInitialAnimals();
            if (!string.IsNullOrWhiteSpace(settings.ScenarioId) && Content.Scenarios.TryGet(settings.ScenarioId, out var scenario))
            {
                sim.ScenarioId = scenario.Id;
                sim.ScenarioStartYear = sim.Clock.Year;
                sim.ScenarioOutcome = ScenarioStatus.Active;
                ScenarioSpawner.SpawnStartingPopulation(sim, scenario);
            }
            else if (settings.SpawnCivs)
                CivStartSpawner.SpawnStartingPopulation(sim);
            ReplaceSim(sim);
        }

        public Task SaveToSlot(int slot) => SaveSystem.SaveAsync(slot, Sim);

        public void CaptureUndoSnapshot()
        {
            if (Sim == null || _undoHistory == null || _undoBusy) return;
            try { _undoHistory.Capture(Sim); }
            catch (Exception e)
            {
                Debug.LogWarning("[Undo] Snapshot could not be captured: " + e.Message);
                _ui?.Toast(Loc.T("ui.undo_snapshot_failed"));
            }
        }

        public async Task UndoLastAction()
        {
            if (_undoBusy || _undoHistory == null || Sim == null || !_undoHistory.CanUndo) return;
            _undoBusy = true;
            try
            {
                var state = await _undoHistory.TakeLastAsync(Content);
                if (state == null) return;
                var sim = new SimWorld(Content, state.World, state.Rng, state.Clock, state.WorldName);
                try { state.ApplyTo(sim); }
                catch { sim.Dispose(); throw; }
                ReplaceSim(sim);
                if (state.Warnings.Count > 0)
                    _ui?.Toast(Loc.T("ui.load_warnings") + "\n" + string.Join("\n", state.Warnings));
            }
            finally { _undoBusy = false; }
        }

        public async Task LoadSlot(int slot)
        {
            var state = await SaveSystem.LoadAsync(slot, Content);
            var sim = new SimWorld(Content, state.World, state.Rng, state.Clock, state.WorldName);
            state.ApplyTo(sim);
            ReplaceSim(sim);
            if (state.Warnings.Count > 0 && _ui != null) _ui.Toast(Loc.T("ui.load_warnings") + "\n" + string.Join("\n", state.Warnings));
        }

        void ReplaceSim(SimWorld sim)
        {
            var old = Sim;
            Commands.Clear();
            PowerSystems.Install(sim, Commands, true);
            Edits.Edits.Clear(); Edits.Laws.Clear(); Edits.Metas.Clear(); Edits.Eras.Clear(); Edits.Control = null;
            EditSystems.Install(sim, Edits);
            Sim = sim;
            // DisasterExecutor only queues the effect here; snapshot the world before the queued power runs.
            sim.Events.Subscribe<DisasterRequestEvent>(_ => Autosave());
            _lastYear = sim.Clock.Year;
            if (Renderer != null) Renderer.Bind(sim.World, sim.Regions);
            if (_features != null) _features.Bind(sim.World);
            if (_nature != null) _nature.Bind(sim.Nature);
            if (_buildings != null) _buildings.Bind(sim);
            if (_units != null) _units.Bind(sim);
            if (_vehicles != null) _vehicles.Bind(sim.Civ);
            if (_powerFx != null) _powerFx.Bind(sim);
            if (Overlay != null) Overlay.Bind(sim);
            if (Audio != null) Audio.Bind(sim);
            if (_runner != null) _runner.Bind(sim);
            _governor?.Bind(sim);
            if (_nature != null) _nature.ViewClock = sim.Clock; // render-only day/night + seasons
            old?.Dispose();
            WorldChanged?.Invoke();
        }

        void Update()
        {
            _governor?.Update(Edits); // even while paused: governor commands follow the normal queue rules
            if (Sim == null) return;
            int year = Sim.Clock.Year;
            if (year == _lastYear) return;
            _lastYear = year;
            if (year > 0 && year % AutosaveEveryYears == 0) Autosave();
        }

        async void Autosave()
        {
            if (_autosaving) return;
            _autosaving = true;
            try
            {
                await SaveSystem.SaveAsync(SaveSystem.NextAutoSlot(), Sim);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _autosaving = false;
            }
        }

        void OnApplicationQuit()
        {
            if (Sim == null || Application.isBatchMode) return;
            try
            {
                SaveSystem.Save(SaveSystem.NextAutoSlot(), Sim);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnDestroy()
        {
            _undoHistory?.Clear();
            if (Renderer != null) Renderer.Unbind();
            if (_features != null) _features.Unbind();
            if (_nature != null) _nature.Unbind();
            if (_units != null) _units.Unbind();
            if (Overlay != null) Overlay.Unbind();
            if (Audio != null) Audio.Unbind();
            Sim?.Dispose();
            Sim = null;
        }
    }
}
