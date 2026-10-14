using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PG.Sim
{
    // Soft personal space (DECISIONS #109): units closer than the sum of their radii are nudged apart after movement,
    // so crowds spread out instead of collapsing onto one pixel. Pushes are computed from the positions left by the
    // movement systems and applied afterwards, so the result does not depend on unit order. No random stream is used.
    // Both steps run as Burst jobs (each unit only writes its own push / position); the walkability rules mirror
    // PathService.MobilityOf / CanStand.
    public sealed class UnitSpacingSystem : ISimSystem, IDisposable
    {
        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 8; // after UnitActSystem (0), before projectiles (10)

        public const float BaseRadius = 0.22f, SizeRadius = 0.06f, MaxPairDistance = 0.9f;
        public const float Stiffness = 0.35f, MaxStep = 0.06f, Epsilon = 1e-3f;

        const uint FixedFlags = (uint)(UnitFlags.Immobile | UnitFlags.PlayerControlled | UnitFlags.Aboard);

        NativeArray<float2> _push;
        NativeArray<byte> _waterSpecies; // species that live in water only (PathService.Mobility.Water)

        public void Tick(in SimContext ctx)
        {
            var w = ctx.Units;
            var u = w.Store;
            var index = w.Index;
            int high = u.HighWater;
            var alive = u.Alive.AsArray();
            if (high == 0 || alive.Length == 0) return;
            if (!_push.IsCreated || _push.Length < high)
            {
                Dispose();
                _push = new NativeArray<float2>(math.ceilpow2(high), Allocator.Persistent);
            }

            if (!_waterSpecies.IsCreated)
            {
                var species = ctx.Content.Species;
                _waterSpecies = new NativeArray<byte>(species.Count, Allocator.Persistent);
                for (int s = 0; s < species.Count; s++) _waterSpecies[s] = (byte)(species[s].Habitat == Habitat.Water ? 1 : 0);
            }

            // Stale stats are rare (a unit changed this tick); refresh them here so the job can read the table directly.
            for (int k = 0; k < alive.Length; k++)
                if (u.StatsDirty[alive[k]] != 0) u.Stat(alive[k], StatId.Size);

            new SpacingJob
            {
                Alive = alive, Pos = u.Pos, Uid = u.Uid, Z = u.Z, Flags = u.Flags, Stats = u.Stats, State = u.State, Task = u.Task,
                Generation = u.Generation, Target = u.Target, CellStart = index.CellStart, Sorted = index.Sorted,
                CellsX = index.CellsX, CellsY = index.CellsY, High = high, Push = _push,
            }.Schedule(alive.Length, 64).Complete();

            var map = w.Map;
            new ApplyJob
            {
                Alive = alive, Pos = u.Pos, Push = _push, Flags = u.Flags, Species = u.Species, WaterSpecies = _waterSpecies,
                TileBits = map.Flags, Width = map.Width, Height = map.Height,
            }.Schedule(alive.Length, 64).Complete();
        }

        [BurstCompile]
        struct SpacingJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> Alive;
            [ReadOnly] public NativeArray<float2> Pos;
            [ReadOnly] public NativeArray<long> Uid;
            [ReadOnly] public NativeArray<float> Z, Stats;
            [ReadOnly] public NativeArray<uint> Flags;
            [ReadOnly] public NativeArray<byte> State, Task;
            [ReadOnly] public NativeArray<int> Generation, CellStart, Sorted;
            [ReadOnly] public NativeArray<EntityId> Target;
            public int CellsX, CellsY, High;
            [NativeDisableParallelForRestriction] public NativeArray<float2> Push;

            public void Execute(int k)
            {
                int i = Alive[k];
                Push[i] = float2.zero;
                if ((Flags[i] & FixedFlags) != 0 || Z[i] > 0.05f) return;
                float2 pos = Pos[i];
                float ri = RadiusOf(i);
                bool flyer = (Flags[i] & (uint)UnitFlags.Fly) != 0;
                int x0 = math.max(0, (int)math.floor(pos.x - MaxPairDistance) >> WorldMap.ZoneShift);
                int y0 = math.max(0, (int)math.floor(pos.y - MaxPairDistance) >> WorldMap.ZoneShift);
                int x1 = math.min(CellsX - 1, (int)math.floor(pos.x + MaxPairDistance) >> WorldMap.ZoneShift);
                int y1 = math.min(CellsY - 1, (int)math.floor(pos.y + MaxPairDistance) >> WorldMap.ZoneShift);
                float2 push = float2.zero;
                for (int cy = y0; cy <= y1; cy++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        int cell = cy * CellsX + cx;
                        for (int s = CellStart[cell]; s < CellStart[cell + 1]; s++)
                        {
                            int j = Sorted[s];
                            if (j == i || j >= High || State[j] != UnitStore.StateAlive) continue;
                            if (Z[j] > 0.05f || ((Flags[j] & (uint)UnitFlags.Fly) != 0) != flyer) continue;
                            float2 d = pos - Pos[j];
                            float limit = math.min(MaxPairDistance, ri + RadiusOf(j));
                            float distSq = math.lengthsq(d);
                            if (distSq >= limit * limit || InContact(i, j)) continue;
                            float dist = math.sqrt(distSq);
                            float2 dir;
                            if (dist > Epsilon) dir = d / dist;
                            else
                            {
                                // Same spot: both units derive the same angle from the pair of ids and take opposite sides.
                                long a = Uid[i], b = Uid[j];
                                float angle = PairAngle(math.min(a, b), math.max(a, b));
                                dir = new float2(math.cos(angle), math.sin(angle));
                                if (a > b) dir = -dir;
                            }
                            push += dir * ((limit - dist) * Stiffness);
                        }
                    }
                float len = math.length(push);
                if (len > MaxStep) push *= MaxStep / len;
                Push[i] = push;
            }

            float RadiusOf(int i) => BaseRadius + SizeRadius * Stats[i * UnitStore.StatCount + (int)StatId.Size];

            // Attackers close in on their victim and courting pairs touch; neither pair may be pushed apart.
            bool InContact(int i, int j) => Contact(i, j) || Contact(j, i);

            bool Contact(int a, int b)
            {
                byte task = Task[a];
                if (task != (byte)UnitTask.Attack && task != (byte)UnitTask.Hunt && task != (byte)UnitTask.Mate) return false;
                EntityId t = Target[a];
                return t.Index == b && Generation[b] == t.Generation && State[b] == UnitStore.StateAlive;
            }
        }

        [BurstCompile]
        struct ApplyJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> Alive;
            [ReadOnly] public NativeArray<float2> Push;
            [ReadOnly] public NativeArray<uint> Flags;
            [ReadOnly] public NativeArray<ushort> Species, TileBits;
            [ReadOnly] public NativeArray<byte> WaterSpecies;
            public int Width, Height;
            [NativeDisableParallelForRestriction] public NativeArray<float2> Pos;

            public void Execute(int k)
            {
                int i = Alive[k];
                float2 push = Push[i];
                if (math.lengthsq(push) < 1e-8f) return;
                uint f = Flags[i];
                float2 pos = Pos[i], moved = pos + push;
                if ((f & (uint)UnitFlags.Fly) != 0) { Pos[i] = moved; return; }
                // Same classes as PathService.MobilityOf: 0 land, 1 water only, 2 amphibious.
                int mob = (f & (uint)UnitFlags.BreatheWater) != 0 && WaterSpecies[Species[i]] != 0 ? 1 : (f & (uint)UnitFlags.Swim) != 0 ? 2 : 0;
                if (CanStand(moved.x, moved.y, mob)) Pos[i] = moved;
                else if (CanStand(moved.x, pos.y, mob)) Pos[i] = new float2(moved.x, pos.y);
                else if (CanStand(pos.x, moved.y, mob)) Pos[i] = new float2(pos.x, moved.y);
            }

            bool CanStand(float px, float py, int mob)
            {
                int x = (int)math.floor(px), y = (int)math.floor(py);
                if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
                ushort t = TileBits[y * Width + x];
                if (mob == 1) return (t & (ushort)TileFlags.Water) != 0;
                if (mob == 2) return (t & (ushort)(TileFlags.Walkable | TileFlags.Water)) != 0;
                return (t & (ushort)TileFlags.Walkable) != 0;
            }
        }

        // Stateless 0..2pi angle from two ids (splitmix-style mixing).
        public static float PairAngle(long a, long b)
        {
            return (Mix((ulong)a * 0x9E3779B97F4A7C15UL ^ (ulong)b * 0xC2B2AE3D27D4EB4FUL) >> 40) * (6.2831853f / (1 << 24));
        }

        static ulong Mix(ulong x)
        {
            x ^= x >> 31; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 29;
            return x;
        }

        // Per-unit stable point inside a disc (DECISIONS #110): arrival spots and path wobble that differ between units
        // instead of everyone taking the exact centre of the same tile.
        public static float2 SlotOffset(long uid, float radius)
        {
            ulong h = Mix((ulong)uid * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL);
            float angle = (h >> 40) * (6.2831853f / (1 << 24));
            float r = math.sqrt((h & 0xFFFFFF) * (1f / (1 << 24))) * radius;
            return new float2(math.cos(angle), math.sin(angle)) * r;
        }

        // Per-unit stable value in [-1, 1] for a given purpose (flee spread, speed spread).
        public static float Spread(long uid, ulong salt)
        {
            return (Mix((ulong)uid * 0x9E3779B97F4A7C15UL ^ salt) >> 40) * (2f / (1 << 24)) - 1f;
        }

        public void Dispose()
        {
            if (_push.IsCreated) _push.Dispose();
            if (_waterSpecies.IsCreated) _waterSpecies.Dispose();
        }
    }
}
