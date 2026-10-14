using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Powers
{
    public static class PowerSystems
    {
        // Every sim that takes player or disaster commands: the queue, the Bölüm 7 world objects, temperature offsets.
        public static PowerCommandSystem Install(SimWorld sim, PowerCommandQueue queue, bool disasters)
        {
            var commands = new PowerCommandSystem(queue, sim.Content);
            sim.Pipeline.Add(commands);
            sim.Pipeline.Add(new PowerWorldSystem(sim.Content));
            sim.Pipeline.Add(new PowerClimateSystem());
            if (disasters) new DisasterExecutor(sim.Events, queue, sim.Content, sim.Clock); // lives as long as the sim's event bus
            return commands;
        }
    }

    // Bölüm 7.2-7.6: falling drops, world objects, receding water, cellular automata, magnet.
    public sealed class PowerWorldSystem : ISimSystem
    {
        public const float TornadoSpeed = 0.15f, TornadoRadius = 4f, BlackHoleRadius = 25f;
        public const int GeyserEvery = 300, LifeEvery = 4, WaveEvery = 4, WaveHalfWidth = 20, WaveTicks = 300;

        readonly IDropEffect[] _effects;
        readonly ushort _obsidian;
        readonly byte _lava, _pit, _shallow;
        readonly int _ash, _imp;

        public PowerWorldSystem(ContentDB db)
        {
            _effects = new IDropEffect[db.Powers.Count];
            for (int p = 0; p < _effects.Length; p++) _effects[p] = DropEffects.Create(db.Powers[p], db);
            _obsidian = db.Features.TryGet("feat.ore_obsidian", out var f) ? f.MapValue : (ushort)0;
            _lava = (byte)db.Tiles.IdOrDefault("tile.lava_hot", 0);
            _pit = (byte)db.Tiles.IdOrDefault("tile.pit", 0);
            _shallow = (byte)db.Tiles.IdOrDefault("tile.shallow", 0);
            _ash = db.Clouds.IdOrDefault("cloud.ash");
            _imp = db.Species.IdOrDefault("sp.ember_imp");
        }

        public SimPhase Phase => SimPhase.World;
        public int Order => 50;

        public void Tick(in SimContext ctx)
        {
            var pw = ctx.Powers;
            ref var rng = ref ctx.Rng.Get(RngStream.Powers);
            long tick = ctx.Clock.Tick;
            Drops(ctx, pw, ref rng);
            Objects(ctx, pw, tick, ref rng);
            if (tick % 10 == 0 && pw.TempTiles.Length > 0) Recede(ctx, pw, tick);
            if (pw.LifeCells > 0 && tick % LifeEvery == 0) Life(ctx, pw);
            if (pw.Grabbed.Length > 0) Magnet(ctx, pw, tick);
        }

        void Drops(in SimContext ctx, PowerWorld pw, ref SimRandom rng)
        {
            var drops = pw.Drops;
            float dt = SimConst.TickDt;
            for (int k = drops.Length - 1; k >= 0; k--)
            {
                var d = drops[k];
                if (d.Z > 0f)
                {
                    d.VZ -= DropSystemConst.Gravity * dt;
                    d.Z = math.max(0f, d.Z + d.VZ * dt);
                    drops[k] = d;
                    if (d.Z > 0f) continue;
                }
                if (d.Delay > 0) { d.Delay--; drops[k] = d; continue; } // fuse
                drops.RemoveAtSwapBack(k);
                var effect = d.Power < _effects.Length ? _effects[d.Power] : null;
                effect?.Land(ctx, ref d, ref rng);
            }
        }

        void Objects(in SimContext ctx, PowerWorld pw, long tick, ref SimRandom rng)
        {
            var objects = pw.Objects;
            for (int k = objects.Length - 1; k >= 0; k--)
            {
                var o = objects[k];
                o.Age++;
                switch (o.Kind)
                {
                    case WorldObjectKind.Tornado: Tornado(ctx, ref o, ref rng); break;
                    case WorldObjectKind.Volcano: Volcano(ctx, ref o, ref rng); break;
                    case WorldObjectKind.Geyser: if (o.Age % GeyserEvery == 0) Geyser(ctx, o); break;
                    case WorldObjectKind.BlackHole: BlackHole(ctx, pw, ref o, ref rng); break;
                    case WorldObjectKind.Ant: Ant(ctx, pw, ref o); break;
                    case WorldObjectKind.Wave: if (o.Age % WaveEvery == 0) Wave(ctx, pw, ref o, tick); break;
                    case WorldObjectKind.Lure: if (o.Age % SimConst.TicksPerMonth == 0) Lure(ctx, o); break;
                    case WorldObjectKind.Rift: if (o.Age % (SimConst.TicksPerMonth * 6) == 1) SpawnImps(ctx, o.Pos, rng.Range(3, 9), ref rng); break;
                }
                if (o.Ticks > 0 && --o.Ticks == 0) o.Dead = 1;
                if (o.Dead != 0) { Finish(ctx, o, ref rng); objects.RemoveAtSwapBack(k); }
                else objects[k] = o;
            }
        }

        void Finish(in SimContext ctx, in WorldObject o, ref SimRandom rng)
        {
            if (o.Kind != WorldObjectKind.Volcano || _obsidian == 0) return;
            var map = ctx.World;
            for (int n = 0; n < 3; n++)
            {
                int x = (int)o.Pos.x + rng.Range(-3, 4), y = (int)o.Pos.y + rng.Range(-3, 4);
                if (map.InBounds(x, y) && !map.IsWater(x, y)) map.SetFeature(x, y, _obsidian, ChangeSource.Power);
            }
        }

        // random walk leaning 60% with the wind, lifts and flings units, uproots trees, batters buildings
        void Tornado(in SimContext ctx, ref WorldObject o, ref SimRandom rng)
        {
            var map = ctx.World;
            if (o.Age % 20 == 0)
            {
                float a = rng.Range(0f, 2f * math.PI);
                float2 wind = new float2(math.cos(ctx.Nature.WindAngle), math.sin(ctx.Nature.WindAngle));
                o.Dir = math.normalizesafe(wind * 0.6f + new float2(math.cos(a), math.sin(a)) * 0.4f);
            }
            o.Pos = math.clamp(o.Pos + o.Dir * TornadoSpeed, 0.5f, new float2(map.Width - 0.5f, map.Height - 0.5f));
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                float2 d = u.Pos[i] - o.Pos;
                float dist = math.length(d);
                if (dist > TornadoRadius || u.Z[i] > 3f || u.State[i] != UnitStore.StateAlive) continue;
                float2 out1 = dist > 0.01f ? d / dist : new float2(1f, 0f);
                u.VZ[i] = math.max(u.VZ[i], 12f);
                if (u.Z[i] <= 0f) u.Z[i] = 0.01f;
                u.Vel[i] = out1 * 6f + new float2(-out1.y, out1.x) * 4f;
            }
            if (o.Age % 5 == 0)
            {
                int2 c = (int2)math.floor(o.Pos);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        ushort f = map.Feature[map.Index(x, y)];
                        if (f != 0 && map.Tables.FeatureKind[f] == FeatureDef.KindTree) map.SetFeature(x, y, 0, ChangeSource.Power);
                    }
            }
            Explosions.DamageBuildings(ctx, o.Pos, TornadoRadius, 10f);
        }

        // Stage 0: the ground rises every 3 ticks toward a summit; stage 1: lava bursts every 10 ticks and ash.
        void Volcano(in SimContext ctx, ref WorldObject o, ref SimRandom rng)
        {
            var map = ctx.World;
            int2 c = (int2)math.floor(o.Pos);
            if (!map.InBounds(c.x, c.y)) { o.Dead = 1; return; }
            if (o.Stage == 0)
            {
                o.Ticks++; // the eruption timer waits for the mountain
                if (o.Age % 3 != 0) return;
                for (int n = 0; n < 5; n++)
                {
                    int x = c.x + (n == 1 ? 1 : n == 2 ? -1 : 0), y = c.y + (n == 3 ? 1 : n == 4 ? -1 : 0);
                    if (map.InBounds(x, y)) map.RaiseLevel(x, y, ChangeSource.Power);
                }
                if (map.GetLevel(c.x, c.y) >= ctx.Content.MaxLevel || o.Age > 90)
                {
                    o.Stage = 1;
                    if (_ash >= 0) ctx.Nature.SpawnCloud(_ash, o.Pos, ref rng);
                }
                return;
            }
            if (o.Age % 10 != 0) return;
            int lx = c.x + rng.Range(-5, 6), ly = c.y + rng.Range(-5, 6);
            if (map.InBounds(lx, ly) && !map.IsWater(lx, ly)) map.SetGround(lx, ly, _lava, ChangeSource.Power);
        }

        static void Geyser(in SimContext ctx, in WorldObject o)
        {
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (math.distancesq(u.Pos[i], o.Pos) <= 4f) Explosions.Launch(u, i, 15f, float2.zero);
            }
            var map = ctx.World;
            int2 c = (int2)math.floor(o.Pos);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (map.InBounds(c.x + dx, c.y + dy)) ctx.Nature.Douse(c.x + dx, c.y + dy);
        }

        // pull ~ 1/d, the core erases (DeathCause.Void), the ground caves into pits from the center outward
        void BlackHole(in SimContext ctx, PowerWorld pw, ref WorldObject o, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                float2 d = o.Pos - u.Pos[i];
                float dist = math.length(d);
                if (dist > BlackHoleRadius) continue;
                if (dist < 1f) { u.Kill(i, DeathCause.Void); continue; }
                u.Pos[i] += d / dist * math.min(dist, 3f / dist + 0.05f);
            }
            var drops = pw.Drops;
            for (int k = drops.Length - 1; k >= 0; k--)
            {
                var dr = drops[k];
                float2 d = o.Pos - dr.Pos;
                float dist = math.length(d);
                if (dist > BlackHoleRadius) continue;
                if (dist < 1f) { drops.RemoveAtSwapBack(k); continue; }
                dr.Pos += d / dist * math.min(dist, 3f / dist + 0.05f);
                drops[k] = dr;
            }
            if (o.Age % 10 != 0) return;
            var map = ctx.World;
            float r = math.max(2f, BlackHoleRadius * o.Age / 400f);
            for (int n = 0; n < 20; n++)
            {
                float a = rng.Range(0f, 2f * math.PI), rr = r * math.sqrt(rng.Value01());
                int x = (int)(o.Pos.x + math.cos(a) * rr), y = (int)(o.Pos.y + math.sin(a) * rr);
                if (map.InBounds(x, y) && !map.IsWater(x, y)) { map.SetFeature(x, y, 0, ChangeSource.Power); map.SetGround(x, y, _pit, ChangeSource.Power); }
            }
        }

        // Langton: off -> turn right, on -> turn left; flip the cell; step forward.
        static void Ant(in SimContext ctx, PowerWorld pw, ref WorldObject o)
        {
            var map = ctx.World;
            int2 t = (int2)o.Pos;
            if (!map.InBounds(t.x, t.y) || !pw.Automaton.IsCreated) { o.Dead = 1; return; }
            int i = map.Index(t.x, t.y);
            bool on = (pw.Automaton[i] & 2) != 0;
            o.Dir = on ? new float2(-o.Dir.y, o.Dir.x) : new float2(o.Dir.y, -o.Dir.x);
            pw.Automaton[i] ^= 2;
            o.Pos += o.Dir;
        }

        void Wave(in SimContext ctx, PowerWorld pw, ref WorldObject o, long tick)
        {
            var map = ctx.World;
            o.Stage++;
            float2 front = o.Pos + o.Dir * o.Stage;
            float2 side = new float2(-o.Dir.y, o.Dir.x);
            int hits = 0;
            for (int s = -WaveHalfWidth; s <= WaveHalfWidth; s++)
            {
                int2 t = (int2)math.floor(front + side * s);
                if (!map.InBounds(t.x, t.y) || map.IsWater(t.x, t.y) || map.GetLevel(t.x, t.y) > 5) continue;
                WorldObjects.Submerge(ctx, pw, t, _shallow, tick + WaveTicks);
                hits++;
                if ((s & 3) == 0) Explosions.DamageBuildings(ctx, (float2)t + 0.5f, 2f, 75f);
            }
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                float2 d = u.Pos[i] - o.Pos;
                float along = math.dot(d, o.Dir), across = math.abs(math.dot(d, side));
                if (along < o.Stage - 1.5f || along > o.Stage + 0.5f || across > WaveHalfWidth) continue;
                Explosions.Launch(u, i, 3f, o.Dir * 6f);
            }
            if (o.Stage > 80 || o.Stage > 12 && hits == 0) o.Dead = 1;
        }

        static void Lure(in SimContext ctx, in WorldObject o)
        {
            var u = ctx.Units.Store;
            int2 dest = (int2)math.floor(o.Pos);
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.City[i] < 0 && u.SpeciesOf(i).Diet == Diet.Brains && math.distancesq(u.Pos[i], o.Pos) < 80f * 80f) u.Dest[i] = dest;
            }
        }

        public void SpawnImps(in SimContext ctx, float2 at, int count, ref SimRandom rng)
        {
            if (_imp < 0) return;
            SpawnSpread.Begin();
            for (int n = 0; n < count && ctx.Units.Store.Count < UnitWorld.HardUnitCap; n++)
                ctx.Units.Spawn(new UnitSpawnRequest
                {
                    Species = _imp, Pos = SpawnSpread.Next(ctx.Units, Mobility.Fly, at, count, 3f, ref rng), AgeYears = 5f, Sex = -1,
                    Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                }, ctx.Clock.Tick, ref rng);
        }

        // flood and tsunami water gives the original ground back
        static void Recede(in SimContext ctx, PowerWorld pw, long tick)
        {
            var map = ctx.World;
            var list = pw.TempTiles;
            using (var b = map.BeginBatch(ChangeSource.Power))
                for (int k = list.Length - 1; k >= 0; k--)
                {
                    var t = list[k];
                    if (t.RestoreTick > tick) continue;
                    int x = t.Index % map.Width, y = t.Index / map.Width;
                    if (map.IsWater(x, y)) b.SetGround(x, y, t.Ground);
                    list.RemoveAtSwapBack(k);
                }
        }

        // Conway B3/S23 inside the live box; live cells hurt units (5) and buildings (2) under them.
        static void Life(in SimContext ctx, PowerWorld pw)
        {
            var map = ctx.World;
            var cells = pw.Automaton;
            int x0 = math.max(0, pw.LifeBox.x - 1), y0 = math.max(0, pw.LifeBox.y - 1);
            int x1 = math.min(map.Width - 1, pw.LifeBox.z + 1), y1 = math.min(map.Height - 1, pw.LifeBox.w + 1);
            if (x1 < x0 || y1 < y0) { pw.LifeCells = 0; return; }
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var next = new NativeArray<byte>(w * h, Allocator.Temp);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < map.Width && ny < map.Height && (cells[ny * map.Width + nx] & 1) != 0) n++;
                        }
                    bool alive = (cells[y * map.Width + x] & 1) != 0;
                    next[(y - y0) * w + (x - x0)] = (byte)(n == 3 || alive && n == 2 ? 1 : 0);
                }
            int count = 0;
            var box = new int4(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            var civ = ctx.Civ;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = y * map.Width + x;
                    bool alive = next[(y - y0) * w + (x - x0)] != 0 && count < PowerWorld.MaxLifeCells;
                    cells[i] = (byte)((cells[i] & ~1) | (alive ? 1 : 0));
                    if (!alive) continue;
                    count++;
                    box = new int4(math.min(box.x, x), math.min(box.y, y), math.max(box.z, x), math.max(box.w, y));
                    int b = map.Building[i];
                    if (civ != null && b >= 0 && b < civ.Buildings.Length)
                    {
                        var d = civ.Buildings[b];
                        d.Hp -= 2f;
                        civ.Buildings[b] = d;
                        if (d.City >= 0) ctx.Events.Publish(new AutomatonDamageEvent(d.City));
                    }
                }
            next.Dispose();
            pw.LifeCells = count;
            pw.LifeBox = box;
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int2 t = u.Tile(i);
                if (map.InBounds(t.x, t.y) && (cells[map.Index(t.x, t.y)] & 1) != 0) ctx.Units.Damage(i, 5f, -1, DeathCause.Divine, ctx.Clock.Tick);
            }
        }

        static void Magnet(in SimContext ctx, PowerWorld pw, long tick)
        {
            var u = ctx.Units.Store;
            if (tick - pw.MagnetTick > PowerHandlers.MagnetInterval + 1) { pw.Grabbed.Clear(); return; } // released: they fall
            for (int k = 0; k < pw.Grabbed.Length; k++)
            {
                var id = pw.Grabbed[k];
                if (!u.IsAlive(id)) continue;
                int i = id.Index;
                float2 target = pw.MagnetPos + new float2((k % 7 - 3) * 0.4f, (k / 7 - 3) * 0.4f);
                u.Pos[i] = math.lerp(u.Pos[i], target, 0.3f);
                u.Z[i] = 3f;
                u.VZ[i] = 0f;
                u.Vel[i] = float2.zero;
            }
        }
    }

    // Bölüm 7.5 one-shot handlers shared by powers and disasters.
    public static class WorldObjects
    {
        public const int FloodTicks = 600;
        static readonly List<int2> _crack = new List<int2>(128);

        public static void ForUnits(in SimContext ctx, float2 center, float radius, Action<UnitWorld, int, float> apply)
        {
            var w = ctx.Units;
            var u = w.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                float d = math.distance(u.Pos[i], center);
                if (d <= radius && u.State[i] == UnitStore.StateAlive) apply(w, i, 1f - d / math.max(0.01f, radius));
            }
        }

        public static void Submerge(in SimContext ctx, PowerWorld pw, int2 t, byte water, long restoreTick)
        {
            var map = ctx.World;
            int i = map.Index(t.x, t.y);
            byte ground = map.Ground[i];
            ushort f = map.Feature[i];
            bool lostCrop = ground == ctx.Civ.TileField || f != 0 && map.Tables.FeatureKind[f] == FeatureDef.KindCrop;
            if (lostCrop) ctx.Civ.RecordFloodCropLoss(t.x, t.y, restoreTick);
            if (ground == ctx.Civ.TileField) ground = (byte)ctx.Content.TileByLevel[4]; // fields are lost
            pw.TempTiles.Add(new TempTile { Index = i, Ground = ground, RestoreTick = restoreTick });
            if (f != 0 && map.Tables.FeatureKind[f] == FeatureDef.KindCrop) map.SetFeature(t.x, t.y, 0, ChangeSource.Power);
            map.SetGround(t.x, t.y, water, ChangeSource.Power);
        }

        // low soil and sand (level <= 4) within the radius turn shallow for a while
        public static void Flood(in SimContext ctx, int2 at, int radius, int ticks)
        {
            var map = ctx.World;
            byte shallow = (byte)ctx.Content.Tiles.IdOrDefault("tile.shallow", 0);
            long until = ctx.Clock.Tick + ticks;
            using (map.BeginBatch(ChangeSource.Power))
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = at.x + dx, y = at.y + dy;
                        if (dx * dx + dy * dy > radius * radius || !map.InBounds(x, y) || map.IsWater(x, y)) continue;
                        int level = map.GetLevel(x, y);
                        if (level > 4 || level < 0 && map.Ground[map.Index(x, y)] != ctx.Civ.TileField) continue;
                        Submerge(ctx, ctx.Powers, new int2(x, y), shallow, until);
                    }
        }

        // two random-walk cracks from the point; levels slip, buildings near the line take 150, units are stunned
        public static void Earthquake(in SimContext ctx, int2 at, int radius, ref SimRandom rng)
        {
            var map = ctx.World;
            _crack.Clear();
            int length = rng.Range(radius, radius * 2 + 1) / 2;
            float angle = rng.Range(0f, 2f * math.PI);
            using (map.BeginBatch(ChangeSource.Power))
                for (int side = 0; side < 2; side++)
                {
                    float a = angle + side * math.PI;
                    float2 p = (float2)at + 0.5f;
                    for (int s = 0; s < length; s++)
                    {
                        a += rng.Range(-0.4f, 0.4f);
                        p += new float2(math.cos(a), math.sin(a));
                        int2 t = (int2)math.floor(p);
                        if (!map.InBounds(t.x, t.y)) break;
                        _crack.Add(t);
                        float roll = rng.Value01();
                        if (roll < 0.5f) map.LowerLevel(t.x, t.y, ChangeSource.Power);
                        else if (roll < 0.7f) map.RaiseLevel(t.x, t.y, ChangeSource.Power);
                    }
                }
            var civ = ctx.Civ;
            for (int b = 0; b < civ.Buildings.Length; b++)
            {
                var d = civ.Buildings[b];
                if (!d.Alive || (d.Flags & BuildingFlags.Ruin) != 0 || !NearCrack((float2)d.Origin + new float2(d.W, d.H) * 0.5f)) continue;
                d.Hp -= 150f;
                civ.Buildings[b] = d;
            }
            var w = ctx.Units;
            var u = w.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (w.StStunned >= 0 && NearCrack(u.Pos[i])) u.AddStatus(i, w.StStunned, 40);
            }
            ctx.Events.Publish(new ExplosionEvent((float2)at + 0.5f, radius)); // camera shake
        }

        static bool NearCrack(float2 p)
        {
            for (int k = 0; k < _crack.Count; k += 2)
                if (math.distancesq(p, (float2)_crack[k] + 0.5f) <= 64f) return true;
            return false;
        }

        // the wave starts at the nearest deep water and runs toward the clicked shore
        public static bool StartWave(in SimContext ctx, int2 target, ref WorldObject o)
        {
            var map = ctx.World;
            for (int r = 2; r <= 96; r += 2)
                for (int n = 0; n < 32; n++)
                {
                    float a = n * (2f * math.PI / 32f);
                    int x = target.x + (int)(math.cos(a) * r), y = target.y + (int)(math.sin(a) * r);
                    if (!map.InBounds(x, y) || !map.HasFlag(x, y, TileFlags.DeepWater)) continue;
                    o.Pos = new float2(x + 0.5f, y + 0.5f);
                    o.Dir = math.normalizesafe((float2)target - (float2)new int2(x, y), new float2(1f, 0f));
                    o.Ticks = -1;
                    return true;
                }
            return false;
        }

        // zone temperature shift that fades after the given months (heat/cold brushes, ice storm, heat wave)
        public static void ShiftTemperature(in SimContext ctx, int2 at, int radius, int delta, int months)
        {
            var map = ctx.World;
            var pw = ctx.Powers;
            int zr = radius / WorldMap.ZoneSize + 1;
            int czx = at.x >> WorldMap.ZoneShift, czy = at.y >> WorldMap.ZoneShift;
            for (int zy = czy - zr; zy <= czy + zr; zy++)
                for (int zx = czx - zr; zx <= czx + zr; zx++)
                {
                    if (zx < 0 || zy < 0 || zx >= map.ZonesX || zy >= map.ZonesY || (zx - czx) * (zx - czx) + (zy - czy) * (zy - czy) > zr * zr) continue;
                    int z = zy * map.ZonesX + zx;
                    var zone = map.Zones[z];
                    zone.TemperatureC = (short)(zone.TemperatureC - pw.ZoneTempOffset[z] + delta);
                    map.Zones[z] = zone;
                    pw.ZoneTempOffset[z] = (sbyte)math.clamp(delta, -100, 100);
                    pw.ZoneTempMonths[z] = (byte)months;
                }
        }

        public static void Locusts(in SimContext ctx, int2 at, int radius)
        {
            var map = ctx.World;
            byte soil = (byte)ctx.Content.TileByLevel[4];
            using (var b = map.BeginBatch(ChangeSource.Nature))
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = at.x + dx, y = at.y + dy;
                        if (dx * dx + dy * dy > radius * radius || !map.InBounds(x, y)) continue;
                        int i = map.Index(x, y);
                        ushort f = map.Feature[i];
                        if (f != 0)
                        {
                            byte kind = map.Tables.FeatureKind[f];
                            if (kind == FeatureDef.KindCrop || kind == FeatureDef.KindPlant) b.SetFeature(x, y, 0);
                        }
                        if (map.Ground[i] == ctx.Civ.TileField) b.SetGround(x, y, soil);
                    }
        }
    }

    // Bölüm 7.5 temperature offsets survive the monthly climate recompute until they fade.
    public sealed class PowerClimateSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Climate;
        public int Order => 1;

        public void Tick(in SimContext ctx)
        {
            if (!ctx.Clock.IsMonthStart) return;
            var pw = ctx.Powers;
            var map = ctx.World;
            for (int z = 0; z < pw.ZoneTempMonths.Length; z++)
            {
                if (pw.ZoneTempMonths[z] == 0) continue;
                var zone = map.Zones[z];
                zone.TemperatureC += pw.ZoneTempOffset[z];
                map.Zones[z] = zone;
                if (--pw.ZoneTempMonths[z] == 0) pw.ZoneTempOffset[z] = 0;
            }
        }
    }

    // Bölüm 7.7: the Sim picks a disaster and a tile, this runs it.
    public static class DisasterHandlers
    {
        public const int DisasterBrushRadius = 8;
        static PowerWorldSystem _imps;

        public static void Apply(in SimContext ctx, int disaster, in PowerCommand cmd, IPointPowerOp[] pointOps, ITileBrushOp[] brushOps, ref SimRandom rng)
        {
            var db = ctx.Content;
            if (disaster < 0 || disaster >= db.Disasters.Count) return;
            var def = db.Disasters[disaster];
            int year = ctx.Clock.Year;
            int city = PowerHandlers.CityAt(ctx, cmd.To);
            switch (def.Id)
            {
                case "dis.famine":
                    if (city >= 0) ctx.Powers.Blight(city, year + rng.Range(3, 7));
                    return;
                case "dis.locusts": WorldObjects.Locusts(ctx, cmd.To, 60); return;
                case "dis.ice_storm": WorldObjects.ShiftTemperature(ctx, cmd.To, 40, -25, 12); break;
                case "dis.heat_wave": WorldObjects.ShiftTemperature(ctx, cmd.To, 40, 25, 12); return;
                case "dis.plague":
                    if (city >= 0 && db.Diseases.TryGet("dis_plague", out var plague))
                    {
                        var residents = ctx.Civ.CityResidents[city];
                        for (int n = 0; n < 5 && residents.Count > 0; n++) Diseases.Infect(ctx.Units, residents[rng.Range(0, residents.Count)], plague.Index, ctx.Clock.Tick);
                    }
                    return;
                case "dis.demon_raid":
                    ctx.Powers.Objects.Add(new WorldObject { Kind = WorldObjectKind.Rift, Pos = (float2)cmd.To + 0.5f, Ticks = 3 * SimConst.TicksPerYear });
                    if (_imps == null) _imps = new PowerWorldSystem(db);
                    _imps.SpawnImps(ctx, (float2)cmd.To + 0.5f, rng.Range(10, 31), ref rng);
                    return;
            }
            int power = def.PowerIndex;
            if (power < 0 || power >= pointOps.Length) return;
            var at = new PowerCommand { PowerId = (ushort)power, From = cmd.To, To = cmd.To, Brush = new BrushSpec(BrushShape.Circle, DisasterBrushRadius), Tick = cmd.Tick };
            if (pointOps[power] != null) pointOps[power].Apply(ctx, at, ref rng);
            else if (brushOps[power] != null) BrushExecutor.Stroke(ctx.World, at.Brush, at.From, at.To, brushOps[power], ref rng);
        }
    }
}
