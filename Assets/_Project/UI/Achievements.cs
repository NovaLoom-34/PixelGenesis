using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Sim;

namespace PG.UI
{
    // Bölüm 8.9 AchievementConditionRegistry: achievements.json ids mapped to checks. Event checks run when the event
    // arrives, counter checks once a year. Ids without a checker simply stay locked (DECISIONS #79).
    public sealed class AchievementTracker
    {
        public const int CheckYears = 1;

        readonly UiContext _ui;
        readonly Action<string, string> _earned; // (id, display name)
        readonly Dictionary<string, Func<SimWorld, bool>> _yearly = new Dictionary<string, Func<SimWorld, bool>>();
        SimWorld _sim;
        int _lastYear = -1, _lastWarYear, _disasterYear = -1;
        readonly HashSet<int> _disastersThisYear = new HashSet<int>();
        readonly HashSet<int> _erasSeen = new HashSet<int>();
        int _eraIce = -1, _eraScorch = -1, _qLegendary = -1, _trHero = -1, _mine = -1;
        int _statueGod = -1, _colossus = -1, _plotAssassin = -1, _plotProphet = -1, _culAstronomers = -1, _culPacifist = -1;
        long _initialLand = -1;
        bool _genesisEligible;
        long _explosionTick = -1;
        int _explosionBuildings;
        int _zombieMajorityYear = -1;
        readonly Dictionary<int, int> _rebelCapital = new Dictionary<int, int>();      // rebel kingdom -> its capital city
        readonly HashSet<int> _rebelLostCapital = new HashSet<int>();                  // rebel kingdoms that lost that capital once
        readonly Dictionary<int, int> _clanKings = new Dictionary<int, int>();         // clan -> kings crowned
        readonly Dictionary<int, long> _automatonHit = new Dictionary<int, long>();    // city -> last Life Game damage tick
        readonly HashSet<int> _zombieMajorityCities = new HashSet<int>();

        public AchievementTracker(UiContext ui, Action<string, string> earned)
        {
            _ui = ui;
            _earned = earned;
            _yearly["ach.first_life"] = s => s.Units.Store.Count > 0;
            _yearly["ach.population_1k"] = s => s.Units.Store.Count >= 1000;
            _yearly["ach.population_10k"] = s => s.Units.Store.Count >= 10000;
            _yearly["ach.empire"] = s => MaxCitiesPerKingdom(s) >= 25;
            _yearly["ach.ancient_kingdom"] = s => OldestKingdomYears(s) >= 500;
            _yearly["ach.world_war"] = s => AliveCount(s.Meta.Wars) >= 5;
            _yearly["ach.peace_age"] = s => s.Clock.Year - _lastWarYear >= 100 && s.Meta.Kingdoms.Count > 1;
            _yearly["ach.plague_lord"] = s => CountUnits(s, (u, i) => u.Disease[i] != 0) >= 1000;
            _yearly["ach.zombie_world"] = s => CountUnits(s, (u, i) => s.Content.Species[u.Species[i]].IsUndeadCategory) >= 500;
            _yearly["ach.babel"] = s => AliveCount(s.Meta.Languages) >= 10;
            _yearly["ach.librarian"] = s => s.Meta.Books.IsCreated && s.Meta.Books.Length >= 500;
            _yearly["ach.slayer"] = s => CountUnits(s, (u, i) => u.Kills[i] >= 100) > 0;
            _yearly["ach.old_one"] = s => CountUnits(s, (u, i) => u.AgeYears(i, s.Clock.Tick) >= 500) > 0;
            _yearly["ach.mixed_city"] = s => MaxSpeciesInCity(s) >= 4;
            _yearly["ach.dragon_age"] = s => HasSpecies(s, "sp.dragon");
            _yearly["ach.fairy_tale"] = s => HasSpecies(s, "sp.fairy") && HasSpecies(s, "sp.dragon") && HasSpecies(s, "sp.walking_tree");
            _yearly["ach.undead_king"] = s => UndeadKing(s);
            _yearly["ach.collector"] = s => DiscoveredTraits(s) >= 100;
            _yearly["ach.completionist"] = s => DiscoveredTraits(s) >= s.Content.UnitTraits.Count;
            _yearly["ach.mad_scientist"] = s => _ui.Host.Edits.SubspeciesEdits >= 10;
            _yearly["ach.puppet_master"] = s => _ui.Profile.Counter("puppet_years") >= 50;
            _yearly["ach.kingmaker"] = s => _ui.Profile.Counter("kingdoms_founded") >= 5;
            _yearly["ach.possessed"] = s => _ui.Profile.Counter("possession_kills") >= 50;
            _yearly["ach.uplift_all"] = s => _ui.Profile.Counter("monolith_awakenings") >= 5;
            _yearly["ach.all_eras"] = s => _erasSeen.Count >= s.Content.Eras.Count;
            _yearly["ach.fire_world"] = s => BurningTiles(s) >= 5000;
            _yearly["ach.gene_splicer"] = s => KnownSynergies(s) >= 5;
            _yearly["ach.biome_master"] = AllNormalBiomesPresent;
            _yearly["ach.frozen_world"] = s => FrozenWaterFraction(s) >= 0.8f;
            _yearly["ach.world_eater"] = s => _initialLand > 0 && LandRemaining(s) <= _initialLand * 0.10;
            _yearly["ach.genesis"] = s => _genesisEligible && s.Units.Store.Count >= 1000;
            _yearly["ach.star_gazer"] = s => KingdomsWithCultureTrait(s, _culAstronomers) >= 3;
            _yearly["ach.pacifist"] = s => AnyCultureTraitAged(s, _culPacifist, 200);
            _yearly["ach.god_named"] = s => CitiesWithBuilding(s, _statueGod) >= 10;
            _yearly["ach.colossus_war"] = s => _colossus >= 0 && CountUnits(s, (u, i) => u.Species[i] == _colossus && u.Kills[i] >= 1000) > 0;
            _yearly["ach.cure_found"] = s => _ui.Profile.Counter("units_cured") >= 1000;
        }

