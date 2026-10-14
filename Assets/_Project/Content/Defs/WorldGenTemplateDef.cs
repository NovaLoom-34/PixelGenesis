using System.Collections.Generic;
using Newtonsoft.Json;

namespace PG.Content
{
    public sealed class WorldGenTemplateDef : ContentDef
    {
        public static readonly string[] MaskTypes = { "none", "radial", "multiBlob", "annulus", "inverse", "full", "empty", "image" };

        public sealed class MaskDef
        {
            public string Type { get; set; } = "none";
            public int[] Count { get; set; } = { 1, 1 };
            public float[] Radius { get; set; } = { 0.4f, 0.4f };
            public float FalloffPower { get; set; } = 2f;
            public float Width { get; set; } = 0.12f;
        }

        public sealed class NoiseDef
        {
            public int Octaves { get; set; } = 6;
            public float FrequencyMul { get; set; } = 1f;
            public float Lacunarity { get; set; } = 2f;
            public float Gain { get; set; } = 0.5f;
            public float RidgeStrength { get; set; }
            public float ErosionStrength { get; set; } = 0.3f;
        }

        public sealed class WarpDef
        {
            public float Amplitude { get; set; } = 0.08f;
            public float FrequencyMul { get; set; } = 2f;
        }

        public MaskDef Mask { get; set; } = new MaskDef();
        public NoiseDef Noise { get; set; } = new NoiseDef();
        public WarpDef Warp { get; set; } = new WarpDef();
        public float EdgeOcean { get; set; } = 0.06f;
        public float? LandRatioDefault { get; set; }
        public float? MountainRatioDefault { get; set; }
        public float? HillRatioDefault { get; set; }
        public float? RiverDensityDefault { get; set; }
        public float? MoistureDefault { get; set; }
        public string FlatTile { get; set; }   // "full"/"empty" masks: tile that fills the map
        public string FlatBiome { get; set; }

        [JsonIgnore] public int FlatTileId { get; private set; } = -1;
        [JsonIgnore] public int FlatBiomeId { get; private set; } = -1;

        internal override void Resolve(ContentDB db, List<string> errors)
        {
            if (System.Array.IndexOf(MaskTypes, Mask?.Type) < 0) errors.Add($"{Id}: unknown mask type '{Mask?.Type}'");
            if (Warp == null) errors.Add($"{Id}.warp: expected an object");
            else
            {
                ValidateRange("warp.amplitude", Warp.Amplitude, 0f, 0.25f, errors);
                ValidateRange("warp.frequencyMul", Warp.FrequencyMul, 0.1f, 8f, errors);
            }
            if (Noise != null && (Noise.RidgeStrength < 0f || Noise.RidgeStrength > 1f))
                errors.Add($"{Id}.noise.ridgeStrength: expected a value between 0 and 1");
            if (Noise != null && (Noise.ErosionStrength < 0f || Noise.ErosionStrength > 1f))
                errors.Add($"{Id}.noise.erosionStrength: expected a value between 0 and 1");
            ValidateDefault("landRatioDefault", LandRatioDefault, 0.10f, 0.85f, errors);
            ValidateDefault("mountainRatioDefault", MountainRatioDefault, 0f, 0.25f, errors);
            ValidateDefault("hillRatioDefault", HillRatioDefault, 0f, 0.30f, errors);
            ValidateDefault("riverDensityDefault", RiverDensityDefault, 0f, 1f, errors);
            ValidateDefault("moistureDefault", MoistureDefault, 0f, 1f, errors);
            if (!string.IsNullOrEmpty(FlatTile))
            {
                if (db.Tiles.TryGet(FlatTile, out var t)) FlatTileId = t.Index;
                else errors.Add($"{Id}.flatTile: unknown tile '{FlatTile}'");
            }
            if (!string.IsNullOrEmpty(FlatBiome))
            {
                if (db.Biomes.TryGet(FlatBiome, out var b)) FlatBiomeId = b.Index;
                else errors.Add($"{Id}.flatBiome: unknown biome '{FlatBiome}'");
            }
            if ((Mask?.Type == "full" || Mask?.Type == "empty") && FlatTileId < 0)
                errors.Add($"{Id}: mask '{Mask.Type}' needs flatTile");
        }

        void ValidateDefault(string field, float? value, float min, float max, List<string> errors)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value) || value.Value < min || value.Value > max))
                errors.Add($"{Id}.{field}: expected a finite value between {min} and {max}");
        }

        void ValidateRange(string field, float value, float min, float max, List<string> errors)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                errors.Add($"{Id}.{field}: expected a finite value between {min} and {max}");
        }
    }
}
