using System;
using PG.Core;

namespace PG.Sim
{
    // Bölüm 6.13 templates; the localization key is "history." + the lower-case name (HistoryKeys).
    public enum HistoryKind : ushort
    {
        KingdomFounded, KingdomFell, NewKing, WarDeclared, Peace, CityFounded, CityRuined, CityCaptured, Rebellion,
        AllianceFormed, CultureFounded, LanguageFounded, ReligionFounded, ClanFounded, MetaDied, PlotSucceeded, PlotFailed,
        Disaster, BookWritten, ItemForged, SpeciesEndangered, SpeciesExtinct, Vassalage, TributePaid, CityPillaged, InventionDiscovered, Famine, PopulationMigrated,
        PersonDied, KingDied, Count,
    }

    public static class HistoryKeys
    {
        public static readonly string[] Keys =
        {
            "history.kingdom_founded", "history.kingdom_fell", "history.new_king", "history.war_declared", "history.peace",
            "history.city_founded", "history.city_ruined", "history.city_captured", "history.rebellion", "history.alliance_formed",
            "history.culture_founded", "history.language_founded", "history.religion_founded", "history.clan_founded",
            "history.meta_died", "history.plot_succeeded", "history.plot_failed", "history.disaster", "history.book_written", "history.item_forged",
            "history.species_endangered", "history.species_extinct", "history.vassalage", "history.tribute_paid", "history.city_pillaged",
            "history.invention_discovered", "history.famine", "history.population_migrated", "history.person_died", "history.king_died",
        };
    }

    // Writes world events into MetaWorld.History (A/B/C are uids; Extra carries a content index or kind).
    public sealed class HistoryRecorder
    {
        readonly SimWorld _sim;
        EraChangedEvent _pendingEraSummary;
        bool _hasPendingEraSummary;

        public HistoryRecorder(SimWorld sim)
        {
            _sim = sim;
            var e = sim.Events;
            e.Subscribe<KingdomFoundedEvent>(x => Rec(HistoryKind.KingdomFounded, KUid(x.Kingdom), _sim.Meta.Kingdoms.Headers[x.Kingdom].FounderUid));
            e.Subscribe<NewKingEvent>(OnNewKing);
            e.Subscribe<WarDeclaredEvent>(OnWarDeclared);
            e.Subscribe<EraChangedEvent>(OnEraChanged);
            e.Subscribe<PeaceEvent>(x => Rec(HistoryKind.Peace, _sim.Meta.Wars.Headers[x.War].Uid, 0, 0, (int)_sim.Meta.Wars.Items[x.War].Result));
            e.Subscribe<CityFoundedEvent>(x => Rec(HistoryKind.CityFounded, CUid(x.City)));
            e.Subscribe<CityRuinedEvent>(OnCityRuined);
            e.Subscribe<CityCapturedEvent>(OnCityCaptured);
            e.Subscribe<CityPillagedEvent>(x => Rec(HistoryKind.CityPillaged, CUid(x.City), KUid(x.Kingdom), x.Gold, x.Rations));
            e.Subscribe<FamineEvent>(OnFamine);
            e.Subscribe<FamineMigrationEvent>(OnFamineMigration);
            e.Subscribe<DisasterMigrationEvent>(OnDisasterMigration);
            e.Subscribe<RebellionEvent>(x => Rec(HistoryKind.Rebellion, CUid(x.City), KUid(x.From), KUid(x.To)));
            e.Subscribe<AllianceFormedEvent>(x => Rec(HistoryKind.AllianceFormed, _sim.Meta.Alliances.Headers[x.Alliance].Uid));
            e.Subscribe<VassalageFormedEvent>(x => Rec(HistoryKind.Vassalage, KUid(x.Subject), KUid(x.Overlord)));
            e.Subscribe<TributePaidEvent>(x => Rec(HistoryKind.TributePaid, KUid(x.Subject), KUid(x.Overlord), 0, x.Amount));
            e.Subscribe<MetaFoundedEvent>(OnFounded);
            e.Subscribe<MetaDiedEvent>(x => Rec(x.Kind == MetaKind.Kingdom ? HistoryKind.KingdomFell : HistoryKind.MetaDied, x.Kind == MetaKind.Kingdom ? KUid(x.Index) : 0, 0, 0, (int)x.Kind * 1000000 + x.Index));
            e.Subscribe<PlotFinishedEvent>(x => Rec(x.Success ? HistoryKind.PlotSucceeded : HistoryKind.PlotFailed, _sim.Meta.Plots.Headers[x.Plot].Uid,
                                                     _sim.Meta.Plots.Headers[x.Plot].FounderUid, 0, _sim.Meta.Plots.Items[x.Plot].Def));
            e.Subscribe<DisasterRequestEvent>(OnDisasterRequested);
            e.Subscribe<UnitBornEvent>(OnUnitBorn);
            e.Subscribe<UnitPairedEvent>(OnUnitPaired);
            e.Subscribe<UnitLevelUpEvent>(OnUnitLevelUp);
            e.Subscribe<UnitDiedEvent>(OnUnitDied);
            e.Subscribe<ItemEquippedEvent>(OnItemEquipped);
        }