        public bool Enabled => _sim != null && !_sim.Nature.Laws.IsOn("law.forbidden_codex");

        public void Attach(SimWorld sim)
        {
            _sim = sim;
            _lastYear = -1;
            _lastWarYear = sim.Clock.Year;
            _disastersThisYear.Clear();
            _erasSeen.Clear();
            _erasSeen.Add(sim.Nature.Era.CurrentEra);
            var db = sim.Content;
            _eraIce = db.Eras.IdOrDefault("era.ice");
            _eraScorch = db.Eras.IdOrDefault("era.scorch");
            _qLegendary = db.ItemQualities.IdOrDefault("q.legendary");
            _trHero = db.UnitTraits.IdOrDefault("tr.hero");
            _mine = db.Buildings.IdOrDefault("bld.mine");
            _statueGod = db.Buildings.IdOrDefault("bld.statue_god");
            _colossus = db.Species.IdOrDefault("sp.colossus");
            _plotAssassin = db.Plots.IdOrDefault("plot.assassination");
            _plotProphet = db.Plots.IdOrDefault("plot.new_religion");
            _culAstronomers = db.CultureTraits.IdOrDefault("cul.astronomers");
            _culPacifist = db.CultureTraits.IdOrDefault("cul.pacifist_creed");
            _initialLand = LandRemaining(sim);
            _genesisEligible = _initialLand == 0;
            _explosionTick = -1;
            _explosionBuildings = 0;
            _zombieMajorityYear = -1;
            _rebelCapital.Clear();
            _rebelLostCapital.Clear();
            _clanKings.Clear();
            _automatonHit.Clear();
            _zombieMajorityCities.Clear();
            var ev = sim.Events;
            ev.Subscribe<CityFoundedEvent>(_ => Earn("ach.first_civ"));
            ev.Subscribe<ColonyShipEvent>(_ => Earn("ach.sailor"));
            ev.Subscribe<MonolithEvolvedEvent>(_ => { Earn("ach.evolution"); Count("monolith_awakenings"); });
            ev.Subscribe<KingdomFoundedEvent>(_ => Count("kingdoms_founded"));
            ev.Subscribe<WarDeclaredEvent>(_ => _lastWarYear = _sim.Clock.Year);
            ev.Subscribe<BuildingCompletedEvent>(e => { if (e.Def == _mine) Earn("ach.first_mine"); });
            ev.Subscribe<ItemCraftedEvent>(e => { if (e.Quality == _qLegendary) Earn("ach.master_smith"); });
            ev.Subscribe<UnitTraitGainedEvent>(e => { if (e.Trait == _trHero) Earn("ach.hero_born"); });
            ev.Subscribe<UnitDiedEvent>(e => { if (e.KillerUid != 0 && e.KillerUid == _ui.PossessedUid) Count("possession_kills"); });
            ev.Subscribe<EraChangedEvent>(e =>
            {
                _erasSeen.Add(e.NewEra);
                if (e.OldEra == _eraIce && e.NewEra == _eraScorch) Earn("ach.ice_to_fire");
            });
            ev.Subscribe<DisasterRequestEvent>(e =>
            {
                if (_disasterYear != _sim.Clock.Year) { _disasterYear = _sim.Clock.Year; _disastersThisYear.Clear(); }
                _disastersThisYear.Add(e.Disaster);
                if (_disastersThisYear.Count >= 3) Earn("ach.apocalypse");
            });
            // plot.assassination always kills a reigning king on success; plot.new_religion always founds one (Sim/Meta/PlotSystem.cs)
            ev.Subscribe<PlotFinishedEvent>(e =>
            {
                if (!e.Success) return;
                int def = _sim.Meta.Plots.Data(e.Plot).Def;
                if (def == _plotAssassin) Earn("ach.assassin");
                else if (def == _plotProphet) Earn("ach.prophet");
            });
            // a rebel kingdom's capital is the city it rebelled with; earning this means it lost that city once and took it back
            ev.Subscribe<RebellionEvent>(e => _rebelCapital[e.To] = e.City);
            ev.Subscribe<CityCapturedEvent>(e =>
            {
                if (_rebelCapital.TryGetValue(e.From, out int lostCap) && lostCap == e.City) _rebelLostCapital.Add(e.From);
                if (_rebelLostCapital.Contains(e.To) && _rebelCapital.TryGetValue(e.To, out int ownCap) && ownCap == e.City) Earn("ach.rebel_king");
            });
            ev.Subscribe<NewKingEvent>(e =>
            {
                var u = _sim.Units.Store;
                if (e.Kingdom < 0 || e.Kingdom >= _sim.Meta.Kingdoms.Count) return;
                var king = _sim.Meta.Kingdoms.Items[e.Kingdom].King;
                if (!u.IsAlive(king) || u.Clan[king.Index] < 0) return;
                int clan = u.Clan[king.Index];
                _clanKings.TryGetValue(clan, out int n);
                _clanKings[clan] = ++n;
                if (n >= 10) Earn("ach.dynasty");
            });
            // ach.destroyer: 100 buildings falling within the same blast (a big power, not cumulative use of one)
            ev.Subscribe<ExplosionEvent>(e =>
            {
                if (e.Radius < 15f) return;
                _explosionTick = _sim.Clock.Tick;
                _explosionBuildings = 0;
            });
            ev.Subscribe<BuildingDestroyedEvent>(_ =>
            {
                if (_explosionTick < 0 || _sim.Clock.Tick - _explosionTick > 2) return;
                if (++_explosionBuildings >= 100) Earn("ach.destroyer");
            });
            ev.Subscribe<DiseaseCuredEvent>(e => { if (e.WasTransforming) Count("units_cured"); });
            ev.Subscribe<AutomatonDamageEvent>(e => _automatonHit[e.City] = _sim.Clock.Tick);
            ev.Subscribe<CityRuinedEvent>(e =>
            {
                if (_automatonHit.TryGetValue(e.City, out long hitTick) && _sim.Clock.Tick - hitTick <= 20 * SimConst.TicksPerMonth) Earn("ach.cellular");
            });
        }

