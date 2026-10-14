using PG.Core;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Render
{
    // Presentation-only state; never changes simulation data or the save format.
    public static class UnitAnimation
    {
        static readonly int[] Counts = { 2, 4, 3, 2, 1, 3 };
        static readonly int[] FrameMs = { 400, 120, 100, 250, 1000, 150 };
        public struct Motion
        {
            public long Uid, Tick;
            public float2 Position;
            public bool Moving, Initialized;
        }

        public static void Observe(ref Motion sample, long uid, long tick, float2 position, bool hasPath)
        {
            if (!sample.Initialized || sample.Uid != uid || tick < sample.Tick)
            {
                sample = new Motion { Initialized = true, Uid = uid, Tick = tick, Position = position, Moving = hasPath };
                return;
            }
            if (sample.Tick == tick) return;
            sample.Moving = math.lengthsq(position - sample.Position) > .000001f;
            sample.Position = position;
            sample.Tick = tick;
        }

        public static int Frame(long tick, long uid, int animation)
        {
            long milliseconds = tick * (1000 / SimConst.TicksPerSecond) + (uid % 17) * 47;
            return (int)(milliseconds / FrameMs[animation] % Counts[animation]);
        }

        public static int AttackFrame(int remainingTicks, float attacksPerSecond)
        {
            float phase = AttackPhase(remainingTicks, attacksPerSecond);
            return phase < 0 ? -1 : Mathf.Min(2, Mathf.FloorToInt(phase * 3));
        }

        public static float AttackPhase(int remainingTicks, float attacksPerSecond)
        {
            if (remainingTicks <= 0) return -1f;
            int totalTicks = Mathf.Max(2, (int)(SimConst.TicksPerSecond / Mathf.Max(.01f, attacksPerSecond)));
            int elapsed = Mathf.Max(0, totalTicks - remainingTicks);
            int duration = Mathf.Min(totalTicks, Mathf.Max(2, SimConst.TicksPerSecond * 3 / 10));
            return elapsed < duration ? elapsed / (float)(duration - 1) : -1f;
        }

        public const int CorpseLifetime = SimConst.TicksPerSecond * 8;
        public static int DeathFrame(long age) => age < 0 ? -1 : age >= CorpseLifetime ? -1 : (int)math.min(2, age / 3);
        public static float CorpseAlpha(long age) => age < 0 || age >= CorpseLifetime ? 0 : math.saturate((CorpseLifetime - age) / (float)(SimConst.TicksPerSecond * 2));

        public struct Injury
        {
            public long Uid, LastTick, HitTick;
            public float Hp;
            public bool Initialized, Hit;
        }
        public static float ObserveInjury(ref Injury sample, long uid, long tick, float hp)
        {
            if (!sample.Initialized || sample.Uid != uid || tick < sample.LastTick)
                sample = new Injury { Initialized = true, Uid = uid, Hp = hp, LastTick = tick };
            if (tick != sample.LastTick)
            {
                if (hp < sample.Hp - .001f) { sample.Hit = true; sample.HitTick = tick; }
                sample.Hp = hp; sample.LastTick = tick;
            }
            return sample.Hit ? math.saturate(1f - (tick - sample.HitTick) / 5f) : 0f;
        }

        public static Vector2 Breath(long tick, long uid)
        {
            float phase = (tick % 80) * Mathf.PI / 40f + (uid % 17) * .61f;
            float wave = Mathf.Sin(phase);
            return new Vector2(1f - wave * .006f, 1f + wave * .016f);
        }
    }
}
