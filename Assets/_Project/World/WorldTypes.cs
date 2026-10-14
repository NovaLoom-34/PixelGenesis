using System;
using PG.Core;

namespace PG.World
{
    [Flags]
    public enum DirtyMask : byte
    {
        None = 0,
        Render = 1,
        Regions = 2,
        Stats = 4,
        Save = 8,
        Overlay = 16,
        Features = 32, // feature sprites (FeatureRenderer)
        All = Render | Regions | Stats | Save | Overlay | Features,
    }

    public enum ChangeSource : byte { WorldGen, Power, Nature, Unit, Building, Load }

    public struct ZoneData
    {
        public int OwnerCity;       // -1 = unowned (Bölüm 5)
        public short TemperatureC;      // current, recomputed monthly (Bölüm 2.5)
        public short BaseTemperatureC;  // worldgen climate, saved
        public ushort LandTiles;        // stats cache
        public ushort WaterTiles;
        public byte DominantBiome;      // recomputed monthly
        public byte TreeCount;          // live feature counters (density caps, Bölüm 2.4)
        public byte PlantCount;
        public byte Moisture;           // persistent 0..255 zone moisture (D2)
        public byte DryMonths;          // consecutive dry months, saturated at 255
        public byte Drought;            // 1 after the dry-month threshold, 0 otherwise
        public float FoodBiomass;       // remaining edible feature yield in this zone; rebuilt from feature tiles
        public ushort ProtectedSpecies; // 0 = none; otherwise species registry index + 1
    }

    // Feature caps per 8x8 zone (Bölüm 2.4), shared by worldgen and FeatureGrowth.
    // Doc: 24 trees + 16 plants; with 5x7-tile tree sprites that hides the ground completely (DECISIONS #30).
    public static class FeatureDensity
    {
        public const int MaxTreesPerZone = 6, MaxPlantsPerZone = 5;
    }

    public struct ChunkData
    {
        public DirtyMask Dirty;
        public int FirstRegion;     // unused by RegionGraph (per-chunk lists); kept for the save/debug layout
        public int RegionCount;
    }

    public readonly struct TileChangedEvent : ISimEvent
    {
        public readonly int X, Y;
        public readonly byte Old, New;

        public TileChangedEvent(int x, int y, byte oldType, byte newType)
        {
            X = x;
            Y = y;
            Old = oldType;
            New = newType;
        }
    }

    public readonly struct ChunkDirtyEvent : ISimEvent
    {
        public readonly int ChunkIndex;
        public readonly DirtyMask Mask;

        public ChunkDirtyEvent(int chunkIndex, DirtyMask mask)
        {
            ChunkIndex = chunkIndex;
            Mask = mask;
        }
    }

    public readonly struct IslandsChangedEvent : ISimEvent
    {
        public readonly int OldCount, NewCount;

        public IslandsChangedEvent(int oldCount, int newCount)
        {
            OldCount = oldCount;
            NewCount = newCount;
        }
    }
}