        public void Update()
        {
            if (!Enabled) return;
            int year = _sim.Clock.Year;
            if (year == _lastYear) return;
            bool first = _lastYear < 0;
            _lastYear = year;
            if (!first) for (int k = 0; k < _sim.Meta.Kingdoms.Count; k++)
                if (_sim.Meta.Kingdoms.IsAlive(k) && (_sim.Meta.Kingdoms.Headers[k].Flags & MetaFlags.PlayerControlled) != 0) { Count("puppet_years"); break; }
            foreach (var kv in _yearly)
                if (!_ui.Profile.HasAchievement(kv.Key) && kv.Value(_sim)) Earn(kv.Key);
            if (!_ui.Profile.HasAchievement("ach.last_city")) CheckLastCity(_sim);
            _ui.Profile.Save();
        }

        // Bölüm 8.9 ach.last_city: once zombies outnumber the living 10:1, remember which cities stood; if any of them
        // is still alive 20 years later, the world held. Resets if the ratio breaks before that.
        void CheckLastCity(SimWorld s)
        {
            if (s.Civ == null) return;
            int zombies = CountUnits(s, (u, i) => s.Content.Species[u.Species[i]].IsUndeadCategory);
            int living = s.Units.Store.Count - zombies;
            if (living <= 0 || zombies < living * 10) { _zombieMajorityYear = -1; return; }
            if (_zombieMajorityYear < 0)
            {
                _zombieMajorityYear = s.Clock.Year;
                _zombieMajorityCities.Clear();
                for (int c = 0; c < s.Civ.CityCount; c++) if (s.Civ.CityAlive(c)) _zombieMajorityCities.Add(c);
                return;
            }
            if (s.Clock.Year - _zombieMajorityYear < 20) return;
            foreach (int c in _zombieMajorityCities)
                if (s.Civ.CityAlive(c)) { Earn("ach.last_city"); return; }
        }

