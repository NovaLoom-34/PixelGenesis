using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    public readonly struct SubspeciesFoundedEvent : ISimEvent
    {
        public readonly int Subspecies, Parent;
        public readonly byte Cause; // 0 isolation, 1 biome, 2 genome split, 3 monolith
        public SubspeciesFoundedEvent(int subspecies, int parent, byte cause) { Subspecies = subspecies; Parent = parent; Cause = cause; }
    }

    public readonly struct MonolithEvolvedEvent : ISimEvent // stage 3: the species awakens (ach.evolution, Bölüm 8)
    {
        public readonly int Subspecies;
        public readonly ushort Species;
        public MonolithEvolvedEvent(int subspecies, ushort species) { Subspecies = subspecies; Species = species; }
    }

    // Bölüm 4.4 mutation and genome split, 4.6 isolation and biome fit (yearly), 4.7 monolith pulses (monthly check).
    public sealed class EvolutionSystem : ISimSystem
    {
        public const int IsolationMin = 20, IsolationYears = 100, SplitMutations = 5, SplitPop = 30, BiomeMin = 10, MaxIsolationEntries = 7;
        public const float IsolationChance = 0.3f, SplitChance = 0.2f, BiomeChance = 0.1f, ChaosFactor = 25f;
        public const int PulseYears = 20, MonolithRadius = 20;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 20;

        int _lawEvolution = -2, _lawPure, _lawUplift, _lawMutation, _lawChaos, _lawMutantBox, _eraGlimmer, _sstMutable, _sstStable, _sstTouched;
        int[] _members = new int[0], _irradiated = new int[0];
        bool[] _dirty = new bool[0];
        readonly Dictionary<long, int> _islands = new Dictionary<long, int>(), _biomes = new Dictionary<long, int>();
        readonly List<long> _keys = new List<long>();

        void Init(in SimContext ctx)
        {
            var laws = ctx.Nature.Laws;
            var db = ctx.Content;
            _lawEvolution = laws.IndexOf("law.evolution");
            _lawMutation = laws.IndexOf("law.mutation");
            _lawChaos = laws.IndexOf("law.gene_chaos");
            _lawMutantBox = laws.IndexOf("law.mutant_box");
            _lawPure = laws.IndexOf("law.pure_lines");
            _lawUplift = laws.IndexOf("law.uplift_all");
            _eraGlimmer = db.Eras.IdOrDefault("era.glimmer");
            _sstMutable = db.SubspeciesTraits.IdOrDefault("sst.mutable");
            _sstStable = db.SubspeciesTraits.IdOrDefault("sst.stable_genome");
            _sstTouched = db.SubspeciesTraits.IdOrDefault("sst.monolith_touched");
        }

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 9) return;
            if (_lawEvolution == -2) Init(ctx);
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            var subs = ctx.Units.Subspecies;
            if (_dirty.Length < subs.Count) { _dirty = new bool[subs.Count * 2]; _members = new int[subs.Count * 2]; _irradiated = new int[subs.Count * 2]; }
            System.Array.Clear(_dirty, 0, _dirty.Length);
            if (ctx.Powers != null) Monoliths(ctx, tick, ref rng);
            if (tick % SimConst.TicksPerYear == 9)
            {
                Census(ctx);
                Mutations(ctx, tick, ref rng);
                if (ctx.Nature.Laws.IsOn(_lawEvolution)) Isolation(ctx, tick, ref rng);
                if (ctx.Nature.Laws.IsOn(_lawUplift)) Uplift(ctx);
            }
            Refresh(ctx);
        }

        void Census(in SimContext ctx)
        {
            var u = ctx.Units.Store;
            var w = ctx.Units;
            int need = w.Subspecies.Count;
            if (_members.Length < need) { System.Array.Resize(ref _members, need * 2); System.Array.Resize(ref _irradiated, need * 2); }
            if (_dirty.Length < need) System.Array.Resize(ref _dirty, need * 2);
            System.Array.Clear(_members, 0, _members.Length);
            System.Array.Clear(_irradiated, 0, _irradiated.Length);
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int s = u.Subspecies[i];
                if (s < 0 || s >= _members.Length) continue;
                _members[s]++;
                if (w.StIrradiated >= 0 && u.HasStatus(i, w.StIrradiated)) _irradiated[s]++;
            }
        }

        // Bölüm 4.4: yearly 0.02 x multiplier per subspecies; more than 5 mutations at 30+ members may split it.
        void Mutations(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var laws = ctx.Nature.Laws;
            if (!laws.IsOn(_lawMutation) || laws.IsOn(_lawPure)) return;
            var db = ctx.Content;
            var subs = ctx.Units.Subspecies;
            float law = laws.Value(_lawMutation) * (laws.IsOn(_lawChaos) ? ChaosFactor : 1f) * (_eraGlimmer >= 0 && ctx.Nature.Era.CurrentEra == _eraGlimmer ? 1.5f : 1f);
            int count = subs.Count;
            for (int s = 0; s < count; s++)
            {
                if (!subs.IsAlive(s) || _members[s] == 0) continue;
                var traits = subs.Traits(s);
                float mul = law * (_sstMutable >= 0 && traits.Has(_sstMutable) ? 2f : 1f) * (_sstStable >= 0 && traits.Has(_sstStable) ? 0.2f : 1f)
                            * (_irradiated[s] * 5f / _members[s] + 1f);
                if (!rng.Chance(db.GeneRules.MutationPerBirth * mul)) continue;
                ref var d = ref subs.Data(s);
                Genetics.Mutate(db, ref d.Genome, ref rng);
                if (d.Mutations < 255) d.Mutations++;
                _dirty[s] = true;
                if (d.Mutations <= SplitMutations || _members[s] < SplitPop || !rng.Chance(SplitChance)) continue;
                int child = subs.Branch(s, d.OriginIsland, d.OriginBiome, tick, 0, laws.IsOn(_lawMutantBox), ref rng);
                subs.Data(s).Mutations = 0;
                subs.Data(child).Mutations = 0;
                Genetics.Mutate(db, ref subs.Data(child).Genome, ref rng); // the new line starts one step apart
                MoveHalf(ctx, s, child, ref rng);
                ctx.Events.Publish(new SubspeciesFoundedEvent(child, s, 2));
            }
        }

        void MoveHalf(in SimContext ctx, int from, int to, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.Subspecies[i] == from && rng.Chance(0.5f)) u.Subspecies[i] = to;
            }
            Mark(to);
        }

        void Mark(int s)
        {
            if (s >= _dirty.Length) System.Array.Resize(ref _dirty, s * 2 + 1);
            _dirty[s] = true;
        }

        static MoveClass ClassOf(ContentDB db, int species) => db.Species[species].Habitat == Habitat.Water ? MoveClass.Water : MoveClass.Land;

        // Bölüm 4.6: 20+ members on a foreign island for 100 years split off (30%); the biome-fit path branches wild lines settled
        // in a new biome (10% a year, DECISIONS #72).
        void Isolation(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            var subs = ctx.Units.Subspecies;
            var db = ctx.Content;
            var map = ctx.World;
            _islands.Clear();
            _biomes.Clear();
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int s = u.Subspecies[i];
                if (s < 0 || s >= subs.Count || _members[s] < IsolationMin) continue;
                int2 t = u.Tile(i);
                if (!map.InBounds(t.x, t.y)) continue;
                int island = ctx.Regions.IslandAt(t.x, t.y, ClassOf(db, u.Species[i]));
                long key = ((long)s << 32) | (uint)island;
                _islands.TryGetValue(key, out int n);
                _islands[key] = n + 1;
                int biome = map.GetBiome(t.x, t.y) - 1;
                if (biome < 0 || db.Species[u.Species[i]].IsCiv) continue;
                key = ((long)s << 32) | (uint)biome;
                _biomes.TryGetValue(key, out n);
                _biomes[key] = n + 1;
            }

            int count = subs.Count;
            for (int s = 0; s < count; s++)
            {
                if (!subs.IsAlive(s) || _members[s] < IsolationMin) continue;
                ref var d = ref subs.Data(s);
                // a foreign island with 20+ members adds a year; an emptied one is forgotten (lean years just do not count)
                for (int e = d.Isolation.Length - 1; e >= 0; e--)
                {
                    var entry = d.Isolation[e];
                    _islands.TryGetValue(((long)s << 32) | (uint)entry.x, out int n);
                    if (n == 0 || entry.x == d.OriginIsland) d.Isolation.RemoveAtSwapBack(e);
                    else if (n >= IsolationMin) d.Isolation[e] = new int2(entry.x, entry.y + 1);
                }
            }
            _keys.Clear();
            foreach (var kv in _islands) if (kv.Value >= IsolationMin) _keys.Add(kv.Key);
            _keys.Sort(); // dictionary order is not deterministic across runs
            foreach (long key in _keys)
            {
                int s = (int)(key >> 32), island = (int)(uint)key;
                ref var d = ref subs.Data(s);
                if (island == d.OriginIsland || island < 0) continue;
                bool known = false;
                for (int e = 0; e < d.Isolation.Length; e++) known |= d.Isolation[e].x == island;
                if (!known && d.Isolation.Length < MaxIsolationEntries) d.Isolation.Add(new int2(island, 1));
            }
            for (int s = 0; s < count; s++)
            {
                if (!subs.IsAlive(s) || _members[s] < IsolationMin) continue;
                for (int e = subs.Data(s).Isolation.Length - 1; e >= 0; e--)
                {
                    var entry = subs.Data(s).Isolation[e];
                    if (entry.y < IsolationYears || !rng.Chance(IsolationChance)) continue;
                    subs.Data(s).Isolation.RemoveAtSwapBack(e);
                    int child = SplitWhere(ctx, s, entry.x, -1, tick, rng.Range(1, 3), ref rng);
                    ctx.Events.Publish(new SubspeciesFoundedEvent(child, s, 0));
                }
            }

            _keys.Clear();
            foreach (var kv in _biomes) if (kv.Value >= BiomeMin) _keys.Add(kv.Key);
            _keys.Sort();
            foreach (long key in _keys)
            {
                int s = (int)(key >> 32), biome = (int)(uint)key;
                if (!subs.IsAlive(s) || biome == subs.Data(s).OriginBiome || subs.Data(s).OriginBiome < 0 || !rng.Chance(BiomeChance)) continue;
                int child = SplitWhere(ctx, s, -1, biome, tick, 1, ref rng);
                ctx.Events.Publish(new SubspeciesFoundedEvent(child, s, 1));
            }
        }

        // Members of s on an island (or in a biome) become a new branch with n random traits.
        int SplitWhere(in SimContext ctx, int s, int island, int biome, long tick, int traits, ref SimRandom rng)
        {
            var subs = ctx.Units.Subspecies;
            var u = ctx.Units.Store;
            var map = ctx.World;
            var db = ctx.Content;
            int2 site = new int2(-1);
            for (int k = 0; k < u.Alive.Length && site.x < 0; k++)
            {
                int i = u.Alive[k];
                if (u.Subspecies[i] != s) continue;
                int2 t = u.Tile(i);
                if (!map.InBounds(t.x, t.y)) continue;
                if (island >= 0 && ctx.Regions.IslandAt(t.x, t.y, ClassOf(db, u.Species[i])) == island || biome >= 0 && map.GetBiome(t.x, t.y) - 1 == biome) site = t;
            }
            int useBiome = biome >= 0 ? biome : site.x >= 0 ? map.GetBiome(site.x, site.y) - 1 : subs.Data(s).OriginBiome;
            int useIsland = island >= 0 ? island : subs.Data(s).OriginIsland;
            int child = subs.Branch(s, useIsland, useBiome, tick, 0, ctx.Nature.Laws.IsOn(_lawMutantBox), ref rng);
            subs.AddRandomTraits(child, traits, ref rng);
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.Subspecies[i] != s) continue;
                int2 t = u.Tile(i);
                if (!map.InBounds(t.x, t.y)) continue;
                bool there = island >= 0 ? ctx.Regions.IslandAt(t.x, t.y, ClassOf(db, u.Species[i])) == island : map.GetBiome(t.x, t.y) - 1 == biome;
                if (there) u.Subspecies[i] = child;
            }
            Mark(child);
            return child;
        }

        // Bölüm 4.7: every 20 years a monolith lifts each evolvable subspecies within 20 tiles one stage.
        void Monoliths(in SimContext ctx, long tick, ref SimRandom rng)
        {
            var objects = ctx.Powers.Objects;
            for (int k = 0; k < objects.Length; k++)
            {
                var o = objects[k];
                if (o.Kind != WorldObjectKind.Monolith || o.Dead != 0) continue;
                if (o.Age < (o.Stage + 1) * PulseYears * SimConst.TicksPerYear) continue;
                o.Stage++;
                objects[k] = o;
                Pulse(ctx, o.Pos, tick, ref rng);
            }
        }

        void Pulse(in SimContext ctx, float2 at, long tick, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            var subs = ctx.Units.Subspecies;
            var db = ctx.Content;
            int count = subs.Count;
            if (_members.Length < count) { System.Array.Resize(ref _members, count * 2); System.Array.Resize(ref _irradiated, count * 2); }
            System.Array.Clear(_members, 0, _members.Length);
            System.Array.Clear(_irradiated, 0, _irradiated.Length); // here: members inside the radius
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int s = u.Subspecies[i];
                if (s < 0 || s >= count) continue;
                _members[s]++;
                if (math.distancesq(u.Pos[i], at) <= MonolithRadius * MonolithRadius) _irradiated[s]++;
            }
            for (int s = 0; s < count; s++)
            {
                bool touched = _sstTouched >= 0 && subs.Traits(s).Has(_sstTouched) && _members[s] > 0; // a touched line keeps rising (DECISIONS #72)
                if (_irradiated[s] == 0 && !touched || !subs.IsAlive(s) || !db.Species[subs.Data(s).Species].CanEvolve || subs.Data(s).MonolithStage >= 3) continue;
                int target = s; // the whole line rises (Bölüm 4.7: every evolvable subspecies in the radius), DECISIONS #72
                ref var d = ref subs.Data(target);
                d.MonolithStage++;
                for (int r = 0; r < db.EvolutionRules.Count; r++)
                    if (db.EvolutionRules[r].Stage == d.MonolithStage)
                        foreach (int t in db.EvolutionRules[r].GrantTraits) subs.AddTrait(target, t);
                if (_sstTouched >= 0) subs.AddTrait(target, _sstTouched);
                Mark(target);
                if (subs.Data(target).MonolithStage == 3) ctx.Events.Publish(new MonolithEvolvedEvent(target, subs.Data(target).Species));
            }
        }

        // law.uplift_all: every living line of an evolvable species gets all monolith grants at once.
        void Uplift(in SimContext ctx)
        {
            var subs = ctx.Units.Subspecies;
            var db = ctx.Content;
            for (int s = 0; s < subs.Count; s++)
            {
                if (!subs.IsAlive(s) || subs.Data(s).MonolithStage >= 3 || !db.Species[subs.Data(s).Species].CanEvolve) continue;
                subs.Data(s).MonolithStage = 3;
                for (int r = 0; r < db.EvolutionRules.Count; r++) foreach (int t in db.EvolutionRules[r].GrantTraits) subs.AddTrait(s, t);
                Mark(s);
                ctx.Events.Publish(new MonolithEvolvedEvent(s, subs.Data(s).Species));
            }
        }

        void Refresh(in SimContext ctx)
        {
            bool any = false;
            for (int s = 0; s < _dirty.Length && !any; s++) any = _dirty[s];
            if (!any) return;
            var u = ctx.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                int s = u.Subspecies[i];
                if (s >= 0 && s < _dirty.Length && _dirty[s]) u.StatsDirty[i] = 1;
            }
        }
    }
}