        long KUid(int k) => k >= 0 && k < _sim.Meta.Kingdoms.Count ? _sim.Meta.Kingdoms.Headers[k].Uid : 0;
        long CUid(int c) => c >= 0 && c < _sim.Civ.CityCount ? _sim.Civ.Cities.Headers[c].Uid : 0;

        void OnFounded(MetaFoundedEvent x)
        {
            var m = _sim.Meta;
            switch (x.Kind)
            {
                case MetaKind.Culture: Rec(HistoryKind.CultureFounded, m.Cultures.Headers[x.Index].Uid, m.Cultures.Headers[x.Index].FounderUid); break;
                case MetaKind.Language: Rec(HistoryKind.LanguageFounded, m.Languages.Headers[x.Index].Uid, m.Languages.Headers[x.Index].FounderUid); break;
                case MetaKind.Religion: Rec(HistoryKind.ReligionFounded, m.Religions.Headers[x.Index].Uid, m.Religions.Headers[x.Index].FounderUid); break;
                case MetaKind.Clan: Rec(HistoryKind.ClanFounded, m.Clans.Headers[x.Index].Uid, m.Clans.Headers[x.Index].FounderUid); break;
            }
        }

        void OnNewKing(NewKingEvent x)
        {
            Rec(HistoryKind.NewKing, KUid(x.Kingdom), x.KingUid);
            EnsureBiography(x.KingUid, true);
            _sim.Meta.AddBiographyEvent(x.KingUid, _sim.Clock.Tick, BiographyEventKind.Office, KUid(x.Kingdom), 1);
        }

        void OnCityRuined(CityRuinedEvent x)
        {
            int kingdom = _sim.Civ != null && (uint)x.City < (uint)_sim.Civ.CityCount
                ? _sim.Civ.City(x.City).Kingdom : -1;
            Rec(HistoryKind.CityRuined, CUid(x.City), KUid(kingdom));
        }

        void OnWarDeclared(WarDeclaredEvent x)
        {
            long warUid = _sim.Meta.Wars.Headers[x.War].Uid;
            int warScale = HistoryImportanceScore.ArmyScale(CountWarSoldiers(_sim.Meta.Wars.Items[x.War]));
            RecScaled(HistoryKind.WarDeclared, KUid(x.Attacker), KUid(x.Defender), warUid,
                unchecked((int)0x40000000) | ((int)x.Cause << 8) | (int)x.Goal, warScale);
            RecordWarParticipants(x.Attacker, warUid);
            RecordWarParticipants(x.Defender, warUid);
        }

        int CountWarSoldiers(in WarData war)
        {
            var units = _sim.Units.Store;
            var civ = _sim.Civ;
            int count = 0;
            for (int n = 0; n < units.Alive.Length; n++)
            {
                int unit = units.Alive[n];
                if (units.Army[unit] < 0 && units.Job[unit] - 1 != civ.JobWarrior) continue;
                int kingdom = civ.KingdomOf(units.City[unit]);
                if (Contains(war.AttackerSide, kingdom) || Contains(war.DefenderSide, kingdom)) count++;
            }
            return count;
        }

        static bool Contains(Unity.Collections.FixedList64Bytes<int> side, int kingdom)
        {
            for (int i = 0; i < side.Length; i++) if (side[i] == kingdom) return true;
            return false;
        }

        void OnEraChanged(EraChangedEvent x)
        {
            _pendingEraSummary = x;
            _hasPendingEraSummary = true;
        }

