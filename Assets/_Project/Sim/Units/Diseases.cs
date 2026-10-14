using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    public readonly struct UnitTraitGainedEvent : ISimEvent
    {
        public readonly long Uid;
        public readonly ushort Trait;
        public UnitTraitGainedEvent(long uid, ushort trait) { Uid = uid; Trait = trait; }
    }

    public readonly struct TraitDiscoveredEvent : ISimEvent
    {
        public readonly ushort Trait;
        public TraitDiscoveredEvent(ushort trait) { Trait = trait; }
    }

    // Bölüm 8.9 ach.cure_found: a transforming disease stopped before it finished (holy light, spell.heal, the cure power).
    public readonly struct DiseaseCuredEvent : ISimEvent
    {
        public readonly long Uid;
        public readonly bool WasTransforming;
        public DiseaseCuredEvent(long uid, bool wasTransforming) { Uid = uid; WasTransforming = wasTransforming; }
    }

    public readonly struct TransformedEvent : ISimEvent
    {
        public readonly long Uid;
        public readonly ushort From, To;
        public TransformedEvent(long uid, ushort from, ushort to) { Uid = uid; From = from; To = to; }
    }

    // Bölüm 4.8: one active disease per unit; infection, monthly course, contact spread, carriers, cloud exposure.
    public static class Diseases
    {
        public const int ImmuneYears = 5;

        // Rats and plague bearers carry without falling ill.
        public static bool Carrier(UnitWorld w, int i) =>
            w.Store.Species[i] == w.SpRat || w.TrPlagueBearer >= 0 && w.Store.Traits[i].Has(w.TrPlagueBearer);

        public static bool Infect(UnitWorld w, int i, int disease, long tick)
        {
            var u = w.Store;
            if (disease < 0 || !u.IsAlive(i) || u.Disease[i] != 0 || u.ImmuneUntil[i] > tick) return false;
            if (u.Has(i, UnitFlags.Undead) || UnitLifeSystem.Immune(w, i, w.Content.Diseases[disease].Id) || w.TrImmune >= 0 && u.Traits[i].Has(w.TrImmune)) return false;
            u.Disease[i] = (byte)(disease + 1);
            u.DiseaseProgress[i] = 0;
            if (disease == w.DisRotbite && w.StTurning >= 0) w.AddStatus(i, w.StTurning);
            return true;
        }

        public static void Cure(UnitWorld w, int i, long tick, bool immune)
        {
            var u = w.Store;
            if (u.Disease[i] == 0) return;
            bool wasTransforming = w.Content.Diseases[u.Disease[i] - 1].TransformSpecies >= 0;
            u.Disease[i] = 0;
            u.DiseaseProgress[i] = 0;
            if (immune) u.ImmuneUntil[i] = tick + ImmuneYears * SimConst.TicksPerYear;
            if (w.StTurning >= 0) u.RemoveStatus(i, w.StTurning);
            w.Events?.Publish(new DiseaseCuredEvent(u.Uid[i], wasTransforming));
        }
    }

    public sealed class DiseaseSystem : ISimSystem
    {
        public const float ContactRadius = 1.5f;
        public const int MaxContacts = 1;
        public const float DensityRadius = 4f, DensityAtMax = 12f, DensityMin = 0.75f, DensityMax = 2f;

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 60;

        int _lawSpread = -2;
        public int[] ActiveCases = new int[0]; // per disease, monthly (statistics, ach.plague_lord)

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 5) return;
            var w = ctx.Units;
            var u = w.Store;
            var db = ctx.Content;
            if (ActiveCases.Length != db.Diseases.Count) ActiveCases = new int[db.Diseases.Count];
            System.Array.Clear(ActiveCases, 0, ActiveCases.Length);
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            int eraPale = db.Eras.IdOrDefault("era.pale");
            float eraMul = eraPale >= 0 && ctx.Nature.Era.CurrentEra == eraPale ? 2f : 1f;
            if (_lawSpread == -2) _lawSpread = ctx.Nature.Laws.IndexOf("law.disease");
            bool spread = _lawSpread < 0 || ctx.Nature.Laws.IsOn(_lawSpread);
            int count = u.Alive.Length;
            for (int k = 0; k < count; k++)
            {
                int i = u.Alive[k];
                int d = u.Disease[i] - 1;
                if (d < 0 || u.State[i] != UnitStore.StateAlive) continue;
                ActiveCases[d]++;
                var def = db.Diseases[d];
                if (spread && (def.SpreadMask & SpreadFlags.Contact) != 0) Spread(ctx, w, i, d, def.Contagion * eraMul, tick, ref rng);
                if (Diseases.Carrier(w, i)) continue;
                Course(ctx, w, i, d, def, tick, ref rng);
            }
            Clouds(ctx, w, tick, ref rng);
            if (spread) Air(ctx, w, tick, ref rng);
        }

        public const float AirFactor = 0.1f; // monthly chance = contagion x this in spore biomes

        // Bölüm 4.8 air spread: only biome-borne (spore lands).
        static void Air(in SimContext ctx, UnitWorld w, long tick, ref SimRandom rng)
        {
            var db = ctx.Content;
            var map = ctx.World;
            var u = w.Store;
            for (int d = 0; d < db.Diseases.Count; d++)
            {
                var def = db.Diseases[d];
                if ((def.SpreadMask & SpreadFlags.Air) == 0) continue;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (u.State[i] != UnitStore.StateAlive || u.Disease[i] != 0) continue;
                    int2 t = u.Tile(i);
                    if (!map.InBounds(t.x, t.y)) continue;
                    byte b = map.Biome[map.Index(t.x, t.y)];
                    if (b == 0 || map.Tables.BiomeEffectCode[b] != (byte)BiomeEffect.Spores) continue;
                    if (rng.Chance(def.Contagion * AirFactor * math.max(0f, 1f - u.Stat(i, StatId.DiseaseResist)))) Diseases.Infect(w, i, d, tick);
                }
            }
        }

        static void Course(in SimContext ctx, UnitWorld w, int i, int d, DiseaseDef def, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            int months = math.max(1, def.DurationMonths);
            int progress = u.DiseaseProgress[i] + 65535 / months;
            float death = def.Lethality / months * (HasHealers(ctx.Civ, u.City[i]) ? 0.5f : 1f) * (Treated(ctx.Civ, u, u.City[i]) ? 0.5f : 1f);
            if (rng.Chance(death)) { w.Store.Kill(i, DeathCause.Disease); return; }
            // transforming diseases do not wear off by themselves: only cures and holy light stop them (DECISIONS #74)
            if (def.TransformSpecies < 0 && rng.Chance(0.1f * (1f + u.Stat(i, StatId.DiseaseResist)))) { Diseases.Cure(w, i, tick, true); return; }
            if (progress < 65535) { u.DiseaseProgress[i] = (ushort)progress; return; }
            int to = w.Morph.PickForDisease(d, u.Species[i], ref rng); // Bölüm 4.9 variants
            if (to >= 0 && to != u.Species[i]) MetamorphosisSystem.Morph(ctx, w, i, to, tick, ref rng);
            else Diseases.Cure(w, i, tick, true);
        }

        // a healer at work in the city treats the sick (x0.5 death chance)
        static bool Treated(CivWorld civ, UnitStore u, int city)
        {
            if (civ == null || city < 0 || !civ.CityAlive(city) || civ.JobHealer < 0) return false;
            foreach (int o in civ.CityResidents[city]) if (u.Job[o] - 1 == civ.JobHealer) return true;
            return false;
        }

        static bool HasHealers(CivWorld civ, int city) =>
            civ != null && city >= 0 && civ.CityAlive(city) && civ.BldHealers >= 0 && civ.CountBuildings(city, civ.BldHealers, true) > 0;

        static void Spread(in SimContext ctx, UnitWorld w, int i, int d, float contagion, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var idx = w.Index;
            var map = ctx.World;
            idx.CellRange(u.Pos[i], ContactRadius, out int x0, out int y0, out int x1, out int y1);
            int contacts = 0;
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * idx.CellsX + cx;
                    for (int k = idx.CellStart[cell]; k < idx.CellStart[cell + 1]; k++)
                    {
                        int o = idx.Sorted[k];
                        if (o == i || u.State[o] != UnitStore.StateAlive || u.Disease[o] != 0) continue;
                        if (math.distancesq(u.Pos[o], u.Pos[i]) > ContactRadius * ContactRadius) continue;
                        if (++contacts > MaxContacts) return; // ponytail: a crowd around one patient counts as one contact a month (DECISIONS #74)
                        float mul = math.max(0f, 1f - u.Stat(o, StatId.DiseaseResist));
                        int2 t = u.Tile(o);
                        if (map.InBounds(t.x, t.y) && map.Biome[map.Index(t.x, t.y)] == w.BiomeSwampValue && w.BiomeSwampValue != 0) mul *= 1.5f;
                        int c = u.City[o];
                        if (ctx.Civ != null && c >= 0 && ctx.Civ.CityAlive(c)) mul *= math.min(3f, 1f + ctx.Civ.City(c).Population / 200f);
                        int nearby = w.CountNear(u.Pos[i], DensityRadius, i);
                        float density = math.lerp(DensityMin, DensityMax, math.saturate(nearby / DensityAtMax));
                        if (rng.Chance(contagion * mul * density)) Diseases.Infect(w, o, d, tick);
                    }
                }
        }

        // Bölüm 2.7 unit clouds: plague/rot infect, blessing blesses, life heals.
        static void Clouds(in SimContext ctx, UnitWorld w, long tick, ref SimRandom rng)
        {
            var clouds = ctx.Nature.Clouds;
            if (clouds.Length == 0) return;
            var u = w.Store;
            var db = ctx.Content;
            for (int c = 0; c < clouds.Length; c++)
            {
                var cloud = clouds[c];
                var effect = db.Clouds[cloud.Type].EffectKind;
                if (effect != CloudEffect.Plague && effect != CloudEffect.Rot && effect != CloudEffect.Blessing && effect != CloudEffect.Life) continue;
                float r2 = cloud.Radius * cloud.Radius;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (math.distancesq(u.Pos[i], cloud.Pos) > r2) continue;
                    switch (effect)
                    {
                        case CloudEffect.Plague: if (rng.Chance(0.25f)) Diseases.Infect(w, i, w.DisPlague, tick); break;
                        case CloudEffect.Rot: if (rng.Chance(0.25f)) Diseases.Infect(w, i, w.DisRotbite, tick); break;
                        case CloudEffect.Blessing: w.AddStatus(i, w.StBlessed); break;
                        case CloudEffect.Life: w.Heal(i, u.Stat(i, StatId.Hp)); break;
                    }
                }
            }
        }
    }
}
