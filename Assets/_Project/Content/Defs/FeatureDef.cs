using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PG.Content
{
    public sealed class FeatureDef : ContentDef
    {
        public string Kind { get; set; }                  // tree, plant, crop, ore
        public Dictionary<string, int> Yields { get; set; }
        public bool Burnable { get; set; }
        public float VeinWeight { get; set; }             // worldgen ore vein weight (0 = never)
        public float[] VeinLevelWeights { get; set; }      // optional multiplier for relief levels 0..8
        public string[] VeinBiomes { get; set; }           // optional biome allowlist; empty means any biome
        public string LandmassResource { get; set; }       // optional resource group guaranteed on large landmasses
        public bool Aquatic { get; set; }                 // lives on water tiles (coral, reed)
        public string BurnsInto { get; set; }             // what fire leaves behind ("" = nothing)
        public string Note { get; set; }

        public const byte KindNone = 0, KindTree = 1, KindPlant = 2, KindCrop = 3, KindOre = 4;

        // Value stored in WorldMap.Feature (0 = no feature).
        [JsonIgnore] public ushort MapValue => (ushort)(Index + 1);
        [JsonIgnore] public ushort BurnsIntoValue { get; private set; }
        [JsonIgnore] public byte KindCode { get; private set; }
        [JsonIgnore] public int TotalYield { get; private set; }
        [JsonIgnore] byte[] _veinBiomeAllowed;

        public float VeinWeightAtLevel(int level)
        {
            if (VeinLevelWeights == null || VeinLevelWeights.Length == 0) return 1f;
            return (uint)level < (uint)VeinLevelWeights.Length ? VeinLevelWeights[level] : 0f;
        }

        public bool AllowsVeinInBiome(byte biomeMapValue) =>
            VeinBiomes == null || VeinBiomes.Length == 0 ||
            (biomeMapValue > 0 && _veinBiomeAllowed != null && biomeMapValue - 1 < _veinBiomeAllowed.Length && _veinBiomeAllowed[biomeMapValue - 1] != 0);

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            KindCode = Kind == "tree" ? KindTree : Kind == "plant" ? KindPlant : Kind == "crop" ? KindCrop : Kind == "ore" ? KindOre : KindNone;
            if (KindCode == KindNone) errors.Add($"{Id}: unknown kind '{Kind}'");
            if (float.IsNaN(VeinWeight) || float.IsInfinity(VeinWeight) || VeinWeight < 0f)
                errors.Add($"{Id}.veinWeight: expected a finite non-negative value");
            if (!string.IsNullOrEmpty(LandmassResource) && KindCode != KindOre && KindCode != KindTree)
                errors.Add($"{Id}.landmassResource: only ore and tree features can provide a landmass resource");
            if (VeinLevelWeights != null && VeinLevelWeights.Length > 0)
            {
                if (VeinLevelWeights.Length != 9) errors.Add($"{Id}.veinLevelWeights: expected 9 values for relief levels 0..8");
                for (int i = 0; i < VeinLevelWeights.Length; i++)
                    if (float.IsNaN(VeinLevelWeights[i]) || float.IsInfinity(VeinLevelWeights[i]) || VeinLevelWeights[i] < 0f)
                        errors.Add($"{Id}.veinLevelWeights[{i}]: expected a finite non-negative value");
            }
            if (VeinBiomes != null && VeinBiomes.Length > 0)
            {
                _veinBiomeAllowed = new byte[db.Biomes.Count];
                for (int i = 0; i < VeinBiomes.Length; i++)
                {
                    if (db.Biomes.TryGet(VeinBiomes[i], out var biome)) _veinBiomeAllowed[biome.Index] = 1;
                    else errors.Add($"{Id}.veinBiomes: unknown biome '{VeinBiomes[i]}'");
                }
            }
            int total = 0;
            if (Yields != null)
                foreach (var y in Yields.Values) total += y;
            TotalYield = total;
            if (!string.IsNullOrEmpty(BurnsInto))
            {
                if (db.Features.TryGet(BurnsInto, out var f)) BurnsIntoValue = f.MapValue;
                else errors.Add($"{Id}.burnsInto: unknown feature '{BurnsInto}'");
            }
        }
    }
}