        public void UpdatePeriodicSummary()
        {
            if (_hasPendingEraSummary)
            {
                int year = _pendingEraSummary.Year >= 0 ? _pendingEraSummary.Year : _sim.Clock.Year;
                int startYear = _pendingEraSummary.OldEraStartedYear >= 0 ? _pendingEraSummary.OldEraStartedYear : year;
                var summary = CaptureWorldSummary(WorldSummaryKind.EraTransition,
                    startYear, year, _pendingEraSummary.OldEra, _pendingEraSummary.NewEra, out var populationSplit);
                _sim.Meta.RecordWorldSummary(summary, populationSplit);
                _hasPendingEraSummary = false;
            }
            var clock = _sim.Clock;
            if (!clock.IsYearStart || clock.Year <= 0 || clock.Year % 100 != 0) return;
            int era = _sim.Nature.Era.CurrentEra;
            var century = CaptureWorldSummary(WorldSummaryKind.Century,
                clock.Year - 100, clock.Year, era, era, out var centuryPopulationSplit);
            _sim.Meta.RecordWorldSummary(century, centuryPopulationSplit);
        }

        WorldSummary CaptureWorldSummary(WorldSummaryKind kind, int startYear, int endYear, int eraFrom, int eraTo,
            out WorldSummaryPopulationSplit populationSplit)
        {
            var sim = _sim;
            var meta = sim.Meta;
            var units = sim.Units.Store;
            var report = new WorldSummary
            {
                Kind = kind, StartYear = startYear, EndYear = endYear,
                Population = units.Count, EraFrom = (ushort)Math.Max(0, eraFrom), EraTo = (ushort)Math.Max(0, eraTo),
            };
            populationSplit = new WorldSummaryPopulationSplit();
            for (int i = 0; i < units.Alive.Length; i++)
            {
                int unit = units.Alive[i];
                if (sim.Content.Species[units.Species[unit]].IsCiv) populationSplit.Civilization++;
                else populationSplit.Wildlife++;
            }

            for (int i = 0; i < sim.Civ.CityCount; i++)
                if (sim.Civ.CityAlive(i)) report.Cities++;
            for (int i = 0; i < meta.Kingdoms.Count; i++)
            {
                if (!meta.KingdomAlive(i)) continue;
                report.Kingdoms++;
                int members = meta.Kingdoms.Headers[i].MemberCount;
                if (members > report.LargestKingdomPopulation)
                {
                    report.LargestKingdomPopulation = members;
                    report.LargestKingdomUid = meta.Kingdoms.Headers[i].Uid;
                }
            }

            for (int i = 0; i < units.Alive.Length; i++)
            {
                int unit = units.Alive[i];
                if (units.Disease[unit] != 0) report.Sick++;
                if (sim.Content.Species[units.Species[unit]].IsUndeadCategory) report.Undead++;
            }

            long startTick = (long)Math.Max(0, startYear) * SimConst.TicksPerYear;
            long endTick = (long)Math.Max(0, endYear) * SimConst.TicksPerYear;
            for (int i = 0; i < meta.Wars.Count; i++)
            {
                var war = meta.Wars.Items[i];
                if (war.StartTick >= endTick) continue;
                long warEnd = war.EndTick > 0 ? war.EndTick : sim.Clock.Tick + 1;
                if (warEnd <= startTick) continue;
                if (war.Result == WarResult.Ongoing) report.Wars++;
                int casualties = Math.Max(0, war.CasualtiesA) + Math.Max(0, war.CasualtiesD);
                if (casualties <= report.DeadliestWarCasualties) continue;
                report.DeadliestWarCasualties = casualties;
                report.DeadliestWarUid = meta.Wars.Headers[i].Uid;
            }

            for (int i = 0; i < meta.BiographyTracks.Length; i++)
            {
                var track = meta.BiographyTracks[i];
                if (track.BirthTick >= endTick || (track.DeathTick > 0 && track.DeathTick < startTick)) continue;
                int score = track.LevelAtDeath * 10 + track.KillsAtDeath;
                if (score <= report.FamousScore) continue;
                report.FamousScore = score;
                report.FamousPersonUid = track.Uid;
            }
            for (int i = 0; i < units.Alive.Length; i++)
            {
                int unit = units.Alive[i];
                if (!meta.TryGetBiography(units.Uid[unit], out _)) continue;
                int score = units.Level[unit] * 10 + units.Kills[unit];
                if (score <= report.FamousScore) continue;
                report.FamousScore = score;
                report.FamousPersonUid = units.Uid[unit];
            }

            for (int i = 0; i < meta.History.Length; i++)
            {
                var entry = meta.History[i];
                if (entry.Tick < startTick || entry.Tick >= endTick) continue;
                if (entry.Template == (ushort)HistoryKind.CultureFounded) report.CulturesFounded++;
                else if (entry.Template == (ushort)HistoryKind.MetaDied && entry.Extra / 1000000 == (int)MetaKind.Culture)
                    report.CulturesEnded++;
            }
            return report;
        }

