using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.11: yearly plot starts (law.plots), monthly recruiting and progress, outcomes by plot id.
    public sealed class PlotSystem : ISimSystem
    {
        public const float StartChance = 0.05f, InviteChance = 0.2f, CatchChance = 0.3f;
        public const int MaxMembers = 7, InviteRadius = 20;

        [Flags] enum Role { None = 0, Any = 1, CityLeader = 2, King = 4, Priest = 8, HighPriest = 16, Noble = 32, General = 64 }

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 7;

        int _law = -2;
        int _trAmbitious, _trZealous, _trDeceitful, _trCruel, _trMageBlood, _stInspired, _stCursed, _eraBlood, _sstMageBlood;
        int[] _plots;
        Role[] _roles;

        void Init(ContentDB db, WorldLaws laws)
        {
            _law = laws.IndexOf("law.plots");
            _trAmbitious = db.UnitTraits.IdOrDefault("tr.ambitious");
            _trZealous = db.UnitTraits.IdOrDefault("tr.zealous");
            _trDeceitful = db.UnitTraits.IdOrDefault("tr.deceitful");
            _trCruel = db.UnitTraits.IdOrDefault("tr.cruel");
            _trMageBlood = db.UnitTraits.IdOrDefault("tr.mage_blood");
            _sstMageBlood = db.SubspeciesTraits.IdOrDefault("sst.mage_blood");
            _stInspired = db.StatusEffects.IdOrDefault("st.inspired");
            _stCursed = db.StatusEffects.IdOrDefault("st.cursed");
            _eraBlood = db.Eras.IdOrDefault("era.blood");
            _plots = new int[db.Plots.Count];
            _roles = new Role[db.Plots.Count];
            for (int p = 0; p < _plots.Length; p++)
            {
                _plots[p] = p;
                _roles[p] = RoleOf(db.Plots[p].Initiator);
            }
        }

        static Role RoleOf(string initiator)
        {
            switch (initiator)
            {
                case "city_leader": return Role.CityLeader;
                case "leader": return Role.CityLeader | Role.King;
                case "king": return Role.King;
                case "king_or_general": return Role.King | Role.General;
                case "noble": return Role.Noble;
                case "priest": return Role.Priest;
                case "high_priest": return Role.HighPriest;
                default: return Role.Any;
            }
        }

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 2) return;
            if (_law == -2) Init(ctx.Content, ctx.Nature.Laws);
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            Progress(ctx, tick, ref rng);
            if (tick % SimConst.TicksPerYear != 2 || _law >= 0 && !ctx.Nature.Laws.IsOn(_law)) return;
            Starts(ctx, tick, ref rng);
        }

        // --- starts ---

        void Starts(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            for (int k = 0; k < meta.Kingdoms.Count; k++)
                if (meta.KingdomAlive(k) && u.IsAlive(meta.Kingdom(k).King)) TryStart(ctx, meta.Kingdom(k).King.Index, tick, ref rng);
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                var residents = civ.CityResidents[c];
                if (residents.Count == 0) continue;
                if (u.IsAlive(civ.City(c).Leader)) TryStart(ctx, civ.City(c).Leader.Index, tick, ref rng);
                TryStart(ctx, residents[rng.Range(0, residents.Count)], tick, ref rng); // an ordinary citizen
                foreach (int i in residents) if (u.Job[i] - 1 == civ.JobPriest && civ.JobPriest >= 0) { TryStart(ctx, i, tick, ref rng); break; }
            }
            for (int cl = 0; cl < meta.Clans.Count; cl++)
                if (meta.Clans.IsAlive(cl) && u.IsAlive(meta.Clans.Items[cl].Chief)) TryStart(ctx, meta.Clans.Items[cl].Chief.Index, tick, ref rng);
        }

        void TryStart(in SimContext ctx, int i, long tick, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            var w = ctx.Units;
            if (!u.IsAlive(i) || (AgeStage)u.Age[i] < AgeStage.Adult || (w.Abilities(i) & AbilityFlags.CanPlot) == 0 || InPlot(ctx.Meta, u.IdOf(i))) return;
            float chance = StartChance * (Has(u, i, _trAmbitious) ? 2f : 1f) * (_eraBlood >= 0 && ctx.Nature.Era.CurrentEra == _eraBlood ? 1.5f : 1f);
            if (!rng.Chance(chance)) return;
            Role roles = Roles(ctx, i);
            var db = ctx.Content;
            // shuffle the plot order so the first matching plot is not always the same
            for (int p = _plots.Length - 1; p > 0; p--) { int q = rng.Range(0, p + 1); (_plots[p], _plots[q]) = (_plots[q], _plots[p]); }
            foreach (int p in _plots)
            {
                if ((_roles[p] & roles) == 0) continue;
                var def = db.Plots[p];
                int target = -1;
                bool ok = true;
                foreach (var cond in def.Conditions ?? Array.Empty<string>())
                    if (!Condition(ctx, i, cond, ref target)) { ok = false; break; }
                if (!ok) continue;
                var data = new PlotData { Def = (ushort)p, Initiator = u.IdOf(i), TargetMeta = target, StartTick = tick, State = PlotState.Active };
                data.Members.Add(u.IdOf(i));
                int plot = ctx.Meta.Plots.Create(data, u.Uid[i], tick);
                ctx.Meta.Plots.Header(plot).NameId = u.NameId[i];
                return;
            }
        }

        Role Roles(in SimContext ctx, int i)
        {
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            var id = u.IdOf(i);
            Role r = Role.Any;
            int c = u.City[i];
            int k = civ.KingdomOf(c);
            if (c >= 0 && civ.CityAlive(c) && civ.City(c).Leader == id) r |= Role.CityLeader;
            if (meta.KingdomAlive(k) && meta.Kingdom(k).King == id) r |= Role.King;
            if (civ.JobPriest >= 0 && u.Job[i] - 1 == civ.JobPriest)
            {
                r |= Role.Priest;
                if (meta.Religions.IsAlive(u.Religion[i]) && meta.Religions.Items[u.Religion[i]].HolyCity == c) r |= Role.HighPriest;
            }
            if (u.Clan[i] >= 0 && meta.Clans.IsAlive(u.Clan[i]) && meta.Clans.Items[u.Clan[i]].Chief == id) r |= Role.Noble;
            if (meta.KingdomAlive(k) && u.IsAlive(meta.Kingdom(k).King) && (u.Mother[i] == meta.Kingdom(k).King || u.Father[i] == meta.Kingdom(k).King)) r |= Role.Noble;
            if (u.Army[i] >= 0 || civ.JobWarrior >= 0 && u.Job[i] - 1 == civ.JobWarrior && u.Stat(i, StatId.Warfare) >= 5f) r |= Role.General;
            return r;
        }

        static bool Has(UnitStore u, int i, int trait) => trait >= 0 && u.Traits[i].Has(trait);

        bool InPlot(MetaWorld meta, EntityId id)
        {
            for (int p = 0; p < meta.Plots.Count; p++)
            {
                if (!meta.Plots.IsAlive(p)) continue;
                var m = meta.Plots.Items[p].Members;
                for (int k = 0; k < m.Length; k++) if (m[k] == id) return true;
            }
            return false;
        }

        // PlotConditionRegistry (Bölüm 6.11): stat thresholds, trait keys and contextual keys; the context keys pick the target.
        bool Condition(in SimContext ctx, int i, string cond, ref int target)
        {
            var u = ctx.Units.Store;
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            int c = u.City[i];
            int k = civ.KingdomOf(c);
            bool kingdom = meta.KingdomAlive(k);
            switch (cond)
            {
                case "ambitious_or_inspired": return Has(u, i, _trAmbitious) || _stInspired >= 0 && u.HasStatus(i, _stInspired);
                case "zealous_or_inspired": return Has(u, i, _trZealous) || _stInspired >= 0 && u.HasStatus(i, _stInspired);
                case "deceitful_or_cruel": return Has(u, i, _trDeceitful) || Has(u, i, _trCruel);
                case "cultist_or_cursed": return _stCursed >= 0 && u.HasStatus(i, _stCursed) || Has(u, i, _trCruel);
                case "ambitious": return Has(u, i, _trAmbitious);
                case "abstract_thought": return (ctx.Units.Abilities(i) & AbilityFlags.CanHoldReligion) != 0;
                case "mage_blood": return Has(u, i, _trMageBlood) || _sstMageBlood >= 0 && u.Subspecies[i] >= 0 && ctx.Units.Subspecies.Traits(u.Subspecies[i]).Has(_sstMageBlood);
                case "2 civil>=2":
                    int civil = (u.Stat(i, StatId.Intel) >= 2 ? 1 : 0) + (u.Stat(i, StatId.Diplo) >= 2 ? 1 : 0) + (u.Stat(i, StatId.Warfare) >= 2 ? 1 : 0) + (u.Stat(i, StatId.Steward) >= 2 ? 1 : 0);
                    return civil >= 2;
                case "kendi şehri": // own city with people of another faith
                    if (c < 0 || u.Religion[i] < 0) return false;
                    foreach (int o in civ.CityResidents[c]) if (u.Religion[o] != u.Religion[i]) { target = c; return true; }
                    return false;
                case "çocuk>=3":
                    int children = 0;
                    var id = u.IdOf(i);
                    for (int n = 0; n < u.Alive.Length && children < 3; n++) if (u.Mother[u.Alive[n]] == id || u.Father[u.Alive[n]] == id) children++;
                    return children >= 3;
                case "farklı kültür/din":
                    if (!kingdom || c < 0) return false;
                    return civ.City(c).Culture != meta.Kingdom(k).Culture || civ.City(c).Religion != meta.Kingdom(k).Religion;
                case "hedef lider": // the ruler of the most disliked kingdom
                    target = Worst(meta, k, out float worst);
                    return target >= 0 && worst < 0f && u.IsAlive(meta.Kingdom(target).King);
                case "ortak düşman":
                    for (int b = 0; kingdom && b < meta.Kingdoms.Count; b++)
                        if (b != k && meta.KingdomAlive(b) && !meta.AtWar(k, b) && SharesEnemy(meta, k, b)) { target = b; return true; }
                    return false;
                case "savaş>=5 yıl":
                    for (int w = 0; kingdom && w < meta.Wars.Count; w++)
                    {
                        if (!meta.Wars.IsAlive(w)) continue;
                        var d = meta.Wars.Items[w];
                        if ((d.Attacker == k || d.Defender == k) && ctx.Clock.Tick - d.StartTick >= 5L * SimConst.TicksPerYear) { target = w; return true; }
                    }
                    return false;
                case "holy_war doktrini":
                    if (!kingdom || !meta.ReligionHas(meta.Kingdom(k).Religion, meta.RelHolyWar)) return false;
                    for (int b = 0; b < meta.Kingdoms.Count; b++)
                        if (b != k && meta.KingdomAlive(b) && meta.Kingdom(b).Religion >= 0 && meta.Kingdom(b).Religion != meta.Kingdom(k).Religion) { target = b; return true; }
                    return false;
                case "dil 3+ şehre yayılmış":
                    if (c < 0 || civ.City(c).Language < 0) return false;
                    int speaking = 0;
                    for (int o = 0; o < civ.CityCount; o++) if (civ.CityAlive(o) && civ.City(o).Language == civ.City(c).Language) speaking++;
                    return speaking >= 3;
                case "uzak şehir":
                    int lang = c >= 0 ? civ.City(c).Language : -1;
                    if (lang < 0 || !meta.Languages.IsAlive(lang)) return false;
                    int origin = meta.Languages.Items[lang].OriginCity;
                    return origin >= 0 && origin < civ.CityCount && origin != c &&
                           math.distance((float2)civ.City(c).Center, civ.City(origin).Center) / WorldMap.ZoneSize >= CultureSystem.DialectZones / 2;
            }
            // "<stat>>=N", "loyalty<N", "opinion<N" / "opinion>N"
            if (cond.StartsWith("loyalty<", StringComparison.Ordinal))
                return c >= 0 && int.TryParse(cond.Substring(8), out int l) && civ.City(c).Loyalty < l && !(kingdom && meta.Kingdom(k).Capital == c);
            if (cond.StartsWith("opinion", StringComparison.Ordinal) && cond.Length > 8 && int.TryParse(cond.Substring(8), out int op))
            {
                bool below = cond[7] == '<';
                for (int b = 0; kingdom && b < meta.Kingdoms.Count; b++)
                {
                    if (b == k || !meta.KingdomAlive(b)) continue;
                    int o = meta.Opinion(k, b);
                    if (below ? o < op : o > op) { target = b; return true; }
                }
                return false;
            }
            int ge = cond.IndexOf(">=", StringComparison.Ordinal);
            if (ge > 0 && float.TryParse(cond.Substring(ge + 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float min) &&
                StatKeys.TryGet(cond.Substring(0, ge), out var stat))
                return u.Stat(i, stat) >= min;
            return false; // unknown key: never true (content validation lists them)
        }

        static int Worst(MetaWorld meta, int k, out float worst)
        {
            int best = -1;
            worst = float.MaxValue;
            for (int b = 0; meta.KingdomAlive(k) && b < meta.Kingdoms.Count; b++)
            {
                if (b == k || !meta.KingdomAlive(b)) continue;
                float o = meta.Opinion(k, b);
                if (o < worst) { worst = o; best = b; }
            }
            return best;
        }

        static bool SharesEnemy(MetaWorld meta, int a, int b)
        {
            for (int e = 0; e < meta.Kingdoms.Count; e++)
                if (e != a && e != b && meta.AtWar(a, e) && meta.AtWar(b, e)) return true;
            return false;
        }

        // --- progress ---

        void Progress(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            var db = ctx.Content;
            for (int p = 0; p < meta.Plots.Count; p++)
            {
                if (!meta.Plots.IsAlive(p)) continue;
                ref var d = ref meta.Plots.Data(p);
                var def = db.Plots[d.Def];
                for (int m = d.Members.Length - 1; m >= 0; m--) if (!u.IsAlive(d.Members[m])) d.Members.RemoveAt(m);
                if (!u.IsAlive(d.Initiator))
                {
                    // the strongest member takes over half the time
                    int heir = -1;
                    for (int m = 0; m < d.Members.Length; m++)
                    {
                        int i = d.Members[m].Index;
                        if (heir < 0 || u.Level[i] > u.Level[heir]) heir = i;
                    }
                    if (heir >= 0 && rng.Chance(0.5f)) d.Initiator = u.IdOf(heir);
                    else { Finish(ctx, p, false, tick); continue; }
                }
                Recruit(ctx, ref d, ref rng);
                if (tick - d.StartTick > 3L * math.max(1, def.DurationMonths) * SimConst.TicksPerMonth) { Finish(ctx, p, false, tick); continue; }
                if (d.Members.Length < math.max(1, def.MinParticipants)) continue;
                float sum = 0f;
                for (int m = 0; m < d.Members.Length; m++) sum += u.Stat(d.Members[m].Index, StatId.Intel) + u.Stat(d.Members[m].Index, StatId.Diplo);
                d.Progress += sum * 0.5f / math.max(1, def.DurationMonths) / 10f;
                if (d.Progress < 1f) continue;
                bool success = Outcome(ctx, p, ref d, def, tick, ref rng);
                Finish(ctx, p, success, tick);
            }
        }

        void Recruit(in SimContext ctx, ref PlotData d, ref SimRandom rng)
        {
            if (d.Members.Length >= MaxMembers || !rng.Chance(InviteChance)) return;
            var u = ctx.Units.Store;
            int c = u.City[d.Initiator.Index];
            if (c < 0) return;
            var residents = ctx.Civ.CityResidents[c];
            if (residents.Count == 0) return;
            int o = residents[rng.Range(0, residents.Count)];
            var id = u.IdOf(o);
            if ((AgeStage)u.Age[o] < AgeStage.Adult || (ctx.Units.Abilities(o) & AbilityFlags.CanPlot) == 0 || InPlot(ctx.Meta, id)) return;
            if (math.distance(u.Pos[o], u.Pos[d.Initiator.Index]) > InviteRadius) return;
            d.Members.Add(id);
        }

        static void Finish(in SimContext ctx, int p, bool success, long tick)
        {
            ref var d = ref ctx.Meta.Plots.Data(p);
            d.State = success ? PlotState.Succeeded : PlotState.Failed;
            ctx.Meta.Plots.MarkDead(p, tick);
            ctx.Events.Publish(new PlotFinishedEvent(p, success));
        }

        // PlotOutcomeRegistry
        bool Outcome(in SimContext ctx, int p, ref PlotData d, PlotDef def, long tick, ref SimRandom rng)
        {
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var w = ctx.Units;
            var u = w.Store;
            int i = d.Initiator.Index;
            int c = u.City[i];
            int k = civ.KingdomOf(c);
            bool city = c >= 0 && civ.CityAlive(c);
            bool kingdom = meta.KingdomAlive(k);
            switch (def.Id)
            {
                case "plot.rebellion":
                    if (!city || !kingdom || meta.Kingdom(k).Capital == c) return false;
                    KingdomSystem.Rebel(ctx, civ, meta, c, k, tick);
                    return true;
                case "plot.secession":
                    if (!city || !kingdom || meta.Kingdom(k).Capital == c) return false;
                    int nk = KingdomSystem.Rebel(ctx, civ, meta, c, k, tick);
                    for (int o = 0; o < civ.CityCount; o++)
                        if (o != c && civ.CityAlive(o) && civ.City(o).Kingdom == k && o != meta.Kingdom(k).Capital &&
                            civ.City(o).Culture == civ.City(c).Culture && civ.City(o).Religion == civ.City(c).Religion)
                            civ.City(o).Kingdom = nk;
                    return true;
                case "plot.new_religion":
                    if (!city) return false;
                    int r = CultureSystem.NewReligion(ctx, c, u.Uid[i], ref rng);
                    for (int m = 0; m < d.Members.Length; m++) { u.Religion[d.Members[m].Index] = r; u.StatsDirty[d.Members[m].Index] = 1; }
                    return true;
                case "plot.culture_split":
                    if (!city || civ.City(c).Culture < 0) return false;
                    int cul = CultureSystem.NewCulture(ctx, c, civ.City(c).Culture, u.Uid[i], ref rng);
                    Reform(ctx, cul, ref rng);
                    foreach (int o in civ.CityResidents[c]) if (u.Culture[o] == civ.City(c).Culture) { u.Culture[o] = cul; u.StatsDirty[o] = 1; }
                    civ.City(c).Culture = cul;
                    return true;
                case "plot.language_split":
                    if (!city || civ.City(c).Language < 0) return false;
                    int lang = CultureSystem.NewLanguage(ctx, c, civ.City(c).Language, u.Uid[i], ref rng);
                    foreach (int o in civ.CityResidents[c]) if (u.Language[o] == civ.City(c).Language) u.Language[o] = lang;
                    civ.City(c).Language = lang;
                    return true;
                case "plot.alliance":
                    return kingdom && meta.KingdomAlive(d.TargetMeta) && KingdomSystem.Ally(ctx, meta, k, d.TargetMeta, tick);
                case "plot.war_declaration":
                    if (!kingdom || !meta.KingdomAlive(d.TargetMeta) || meta.AtWar(k, d.TargetMeta)) return false;
                    WarSystem.Declare(ctx, meta, k, d.TargetMeta, meta.WarConquest, -1, tick);
                    return true;
                case "plot.crusade":
                    if (!kingdom || !meta.KingdomAlive(d.TargetMeta) || meta.AtWar(k, d.TargetMeta)) return false;
                    WarSystem.Declare(ctx, meta, k, d.TargetMeta, meta.WarReligious, -1, tick);
                    return true;
                case "plot.assassination":
                    if (!meta.KingdomAlive(d.TargetMeta) || !u.IsAlive(meta.Kingdom(d.TargetMeta).King)) return false;
                    u.Kill(meta.Kingdom(d.TargetMeta).King.Index, DeathCause.Killed, i);
                    if (rng.Chance(CatchChance)) u.Kill(i, DeathCause.Killed); // caught and executed
                    return true;
                case "plot.usurpation":
                    if (!kingdom || meta.Kingdom(k).King == d.Initiator) return false;
                    meta.Kingdom(k).King = d.Initiator;
                    meta.Kingdom(k).SuccessionTick = tick;
                    ctx.Events.Publish(new NewKingEvent(k, u.Uid[i]));
                    return true;
                case "plot.conversion":
                    if (!city || u.Religion[i] < 0) return false;
                    foreach (int o in civ.CityResidents[c]) if ((w.Abilities(o) & AbilityFlags.CanHoldReligion) != 0) { u.Religion[o] = u.Religion[i]; u.StatsDirty[o] = 1; }
                    return true;
                case "plot.new_clan":
                    int clan = CultureSystem.NewClan(ctx, i, ref rng);
                    var id = d.Initiator;
                    u.Clan[i] = clan;
                    for (int n = 0; n < u.Alive.Length; n++) { int o = u.Alive[n]; if (u.Mother[o] == id || u.Father[o] == id) u.Clan[o] = clan; }
                    return true;
                case "plot.royal_marriage":
                    if (!kingdom || !meta.KingdomAlive(d.TargetMeta)) return false;
                    meta.SetOpinion(k, d.TargetMeta, meta.Opinion(k, d.TargetMeta) + 30);
                    meta.SetOpinion(d.TargetMeta, k, meta.Opinion(d.TargetMeta, k) + 30);
                    if (rng.Chance(0.3f)) KingdomSystem.Ally(ctx, meta, k, d.TargetMeta, tick);
                    return true;
                case "plot.reform":
                    if (!kingdom || !meta.Cultures.IsAlive(meta.Kingdom(k).Culture)) return false;
                    Reform(ctx, meta.Kingdom(k).Culture, ref rng);
                    return true;
                case "plot.peace_treaty":
                    if (d.TargetMeta < 0 || !meta.Wars.IsAlive(d.TargetMeta)) return false;
                    WarSystem.EndWar(ctx, meta, d.TargetMeta, WarResult.Peace, tick);
                    return true;
                case "plot.dark_ritual":
                    if (!city) return false;
                    float2 at = (float2)CityPlanner.Below(civ.City(c).Center) + new float2(rng.Range(-15f, 15f), rng.Range(-15f, 15f));
                    if (rng.Chance(0.5f)) ctx.Powers.Objects.Add(new WorldObject { Kind = WorldObjectKind.Rift, Pos = at, Ticks = 3 * SimConst.TicksPerYear });
                    else RaiseDead(ctx, at, 30f, ref rng);
                    return true;
            }
            return false;
        }

        // a culture swaps one trait for another of the same group
        static void Reform(in SimContext ctx, int culture, ref SimRandom rng)
        {
            var defs = ctx.Content.CultureTraits;
            ref var traits = ref ctx.Meta.Cultures.Header(culture).Traits;
            int from = -1;
            for (int t = 0; t < defs.Count; t++) if (traits.Has(t) && rng.Chance(0.5f)) { from = t; break; }
            string group = from >= 0 ? defs[from].Group : null;
            for (int n = 0; n < 16; n++)
            {
                int to = rng.Range(0, defs.Count);
                if (to == from || group != null && defs[to].Group != group) continue;
                if (from >= 0) traits.Set(from, false);
                traits.Set(to, true);
                return;
            }
        }

        static void RaiseDead(in SimContext ctx, float2 at, float radius, ref SimRandom rng)
        {
            int zombie = ctx.Content.Species.IdOrDefault("sp.zombie");
            if (zombie < 0) return;
            var u = ctx.Units.Store;
            var map = ctx.World;
            int raised = 0;
            for (int k = u.Corpses.Length - 1; k >= 0 && raised < 20; k--)
            {
                var c = u.Corpses[k];
                float2 p = new float2(c.Tile % map.Width, c.Tile / map.Width) + 0.5f;
                if (math.distance(p, at) > radius) continue;
                ctx.Units.Spawn(new UnitSpawnRequest { Species = zombie, Pos = p, AgeYears = 1f, Sex = 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1 }, ctx.Clock.Tick, ref rng);
                u.Corpses.RemoveAtSwapBack(k);
                raised++;
            }
        }
    }
}
