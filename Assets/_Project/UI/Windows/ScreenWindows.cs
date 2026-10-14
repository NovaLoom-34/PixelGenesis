using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Persistence;
using PG.Powers;
using PG.Render;
using PG.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.11 era clock: 8 slots drawn with Painter2D, a needle on the current slot, slot assignment, change now, freeze, enable list.
    public sealed class EraClockWindow : WindowBase
    {
        const float Size = 220f;
        public EraClockWindow(UiContext ui) : base(ui.Host, Loc.T("ui.era_clock")) { Rebuild(); }

        protected override void Refresh() { }

        void Send(EraOrder order, int era, int value = 0) { Host.Edits.Eras.Add(new EraCommand { Order = order, Era = era, Value = value }); Rebuild(); }

        protected override void Build()
        {
            var era = Host.Sim.Nature.Era;
            var eras = Host.Content.Eras;
            var dial = new VisualElement();
            dial.style.width = Size; dial.style.height = Size; dial.style.alignSelf = Align.Center;
            int slot = era.CurrentSlot;
            dial.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                var c = new Vector2(Size / 2, Size / 2);
                for (int s = 0; s < EraState.SlotCount; s++)
                {
                    float a0 = s * 45f - 90f - 22f, a1 = a0 + 44f;
                    p.fillColor = s == slot ? UiTheme.DialActive : era.ClockSlots[s] > 0 ? UiTheme.DialSet : UiTheme.DialIdle;
                    p.BeginPath(); p.MoveTo(c); p.Arc(c, Size / 2 - 4, a0, a1); p.ClosePath(); p.Fill();
                }
                float a = (slot * 45f - 90f) * Mathf.Deg2Rad;
                p.strokeColor = UiTheme.Needle; p.lineWidth = 3;
                p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (Size / 2 - 16)); p.Stroke();
            };
            for (int s = 0; s < EraState.SlotCount; s++)
            {
                float a = (s * 45f - 90f) * Mathf.Deg2Rad;
                int e = era.ClockSlots[s] - 1;
                var l = new Label(e >= 0 ? Loc.Name(eras[e]) : "—") { pickingMode = PickingMode.Ignore };
                l.AddToClassList("pg-clock-label");
                l.style.position = Position.Absolute;
                l.style.left = Size / 2 + Mathf.Cos(a) * Size * 0.3f - 34; l.style.top = Size / 2 + Mathf.Sin(a) * Size * 0.3f - 8;
                dial.Add(l);
            }
            Body.Add(dial);
            Line(Loc.F("ui.era_fmt", Loc.Name(eras[era.CurrentEra]), Mathf.Max(0, era.StartedYear + era.DurationYears - Host.Sim.Clock.Year)));
            var top = Row();
            var freeze = Small(era.Frozen ? "ui.era_unfreeze" : "ui.era_freeze", () => Send(EraOrder.Freeze, -1, era.Frozen ? 0 : 1));
            top.Add(freeze);

            var names = new List<string> { "—" };
            foreach (var d in eras.All) names.Add(Loc.Name(d));
            Heading("ui.era_slots");
            for (int s = 0; s < EraState.SlotCount; s++)
            {
                int slotIndex = s;
                var pick = new PopupField<string>(Loc.F("ui.slot_fmt", s + 1), names, era.ClockSlots[s]);
                pick.RegisterValueChangedCallback(_ => Send(EraOrder.Slot, pick.index - 1, slotIndex));
                Body.Add(pick);
            }
            Heading("ui.era_list");
            for (int i = 0; i < eras.Count; i++)
            {
                int e = i;
                var row = Row();
                var on = new Toggle(Loc.Name(eras[i])) { value = era.Enabled[i] };
                on.RegisterValueChangedCallback(v => Send(EraOrder.Enable, e, v.newValue ? 1 : 0));
                row.Add(on);
                row.Add(Small("ui.era_change_now", () => Send(EraOrder.ChangeNow, e)));
            }
        }
    }

    // Recent yearly samples are transient; persisted century summaries seed the graph after a world is loaded.
    public sealed class StatsRecorder
    {
        public const int MaxYears = 1000;
        public readonly List<int> Years = new List<int>();
        public readonly Dictionary<string, List<float>> Series = new Dictionary<string, List<float>>();
        int _last = -1;
        bool _summariesLoaded;

        public static readonly string[] WorldSeries = { "population", "civ_population", "wildlife_population", "cities", "kingdoms", "wars", "sick", "undead" };

        public void Reset() { Years.Clear(); Series.Clear(); _last = -1; _summariesLoaded = false; }

        public void Update(SimWorld sim)
        {
            if (!_summariesLoaded) SeedWorldSummaries(sim.Meta);
            int year = sim.Clock.Year;
            if (year == _last) return;
            _last = year;
            var u = sim.Units.Store;
            int sick = 0, undead = 0, civPopulation = 0;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.Disease[i] != 0) sick++;
                var species = sim.Content.Species[u.Species[i]];
                if (species.IsUndeadCategory) undead++;
                if (species.IsCiv) civPopulation++;
            }
            int cities = 0, kingdoms = 0, wars = 0;
            if (sim.Civ != null) for (int c = 0; c < sim.Civ.CityCount; c++) if (sim.Civ.CityAlive(c)) cities++;
            for (int k = 0; k < sim.Meta.Kingdoms.Count; k++) if (sim.Meta.Kingdoms.IsAlive(k)) kingdoms++;
            for (int w = 0; w < sim.Meta.Wars.Count; w++) if (sim.Meta.Wars.IsAlive(w)) wars++;
            if (Years.Count == 0 || Years[Years.Count - 1] != year) Years.Add(year);
            int n = Years.Count;
            Put("population", u.Count, n); Put("civ_population", civPopulation, n);
            Put("wildlife_population", u.Count - civPopulation, n); Put("cities", cities, n); Put("kingdoms", kingdoms, n);
            Put("wars", wars, n); Put("sick", sick, n); Put("undead", undead, n);
            for (int k = 0; k < sim.Meta.Kingdoms.Count; k++)
                Put("k" + sim.Meta.Kingdoms.Headers[k].Uid, sim.Meta.Kingdoms.IsAlive(k) ? sim.Meta.Kingdoms.Headers[k].MemberCount : 0, n);
            if (n > MaxYears)
            {
                Years.RemoveAt(0);
                foreach (var s in Series.Values) s.RemoveAt(0);
            }
        }

        public void SeedWorldSummaries(MetaWorld meta)
        {
            _summariesLoaded = true;
            for (int i = 0; i < meta.WorldSummaries.Length; i++)
            {
                var report = meta.WorldSummaries[i];
                if (report.Kind != WorldSummaryKind.Century) continue;
                if (Years.Count > 0 && Years[Years.Count - 1] >= report.EndYear) continue;
                Years.Add(report.EndYear);
                int n = Years.Count;
                Put("population", report.Population, n);
                var split = i < meta.WorldSummaryPopulationSplits.Length
                    ? meta.WorldSummaryPopulationSplits[i] : WorldSummaryPopulationSplit.Unknown;
                Put("civ_population", split.Civilization >= 0 ? split.Civilization : float.NaN, n);
                Put("wildlife_population", split.Wildlife >= 0 ? split.Wildlife : float.NaN, n);
                Put("cities", report.Cities, n);
                Put("kingdoms", report.Kingdoms, n);
                Put("wars", report.Wars, n);
                Put("sick", report.Sick, n);
                Put("undead", report.Undead, n);
                if (n > MaxYears)
                {
                    Years.RemoveAt(0);
                    foreach (var series in Series.Values) series.RemoveAt(0);
                }
            }
        }

        void Put(string key, float v, int n)
        {
            if (!Series.TryGetValue(key, out var list)) { list = new List<float>(); Series[key] = list; }
            if (list.Count == n) { list[n - 1] = v; return; }
            while (list.Count < n - 1) list.Add(0f); // series born later are zero before
            list.Add(v);
        }
    }

    // Bölüm 8.11 graphs: Painter2D line chart, world series or up to 8 kingdoms, range 10/50/100/1000 years.
    public sealed class GraphsWindow : WindowBase
    {
        public const int MaxSelected = 8;
        static readonly int[] Ranges = { 10, 50, 100, 1000 };
        static readonly Color[] Palette = UiTheme.GraphPalette;

        readonly StatsRecorder _rec;
        readonly List<string> _selected = new List<string> { "population" };
        int _range = 1;
        VisualElement _chart;
        int _drawnYears = -1;

        public GraphsWindow(UiContext ui, StatsRecorder rec) : base(ui.Host, Loc.T("ui.graphs")) { _rec = rec; Rebuild(); }

        protected override void Refresh()
        {
            if (_rec.Years.Count != _drawnYears) { _drawnYears = _rec.Years.Count; _chart?.MarkDirtyRepaint(); }
        }

        protected override void Build()
        {
            var ranges = Row();
            for (int r = 0; r < Ranges.Length; r++)
            {
                int index = r;
                var b = Raw(Loc.F("ui.years_fmt", Ranges[r]), () => { _range = index; Rebuild(); });
                b.EnableInClassList("pg-button--selected", r == _range);
                ranges.Add(b);
            }
            _chart = new VisualElement();
            _chart.AddToClassList("pg-chart");
            _chart.style.height = 180;
            _chart.generateVisualContent += Draw;
            Body.Add(_chart);

            var series = Row();
            foreach (var key in StatsRecorder.WorldSeries) SeriesToggle(series, key, Loc.T("ui.series." + key));
            var sim = Host.Sim;
            Heading("ui.kingdoms");
            var kingdoms = Row();
            for (int k = 0; k < sim.Meta.Kingdoms.Count; k++)
                if (sim.Meta.Kingdoms.IsAlive(k)) SeriesToggle(kingdoms, "k" + sim.Meta.Kingdoms.Headers[k].Uid, Names.Kingdom(sim, k));
        }

        void SeriesToggle(VisualElement row, string key, string label)
        {
            int idx = _selected.IndexOf(key);
            var b = Raw(label, () =>
            {
                if (!_selected.Remove(key) && _selected.Count < MaxSelected) _selected.Add(key);
                Rebuild();
            });
            if (idx >= 0) { b.AddToClassList("pg-button--selected"); b.style.color = Palette[idx % Palette.Length]; }
            row.Add(b);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var rect = _chart.contentRect;
            int n = _rec.Years.Count;
            if (n < 2 || rect.width < 10) return;
            int from = Mathf.Max(0, n - Ranges[_range]);
            float max = 1f;
            foreach (var key in _selected)
                if (_rec.Series.TryGetValue(key, out var s))
                    for (int i = from; i < s.Count; i++) if (!float.IsNaN(s[i]) && !float.IsInfinity(s[i])) max = Mathf.Max(max, s[i]);
            var p = ctx.painter2D;
            p.lineWidth = 2;
            for (int k = 0; k < _selected.Count; k++)
            {
                if (!_rec.Series.TryGetValue(_selected[k], out var s)) continue;
                p.strokeColor = Palette[k % Palette.Length];
                p.BeginPath();
                bool hasPoint = false;
                for (int i = from; i < s.Count; i++)
                {
                    if (float.IsNaN(s[i]) || float.IsInfinity(s[i])) { hasPoint = false; continue; }
                    var pt = new Vector2(rect.width * (i - from) / Mathf.Max(1, n - 1 - from), rect.height * (1f - s[i] / max));
                    if (!hasPoint) p.MoveTo(pt); else p.LineTo(pt);
                    hasPoint = true;
                }
                p.Stroke();
            }
        }
    }

    public sealed class AchievementsWindow : WindowBase
    {
        readonly UiContext _ui;
        public AchievementsWindow(UiContext ui) : base(ui.Host, Loc.T("ui.achievements")) { _ui = ui; Rebuild(); }
        protected override void Refresh() { }
        protected override void Build()
        {
            int earned = 0;
            foreach (var a in Host.Content.Achievements.All) if (_ui.Profile.HasAchievement(a.Id)) earned++;
            Line(Loc.F("ui.achievements_fmt", earned, Host.Content.Achievements.Count));
            var export = Row();
            export.Add(Small("ui.achievement_export", () => WorldShareCode.ExportAchievements(Host.Content, _ui.Profile, _ui.Toast)));
            if (Host.Sim.Nature.Laws.IsOn("law.forbidden_codex")) Line(Loc.T("ui.codex_no_achievements"));
            foreach (var a in Host.Content.Achievements.All)
            {
                bool has = _ui.Profile.HasAchievement(a.Id);
                var l = Line((has ? "★ " : "☆ ") + Loc.Name(a) + " — " + a.Condition);
                l.EnableInClassList("pg-dim", !has);
            }
        }
    }

    // Bölüm 8.7 overlay picker: any number of modes; active ones alternate each second.
    public sealed class OverlayWindow : WindowBase
    {
        Label _seasonLegend;
        public OverlayWindow(UiContext ui) : base(ui.Host, Loc.T("ui.overlays")) { Rebuild(); }
        protected override void Refresh()
        {
            if (_seasonLegend != null) _seasonLegend.text = CurrentSeasonLegend();
        }
        protected override void Build()
        {
            var overlay = Host.Overlay;
            if (overlay == null) return;
            var row = Row();
            foreach (OverlayMode mode in Enum.GetValues(typeof(OverlayMode)))
            {
                var m = mode;
                var b = Small("ui.overlay." + mode.ToString().ToLowerInvariant(), () => { overlay.Toggle(m); Rebuild(); });
                b.EnableInClassList("pg-button--selected", overlay.Modes.Contains(mode));
                row.Add(b);
            }
            var legend = new Label(Loc.T("ui.overlay.legend_note"));
            legend.AddToClassList("pg-dim");
            Body.Add(legend);
            Body.Add(new Label(Loc.T("ui.overlay.legend_moisture")));
            Body.Add(new Label(Loc.T("ui.overlay.legend_cropyield")));
            Body.Add(new Label(Loc.T("ui.overlay.legend_protection")));
            _seasonLegend = new Label(CurrentSeasonLegend());
            Body.Add(_seasonLegend);
            Body.Add(Small("ui.overlay_off", () => { overlay.Modes.Clear(); Rebuild(); }));
        }

        string CurrentSeasonLegend()
        {
            string season;
            switch (Host.Sim.Nature.Season.Current)
            {
                case Season.Spring: season = "ui.season.spring"; break;
                case Season.Summer: season = "ui.season.summer"; break;
                case Season.Autumn: season = "ui.season.autumn"; break;
                default: season = "ui.season.winter"; break;
            }
            return Loc.F("ui.overlay.legend_season_fmt", Loc.T(season));
        }
    }

    // Bölüm 8.14 tutorial: 8 steps, each waits for a condition read from the world or the UI context.
    public sealed class Tutorial
    {
        readonly UiContext _ui;
        readonly VisualElement _panel;
        readonly Label _text;
        int _step;
        long _startTick;
        int _startLand = -1;
        int _startCivPopulation, _startCreaturePopulation;
        int _startCityInspected, _startLawsOpened;
        long _startPowerActionCount;
        SimWorld _trackedWorld;
        bool _wasActive;
        public bool Saved;
        public Func<string> LastPowerTab = () => null;
        public Func<long> PowerActionCount = () => 0;

        public Tutorial(UiContext ui, VisualElement layer)
        {
            _ui = ui;
            _panel = new VisualElement();
            _panel.AddToClassList("pg-tutorial");
            _text = new Label();
            _text.AddToClassList("pg-line");
            _panel.Add(_text);
            _panel.Add(WindowBase.Small("ui.tutorial_skip", Finish));
            layer.Add(_panel);
            _panel.style.display = ui.Profile.TutorialDone ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public bool Active => !_ui.Profile.TutorialDone;

        public void Update()
        {
            var sim = _ui.Sim;
            if (!Active) { _wasActive = false; return; }
            if (!_wasActive)
            {
                _wasActive = true;
                _step = 0;
                _trackedWorld = null;
                Saved = false;
            }
            if (sim == null) return;
            if (_trackedWorld != sim)
            {
                _trackedWorld = sim;
                _step = 0;
                Saved = false;
                CaptureStepBaseline(sim);
            }
            if (Done(sim))
            {
                _step++;
                CaptureStepBaseline(sim);
                if (_step >= 8) { Finish(); return; }
            }
            string instructionKey = "ui.tutorial." + (_step + 1) + (PlatformInfo.IsTouchDevice ? ".mobile" : ".desktop");
            _text.text = Loc.F("ui.tutorial_fmt", _step + 1, 8, Loc.T(instructionKey));
        }

        bool Done(SimWorld sim)
        {
            switch (_step)
            {
                case 0: return LastPowerTab() == "world" && Land(sim) != _startLand;
                case 1: return PopulationActionCompleted(LastPowerTab(), "creatures", _startCreaturePopulation, CreaturePopulation(sim));
                case 2: return PopulationActionCompleted(LastPowerTab(), "civ", _startCivPopulation, CivPopulation(sim));
                case 3: return sim.Clock.Tick - _startTick >= SimConst.TicksPerYear;
                case 4: return CounterActionCompleted(_startCityInspected, _ui.CityInspected);
                case 5: return PowerActionCompleted(LastPowerTab(), _startPowerActionCount, PowerActionCount());
                case 6: return CounterActionCompleted(_startLawsOpened, _ui.LawsOpened);
                case 7: return Saved;
            }
            return true;
        }

        void CaptureStepBaseline(SimWorld sim)
        {
            _startTick = sim.Clock.Tick;
            _startLand = Land(sim);
            _startCivPopulation = CivPopulation(sim);
            _startCreaturePopulation = CreaturePopulation(sim);
            _startCityInspected = _ui.CityInspected;
            _startLawsOpened = _ui.LawsOpened;
            _startPowerActionCount = PowerActionCount();
        }

        static int CivPopulation(SimWorld sim)
        {
            int count = 0;
            var units = sim.Units.Store;
            for (int k = 0; k < units.Alive.Length; k++)
                if (sim.Content.Species[units.Species[units.Alive[k]]].IsCiv) count++;
            return count;
        }

        static int CreaturePopulation(SimWorld sim)
        {
            int count = 0;
            var units = sim.Units.Store;
            for (int k = 0; k < units.Alive.Length; k++)
            {
                var species = sim.Content.Species[units.Species[units.Alive[k]]];
                if (!species.IsCiv && !species.IsUndeadCategory) count++;
            }
            return count;
        }

        public static bool PopulationActionCompleted(string activeTab, string expectedTab, int before, int after) =>
            string.Equals(activeTab, expectedTab, StringComparison.Ordinal) && after > before;

        public static bool CounterActionCompleted(int before, int after) => after > before;

        public static bool PowerActionCompleted(string activeTab, long before, long after) =>
            (string.Equals(activeTab, "nature", StringComparison.Ordinal) ||
             string.Equals(activeTab, "destruction", StringComparison.Ordinal)) && after > before;

        // coarse land count from the zone cache (LandTiles per zone)
        static int Land(SimWorld sim)
        {
            int n = 0;
            var zones = sim.World.Zones;
            for (int z = 0; z < zones.Length; z++) n += zones[z].LandTiles;
            return n;
        }

        public void Restart()
        {
            _ui.Profile.TutorialDone = false;
            _ui.Profile.Dirty = true;
            _step = 0;
            _startLand = -1;
            _trackedWorld = null;
            _wasActive = true;
            Saved = false;
            _panel.style.display = DisplayStyle.Flex;
        }

        void Finish()
        {
            _ui.Profile.TutorialDone = true;
            _ui.Profile.Dirty = true;
            _ui.Profile.Save();
            _panel.style.display = DisplayStyle.None;
        }
    }

    // Bölüm 9.6: "What's new" from the bundled CHANGELOG.md (latest two sections), shown once per game version.
    public sealed class WhatsNewWindow : WindowBase
    {
        public WhatsNewWindow(UiContext ui) : base(ui.Host, Loc.T("ui.whats_new") + " · " + Application.version) { Rebuild(); }
        protected override void Refresh() { }
        protected override void Build()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "CHANGELOG.md");
            if (!System.IO.File.Exists(path)) { Line("-"); return; }
            int sections = 0;
            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                if (raw.StartsWith("## ", StringComparison.Ordinal) && ++sections > 2) break;
                if (sections == 0 || raw.Trim().Length == 0) continue;
                if (raw.StartsWith("#", StringComparison.Ordinal)) { var h = new Label(raw.TrimStart('#', ' ')); h.AddToClassList("pg-heading"); Body.Add(h); }
                else Line(raw.TrimStart('-', ' '));
            }
        }
    }

    // Bölüm 8.14 main menu over the idle default world: continue, new world, load, settings, achievements, quit.
    // The veil is visual only (picking ignored) so world interaction outside the card behaves exactly as before.
    public sealed class MainMenu
    {
        readonly VisualElement _root;
        readonly VisualElement _panel;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public MainMenu(UiContext ui, VisualElement layer, Action newWorld, Action load, Action<string> toast)
        {
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            _root.AddToClassList("pg-mainmenu-root");
            var veil = new VisualElement { pickingMode = PickingMode.Ignore };
            veil.AddToClassList("pg-mainmenu-veil");
            _root.Add(veil);
            _panel = new VisualElement();
            _panel.AddToClassList("pg-mainmenu");
            var title = new Label(Loc.T("ui.game_title"));
            title.AddToClassList("pg-mainmenu-title");
            _panel.Add(title);
            var subtitle = new Label(Loc.T("ui.mainmenu.subtitle"));
            subtitle.AddToClassList("pg-mainmenu-subtitle");
            _panel.Add(subtitle);
            var divider = new VisualElement();
            divider.AddToClassList("pg-mainmenu-divider");
            _panel.Add(divider);
            SaveSlotInfo latest = null;
            foreach (var s in SaveSystem.ListSlots())
                if (s.Exists && (latest == null || s.CreatedUtc > latest.CreatedUtc)) latest = s;
            var cont = WindowBase.Small("ui.continue", async () =>
            {
                Close();
                try { await ui.Host.LoadSlot(latest.Slot); }
                catch (Exception e) { toast(Loc.F("ui.load_failed", e.Message)); }
            }, "continue");
            cont.SetEnabled(latest != null);
            cont.AddToClassList("pg-button--primary");
            _panel.Add(cont);
            var newWorldBtn = WindowBase.Small("ui.new_world", () => { Close(); newWorld(); }, "new_world");
            newWorldBtn.AddToClassList("pg-button--primary");
            _panel.Add(newWorldBtn);
            _panel.Add(WindowBase.Small("ui.load", () => { Close(); load(); }, "load"));
            _panel.Add(WindowBase.Small("ui.settings", () => { Close(); ui.Windows.Show("settings", () => new SettingsWindow(ui)); }, "settings"));
            _panel.Add(WindowBase.Small("ui.achievements", () => { Close(); ui.Windows.Show("achievements", () => new AchievementsWindow(ui)); }, "achievements"));
            _panel.Add(WindowBase.Small("ui.play_now", Close, "play_now"));
            _panel.Add(WindowBase.Small("ui.quit", Application.Quit, "quit"));
            var footer = new Label("v" + Application.version);
            footer.AddToClassList("pg-mainmenu-footer");
            _panel.Add(footer);
            _root.Add(_panel);
            layer.Add(_root);
        }

        public void Open() { _root.style.display = DisplayStyle.Flex; _root.BringToFront(); }
        public void Close() => _root.style.display = DisplayStyle.None;
    }
}