        void RecordWarParticipants(int kingdom, long warUid)
        {
            if (kingdom < 0 || kingdom >= _sim.Meta.Kingdoms.Count) return;
            var meta = _sim.Meta;
            long kingUid = UnitUid(meta.Kingdom(kingdom).King);
            if (kingUid != 0)
            {
                EnsureBiography(kingUid, true);
                meta.AddBiographyEvent(kingUid, _sim.Clock.Tick, BiographyEventKind.Battle, warUid, kingdom);
            }
            for (int i = 0; i < meta.Armies.Length; i++)
            {
                var army = meta.Armies[i];
                if (army.Kingdom != kingdom) continue;
                long captainUid = UnitUid(army.Captain);
                if (captainUid == 0) continue;
                EnsureBiography(captainUid, true);
                meta.AddBiographyEvent(captainUid, _sim.Clock.Tick, BiographyEventKind.Battle, warUid, kingdom);
            }
        }

        void OnUnitBorn(UnitBornEvent x)
        {
            var meta = _sim.Meta;
            bool familyIsNotable = meta.TryGetBiography(x.MotherUid, out _) || meta.TryGetBiography(x.FatherUid, out _)
                || IsRulerOrCaptain(x.MotherUid) || IsRulerOrCaptain(x.FatherUid);
            if (!familyIsNotable) return;
            int unit = FindLiveUnit(x.Uid);
            if (unit < 0) return;
            TrackUnit(unit);
            RecordChildForParent(x.MotherUid, x.Uid);
            RecordChildForParent(x.FatherUid, x.Uid);
        }

        void RecordChildForParent(long parentUid, long childUid)
        {
            if (parentUid == 0) return;
            EnsureBiography(parentUid, true);
            _sim.Meta.AddBiographyEvent(parentUid, _sim.Clock.Tick, BiographyEventKind.Child, childUid);
        }

        void OnUnitPaired(UnitPairedEvent x)
        {
            bool notable = _sim.Meta.TryGetBiography(x.FirstUid, out _) || _sim.Meta.TryGetBiography(x.SecondUid, out _)
                || IsRulerOrCaptain(x.FirstUid) || IsRulerOrCaptain(x.SecondUid);
            if (!notable) return;
            EnsureBiography(x.FirstUid, true);
            EnsureBiography(x.SecondUid, true);
            if (_sim.Meta.TryGetBiography(x.FirstUid, out int a))
            {
                var track = _sim.Meta.BiographyTracks[a]; track.MateUid = x.SecondUid; _sim.Meta.BiographyTracks[a] = track;
            }
            if (_sim.Meta.TryGetBiography(x.SecondUid, out int b))
            {
                var track = _sim.Meta.BiographyTracks[b]; track.MateUid = x.FirstUid; _sim.Meta.BiographyTracks[b] = track;
            }
            _sim.Meta.AddBiographyEvent(x.FirstUid, _sim.Clock.Tick, BiographyEventKind.Partnership, x.SecondUid);
            _sim.Meta.AddBiographyEvent(x.SecondUid, _sim.Clock.Tick, BiographyEventKind.Partnership, x.FirstUid);
        }

        void OnUnitLevelUp(UnitLevelUpEvent x)
        {
            if (x.Level < 5) return;
            EnsureBiography(x.Uid, true);
            _sim.Meta.AddBiographyEvent(x.Uid, _sim.Clock.Tick, BiographyEventKind.Milestone, 0, x.Level);
        }

