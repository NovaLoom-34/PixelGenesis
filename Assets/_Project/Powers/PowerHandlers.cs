using System;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Powers
{
    // Bölüm 7.1 locks: achievements arrive with Bölüm 8; until then every power counts as earned (DECISIONS #63).
    public static class PowerLocks
    {
        public static Func<string, bool> IsEarned = _ => true;

        public static bool CanUse(PowerDef def, WorldLaws laws) =>
            string.IsNullOrEmpty(def.UnlockedBy) || laws.IsOn("law.forbidden_codex") || IsEarned(def.UnlockedBy);
    }

    // Bölüm 7.1 handler map for everything past tile brushes, clouds, seeds and creature spawns.
    public static class PowerHandlers
    {
        public const int AreaInterval = 4, MagnetInterval = 2;
        public static int LastCivSpecies = -1; // "found village" uses the last civilized species spawned (UI state, not saved)

        // Powers applied repeatedly while the button is held.
        public static bool Repeats(PowerDef def, ContentDB db, out int interval)
        {
            interval = -1;
            if (DropEffects.Create(def, db) != null) interval = (int)math.max(1f, def.ParamNumber("interval", PowerOps.DefaultDropInterval));
            else if (def.ParamString("grab") != null) interval = MagnetInterval;
            else if (def.Type == "brush" && Create(def, db) != null) interval = AreaInterval;
            return interval > 0;
        }

        public static IPointPowerOp Create(PowerDef def, ContentDB db)
        {
            if (def.Type == "drop" && DropEffects.Create(def, db) != null) return new DropSpawnOp((ushort)def.Index);
            string spawn = def.ParamString("spawn");
            switch (spawn)
            {
                case "tornado": return new ObjectOp(WorldObjectKind.Tornado, (int)def.ParamNumber("durationTicks", 600f));
                case "volcano": return new ObjectOp(WorldObjectKind.Volcano, 400);
                case "geyser": return new ObjectOp(WorldObjectKind.Geyser, -1);
                case "monolith": return new ObjectOp(WorldObjectKind.Monolith, -1);
                case "lure": return new ObjectOp(WorldObjectKind.Lure, 10 * SimConst.TicksPerYear);
                case "village": return new VillageOp();
            }
            if (def.ParamString("pull") != null) return new ObjectOp(WorldObjectKind.BlackHole, (int)def.ParamNumber("durationTicks", 400f));
            string automaton = def.ParamString("automaton");
            if (automaton == "langton") return new ObjectOp(WorldObjectKind.Ant, -1);
            if (automaton == "conway") return new LifeGameOp();

            string target = def.ParamString("target");
            string status = def.ParamString("status");
            if (target == "unit" && status != null) return new UnitStatusOp(db.StatusEffects.IdOrDefault("st." + status));
            if (target == "city" && status != null) return new CityStatusOp(db.StatusEffects.IdOrDefault("st." + status));
            if (target == "kingdom_pair" && def.ParamNumber("alliance", 0f) > 0f) return new AllianceOp();
            if (target == "kingdom_pair") return new OpinionOp((int)def.ParamNumber("opinion", 0f));
            if (target == "city" && def.ParamNumber("loyalty", 0f) != 0f) return new LoyaltyOp((int)def.ParamNumber("loyalty", 0f));
            if (target == "city" && def.ParamNumber("makeCapital", 0f) > 0f) return new CapitalOp();
            if (target == "city" && def.ParamNumber("spawnMigrants", 0f) > 0f) return new MigrantsOp((int)def.ParamNumber("spawnMigrants", 0f));
            if (target == "city" && def.ParamNumber("happiness", 0f) > 0f) return new FestivalOp(db.HappinessEvents.IdOrDefault("hap.festival"));
            if (target == "unit" && def.ParamNumber("makeKing", 0f) > 0f) return new CrownOp();
            if (target == "unit" && def.ParamNumber("changeKingdom", 0f) > 0f) return new RelocateOp();

            string infect = def.ParamString("infect");
            if (infect != null && db.Diseases.TryGet(infect, out var disease)) return new InfectOp(disease.Index);
            if (def.ParamString("grab") != null) return new MagnetOp((int)def.ParamNumber("max", 50f));
            if (def.ParamNumber("poke", 0f) > 0f) return new FingerOp();
            if (def.ParamString("lightning") != null)
                return new StormOp(db.Powers.IdOrDefault("pw.lightning"), Arg(def.ParamString("lightning"), 20), def.ParamNumber("radius", 20f));
            if (def.ParamString("quake") != null) return new QuakeOp(Arg(def.ParamString("quake"), 30));
            if (def.ParamString("flood") != null) return new FloodOp(Arg(def.ParamString("flood"), 25));
            if (def.ParamString("wave") != null) return new ObjectOp(WorldObjectKind.Wave, -1);

            if (def.Type != "brush") return null;
            if (def.ParamNumber("growPlants", 0f) > 0f) return new FertileRainOp();
            if (def.ParamNumber("temp", 0f) != 0f) return new TempOp((int)def.ParamNumber("temp", 0f));
            if (def.ParamString("reanimate") != null) return new RaiseDeadOp(db.Species.IdOrDefault("sp." + (def.ParamString("as") ?? "zombie")));
            string cure = def.ParamString("cure");
            if (cure != null)
                return new CureOp(cure == "all" ? -1 : db.Diseases.IdOrDefault(cure), db.StatusEffects.IdOrDefault("st." + (def.ParamString("removeStatus") ?? "")),
                                  status != null ? db.StatusEffects.IdOrDefault("st." + status) : -1, cure == "all");
            if (def.ParamNumber("ray", 0f) > 0f) return new HeatRayOp();
            if (def.ParamString("dryWater") != null) return new DroughtOp((byte)db.Tiles.IdOrDefault("tile.sand", 0));
            string op = def.ParamString("op");
            if (op == "zone_add" || op == "zone_remove") return new ZoneOp(op == "zone_add");
            return null;
        }

        // "radius:30" / "count:20" -> 30 / 20
        static int Arg(string value, int fallback)
        {
            int colon = value?.IndexOf(':') ?? -1;
            return colon >= 0 && int.TryParse(value.Substring(colon + 1), out int n) ? n : fallback;
        }

        // --- shared helpers ---

        public static int NearestUnit(UnitStore u, int2 tile, float radius)
        {
            float2 p = (float2)tile + 0.5f;
            int best = -1;
            float bestD = radius * radius;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                float d = math.distancesq(u.Pos[i], p);
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public static int CityAt(in SimContext ctx, int2 t)
        {
            int c = ctx.Civ.CityAtTile(t.x, t.y);
            return c >= 0 && ctx.Civ.CityAlive(c) ? c : -1;
        }

        static bool InBrush(in PowerCommand cmd, float2 pos) => math.distancesq(pos, (float2)cmd.To + 0.5f) <= (cmd.Brush.Radius + 0.75f) * (cmd.Brush.Radius + 0.75f);

        static bool InBrush(in PowerCommand cmd, int dx, int dy) => Brushes.Contains(cmd.Brush.Shape, cmd.Brush.Radius, dx, dy);

        // --- drops ---

        sealed class DropSpawnOp : IPointPowerOp
        {
            readonly ushort _power;
            public DropSpawnOp(ushort power) => _power = power;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var drops = ctx.Powers.Drops;
                if (drops.Length >= PowerWorld.MaxDrops) return;
                int r = cmd.Brush.Radius;
                float2 pos = (float2)cmd.To + 0.5f;
                if (r > 0) pos += new float2(rng.Range(-r, r + 1), rng.Range(-r, r + 1));
                var map = ctx.World;
                pos = math.clamp(pos, 0.5f, new float2(map.Width - 0.5f, map.Height - 0.5f));
                drops.Add(new DropData { Power = _power, Pos = pos, Z = DropSystemConst.StartZ, Delay = DropEffects.DelayOf(ctx.Content.Powers[_power]) });
            }
        }

        // --- world objects ---

        sealed class ObjectOp : IPointPowerOp
        {
            readonly WorldObjectKind _kind;
            readonly int _ticks;

            public ObjectOp(WorldObjectKind kind, int ticks)
            {
                _kind = kind;
                _ticks = ticks;
            }

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var objects = ctx.Powers.Objects;
                if (_kind == WorldObjectKind.Ant)
                {
                    int ants = 0;
                    for (int k = 0; k < objects.Length; k++) if (objects[k].Dead == 0 && objects[k].Kind == WorldObjectKind.Ant) ants++;
                    if (ants >= PowerWorld.MaxAnts) return;
                    ctx.Powers.EnsureAutomaton();
                }
                var o = new WorldObject { Kind = _kind, Pos = (float2)cmd.To + 0.5f, Ticks = _ticks, Power = cmd.PowerId };
                switch (_kind)
                {
                    case WorldObjectKind.Tornado:
                        o.Dir = new float2(math.cos(ctx.Nature.WindAngle), math.sin(ctx.Nature.WindAngle));
                        break;
                    case WorldObjectKind.Ant:
                        o.Pos = math.floor(o.Pos);
                        o.Dir = new float2(0f, 1f);
                        break;
                    case WorldObjectKind.Wave:
                        if (!WorldObjects.StartWave(ctx, cmd.To, ref o)) return;
                        break;
                }
                objects.Add(o);
            }
        }

        // Bölüm 7.5: 8 adults of the last civilized species; the settlement system founds the camp next month.
        sealed class VillageOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int species = LastCivSpecies >= 0 ? LastCivSpecies : ctx.Content.Species.IdOrDefault("sp.human");
                if (species < 0) return;
                SpawnGroup(ctx, species, cmd.To, 8, -1, ref rng);
            }
        }

        public static void SpawnGroup(in SimContext ctx, int species, int2 at, int count, int city, ref SimRandom rng)
        {
            var units = ctx.Units;
            var sp = ctx.Content.Species[species];
            if (!units.Paths.NearestStandable(at, Mobility.Land, out int2 stand)) return;
            float lifespan = sp.BaseStats[(int)StatId.Lifespan];
            SpawnSpread.Begin();
            for (int n = 0; n < count && units.Store.Count < UnitWorld.HardUnitCap; n++)
            {
                var id = units.Spawn(new UnitSpawnRequest
                {
                    Species = species, Pos = SpawnSpread.Next(units, Mobility.Land, (float2)stand + 0.5f, count, 3f, ref rng),
                    AgeYears = lifespan > 0f ? lifespan * rng.Range(0.22f, 0.35f) : 20f,
                    Sex = n % 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                }, ctx.Clock.Tick, ref rng);
                if (city >= 0 && !id.IsNone) units.Store.City[id.Index] = city;
            }
        }

        // --- targeted ---

        sealed class UnitStatusOp : IPointPowerOp
        {
            readonly int _status;
            public UnitStatusOp(int status) => _status = status;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int i = NearestUnit(ctx.Units.Store, cmd.To, 3f);
                if (i >= 0) ctx.Units.AddStatus(i, _status);
            }
        }

        sealed class CityStatusOp : IPointPowerOp
        {
            readonly int _status;
            public CityStatusOp(int status) => _status = status;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int c = CityAt(ctx, cmd.To);
                if (c < 0) return;
                foreach (int i in ctx.Civ.CityResidents[c]) ctx.Units.AddStatus(i, _status);
            }
        }

        // Two clicks: the first city (From) and the second (To); both kingdoms change their view of each other.
        sealed class OpinionOp : IPointPowerOp
        {
            readonly int _amount;
            public OpinionOp(int amount) => _amount = amount;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int a = ctx.Civ.KingdomOf(CityAt(ctx, cmd.From)), b = ctx.Civ.KingdomOf(CityAt(ctx, cmd.To));
                var meta = ctx.Meta;
                if (a < 0 || b < 0 || a == b || !meta.KingdomAlive(a) || !meta.KingdomAlive(b)) return;
                meta.SetOpinion(a, b, meta.Opinion(a, b) + _amount);
                meta.SetOpinion(b, a, meta.Opinion(b, a) + _amount);
            }
        }

        // Two clicks: directly allies two kingdoms (ends any active war between them first).
        sealed class AllianceOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int a = ctx.Civ.KingdomOf(CityAt(ctx, cmd.From)), b = ctx.Civ.KingdomOf(CityAt(ctx, cmd.To));
                var meta = ctx.Meta;
                if (a < 0 || b < 0 || a == b || !meta.KingdomAlive(a) || !meta.KingdomAlive(b)) return;
                int war = meta.WarBetween(a, b);
                if (war >= 0) WarSystem.EndWar(ctx, meta, war, WarResult.Peace, ctx.Clock.Tick);
                KingdomSystem.Ally(ctx, meta, a, b, ctx.Clock.Tick);
            }
        }

        sealed class LoyaltyOp : IPointPowerOp
        {
            readonly int _amount;
            public LoyaltyOp(int amount) => _amount = amount;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int c = CityAt(ctx, cmd.To);
                if (c >= 0) ctx.Civ.City(c).LoyaltyMod = math.clamp(ctx.Civ.City(c).LoyaltyMod + _amount, -200, 200);
            }
        }

        sealed class CapitalOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int c = CityAt(ctx, cmd.To);
                int k = ctx.Civ.KingdomOf(c);
                if (c < 0 || !ctx.Meta.KingdomAlive(k)) return;
                ctx.Meta.Kingdom(k).Capital = c;
                ctx.Civ.Version++;
            }
        }

        sealed class MigrantsOp : IPointPowerOp
        {
            readonly int _count;
            public MigrantsOp(int count) => _count = count;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int c = CityAt(ctx, cmd.To);
                if (c >= 0) SpawnGroup(ctx, ctx.Civ.City(c).Species, CityPlanner.Below(ctx.Civ.City(c).Center), _count, c, ref rng);
            }
        }

        sealed class FestivalOp : IPointPowerOp
        {
            readonly int _event;
            public FestivalOp(int hap) => _event = hap;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                int c = CityAt(ctx, cmd.To);
                if (c < 0 || _event < 0) return;
                foreach (int i in ctx.Civ.CityResidents[c]) Happiness.Add(ctx.Units.Store, i, _event);
            }
        }

        sealed class CrownOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var u = ctx.Units.Store;
                int i = NearestUnit(u, cmd.To, 3f);
                int k = i >= 0 ? ctx.Civ.KingdomOf(u.City[i]) : -1;
                if (!ctx.Meta.KingdomAlive(k)) return;
                ref var d = ref ctx.Meta.Kingdom(k);
                d.King = u.IdOf(i);
                d.SuccessionTick = ctx.Clock.Tick;
                ctx.Events.Publish(new NewKingEvent(k, u.Uid[i]));
            }
        }

        // First click the unit (From), second click a city of another kingdom (To).
        sealed class RelocateOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var u = ctx.Units.Store;
                int i = NearestUnit(u, cmd.From, 3f);
                int c = CityAt(ctx, cmd.To);
                if (i < 0 || c < 0 || u.City[i] == c) return;
                u.City[i] = c;
                u.HomeBuilding[i] = -1;
                u.Job[i] = 0;
                u.Army[i] = -1;
                u.Task[i] = 0;
            }
        }

        sealed class InfectOp : IPointPowerOp
        {
            readonly int _disease;
            public InfectOp(int disease) => _disease = disease;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var u = ctx.Units.Store;
                bool any = false;
                for (int k = 0; k < u.Alive.Length; k++)
                    if (InBrush(cmd, u.Pos[u.Alive[k]])) any |= Diseases.Infect(ctx.Units, u.Alive[k], _disease, ctx.Clock.Tick);
                if (!any)
                {
                    int i = NearestUnit(u, cmd.To, 3f);
                    if (i >= 0) Diseases.Infect(ctx.Units, i, _disease, ctx.Clock.Tick);
                }
            }
        }

        // Held: up to max units hover (Z = 3) and follow the pointer; released when the commands stop (PowerWorldSystem).
        sealed class MagnetOp : IPointPowerOp
        {
            readonly int _max;
            public MagnetOp(int max) => _max = max;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var pw = ctx.Powers;
                var u = ctx.Units.Store;
                if (pw.Grabbed.Length == 0)
                    for (int k = 0; k < u.Alive.Length && pw.Grabbed.Length < _max; k++)
                    {
                        int i = u.Alive[k];
                        if (u.State[i] == UnitStore.StateAlive && InBrush(cmd, u.Pos[i])) pw.Grabbed.Add(u.IdOf(i));
                    }
                pw.MagnetTick = ctx.Clock.Tick;
                pw.MagnetPos = (float2)cmd.To + 0.5f;
            }
        }

        sealed class FingerOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var u = ctx.Units.Store;
                int i = NearestUnit(u, cmd.To, 3f);
                if (i < 0) return;
                float a = rng.Range(0f, 2f * math.PI), speed = rng.Range(4f, 8f);
                Explosions.Launch(u, i, 25f, new float2(math.cos(a), math.sin(a)) * speed);
            }
        }

        sealed class StormOp : IPointPowerOp
        {
            readonly int _lightning, _count;
            readonly float _radius;

            public StormOp(int lightning, int count, float radius)
            {
                _lightning = lightning;
                _count = count;
                _radius = radius;
            }

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                if (_lightning < 0) return;
                var map = ctx.World;
                var drops = ctx.Powers.Drops;
                for (int n = 0; n < _count && drops.Length < PowerWorld.MaxDrops; n++)
                {
                    float a = rng.Range(0f, 2f * math.PI), r = _radius * math.sqrt(rng.Value01());
                    float2 p = math.clamp((float2)cmd.To + 0.5f + new float2(math.cos(a), math.sin(a)) * r, 0.5f, new float2(map.Width - 0.5f, map.Height - 0.5f));
                    drops.Add(new DropData { Power = (ushort)_lightning, Pos = p, Z = DropSystemConst.StartZ + rng.Range(0f, 60f) });
                }
            }
        }

        sealed class QuakeOp : IPointPowerOp
        {
            readonly int _radius;
            public QuakeOp(int radius) => _radius = radius;
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng) => WorldObjects.Earthquake(ctx, cmd.To, _radius, ref rng);
        }

        sealed class FloodOp : IPointPowerOp
        {
            readonly int _radius;
            public FloodOp(int radius) => _radius = radius;
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng) => WorldObjects.Flood(ctx, cmd.To, _radius, WorldObjects.FloodTicks);
        }

        // --- area brushes ---

        sealed class FertileRainOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var map = ctx.World;
                int r = cmd.Brush.Radius;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cmd.To.x + dx, y = cmd.To.y + dy;
                        if (!InBrush(cmd, dx, dy) || !map.InBounds(x, y)) continue;
                        ctx.Nature.RainDrop(x, y, ref rng);
                        int i = map.Index(x, y);
                        if (map.Feature[i] != 0) { int s = map.GetFeatureStage(i); if (s < 3) map.SetFeatureStage(x, y, s + 1); }
                    }
            }
        }

        sealed class TempOp : IPointPowerOp
        {
            readonly int _delta;
            public TempOp(int delta) => _delta = delta;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng) =>
                WorldObjects.ShiftTemperature(ctx, cmd.To, math.max(WorldMap.ZoneSize, cmd.Brush.Radius), _delta, 12);
        }

        sealed class RaiseDeadOp : IPointPowerOp
        {
            readonly int _species;
            public RaiseDeadOp(int species) => _species = species;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                if (_species < 0) return;
                var u = ctx.Units.Store;
                var map = ctx.World;
                for (int k = u.Corpses.Length - 1; k >= 0; k--)
                {
                    var c = u.Corpses[k];
                    float2 p = new float2(c.Tile % map.Width, c.Tile / map.Width) + 0.5f;
                    if (!InBrush(cmd, p) || u.Count >= UnitWorld.HardUnitCap) continue;
                    ctx.Units.Spawn(new UnitSpawnRequest
                    {
                        Species = _species, Pos = p, AgeYears = 1f, Sex = 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                    }, ctx.Clock.Tick, ref rng);
                    u.Corpses.RemoveAtSwapBack(k);
                }
            }
        }

        sealed class LifeGameOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var pw = ctx.Powers;
                pw.EnsureAutomaton();
                var map = ctx.World;
                int r = math.max(1, cmd.Brush.Radius);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = cmd.To.x + dx, y = cmd.To.y + dy;
                        if (!Brushes.Contains(cmd.Brush.Shape, r, dx, dy) || !map.InBounds(x, y) || !rng.Chance(0.5f)) continue;
                        int i = map.Index(x, y);
                        if ((pw.Automaton[i] & 1) != 0 || pw.LifeCells >= PowerWorld.MaxLifeCells) continue;
                        pw.Automaton[i] |= 1;
                        pw.LifeCells++;
                        pw.LifeBox = new int4(math.min(pw.LifeBox.x, x), math.min(pw.LifeBox.y, y), math.max(pw.LifeBox.z, x), math.max(pw.LifeBox.w, y));
                    }
            }
        }

        sealed class CureOp : IPointPowerOp
        {
            public const float HolyDamage = 200f;
            readonly int _disease, _removeStatus, _addStatus;
            readonly bool _holy;

            public CureOp(int disease, int removeStatus, int addStatus, bool holy)
            {
                _disease = disease;
                _removeStatus = removeStatus;
                _addStatus = addStatus;
                _holy = holy;
            }

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var w = ctx.Units;
                var u = w.Store;
                long tick = ctx.Clock.Tick;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (u.State[i] != UnitStore.StateAlive || !InBrush(cmd, u.Pos[i])) continue;
                    if (_holy && u.Has(i, UnitFlags.Undead)) { w.Damage(i, HolyDamage, -1, DeathCause.Divine, tick); continue; }
                    if (_disease < 0 || u.Disease[i] - 1 == _disease) Diseases.Cure(w, i, tick, false);
                    if (_removeStatus >= 0) u.RemoveStatus(i, _removeStatus);
                    if (_addStatus >= 0) w.AddStatus(i, _addStatus);
                }
            }
        }

        // Dragged: 40 damage per application under the pointer, melts a level every 20 ticks, sets fires.
        sealed class HeatRayOp : IPointPowerOp
        {
            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var map = ctx.World;
                int x = cmd.To.x, y = cmd.To.y;
                long tick = ctx.Clock.Tick;
                if (tick % 20 < AreaInterval) map.LowerLevel(x, y, ChangeSource.Power);
                ctx.Nature.Ignite(x, y, NatureState.IgniteDefault, false, NatureState.IgnitePower);
                var u = ctx.Units.Store;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (math.distancesq(u.Pos[i], (float2)cmd.To + 0.5f) <= 2.25f) ctx.Units.Damage(i, 40f, -1, DeathCause.Burned, tick);
                }
                Explosions.DamageBuildings(ctx, (float2)cmd.To + 0.5f, 1.5f, 40f);
            }
        }

        sealed class DroughtOp : IPointPowerOp
        {
            readonly byte _sand;
            public DroughtOp(byte sand) => _sand = sand;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var map = ctx.World;
                int r = cmd.Brush.Radius;
                using (var b = map.BeginBatch(ChangeSource.Power))
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int x = cmd.To.x + dx, y = cmd.To.y + dy;
                            if (!InBrush(cmd, dx, dy) || !map.InBounds(x, y)) continue;
                            var f = map.GetFlags(x, y);
                            if ((f & TileFlags.Water) != 0 && (f & TileFlags.Walkable) != 0) b.SetGround(x, y, _sand);
                            int i = map.Index(x, y);
                            if (map.Feature[i] != 0 && map.Tables.FeatureKind[map.Feature[i]] == FeatureDef.KindPlant && rng.Chance(0.3f)) b.SetFeature(x, y, 0);
                        }
            }
        }

        // Zone brush: the city under the stroke start claims (or drops) the zones it passes.
        sealed class ZoneOp : IPointPowerOp
        {
            readonly bool _add;
            public ZoneOp(bool add) => _add = add;

            public void Apply(in SimContext ctx, in PowerCommand cmd, ref SimRandom rng)
            {
                var civ = ctx.Civ;
                var map = ctx.World;
                int city = CityAt(ctx, cmd.From);
                if (city < 0) return;
                int z = map.ZoneIndexOf(cmd.To.x, cmd.To.y);
                int owner = map.Zones[z].OwnerCity;
                if (_add && owner < 0) civ.ClaimZone(city, z);
                else if (!_add && owner >= 0 && civ.CityZones[owner].Count > 1) civ.ReleaseZone(owner, z);
            }
        }
    }
}
