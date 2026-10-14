using System;
using System.Text;
using PG.Content;
using UnityEngine;

namespace PG.WorldGen
{
    public enum MapSizePreset { Tiny, Small, Medium, Large, Huge, Giant, Titanic }

    public static class MapSizes
    {
        static readonly int[] Tiles = { 128, 192, 256, 384, 512, 768, 1024 };
        static readonly string[] Keys = { "ui.size.tiny", "ui.size.small", "ui.size.medium", "ui.size.large", "ui.size.huge", "ui.size.giant", "ui.size.titanic" };

        public static int Count => Tiles.Length;
        public static int TilesFor(MapSizePreset preset) => Tiles[(int)preset];
        public static string LocKey(MapSizePreset preset) => Keys[(int)preset];
    }

    [Serializable]
    public sealed class WorldGenSettings
    {
        public ulong Seed;                          // 0 = random (resolved by the generator)
        public MapSizePreset Size = MapSizePreset.Medium;
        public string Template = "wgt.continents";  // worldgen_templates.json
        public float LandRatio = 0.45f;             // 0.10-0.85 land / total target
        public float MountainRatio = 0.08f;         // 0.00-0.25 share of land that is mountain
        public float HillRatio = 0.12f;             // 0.00-0.30
        public float Roughness = 0.50f;             // 0-1 coastline warp strength
        public float ForestDensity = 0.50f;         // 0-1 (Bölüm 2)
        public float BiomeVariety = 0.50f;          // 0-1 how many special biome patches
        public float OreDensity = 0.50f;            // 0-1
        public float Temperature = 0.50f;           // 0 cold - 1 hot (global shift)
        public float Moisture = 0.50f;
        public float RiverDensity = 0.63f;           // 0 = lakes only, 1 = dense walkable river network
        public bool SpawnAnimals = true;            // filled in Bölüm 3
        public bool SpawnCivs = true;
        public string ScenarioId = "";             // optional challenge selected from scenarios.json
        public string ImagePath;                    // wgt.custom_image source PNG
        public int PreviewDivisor = 1;              // 4 = quarter-resolution preview

        public const float DefaultLandRatio = 0.45f;

        public WorldGenSettings Clone() => (WorldGenSettings)MemberwiseClone();

        // A template resets land share and applies only its declared terrain/climate defaults.
        public void ApplyTemplateDefaults(WorldGenTemplateDef template)
        {
            Template = template.Id;
            LandRatio = template.LandRatioDefault ?? DefaultLandRatio;
            if (template.MountainRatioDefault.HasValue) MountainRatio = template.MountainRatioDefault.Value;
            if (template.HillRatioDefault.HasValue) HillRatio = template.HillRatioDefault.Value;
            if (template.RiverDensityDefault.HasValue) RiverDensity = template.RiverDensityDefault.Value;
            if (template.MoistureDefault.HasValue) Moisture = template.MoistureDefault.Value;
        }

        public void Clamp()
        {
            if ((uint)Size >= (uint)MapSizes.Count) Size = MapSizePreset.Medium;
            LandRatio = Math.Clamp(LandRatio, 0.10f, 0.85f);
            MountainRatio = Math.Clamp(MountainRatio, 0f, 0.25f);
            HillRatio = Math.Clamp(HillRatio, 0f, 0.30f);
            Roughness = Math.Clamp(Roughness, 0f, 1f);
            ForestDensity = Math.Clamp(ForestDensity, 0f, 1f);
            BiomeVariety = Math.Clamp(BiomeVariety, 0f, 1f);
            OreDensity = Math.Clamp(OreDensity, 0f, 1f);
            Temperature = Math.Clamp(Temperature, 0f, 1f);
            Moisture = Math.Clamp(Moisture, 0f, 1f);
            RiverDensity = Math.Clamp(RiverDensity, 0f, 1f);
            if (PreviewDivisor < 1) PreviewDivisor = 1;
        }
    }

    // A save keeps the portable world-generation recipe. Image-backed recipes are
    // intentionally excluded because the selected PNG is a local file, not save data.
    public static class WorldGenSettingsCodec
    {
        const int CurrentVersion = 1;

        [Serializable]
        sealed class Envelope
        {
            public int Version;
            public WorldGenSettings Settings;
        }

        public static byte[] Encode(WorldGenSettings source)
        {
            if (source == null || source.Seed == 0 || string.IsNullOrWhiteSpace(source.Template) || source.Template == "wgt.custom_image")
                return null;

            var settings = source.Clone();
            settings.ImagePath = null;
            settings.PreviewDivisor = 1;
            settings.Clamp();
            string json = JsonUtility.ToJson(new Envelope { Version = CurrentVersion, Settings = settings });
            return Encoding.UTF8.GetBytes(json);
        }

        public static bool TryDecode(byte[] raw, out WorldGenSettings settings)
        {
            settings = null;
            if (raw == null || raw.Length == 0) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(Encoding.UTF8.GetString(raw));
                if (envelope == null || envelope.Version != CurrentVersion || envelope.Settings == null ||
                    envelope.Settings.Seed == 0 || string.IsNullOrWhiteSpace(envelope.Settings.Template) ||
                    envelope.Settings.Template == "wgt.custom_image") return false;

                settings = envelope.Settings;
                settings.ImagePath = null;
                settings.PreviewDivisor = 1;
                settings.Clamp();
                return true;
            }
            catch { return false; }
        }
    }
}
