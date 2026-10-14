using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 4.1 biological fields; traits live in MetaHeader.Traits (subspecies_traits.json indices).
    public struct SubspeciesData
    {
        public ushort Species;
        public int Parent;          // -1 = root
        public int PaletteRow;      // -1 = species row (phenotypes: Bölüm 4.5)
        public int OriginIsland;    // isolation tracking
        public int FoundedYear;
        public byte MonolithStage;  // 0-3
        public ushort Abilities;    // AbilityFlags, cached from traits
        public int Population;      // monthly cache
        public GeneGenome Genome;   // Bölüm 4.4
        public short Phenotype;     // phenotypes.json index, -1 = species colours (civilized species)
        public short OriginBiome;   // biome index at founding, -1 = unknown
        public byte Mutations;      // accumulated since the last split
        public FixedList64Bytes<int2> Isolation; // (island, years) for Bölüm 4.6
        public ulong DisabledNeurons; // Brain panel mask (Bölüm 3.8.2), bit = NeuronId
    }

    public sealed class SubspeciesStore : IDisposable
    {
        public const int MaxRandomTraits = 4;

        public readonly MetaStore<SubspeciesData> Meta = new MetaStore<SubspeciesData>(MetaKind.Subspecies);
        readonly ContentDB _content;
        readonly TraitSet[] _defaults;   // per species (Bölüm 4.1 derivation rules)
        readonly int[] _randomPool;      // traits with canAppearRandomly

        public SubspeciesStore(ContentDB content)
        {
            _content = content;
            _defaults = new TraitSet[content.Species.Count];
            for (int s = 0; s < content.Species.Count; s++) _defaults[s] = DefaultTraits(content, content.Species[s]);
            var pool = new List<int>();
            for (int t = 0; t < content.SubspeciesTraits.Count; t++)
                if (content.SubspeciesTraits[t].CanAppearRandomly) pool.Add(t);
            _randomPool = pool.ToArray();
        }

        public int Count => Meta.Count;
        public bool IsAlive(int s) => Meta.IsAlive(s);
        public ref SubspeciesData Data(int s) => ref Meta.Data(s);
        public ref MetaHeader Header(int s) => ref Meta.Header(s);
        public TraitSet Traits(int s) => Meta.Headers[s].Traits;
        public AbilityFlags Abilities(int s) => (AbilityFlags)Meta.Items[s].Abilities;

        public int CreateRoot(int species, int island, int biome, long tick, long founderUid, bool mutantBox, ref SimRandom rng)
        {
            int s = Meta.Create(new SubspeciesData
            {
                Species = (ushort)species,
                Parent = -1,
                PaletteRow = -1,
                OriginIsland = island,
                OriginBiome = (short)biome,
                FoundedYear = (int)(tick / SimConst.TicksPerYear),
                Genome = Genetics.Random(_content, ref rng),
                Phenotype = _content.Species[species].IsCiv ? (short)-1 : Genetics.PickPhenotype(_content, biome, ref rng),
            }, founderUid, tick);
            ref var h = ref Meta.Header(s);
            h.Traits = _defaults[species];
            h.Color = rng.NextUInt() | 0xFFu; // random opaque meta color
            if (mutantBox) AddRandomTraits(s, rng.Range(1, MaxRandomTraits + 1), ref rng);
            RefreshAbilities(s);
            return s;
        }

        // Child subspecies (isolation, biome fit, genome split): copies the parent's traits and genome; a new biome may recolour it.
        public int Branch(int parent, int island, int biome, long tick, long founderUid, bool mutantBox, ref SimRandom rng)
        {
            var p = Meta.Items[parent];
            var parentTraits = Meta.Headers[parent].Traits;
            uint color = Meta.Headers[parent].Color;
            int s = Meta.Create(new SubspeciesData
            {
                Species = p.Species,
                Parent = parent,
                PaletteRow = p.PaletteRow,
                OriginIsland = island,
                FoundedYear = (int)(tick / SimConst.TicksPerYear),
                MonolithStage = p.MonolithStage,
                Genome = p.Genome,
                OriginBiome = (short)biome,
                Phenotype = p.Phenotype < 0 || biome == p.OriginBiome ? p.Phenotype : Genetics.PickPhenotype(_content, biome, ref rng),
            }, founderUid, tick);
            ref var h = ref Meta.Header(s);
            h.Traits = parentTraits;
            h.Color = color;
            if (mutantBox) AddRandomTraits(s, rng.Range(1, MaxRandomTraits + 1), ref rng);
            RefreshAbilities(s);
            return s;
        }

        // Brain panel (Bölüm 3.8.2): switch a neuron off/on for the whole line; callers mark members' stats dirty.
        public void SetNeuron(int s, NeuronId n, bool enabled)
        {
            ref var d = ref Meta.Data(s);
            if (enabled) d.DisabledNeurons &= ~(1UL << (int)n);
            else d.DisabledNeurons |= 1UL << (int)n;
        }

        public void AddTrait(int s, int trait)
        {
            ref var h = ref Meta.Header(s);
            h.Traits.Set(trait, true);
            RefreshAbilities(s);
        }

        public void AddRandomTraits(int s, int n, ref SimRandom rng)
        {
            if (_randomPool.Length == 0) return;
            ref var h = ref Meta.Header(s);
            for (int k = 0; k < n; k++) h.Traits.Set(_randomPool[rng.Range(0, _randomPool.Length)], true);
            RefreshAbilities(s);
        }

        public int RandomTrait(ref SimRandom rng) => _randomPool.Length == 0 ? -1 : _randomPool[rng.Range(0, _randomPool.Length)];

        void RefreshAbilities(int s)
        {
            var traits = Meta.Headers[s].Traits;
            var a = AbilityFlags.None;
            for (int t = 0; t < _content.SubspeciesTraits.Count; t++)
                if (traits.Has(t)) a |= _content.SubspeciesTraits[t].Abilities;
            Meta.Data(s).Abilities = (ushort)a;
        }

        public void RefreshAllAbilities()
        {
            for (int s = 0; s < Meta.Count; s++) RefreshAbilities(s);
        }

        // Bölüm 4.1 default trait derivation (until species.json carries defaultSubspeciesTraits).
        static TraitSet DefaultTraits(ContentDB db, SpeciesDef sp)
        {
            var set = default(TraitSet);
            void Add(string id)
            {
                int t = db.SubspeciesTraits.IdOrDefault(id);
                if (t >= 0) set.Set(t, true);
            }
            switch (sp.Diet)
            {
                case Diet.Herb: Add("sst.herbivore"); break;
                case Diet.Carn: case Diet.Blood: Add("sst.carnivore"); break;
                case Diet.Omni: Add("sst.omnivore"); break;
                case Diet.Brains: Add("sst.brain_eater"); break;
            }
            switch (sp.Reproduction)
            {
                case Reproduction.Live: Add("sst.live_birth"); break;
                case Reproduction.Egg: Add("sst.egg_laying"); break;
                case Reproduction.Split: Add("sst.fission"); break;
                case Reproduction.Spore: Add("sst.spore_birth"); break;
            }
            switch (sp.Habitat)
            {
                case Habitat.Amph: Add("sst.amphibious"); break;
                case Habitat.Water: Add("sst.aquatic"); break;
                case Habitat.Air: Add("sst.flying"); break;
                case Habitat.Under: Add("sst.burrowing"); break;
            }
            int size = (int)Math.Round(sp.BaseStats[(int)StatId.Size]);
            switch (Math.Max(1, Math.Min(5, size)))
            {
                case 1: Add("sst.size_tiny"); break;
                case 2: Add("sst.size_small"); break;
                case 3: Add("sst.size_medium"); break;
                case 4: Add("sst.size_large"); break;
                default: Add("sst.size_huge"); break;
            }
            if (sp.IsCiv)
            {
                Add("sst.sapience"); Add("sst.tool_use"); Add("sst.speech_center"); Add("sst.advanced_memory");
                Add("sst.abstract_thought"); Add("sst.planning"); Add("sst.fire_makers");
            }
            return set;
        }

        public void Dispose() => Meta.Dispose();
    }
}
