using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace PG.Content
{
    // BiomeEffectRegistry (Bölüm 2.3): behaviour codes known to the simulation. Unit-facing codes are read by later chapters.
    public enum BiomeEffect : byte
    {
        None, RandomFire, DiseaseBoost, Evaporate, OreBoost, Cold, Happy, Luck, Spores, ManaBoost, Healing,
        Corruption, Repel, Holy, NoPlants, Aging, ManaVoid, FishBoost, LavaBubble, NightSkeleton, FoodBoost,
    }

    public sealed class BiomeDef : ContentDef
    {
        public bool Special { get; set; }
        public int GrowStrength { get; set; }
        public float Temp { get; set; }
        public float Moisture { get; set; }
        public string[] Trees { get; set; }
        public string[] Plants { get; set; }
        public string[] Animals { get; set; }
        public string[] UnitTraitPool { get; set; }
        public string[] CulturePool { get; set; }
        public string[] LanguagePool { get; set; }
        public string[] ReligionPool { get; set; }
        public string Effect { get; set; }
        public string[] GroundColors { get; set; }
        public string Description { get; set; }
        public string EffectCode { get; set; }   // behaviour code (Bölüm 2.3 table), "" = none
        public int TempOffset { get; set; }      // zone temperature offset, °C
        public bool SnowCover { get; set; }      // snow stays while cold
        public bool WaterOnly { get; set; }      // only on water tiles (coral)
        public bool Fireproof { get; set; }      // tiles of this biome never burn

        // Value stored in WorldMap.Biome (0 = no biome).
        [JsonIgnore] public byte MapValue => (byte)(Index + 1);
        [JsonIgnore] public Color32[] ParsedColors { get; private set; }
        [JsonIgnore] public ushort[] TreeValues { get; private set; }  // feature map values
        [JsonIgnore] public ushort[] PlantValues { get; private set; }
        [JsonIgnore] public BiomeEffect EffectKind { get; private set; }
        [JsonIgnore] public int[] AnimalSpecies { get; private set; } = System.Array.Empty<int>();
        [JsonIgnore] public int[] UnitTraits { get; private set; } = System.Array.Empty<int>(); // UnitTraitDef indices
        [JsonIgnore] public int[] CultureTraits { get; private set; } = System.Array.Empty<int>(); // Bölüm 6.1 creation pools
        [JsonIgnore] public int[] LanguageTraits { get; private set; } = System.Array.Empty<int>();
        [JsonIgnore] public int[] ReligionTraits { get; private set; } = System.Array.Empty<int>();

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            ParsedColors = HexColor.ParseVariants(GroundColors, Id, errors);
            TreeValues = Features(db, Trees, errors);
            PlantValues = Features(db, Plants, errors);
            var animals = new List<int>();
            foreach (var a in Animals ?? System.Array.Empty<string>())
            {
                int sp = db.Species.IdOrDefault(a);
                if (sp >= 0) animals.Add(sp);
                else errors.Add($"{Id}: unknown animal '{a}'");
            }
            AnimalSpecies = animals.ToArray();
            var pool = new List<int>();
            foreach (var t in UnitTraitPool ?? System.Array.Empty<string>())
            {
                int id = db.UnitTraits.IdOrDefault(t);
                if (id >= 0) pool.Add(id);
                else errors.Add($"{Id}: unknown unit trait '{t}'");
            }
            UnitTraits = pool.ToArray();
            CultureTraits = Pool(db.CultureTraits, CulturePool, errors);
            LanguageTraits = Pool(db.LanguageTraits, LanguagePool, errors);
            ReligionTraits = Pool(db.ReligionTraits, ReligionPool, errors);
            if (string.IsNullOrEmpty(EffectCode)) EffectKind = BiomeEffect.None;
            else if (System.Enum.TryParse(EffectCode, out BiomeEffect e)) EffectKind = e;
            else errors.Add($"{Id}: unknown effectCode '{EffectCode}' (mods may only reuse existing codes)");
        }

        int[] Pool(Registry<MetaTraitDef> registry, string[] ids, List<string> errors)
        {
            var list = new List<int>();
            foreach (var t in ids ?? System.Array.Empty<string>())
            {
                int id = registry.IdOrDefault(t);
                if (id >= 0) list.Add(id);
                else errors.Add($"{Id}: unknown pool trait '{t}'");
            }
            return list.ToArray();
        }

        ushort[] Features(ContentDB db, string[] ids, List<string> errors)
        {
            if (ids == null) return System.Array.Empty<ushort>();
            var result = new List<ushort>(ids.Length);
            foreach (var id in ids)
            {
                if (db.Features.TryGet(id, out var f)) result.Add(f.MapValue);
                else errors.Add($"{Id}: unknown feature '{id}'");
            }
            return result.ToArray();
        }
    }
}
