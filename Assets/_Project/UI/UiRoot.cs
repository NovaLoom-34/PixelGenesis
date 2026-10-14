using System;
using PG.Powers;
using PG.Render;
using PG.Sim;
using PG.Content;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Owns the UI Toolkit panel and routes input: hotkeys -> clock/tool, pointer -> camera and PowerTool.
    public sealed class UiRoot : MonoBehaviour
    {
        const string ThemeResource = "PG_Theme";
        const string StyleResource = "PG_Hud";

        IGameHost _host;
        GameInput _input;
        CameraController _camera;
        BrushCursor _cursor;
        readonly PowerTool _tool = new PowerTool();
        VisualElement _root;
        HudView _hud;
        WorldGenWindow _worldGen;
        SaveLoadWindow _saves;
        DebugOverlay _debug;
        bool _spaceUsedForDrag;
        bool _mapTapCandidate;
        Vector2 _mapTapStart;
        UiContext _context;
        PossessionController _possession;
        Minimap _minimap;
        float _nextDiscover;
        int _discoverCursor;
        bool _inspect;
        AchievementTracker _achievements;
        Nameplates _plates;
        HistoryMarkers _historyMarkers;
        readonly StatsRecorder _stats = new StatsRecorder();
        Tutorial _tutorial;
        MainMenu _menu;
        string _lastPowerTab;
        long _tutorialPowerActions;
        bool _autoOverlay;
        PowerDef _pendingTarget; // target-type UI power waiting for a map click
        int _lastCursorPower = -2;
        SimWorld _historyWorld;
        long _lastNotifiedHistoryEventId;
        int _scenarioYear = -1;
        ScenarioStatus _scenarioStatus = ScenarioStatus.Sandbox;
        VisualElement _layer;
        PanelSettings _panelSettings;
        public UiContext Context => _context;

        public PowerTool Tool => _tool;

        public void Init(IGameHost host, CameraController camera)
        {
            RuntimeDiagnostics.Initialize();
            _host = host;
            _camera = camera;
            _input = new GameInput();

            _root = CreatePanel(gameObject, out _panelSettings);
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("pg-layer");
            _layer = layer;
            _root.Add(layer);

            _hud = new HudView(layer, host, _tool);
            _tool.BeforeMajorAction = host.CaptureUndoSnapshot;
            var profile = Profile.Load();
            Loc.Language = profile.Language;
            _context = new UiContext { Host = host, Camera = camera, Profile = profile, Windows = new WindowManager(layer), Toast = Toast };
            _context.SelectPower = _hud.TrySelectPower;
            _hud.Muted = c => profile.MutedNotifications.Contains(c);
            PowerLocks.IsEarned = profile.HasAchievement;
            _achievements = new AchievementTracker(_context, (id, name) =>
            {
                _hud.Notify("achievement", Loc.F("ui.achievement_fmt", name), () => _context.Windows.Show("achievements", () => new AchievementsWindow(_context)));
                RevealUnlockedPowers(id, true);
                host.Audio?.Play("achievement");
                _hud.RebuildPowers();
            });
            _plates = new Nameplates(_context, layer);
            _historyMarkers = new HistoryMarkers(_context, layer);
            _context.ShowHistoryMarker = _historyMarkers.Show;
            _minimap = new Minimap(_context, layer);
            _root.RegisterCallback<ClickEvent>(e => { if (e.target is Button) host.Audio?.Play("ui.click"); }, TrickleDown.TrickleDown);
            _hud.Notified = () => host.Audio?.Play("ui.notify");
            _context.Windows.Sound = opened => host.Audio?.Play(opened ? "ui.open" : "ui.close");
            _possession = new PossessionController(_context, layer);
            _context.Possess = i => { _tool.Cancel(); _context.Followed = -1; _possession.Begin(i); };
            _context.SettingsChanged = ApplySettings;
            var tools = new ScrollView(ScrollViewMode.Horizontal);
            tools.AddToClassList("pg-tools");
            tools.contentContainer.style.flexDirection = FlexDirection.Row;
            tools.contentContainer.style.flexWrap = Wrap.NoWrap;
            layer.Add(tools);
            tools.Add(WindowBase.Small("ui.inspect", () => { _inspect = !_inspect; _tool.Cancel(); _hud.RefreshSelection(); }, "inspect"));
            tools.Add(WindowBase.Small("ui.suggestions", () => _context.Windows.Show("suggestions", () => new StateSuggestionsWindow(_context)), "history"));
            tools.Add(WindowBase.Small("ui.laws", () => _context.Windows.Show("laws", () => new LawsWindow(_context)), "laws"));
            tools.Add(WindowBase.Small("ui.statistics", () => _context.Windows.Show("stats", () => new StatsWindow(_context)), "statistics"));
            tools.Add(WindowBase.Small("ui.history", () => _context.Windows.Show("history", () => new HistoryWindow(_context)), "history"));
            tools.Add(WindowBase.Small("ui.settings", () => _context.Windows.Show("settings", () => new SettingsWindow(_context)), "settings"));
            tools.Add(WindowBase.Small("ui.era_clock", () => _context.Windows.Show("eras", () => new EraClockWindow(_context)), "era_clock"));
            tools.Add(WindowBase.Small("ui.graphs", () => _context.Windows.Show("graphs", () => new GraphsWindow(_context, _stats)), "graphs"));
            tools.Add(WindowBase.Small("ui.achievements", () => _context.Windows.Show("achievements", () => new AchievementsWindow(_context)), "achievements"));
            tools.Add(WindowBase.Small("ui.scenario_objectives", () => _context.Windows.Show("scenario", () => new ScenarioWindow(host)), "target"));
            tools.Add(WindowBase.Small("ui.overlays", () => _context.Windows.Show("overlays", () => new OverlayWindow(_context)), "overlays"));
            tools.Add(WindowBase.Small("ui.plates", () => _plates.Visible = !_plates.Visible, "plates"));
            tools.Add(WindowBase.Small("ui.menu", () => _menu.Open(), "menu"));
            // Bölüm 1.14 mobile: on-screen zoom (camera gestures cover pan/zoom otherwise).
            if (PlatformInfo.IsTouchDevice)
            {
                var zoomPad = new VisualElement();
                zoomPad.AddToClassList("pg-touchzoom");
                var zoomIn = WindowBase.Raw("+", () => _camera.ZoomBy(1f / 1.35f));
                var zoomOut = WindowBase.Raw("-", () => _camera.ZoomBy(1.35f));
                zoomIn.AddToClassList("pg-zoombtn");
                zoomOut.AddToClassList("pg-zoombtn");
                zoomPad.Add(zoomIn);
                zoomPad.Add(zoomOut);
                layer.Add(zoomPad);
            }
            camera.LodChanged += OnLod;
            _hud.AttachProfile(profile);
            RevealUnlockedPowers(null, false);
            _hud.UiPowerClicked += OnUiPower;
            _hud.UndoClicked += UndoLastAction;
            ApplySettings();
            _debug = new DebugOverlay(layer);
            _worldGen = new WorldGenWindow(layer, host, Toast);
            _context.WorldSettings = host.CurrentWorldSettings?.Clone();
            _saves = new SaveLoadWindow(layer, host, t => { if (t == Loc.T("ui.saved") && _tutorial != null) _tutorial.Saved = true; Toast(t); });
            _tutorial = new Tutorial(_context, layer)
            {
                LastPowerTab = () => _lastPowerTab,
                PowerActionCount = () => _tutorialPowerActions,
            };
            _menu = new MainMenu(_context, layer, () => { _saves.Close(); _worldGen.Open(); }, () => { _worldGen.Close(); _saves.Open(); }, Toast);
            if (!Application.isBatchMode) _menu.Open(); else _menu.Close();
            if (!Application.isBatchMode && profile.LastSeenVersion != Application.version)
            {
                profile.LastSeenVersion = Application.version; profile.Dirty = true; profile.Save();
                _context.Windows.Show("whatsnew", () => new WhatsNewWindow(_context));
            }
            _hud.NewWorldClicked += () =>
            {
                _saves.Close();
                _worldGen.Open();
            };
            _hud.SavesClicked += () =>
            {
                _worldGen.Close();
                _saves.Open();
            };

            var cursorGo = new GameObject("BrushCursor", typeof(MeshFilter), typeof(MeshRenderer));
            cursorGo.transform.SetParent(transform, false);
            _cursor = cursorGo.AddComponent<BrushCursor>();
            _cursor.Hide();

            host.WorldChanged += OnWorldChanged;
            OnWorldChanged();
        }

        public void Toast(string text) => _hud?.Toast(text);

        void OnWorldChanged()
        {
            if (_context != null) _context.WorldSettings = _host.CurrentWorldSettings?.Clone();
            _historyMarkers?.Hide();
            _context?.Windows.CloseAll();
            _possession?.Release(true);
            if (_context != null) _context.Followed = -1;
            _tool.Cancel(); _discoverCursor = 0;
            _stats.Reset();
            var sim = _host.Sim;
            if (sim == null) return;
            _historyWorld = sim;
            _lastNotifiedHistoryEventId = sim.Meta.NextHistoryEventId - 1;
            _achievements.Attach(sim);
            _scenarioYear = sim.Clock.Year;
            _scenarioStatus = ScenarioStatus.Sandbox;
            if (!string.IsNullOrEmpty(sim.ScenarioId) && _host.Content.Scenarios.TryGet(sim.ScenarioId, out var activeScenario))
            {
                _scenarioStatus = ScenarioRules.Evaluate(sim, activeScenario).Status;
                _context.Windows.Show("scenario", () => new ScenarioWindow(_host));
            }
            _camera.Bind(sim.World.Width, sim.World.Height);
            _hud.RebuildPowers();
            _hud.ResetCachedState();
            sim.Events.Subscribe<EraChangedEvent>(OnEraChanged);
            sim.Events.Subscribe<DiplomacyProposalEvent>(OnProposal);
            sim.Events.Subscribe<TraitDiscoveredEvent>(e =>
            {
                var def = _host.Content.UnitTraits[e.Trait];
                if (_host.Sim.Nature.Laws.IsOn("law.forbidden_codex") || !_context.Profile.Discover(def.Id)) return;
                _hud.Notify("discovery", Loc.F("ui.discovery_fmt", Loc.Name(def)), null);
            });
            sim.Events.Subscribe<UnitDiedEvent>(e => { if (_context.Followed >= 0 && _host.Sim.Units.Store.Uid[_context.Followed] == e.Uid) _hud.Notify("favorite", Loc.T("ui.followed_died"), null); });
        }

        void ApplySettings()
        {
            var audio = _host.Audio;
            if (audio != null) for (int i = 0; i < audio.Volumes.Length && i < _context.Profile.Volumes.Length; i++) audio.Volumes[i] = _context.Profile.Volumes[i];
            Application.targetFrameRate = _context.Profile.TargetFps;
            PG.Render.NatureRenderer.PerformanceQuality = _context.Profile.PerformancePreset == "low" ? 0 : _context.Profile.PerformancePreset == "high" ? 2 : 1;
            _camera.Settings.EdgePan = _context.Profile.EdgePan;
            PG.Render.NatureRenderer.DayNightEnabled = _context.Profile.DayNight; // Bölüm 11 G4
            PG.Render.PowerFxRenderer.ScreenShakeEnabled = !_context.Profile.DisableScreenShake;
            if (_root != null) _root.EnableInClassList("pg-high-contrast", _context.Profile.HighContrast);
            if (_host.Overlay != null)
            {
                _host.Overlay.UseColorBlindIdentityPalette = _context.Profile.ColorBlindPalette;
                _host.Overlay.HighContrast = _context.Profile.HighContrast;
            }
            if (_panelSettings != null) _panelSettings.scale = Mathf.Clamp(_context.Profile.UiScale, 0.75f, 2f);
            _hud.RebuildPowers();
        }

        void RevealUnlockedPowers(string achievementId, bool notify)
        {
            if (_context?.Profile == null || _host?.Content == null) return;
            var powers = _host.Content.Powers;
            int count = PowerDiscovery.RevealEarned(_context.Profile, powers, achievementId, out var firstPower);
            if (count == 0) return;
            _context.Profile.Save();
            _hud.RebuildPowers();
            if (!notify) return;
            string discoveredName = powers.TryGet(firstPower, out var discovered) ? Loc.Name(discovered) : firstPower;
            string message = count == 1 ? Loc.F("ui.power_discovered_fmt", discoveredName) : Loc.F("ui.powers_discovered_fmt", count);
            string targetPower = firstPower;
            _hud.Notify("discovery", message, () => _hud.TrySelectPower(targetPower));
        }

        // Bounded scan also discovers traits already present in a loaded world.
        void Discover()
        {
            if (Time.unscaledTime < _nextDiscover || _host.Sim.Nature.Laws.IsOn("law.forbidden_codex")) return;
            _nextDiscover = Time.unscaledTime + 0.5f;
            var u = _host.Sim.Units.Store; var db = _host.Content;
            for (int n = 0; n < Mathf.Min(32, u.Alive.Length); n++)
            {
                if (_discoverCursor >= u.Alive.Length) _discoverCursor = 0;
                int i = u.Alive[_discoverCursor++];
                _context.Profile.Discover(db.Species[u.Species[i]].Id);
                for (int t = 0; t < db.UnitTraits.Count; t++) if (u.Traits[i].Has(t)) _context.Profile.Discover(db.UnitTraits[t].Id);
                int sub = u.Subspecies[i];
                if (!_host.Sim.Units.Subspecies.IsAlive(sub)) continue;
                var genome = _host.Sim.Units.Subspecies.Data(sub).Genome;
                for (int g = 0; g < genome.Slots.Length; g++) if (genome.Gene(g) >= 0) _context.Profile.Discover(db.Genes[genome.Gene(g)].Id);
                var traits = _host.Sim.Units.Subspecies.Traits(sub);
                for (int t = 0; t < db.SubspeciesTraits.Count; t++) if (traits.Has(t)) _context.Profile.Discover(db.SubspeciesTraits[t].Id);
            }
        }

        int PickUnit(Vector2 world)
        {
            var u = _host.Sim.Units.Store; float best = 9f; int found = -1;
            for (int n = 0; n < u.Alive.Length; n++)
            { int i = u.Alive[n]; float d = math.distancesq(u.Pos[i], new float2(world.x, world.y)); if (d < best) { best = d; found = i; } }
            return found;
        }

        // Tools/tests: run a UI power as if its button was pressed; target powers act on the given world point.
        public void UseUiPower(PowerDef p, Vector2 at)
        {
            OnUiPower(p);
            if (_pendingTarget != null) { var power = _pendingTarget; _pendingTarget = null; ApplyTarget(power, at, new Vector2Int((int)at.x, (int)at.y)); }
        }

        void OnUiPower(PowerDef p)
        {
            if (!IsScenarioPowerAllowed(p)) { Toast(Loc.T("ui.scenario_power_blocked")); return; }
            _pendingTarget = null;
            string window = p.ParamString("window");
            if (p.ParamString("highlight") != null) { _plates.Eye = !_plates.Eye; return; }
            if (window == null || window.StartsWith("editor_", System.StringComparison.Ordinal))
            {
                _pendingTarget = p;
                _tool.Cancel();
                Toast(Loc.T("ui.pick_target"));
                return;
            }
            switch (window)
            {
                case "stats": _context.Windows.Show("stats", () => new StatsWindow(_context)); break;
                case "graphs": _context.Windows.Show("graphs", () => new GraphsWindow(_context, _stats)); break;
                case "history": _context.Windows.Show("history", () => new HistoryWindow(_context)); break;
                case "laws": _context.Windows.Show("laws", () => new LawsWindow(_context)); break;
                case "eras": _context.Windows.Show("eras", () => new EraClockWindow(_context)); break;
                case "achievements": _context.Windows.Show("achievements", () => new AchievementsWindow(_context)); break;
                case "settings": _context.Windows.Show("settings", () => new SettingsWindow(_context)); break;
                case "save": case "load": _worldGen.Close(); _saves.Open(); break;
                case "worldgen": _saves.Close(); _worldGen.Open(); break;
                case "tutorial": _tutorial.Restart(); break;
            }
        }

        // Target powers and editors act on the unit under the cursor, else on the city (and its kingdom / majority metas).
        void ApplyTarget(PowerDef p, Vector2 world, Vector2Int tile)
        {
            var sim = _host.Sim;
            var u = sim.Units.Store;
            int unit = PickUnit(world);
            int city = unit >= 0 ? u.City[unit] : sim.World.InBounds(tile.x, tile.y) ? sim.Civ.CityAtTile(tile.x, tile.y) : -1;
            int kingdom = sim.Civ.KingdomOf(city);
            var cityData = city >= 0 && sim.Civ.CityAlive(city) ? sim.Civ.City(city) : default;
            bool hasCity = city >= 0 && sim.Civ.CityAlive(city);
            string window = p.ParamString("window");
            if (p.ParamString("open") != null) { if (unit >= 0) _context.OpenUnit(unit); else _context.OpenMeta(MetaKind.City, city); }
            else if (p.ParamString("camera") != null) { if (unit >= 0) _context.Followed = unit; }
            else if (p.ParamNumber("possess", 0f) > 0f) { if (unit >= 0) _context.Possess(unit); }
            else if (p.ParamString("control") != null) { if (kingdom >= 0) _context.Windows.Show(("control", kingdom), () => new MetaControlWindow(_context, kingdom)); }
            else switch (window)
            {
                case "editor_unit": if (unit >= 0) _context.OpenTraitEditor(MetaKind.Family, unit, true); break;
                case "editor_item": if (unit >= 0) _context.Windows.Show(("item", u.Uid[unit]), () => new ItemEditorWindow(_context, unit)); break;
                case "editor_subspecies": if (unit >= 0) _context.OpenTraitEditor(MetaKind.Subspecies, u.Subspecies[unit], false); break;
                case "editor_genes":
                    if (unit >= 0 && sim.Units.Subspecies.IsAlive(u.Subspecies[unit])) { int sub = u.Subspecies[unit]; _context.Windows.Show(("genes", sub), () => new GeneEditorWindow(_context, sub)); }
                    break;
                case "editor_clan": if (unit >= 0) _context.OpenTraitEditor(MetaKind.Clan, u.Clan[unit], false); break;
                case "editor_culture": _context.OpenTraitEditor(MetaKind.Culture, unit >= 0 ? u.Culture[unit] : hasCity ? cityData.Culture : -1, false); break;
                case "editor_religion": _context.OpenTraitEditor(MetaKind.Religion, unit >= 0 ? u.Religion[unit] : hasCity ? cityData.Religion : -1, false); break;
                case "editor_language": _context.OpenTraitEditor(MetaKind.Language, unit >= 0 ? u.Language[unit] : hasCity ? cityData.Language : -1, false); break;
                case "editor_kingdom": _context.OpenTraitEditor(MetaKind.Kingdom, kingdom, false); break;
            }
        }

        System.Action CityLink(int city) => () =>
        {
            var civ = _host.Sim?.Civ;
            if (civ == null || city < 0 || city >= civ.CityCount) return;
            _context.GoTo(civ.Cities.Items[city].Center);
            if (civ.CityAlive(city)) _context.OpenMeta(MetaKind.City, city);
        };

        System.Action KingdomLink(int kingdom) => () =>
        {
            var sim = _host.Sim;
            if (sim?.Meta == null || !sim.Meta.KingdomAlive(kingdom)) return;
            int capital = sim.Meta.Kingdoms.Data(kingdom).Capital;
            if (sim.Civ.CityAlive(capital)) _context.GoTo(sim.Civ.Cities.Items[capital].Center);
            _context.OpenMeta(MetaKind.Kingdom, kingdom);
        };

        System.Action WarLink(int warIndex) => () =>
        {
            var sim = _host.Sim;
            if (sim?.Meta == null || warIndex < 0 || warIndex >= sim.Meta.Wars.Count) return;
            var war = sim.Meta.Wars.Items[warIndex];
            int target = war.TargetCity;
            if (sim.Civ.CityAlive(target)) _context.GoTo(sim.Civ.Cities.Items[target].Center);
            else if (sim.Meta.KingdomAlive(war.Attacker))
            {
                int capital = sim.Meta.Kingdoms.Data(war.Attacker).Capital;
                if (sim.Civ.CityAlive(capital)) _context.GoTo(sim.Civ.Cities.Items[capital].Center);
            }
            _context.OpenMeta(MetaKind.War, warIndex);
        };

        System.Action MetaLink(MetaKind kind, int index) => () => _context.OpenMeta(kind, index);

        void OnLod(int lod)
        {
            var overlay = _host.Overlay;
            if (overlay == null) return;
            if (lod == 3 && overlay.Modes.Count == 0) { overlay.Toggle(OverlayMode.Kingdom); _autoOverlay = true; }
            else if (lod < 3 && _autoOverlay) { overlay.Modes.Remove(OverlayMode.Kingdom); _autoOverlay = false; }
        }

        string Kingdom(int k) => k >= 0 && k < _host.Sim.Meta.Kingdoms.Count ? _host.Sim.Meta.KingdomName(k) : "?";
        string CityName(int c) => _host.Sim.Meta.Names.Get(_host.Sim.Civ.Cities.Headers[c].NameId);

        void OnEraChanged(EraChangedEvent e) => Toast(Loc.F("ui.era_changed_fmt", Loc.Name(_host.Content.Eras[e.NewEra])));

        // Bölüm 10: a kingdom sent our (controlled) kingdom a peace or alliance proposal.
        void OnProposal(DiplomacyProposalEvent e)
        {
            var sim = _host.Sim;
            if (sim?.Meta == null) return;
            if ((sim.Meta.Kingdoms.Header(e.To).Flags & MetaFlags.PlayerControlled) == 0) return;
            string from = sim.Meta.KingdomAlive(e.From) ? sim.Meta.KingdomName(e.From) : "?";
            string key = e.Kind == ProposalKind.Alliance ? "ui.gov.proposal_alliance_fmt"
                : e.Kind == ProposalKind.TradeAgreement ? "ui.gov.proposal_trade_fmt"
                : e.Kind == ProposalKind.NonAggression ? "ui.gov.proposal_nonaggression_fmt"
                : e.Kind == ProposalKind.TradeEmbargo ? "ui.gov.proposal_embargo_fmt"
                : e.Kind == ProposalKind.Vassalage ? "ui.gov.proposal_vassalage_fmt" : "ui.gov.proposal_peace_fmt";
            _hud.Notify("kingdom", Loc.F(key, from),
                () => _context.OpenMeta(MetaKind.Kingdom, e.To));
        }

        void Update()
        {
            if (_host?.Sim == null) return;
            NotifyNewHistoryEvents(_host.Sim);
            var clock = _host.Sim.Clock;
            if (clock.Year != _scenarioYear)
            {
                _scenarioYear = clock.Year;
                if (!string.IsNullOrEmpty(_host.Sim.ScenarioId) && _host.Content.Scenarios.TryGet(_host.Sim.ScenarioId, out var scenario))
                {
                    var observed = ScenarioRules.EvaluateCurrent(_host.Sim, scenario).Status;
                    if (_host.Sim.ScenarioOutcome == ScenarioStatus.Active &&
                        (observed == ScenarioStatus.Won || observed == ScenarioStatus.Lost))
                        _host.Sim.ScenarioOutcome = observed;
                    var status = ScenarioRules.Evaluate(_host.Sim, scenario).Status;
                    if (status != _scenarioStatus && status == ScenarioStatus.Won) Toast(Loc.T("ui.scenario_won"));
                    else if (status != _scenarioStatus && status == ScenarioStatus.Lost) Toast(Loc.T("ui.scenario_lost"));
                    _scenarioStatus = status;
                }
            }
            float dt = Time.unscaledDeltaTime;
            Vector2 pointer = _input.Point.ReadValue<Vector2>();
            bool overUi = IsPointerOverUi(pointer);
            bool windowOpen = _worldGen.IsOpen || _saves.IsOpen;
            bool typing = IsTyping();

            if (!typing && !_possession.Active)
            {
                if (Keyboard.current?.ctrlKey.isPressed == true && Keyboard.current.fKey.wasPressedThisFrame) _hud.FocusSearch();
                if (Keyboard.current?.ctrlKey.isPressed == true && Keyboard.current.zKey.wasPressedThisFrame && _host.CanUndo)
                    UndoLastAction();
                for (int i = 0; i < _input.Speed.Length; i++)
                {
                    if (!_input.Speed[i].WasPressedThisFrame()) continue;
                    clock.SuperSpeed = false;
                    clock.SetSpeed(i);
                }
                if (_input.Step.WasPressedThisFrame()) clock.StepOnce();
                if (_input.BrushSizeUp.WasPressedThisFrame()) _hud.ChangeBrush(1);
                if (_input.BrushSizeDown.WasPressedThisFrame()) _hud.ChangeBrush(-1);
                if (_input.TabPrev.WasPressedThisFrame()) _hud.SelectTab(-1);
                if (_input.TabNext.WasPressedThisFrame()) _hud.SelectTab(1);
                if (_input.DebugOverlay.WasPressedThisFrame()) _debug.Visible = !_debug.Visible;
                if (_input.RegionOverlay.WasPressedThisFrame() && _host.Renderer != null)
                    _host.Renderer.ShowRegions = !_host.Renderer.ShowRegions;
                if (_input.CancelPower.WasPressedThisFrame())
                {
                    if (_menu.IsOpen) _menu.Close();
                    else if (windowOpen)
                    {
                        _worldGen.Close();
                        _saves.Close();
                    }
                    else if (_context.Windows.AnyOpen) _context.Windows.CloseTop();
                    else
                    {
                        _inspect = false; _context.Followed = -1;
                        _tool.Cancel();
                        _hud.RefreshSelection();
                    }
                }

                // Space: tap = pause, hold + left drag = pan.
                if (_input.PanModifier.WasPressedThisFrame()) _spaceUsedForDrag = false;
                if (_input.PanModifier.IsPressed() && _input.UsePower.IsPressed()) _spaceUsedForDrag = true;
                if (_input.PanModifier.WasReleasedThisFrame() && !_spaceUsedForDrag) clock.TogglePause();
            }

            bool spaceDrag = !typing && !_possession.Active && _input.PanModifier.IsPressed() && _input.UsePower.IsPressed();
            // Bölüm 1.14: brush aracı seçili değilken tek parmak sürükleme haritayı kaydırır.
            bool oneFingerPan = !_tool.Active && !_possession.Active;
            bool mapTapReady = !_tool.Active && _pendingTarget == null && !_possession.Active && !windowOpen && !typing;
            if (_input.UsePower.WasPressedThisFrame())
            {
                _mapTapCandidate = mapTapReady && !overUi && !_input.PanModifier.IsPressed();
                _mapTapStart = pointer;
            }

            if (!_possession.Active) _camera.Tick(_input, overUi || windowOpen || typing, spaceDrag, dt, oneFingerPan);
            // TouchSteering (two-finger pan/zoom) must not also paint the map.
            bool held = _input.UsePower.IsPressed() && !spaceDrag && !windowOpen && !_camera.IsDragging && !_camera.TouchSteering && !typing && !_possession.Active;
            Vector2 world = _camera.ScreenToWorld(pointer);
            var tile = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
            var map = _host.Sim.World;
            if (_input.UsePower.WasReleasedThisFrame())
            {
                if (_mapTapCandidate && mapTapReady && !overUi && !_camera.IsDragging && !_camera.TouchSteering &&
                    !_input.PanModifier.IsPressed() && Vector2.Distance(pointer, _mapTapStart) <= 10f && map.InBounds(tile.x, tile.y))
                {
                    if (_inspect)
                    {
                        int unit = PickUnit(world);
                        if (unit >= 0) _context.OpenUnit(unit);
                        else
                        {
                            int city = _host.Sim.Civ?.CityAtTile(tile.x, tile.y) ?? -1;
                            if (city >= 0) _context.OpenMeta(MetaKind.City, city);
                            else OpenTileDetails(tile, -1);
                        }
                    }
                    else OpenTileDetails(tile, PickUnit(world));
                }
                _mapTapCandidate = false;
            }
            if (_pendingTarget != null && !_tool.Active && held && _input.UsePower.WasPressedThisFrame() && !overUi)
            {
                var power = _pendingTarget;
                _pendingTarget = null;
                ApplyTarget(power, world, tile);
            }
            _possession.Update(_input, world, overUi || windowOpen || typing);
            if (!_possession.Active && _context.Followed >= 0)
            {
                if (_host.Sim.Units.Store.IsAlive(_context.Followed)) _camera.Focus(_host.Sim.Units.Store.Pos[_context.Followed]);
                else _context.Followed = -1;
            }
            if (held && _tool.Active && !overUi)
            {
                var power = _host.Content.Powers[_tool.SelectedPower];
                if (!IsScenarioPowerAllowed(power))
                {
                    _tool.Cancel();
                    Toast(Loc.T("ui.scenario_power_blocked"));
                }
            }
            if (held && _tool.Active && !overUi)
            {
                var power = _host.Content.Powers[_tool.SelectedPower];
                _lastPowerTab = power.Tab;
                if (_input.UsePower.WasPressedThisFrame() || power.Type == "brush")
                {
                    _tutorialPowerActions++;
                    _host.Audio?.PlayPower(power);
                }
            }
            _tool.Update(_host, tile, held, _input.UsePower.WasPressedThisFrame(), overUi);

            int selectedPower = _tool.SelectedPower;
            if (selectedPower != _lastCursorPower)
            {
                _lastCursorPower = selectedPower;
                _cursor.SetTint(CursorTint(selectedPower));
            }
            if (_tool.Active && !overUi && !windowOpen && map.InBounds(tile.x, tile.y)) _cursor.Show(tile, _tool.Radius, _tool.Shape);
            else _cursor.Hide();

            _hud.Update();
            _worldGen.Update();
            _debug.Update(_host, world);
            _context.Windows.Update();
            // Panel pixels differ from device pixels when UI scaling is active.
            float panelWidth = _layer.resolvedStyle.width;
            _layer.EnableInClassList("pg-narrow", Screen.width < Screen.height || (panelWidth > 0f && panelWidth < 900f));
            _layer.EnableInClassList("pg-touch", PlatformInfo.IsTouchDevice);
            // Bölüm 1.14 mobile: keep the HUD inside Screen.safeArea (punch-holes, rounded corners).
            // Layer insets are panel-space, so convert screen pixels by the panel's scale.
            var sa = Screen.safeArea;
            var panel = _root.panel;
            float spp = panel != null && panel.scaledPixelsPerPoint > 0f ? panel.scaledPixelsPerPoint : 1f;
            _layer.style.left = sa.x / spp;
            _layer.style.right = (Screen.width - sa.xMax) / spp;
            _layer.style.top = (Screen.height - sa.yMax) / spp;
            _layer.style.bottom = sa.y / spp;
            _plates.Update();
            _historyMarkers.Update();
            _minimap.Update();
            _stats.Update(_host.Sim);
            _achievements.Update();
            _tutorial.Update();
            Discover();
        }

        async void UndoLastAction()
        {
            _tool.Cancel();
            try
            {
                await _host.UndoLastAction();
                _hud.Toast(Loc.T("ui.undo_complete"));
            }
            catch (Exception e)
            {
                _hud.Toast(Loc.F("ui.undo_failed", e.Message));
            }
        }

        bool IsScenarioPowerAllowed(PowerDef power)
        {
            var sim = _host?.Sim;
            if (sim == null || string.IsNullOrEmpty(sim.ScenarioId)) return true;
            if (!_host.Content.Scenarios.TryGet(sim.ScenarioId, out var scenario)) return true;
            return HudView.IsUiPower(power) || ScenarioRules.CanUsePower(scenario, power.Id);
        }

        void NotifyNewHistoryEvents(SimWorld sim)
        {
            if (_historyWorld != sim)
            {
                _historyWorld = sim;
                _lastNotifiedHistoryEventId = sim.Meta.NextHistoryEventId - 1;
                return;
            }

            var history = sim.Meta.History;
            var details = sim.Meta.HistoryDetails;
            if (details.Length == 0 || sim.Meta.NextHistoryEventId <= _lastNotifiedHistoryEventId + 1) return;

            int start = details.Length;
            while (start > 0 && details[start - 1].EventId > _lastNotifiedHistoryEventId) start--;
            System.Action documentaryFocus = null;
            for (int i = start; i < details.Length && i < history.Length; i++)
            {
                var metadata = details[i];
                if (metadata.EventId <= _lastNotifiedHistoryEventId) continue;
                var entry = history[i];
                bool watchedKingdomEvent = _context.Profile.WatchesKingdomEvent(sim.Seed, sim.World.Width, sim.World.Height,
                    entry.A, entry.B, entry.C);
                if (HistoryNotificationPolicy.ShouldNotify(metadata.Importance, _context.Profile.HistoryNotificationThreshold, watchedKingdomEvent))
                {
                    string text = HistoryWindow.Describe(sim, entry, i);
                    var kind = (HistoryKind)entry.Template;
                    var navigate = HistoryWindow.CreateHistoryLink(_context, sim, entry, text);
                    _hud.Notify(HistoryNotificationCategory(kind), text, navigate ?? (() =>
                        _context.Windows.Show("history", () => new HistoryWindow(_context))));

                    if (_context.Profile.DocumentaryCameraMode && HistoryWindow.HasHistoryFocus(sim, entry))
                        documentaryFocus = HistoryWindow.CreateHistoryLink(_context, sim, entry, text, false) ?? documentaryFocus;
                }
                _lastNotifiedHistoryEventId = metadata.EventId;
            }
            documentaryFocus?.Invoke();
        }

        static string HistoryNotificationCategory(HistoryKind kind)
        {
            switch (kind)
            {
                case HistoryKind.WarDeclared: case HistoryKind.Peace: case HistoryKind.CityCaptured:
                case HistoryKind.CityPillaged: return "war";
                case HistoryKind.CityFounded: case HistoryKind.CityRuined: case HistoryKind.Disaster:
                case HistoryKind.PopulationMigrated: return "city";
                case HistoryKind.KingdomFounded: case HistoryKind.KingdomFell: case HistoryKind.AllianceFormed:
                case HistoryKind.Vassalage: case HistoryKind.TributePaid: return "kingdom";
                case HistoryKind.NewKing: case HistoryKind.KingDied: return "king";
                case HistoryKind.Rebellion: return "rebellion";
                case HistoryKind.Famine: return "famine";
                case HistoryKind.ReligionFounded: return "religion";
                case HistoryKind.ItemForged: return "item";
                case HistoryKind.InventionDiscovered: return "discovery";
                default: return "misc";
            }
        }

        void OpenTileDetails(Vector2Int tile, int unit)
        {
            var details = _context.Windows.Show("tile-details", () => new TileDetailsWindow(_context, tile, unit));
            if (details == null) return;
            details.SetTarget(tile, unit);
            if (Screen.width < Screen.height)
            {
                details.Root.style.width = Length.Percent(96f);
                details.Root.style.left = Length.Percent(2f);
            }
            else details.Root.style.width = 380;
        }

        // Brush cursor tint follows the selected power's tab (bright tints keep the outline readable).
        Color CursorTint(int powerId)
        {
            if (powerId < 0 || _host.Content == null || powerId >= _host.Content.Powers.Count) return Color.white;
            return _host.Content.Powers[powerId].Tab switch
            {
                "world" => new Color(0.86f, 1f, 0.78f),
                "civ" => new Color(1f, 0.9f, 0.74f),
                "creatures" => new Color(1f, 0.84f, 0.9f),
                "nature" => new Color(0.76f, 0.9f, 1f),
                "destruction" => new Color(1f, 0.8f, 0.74f),
                _ => new Color(0.9f, 0.9f, 1f),
            };
        }

        bool IsPointerOverUi(Vector2 screen)
        {
            var panel = _root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(p) != null;
        }

        bool IsTyping()
        {
            var focused = _root?.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        }

        static VisualElement CreatePanel(GameObject host, out PanelSettings settings)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "PG_Panel";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            // Phones are far smaller than the desktop reference: a lower reference keeps text and buttons tappable.
            settings.referenceResolution = PlatformInfo.IsTouchDevice ? new Vector2Int(960, 540) : new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            var root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var style = Resources.Load<StyleSheet>(StyleResource);
            if (style != null) root.styleSheets.Add(style);
            return root;
        }

        // Content failed to load: nothing else can run. Localization is unavailable, so the header comes from the caller.
        public static void ShowFatal(string header, IReadOnlyList<string> errors)
        {
            var go = new GameObject("FatalError");
            var root = CreatePanel(go, out _);
            var panel = new ScrollView();
            panel.AddToClassList("pg-fatal");
            root.Add(panel);
            var title = new Label(header);
            title.AddToClassList("pg-window-title");
            panel.Add(title);
            foreach (var e in errors)
            {
                var line = new Label("• " + e);
                line.AddToClassList("pg-fatal-text");
                panel.Add(line);
            }
        }

        // Bölüm 9.6 mobile: the simulation pauses while the app is in the background.
        void OnApplicationPause(bool paused)
        {
            var clock = _host?.Sim?.Clock;
            if (paused && PlatformInfo.IsTouchDevice && clock != null && !clock.Paused) clock.TogglePause();
            if (paused) _context?.Profile.Save();
        }

        void OnDestroy()
        {
            _context?.Profile.Save();
            if (_host != null) _host.WorldChanged -= OnWorldChanged;
            _input?.Dispose();
        }
    }
}