        void OnUnitDied(UnitDiedEvent x)
        {
            bool notable = _sim.Meta.TryGetBiography(x.Uid, out _) || x.WasRulerOrCaptain || x.WasKing || IsRulerOrCaptain(x.Uid)
                || x.Level >= 5 || x.Kills >= 10 || x.HasLegendaryItem;
            if (!notable) return;
            bool existed = _sim.Meta.TryGetBiography(x.Uid, out _);
            _sim.Meta.TrackBiography(x.Uid, x.BirthTick, x.Species, x.NameId, x.MotherUid, x.FatherUid);
            if (!existed)
                _sim.Meta.AddBiographyEvent(x.Uid, x.BirthTick, BiographyEventKind.Birth, 0, x.Species);
            _sim.Meta.SetBiographyDeath(x.Uid, _sim.Clock.Tick, x.KillerUid, x.Cause, x.MateUid,
                x.MotherUid, x.FatherUid, x.Species, x.Level, x.Kills, x.NameId, x.BirthTick);
            bool wasKing = x.WasKing && x.Kingdom >= 0 && x.Kingdom < _sim.Meta.Kingdoms.Count;
            if (wasKing)
                Rec(DeathHistoryKind(true), KUid(x.Kingdom), x.Uid, x.KillerUid, (int)x.Cause);
            else
                Rec(DeathHistoryKind(false), x.Uid, x.KillerUid, 0, (int)x.Cause);
            if (x.KillerUid != 0 && EnsureBiography(x.KillerUid, false))
                _sim.Meta.AddBiographyEvent(x.KillerUid, _sim.Clock.Tick, BiographyEventKind.Battle, x.Uid, 1);
        }

        public static HistoryKind DeathHistoryKind(bool wasKing) => wasKing ? HistoryKind.KingDied : HistoryKind.PersonDied;

        void OnCityCaptured(CityCapturedEvent x)
        {
            Rec(HistoryKind.CityCaptured, CUid(x.City), KUid(x.From), KUid(x.To));
            RecordKingBattle(x.From, CUid(x.City), x.To);
            RecordKingBattle(x.To, CUid(x.City), x.From);
        }

        void OnFamine(FamineEvent x)
        {
            if ((uint)x.City >= (uint)_sim.Civ.CityCount || !_sim.Civ.CityAlive(x.City)) return;
            var city = _sim.Civ.City(x.City);
            Rec(HistoryKind.Famine, CUid(x.City), KUid(city.Kingdom), 0, (int)x.Cause);
        }

        void OnFamineMigration(FamineMigrationEvent x)
        {
            if ((uint)x.FromCity >= (uint)_sim.Civ.CityCount || (uint)x.ToCity >= (uint)_sim.Civ.CityCount
                || !_sim.Civ.CityAlive(x.FromCity) || !_sim.Civ.CityAlive(x.ToCity) || x.Migrants <= 0) return;
            int kingdom = _sim.Civ.City(x.FromCity).Kingdom;
            Rec(HistoryKind.PopulationMigrated, CUid(x.FromCity), CUid(x.ToCity), KUid(kingdom), x.Migrants);
        }

        void OnDisasterMigration(DisasterMigrationEvent x)
        {
            if ((uint)x.FromCity >= (uint)_sim.Civ.CityCount || (uint)x.ToCity >= (uint)_sim.Civ.CityCount
                || !_sim.Civ.CityAlive(x.FromCity) || !_sim.Civ.CityAlive(x.ToCity) || x.Migrants <= 0) return;
            int kingdom = _sim.Civ.City(x.FromCity).Kingdom;
            _sim.Meta.RecordWithMetadata(HistoryKind.PopulationMigrated, _sim.Clock.Tick,
                CUid(x.FromCity), CUid(x.ToCity), KUid(kingdom), x.Migrants, 0, HistoryMetadataFlags.DisasterMigration);
        }

        void OnDisasterRequested(DisasterRequestEvent x)
        {
            int city = NearestAffectedCity(x.X, x.Y);
            long cityUid = city >= 0 ? CUid(city) : 0;
            long kingdomUid = city >= 0 ? KUid(_sim.Civ.City(city).Kingdom) : 0;
            Rec(HistoryKind.Disaster, cityUid, kingdomUid, 0, x.Disaster);
        }

        int NearestAffectedCity(int x, int y)
        {
            const float maxDistanceSq = 64f; // DisasterExecutor uses an 8-tile brush.
            int best = -1;
            float bestDistanceSq = maxDistanceSq;
            for (int c = 0; c < _sim.Civ.CityCount; c++)
            {
                if (!_sim.Civ.CityAlive(c)) continue;
                var center = CityPlanner.Below(_sim.Civ.City(c).Center);
                float dx = center.x - x, dy = center.y - y;
                float distanceSq = dx * dx + dy * dy;
                if (distanceSq > bestDistanceSq) continue;
                best = c;
                bestDistanceSq = distanceSq;
            }
            return best;
        }

