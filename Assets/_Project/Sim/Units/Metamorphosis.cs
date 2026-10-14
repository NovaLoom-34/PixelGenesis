using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 4.9 metamorphoses.json indexed by trigger.
    public sealed class MetamorphosisRules
    {
        readonly ContentDB _db;
        public readonly MetamorphosisDef[][] AgeBySpecies;     // species -> age rules
        public readonly MetamorphosisDef[][] MergeBySpecies;   // species -> merge rules
        public readonly MetamorphosisDef[][] ByDisease;        // disease -> rules
        public readonly MetamorphosisDef[] EraRules;
        public readonly bool AnyAge, AnyMerge;

        public MetamorphosisRules(ContentDB db)
        {
            _db = db;
            var age = new List<MetamorphosisDef>[db.Species.Count];
            var merge = new List<MetamorphosisDef>[db.Species.Count];
            var dis = new List<MetamorphosisDef>[db.Diseases.Count];
            var era = new List<MetamorphosisDef>();
            for (int m = 0; m < db.Metamorphoses.Count; m++)
            {
                var def = db.Metamorphoses[m];
                if (def.ToSpecies < 0) continue;
                switch (def.TriggerKind)
                {
                    case MetaTrigger.Age when def.Filter == MetaFilter.Species: (age[def.FromSpecies] ??= new List<MetamorphosisDef>()).Add(def); AnyAge = true; break;
                    case MetaTrigger.Merge when def.Filter == MetaFilter.Species: (merge[def.FromSpecies] ??= new List<MetamorphosisDef>()).Add(def); AnyMerge = true; break;
                    case MetaTrigger.Disease when def.TriggerDisease >= 0: (dis[def.TriggerDisease] ??= new List<MetamorphosisDef>()).Add(def); break;
                    case MetaTrigger.Era: era.Add(def); break;
                }
            }
            AgeBySpecies = Arrays(age);
            MergeBySpecies = Arrays(merge);
            ByDisease = Arrays(dis);
            EraRules = era.ToArray();
        }

        static MetamorphosisDef[][] Arrays(List<MetamorphosisDef>[] lists)
        {
            var a = new MetamorphosisDef[lists.Length][];
            for (int k = 0; k < a.Length; k++) a[k] = lists[k]?.ToArray() ?? System.Array.Empty<MetamorphosisDef>();
            return a;
        }

        public static bool IsAnimal(SpeciesDef sp) => sp.Category == "animal" || sp.Category == "evo";

        // Disease-end variant (zombie_rules.json variantPick): exact species, then size, then animal, then the chance rules, then the default.
        public int PickForDisease(int disease, int species, ref SimRandom rng)
        {
            var rules = disease >= 0 && disease < ByDisease.Length ? ByDisease[disease] : System.Array.Empty<MetamorphosisDef>();
            if (rules.Length == 0) return disease >= 0 ? _db.Diseases[disease].TransformSpecies : -1;
            var sp = _db.Species[species];
            foreach (var r in rules) if (r.Filter == MetaFilter.Species && r.FromSpecies == species) return r.ToSpecies;
            foreach (var r in rules) if (r.Filter == MetaFilter.SizeAtLeast && sp.BaseStats[(int)StatId.Size] >= r.FilterSize) return r.ToSpecies;
            if (IsAnimal(sp)) foreach (var r in rules) if (r.Filter == MetaFilter.Animal) return r.ToSpecies;
            float roll = rng.Value01(), cum = 0f;
            foreach (var r in rules)
            {
                if (r.Filter != MetaFilter.Any || r.Chance >= 1f) continue;
                cum += r.Chance;
                if (roll < cum) return r.ToSpecies;
            }
            foreach (var r in rules) if (r.Filter == MetaFilter.Any && r.Chance >= 1f) return r.ToSpecies;
            return _db.Diseases[disease].TransformSpecies;
        }
    }

    // Monthly age, era and merge metamorphoses.
    public sealed class MetamorphosisSystem : ISimSystem
    {
        public const float EraSpeciesChance = 0.02f, EraFeatureChance = 0.5f, MergeRadius = 2f;
        public const int EraFeatureSamples = 64;
        int _law = -2;

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 61;

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 6) return;
            if (_law == -2) _law = ctx.Nature.Laws.IndexOf("law.metamorphosis");
            if (_law >= 0 && !ctx.Nature.Laws.IsOn(_law)) return;
            var w = ctx.Units;
            var rules = w.Morph;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            if (rules.AnyAge) Ages(ctx, w, rules, tick, ref rng);
            if (rules.EraRules.Length > 0) Eras(ctx, w, rules, tick, ref rng);
            if (rules.AnyMerge) Merges(ctx, w, rules, tick);
        }

        public static float FormYears(UnitStore u, int i, long tick) => (tick - math.max(u.BirthTick[i], u.TransformTick[i])) / (float)SimConst.TicksPerYear;

        static void Ages(in SimContext ctx, UnitWorld w, MetamorphosisRules rules, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            for (int k = u.Alive.Length - 1; k >= 0; k--)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                var list = rules.AgeBySpecies[u.Species[i]];
                foreach (var r in list)
                {
                    if (r.ToSpecies == u.Species[i] || FormYears(u, i, tick) < r.TriggerValue) continue;
                    Morph(ctx, w, i, r.ToSpecies, tick, ref rng);
                    break;
                }
            }
        }

        public static void Morph(in SimContext ctx, UnitWorld w, int i, int to, long tick, ref SimRandom rng)
        {
            ushort from = w.Transform(i, to, tick, ref rng);
            ctx.Events.Publish(new TransformedEvent(w.Store.Uid[i], from, (ushort)to));
        }

        static void Eras(in SimContext ctx, UnitWorld w, MetamorphosisRules rules, long tick, ref SimRandom rng)
        {
            int era = ctx.Nature.Era.CurrentEra;
            var map = ctx.World;
            var u = w.Store;
            foreach (var r in rules.EraRules)
            {
                if (r.TriggerEra != era) continue;
                if (r.Filter == MetaFilter.Feature && r.FromFeature != 0)
                {
                    for (int s = 0; s < EraFeatureSamples; s++)
                    {
                        int x = rng.Range(0, map.Width), y = rng.Range(0, map.Height);
                        if (map.Feature[map.Index(x, y)] != r.FromFeature || !rng.Chance(EraFeatureChance)) continue;
                        map.SetFeature(x, y, 0, ChangeSource.Unit);
                        w.Spawn(new UnitSpawnRequest { Species = r.ToSpecies, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = 1f, Sex = -1, Subspecies = -1 }, tick, ref rng);
                    }
                }
                else if (r.Filter == MetaFilter.Species)
                    for (int k = u.Alive.Length - 1; k >= 0; k--)
                    {
                        int i = u.Alive[k];
                        if (u.State[i] == UnitStore.StateAlive && u.Species[i] == r.FromSpecies && rng.Chance(EraSpeciesChance)) Morph(ctx, w, i, r.ToSpecies, tick, ref rng);
                    }
            }
        }

        // merge:n — n of a kind within 2 tiles fuse: the rest vanish, the survivor grows giant; giants merge no further (DECISIONS #73).
        static void Merges(in SimContext ctx, UnitWorld w, MetamorphosisRules rules, long tick)
        {
            var u = w.Store;
            var idx = w.Index;
            int giant = w.TrGiant;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                var list = rules.MergeBySpecies[u.Species[i]];
                if (list.Length == 0 || giant >= 0 && u.Traits[i].Has(giant)) continue;
                int need = (int)list[0].TriggerValue - 1;
                idx.CellRange(u.Pos[i], MergeRadius, out int x0, out int y0, out int x1, out int y1);
                int found = 0;
                for (int cy = y0; cy <= y1 && found < need; cy++)
                    for (int cx = x0; cx <= x1 && found < need; cx++)
                    {
                        int cell = cy * idx.CellsX + cx;
                        for (int q = idx.CellStart[cell]; q < idx.CellStart[cell + 1] && found < need; q++)
                        {
                            int o = idx.Sorted[q];
                            if (o == i || u.State[o] != UnitStore.StateAlive || u.Species[o] != u.Species[i] || giant >= 0 && u.Traits[o].Has(giant)) continue;
                            if (math.distancesq(u.Pos[o], u.Pos[i]) > MergeRadius * MergeRadius) continue;
                            found++;
                        }
                    }
                if (found < need) continue;
                int removed = 0;
                for (int cy = y0; cy <= y1 && removed < need; cy++)
                    for (int cx = x0; cx <= x1 && removed < need; cx++)
                    {
                        int cell = cy * idx.CellsX + cx;
                        for (int q = idx.CellStart[cell]; q < idx.CellStart[cell + 1] && removed < need; q++)
                        {
                            int o = idx.Sorted[q];
                            if (o == i || u.State[o] != UnitStore.StateAlive || u.Species[o] != u.Species[i] || giant >= 0 && u.Traits[o].Has(giant)) continue;
                            if (math.distancesq(u.Pos[o], u.Pos[i]) > MergeRadius * MergeRadius) continue;
                            u.Kill(o, DeathCause.Transformed);
                            removed++;
                        }
                    }
                if (giant >= 0)
                {
                    var traits = u.Traits[i];
                    UnitLife.AddTrait(ctx.Content, ref traits, giant);
                    u.Traits[i] = traits;
                    UnitStats.Recompute(u, i);
                }
                u.Hp[i] = u.Stat(i, StatId.Hp);
                ushort sp = u.Species[i];
                if (list[0].ToSpecies != sp) Morph(ctx, w, i, list[0].ToSpecies, tick, ref ctx.Rng.Get(RngStream.UnitAI));
            }
        }
    }

    // Bölüm 4.9 zombie rules: rising dead, decay, the scorch era, hordes and their lords, the bloater's last breath.
    public sealed class ZombieSystem : ISimSystem
    {
        public const int RiseEvery = 10, MaxRisesPerPass = 64, HordeMin = 50, LordPer = 500, DecayYears = 10, ScorchEvery = 60;
        public const float BloaterRadius = 3f, BloaterChance = 0.5f;

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 62;

        int _lawApocalypse = -2, _lawRising, _lawDecay, _lawHordes, _eraScorch, _culBurners, _spLord, _spBloater;
        int[] _zoneCount = new int[0], _zoneComp = new int[0], _queue = new int[0];
        readonly List<int> _compZones = new List<int>();
        bool _subscribed;

        void Init(in SimContext ctx)
        {
            var laws = ctx.Nature.Laws;
            var db = ctx.Content;
            _lawApocalypse = laws.IndexOf("law.zombie_apocalypse");
            _lawDecay = laws.IndexOf("law.zombie_decay");
            _lawHordes = laws.IndexOf("law.zombie_hordes");
            _lawRising = laws.IndexOf("law.undead_rising");
            _eraScorch = db.Eras.IdOrDefault("era.scorch");
            _culBurners = db.CultureTraits.IdOrDefault("cul.corpse_burners");
            _spLord = db.Species.IdOrDefault("sp.zombie_lord");
            _spBloater = db.Species.IdOrDefault("sp.zombie_bloater");
        }

        public void Tick(in SimContext ctx)
        {
            if (_lawApocalypse == -2) Init(ctx);
            var w = ctx.Units;
            long tick = ctx.Clock.Tick;
            if (!_subscribed)
            {
                _subscribed = true;
                var units = w;
                int bloater = _spBloater;
                var rng = ctx.Rng;
                ctx.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == bloater && bloater >= 0) LastBreath(units, e.Tile, rng); });
            }
            ref var r = ref ctx.Rng.Get(RngStream.UnitAI);
            if (tick % RiseEvery == 0) Rise(ctx, w, tick, ref r);
            if (_eraScorch >= 0 && ctx.Nature.Era.CurrentEra == _eraScorch && tick % ScorchEvery == 8) Scorch(w, tick);
            if (tick % SimConst.TicksPerMonth != 7) return;
            if (ctx.Nature.Laws.IsOn(_lawDecay)) Decay(ctx, w, tick);
            if (ctx.Nature.Laws.IsOn(_lawHordes)) Hordes(ctx, w, tick, ref r);
        }

        // Infected corpses (every corpse under law.zombie_apocalypse) rise 40 ticks after death unless burned by a pyre or corpse burners.
        void Rise(in SimContext ctx, UnitWorld w, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var corpses = u.Corpses;
            bool apocalypse = ctx.Nature.Laws.IsOn(_lawApocalypse);
            if (!apocalypse && _lawRising >= 0 && !ctx.Nature.Laws.IsOn(_lawRising)) return;
            var db = ctx.Content;
            var map = ctx.World;
            int after = db.ZombieRules.RiseAfterTicks, risen = 0;
            for (int k = corpses.Length - 1; k >= 0 && risen < MaxRisesPerPass; k--)
            {
                var c = corpses[k];
                if (c.Rise == 0 && !apocalypse || tick - c.Tick < after) continue;
                var sp = db.Species[c.Species];
                if ((sp.Flags & UnitFlags.Undead) != 0 || sp.Category == "elemental" || sp.Category == "special") continue;
                corpses.RemoveAtSwapBack(k);
                int x = c.Tile % map.Width, y = c.Tile / map.Width;
                if (Burned(ctx, x, y)) continue;
                int to = w.Morph.PickForDisease(w.DisRotbite, c.Species, ref rng);
                if (to < 0) continue;
                var id = w.Spawn(new UnitSpawnRequest { Species = to, Pos = new float2(x + 0.5f, y + 0.5f), AgeYears = 1f, Sex = -1, Subspecies = -1 }, tick, ref rng);
                if (!u.IsAlive(id)) continue;
                u.FormerSpecies[id.Index] = (ushort)(c.Species + 1);
                u.TransformTick[id.Index] = tick;
                if (w.TrHollow >= 0)
                {
                    var traits = u.Traits[id.Index];
                    UnitLife.AddTrait(db, ref traits, w.TrHollow);
                    u.Traits[id.Index] = traits;
                }
                UnitStats.Recompute(u, id.Index);
                u.Hp[id.Index] = u.Stat(id.Index, StatId.Hp);
                ctx.Events.Publish(new TransformedEvent(u.Uid[id.Index], c.Species, (ushort)to));
                risen++;
            }
        }

        bool Burned(in SimContext ctx, int x, int y)
        {
            var civ = ctx.Civ;
            if (civ == null) return false;
            int city = civ.CityAtTile(x, y);
            if (city < 0 || !civ.CityAlive(city)) return false;
            if (civ.BldPyre >= 0 && civ.CountBuildings(city, civ.BldPyre, true) > 0) return true;
            return ctx.Meta != null && ctx.Meta.CultureHas(civ.City(city).Culture, _culBurners);
        }

        static void Scorch(UnitWorld w, long tick)
        {
            var u = w.Store;
            for (int k = u.Alive.Length - 1; k >= 0; k--)
            {
                int i = u.Alive[k];
                if (u.State[i] == UnitStore.StateAlive && w.IsZombie(i)) w.Damage(i, 1f, -1, DeathCause.Burned, tick);
            }
        }

        // law.zombie_decay: ten years in zombie form leave a bone pile (the runner-to-crawler step is a metamorphosis rule).
        static void Decay(in SimContext ctx, UnitWorld w, long tick)
        {
            var u = w.Store;
            var map = ctx.World;
            for (int k = u.Alive.Length - 1; k >= 0; k--)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive || !w.IsZombie(i) || MetamorphosisSystem.FormYears(u, i, tick) < DecayYears) continue;
                int2 t = u.Tile(i);
                if (w.BonesFeature != 0 && map.InBounds(t.x, t.y) && map.Feature[map.Index(t.x, t.y)] == 0) map.SetFeature(t.x, t.y, w.BonesFeature, 2, ChangeSource.Unit);
                u.Kill(i, DeathCause.Void);
            }
        }

        // Zone-connected clusters of 50+ zombies march on the nearest city; one lord per 500 walks at the centre.
        void Hordes(in SimContext ctx, UnitWorld w, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var map = ctx.World;
            int zones = map.ZonesX * map.ZonesY;
            if (_zoneCount.Length != zones) { _zoneCount = new int[zones]; _zoneComp = new int[zones]; _queue = new int[zones]; }
            System.Array.Clear(_zoneCount, 0, zones);
            int total = 0;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive || !w.IsZombie(i)) continue;
                int2 t = u.Tile(i);
                if (!map.InBounds(t.x, t.y)) continue;
                _zoneCount[map.ZoneIndexOf(t.x, t.y)]++;
                total++;
            }
            if (total < HordeMin) return;
            for (int z = 0; z < zones; z++) _zoneComp[z] = -1;
            int comps = 0;
            for (int z = 0; z < zones; z++)
            {
                if (_zoneCount[z] == 0 || _zoneComp[z] >= 0) continue;
                int head = 0, tail = 0, size = 0;
                float2 sum = 0f;
                _queue[tail++] = z;
                _zoneComp[z] = comps;
                while (head < tail)
                {
                    int q = _queue[head++];
                    size += _zoneCount[q];
                    sum += new float2((q % map.ZonesX + 0.5f) * WorldMap.ZoneSize, (q / map.ZonesX + 0.5f) * WorldMap.ZoneSize) * _zoneCount[q];
                    int qx = q % map.ZonesX, qy = q / map.ZonesX;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = qx + dx, ny = qy + dy;
                            if (nx < 0 || ny < 0 || nx >= map.ZonesX || ny >= map.ZonesY) continue;
                            int n = ny * map.ZonesX + nx;
                            if (_zoneCount[n] == 0 || _zoneComp[n] >= 0) continue;
                            _zoneComp[n] = comps;
                            _queue[tail++] = n;
                        }
                }
                if (size >= HordeMin) March(ctx, w, comps, size, sum / size, tick, ref rng);
                comps++;
            }
        }

        void March(in SimContext ctx, UnitWorld w, int comp, int size, float2 center, long tick, ref SimRandom rng)
        {
            var civ = ctx.Civ;
            var u = w.Store;
            var map = ctx.World;
            int target = -1;
            float best = float.MaxValue;
            for (int c = 0; civ != null && c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c) || civ.City(c).Population == 0) continue;
                float d = math.distancesq(center, civ.City(c).Center);
                if (d < best) { best = d; target = c; }
            }
            int lords = 0;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive || !w.IsZombie(i)) continue;
                int2 t = u.Tile(i);
                if (!map.InBounds(t.x, t.y) || _zoneComp[map.ZoneIndexOf(t.x, t.y)] != comp) continue;
                if (u.Species[i] == _spLord) { lords++; u.Dest[i] = (int2)center; continue; } // the lord keeps to the centre
                if (target >= 0) u.Dest[i] = civ.City(target).Center;
            }
            for (int want = size / LordPer; lords < want && _spLord >= 0; lords++)
                w.Spawn(new UnitSpawnRequest { Species = _spLord, Pos = center, AgeYears = 1f, Sex = -1, Subspecies = -1 }, tick, ref rng);
        }

        // The bloater bursts: living units within 3 tiles catch rotbite half the time (a one-off cloud).
        static void LastBreath(UnitWorld w, int2 tile, SimRandomProvider rngs)
        {
            var u = w.Store;
            ref var rng = ref rngs.Get(RngStream.UnitAI);
            float2 p = (float2)tile + 0.5f;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive || math.distancesq(u.Pos[i], p) > BloaterRadius * BloaterRadius || !rng.Chance(BloaterChance)) continue;
                Diseases.Infect(w, i, w.DisRotbite, 0);
            }
        }
    }
}
