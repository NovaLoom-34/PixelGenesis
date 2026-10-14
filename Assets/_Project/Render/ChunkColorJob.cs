using PG.Content;
using PG.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Render
{
    // Bölüm 1.11.2: pixel color per tile for a batch of chunks (4096 pixels each).
    // Alpha carries the shader surface code: 0 plain land, 32 sand, 64 grass,
    // 96 rock, 128 water, 192 snow, 224 river, 255 lava. Details fade at distant zoom.
    [BurstCompile]
    public struct ChunkColorJob : IJobParallelFor
    {
        public const int PixelsPerChunk = WorldMap.ChunkSize * WorldMap.ChunkSize;

        public int Width, Height, ChunksX;
        public bool ShowRegions;
        public Color32 Snow, Road;

        [ReadOnly] public NativeArray<int> ChunkList;
        [ReadOnly] public NativeArray<byte> Ground, Biome, Variant, Fire;
        [ReadOnly] public NativeArray<ushort> Flags;
        [ReadOnly] public NativeArray<Color32> TileColors;   // tileCount * 4
        [ReadOnly] public NativeArray<Color32> BiomeColors;  // (biomeCount + 1) * 4, slot 0 unused
        [ReadOnly] public NativeArray<byte> GrassBiome;
        [ReadOnly] public NativeArray<byte> NaturalGreenBiome;
        [ReadOnly] public NativeArray<Color32> FirePalette;  // 4
        [ReadOnly] public NativeArray<byte> ReliefLevel, Material;
        [ReadOnly] public NativeArray<sbyte> Level;
        [ReadOnly] public NativeArray<float> ReliefShade;
        [ReadOnly] public NativeArray<int> LandRegion, WaterRegion, IslandOf;

        [WriteOnly] public NativeArray<Color32> Pixels;

        public void Execute(int p)
        {
            int k = p >> 12, local = p & (PixelsPerChunk - 1);
            int chunk = ChunkList[k];
            int x = (chunk % ChunksX) * WorldMap.ChunkSize + (local & 63);
            int y = (chunk / ChunksX) * WorldMap.ChunkSize + (local >> 6);
            int i = y * Width + x;

            byte g = Ground[i];
            int v = Variant[i] & 3;
            byte b = Biome[i];
            ushort f = Flags[i];
            var c = ToF(b != 0 ? BiomeColors[b * 4 + v] : TileColors[g * 4 + v]);
            byte material = Material[g];
            bool grass = material == TileTables.MaterialLand && Level[g] >= 4 && Level[g] <= 6 &&
                         (b == 0 ? Level[g] <= 5 : GrassBiome[b] != 0);
            bool sand = material == TileTables.MaterialLand && Level[g] == 3;
            bool rock = material == TileTables.MaterialLand && (Level[g] == 7 || (Level[g] == 6 && !grass));
            bool summit = material == TileTables.MaterialLand && Level[g] == 8;
            bool water = material == TileTables.MaterialWater;
            bool river = material == TileTables.MaterialLand && (f & (ushort)TileFlags.River) != 0;
            bool floodplain = material == TileTables.MaterialLand && Level[g] >= 4 && Level[g] <= 6 && !river && TouchesRiver(x, y);
            byte greenTone = b != 0 ? NaturalGreenBiome[b] : Level[g] <= 5 ? (byte)1 : (byte)0;
            bool naturalGreen = material == TileTables.MaterialLand && Level[g] >= 4 && Level[g] <= 6 && greenTone != 0;
            if (grass || sand || rock || summit || water)
            {
                // The four terrain variants remain visible, but broad checkerboard blocks
                // no longer overpower the small grass and tree sprites.
                int offset = b != 0 ? b * 4 : g * 4;
                float4 average = b != 0
                    ? (ToF(BiomeColors[offset]) + ToF(BiomeColors[offset + 1]) + ToF(BiomeColors[offset + 2]) + ToF(BiomeColors[offset + 3])) * .25f
                    : (ToF(TileColors[offset]) + ToF(TileColors[offset + 1]) + ToF(TileColors[offset + 2]) + ToF(TileColors[offset + 3])) * .25f;
                c = math.lerp(c, average, grass ? .88f : water ? .74f : summit ? .64f : sand ? .76f : .54f);
            }
            if (naturalGreen) c = ToneNaturalGreen(c, greenTone);
            if (river) c.xyz = math.lerp(c.xyz, new float3(.32f, .53f, .59f), .36f);
            else if (floodplain) c.xyz = math.lerp(c.xyz, new float3(.34f, .48f, .27f), .12f);

            // Mix just the two edge rows between land biomes and sand/soil. The
            // interior keeps its own palette, and roads/snow/fire remain crisp.
            if (material == TileTables.MaterialLand && Level[g] >= 3 && Level[g] <= 6 &&
                (f & ((ushort)TileFlags.SnowCover | (ushort)TileFlags.Burning | (ushort)TileFlags.Road | (ushort)TileFlags.River)) == 0)
            {
                float4 edge = new float4(0f);
                int edgeCount = 0;
                if (x > 0) AddLandEdge(i - 1, b, sand, ref edge, ref edgeCount);
                if (x + 1 < Width) AddLandEdge(i + 1, b, sand, ref edge, ref edgeCount);
                if (y > 0) AddLandEdge(i - Width, b, sand, ref edge, ref edgeCount);
                if (y + 1 < Height) AddLandEdge(i + Width, b, sand, ref edge, ref edgeCount);
                if (edgeCount > 0) c = math.lerp(c, edge / edgeCount, .18f);
            }

            if ((f & (ushort)TileFlags.SnowCover) != 0) c = math.lerp(c, ToF(Snow), 0.85f);
            if ((f & (ushort)TileFlags.Burning) != 0) c = ToF(FirePalette[Fire[i] >> 6]);
            if ((f & (ushort)TileFlags.Road) != 0) c = ToF(Road);

            if (y + 1 < Height)
            {
                int dNorth = ReliefLevel[g] - ReliefLevel[Ground[i + Width]];
                c.xyz *= 1f + math.clamp(dNorth, -2, 2) * ReliefShade[g];
            }

            // Bölüm 11 görsel cilası (G2): subtle per-pixel dither so large uniform areas read as
            // texture instead of flat fills. Deterministic hash, ~±5%.
            uint n = math.hash(new uint3((uint)x, (uint)y, (uint)(g * 4 + v)));
            c.xyz *= 1f + ((n & 15) / 255f - 0.028f);

            // G3: shallow water brightens toward the coast (foam band), depth blends between palettes.
            if (water) c = WaterShading(c, i, x, y);

            if (ShowRegions) c = RegionTint(c, i, x, y);

            bool bridge = water && (f & ((ushort)TileFlags.River | (ushort)TileFlags.Road)) ==
                                   ((ushort)TileFlags.River | (ushort)TileFlags.Road);
            if (bridge) c.xyz = math.lerp(c.xyz, ToF(Road).xyz, 0.82f);
            bool snowCover = (f & (ushort)TileFlags.SnowCover) != 0;
            bool blocked = (f & ((ushort)TileFlags.Burning | (ushort)TileFlags.Road)) != 0;
            c.w = bridge ? 224f / 255f : material == TileTables.MaterialWater ? 128f / 255f : material == TileTables.MaterialLava ? 1f
                : blocked ? 0f : river ? 224f / 255f : (snowCover || summit) ? 192f / 255f : rock ? 96f / 255f
                : floodplain ? 160f / 255f
                : (grass || naturalGreen) ? 64f / 255f : sand ? 32f / 255f : 0f;
            c = math.saturate(c) * 255f + 0.5f;
            Pixels[p] = new Color32((byte)c.x, (byte)c.y, (byte)c.z, (byte)c.w);
        }

        void AddLandEdge(int neighbor, byte biome, bool sand, ref float4 sum, ref int count)
        {
            byte ng = Ground[neighbor];
            if (Material[ng] != TileTables.MaterialLand || Level[ng] < 3 || Level[ng] > 6 ||
                (Flags[neighbor] & ((ushort)TileFlags.SnowCover | (ushort)TileFlags.Burning | (ushort)TileFlags.Road)) != 0)
                return;
            byte nb = Biome[neighbor];
            if (nb == biome && (Level[ng] == 3) == sand) return;
            int variant = Variant[neighbor] & 3;
            float4 nc = ToF(nb != 0 ? BiomeColors[nb * 4 + variant] : TileColors[ng * 4 + variant]);
            byte tone = nb != 0 ? NaturalGreenBiome[nb] : Level[ng] <= 5 ? (byte)1 : (byte)0;
            if (Level[ng] >= 4 && tone != 0) nc = ToneNaturalGreen(nc, tone);
            sum += nc;
            count++;
        }

        bool TouchesRiver(int x, int y)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                int nx = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                int ny = y + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                if ((uint)nx >= (uint)Width || (uint)ny >= (uint)Height) continue;
                if ((Flags[ny * Width + nx] & (ushort)TileFlags.River) != 0) return true;
            }
            return false;
        }

        static float4 ToneNaturalGreen(float4 c, byte tone)
        {
            float3 factor = tone == 2 ? new float3(.87f, .92f, .84f)
                : tone == 3 ? new float3(.96f, .93f, .96f)
                : tone == 4 ? new float3(.89f, .94f, .84f)
                : new float3(.78f, .82f, .78f);
            c.xyz *= factor;
            return c;
        }

        // Coast treatment: water touching sand has a soft shallow rim; water
        // depth transitions blend toward their neighbours' own color variants.
        float4 WaterShading(float4 c, int i, int x, int y)
        {
            bool landN = y + 1 < Height && Material[Ground[i + Width]] == TileTables.MaterialLand;
            bool landS = y > 0 && Material[Ground[i - Width]] == TileTables.MaterialLand;
            bool landE = x + 1 < Width && Material[Ground[i + 1]] == TileTables.MaterialLand;
            bool landW = x > 0 && Material[Ground[i - 1]] == TileTables.MaterialLand;
            int coast = (landN ? 1 : 0) + (landS ? 1 : 0) + (landE ? 1 : 0) + (landW ? 1 : 0);
            if (coast > 0)
            {
                int sand = (landN && Level[Ground[i + Width]] == 3 ? 1 : 0)
                    + (landS && Level[Ground[i - Width]] == 3 ? 1 : 0)
                    + (landE && Level[Ground[i + 1]] == 3 ? 1 : 0)
                    + (landW && Level[Ground[i - 1]] == 3 ? 1 : 0);
                // Sand has a soft shallow-water rim. Cliffs and other land only
                // lift the water a little, rather than painting a white outline.
                var shore = new float4(0.62f, 0.79f, 0.86f, c.w);
                float strength = sand > 0 ? (coast >= 2 ? .24f : .16f) : (coast >= 2 ? .14f : .08f);
                c = math.lerp(c, shore, strength);
                return c;
            }
            // Between two different water tiles, average halfway toward the deeper/lighter palette
            // so the shallow -> ocean -> deep_ocean steps read as one smooth gradient.
            float4 sum = c; int n = 1;
            if (y + 1 < Height && IsWater(i + Width)) { sum += NeighborColor(i + Width); n++; }
            if (y > 0 && IsWater(i - Width)) { sum += NeighborColor(i - Width); n++; }
            if (x + 1 < Width && IsWater(i + 1)) { sum += NeighborColor(i + 1); n++; }
            if (x > 0 && IsWater(i - 1)) { sum += NeighborColor(i - 1); n++; }
            return n > 1 ? math.lerp(c, sum / n, 0.45f) : c;
        }

        bool IsWater(int i) => Material[Ground[i]] == TileTables.MaterialWater;

        float4 NeighborColor(int i)
        {
            byte gg = Ground[i];
            int v = Variant[i] & 3;
            return ToF(Biome[i] != 0 ? BiomeColors[Biome[i] * 4 + v] : TileColors[gg * 4 + v]);
        }

        float4 RegionTint(float4 c, int i, int x, int y)
        {
            int region = LandRegion[i] >= 0 ? LandRegion[i] : WaterRegion[i];
            if (region < 0) return c * 0.4f;

            uint h = math.hash(new int2(region, 7919));
            var hue = new float4((h & 255) / 255f, ((h >> 8) & 255) / 255f, ((h >> 16) & 255) / 255f, 1f);
            c = math.lerp(c, hue, 0.55f);

            // Thick island borders: darken where a neighbour belongs to another island.
            int island = Island(i);
            if ((x + 1 < Width && Island(i + 1) != island) || (y + 1 < Height && Island(i + Width) != island) ||
                (x > 0 && Island(i - 1) != island) || (y > 0 && Island(i - Width) != island))
                c.xyz *= 0.35f;
            return c;
        }

        int Island(int i)
        {
            int r = LandRegion[i] >= 0 ? LandRegion[i] : WaterRegion[i];
            return r >= 0 && r < IslandOf.Length ? IslandOf[r] : -1;
        }

        static float4 ToF(Color32 c) => new float4(c.r, c.g, c.b, c.a) / 255f;
    }
}