        void RecordKingBattle(int kingdom, long cityUid, int opposingKingdom)
        {
            if (kingdom < 0 || kingdom >= _sim.Meta.Kingdoms.Count) return;
            long uid = UnitUid(_sim.Meta.Kingdom(kingdom).King);
            if (uid == 0) return;
            EnsureBiography(uid, true);
            _sim.Meta.AddBiographyEvent(uid, _sim.Clock.Tick, BiographyEventKind.Battle, cityUid, opposingKingdom);
        }

        void OnItemEquipped(ItemEquippedEvent x)
        {
            int unique = _sim.Content.ItemQualities.IdOrDefault("q.unique");
            int legendary = _sim.Content.ItemQualities.IdOrDefault("q.legendary");
            if (x.Quality != unique && x.Quality != legendary) return;
            EnsureBiography(x.UnitUid, true);
            _sim.Meta.AddBiographyEvent(x.UnitUid, _sim.Clock.Tick, BiographyEventKind.LegendaryItem, x.ItemUid, x.Quality);
        }

        bool EnsureBiography(long uid, bool force)
        {
            if (uid == 0) return false;
            if (_sim.Meta.TryGetBiography(uid, out _)) return true;
            int unit = FindLiveUnit(uid);
            if (unit < 0 || (!force && !IsNotableUnit(unit))) return false;
            TrackUnit(unit);
            return true;
        }

        void TrackUnit(int unit)
        {
            var u = _sim.Units.Store;
            long uid = u.Uid[unit];
            bool existed = _sim.Meta.TryGetBiography(uid, out _);
            _sim.Meta.TrackBiography(uid, u.BirthTick[unit], u.Species[unit], u.NameId[unit],
                UnitUid(u.Mother[unit]), UnitUid(u.Father[unit]));
            if (!existed)
                _sim.Meta.AddBiographyEvent(uid, u.BirthTick[unit], BiographyEventKind.Birth, 0, u.Species[unit]);
        }

        bool IsNotableUnit(int unit)
        {
            var u = _sim.Units.Store;
            if (u.Level[unit] >= 5 || u.Kills[unit] >= 10) return true;
            int hero = _sim.Content.UnitTraits.IdOrDefault("tr.hero");
            if (hero >= 0 && u.Traits[unit].Has(hero)) return true;
            for (int slot = 0; slot < UnitStore.EquipSlots; slot++)
            {
                int itemIndex = u.Equip[unit * UnitStore.EquipSlots + slot];
                if (itemIndex < 0 || itemIndex >= _sim.Civ.Items.Length) continue;
                ushort quality = _sim.Civ.Items[itemIndex].Quality;
                if (quality == _sim.Content.ItemQualities.IdOrDefault("q.unique")
                    || quality == _sim.Content.ItemQualities.IdOrDefault("q.legendary")) return true;
            }
            return false;
        }

        bool IsRulerOrCaptain(long uid)
        {
            if (uid == 0) return false;
            var meta = _sim.Meta;
            for (int i = 0; i < meta.Kingdoms.Count; i++)
                if (meta.KingdomAlive(i) && UnitUid(meta.Kingdom(i).King) == uid) return true;
            for (int i = 0; i < meta.Armies.Length; i++)
                if (UnitUid(meta.Armies[i].Captain) == uid) return true;
            return false;
        }

        int FindLiveUnit(long uid)
        {
            if (uid == 0) return -1;
            var units = _sim.Units.Store;
            for (int i = 0; i < units.Alive.Length; i++)
            {
                int index = units.Alive[i];
                if (units.Uid[index] == uid) return index;
            }
            return -1;
        }

        long UnitUid(EntityId id)
        {
            var units = _sim.Units.Store;
            return units.IsAlive(id) ? units.Uid[id.Index] : 0;
        }

        void Rec(HistoryKind kind, long a, long b = 0, long c = 0, int extra = 0) => _sim.Meta.Record(kind, _sim.Clock.Tick, a, b, c, extra);

        void RecScaled(HistoryKind kind, long a, long b, long c, int extra, int armyScale)
            => _sim.Meta.RecordScaled(kind, _sim.Clock.Tick, a, b, c, extra, armyScale);
    }
}
