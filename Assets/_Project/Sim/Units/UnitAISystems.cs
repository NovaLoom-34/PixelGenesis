using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Phase 3 start: spatial index, path budget, per-species counts.
    public sealed class UnitIndexSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.UnitsThink;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            var w = ctx.Units;
            w.Paths.BeginTick();
            w.Index.Rebuild(w.Store);
            Array.Clear(w.SpeciesCount, 0, w.SpeciesCount.Length);
            var alive = w.Store.Alive;
            for (int k = 0; k < alive.Length; k++) w.SpeciesCount[w.Store.Species[alive[k]]]++;
        }
    }

    // Bölüm 3.8: utility neurons pick a task; units think every 10 ticks (4 in combat, 40 asleep).
    public sealed class UnitThinkSystem : ISimSystem
    {
        public const int ThinkNormal = 10, ThinkCombat = 4, ThinkSleep = 40;
        public const float MateRadius = 24f, HerdRadius = 40f;

        UnitWorld _w;
        readonly Func<int, int, bool> _isThreat, _isEnemy, _isPrey, _isMate, _isKin, _isFightingKin, _isStranger;

        public UnitThinkSystem()
        {
            _isThreat = IsThreat;
            _isEnemy = IsEnemy;
            _isFightingKin = IsFightingKin;
            _isStranger = IsStranger;
            _isPrey = IsPrey;
            _isMate = IsMate;
            _isKin = IsKin;
        }

        public SimPhase Phase => SimPhase.UnitsThink;
        public int Order => 10;

        public void Tick(in SimContext ctx)
        {
            _w = ctx.Units;
            var u = _w.Store;
            long tick = ctx.Clock.Tick;
            var laws = ctx.Nature.Laws;
            int divisions = laws.IsOn(ctx.Nature.LawSimulationDetail)
                ? Divisions(laws.Value(ctx.Nature.LawSimulationDetail)) : 1;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            var alive = u.Alive;
            for (int k = 0; k < alive.Length; k++)
            {
                int i = alive[k];
                if (!IsScheduledForThink(tick, u.Uid[i], divisions)) continue;
                if (u.State[i] != UnitStore.StateAlive || u.NextThink[i] > tick) continue;
                if (u.Has(i, UnitFlags.Stunned) || u.Has(i, UnitFlags.Immobile) || u.Has(i, UnitFlags.PlayerControlled) || u.Has(i, UnitFlags.Aboard))
                {
                    u.NextThink[i] = tick + ThinkNormal;
                    continue;
                }
                Think(i, tick, ref rng);
            }
        }

        public static int Divisions(float configured) => math.clamp((int)math.round(configured), 1, 4);

        // A unit stays in the same deterministic tick slice regardless of camera or runtime platform.
        public static bool IsScheduledForThink(long tick, long uid, int divisions)
        {
            divisions = math.clamp(divisions, 1, 4);
            return tick % divisions == uid % divisions;
        }

        void Think(int i, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var sp = u.SpeciesOf(i);
            var current = (UnitTask)u.Task[i];
            float sight = u.Stat(i, StatId.Sight);
            float hpRatio = u.Hp[i] / u.Stat(i, StatId.Hp);
            ulong nm = u.NeuronMask[i]; // fresh: Stat() above recomputed a dirty unit
            bool N(NeuronId n) => (nm & (1UL << (int)n)) != 0;

            var best = UnitTask.Wander;
            float bestScore = 10f;
            var bestTarget = EntityId.None;

            void Consider(UnitTask task, float score, EntityId target, ref SimRandom r)
            {
                if (score < bestScore || (score == bestScore && !r.Chance(0.5f))) return;
                best = task;
                bestScore = score;
                bestTarget = target;
            }

            // flee from stronger threats
            int threat = N(NeuronId.Flee) && (u.Has(i, UnitFlags.Afraid) || hpRatio < 0.9f || !sp.IsMonster) ? _w.Nearest(i, sight, _isThreat, UnitWorld.Among.Foreign) : -1;
            if (threat >= 0) Consider(UnitTask.Flee, (u.Has(i, UnitFlags.Afraid) ? 300f : 120f * (1.3f - hpRatio) * u.Stat(i, StatId.FleeThreshold)) * (N(NeuronId.Instinct) ? 1.5f : 1f), u.IdOf(threat), ref rng);
            if (threat >= 0 && N(NeuronId.TeleportHome)) TeleportHome(i, ref rng);

            // fight back / attack on sight
            var attacker = u.LastAttacker[i];
            bool fights = N(NeuronId.Attack);
            if (fights && u.IsAlive(attacker) && tick - u.LastAttackedTick[i] < 100 && math.distance(u.Pos[i], u.Pos[attacker.Index]) < sight * 1.5f)
                Consider(UnitTask.Attack, 90f, attacker, ref rng);
            int enemy = fights ? _w.Nearest(i, sight, _isEnemy, UnitWorld.Among.Foreign) : -1;
            if (enemy >= 0) Consider(UnitTask.Attack, N(NeuronId.Charge) ? 90f : 70f, u.IdOf(enemy), ref rng);
            if (fights && N(NeuronId.PackHunt)) // join a packmate's fight (+50)
            {
                int mate = _w.Nearest(i, PackRadius, _isFightingKin, UnitWorld.Among.Kin);
                if (mate >= 0) Consider(UnitTask.Attack, 110f, u.Target[mate], ref rng);
            }
            if (fights && N(NeuronId.GuardTerritory) && u.Home[i].x >= 0 && math.distance(u.Pos[i], u.Home[i]) < TerritoryRadius * 2f)
            {
                int stranger = _w.Nearest(i, TerritoryRadius, _isStranger, UnitWorld.Among.Foreign);
                if (stranger >= 0) Consider(UnitTask.Attack, 80f, u.IdOf(stranger), ref rng);
            }

            bool needs = !u.Has(i, UnitFlags.NoNeeds);
            var stage = (AgeStage)u.Age[i];
            // city residents eat from the store when hungry and only forage while it is empty (DECISIONS #53)
            if (needs && u.City[i] >= 0 && u.Army[i] < 0 && u.Saturation[i] < 60 && _w.Civ != null && _w.Civ.EatFromCarry(u, i) < 0) _w.Civ.EatFromStore(u, i);
            if (needs && N(NeuronId.Eat) && u.Saturation[i] < (u.City[i] >= 0 ? 35 : N(NeuronId.HoardFood) ? 50 : 70))
            {
                float hunger = (100 - u.Saturation[i]) * 1.2f * (u.Saturation[i] == 0 ? 2f : 1f);
                bool eatsPlants = sp.Diet == Diet.Herb || sp.Diet == Diet.Omni || u.City[i] >= 0; // citizens eat like omnivores (DECISIONS #59)
                float preySight = sight * (1f + 0.75f * (1f - u.Saturation[i] / 100f));
                int prey = sp.Diet != Diet.Herb && stage != AgeStage.Baby && N(NeuronId.Hunt) ? _w.Nearest(i, preySight, _isPrey, UnitWorld.Among.Foreign) : -1;
                if (prey >= 0 && !eatsPlants) Consider(UnitTask.Hunt, hunger * 1.1f, u.IdOf(prey), ref rng);
                else if (prey >= 0 && u.Saturation[i] < 25) Consider(UnitTask.Hunt, hunger, u.IdOf(prey), ref rng);
                else Consider(UnitTask.FindFood, hunger, EntityId.None, ref rng);
            }
            if (needs && N(NeuronId.Sleep) && u.Energy[i] < 40) Consider(UnitTask.Sleep, 100 - u.Energy[i], EntityId.None, ref rng);

            bool searchedHerdRange = false;
            if ((u.Wants[i] & 1) != 0 && stage == AgeStage.Adult && N(NeuronId.Mate))
            {
                int mate = u.IsAlive(u.Mate[i]) && IsMate(i, u.Mate[i].Index)
                    ? u.Mate[i].Index : _w.Nearest(i, MateRadius, _isMate, UnitWorld.Among.Kin);
                if (mate >= 0) Consider(UnitTask.Mate, 75f, u.IdOf(mate), ref rng);
                else
                {
                    // herd cohesion: drift toward the nearest of the kind so partners can meet
                    int kin = _w.Nearest(i, HerdRadius, _isKin, UnitWorld.Among.Kin);
                    searchedHerdRange = true;
                    if (kin >= 0) Consider(UnitTask.Follow, 30f, u.IdOf(kin), ref rng);
                }
            }
            // herd animals keep together even when not mating
            // A completed HerdRadius search already proves that the smaller HerdNear radius is empty.
            if (N(NeuronId.FollowHerd) && best == UnitTask.Wander && !searchedHerdRange && _w.Nearest(i, HerdNear, _isKin, UnitWorld.Among.Kin) < 0)
            {
                int kin = _w.Nearest(i, HerdRadius, _isKin, UnitWorld.Among.Kin);
                if (kin >= 0) Consider(UnitTask.Follow, 25f, u.IdOf(kin), ref rng);
            }
            if (N(NeuronId.FollowParent) && stage == AgeStage.Baby && u.IsAlive(u.Mother[i]) && math.distance(u.Pos[i], u.Pos[u.Mother[i].Index]) > 3f)
                Consider(UnitTask.Follow, 60f, u.Mother[i], ref rng);
            if (N(NeuronId.Rest) && u.Stamina[i] < 20f) Consider(UnitTask.Rest, 45f, EntityId.None, ref rng);
            // settlers without a city walk to their chosen site (DECISIONS #55)
            if (u.City[i] < 0 && u.Dest[i].x >= 0 && math.distance(u.Pos[i], u.Dest[i]) > 4f) Consider(UnitTask.Migrate, 50f, EntityId.None, ref rng);
            bool sapient = (_w.Abilities(i) & AbilityFlags.Sapient) != 0;
            if (u.City[i] < 0 && !sapient && u.Dest[i].x < 0 && stage >= AgeStage.Adult) Wanderlust(i, nm, ref rng); // explore, roam, go home
            if (u.City[i] < 0 && !sp.IsCiv && stage >= AgeStage.Adult && u.Dest[i].x < 0 &&
                best != UnitTask.Flee && best != UnitTask.Attack && best != UnitTask.Hunt && TryChooseMigration(i, ref rng))
                Consider(UnitTask.Migrate, 75f, EntityId.None, ref rng);
            if (N(NeuronId.StayHome) && u.City[i] >= 0 && _w.Civ != null && _w.Civ.CityAlive(u.City[i]) && math.distance(u.Pos[i], _w.Civ.City(u.City[i]).Center) > 12f)
                Consider(UnitTask.Work, 45f, EntityId.None, ref rng); // homebodies head back in
            if (N(NeuronId.Work) && u.City[i] >= 0 && stage >= AgeStage.Adult) Consider(UnitTask.Work, u.CarryAmount[i] > 0 || u.Army[i] >= 0 ? 70f : u.Job[i] > 0 ? 40f : 20f, EntityId.None, ref rng); // Bölüm 5.6 work neuron; loads get delivered first
            else if (u.City[i] >= 0 && _w.Civ != null && _w.Civ.CityAlive(u.City[i]) && math.distance(u.Pos[i], _w.Civ.City(u.City[i]).Center) > 24f)
                Consider(UnitTask.Work, 25f, EntityId.None, ref rng); // children drift home

            var mob = PathService.MobilityOf(u, i);
            int2 tile = u.Tile(i);
            if (!_w.Paths.CanStand(tile.x, tile.y, mob) && N(NeuronId.SwimToLand)) Consider(UnitTask.GoLand, 150f, EntityId.None, ref rng);
            else if (mob == Mobility.Amphibious && _w.Map.IsWater(tile.x, tile.y) && u.Stamina[i] < 30f && sp.Habitat != Habitat.Water)
                Consider(UnitTask.GoLand, 80f, EntityId.None, ref rng);

            // burning units run for water (status_effects.json: water ends st.burning)
            int2 water = new int2(-1);
            if (_w.StBurning >= 0 && u.HasStatus(i, _w.StBurning) && !u.Has(i, UnitFlags.Fly) && NearestWater(tile, out water))
                Consider(UnitTask.Douse, 200f, EntityId.None, ref rng);

            if (N(NeuronId.CastSpell) && ReadySpell(_w, i, enemy, out _, out _)) Consider(UnitTask.Cast, 65f, enemy >= 0 ? u.IdOf(enemy) : EntityId.None, ref rng);

            // Tasks in progress keep running unless something clearly more urgent came up.
            bool lowPriority = current == UnitTask.None || current == UnitTask.Wander || current == UnitTask.Rest || current == UnitTask.Follow || current == UnitTask.Work && best != UnitTask.Wander;
            bool switchTask = lowPriority ? best != current || current == UnitTask.None : bestScore >= 90f && best != current;
            if (switchTask) StartTask(i, best, bestTarget);
            if (switchTask && best == UnitTask.Douse) u.TargetTile[i] = water;

            bool combat = best == UnitTask.Attack || best == UnitTask.Hunt || best == UnitTask.Flee;
            int interval = (UnitTask)u.Task[i] == UnitTask.Sleep ? ThinkSleep : combat ? ThinkCombat : ThinkNormal;
            u.NextThink[i] = tick + interval + ((tick + i) % 3);
        }

        public const float PackRadius = 10f, TerritoryRadius = 6f, HerdNear = 8f, ExploreChance = 0.15f, RoamChance = 0.1f, HomeRange = 30f, TeleportChance = 0.25f;

        // explore (curious): 20-30 tiles; roam (loner): 12-20; return_home: back to the nest from afar (Bölüm 3.8.2)
        void Wanderlust(int i, ulong nm, ref SimRandom rng)
        {
            var u = _w.Store;
            int far = 0, near = 0;
            if ((nm & (1UL << (int)NeuronId.Explore)) != 0 && rng.Chance(ExploreChance)) { near = 20; far = 30; }
            else if ((nm & (1UL << (int)NeuronId.Roam)) != 0 && rng.Chance(RoamChance)) { near = 12; far = 20; }
            else if ((nm & (1UL << (int)NeuronId.ReturnHome)) != 0 && u.Home[i].x >= 0 && math.distance(u.Pos[i], u.Home[i]) > HomeRange)
            {
                u.Dest[i] = u.Home[i];
                return;
            }
            if (far == 0) return;
            var mob = PathService.MobilityOf(u, i);
            for (int t = 0; t < 8; t++)
            {
                float angle = rng.Range(0f, 6.2831853f);
                int2 d = u.Tile(i) + (int2)(new float2(math.cos(angle), math.sin(angle)) * rng.Range(near, far));
                if (!_w.Map.InBounds(d.x, d.y) || !_w.Paths.CanStand(d.x, d.y, mob)) continue;
                u.Dest[i] = d;
                return;
            }
        }

        // Wild adults leave a crowded or depleted zone only when an adjacent, reachable zone offers a clear food advantage.
        bool TryChooseMigration(int i, ref SimRandom rng)
        {
            var u = _w.Store;
            if (u.Has(i, UnitFlags.NoNeeds) || !rng.Chance(0.08f)) return false; // stagger decisions and avoid synchronized herds
            var sp = u.SpeciesOf(i);
            int2 tile = u.Tile(i);
            if (!_w.Map.InBounds(tile.x, tile.y)) return false;
            var map = _w.Map;
            int zone = map.ZoneIndexOf(tile.x, tile.y);
            int zx = zone % map.ZonesX, zy = zone / map.ZonesX;
            int localCount = _w.CountSpeciesInZone(u.Species[i], zone);
            float localFood = MigrationFood(i, zone);
            float carrying = math.max(6f, localFood / math.max(0.5f, sp.FoodNeed * 4f));
            bool crowded = localCount > carrying;
            bool hungry = u.Saturation[i] < 40 && localFood < math.max(4f, sp.FoodNeed * 8f);
            if (!crowded && !hungry) return false;

            var mobility = PathService.MobilityOf(u, i);
            int sourceX = math.clamp(tile.x, 0, map.Width - 1), sourceY = math.clamp(tile.y, 0, map.Height - 1);
            float currentScore = localFood / (1f + localCount * 0.5f);
            float bestScore = currentScore;
            int2 bestTile = new int2(-1, -1);
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = zx + ox, ny = zy + oy;
                    if ((uint)nx >= (uint)map.ZonesX || (uint)ny >= (uint)map.ZonesY) continue;
                    int targetZone = ny * map.ZonesX + nx;
                    int count = _w.CountSpeciesInZone(u.Species[i], targetZone);
                    float food = MigrationFood(i, targetZone);
                    float score = food / (1f + count * 0.5f);
                    if (score <= bestScore * 1.25f + 0.5f) continue;
                    if (!FindMigrationTile(sourceX, sourceY, nx, ny, mobility, out int2 target)) continue;
                    bestScore = score;
                    bestTile = target;
                }
            if (bestTile.x < 0) return false;
            u.Dest[i] = bestTile;
            return true;
        }

        float MigrationFood(int i, int zone)
        {
            var diet = _w.Store.SpeciesOf(i).Diet;
            float plants = _w.Map.Zones[zone].FoodBiomass;
            if (diet == Diet.Herb) return plants;
            float prey = _w.CountPreyInZone(i, zone) * math.max(1f, _w.Store.SpeciesOf(i).FoodNeed * 2f);
            if (diet == Diet.Carn || diet == Diet.Blood || diet == Diet.Brains) return prey;
            return plants + prey * 0.5f;
        }

        bool FindMigrationTile(int fromX, int fromY, int zoneX, int zoneY, Mobility mobility, out int2 result)
        {
            var map = _w.Map;
            int x0 = zoneX << WorldMap.ZoneShift, y0 = zoneY << WorldMap.ZoneShift;
            int2 center = new int2(x0 + WorldMap.ZoneSize / 2, y0 + WorldMap.ZoneSize / 2);
            result = new int2(-1, -1);
            float bestDistance = float.PositiveInfinity;
            for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                {
                    if (!_w.Paths.CanStand(x, y, mobility) || !_w.SameMobilityIsland(fromX, fromY, x, y, mobility)) continue;
                    float d = math.distancesq(new int2(x, y), center);
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    result = new int2(x, y);
                }
            return result.x >= 0;
        }

        // teleport_home: when danger comes and home is far, blink back
        void TeleportHome(int i, ref SimRandom rng)
        {
            var u = _w.Store;
            int2 home = u.Home[i];
            if (home.x < 0 && u.City[i] >= 0 && _w.Civ != null && _w.Civ.CityAlive(u.City[i])) home = _w.Civ.City(u.City[i]).Center;
            if (home.x < 0 || math.distance(u.Pos[i], home) < 20f || !rng.Chance(TeleportChance)) return;
            u.Pos[i] = (float2)home + 0.5f;
            _w.ClearPath(i);
        }

        public const int WaterSearchRadius = 16;

        bool NearestWater(int2 c, out int2 found)
        {
            var map = _w.Map;
            for (int r = 1; r <= WaterSearchRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        ushort f = map.Flags[map.Index(x, y)];
                        if ((f & (ushort)TileFlags.Water) == 0 || (f & (ushort)TileFlags.Walkable) == 0) continue;
                        found = new int2(x, y);
                        return true;
                    }
            found = default;
            return false;
        }

        void StartTask(int i, UnitTask task, EntityId target)
        {
            var u = _w.Store;
            u.Task[i] = (byte)task;
            u.Action[i] = 0;
            u.Timer[i] = 0;
            u.Target[i] = target;
            u.PathFails[i] = 0;
            _w.ClearPath(i);
            if (task != UnitTask.Sleep && _w.StSleeping >= 0) u.RemoveStatus(i, _w.StSleeping);
        }

        // First castable spell in slot order (species spells, then trait spells), with the slot that holds its cooldown.
        public static bool ReadySpell(UnitWorld w, int i, int enemy, out int spell, out int slot)
        {
            spell = -1;
            var u = w.Store;
            var sp = u.SpeciesOf(i);
            slot = 0;
            for (int k = 0; k < sp.SpellIds.Length && slot < UnitStore.SpellSlots; k++, slot++)
                if (Check(w, i, slot, sp.SpellIds[k], enemy)) { spell = sp.SpellIds[k]; return true; }
            var traits = u.Traits[i];
            for (int t = 0; !traits.IsEmpty && t < w.Content.UnitTraits.Count && slot < UnitStore.SpellSlots; t++)
            {
                if (!traits.Has(t)) continue;
                foreach (int s in w.Content.UnitTraits[t].SpellIds)
                {
                    if (slot >= UnitStore.SpellSlots) break;
                    if (Check(w, i, slot, s, enemy)) { spell = s; return true; }
                    slot++;
                }
            }
            // Bölüm 6.9: religion spells for priests, and for three believers in ten
            var meta = u.Meta;
            int rel = u.Religion[i];
            if (meta == null || slot >= UnitStore.SpellSlots || !meta.Religions.IsAlive(rel)) return false;
            bool priest = w.Civ != null && u.Job[i] - 1 == w.Civ.JobPriest && w.Civ.JobPriest >= 0;
            if (!priest && u.Uid[i] % 10 >= 3) return false;
            var faith = meta.Religions.Headers[rel].Traits;
            var defs = w.Content.ReligionTraits;
            for (int t = 0; !faith.IsEmpty && t < defs.Count && slot < UnitStore.SpellSlots; t++)
            {
                if (!faith.Has(t)) continue;
                foreach (int s in defs[t].SpellIds)
                {
                    if (slot >= UnitStore.SpellSlots) break;
                    if (Check(w, i, slot, s, enemy)) { spell = s; return true; }
                    slot++;
                }
            }
            return false;
        }

        static bool Check(UnitWorld w, int i, int slot, int spell, int enemy)
        {
            var u = w.Store;
            if (u.SpellCooldown[i * UnitStore.SpellSlots + slot] > 0) return false;
            var def = w.Content.Spells[spell];
            if (u.Mana[i] < def.ManaCost) return false;
            return SpellRegistry.Useful(w, i, def, enemy);
        }

        bool IsFightingKin(int self, int other)
        {
            var u = _w.Store;
            var task = (UnitTask)u.Task[other];
            return u.Species[self] == u.Species[other] && (task == UnitTask.Attack || task == UnitTask.Hunt) && u.IsAlive(u.Target[other]) && u.Target[other].Index != self;
        }

        // guard_territory: another species that can be fought (not undead, not much stronger)
        bool IsStranger(int self, int other)
        {
            var u = _w.Store;
            return u.Species[self] != u.Species[other] && !_w.IsUndead(other) && !u.Has(other, UnitFlags.Fly) && _w.Power(other) < _w.Power(self) * 1.5f;
        }

        bool IsThreat(int self, int other) =>
            (_w.Hostile(other, self) || _w.Prey(other, self)) && _w.Power(other) > _w.Power(self) * 1.2f;

        bool IsEnemy(int self, int other) => _w.Hostile(self, other) && !IsThreat(self, other);

        bool IsPrey(int self, int other) => _w.Prey(self, other) && _w.Power(other) < _w.Power(self) * 1.5f;

        bool IsKin(int self, int other) => _w.Store.Species[self] == _w.Store.Species[other];

        bool IsMate(int self, int other)
        {
            return UnitCourtship.CanPair(_w, self, other);
        }
    }

    // Phase 4: runs the current task's actions and moves the unit.
    public sealed class UnitActSystem : ISimSystem
    {
        public const int ChaseGiveUpTicks = 120; // two months
        public const int SeekGiveUpTicks = 900;  // walking to a mate / parent / herd across the map
        public const int FoodSearchRadius = 10;
        public const int GrazeSaturation = 12;
        public const int FarFoodRadius = 32;
        public const int NearFoodRadius = 12;
        public const int SleepEnergyPerTick = 1;

        UnitWorld _w;
        NatureState _nature;

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            _w = ctx.Units;
            _nature = ctx.Nature;
            var u = _w.Store;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            ref var combatRng = ref ctx.Rng.Get(RngStream.Combat);
            float dt = SimConst.TickDt;
            int count = u.Alive.Length; // units spawned during the loop act next tick
            for (int k = 0; k < count; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                if (u.AttackCooldown[i] > 0) u.AttackCooldown[i]--;
                for (int s = 0; s < UnitStore.SpellSlots; s++)
                    if (u.SpellCooldown[i * UnitStore.SpellSlots + s] > 0) u.SpellCooldown[i * UnitStore.SpellSlots + s]--;
                if (u.Has(i, UnitFlags.Stunned) || u.Has(i, UnitFlags.Aboard)) continue;

                bool done;
                switch ((UnitTask)u.Task[i])
                {
                    case UnitTask.Wander: done = Wander(i, dt, ref rng); break;
                    case UnitTask.FindFood: done = FindFood(i, dt, ref rng); break;
                    case UnitTask.Hunt:
                    case UnitTask.Attack: done = Fight(i, dt, tick, ref combatRng); break;
                    case UnitTask.Flee: done = Flee(i, dt); break;
                    case UnitTask.Sleep: done = Sleep(i); break;
                    case UnitTask.Mate: done = Mate(i, dt, tick, ref rng); break;
                    case UnitTask.Follow: done = Follow(i, dt); break;
                    case UnitTask.Rest: done = u.Stamina[i] >= 60f; break;
                    case UnitTask.GoLand: done = GoLand(i, dt); break;
                    case UnitTask.Migrate: done = Migrate(i, dt); break;
                    case UnitTask.Douse: done = _w.StBurning < 0 || !u.HasStatus(i, _w.StBurning) || MoveTo(i, u.TargetTile[i], dt, out bool lost) || lost; break;
                    case UnitTask.Cast: done = Cast(i, tick, ref combatRng); break;
                    case UnitTask.Work: done = _w.Civ == null || CivJobs.Work(_w, _w.Civ, i, dt, tick, ref rng); break;
                    default: done = false; break;
                }
                if (done)
                {
                    u.Task[i] = (byte)UnitTask.None;
                    u.Action[i] = 0;
                    _w.ClearPath(i);
                    if (u.NextThink[i] > tick + 1) u.NextThink[i] = tick + 1;
                }
            }
        }

        bool MoveTo(int i, int2 dest, float dt, out bool failed) => _w.MoveTo(i, dest, dt, out failed);

        bool Wander(int i, float dt, ref SimRandom rng)
        {
            var u = _w.Store;
            switch (u.Action[i])
            {
                case 0:
                    var mob = PathService.MobilityOf(u, i);
                    int2 here = u.Tile(i);
                    bool landLover = mob == Mobility.Fly && u.SpeciesOf(i).Habitat != Habitat.Water; // fliers roam over land
                    for (int t = 0; t < 8; t++)
                    {
                        int2 d = here + new int2(rng.Range(-6, 7), rng.Range(-6, 7));
                        if (!_w.Paths.CanStand(d.x, d.y, mob)) continue;
                        if (landLover && (!_w.Map.InBounds(d.x, d.y) || _w.Map.IsWater(d.x, d.y)) && t < 7) continue;
                        u.TargetTile[i] = d;
                        u.Action[i] = 1;
                        return false;
                    }
                    return true;
                case 1:
                    bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
                    if (failed) return true;
                    if (arrived)
                    {
                        u.Action[i] = 2;
                        u.Timer[i] = (short)rng.Range(20, 61);
                    }
                    return false;
                default:
                    return --u.Timer[i] <= 0;
            }
        }

        bool FindFood(int i, float dt, ref SimRandom rng)
        {
            var u = _w.Store;
            var sp = u.SpeciesOf(i);
            switch (u.Action[i])
            {
                case 0:
                    // city residents forage where they stand (roots, small game) and leave bushes to the gatherers (DECISIONS #53)
                    if (u.City[i] >= 0 && CanGraze(u.Tile(i))) // meat eaters catch small game
                    {
                        u.TargetTile[i] = u.Tile(i);
                        u.Action[i] = 2;
                        u.Timer[i] = 20;
                        return false;
                    }
                    if ((sp.Diet == Diet.Herb || sp.Diet == Diet.Omni) && FindPlant(i, out int2 plant))
                    {
                        u.TargetTile[i] = plant;
                        u.Action[i] = 1;
                        return false;
                    }
                    if (sp.Diet != Diet.Herb && FindCorpse(i, out int2 corpse))
                    {
                        u.TargetTile[i] = corpse;
                        u.Action[i] = 3;
                        return false;
                    }
                    if ((sp.Diet == Diet.Herb || sp.Diet == Diet.Omni) && CanGraze(u.Tile(i)))
                    {
                        u.TargetTile[i] = u.Tile(i);
                        u.Action[i] = 2;
                        u.Timer[i] = 20;
                        return false;
                    }
                    // nothing close: head for vegetated ground further away (sampled), else give up for a while
                    if (sp.Diet == Diet.Herb || sp.Diet == Diet.Omni)
                    {
                        var mob = PathService.MobilityOf(u, i);
                        int r = u.Has(i, UnitFlags.Fly) ? FarFoodRadius : NearFoodRadius; // walkers pay for A*, keep their trips short
                        for (int t = 0; t < 24; t++)
                        {
                            int2 d = u.Tile(i) + new int2(rng.Range(-r, r + 1), rng.Range(-r, r + 1));
                            if (!CanGraze(d) || !_w.Paths.CanStand(d.x, d.y, mob)) continue;
                            u.TargetTile[i] = d;
                            u.Action[i] = 1;
                            return false;
                        }
                    }
                    else
                    {
                        // meat eaters prowl: walk a stretch so prey can come into sight (standing still they starve)
                        var mob = PathService.MobilityOf(u, i);
                        for (int t = 0; t < 12; t++)
                        {
                            int radius = (int)math.round(math.lerp(MinProwlRadius, ProwlRadius, 1f - u.Saturation[i] / 100f));
                            int2 d = u.Tile(i) + new int2(rng.Range(-radius, radius + 1), rng.Range(-radius, radius + 1));
                            if (!_w.Map.InBounds(d.x, d.y) || !_w.Paths.CanStand(d.x, d.y, mob)) continue;
                            u.TargetTile[i] = d;
                            u.Action[i] = 5;
                            return false;
                        }
                    }
                    u.NextThink[i] += 20; // don't re-search every think
                    return true;
                case 5:
                    bool there = MoveTo(i, u.TargetTile[i], dt, out bool lost);
                    return there || lost;
                case 1:
                case 3:
                    bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
                    if (failed) return true;
                    if (arrived)
                    {
                        u.Action[i]++;
                        u.Timer[i] = 20;
                    }
                    return false;
                case 2:
                    if (--u.Timer[i] > 0) return false;
                    EatPlant(i, u.TargetTile[i]);
                    return true;
                default:
                    if (--u.Timer[i] > 0) return false;
                    EatCorpse(i, u.TargetTile[i]);
                    return true;
            }
        }

        public const int MinProwlRadius = 8, ProwlRadius = 28;

        bool FindPlant(int i, out int2 found)
        {
            var map = _w.Map;
            var u = _w.Store;
            int2 c = u.Tile(i);
            var mob = PathService.MobilityOf(u, i);
            // Among the plants of the nearest ring and the next one, take the least crowded (DECISIONS #111):
            // a herd no longer queues up on a single bush.
            int2 best = default;
            int bestCrowd = int.MaxValue, firstRing = -1, seen = 0;
            for (int r = 0; r <= FoodSearchRadius && seen < PlantCandidates; r++)
            {
                if (firstRing >= 0 && r > firstRing + 1) break;
                for (int dy = -r; dy <= r && seen < PlantCandidates; dy++)
                    for (int dx = -r; dx <= r && seen < PlantCandidates; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        ushort f = map.Feature[map.Index(x, y)];
                        int tileIndex = map.Index(x, y);
                        if (f == 0 || _w.FeatureNutrition[f] <= 0f || map.GetFeatureResource(tileIndex) <= 0 || !_w.Paths.CanStand(x, y, mob)) continue;
                        int crowd = _w.CountNear(new float2(x + 0.5f, y + 0.5f), PlantCrowdRadius, i);
                        if (crowd < bestCrowd) { best = new int2(x, y); bestCrowd = crowd; }
                        if (firstRing < 0) firstRing = r;
                        seen++;
                    }
            }
            found = best;
            return seen > 0;
        }

        public const int PlantCandidates = 6;
        public const float PlantCrowdRadius = 0.9f;

        // Grazing draws from a persistent biome forage reserve when no edible feature occupies the tile (DECISIONS #119).
        bool CanGraze(int2 tile)
        {
            var map = _w.Map;
            if (!map.InBounds(tile.x, tile.y)) return false;
            int index = map.Index(tile.x, tile.y);
            if (map.Feature[index] != 0 || map.GetFeatureResource(index) <= 0) return false;
            byte b = map.Biome[index];
            if (b == 0 || map.Tables.BiomeEffectCode[b] == (byte)BiomeEffect.NoPlants) return false;
            var biome = _w.Content.Biomes[b - 1];
            return biome.PlantValues.Length > 0 || biome.TreeValues.Length > 0;
        }

        bool FindCorpse(int i, out int2 found)
        {
            var u = _w.Store;
            var corpses = u.Corpses;
            float best = FoodSearchRadius * FoodSearchRadius;
            found = default;
            bool any = false;
            for (int k = 0; k < corpses.Length; k++)
            {
                int t = corpses[k].Tile;
                var p = new int2(t % _w.Map.Width, t / _w.Map.Width);
                float d = math.distancesq(p, u.Pos[i]);
                if (d >= best) continue;
                best = d;
                found = p;
                any = true;
            }
            return any;
        }

        void EatPlant(int i, int2 tile)
        {
            var map = _w.Map;
            var u = _w.Store;
            int ti = map.Index(tile.x, tile.y);
            ushort f = map.Feature[ti];
            int resource = map.GetFeatureResource(ti);
            float maxResource = map.Tables.FeatureResource[f];
            float food = maxResource <= 0f ? 0f : _w.FeatureNutrition[f] * (resource / maxResource);
            if (food <= 0f)
            {
                if (CanGraze(tile))
                {
                    u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + GrazeSaturation);
                    map.SetFeatureResource(tile.x, tile.y, math.max(0, map.GetFeatureResource(ti) - 8));
                }
                return;
            }
            u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + food * 10f);
            int res = resource - 1;
            byte kind = map.Tables.FeatureKind[f];
            if (res > 0) map.SetFeatureResource(tile.x, tile.y, res);
            else if (kind == FeatureDef.KindTree) map.SetFeatureResource(tile.x, tile.y, 0);
            else map.SetFeature(tile.x, tile.y, 0, ChangeSource.Unit);
        }

        void EatCorpse(int i, int2 tile)
        {
            var u = _w.Store;
            int ti = tile.y * _w.Map.Width + tile.x;
            var corpses = u.Corpses;
            for (int k = 0; k < corpses.Length; k++)
            {
                if (corpses[k].Tile != ti) continue;
                corpses.RemoveAt(k);
                u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + 40);
                return;
            }
        }

        bool Fight(int i, float dt, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var target = u.Target[i];
            if (!u.IsAlive(target))
            {
                if ((UnitTask)u.Task[i] == UnitTask.Hunt && u.Action[i] == 1) u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + 20 * (int)u.Stat(i, StatId.Size) + 20);
                return true;
            }
            int t = target.Index;
            if (++u.Timer[i] > ChaseGiveUpTicks) return true;

            float range = math.max(1f, u.Stat(i, StatId.Range));
            float dist = math.distance(u.Pos[i], u.Pos[t]);
            if (dist <= range + 0.5f)
            {
                _w.ClearPath(i);
                if (u.Pos[t].x != u.Pos[i].x) u.Facing[i] = (byte)(u.Pos[t].x < u.Pos[i].x ? 1 : 0);
                if (u.AttackCooldown[i] > 0) return false;
                u.AttackCooldown[i] = (short)math.max(2, (int)(20f / u.Stat(i, StatId.AtkSpd)));
                if (range > 1.5f) _w.LaunchProjectile(i, u.Pos[t], u.Stat(i, StatId.Dmg), 0f, false);
                else _w.MeleeHit(i, t, ref rng, tick);
                if (u.State[t] != UnitStore.StateAlive) u.Action[i] = 1; // killed: hunters eat
                return false;
            }

            // Chase: steer directly when close and the way is clear, otherwise path (re-planned when the target drifts).
            var mob = PathService.MobilityOf(u, i);
            float2 dir = (u.Pos[t] - u.Pos[i]) / math.max(dist, 1e-4f);
            int2 ahead = (int2)math.floor(u.Pos[i] + dir * 0.6f);
            if (dist < 8f && _w.Paths.CanStand(ahead.x, ahead.y, mob))
            {
                _w.ClearPath(i);
                _w.StepToward(i, ChaseSpot(i, t, range), dt);
                return false;
            }
            int2 goal = u.Tile(t);
            if (u.PathHandle[i] >= 0 && math.distance(u.TargetTile[i], goal) > 3f) _w.ClearPath(i);
            MoveTo(i, goal, dt, out bool failed);
            return failed && u.PathFails[i] >= 3;
        }

        // Attackers close in on their own spot around the victim, on their side of it, instead of piling on its centre (DECISIONS #113).
        float2 ChaseSpot(int i, int target, float range)
        {
            var u = _w.Store;
            float2 toMe = u.Pos[i] - u.Pos[target];
            float len = math.length(toMe);
            float2 side = len > 1e-3f ? toMe / len : new float2(1f, 0f);
            float turn = UnitSpacingSystem.Spread(u.Uid[i], 0xA77AC) * ChaseFanRadians;
            float2 fan = new float2(math.cos(turn), math.sin(turn));
            side = new float2(side.x * fan.x - side.y * fan.y, side.x * fan.y + side.y * fan.x);
            float2 spot = u.Pos[target] + side * math.max(1f, range * 0.8f);
            var mob = PathService.MobilityOf(u, i);
            return _w.Paths.CanStand((int)math.floor(spot.x), (int)math.floor(spot.y), mob) ? spot : u.Pos[target];
        }

        public const float ChaseFanRadians = 1.05f; // +-60 degrees
        public const float FleeFanRadians = 0.61f, FleeDistance = 10f, FleeDistanceSpread = 6f; // +-35 degrees, 10..16 tiles

        bool Flee(int i, float dt)
        {
            var u = _w.Store;
            if (u.Action[i] == 0)
            {
                var threat = u.Target[i];
                float2 away = u.IsAlive(threat) ? u.Pos[i] - u.Pos[threat.Index] : new float2(1f, 0f);
                float len = math.length(away);
                away = len > 1e-3f ? away / len : new float2(1f, 0f);
                // Every runner gets its own bearing and distance (DECISIONS #113) so a fleeing group fans out.
                float turn = UnitSpacingSystem.Spread(u.Uid[i], 0xF1EE) * FleeFanRadians;
                float2 fan = new float2(math.cos(turn), math.sin(turn));
                away = new float2(away.x * fan.x - away.y * fan.y, away.x * fan.y + away.y * fan.x);
                float distance = FleeDistance + (UnitSpacingSystem.Spread(u.Uid[i], 0xD157) + 1f) * 0.5f * FleeDistanceSpread;
                int2 dest = (int2)math.floor(u.Pos[i] + away * distance);
                dest = math.clamp(dest, 0, new int2(_w.Map.Width - 1, _w.Map.Height - 1));
                if (!_w.Paths.NearestStandable(dest, PathService.MobilityOf(u, i), out dest)) return true;
                u.TargetTile[i] = dest;
                u.Action[i] = 1;
            }
            bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
            return arrived || failed;
        }

        bool Sleep(int i)
        {
            var u = _w.Store;
            if (u.Action[i] == 0)
            {
                if (_w.StSleeping >= 0) u.AddStatus(i, _w.StSleeping, -1);
                u.Action[i] = 1;
            }
            if (_w.StSleeping >= 0 && !u.HasStatus(i, _w.StSleeping)) return true; // woken up
            // +1 energy per tick (doc: +0.3); a month is only 60 ticks, so the doc rate kept animals asleep most of the time (DECISIONS #42)
            u.Energy[i] = (byte)math.min(100, u.Energy[i] + SleepEnergyPerTick);
            if (u.Energy[i] < 100) return false;
            if (_w.StSleeping >= 0) u.RemoveStatus(i, _w.StSleeping);
            return true;
        }

        bool Mate(int i, float dt, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var mate = u.Target[i];
            if (!u.IsAlive(mate)) return true;
            int m = mate.Index;
            bool civilized = u.SpeciesOf(i).IsCiv;
            bool reciprocalCourtship = civilized && (UnitTask)u.Task[i] == UnitTask.Mate &&
                u.Target[i] == u.IdOf(m) && (UnitTask)u.Task[m] == UnitTask.Mate && u.Target[m] == u.IdOf(i);
            if ((u.Wants[i] & 1) == 0 && !reciprocalCourtship) return true;
            if (!UnitCourtship.CanPair(_w, i, m)) return true;
            if (u.Action[i] != 0 && math.distance(u.Pos[i], u.Pos[m]) > 1.2f)
            {
                u.Action[i] = 0;
                u.Timer[i] = 0;
            }
            if (u.Action[i] == 0)
            {
                if (math.distance(u.Pos[i], u.Pos[m]) > 1.2f)
                {
                    SeekUnit(i, m, dt);
                    return ++u.Timer[i] > SeekGiveUpTicks;
                }
                _w.ClearPath(i);
                u.Action[i] = 1;
                u.Timer[i] = UnitCourtship.DurationTicks;
                if (civilized)
                {
                    // Establish a reciprocal bond and let both adults take part in the same courtship.
                    bool alreadyBonded = UnitCourtship.HasLivingBond(u, i) && u.Mate[i] == u.IdOf(m);
                    u.Mate[i] = u.IdOf(m);
                    u.Mate[m] = u.IdOf(i);
                    if (!alreadyBonded) _w.Events?.Publish(new UnitPairedEvent(u.Uid[i], u.Uid[m]));
                    _w.ClearPath(m);
                    u.Task[m] = (byte)UnitTask.Mate;
                    u.Target[m] = u.IdOf(i);
                    u.Action[m] = 1;
                    u.Timer[m] = UnitCourtship.DurationTicks;
                }
                return false;
            }
            u.Facing[i] = (byte)(u.Pos[m].x < u.Pos[i].x ? 1 : 0);
            if (civilized && ((UnitTask)u.Task[m] != UnitTask.Mate || u.Target[m] != u.IdOf(i))) return true;
            if (--u.Timer[i] > 0) return false;
            int mother = u.Sex[i] == 0 ? i : m, father = mother == i ? m : i;
            UnitLife.Conceive(_w, mother, father, tick, ref rng);
            u.Wants[i] &= 0xFE;
            u.Wants[m] &= 0xFE;
            return true;
        }

        bool Follow(int i, float dt)
        {
            var u = _w.Store;
            var target = u.Target[i];
            if (!u.IsAlive(target)) return true;
            if (math.distance(u.Pos[i], u.Pos[target.Index]) <= 2f) return true;
            SeekUnit(i, target.Index, dt);
            return ++u.Timer[i] > SeekGiveUpTicks;
        }

        // Walk to another unit: straight when close and clear, else a path to its tile (re-planned when it drifts away).
        void SeekUnit(int i, int other, float dt)
        {
            var u = _w.Store;
            float dist = math.distance(u.Pos[i], u.Pos[other]);
            var mob = PathService.MobilityOf(u, i);
            float2 dir = (u.Pos[other] - u.Pos[i]) / math.max(dist, 1e-4f);
            int2 ahead = (int2)math.floor(u.Pos[i] + dir * 0.6f);
            if (dist < 6f && _w.Paths.CanStand(ahead.x, ahead.y, mob))
            {
                _w.ClearPath(i);
                _w.StepToward(i, u.Pos[other], dt);
                return;
            }
            int2 goal = u.Tile(other);
            if (u.PathHandle[i] >= 0 && math.distance(u.TargetTile[i], goal) > 4f) _w.ClearPath(i);
            MoveTo(i, goal, dt, out _);
        }

        bool Migrate(int i, float dt)
        {
            var u = _w.Store;
            if (u.City[i] >= 0 || u.Dest[i].x < 0) return true;
            bool arrived = MoveTo(i, u.Dest[i], dt, out bool failed);
            if (arrived || failed || ++u.Timer[i] > SeekGiveUpTicks * 2) { u.Dest[i] = new int2(-1, -1); return true; }
            return false;
        }

        bool GoLand(int i, float dt)
        {
            var u = _w.Store;
            if (u.Action[i] == 0)
            {
                int2 c = u.Tile(i);
                bool found = false;
                for (int r = 1; r <= 16 && !found; r++)
                    for (int dy = -r; dy <= r && !found; dy++)
                        for (int dx = -r; dx <= r && !found; dx++)
                        {
                            if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                            int x = c.x + dx, y = c.y + dy;
                            if (!_w.Map.InBounds(x, y) || !_w.Map.IsWalkable(x, y) || _w.Map.IsWater(x, y)) continue;
                            u.TargetTile[i] = new int2(x, y);
                            found = true;
                        }
                if (!found) return true;
                u.Action[i] = 1;
            }
            // straight swim: the path finder has no water class for walkers
            return _w.StepToward(i, (float2)u.TargetTile[i] + 0.5f, dt) || ++u.Timer[i] > ChaseGiveUpTicks;
        }

        bool Cast(int i, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            int enemy = u.IsAlive(u.Target[i]) ? u.Target[i].Index : -1;
            if (!UnitThinkSystem.ReadySpell(_w, i, enemy, out int spell, out int slot)) return true;
            var def = _w.Content.Spells[spell];
            u.Mana[i] -= def.ManaCost;
            u.SpellCooldown[i * UnitStore.SpellSlots + slot] = (ushort)math.max(1, def.CooldownTicks);
            SpellRegistry.Cast(_w, _nature, i, def, enemy, tick, ref rng);
            return true;
        }
    }
}
