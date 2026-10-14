using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 3.8.2 keyed neurons that act monthly rather than per think: nests, migrations, gifts, burials,
    // leisure (dance, stargaze, stories, greeting the sun), fire making, taming and shiny collecting.
    public sealed class NeuronLifeSystem : ISimSystem
    {
        public const float GiftChance = 0.1f, DanceChance = 0.1f, PrayChance = 0.1f, TameChance = 0.05f, ShinyChance = 0.1f, FireRisk = 0.005f;
        public const int MigrateNear = 40, MigrateFar = 60, BuryRadius = 6, TameRadius = 6, ShinyRadius = 5, StoryRadius = 5, FireColdC = 5;

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 63;

        int _hapGift = -2, _hapPrayed, _stCelebrating, _featGold;
        const ulong Keyed = ~NeuronKeys.Core;

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 11) return;
            var db = ctx.Content;
            if (_hapGift == -2)
            {
                _hapGift = db.HappinessEvents.IdOrDefault("hap.gift_received");
                _hapPrayed = db.HappinessEvents.IdOrDefault("hap.prayed");
                _stCelebrating = db.StatusEffects.IdOrDefault("st.celebrating");
                _featGold = db.Features.TryGet("feat.ore_gold", out var gold) ? gold.MapValue : -1;
            }
            var w = ctx.Units;
            var u = w.Store;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            int month = ctx.Clock.Month;
            for (int k = u.Alive.Length - 1; k >= 0; k--)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                u.Stat(i, StatId.Hp); // refresh a dirty mask
                ulong nm = u.NeuronMask[i];
                if ((nm & Keyed) == 0 || (AgeStage)u.Age[i] < AgeStage.Adult) continue;
                bool wild = u.City[i] < 0 && (w.Abilities(i) & AbilityFlags.Sapient) == 0;

                if (Has(nm, NeuronId.BuildNest) && wild && u.Home[i].x < 0) u.Home[i] = u.Tile(i);
                if (Has(nm, NeuronId.Migrate) && wild && month == 0 && u.Dest[i].x < 0 && rng.Chance(0.5f)) u.Dest[i] = Far(ctx, i, false, false, ref rng);
                if (Has(nm, NeuronId.SeasonalMigrate) && wild && (month == 3 || month == 9) && u.Dest[i].x < 0) u.Dest[i] = Far(ctx, i, true, month == 9, ref rng);
                if (Has(nm, NeuronId.GiveGift) && u.City[i] >= 0 && rng.Chance(GiftChance)) Gift(ctx, i, ref rng);
                if (Has(nm, NeuronId.BuryDead)) Bury(ctx, i);
                if (Has(nm, NeuronId.Dance) && rng.Chance(DanceChance)) w.AddStatus(i, _stCelebrating);
                if (Has(nm, NeuronId.Stargaze)) w.GainXp(i, 5f);
                if (Has(nm, NeuronId.TellStory)) Story(ctx, i);
                if (Has(nm, NeuronId.GreetSun) && rng.Chance(PrayChance)) Happiness.Add(u, i, _hapPrayed);
                if (Has(nm, NeuronId.MakeFire)) Fire(ctx, i, ref rng);
                if (Has(nm, NeuronId.TameAnimal) && rng.Chance(TameChance)) Tame(ctx, i);
                if (Has(nm, NeuronId.CollectShiny) && rng.Chance(ShinyChance)) Shiny(ctx, i);
            }
        }

        static bool Has(ulong mask, NeuronId n) => (mask & (1UL << (int)n)) != 0;

        // a destination 40-60 tiles off; seasonal migrants pick the warmest (winter) or coolest (summer) of 8 samples
        static int2 Far(in SimContext ctx, int i, bool seasonal, bool winter, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            var map = ctx.World;
            int2 best = new int2(-1);
            float bestScore = float.MinValue;
            for (int t = 0; t < 8; t++)
            {
                float angle = rng.Range(0f, 6.2831853f);
                int2 d = u.Tile(i) + (int2)(new float2(math.cos(angle), math.sin(angle)) * rng.Range(MigrateNear, MigrateFar));
                if (!map.InBounds(d.x, d.y) || !map.IsWalkable(d.x, d.y) || map.IsWater(d.x, d.y)) continue;
                float temp = map.Zones[map.ZoneIndexOf(d.x, d.y)].TemperatureC;
                float score = seasonal ? (winter ? temp : -temp) : rng.Value01();
                if (score > bestScore) { bestScore = score; best = d; }
            }
            return best;
        }

        void Gift(in SimContext ctx, int i, ref SimRandom rng)
        {
            var residents = ctx.Civ?.CityResidents[ctx.Units.Store.City[i]];
            if (residents == null || residents.Count < 2) return;
            int o = residents[rng.Range(0, residents.Count)];
            if (o != i) Happiness.Add(ctx.Units.Store, o, _hapGift);
        }

        static void Bury(in SimContext ctx, int i)
        {
            var u = ctx.Units.Store;
            var corpses = u.Corpses;
            int w = ctx.World.Width;
            int2 t = u.Tile(i);
            for (int k = corpses.Length - 1; k >= 0; k--)
            {
                int tile = corpses[k].Tile;
                if (math.abs(tile % w - t.x) > BuryRadius || math.abs(tile / w - t.y) > BuryRadius) continue;
                corpses.RemoveAtSwapBack(k); // laid to rest: no scavenging, no rising
                return;
            }
        }

        static void Story(in SimContext ctx, int i)
        {
            var w = ctx.Units;
            var u = w.Store;
            int c = u.City[i];
            if (c < 0 || ctx.Civ == null) return;
            int told = 0;
            foreach (int o in ctx.Civ.CityResidents[c])
            {
                if (o == i || math.distancesq(u.Pos[o], u.Pos[i]) > StoryRadius * StoryRadius) continue;
                w.GainXp(o, 3f);
                if (++told >= 5) break;
            }
        }

        // fire makers warm themselves and kin in the cold (frost ends); a campfire may escape now and then
        static void Fire(in SimContext ctx, int i, ref SimRandom rng)
        {
            var w = ctx.Units;
            var u = w.Store;
            var map = ctx.World;
            int2 t = u.Tile(i);
            if (!map.InBounds(t.x, t.y) || map.Zones[map.ZoneIndexOf(t.x, t.y)].TemperatureC > FireColdC || w.StFrozen < 0) return;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int o = u.Alive[k];
                if (u.Species[o] == u.Species[i] && math.distancesq(u.Pos[o], u.Pos[i]) <= 9f) u.RemoveStatus(o, w.StFrozen);
            }
            if (rng.Chance(FireRisk)) ctx.Nature.Ignite(t.x + 1, t.y, NatureState.IgniteDefault, false, NatureState.IgniteUnit);
        }

        static void Tame(in SimContext ctx, int i)
        {
            var w = ctx.Units;
            var u = w.Store;
            var db = ctx.Content;
            if (w.StCharmed < 0) return;
            int best = -1;
            float bestD = TameRadius * TameRadius;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int o = u.Alive[k];
                var sp = db.Species[u.Species[o]];
                if (o == i || !MetamorphosisRules.IsAnimal(sp) || sp.IsCiv || u.HasStatus(o, w.StCharmed)) continue;
                float d = math.distancesq(u.Pos[o], u.Pos[i]);
                if (d < bestD) { bestD = d; best = o; }
            }
            if (best >= 0) w.AddStatus(best, w.StCharmed);
        }

        void Shiny(in SimContext ctx, int i)
        {
            var u = ctx.Units.Store;
            var map = ctx.World;
            if (_featGold < 0) return;
            int2 t = u.Tile(i);
            for (int dy = -ShinyRadius; dy <= ShinyRadius; dy++)
                for (int dx = -ShinyRadius; dx <= ShinyRadius; dx++)
                {
                    int x = t.x + dx, y = t.y + dy;
                    if (!map.InBounds(x, y) || map.Feature[map.Index(x, y)] != _featGold) continue;
                    map.SetFeature(x, y, 0, ChangeSource.Unit);
                    if (ctx.Civ != null && u.City[i] >= 0 && ctx.Civ.CityAlive(u.City[i])) ctx.Civ.AddRes(u.City[i], ctx.Civ.ResGold, 5);
                    return;
                }
        }
    }
}
