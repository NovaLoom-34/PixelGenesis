using System;
using System.Collections.Generic;
using PG.Core;
using PG.Powers;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    public sealed class LawsWindow : WindowBase
    {
        public LawsWindow(UiContext ui) : base(ui.Host, Loc.T("ui.laws")) { ui.LawsOpened++; Rebuild(); }
        protected override void Refresh() { }
        protected override void Build()
        {
            Line(Loc.T("ui.edit_next_tick"));
            string group = null;
            foreach (var law in Host.Content.Laws.All)
            {
                if (group != law.Group) { group = law.Group; Heading("ui.law_group." + group); }
                int id = law.Index;
                bool simulationDetail = law.Id == LawIds.SimulationDetail;
                var on = new Toggle(simulationDetail ? Loc.T("ui.law.simulation_detail") : Loc.Name(law)) { value = Host.Sim.Nature.Laws.IsOn(id) }; Body.Add(on);
                Slider slider = null;
                if (law.HasSlider) { slider = new Slider(law.Slider[0], law.Slider[1]) { value = Host.Sim.Nature.Laws.Value(id), showInputField = true }; Body.Add(slider); }
                Action send = () => Host.Edits.Laws.Add(new LawCommand { Law = id, On = on.value, Value = slider?.value ?? 0 });
                on.RegisterValueChangedCallback(_ => send());
                if (simulationDetail && slider != null)
                {
                    Line(Loc.T("ui.law.simulation_detail_note"));
                    slider.RegisterValueChangedCallback(e => { slider.SetValueWithoutNotify(Mathf.Round(e.newValue)); send(); });
                }
                else slider?.RegisterValueChangedCallback(_ => send());
            }
        }
    }

    public sealed class HistoryWindow : WindowBase
    {
        const int PageSize = 100;
        readonly UiContext _ui;
        string _query = "";
        string _personQuery = "";
        string _kingdomQuery = "";
        VisualElement _entries;
        Label _summary;
        Button _more;
        int _count = -1;
        int _limit = PageSize;
        int _peopleLimit = PageSize;
        int _kingdomLimit = PageSize;
        int _filter;
        int _thresholdAtBuild = -1;
        long _lastEventId;
        long _selectedBiographyUid;
        int _selectedBiographyEventCount = -1;
        long _selectedBiographyDeathTick;
        long _selectedBiographyLastEventTick;
        BiographyEventKind _selectedBiographyLastEventKind;
        int _biographyCount = -1, _kingdomCount = -1, _kingdomHistoryCount = -1;
        long _kingdomLastEventId;
        Button _peopleMore;
        Button _kingdomMore;

        public HistoryWindow(UiContext ui) : base(ui.Host, Loc.T("ui.history"))
        {
            _ui = ui;
            SetTabs("ui.tab_history", "ui.history_people", "ui.history_kingdom_tree");
        }

        protected override void Build()
        {
            if (Tab == 1) { BuildPeople(); return; }
            if (Tab == 2) { BuildKingdomTree(); return; }

            BuildTimeline();
        }

        void BuildTimeline()
        {
            var share = Row();
            var exportText = Small("ui.world_export_chronicle", () => WorldShareCode.ExportChronicle(Host.Sim, _ui.Toast));
            share.Add(exportText);
            var exportImage = Small("ui.world_export_summary", () => WorldShareCode.ExportSummary(Host.Sim, _ui.Toast));
            share.Add(exportImage);
            var copyCode = Small("ui.world_share_copy", () => WorldShareCode.CopyCode(_ui.WorldSettings, _ui.Toast));
            copyCode.SetEnabled(_ui.WorldSettings != null);
            share.Add(copyCode);
            Body.Add(share);
            _summary = Line(Loc.F("ui.world_summary_fmt", WorldShareCode.SummaryDetails(Host.Sim)), Body);
            var filters = new List<string> { Loc.T("ui.history_filter_all"), Loc.F("ui.history_filter_important_fmt", _ui.Profile.HistoryNotificationThreshold) };
            _thresholdAtBuild = _ui.Profile.HistoryNotificationThreshold;
            var filter = new PopupField<string>(Loc.T("ui.history_filter"), filters, _filter);
            filter.RegisterValueChangedCallback(e => { _filter = filter.index; _limit = PageSize; Fill(); });
            Body.Add(filter);
            var search = new TextField(Loc.T("ui.search")) { value = _query }; Body.Add(search);
            search.RegisterValueChangedCallback(e => { _query = e.newValue; Fill(); });
            _entries = new VisualElement(); Body.Add(_entries);
            _more = Small("ui.history_older", () => { _limit += PageSize; Fill(); });
            Body.Add(_more);
            Fill();
        }

        protected override void Refresh()
        {
            var meta = Host.Sim.Meta;
            if (Tab == 0)
            {
                if (_thresholdAtBuild != _ui.Profile.HistoryNotificationThreshold) { Rebuild(); return; }
                long lastEventId = meta.HistoryDetails.Length > 0 ? meta.HistoryDetails[meta.HistoryDetails.Length - 1].EventId : 0;
                if (_count != meta.History.Length || _lastEventId != lastEventId) Fill();
            }
            else if (Tab == 1)
            {
                bool changed = _biographyCount != meta.BiographyTracks.Length;
                if (!changed && _selectedBiographyUid != 0 && meta.TryGetBiography(_selectedBiographyUid, out int selected))
                {
                    var track = meta.BiographyTracks[selected];
                    var latest = track.EventCount > 0 ? meta.GetBiographyEvent(track.Uid, track.EventCount - 1) : default;
                    changed = track.EventCount != _selectedBiographyEventCount || track.DeathTick != _selectedBiographyDeathTick
                        || latest.Tick != _selectedBiographyLastEventTick || latest.Kind != _selectedBiographyLastEventKind;
                }
                if (changed) Rebuild();
            }
            else if (Tab == 2)
            {
                long lastEventId = meta.HistoryDetails.Length > 0 ? meta.HistoryDetails[meta.HistoryDetails.Length - 1].EventId : 0;
                if (_kingdomCount != meta.Kingdoms.Count || _kingdomHistoryCount != meta.History.Length || _kingdomLastEventId != lastEventId)
                    FillKingdomTree();
            }
        }

        void Fill()
        {
            if (_entries == null) return;
            _entries.Clear(); var meta = Host.Sim.Meta; var h = meta.History; _count = h.Length;
            if (_summary != null) _summary.text = Loc.F("ui.world_summary_fmt", WorldShareCode.SummaryDetails(Host.Sim));
            _lastEventId = meta.HistoryDetails.Length > 0 ? meta.HistoryDetails[meta.HistoryDetails.Length - 1].EventId : 0;
            int shown = 0;
            bool hasMore = false;
            for (int i = h.Length - 1; i >= 0; i--)
            {
                if (_filter == 1 && (i >= meta.HistoryDetails.Length || !HistoryNotificationPolicy.ShouldNotify(meta.HistoryDetails[i].Importance, _ui.Profile.HistoryNotificationThreshold))) continue;
                string text = Describe(Host.Sim, h[i], i);
                if (text.IndexOf(_query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (shown >= _limit) { hasMore = true; break; }
                var link = CreateHistoryLink(_ui, Host.Sim, h[i], text, true, i);
                VisualElement entry;
                if (link != null)
                {
                    var button = new Button(() => link()) { text = text };
                    button.AddToClassList("pg-history-entry");
                    button.AddToClassList("pg-button");
                    entry = button;
                }
                else
                {
                    var label = new Label(text);
                    label.AddToClassList("pg-history-entry");
                    entry = label;
                }
                _entries.Add(entry);
                shown++;
            }
            if (_more != null) _more.style.display = hasMore ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void BuildPeople()
        {
            var search = new TextField(Loc.T("ui.history_person_search")) { value = _personQuery };
            search.RegisterValueChangedCallback(e => { _personQuery = e.newValue; FillPeople(); });
            Body.Add(search);
            _entries = new VisualElement(); Body.Add(_entries);
            _peopleMore = Small("ui.history_older", () => { _peopleLimit += PageSize; FillPeople(); });
            Body.Add(_peopleMore);
            BuildSelectedBiography();
            FillPeople();
        }

        void FillPeople()
        {
            if (_entries == null) return;
            _entries.Clear();
            var sim = Host.Sim;
            var tracks = sim.Meta.BiographyTracks;
            var liveByUid = new Dictionary<long, int>();
            var units = sim.Units.Store;
            for (int i = 0; i < units.Alive.Length; i++)
            {
                int unit = units.Alive[i];
                liveByUid[units.Uid[unit]] = unit;
            }
            _biographyCount = tracks.Length;
            int shown = 0;
            bool hasMore = false;
            for (int i = tracks.Length - 1; i >= 0; i--)
            {
                var track = tracks[i];
                string name = BiographyName(sim, track, liveByUid);
                string species = track.Species < sim.Content.Species.Count ? Loc.Name(sim.Content.Species[track.Species]) : "";
                if (_personQuery.Length > 0
                    && name.IndexOf(_personQuery, StringComparison.OrdinalIgnoreCase) < 0
                    && species.IndexOf(_personQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (shown >= _peopleLimit) { hasMore = true; break; }
                long uid = track.Uid;
                int score = track.IsDead != 0 ? track.LevelAtDeath * 10 + track.KillsAtDeath
                    : liveByUid.TryGetValue(uid, out int unit) ? units.Level[unit] * 10 + units.Kills[unit] : 0;
                string date = Loc.F("ui.history_birth_fmt", track.BirthTick / SimConst.TicksPerYear + 1);
                var button = Raw(Loc.F("ui.history_person_record_fmt", name, species, date + " · " + Loc.F("ui.kills_fmt", score)), () =>
                {
                    _selectedBiographyUid = uid;
                    Rebuild();
                });
                button.AddToClassList("pg-history-entry");
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                _entries.Add(button);
                shown++;
            }
            if (shown == 0) Line(Loc.T("ui.history_no_people"), _entries);
            if (_peopleMore != null) _peopleMore.style.display = hasMore ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void BuildSelectedBiography()
        {
            _selectedBiographyEventCount = -1;
            _selectedBiographyDeathTick = 0;
            _selectedBiographyLastEventTick = 0;
            _selectedBiographyLastEventKind = default;
            if (_selectedBiographyUid == 0 || !Host.Sim.Meta.TryGetBiography(_selectedBiographyUid, out int index)) return;
            var sim = Host.Sim;
            var track = sim.Meta.BiographyTracks[index];
            _selectedBiographyEventCount = track.EventCount;
            _selectedBiographyDeathTick = track.DeathTick;
            if (track.EventCount > 0)
            {
                var latest = sim.Meta.GetBiographyEvent(track.Uid, track.EventCount - 1);
                _selectedBiographyLastEventTick = latest.Tick;
                _selectedBiographyLastEventKind = latest.Kind;
            }
            Heading("ui.history_biography");
            int live = FindLiveUnit(sim, track.Uid);
            string name = track.NameId >= 0 ? sim.Meta.Names.Get(track.NameId) : live >= 0 ? Names.Unit(sim, live) : "#" + track.Uid;
            Line(name);
            if (track.Species < sim.Content.Species.Count) Line(Loc.Name(sim.Content.Species[track.Species]));
            Line(Loc.F("ui.history_birth_fmt", track.BirthTick / SimConst.TicksPerYear + 1));
            if (track.DeathTick > 0) Line(Loc.F("ui.died_fmt", track.DeathTick / SimConst.TicksPerYear + 1));
            else if (live >= 0) Line(Loc.T("ui.follow") + " · " + Loc.F("ui.year_fmt", sim.Clock.Year + 1));
            AddBiographyRelation("ui.mother", track.MotherUid);
            AddBiographyRelation("ui.father", track.FatherUid);
            AddBiographyRelation("ui.mate", track.MateUid);
            if (live >= 0)
            {
                long uid = track.Uid;
                var go = Small("ui.go_to", () =>
                {
                    int current = FindLiveUnit(sim, uid);
                    if (current < 0) return;
                    FocusHistory(_ui, sim.Units.Store.Pos[current], name);
                    _ui.OpenUnit(current);
                });
                Body.Add(go);
            }
            for (int e = 0; e < track.EventCount; e++)
            {
                var biographyEvent = sim.Meta.GetBiographyEvent(track.Uid, e);
                string key = biographyEvent.Kind switch
                {
                    BiographyEventKind.Birth => "ui.history_bio_birth",
                    BiographyEventKind.Partnership => "ui.history_bio_partnership",
                    BiographyEventKind.Child => "ui.history_bio_child",
                    BiographyEventKind.Battle => "ui.history_bio_battle",
                    BiographyEventKind.Office => "ui.history_bio_office",
                    BiographyEventKind.Death => "ui.history_bio_death",
                    BiographyEventKind.Milestone => "ui.history_bio_milestone",
                    _ => "ui.history_bio_legendary_item",
                };
                Line(Loc.F("ui.history_bio_event_fmt", Loc.T(key), Loc.F("ui.year_fmt", biographyEvent.Tick / SimConst.TicksPerYear + 1)));
            }
        }

        void AddBiographyRelation(string key, long uid)
        {
            if (uid == 0 || !Host.Sim.Meta.TryGetBiography(uid, out int index)) return;
            var track = Host.Sim.Meta.BiographyTracks[index];
            var units = Host.Sim.Units.Store;
            string name = track.NameId >= 0 ? Host.Sim.Meta.Names.Get(track.NameId) : "#" + uid;
            for (int i = 0; i < units.Alive.Length; i++)
                if (units.Uid[units.Alive[i]] == uid) { name = Names.Unit(Host.Sim, units.Alive[i]); break; }
            Line(Loc.T(key) + ": " + name);
        }

        void BuildKingdomTree()
        {
            var search = new TextField(Loc.T("ui.search")) { value = _kingdomQuery };
            search.RegisterValueChangedCallback(e => { _kingdomQuery = e.newValue; _kingdomLimit = PageSize; FillKingdomTree(); });
            Body.Add(search);
            _entries = new VisualElement(); Body.Add(_entries);
            _kingdomMore = Small("ui.history_older", () => { _kingdomLimit += PageSize; FillKingdomTree(); });
            Body.Add(_kingdomMore);
            FillKingdomTree();
        }

        void FillKingdomTree()
        {
            if (_entries == null) return;
            _entries.Clear();
            var sim = Host.Sim;
            var kingdoms = sim.Meta.Kingdoms;
            _kingdomCount = kingdoms.Count;
            _kingdomHistoryCount = sim.Meta.History.Length;
            _kingdomLastEventId = sim.Meta.HistoryDetails.Length > 0 ? sim.Meta.HistoryDetails[sim.Meta.HistoryDetails.Length - 1].EventId : 0;
            var parents = new Dictionary<long, long>();
            var names = new Dictionary<long, string>(kingdoms.Count);
            for (int i = 0; i < kingdoms.Count; i++) names[kingdoms.Headers[i].Uid] = Names.Kingdom(sim, i);
            var history = sim.Meta.History;
            for (int i = 0; i < history.Length; i++)
            {
                var entry = history[i];
                if (entry.Template == (ushort)HistoryKind.Rebellion && entry.B != 0 && entry.C != 0)
                    parents[entry.C] = entry.B;
            }
            int shown = 0;
            bool hasMore = false;
            for (int i = 0; i < kingdoms.Count; i++)
            {
                long uid = kingdoms.Headers[i].Uid;
                string name = names[uid];
                string parentName = parents.TryGetValue(uid, out long parentUid) && names.TryGetValue(parentUid, out var resolvedParent)
                    ? resolvedParent : "";
                if (_kingdomQuery.Length > 0 && name.IndexOf(_kingdomQuery, StringComparison.OrdinalIgnoreCase) < 0
                    && parentName.IndexOf(_kingdomQuery, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (shown >= _kingdomLimit) { hasMore = true; break; }
                var header = kingdoms.Headers[i];
                int founded = (int)(header.FoundedTick / SimConst.TicksPerYear + 1);
                string ended = header.DiedTick > 0 ? Loc.F("ui.year_fmt", header.DiedTick / SimConst.TicksPerYear + 1) : "—";
                int depth = KingdomDepth(header.Uid, parents, kingdoms.Count);
                int kingdom = i;
                var button = Raw(Loc.F("ui.history_kingdom_record_fmt", name, Loc.F("ui.year_fmt", founded), ended), () => OpenKingdom(kingdom));
                button.AddToClassList("pg-history-entry");
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = Mathf.Min(240, depth * 14);
                _entries.Add(button);
                shown++;
            }
            if (shown == 0) Line(Loc.T("ui.history_no_kingdoms"), _entries);
            if (_kingdomMore != null) _kingdomMore.style.display = hasMore ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static int KingdomDepth(long uid, Dictionary<long, long> parents, int kingdomCount)
        {
            int depth = 0;
            long current = uid;
            int maxDepth = Mathf.Min(16, kingdomCount);
            while (depth < maxDepth && parents.TryGetValue(current, out long parent) && parent != 0 && parent != current)
            {
                depth++;
                current = parent;
            }
            return depth;
        }

        void OpenKingdom(int kingdom)
        {
            var sim = Host.Sim;
            if (sim.Meta.KingdomAlive(kingdom))
            {
                int capital = sim.Meta.Kingdoms.Data(kingdom).Capital;
                if (sim.Civ.CityAlive(capital)) FocusHistory(_ui, sim.Civ.Cities.Items[capital].Center, Names.Kingdom(sim, kingdom));
            }
            _ui.OpenMeta(MetaKind.Kingdom, kingdom);
        }

        static string BiographyName(SimWorld sim, BiographyTrack track, Dictionary<long, int> liveByUid)
        {
            if (track.NameId >= 0) return sim.Meta.Names.Get(track.NameId);
            return liveByUid.TryGetValue(track.Uid, out int unit) ? Names.Unit(sim, unit) : "#" + track.Uid;
        }

        static int FindLiveUnit(SimWorld sim, long uid)
        {
            var units = sim.Units.Store;
            for (int i = 0; i < units.Alive.Length; i++)
            {
                int unit = units.Alive[i];
                if (units.Uid[unit] == uid) return unit;
            }
            return -1;
        }

        static void FocusHistory(UiContext ui, float2 position, string label)
        {
            ui.GoTo(position);
            if (label.Length > 52) label = label.Substring(0, 49) + "…";
            ui.ShowHistoryMarker?.Invoke(position, label);
        }

        public static bool HasHistoryFocus(SimWorld sim, HistoryEntry entry)
        {
            var kind = (HistoryKind)entry.Template;
            if (kind == HistoryKind.CityFounded || kind == HistoryKind.CityRuined || kind == HistoryKind.CityCaptured
                || kind == HistoryKind.Rebellion || kind == HistoryKind.CityPillaged || kind == HistoryKind.Famine
                || kind == HistoryKind.PopulationMigrated || kind == HistoryKind.Disaster)
            {
                if (entry.A == 0) return false;
                for (int city = 0; city < sim.Civ.CityCount; city++)
                    if (sim.Civ.Cities.Headers[city].Uid == entry.A) return true;
                return false;
            }
            if (kind == HistoryKind.KingdomFounded || kind == HistoryKind.KingdomFell || kind == HistoryKind.NewKing
                || kind == HistoryKind.InventionDiscovered || kind == HistoryKind.Vassalage || kind == HistoryKind.TributePaid)
            {
                for (int kingdom = 0; kingdom < sim.Meta.Kingdoms.Count; kingdom++)
                {
                    if (sim.Meta.Kingdoms.Headers[kingdom].Uid != entry.A || !sim.Meta.KingdomAlive(kingdom)) continue;
                    return sim.Civ.CityAlive(sim.Meta.Kingdoms.Data(kingdom).Capital);
                }
                return false;
            }
            if (kind == HistoryKind.WarDeclared || kind == HistoryKind.Peace)
            {
                long uid = kind == HistoryKind.WarDeclared ? entry.C : entry.A;
                for (int warIndex = 0; warIndex < sim.Meta.Wars.Count; warIndex++)
                {
                    if (sim.Meta.Wars.Headers[warIndex].Uid != uid) continue;
                    var war = sim.Meta.Wars.Items[warIndex];
                    if (sim.Civ.CityAlive(war.TargetCity)) return true;
                    if (sim.Meta.KingdomAlive(war.Attacker))
                        return sim.Civ.CityAlive(sim.Meta.Kingdoms.Data(war.Attacker).Capital);
                    return false;
                }
            }
            return false;
        }

        public static Action CreateHistoryLink(UiContext ui, SimWorld sim, HistoryEntry entry, string label,
            bool openDetails = true, int historyIndex = -1)
        {
            var kind = (HistoryKind)entry.Template;
            if (kind == HistoryKind.KingdomFell
                && HistoryChronicle.TryGetCauseIndex(sim.Meta, historyIndex, out int causeIndex))
            {
                var causeLink = CreateHistoryLink(ui, sim, sim.Meta.History[causeIndex], label, openDetails, causeIndex);
                if (causeLink != null) return causeLink;
            }
            if (kind == HistoryKind.CityFounded || kind == HistoryKind.CityRuined || kind == HistoryKind.CityCaptured
                || kind == HistoryKind.Rebellion || kind == HistoryKind.CityPillaged || kind == HistoryKind.Famine
                || kind == HistoryKind.PopulationMigrated || kind == HistoryKind.Disaster && entry.A != 0)
            {
                long uid = entry.A;
                return () =>
                {
                    for (int c = 0; c < sim.Civ.CityCount; c++)
                        if (sim.Civ.Cities.Headers[c].Uid == uid)
                        {
                            FocusHistory(ui, sim.Civ.Cities.Items[c].Center, label);
                            if (openDetails) ui.OpenMeta(MetaKind.City, c);
                            return;
                        }
                };
            }
            if (kind == HistoryKind.KingdomFounded || kind == HistoryKind.KingdomFell || kind == HistoryKind.NewKing
                || kind == HistoryKind.InventionDiscovered || kind == HistoryKind.Vassalage || kind == HistoryKind.TributePaid)
            {
                long uid = entry.A;
                return () =>
                {
                    for (int kingdom = 0; kingdom < sim.Meta.Kingdoms.Count; kingdom++)
                        if (sim.Meta.Kingdoms.Headers[kingdom].Uid == uid)
                        {
                            int capital = sim.Meta.KingdomAlive(kingdom) ? sim.Meta.Kingdoms.Data(kingdom).Capital : -1;
                            if (sim.Civ.CityAlive(capital)) FocusHistory(ui, sim.Civ.Cities.Items[capital].Center, label);
                            if (openDetails) ui.OpenMeta(MetaKind.Kingdom, kingdom);
                            return;
                        }
                };
            }
            if (kind == HistoryKind.WarDeclared || kind == HistoryKind.Peace)
            {
                long uid = kind == HistoryKind.WarDeclared ? entry.C : entry.A;
                return () =>
                {
                    for (int warIndex = 0; warIndex < sim.Meta.Wars.Count; warIndex++)
                        if (sim.Meta.Wars.Headers[warIndex].Uid == uid)
                        {
                            var war = sim.Meta.Wars.Items[warIndex];
                            int target = war.TargetCity;
                            if (sim.Civ.CityAlive(target)) FocusHistory(ui, sim.Civ.Cities.Items[target].Center, label);
                            else if (sim.Meta.KingdomAlive(war.Attacker))
                            {
                                int capital = sim.Meta.Kingdoms.Data(war.Attacker).Capital;
                                if (sim.Civ.CityAlive(capital)) FocusHistory(ui, sim.Civ.Cities.Items[capital].Center, label);
                            }
                            if (openDetails) ui.OpenMeta(MetaKind.War, warIndex);
                            return;
                        }
                };
            }
            return null;
        }
        public static string Describe(SimWorld sim, HistoryEntry e)
        {
            return HistoryChronicle.Describe(sim, e);
        }
        public static string Describe(SimWorld sim, HistoryEntry e, int historyIndex) => HistoryChronicle.Describe(sim, e, historyIndex);
    }

    public sealed class StatsWindow : WindowBase
    {
        readonly UiContext _ui;
        public StatsWindow(UiContext ui) : base(ui.Host, Loc.T("ui.statistics")) { _ui = ui; Rebuild(); }
        public static bool ShouldShowCivilizationCollapseWarning(int civilizationPopulation, int livingKingdoms, int fallenKingdoms)
            => civilizationPopulation == 0 && livingKingdoms == 0 && fallenKingdoms > 0;

        protected override void Build()
        {
            var sim = Host.Sim;
            var u = sim.Units.Store;
            var db = sim.Content;
            Line(Loc.F("ui.population_fmt", u.Count));
            var perSpecies = new int[db.Species.Count];
            int civilizationPopulation = 0;
            int oldest = -1, killer = -1;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                int species = u.Species[i];
                perSpecies[species]++;
                if (db.Species[species].IsCiv) civilizationPopulation++;
                if (oldest < 0 || u.BirthTick[i] < u.BirthTick[oldest]) oldest = i;
                if (killer < 0 || u.Kills[i] > u.Kills[killer]) killer = i;
            }
            Line(Loc.F("ui.population_civilization_fmt", civilizationPopulation));
            Line(Loc.F("ui.population_wildlife_fmt", u.Count - civilizationPopulation));
            int kinds = 0;
            foreach (var c in perSpecies) if (c > 0) kinds++;
            Line(Loc.F("ui.species_count_fmt", kinds));
            if (oldest >= 0) { int o = oldest; Body.Add(Raw(Loc.F("ui.oldest_unit_fmt", Names.Unit(sim, o), (int)u.AgeYears(o, sim.Clock.Tick)), () => _ui.OpenUnit(o))); }
            if (killer >= 0 && u.Kills[killer] > 0) { int k = killer; Body.Add(Raw(Loc.F("ui.top_killer_fmt", Names.Unit(sim, k), u.Kills[k]), () => _ui.OpenUnit(k))); }
            int alive = 0, dead = 0, biggest = -1, eldest = -1;
            for (int k = 0; k < sim.Meta.Kingdoms.Count; k++)
            {
                if (!sim.Meta.KingdomAlive(k)) { dead++; continue; }
                alive++;
                if (biggest < 0 || sim.Meta.Kingdoms.Headers[k].MemberCount > sim.Meta.Kingdoms.Headers[biggest].MemberCount) biggest = k;
                if (eldest < 0 || sim.Meta.Kingdoms.Headers[k].FoundedTick < sim.Meta.Kingdoms.Headers[eldest].FoundedTick) eldest = k;
            }
            Line(Loc.F("ui.kingdoms_fmt", alive, dead));
            if (ShouldShowCivilizationCollapseWarning(civilizationPopulation, alive, dead))
            {
                var warning = new Label(Loc.T("ui.civilization_extinct_warning"));
                warning.AddToClassList("pg-stat-warning");
                Body.Add(warning);
            }
            Line(Loc.F("ui.wars_total_fmt", sim.Meta.Wars.Count));
            if (biggest >= 0) { int b = biggest; Body.Add(Raw(Loc.F("ui.biggest_kingdom_fmt", Names.Kingdom(sim, b)), () => _ui.OpenMeta(MetaKind.Kingdom, b))); }
            if (eldest >= 0) { int e = eldest; Body.Add(Raw(Loc.F("ui.oldest_kingdom_fmt", Names.Kingdom(sim, e)), () => _ui.OpenMeta(MetaKind.Kingdom, e))); }
            Heading("ui.species");
            var row = Row();
            for (int s = 0; s < perSpecies.Length; s++)
                if (perSpecies[s] > 0) { var l = new Label(Loc.Name(db.Species[s]) + " " + perSpecies[s]); l.AddToClassList("pg-chip"); row.Add(l); }
            Heading("ui.cities");
            if (sim.Civ == null) return;
            for (int c = 0; c < sim.Civ.CityCount; c++)
            {
                if (!sim.Civ.CityAlive(c)) continue;
                int id = c; Body.Add(Raw(Names.City(sim, c) + " · " + sim.Civ.Cities.Headers[c].MemberCount, () => _ui.OpenMeta(MetaKind.City, id)));
            }
        }
    }

    public sealed class SettingsWindow : WindowBase
    {
        public static readonly string[] NotificationCategories = { "war", "city", "kingdom", "king", "rebellion", "famine", "religion", "item", "discovery", "achievement", "favorite", "misc" };
        sealed class SearchEntry { public VisualElement Element; public string Label; }
        sealed class SettingsSection { public Foldout Foldout; public readonly List<SearchEntry> Entries = new List<SearchEntry>(); }
        readonly UiContext _ui;
        readonly List<SettingsSection> _sections = new List<SettingsSection>();
        Label _govStatus;
        Label _noSearchResults;
        string _searchQuery = string.Empty;
        bool _mobileDetailed;
        public SettingsWindow(UiContext ui) : base(ui.Host, Loc.T("ui.settings")) { _ui = ui; Rebuild(); }
        protected override void Refresh()
        {
            if (_govStatus == null || PG.Governor.GovernorRunner.Active == null) return;
            var gov = PG.Governor.GovernorRunner.Active;
            _govStatus.text = Loc.T("ui.gov.status." + gov.Status.ToString().ToLowerInvariant()) +
                (string.IsNullOrEmpty(gov.StatusMessage) ? "" : " — " + gov.StatusMessage);
        }
        protected override void Build()
        {
            SetTitle(Loc.T("ui.settings"));
            _sections.Clear();
            var search = new TextField(Loc.T("ui.settings_search"));
            search.AddToClassList("pg-settings-search");
            search.SetValueWithoutNotify(_searchQuery);
            Body.Add(search);
            _noSearchResults = new Label(Loc.T("ui.settings_no_results"));
            _noSearchResults.AddToClassList("pg-dim");
            _noSearchResults.style.display = DisplayStyle.None;
            Body.Add(_noSearchResults);
            search.RegisterValueChangedCallback(e =>
            {
                _searchQuery = e.newValue;
                if (PlatformInfo.IsTouchDevice && !_mobileDetailed && !string.IsNullOrWhiteSpace(_searchQuery))
                {
                    _mobileDetailed = true;
                    Rebuild();
                }
                else FilterSettings(_searchQuery);
            });
            var resetRow = Row();
            resetRow.Add(Small("ui.settings_reset", () =>
            {
                _ui.Profile.ResetSettings();
                Loc.Language = _ui.Profile.Language;
                _ui.Camera.Settings.EdgePan = _ui.Profile.EdgePan;
                Changed();
                _ui.SettingsChanged?.Invoke();
                Rebuild();
                _ui.Toast?.Invoke(Loc.T("ui.settings_reset_done"));
            }));

            if (PlatformInfo.IsTouchDevice && !_mobileDetailed)
            {
                BuildMobileQuickSettings();
                FilterSettings(search.value);
                return;
            }
            if (PlatformInfo.IsTouchDevice)
                Row().Add(Small("ui.settings_compact", () => { _mobileDetailed = false; _searchQuery = string.Empty; Rebuild(); }));

            var general = Section("ui.settings_general");
            var language = new PopupField<string>(Loc.T("ui.language"), new System.Collections.Generic.List<string> { "tr", "en" }, _ui.Profile.Language == "en" ? 1 : 0);
            AddSetting(general, "ui.language", language);
            language.RegisterValueChangedCallback(e => { _ui.Profile.Language = e.newValue; Loc.Language = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); Rebuild(); });
            var scale = new Slider(Loc.T("ui.ui_scale"), 0.75f, 2f) { value = _ui.Profile.UiScale, showInputField = true };
            AddSetting(general, "ui.ui_scale", scale);
            scale.RegisterValueChangedCallback(e => { _ui.Profile.UiScale = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
            var edge = new Toggle(Loc.T("ui.edge_pan")) { value = _ui.Profile.EdgePan };
            AddSetting(general, "ui.edge_pan", edge);
            edge.RegisterValueChangedCallback(e => { _ui.Profile.EdgePan = e.newValue; _ui.Camera.Settings.EdgePan = e.newValue; Changed(); });

            var accessibility = Section("ui.accessibility");
            var colorBlind = new Toggle(Loc.T("ui.colorblind_palette")) { value = _ui.Profile.ColorBlindPalette };
            AddSetting(accessibility, "ui.colorblind_palette", colorBlind);
            colorBlind.RegisterValueChangedCallback(e => { _ui.Profile.ColorBlindPalette = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
            var highContrast = new Toggle(Loc.T("ui.high_contrast")) { value = _ui.Profile.HighContrast };
            AddSetting(accessibility, "ui.high_contrast", highContrast);
            highContrast.RegisterValueChangedCallback(e => { _ui.Profile.HighContrast = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
            var screenShake = new Toggle(Loc.T("ui.screen_shake")) { value = !_ui.Profile.DisableScreenShake };
            AddSetting(accessibility, "ui.screen_shake", screenShake);
            screenShake.RegisterValueChangedCallback(e => { _ui.Profile.DisableScreenShake = !e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });

            BuildDiagnostics(Section("ui.diagnostics"));

            var performance = Section("ui.performance");
            var presetKeys = new[] { "low", "balanced", "high" };
            var presetLabels = new System.Collections.Generic.List<string>
            {
                Loc.T("ui.performance.low"), Loc.T("ui.performance.balanced"), Loc.T("ui.performance.high"),
            };
            int presetIndex = Array.IndexOf(presetKeys, _ui.Profile.PerformancePreset);
            if (presetIndex < 0) presetIndex = 1;
            var preset = new PopupField<string>(Loc.T("ui.performance_preset"), presetLabels, presetIndex);
            AddSetting(performance, "ui.performance_preset", preset);
            AddSetting(performance, "ui.performance_note", new Label(Loc.T("ui.performance_note")));
            var fpsChoices = new System.Collections.Generic.List<string> { "30", "60", "120" };
            var fps = new PopupField<string>(Loc.T("ui.max_fps"), fpsChoices, _ui.Profile.TargetFps <= 30 ? 0 : _ui.Profile.TargetFps <= 60 ? 1 : 2);
            AddSetting(performance, "ui.max_fps", fps);
            preset.RegisterValueChangedCallback(e =>
            {
                int index = Mathf.Clamp(preset.index, 0, presetKeys.Length - 1);
                _ui.Profile.PerformancePreset = presetKeys[index];
                _ui.Profile.TargetFps = index == 0 ? 30 : index == 1 ? 60 : 120;
                fps.SetValueWithoutNotify(fpsChoices[index]);
                Changed();
                _ui.SettingsChanged?.Invoke();
            });
            fps.RegisterValueChangedCallback(e => { _ui.Profile.TargetFps = int.Parse(e.newValue); Changed(); _ui.SettingsChanged?.Invoke(); });
            var dayNight = new Toggle(Loc.T("ui.day_night")) { value = _ui.Profile.DayNight }; // Bölüm 11 G4
            AddSetting(performance, "ui.day_night", dayNight);
            dayNight.RegisterValueChangedCallback(e => { _ui.Profile.DayNight = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
            var audio = Section("ui.audio");
            for (int v = 0; v < _ui.Profile.Volumes.Length; v++)
            {
                int index = v;
                var slider = new Slider(Loc.T("ui.volume." + v), 0f, 1f) { value = _ui.Profile.Volumes[v] };
                slider.RegisterValueChangedCallback(e => { _ui.Profile.Volumes[index] = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
                AddSetting(audio, "ui.volume." + v, slider);
            }

            var history = Section("ui.history_notifications");
            var importance = new SliderInt(Loc.T("ui.history_importance_threshold"), 20, 100)
            {
                value = HistoryNotificationPolicy.NormalizeThreshold(_ui.Profile.HistoryNotificationThreshold),
                showInputField = true,
            };
            importance.RegisterValueChangedCallback(e =>
            {
                _ui.Profile.HistoryNotificationThreshold = HistoryNotificationPolicy.NormalizeThreshold(e.newValue);
                Changed();
            });
            AddSetting(history, "ui.history_importance_threshold", importance);
            var documentary = new Toggle(Loc.T("ui.history_documentary_camera")) { value = _ui.Profile.DocumentaryCameraMode };
            documentary.RegisterValueChangedCallback(e => { _ui.Profile.DocumentaryCameraMode = e.newValue; Changed(); });
            AddSetting(history, "ui.history_documentary_camera", documentary);

            var notifications = Section("ui.notifications");
            foreach (var cat in NotificationCategories)
            {
                string c = cat;
                var t = new Toggle(Loc.T("ui.notify." + c)) { value = !_ui.Profile.MutedNotifications.Contains(c) };
                t.RegisterValueChangedCallback(e => { if (e.newValue) _ui.Profile.MutedNotifications.Remove(c); else if (!_ui.Profile.MutedNotifications.Contains(c)) _ui.Profile.MutedNotifications.Add(c); Changed(); });
                AddSetting(notifications, "ui.notify." + c, t);
            }
            AddSetting(notifications, "ui.tutorial_restart", Small("ui.tutorial_restart", () => { _ui.Profile.TutorialDone = false; Changed(); _ui.Toast(Loc.T("ui.tutorial_restart_note")); }));

            BuildGovernor(Section("ui.gov.title", true));
            FilterSettings(search.value);
        }

        void BuildMobileQuickSettings()
        {
            var quick = Section("ui.settings_mobile_quick");
            quick.Foldout.value = true;

            var language = new PopupField<string>(Loc.T("ui.language"), new List<string> { "tr", "en" }, _ui.Profile.Language == "en" ? 1 : 0);
            AddSetting(quick, "ui.language", language);
            language.RegisterValueChangedCallback(e => { _ui.Profile.Language = e.newValue; Loc.Language = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); Rebuild(); });

            var scale = new Slider(Loc.T("ui.ui_scale"), 0.75f, 2f) { value = _ui.Profile.UiScale, showInputField = true };
            AddSetting(quick, "ui.ui_scale", scale);
            scale.RegisterValueChangedCallback(e => { _ui.Profile.UiScale = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });

            string[] presetKeys = { "low", "balanced", "high" };
            var labels = new List<string> { Loc.T("ui.performance.low"), Loc.T("ui.performance.balanced"), Loc.T("ui.performance.high") };
            int presetIndex = Array.IndexOf(presetKeys, _ui.Profile.PerformancePreset);
            if (presetIndex < 0) presetIndex = 1;
            var preset = new PopupField<string>(Loc.T("ui.performance_preset"), labels, presetIndex);
            AddSetting(quick, "ui.performance_preset", preset);
            preset.RegisterValueChangedCallback(e =>
            {
                int index = Mathf.Clamp(preset.index, 0, presetKeys.Length - 1);
                _ui.Profile.PerformancePreset = presetKeys[index];
                _ui.Profile.TargetFps = index == 0 ? 30 : index == 1 ? 60 : 120;
                Changed();
                _ui.SettingsChanged?.Invoke();
            });

            var contrast = new Toggle(Loc.T("ui.high_contrast")) { value = _ui.Profile.HighContrast };
            AddSetting(quick, "ui.high_contrast", contrast);
            contrast.RegisterValueChangedCallback(e => { _ui.Profile.HighContrast = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
            var edge = new Toggle(Loc.T("ui.edge_pan")) { value = _ui.Profile.EdgePan };
            AddSetting(quick, "ui.edge_pan", edge);
            edge.RegisterValueChangedCallback(e => { _ui.Profile.EdgePan = e.newValue; _ui.Camera.Settings.EdgePan = e.newValue; Changed(); });
            var night = new Toggle(Loc.T("ui.day_night")) { value = _ui.Profile.DayNight };
            AddSetting(quick, "ui.day_night", night);
            night.RegisterValueChangedCallback(e => { _ui.Profile.DayNight = e.newValue; Changed(); _ui.SettingsChanged?.Invoke(); });
            BuildDiagnostics(Section("ui.diagnostics"));
            Row().Add(Small("ui.settings_all", () => { _mobileDetailed = true; Rebuild(); }));
        }

        void BuildDiagnostics(SettingsSection section)
        {
            if (section == null) return;
            AddSetting(section, "ui.diagnostics_note", new Label(Loc.T("ui.diagnostics_note")));
            AddSetting(section, "ui.diagnostics_copy", Small("ui.diagnostics_copy", () =>
            {
                try
                {
                    WorldShareCode.CopyText(RuntimeDiagnostics.BuildBundle(_ui.Sim, _ui.Profile));
                    _ui.Toast?.Invoke(Loc.T("ui.diagnostics_copied"));
                }
                catch (Exception)
                {
                    _ui.Toast?.Invoke(Loc.T("ui.diagnostics_copy_failed"));
                }
            }, "export"));
        }

        SettingsSection Section(string titleKey, bool skip = false)
        {
            if (skip && PG.Governor.GovernorRunner.Active == null) return null;
            var section = new SettingsSection
            {
                Foldout = new Foldout { text = Loc.T(titleKey), value = !PlatformInfo.IsTouchDevice },
            };
            section.Foldout.AddToClassList("pg-settings-section");
            Body.Add(section.Foldout);
            _sections.Add(section);
            return section;
        }

        static bool Contains(string value, string query) => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        void AddSetting(SettingsSection section, string labelKey, VisualElement control)
        {
            section.Foldout.Add(control);
            section.Entries.Add(new SearchEntry { Element = control, Label = Loc.T(labelKey) + " " + labelKey });
        }

        void FilterSettings(string query)
        {
            query = (query ?? string.Empty).Trim();
            bool anySection = false;
            foreach (var section in _sections)
            {
                bool titleMatches = query.Length == 0 || Contains(section.Foldout.text, query);
                bool any = false;
                foreach (var entry in section.Entries)
                {
                    bool show = query.Length == 0 || titleMatches || Contains(entry.Label, query);
                    entry.Element.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    any |= show;
                }
                section.Foldout.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
                anySection |= any;
                if (query.Length > 0 && any) section.Foldout.value = true;
            }
            if (_noSearchResults != null)
                _noSearchResults.style.display = query.Length > 0 && !anySection ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Kingdom governors (LLM brains): on/off, local vs cloud endpoint, model, scope.
        void BuildGovernor(SettingsSection section)
        {
            var gov = PG.Governor.GovernorRunner.Active;
            if (gov == null) return;
            var s = gov.Settings;
            var enabled = new Toggle(Loc.T("ui.gov.enabled")) { value = s.Enabled };
            enabled.RegisterValueChangedCallback(e => { s.Enabled = e.newValue; s.Save(); if (e.newValue) gov.ScheduleYear(); });
            AddSetting(section, "ui.gov.enabled", enabled);
            var profile = new PopupField<string>(Loc.T("ui.gov.profile"),
                new System.Collections.Generic.List<string> { Loc.T("ui.gov.profile.cloud"), Loc.T("ui.gov.profile.local") }, s.UseLocal ? 1 : 0);
            AddSetting(section, "ui.gov.profile", profile);
            bool localDraft = s.UseLocal;
            var baseUrl = new TextField(Loc.T("ui.gov.base_url")) { value = s.BaseUrl };
            AddSetting(section, "ui.gov.base_url", baseUrl);
            var model = new TextField(Loc.T("ui.gov.model")) { value = s.ModelName };
            AddSetting(section, "ui.gov.model", model);
            bool computerKeyAvailable = !string.IsNullOrEmpty(PG.Governor.GovernorSettings.LoadApiKey());
            bool externalDefault = localDraft ? s.LocalExternalKey : s.CloudExternalKey;
            if (Application.platform == RuntimePlatform.WebGLPlayer && !computerKeyAvailable) externalDefault = false;
            var externalKey = new Toggle(Loc.T("ui.gov.external_key")) { value = externalDefault };
            AddSetting(section, "ui.gov.external_key", externalKey);
            var apiKey = new TextField(Loc.T("ui.gov.api_key")) { isPasswordField = true };
            apiKey.SetEnabled(!externalKey.value);
            apiKey.value = externalKey.value ? "" : (localDraft ? s.LocalApiKey : s.CloudApiKey);
            AddSetting(section, "ui.gov.api_key", apiKey);
            string cloudUrlDraft = s.CloudBaseUrl, localUrlDraft = s.LocalBaseUrl;
            string cloudModelDraft = s.CloudModel, localModelDraft = s.LocalModel;
            string cloudKeyDraft = localDraft ? "" : apiKey.value, localKeyDraft = localDraft ? apiKey.value : "";
            bool cloudExternalDraft = localDraft ? s.CloudExternalKey : externalDefault;
            bool localExternalDraft = localDraft ? externalDefault : s.LocalExternalKey;
            void StoreProfileDraft()
            {
                if (localDraft) { localUrlDraft = baseUrl.value; localModelDraft = model.value; localKeyDraft = apiKey.value; localExternalDraft = externalKey.value; }
                else { cloudUrlDraft = baseUrl.value; cloudModelDraft = model.value; cloudKeyDraft = apiKey.value; cloudExternalDraft = externalKey.value; }
            }
            profile.RegisterValueChangedCallback(e =>
            {
                StoreProfileDraft();
                localDraft = profile.index == 1;
                baseUrl.SetValueWithoutNotify(localDraft ? localUrlDraft : cloudUrlDraft);
                model.SetValueWithoutNotify(localDraft ? localModelDraft : cloudModelDraft);
                externalKey.SetValueWithoutNotify(localDraft ? localExternalDraft : cloudExternalDraft);
                apiKey.SetValueWithoutNotify(externalKey.value ? "" : (localDraft ? localKeyDraft : cloudKeyDraft));
                apiKey.SetEnabled(!externalKey.value);
            });
            baseUrl.RegisterValueChangedCallback(_ => StoreProfileDraft());
            model.RegisterValueChangedCallback(_ => StoreProfileDraft());
            apiKey.RegisterValueChangedCallback(_ => StoreProfileDraft());
            externalKey.RegisterValueChangedCallback(e =>
            {
                apiKey.SetEnabled(!e.newValue);
                if (e.newValue) apiKey.SetValueWithoutNotify("");
                StoreProfileDraft();
            });
            AddSetting(section, "ui.gov.save_connection", Small("ui.gov.save_connection", () =>
            {
                if (gov.InFlight != 0 || gov.Status == PG.Governor.GovernorConnectionStatus.Testing)
                { _govStatus.text = "Bağlantı testi veya AI isteği sürerken ayarlar değiştirilemez."; return; }
                if (!s.ApplyConnection(localDraft, baseUrl.value, model.value, apiKey.value, externalKey.value))
                {
                    _govStatus.text = Loc.T("ui.gov.config_error");
                    return;
                }
                s.Save();
                _govStatus.text = Loc.T("ui.gov.config_applied");
            }));
            var scope = new PopupField<string>(Loc.T("ui.gov.scope"),
                new System.Collections.Generic.List<string>
                {
                    Loc.T("ui.gov.scope.all"), Loc.T("ui.gov.scope.controlled"), Loc.T("ui.gov.scope.topn"),
                }, (int)s.Scope);
            scope.RegisterValueChangedCallback(e => { s.Scope = (PG.Governor.GovernorSettings.ScopeKind)scope.index; s.Save(); });
            AddSetting(section, "ui.gov.scope", scope);
            AddSetting(section, "ui.gov.test_connection", Small("ui.gov.test_connection", () => gov.TestConnection()));
            _govStatus = new Label(); _govStatus.AddToClassList("pg-label"); AddSetting(section, "ui.gov.title", _govStatus);
            AddSetting(section, "ui.gov.limits_fmt", new Label(Loc.F("ui.gov.limits_fmt", s.CooldownSeconds, s.MaxConcurrent, s.MaxCallsPerHour)));
            string hintKey = Application.platform == RuntimePlatform.WebGLPlayer ? "ui.gov.web_hint" : "ui.gov.hint";
            AddSetting(section, hintKey, new Label(Loc.T(hintKey)));
        }
        void Changed() { _ui.Profile.Dirty = true; _ui.Profile.Save(); }
    }
}
