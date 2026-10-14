using PG.Core;
using Unity.Mathematics;

namespace PG.Sim
{
    // Places a group of new units on a disc that grows with the group size, keeping a minimum gap between them
    // (DECISIONS #112). A drop of 40 units used to land inside a 0.6 x 0.6 box and look like one sprite.
    // Usage: Begin(), then Next() once per unit. Single-threaded sim: the buffer is reused between calls.
    public static class SpawnSpread
    {
        public const float MinGap = 0.55f, BaseRadius = 0.5f, RadiusPerRootUnit = 0.45f;
        const int Tries = 12;
        static float2[] _placed = new float2[64];
        static int _count;

        public static void Begin() => _count = 0;

        public static float Radius(int total, float maxRadius) => math.min(maxRadius, BaseRadius + RadiusPerRootUnit * math.sqrt(total));

        public static float2 Next(UnitWorld w, Mobility mob, float2 center, int total, float maxRadius, ref SimRandom rng)
        {
            float radius = Radius(total, maxRadius);
            float2 best = center;
            float bestGap = -1f;
            for (int t = 0; t < Tries; t++)
            {
                float angle = rng.Range(0f, 6.2831853f);
                float2 p = center + new float2(math.cos(angle), math.sin(angle)) * (radius * math.sqrt(rng.Range(0f, 1f)));
                if (!w.Paths.CanStand((int)math.floor(p.x), (int)math.floor(p.y), mob)) continue;
                float gap = NearestPlaced(p);
                if (gap >= MinGap) { best = p; break; }
                if (gap > bestGap) { best = p; bestGap = gap; }
            }
            if (_count == _placed.Length) System.Array.Resize(ref _placed, _count * 2);
            _placed[_count++] = best;
            return best;
        }

        static float NearestPlaced(float2 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < _count; k++) best = math.min(best, math.distance(p, _placed[k]));
            return best;
        }
    }
}