        void Count(string key) { if (Enabled) _ui.Profile.AddCounter(key, 1); }

        void Earn(string id)
        {
            if (!Enabled || !_ui.Profile.Earn(id)) return;
            _ui.Profile.Save();
            _earned(id, _sim.Content.Achievements.TryGet(id, out var def) ? Loc.Name(def) : id);
        }

        static int AliveCount<T>(MetaStore<T> store) where T : unmanaged
        {
            int n = 0;
            for (int i = 0; i < store.Count; i++) if (store.IsAlive(i)) n++;
            return n;
        }

        static int CountUnits(SimWorld s, Func<UnitStore, int, bool> match)
        {
            var u = s.Units.Store;
            int n = 0;
            for (int k = 0; k < u.Alive.Length; k++) if (match(u, u.Alive[k])) n++;
            return n;
        }

        static bool HasSpecies(SimWorld s, string id)
        {
            if (!s.Content.Species.TryGet(id, out var def)) return false;
            return CountUnits(s, (u, i) => u.Species[i] == def.Index) > 0;
        }

        static int MaxCitiesPerKingdom(SimWorld s)
        {
            var civ = s.Civ;
            if (civ == null) return 0;
            var per = new Dictionary<int, int>();
            int best = 0;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                int k = civ.KingdomOf(c);
                if (k < 0) continue;
                per.TryGetValue(k, out int n);
                per[k] = ++n;
                if (n > best) best = n;
            }
            return best;
        }

        static int OldestKingdomYears(SimWorld s)
        {
            long best = 0;
            for (int k = 0; k < s.Meta.Kingdoms.Count; k++)
                if (s.Meta.Kingdoms.IsAlive(k)) best = Math.Max(best, s.Clock.Tick - s.Meta.Kingdoms.Headers[k].FoundedTick);
            return (int)(best / SimConst.TicksPerYear);
        }

