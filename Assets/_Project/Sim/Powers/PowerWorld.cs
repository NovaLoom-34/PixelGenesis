using System;
using System.IO;
using System.Runtime.InteropServices;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 7.2: pseudo-3D falling power objects; the Powers layer applies the effect on landing.
    public struct DropData
    {
        public ushort Power;
        public float2 Pos;
        public float Z, VZ;
        public int Delay;        // ticks after landing (dynamite fuse)
        public byte Child;       // cluster sub-drop
    }

    public enum WorldObjectKind : byte { Tornado, Volcano, Geyser, BlackHole, Ant, Monolith, Lure, Wave, Rift }

    // Bölüm 7.5 world objects (tornado, volcano, geyser, black hole, Langton ant, monolith, lure, tsunami front, demon rift).
    public struct WorldObject
    {
        public WorldObjectKind Kind;
        public float2 Pos, Dir;
        public int Ticks;        // remaining, -1 = permanent
        public int Age, Stage;
        public ushort Power;
        public byte Dead;
    }

    // Flood/tsunami water that recedes: the original ground comes back (Bölüm 7.5, 7.8 #7).
    public struct TempTile
    {
        public int Index;
        public byte Ground;
        public long RestoreTick;
    }

    // Bölüm 8.9 ach.cellular: a Life Game cell damaged a building in this city (correlated with a later CityRuinedEvent).
    public readonly struct AutomatonDamageEvent : ISimEvent
    {
        public readonly int City;
        public AutomatonDamageEvent(int city) { City = city; }
    }

    public readonly struct ExplosionEvent : ISimEvent
    {
        public readonly float2 Pos;
        public readonly float Radius;
        public ExplosionEvent(float2 pos, float radius) { Pos = pos; Radius = radius; }
    }

    public sealed class PowerWorld : IDisposable
    {
        public const int MaxDrops = 512, MaxAnts = 16, MaxLifeCells = 200000;

        public NativeList<DropData> Drops;
        public NativeList<WorldObject> Objects;
        public NativeList<TempTile> TempTiles;
        public NativeArray<byte> Automaton;      // per tile: bit0 life cell, bit1 ant flag (lazy)
        public int4 LifeBox = new int4(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue); // x0,y0,x1,y1
        public int LifeCells;
        public NativeList<EntityId> Grabbed;     // magnet (Bölüm 7.5)
        public long MagnetTick;
        public float2 MagnetPos;
        public NativeArray<sbyte> ZoneTempOffset; // heat/cold brush, ice storm, heat wave
        public NativeArray<byte> ZoneTempMonths;
        public NativeArray<int> CityBlightUntilYear; // dis.famine: crops x0.2 (index = city)
        readonly WorldMap _map;

        public PowerWorld(WorldMap map)
        {
            _map = map;
            Drops = new NativeList<DropData>(64, Allocator.Persistent);
            Objects = new NativeList<WorldObject>(16, Allocator.Persistent);
            TempTiles = new NativeList<TempTile>(256, Allocator.Persistent);
            Grabbed = new NativeList<EntityId>(64, Allocator.Persistent);
            ZoneTempOffset = new NativeArray<sbyte>(map.Zones.Length, Allocator.Persistent);
            ZoneTempMonths = new NativeArray<byte>(map.Zones.Length, Allocator.Persistent);
            CityBlightUntilYear = new NativeArray<int>(0, Allocator.Persistent);
        }

        public void EnsureAutomaton()
        {
            if (!Automaton.IsCreated) Automaton = new NativeArray<byte>(_map.TileCount, Allocator.Persistent);
        }

        public bool Blighted(int city, int year) => city >= 0 && city < CityBlightUntilYear.Length && CityBlightUntilYear[city] > year;

        public void Blight(int city, int untilYear)
        {
            if (city >= CityBlightUntilYear.Length)
            {
                var grown = new NativeArray<int>(math.max(city + 1, CityBlightUntilYear.Length * 2), Allocator.Persistent);
                NativeArray<int>.Copy(CityBlightUntilYear, grown, CityBlightUntilYear.Length);
                CityBlightUntilYear.Dispose();
                CityBlightUntilYear = grown;
            }
            CityBlightUntilYear[city] = math.max(CityBlightUntilYear[city], untilYear);
        }

        public void Write(BinaryWriter w)
        {
            WriteList(w, Drops);
            WriteList(w, Objects);
            WriteList(w, TempTiles);
            WriteList(w, Grabbed);
            w.Write(MagnetTick);
            w.Write(MagnetPos.x); w.Write(MagnetPos.y);
            w.Write(Automaton.IsCreated);
            if (Automaton.IsCreated) w.Write(Automaton.AsReadOnlySpan());
            w.Write(LifeBox.x); w.Write(LifeBox.y); w.Write(LifeBox.z); w.Write(LifeBox.w);
            w.Write(LifeCells);
            w.Write(MemoryMarshal.AsBytes(ZoneTempOffset.AsReadOnlySpan()));
            w.Write(ZoneTempMonths.AsReadOnlySpan());
            w.Write(CityBlightUntilYear.Length);
            w.Write(MemoryMarshal.AsBytes(CityBlightUntilYear.AsReadOnlySpan()));
        }

        public void Read(BinaryReader r)
        {
            ReadList(r, Drops);
            ReadList(r, Objects);
            ReadList(r, TempTiles);
            ReadList(r, Grabbed);
            MagnetTick = r.ReadInt64();
            MagnetPos = new float2(r.ReadSingle(), r.ReadSingle());
            if (r.ReadBoolean()) { EnsureAutomaton(); ReadExact(r, Automaton.AsSpan()); }
            LifeBox = new int4(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            LifeCells = r.ReadInt32();
            ReadExact(r, MemoryMarshal.AsBytes(ZoneTempOffset.AsSpan()));
            ReadExact(r, ZoneTempMonths.AsSpan());
            CityBlightUntilYear.Dispose();
            CityBlightUntilYear = new NativeArray<int>(r.ReadInt32(), Allocator.Persistent);
            ReadExact(r, MemoryMarshal.AsBytes(CityBlightUntilYear.AsSpan()));
        }

        static void WriteList<T>(BinaryWriter w, NativeList<T> list) where T : unmanaged
        {
            w.Write(list.Length);
            w.Write(MemoryMarshal.AsBytes(list.AsArray().AsReadOnlySpan()));
        }

        static void ReadList<T>(BinaryReader r, NativeList<T> list) where T : unmanaged
        {
            list.Resize(r.ReadInt32(), NativeArrayOptions.ClearMemory);
            ReadExact(r, MemoryMarshal.AsBytes(list.AsArray().AsSpan()));
        }

        static void ReadExact(BinaryReader r, Span<byte> bytes)
        {
            if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated power section");
        }

        public void Dispose()
        {
            if (Drops.IsCreated) Drops.Dispose();
            if (Objects.IsCreated) Objects.Dispose();
            if (TempTiles.IsCreated) TempTiles.Dispose();
            if (Grabbed.IsCreated) Grabbed.Dispose();
            if (Automaton.IsCreated) Automaton.Dispose();
            if (ZoneTempOffset.IsCreated) ZoneTempOffset.Dispose();
            if (ZoneTempMonths.IsCreated) ZoneTempMonths.Dispose();
            if (CityBlightUntilYear.IsCreated) CityBlightUntilYear.Dispose();
        }
    }

    public enum ExplosionSize : byte { Small, Medium, Huge, Void }

    public struct ExplosionSpec
    {
        public ExplosionSize Size;
        public float Radius, Damage, Knockback, FireChance;
        public bool Irradiate;
        public int CenterTile;       // tile type for the core (-1 = by size)

        // Bölüm 7.3 table
        public static ExplosionSpec Preset(ExplosionSize size)
        {
            switch (size)
            {
                case ExplosionSize.Medium: return new ExplosionSpec { Size = size, Radius = 10f, Damage = 400f, Knockback = 10f, FireChance = 0.4f, CenterTile = -1 };
                case ExplosionSize.Huge: return new ExplosionSpec { Size = size, Radius = 40f, Damage = 5000f, Knockback = 20f, FireChance = 0.6f, Irradiate = true, CenterTile = -1 };
                case ExplosionSize.Void: return new ExplosionSpec { Size = size, Radius = 60f, Damage = float.MaxValue, Knockback = 0f, FireChance = 0f, CenterTile = -1 };
                default: return new ExplosionSpec { Size = size, Radius = 4f, Damage = 120f, Knockback = 6f, FireChance = 0.3f, CenterTile = -1 };
            }
        }
    }

    public static class Explosions
    {
        // Bölüm 7.3: one pass over the tile disc, buildings by footprint center, units by spatial query.
        public static void Explode(in SimContext ctx, float2 center, in ExplosionSpec spec, ref SimRandom rng)
        {
            var map = ctx.World;
            var db = ctx.Content;
            float R = math.max(1f, spec.Radius);
            int r = (int)math.ceil(R);
            int2 c = (int2)math.floor(center);
            byte pit = (byte)db.Tiles.IdOrDefault("tile.pit", 0), scorched = (byte)db.Tiles.IdOrDefault("tile.scorched", 0);
            byte wasteland = (byte)db.Tiles.IdOrDefault("tile.wasteland", 0), deep = (byte)db.Tiles.IdOrDefault("tile.deep_ocean", 0);
            byte core = spec.CenterTile >= 0 ? (byte)spec.CenterTile : spec.Size == ExplosionSize.Huge ? wasteland : pit;
            using (var b = map.BeginBatch(ChangeSource.Power))
            {
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        float d = math.sqrt(dx * dx + dy * dy);
                        if (d > R) continue;
                        float f = 1f - d / R;
                        int i = map.Index(x, y);
                        if (spec.Size == ExplosionSize.Void)
                        {
                            b.SetFeature(x, y, 0);
                            b.SetBiome(x, y, 0);
                            b.SetGround(x, y, deep);
                            continue;
                        }
                        bool water = map.IsWater(x, y);
                        if (f > 0.66f && !water)
                        {
                            if (spec.Size == ExplosionSize.Small) { if (rng.Chance(0.5f)) b.SetGround(x, y, core); }
                            else
                            {
                                int steps = spec.Size == ExplosionSize.Huge ? 3 : 2;
                                for (int s = 0; s < steps; s++) b.LowerLevel(x, y);
                                if (spec.Size == ExplosionSize.Huge && !map.IsWater(x, y)) b.SetGround(x, y, core);
                            }
                            if (spec.Irradiate) b.SetFlag(x, y, TileFlags.Irradiated, true);
                        }
                        else if (!water && scorched != 0 && (spec.Size != ExplosionSize.Huge || f > 0.1f)) b.SetGround(x, y, scorched);
                        if (map.Feature[i] != 0)
                        {
                            if (f > 0.33f) b.SetFeature(x, y, 0);
                            else if (rng.Chance(spec.FireChance) && ctx.Nature.CanBurn(i)) ctx.Nature.Ignite(x, y, NatureState.IgniteDefault, false, NatureState.IgnitePower);
                        }
                        else if (f < 0.66f && rng.Chance(spec.FireChance * 0.1f) && ctx.Nature.CanBurn(i)) ctx.Nature.Ignite(x, y, NatureState.IgniteDefault, false, NatureState.IgnitePower);
                    }
            }
            DamageBuildings(ctx, center, R, spec.Size == ExplosionSize.Void ? float.MaxValue : spec.Damage * 2f);
            HitUnits(ctx, center, spec, ref rng);
            ctx.Events.Publish(new ExplosionEvent(center, R));
        }

        public static void DamageBuildings(in SimContext ctx, float2 center, float radius, float damage)
        {
            var civ = ctx.Civ;
            if (civ == null) return;
            for (int bi = 0; bi < civ.Buildings.Length; bi++)
            {
                var d = civ.Buildings[bi];
                if (!d.Alive || (d.Flags & BuildingFlags.Ruin) != 0) continue;
                float2 mid = (float2)d.Origin + new float2(d.W, d.H) * 0.5f;
                float dist = math.distance(mid, center);
                if (dist > radius + math.max((int)d.W, (int)d.H) * 0.5f) continue;
                float f = math.saturate(1f - dist / radius);
                d.Hp -= damage >= float.MaxValue ? d.Hp + 1f : damage * math.max(0.1f, f);
                civ.Buildings[bi] = d; // BuildingSystem ruins it at Hp <= 0
            }
        }

        static void HitUnits(in SimContext ctx, float2 center, in ExplosionSpec spec, ref SimRandom rng)
        {
            var w = ctx.Units;
            var u = w.Store;
            float R = spec.Radius;
            long tick = ctx.Clock.Tick;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                float2 delta = u.Pos[i] - center;
                float d = math.length(delta);
                if (d > R) continue;
                if (spec.Size == ExplosionSize.Void) { u.Kill(i, DeathCause.Void); continue; }
                float f = 1f - d / R;
                w.Damage(i, spec.Damage * f * f, -1, DeathCause.Explosion, tick);
                if (u.State[i] != UnitStore.StateAlive) continue;
                Launch(u, i, spec.Knockback * f, d > 0.01f ? delta / d * spec.Knockback * f : float2.zero);
                if (spec.Irradiate && w.StIrradiated >= 0) w.AddStatus(i, w.StIrradiated);
            }
        }

        // Bölüm 7.4: airborne units keep their horizontal speed until they land.
        public static void Launch(UnitStore u, int i, float vz, float2 vel)
        {
            u.VZ[i] += vz;
            if (u.Z[i] <= 0f) u.Z[i] = 0.01f;
            u.Vel[i] += vel;
        }
    }
}