        static int MaxSpeciesInCity(SimWorld s)
        {
            var u = s.Units.Store;
            var seen = new HashSet<long>();
            var per = new Dictionary<int, int>();
            int best = 0;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k], c = u.City[i];
                if (c < 0 || !seen.Add(((long)c << 16) | u.Species[i])) continue;
                per.TryGetValue(c, out int n);
                per[c] = ++n;
                if (n > best) best = n;
            }
            return best;
        }

        static bool UndeadKing(SimWorld s)
        {
            var u = s.Units.Store;
            for (int k = 0; k < s.Meta.Kingdoms.Count; k++)
            {
                if (!s.Meta.Kingdoms.IsAlive(k)) continue;
                var king = s.Meta.Kingdoms.Items[k].King;
                if (u.IsAlive(king) && s.Content.Species[u.Species[king.Index]].IsUndeadCategory) return true;
            }
            return false;
        }

        int DiscoveredTraits(SimWorld s)
        {
            int n = 0;
            foreach (var t in s.Content.UnitTraits.All) if (_ui.Profile.IsDiscovered(t.Id)) n++;
            return n;
        }

        int KnownSynergies(SimWorld s)
        {
            int n = 0;
            foreach (var syn in s.Content.GeneSynergies.All)
            {
                bool all = syn.Requires != null && syn.Requires.Length > 0;
                if (all) foreach (var g in syn.Requires) all &= _ui.Profile.IsDiscovered(g);
                if (all) n++;
            }
            return n;
        }

        static long LandRemaining(SimWorld s)
        {
            long n = 0;
            var zones = s.World.Zones;
            for (int i = 0; i < zones.Length; i++) n += zones[i].LandTiles;
            return n;
        }

        static float FrozenWaterFraction(SimWorld s)
        {
            var map = s.World;
            long water = 0, frozen = 0;
            int n = map.Width * map.Height;
            for (int i = 0; i < n; i++)
            {
                ushort f = map.Flags[i];
                if ((f & (ushort)TileFlags.Water) == 0) continue;
                water++;
                if ((f & (ushort)TileFlags.Frozen) != 0) frozen++;
            }
            return water > 0 ? frozen / (float)water : 0f;
        }

        static bool AllNormalBiomesPresent(SimWorld s)
        {
            var map = s.World;
            var seen = new HashSet<byte>();
            int n = map.Width * map.Height;
            for (int i = 0; i < n; i++) if (map.Biome[i] != 0) seen.Add(map.Biome[i]);
            var db = s.Content;
            for (int i = 0; i < db.Biomes.Count; i++)
                if (!db.Biomes[i].Special && !seen.Contains(db.Biomes[i].MapValue)) return false;
            return true;
        }

        static int KingdomsWithCultureTrait(SimWorld s, int trait)
        {
            if (trait < 0 || s.Civ == null) return 0;
            var seen = new HashSet<int>();
            for (int c = 0; c < s.Civ.CityCount; c++)
            {
                if (!s.Civ.CityAlive(c)) continue;
                int cul = s.Civ.City(c).Culture;
                if (cul < 0 || !s.Meta.Cultures.IsAlive(cul) || !s.Meta.Cultures.Header(cul).Traits.Has(trait)) continue;
                int k = s.Civ.KingdomOf(c);
                if (k >= 0) seen.Add(k);
            }
            return seen.Count;
        }

        static bool AnyCultureTraitAged(SimWorld s, int trait, int years)
        {
            if (trait < 0) return false;
            for (int c = 0; c < s.Meta.Cultures.Count; c++)
            {
                if (!s.Meta.Cultures.IsAlive(c) || !s.Meta.Cultures.Header(c).Traits.Has(trait)) continue;
                if (s.Clock.Tick - s.Meta.Cultures.Header(c).FoundedTick >= years * (long)SimConst.TicksPerYear) return true;
            }
            return false;
        }

        static int CitiesWithBuilding(SimWorld s, int def)
        {
            if (def < 0 || s.Civ == null) return 0;
            int n = 0;
            for (int c = 0; c < s.Civ.CityCount; c++) if (s.Civ.CityAlive(c) && s.Civ.CountBuildings(c, def, true) > 0) n++;
            return n;
        }

        static int BurningTiles(SimWorld s)
        {
            var fire = s.World.Fire;
            int n = 0;
            for (int i = 0; i < fire.Length; i++) if (fire[i] != 0) n++;
            return n;
        }
    }
}
