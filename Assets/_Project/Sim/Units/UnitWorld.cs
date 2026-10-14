using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    public enum UnitTask : byte { None, Wander, FindFood, Hunt, Attack, Flee, Sleep, Mate, Follow, Rest, GoLand, Cast, Work, Migrate, Douse }

    public enum MoveResult : byte { Moving, Arrived, Waiting, Failed }
    public enum CombatImpactKind : byte { Melee, Ranged, Magic }

    public struct Projectile
    {
        public float2 From, To, Pos;
        public float Z, T, Duration;
        public int Owner;          // unit index
        public long OwnerUid;
        public float Damage;
        public float Area;         // > 0: splash radius
        public byte Ignite;        // 1 = sets tiles on fire at impact
        public CombatImpactKind ImpactKind;
    }

    // Everything units need besides the map: store, spatial index, paths, projectiles, cached content ids and shared rules.
    public sealed class UnitWorld : IDisposable
    {
        public const int HardUnitCap = 20000;
        public const int MaxProjectiles = 4096;

        public readonly UnitStore Store;
        public readonly SubspeciesStore Subspecies;
        public CivWorld Civ; // set by SimWorld (BÃ¶lÃ¼m 5)
        public MetaWorld Meta; // set by SimWorld (BÃ¶lÃ¼m 6)
        public readonly SpatialIndex Index;
        public readonly PathService Paths;
        public SeasonState Season = SeasonState.Neutral;
        public NativeList<Projectile> Projectiles;
        public readonly int[] SpeciesCount;
        readonly WorldMap _map;
        readonly ContentDB _content;
        readonly RegionGraph _regions;
        readonly WorldLaws _laws;

        // cached content ids (-1 when missing)
        public readonly int StStarving, StSleeping, StPregnant, StBreedingCooldown, StInEgg, StBurning, StWet, StFrozen, StStunned, StShielded,
                            StBlessed, StCursed, StHaste, StWeakened, StRooted, StInvisible, StCharmed, StInspired, StPoisoned, StAfraid;
        public readonly int TrVeteran;
        public readonly int LawAging, LawHunger, LawReproduction, LawPopulationCap, LawAnimalSpawn, LawMutantBox;
        public readonly int StIrradiated;
        public readonly int StTurning, SpRat, TrPlagueBearer, TrImmune, TrHollow, MatSilver, DisPlague, DisRotbite; // BÃ¶lÃ¼m 4.8
        public readonly byte BiomeSwampValue;
        public readonly byte[] TraitRarity;          // unit trait index -> 0 normal .. 3 legendary
        readonly int[] _charTraits;                  // "char" group: the personality a civilized unit is born with
        readonly int[][] _newTraitPool;              // by rarity: traits that can appear at birth (inheritChance > 0)
        public readonly float[] FeatureNutrition; // by feature map value, 0 = not food for plant eaters
        public readonly ushort BonesFeature;
        public readonly MetamorphosisRules Morph;   // BÃ¶lÃ¼m 4.9
        public readonly int TrGiant, SpMosquito, GeneChimera;
        public readonly int TrKingslayer, TrHero, TrScarred, TrLame, TrOneEye, TrSurvivor; // BÃ¶lÃ¼m 4.3 gained traits
        public TraitSet Discovered; // traits that have appeared naturally (BÃ¶lÃ¼m 8.9 discovery hook), saved in section 10

        public UnitWorld(WorldMap map, RegionGraph regions, ContentDB content, WorldLaws laws)
        {
            _map = map;
            _content = content;
            _regions = regions;
            _laws = laws;
            Store = new UnitStore(content);
            Subspecies = new SubspeciesStore(content);
            Store.SubspeciesMeta = Subspecies;
            Index = new SpatialIndex(map);
            Paths = new PathService(map, regions);
            Projectiles = new NativeList<Projectile>(64, Allocator.Persistent);
            SpeciesCount = new int[content.Species.Count];

            int S(string id) => content.StatusEffects.IdOrDefault(id);
            StStarving = S("st.starving"); StSleeping = S("st.sleeping"); StPregnant = S("st.pregnant");
            StBreedingCooldown = S("st.breeding_cooldown"); StInEgg = S("st.in_egg");
            StBurning = S("st.burning"); StWet = S("st.wet"); StFrozen = S("st.frozen"); StStunned = S("st.stunned");
            StShielded = S("st.shielded"); StBlessed = S("st.blessed"); StCursed = S("st.cursed"); StHaste = S("st.haste");
            StWeakened = S("st.weakened"); StRooted = S("st.rooted"); StInvisible = S("st.invisible"); StCharmed = S("st.charmed");
            StInspired = S("st.inspired"); StPoisoned = S("st.poisoned"); StAfraid = S("st.afraid");
            TrVeteran = content.UnitTraits.IdOrDefault("tr.veteran");
            LawAging = laws.IndexOf("law.aging");
            LawHunger = laws.IndexOf("law.hunger");
            LawReproduction = laws.IndexOf("law.reproduction");
            LawPopulationCap = laws.IndexOf("law.population_cap");
            LawAnimalSpawn = laws.IndexOf("law.animal_spawn");
            LawMutantBox = laws.IndexOf("law.mutant_box");
            StIrradiated = S("st.irradiated");
            StTurning = S("st.turning");
            SpRat = content.Species.IdOrDefault("sp.rat");
            TrPlagueBearer = content.UnitTraits.IdOrDefault("tr.plague_bearer");
            TrImmune = content.UnitTraits.IdOrDefault("tr.immune");
            TrHollow = content.UnitTraits.IdOrDefault("tr.hollow");
            MatSilver = content.Materials.IdOrDefault("mat.silver");
            TrGiant = content.UnitTraits.IdOrDefault("tr.giant");
            SpMosquito = content.Species.IdOrDefault("sp.mosquito_swarm");
            GeneChimera = content.Genes.IdOrDefault("gene.chimera");
            TrKingslayer = content.UnitTraits.IdOrDefault("tr.kingslayer"); TrHero = content.UnitTraits.IdOrDefault("tr.hero");
            TrScarred = content.UnitTraits.IdOrDefault("tr.scarred"); TrLame = content.UnitTraits.IdOrDefault("tr.lame");
            TrOneEye = content.UnitTraits.IdOrDefault("tr.one_eye"); TrSurvivor = content.UnitTraits.IdOrDefault("tr.survivor");
            Morph = new MetamorphosisRules(content);
            DisPlague = content.Diseases.IdOrDefault("dis_plague");
            DisRotbite = content.Diseases.IdOrDefault("dis_rotbite");
            BiomeSwampValue = content.Biomes.TryGet("bio.swamp", out var swamp) ? swamp.MapValue : (byte)0;
            TraitRarity = new byte[content.UnitTraits.Count];
            var pools = new System.Collections.Generic.List<int>[4];
            for (int r = 0; r < 4; r++) pools[r] = new System.Collections.Generic.List<int>();
            for (int t = 0; t < content.UnitTraits.Count; t++)
            {
                var def = content.UnitTraits[t];
                int r = def.Rarity == "legendary" ? 3 : def.Rarity == "epic" ? 2 : def.Rarity == "rare" ? 1 : 0;
                TraitRarity[t] = (byte)r;
                if (def.InheritChance > 0f) pools[r].Add(t);
            }
            var chars = new System.Collections.Generic.List<int>();
            for (int t = 0; t < content.UnitTraits.Count; t++) if (content.UnitTraits[t].Group == "char") chars.Add(t);
            _charTraits = chars.ToArray();
            _newTraitPool = new int[4][];
            for (int r = 0; r < 4; r++) _newTraitPool[r] = pools[r].ToArray();
            BonesFeature = content.Features.TryGet("feat.bones_pile", out var bones) ? bones.MapValue : (ushort)0;

            FeatureNutrition = new float[content.Features.Count + 1];
            for (int f = 0; f < content.Features.Count; f++)
            {
                var def = content.Features[f];
                FeatureNutrition[def.MapValue] = map.Tables.FeatureFoodValue[def.MapValue];
            }
        }

        public WorldMap Map => _map;
        public WorldLaws Laws => _laws;
        public const float SpawnJoinRadius = 20f;

        // Every unit creation outside birth goes through here: BÃ¶lÃ¼m 4.1 subspecies resolution.
        // reuseIsland: natural/worldgen spawns share one root subspecies per species and island.
        public EntityId Spawn(UnitSpawnRequest req, long tick, ref SimRandom rng, bool reuseIsland = false)
        {
            if (!Subspecies.IsAlive(req.Subspecies)) req.Subspecies = ResolveSubspecies(req.Species, req.Pos, tick, reuseIsland, ref rng);
            var id = Store.Spawn(req, tick, ref rng);
            // first-generation civilized units: a birth's new-trait roll plus a personality (DECISIONS #68); births use BÃ¶lÃ¼m 4.3
            if (req.Mother.IsNone && _content.Species[req.Species].IsCiv)
            {
                var traits = Store.Traits[id.Index];
                int t = rng.Chance(0.5f) ? RandomUnitTrait(ref rng) : -1;
                if (t >= 0) UnitLife.AddTrait(_content, ref traits, t);
                RollCharacter(ref traits, ref rng);
                Store.Traits[id.Index] = traits;
                UnitStats.Recompute(Store, id.Index);
                Store.Hp[id.Index] = Store.Stat(id.Index, StatId.Hp);
            }
            // BÃ¶lÃ¼m 6.1: a kin within 20 tiles lends its culture, language and religion
            int kin = -1;
            float best = SpawnJoinRadius * SpawnJoinRadius;
            for (int k = 0; k < Store.Alive.Length; k++)
            {
                int o = Store.Alive[k];
                if (o == id.Index || Store.Species[o] != req.Species || Store.Culture[o] < 0 && Store.Religion[o] < 0) continue;
                float d = math.distancesq(Store.Pos[o], req.Pos);
                if (d < best) { best = d; kin = o; }
            }
            if (kin >= 0)
            {
                Store.Culture[id.Index] = Store.Culture[kin];
                Store.Language[id.Index] = Store.Language[kin];
                Store.Religion[id.Index] = Store.Religion[kin];
            }
            return id;
        }

        // BÃ¶lÃ¼m 4.9 general rule: same slot, uid and name, new species at full health; metas are dropped.
        public ushort Transform(int i, int species, long tick, ref SimRandom rng)
        {
            var u = Store;
            ushort from = u.Species[i];
            u.Species[i] = (ushort)species;
            u.Subspecies[i] = ResolveSubspecies(species, u.Pos[i], tick, false, ref rng);
            u.City[i] = -1;
            u.Job[i] = 0;
            u.HomeBuilding[i] = -1;
            u.Army[i] = -1;
            u.Disease[i] = 0;
            u.DiseaseProgress[i] = 0;
            u.Task[i] = 0;
            u.Culture[i] = -1; u.Language[i] = -1; u.Religion[i] = -1; u.Clan[i] = -1; // BÃ¶lÃ¼m 4.9 meta loss
            if (u.FormerSpecies[i] == 0) u.FormerSpecies[i] = (ushort)(from + 1);
            u.TransformTick[i] = tick;
            if (TrHollow >= 0 && (_content.Species[species].Flags & UnitFlags.Undead) != 0)
            {
                var traits = u.Traits[i];
                UnitLife.AddTrait(_content, ref traits, TrHollow);
                u.Traits[i] = traits;
            }
            ClearPath(i);
            if (StTurning >= 0) u.RemoveStatus(i, StTurning);
            UnitStats.Recompute(u, i);
            u.Hp[i] = u.Stat(i, StatId.Hp);
            return from;
        }

        // BÃ¶lÃ¼m 4.9 zombie weaknesses: fire x2, holy x3, a silver weapon x1.5.
        float Weakness(int attacker, DeathCause cause)
        {
            if (cause == DeathCause.Burned) return 2f;
            if (cause == DeathCause.Divine) return 3f;
            if (attacker < 0 || Civ == null || MatSilver < 0 || Store.State[attacker] != UnitStore.StateAlive) return 1f;
            for (int s = 0; s < UnitStore.EquipSlots; s++)
            {
                int item = Store.Equip[attacker * UnitStore.EquipSlots + s];
                if (item >= 0 && item < Civ.Items.Length && Civ.Items[item].Material == MatSilver) return 1.5f;
            }
            return 1f;
        }

        bool[] _zombie;
        public bool IsZombie(int i) => IsZombieSpecies(Store.Species[i]);

        bool IsZombieSpecies(int species)
        {
            if (_zombie == null)
            {
                _zombie = new bool[_content.Species.Count];
                for (int s = 0; s < _zombie.Length; s++) _zombie[s] = _content.Species[s].Id.StartsWith("sp.zombie", System.StringComparison.Ordinal);
            }
            return _zombie[species];
        }

        int ResolveSubspecies(int species, float2 pos, long tick, bool reuseIsland, ref SimRandom rng)
        {
            // ponytail: linear scan over the living (the spatial index is stale between spawns of one brush stroke); spawns are rare
            int best = -1;
            float bestD = SpawnJoinRadius * SpawnJoinRadius;
            var alive = Store.Alive;
            for (int k = 0; k < alive.Length; k++)
            {
                int o = alive[k];
                if (Store.State[o] != UnitStore.StateAlive || Store.Species[o] != species || !Subspecies.IsAlive(Store.Subspecies[o])) continue;
                float d = math.distancesq(pos, Store.Pos[o]);
                if (d > bestD || (d == bestD && o > best)) continue;
                best = o;
                bestD = d;
            }
            if (best >= 0) return Store.Subspecies[best];

            int2 t = (int2)math.floor(pos);
            var cls = _content.Species[species].Habitat == Habitat.Water ? MoveClass.Water : MoveClass.Land;
            int island = _map.InBounds(t.x, t.y) ? _regions.IslandAt(t.x, t.y, cls) : -1;
            if (reuseIsland)
                for (int s = 0; s < Subspecies.Count; s++) // ponytail: linear scan, index by species if subspecies counts reach thousands
                {
                    var d = Subspecies.Meta.Items[s];
                    if (d.Species == species && d.OriginIsland == island && d.Parent < 0 && Subspecies.IsAlive(s)) return s;
                }
            int biome = _map.InBounds(t.x, t.y) ? _map.GetBiome(t.x, t.y) - 1 : -1;
            return Subspecies.CreateRoot(species, island, biome, tick, 0, _laws.IsOn(LawMutantBox), ref rng);
        }

        public AbilityFlags Abilities(int i)
        {
            int s = Store.Subspecies[i];
            return s >= 0 && s < Subspecies.Count ? Subspecies.Abilities(s) : AbilityFlags.None;
        }

        // BÃ¶lÃ¼m 4.3 new trait at birth: rarity N70 R22 E7 L1, then uniform within the rarity.
        public const float CharacterChance = 0.5f;

        // Civilized units get a personality half the time (DECISIONS #68); an inherited one counts.
        public void RollCharacter(ref TraitSet traits, ref SimRandom rng)
        {
            foreach (int t in _charTraits) if (traits.Has(t)) return;
            if (_charTraits.Length == 0 || !rng.Chance(CharacterChance)) return;
            int c = _charTraits[rng.Range(0, _charTraits.Length)];
            UnitLife.AddTrait(_content, ref traits, c);
            Discover(c);
        }

        public int RandomUnitTrait(ref SimRandom rng)
        {
            int roll = rng.Range(0, 100);
            int r = roll < 70 ? 0 : roll < 92 ? 1 : roll < 99 ? 2 : 3;
            for (; r >= 0; r--)
                if (_newTraitPool[r].Length > 0) return _newTraitPool[r][rng.Range(0, _newTraitPool[r].Length)];
            return -1;
        }
        public ContentDB Content => _content;

        // Status durations are months (status_effects.json); "/tick" damage means per second (DECISIONS #41).
        public static int StatusTicks(StatusEffectDef def) => def.DurationMonths <= 0 ? -1 : def.DurationMonths * SimConst.TicksPerMonth;

        public void AddStatus(int unit, int status)
        {
            if (status < 0) return;
            Store.AddStatus(unit, status, StatusTicks(_content.StatusEffects[status]));
        }

        // --- relations ---

        public bool IsUndead(int i) => Store.Has(i, UnitFlags.Undead);

        // a wants to attack b on sight (monsters, undead, attack_all). Hunting for food is Prey().
        public bool Hostile(int a, int b)
        {
            if (a == b) return false;
            // hot path (BÃ¶lÃ¼m 9.4): same kind, no berserker, not citizens of two different cities -> never hostile below
            if (Store.Species[a] == Store.Species[b] && (Store.Flags[a] & (uint)UnitFlags.AttackAll) == 0)
            {
                int ca = Store.City[a], cb = Store.City[b];
                if (ca < 0 || cb < 0 || ca == cb) return false;
            }
            var sa = Store.SpeciesOf(a);
            var sb = Store.SpeciesOf(b);
            if (Store.Has(a, UnitFlags.Charmed) || Store.Has(b, UnitFlags.Untargetable) || Store.Has(a, UnitFlags.Aboard) || Store.Has(b, UnitFlags.Aboard)) return false;
            if (Store.Has(a, UnitFlags.AttackAll)) return true;
            if (Meta != null && Store.City[a] >= 0 && Store.City[b] >= 0 && Meta.AtWar(Civ.KingdomOf(Store.City[a]), Civ.KingdomOf(Store.City[b]))) return true; // BÃ¶lÃ¼m 6.5
            if (Store.Species[a] == Store.Species[b]) return false;
            if (Store.City[a] < 0 && Store.City[b] >= 0 && Store.Neuron(a, NeuronId.Raid)) return true; // raiding bands (BÃ¶lÃ¼m 3.8.2)
            if (sa.IsMonster && !sb.IsMonster) return true;
            if (IsUndead(a) && !IsUndead(b)) return true;
            return false;
        }

        public const int DesperateSaturation = 20;

        public bool Prey(int hunter, int prey)
        {
            if (hunter == prey || Store.Species[hunter] == Store.Species[prey] || Store.Has(prey, UnitFlags.Untargetable)) return false;
            int2 preyTile = (int2)math.floor(Store.Pos[prey]);
            if (Map.IsSpeciesProtectedAt(Store.Species[prey], preyTile.x, preyTile.y)) return false;
            var diet = Store.SpeciesOf(hunter).Diet;
            if (diet != Diet.Carn && diet != Diet.Omni && diet != Diet.Brains && diet != Diet.Blood) return false;
            if (IsUndead(prey)) return false;
            if (!Store.SpeciesOf(hunter).CanEatSpecies(Store.Species[prey])) return false;
            if (Store.City[prey] >= 0 && Store.City[hunter] < 0 && Store.Saturation[hunter] > DesperateSaturation) return false; // wild predators shun towns until starving (DECISIONS #76)
            return Store.Stat(prey, StatId.Size) <= Store.Stat(hunter, StatId.Size) + 0.5f;
        }

        public float Power(int i) => Store.Hp[i] * math.max(1f, Store.Stat(i, StatId.Dmg));

        // --- queries (BÃ¶lÃ¼m 3.3) ---

        // Rings of cells outward from the unit's cell; stops once the next ring cannot hold anything closer than the best
        // hit. Same result as scanning the whole radius (ties keep the lower index), so herd/mate searches with big radii
        // stay cheap in crowds (BÃ¶lÃ¼m 9.4).
        public enum Among : byte { Any, Foreign, Kin }

        // Foreign: the predicate needs another species or a hostile of any kind (threat/enemy/prey/stranger), so cells that
        // hold only the searcher's own species skip unless a berserker or (for a citizen) a townsfolk is inside.
        // Kin: the predicate needs the same species, so cells holding only another species skip. Both are exact.
        public int Nearest(int self, float radius, Func<int, int, bool> accept, Among among = Among.Any)
        {
            int species = Store.Species[self];
            bool selfBerserk = (Store.Flags[self] & (uint)UnitFlags.AttackAll) != 0, selfCitizen = Store.City[self] >= 0;
            float2 p = Store.Pos[self];
            Index.CellRange(p, radius, out int x0, out int y0, out int x1, out int y1);
            int ccx = (int)math.floor(p.x) >> PG.World.WorldMap.ZoneShift, ccy = (int)math.floor(p.y) >> PG.World.WorldMap.ZoneShift;
            const float z = PG.World.WorldMap.ZoneSize;
            int maxRing = math.max(math.max(ccx - x0, x1 - ccx), math.max(ccy - y0, y1 - ccy));
            int best = -1;
            float bestD = radius * radius;
            for (int r = 0; r <= maxRing; r++)
            {
                if (r > 0)
                {
                    float edge = math.min(math.min(p.x - (ccx - r + 1) * z, (ccx + r) * z - p.x), math.min(p.y - (ccy - r + 1) * z, (ccy + r) * z - p.y));
                    if (edge > 0f && edge * edge > bestD) break;
                }
                for (int cy = math.max(y0, ccy - r); cy <= math.min(y1, ccy + r); cy++)
                {
                    bool rowEdge = cy == ccy - r || cy == ccy + r;
                    int step = rowEdge ? 1 : 2 * r; // inner rows: only the ring's left and right cells
                    for (int cx = ccx - r; cx <= ccx + r; cx += math.max(1, step))
                    {
                        if (cx < x0 || cx > x1) continue;
                        int cell = cy * Index.CellsX + cx;
                        int kind = Index.CellKind[cell];
                        if (kind == SpatialIndex.EmptyCell) continue;
                        if (among == Among.Kin && kind != SpatialIndex.MixedCell && kind != species) continue;
                        if (among == Among.Foreign && kind == species && !selfBerserk)
                        {
                            byte mark = Index.CellMark[cell];
                            if ((mark & SpatialIndex.MarkAttackAll) == 0 && !(selfCitizen && (mark & SpatialIndex.MarkCitizen) != 0)) continue;
                        }
                        for (int k = Index.CellStart[cell]; k < Index.CellStart[cell + 1]; k++)
                        {
                            int o = Index.Sorted[k];
                            if (o == self || Store.State[o] != UnitStore.StateAlive) continue;
                            float d = math.distancesq(p, Store.Pos[o]);
                            if (d > bestD || (d == bestD && o > best)) continue;
                            if (!accept(self, o)) continue;
                            best = o;
                            bestD = d;
                        }
                    }
                }
            }
            return best;
        }

        // Living units within `radius` of a point (spatial index, so positions are as of the tick start).
        public int CountNear(float2 p, float radius, int exclude)
        {
            int count = 0;
            float rr = radius * radius;
            Index.CellRange(p, radius, out int x0, out int y0, out int x1, out int y1);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * Index.CellsX + cx;
                    for (int k = Index.CellStart[cell]; k < Index.CellStart[cell + 1]; k++)
                    {
                        int o = Index.Sorted[k];
                        if (o != exclude && Store.State[o] == UnitStore.StateAlive && math.distancesq(p, Store.Pos[o]) <= rr) count++;
                    }
                }
            return count;
        }

        // Whitelisted prey in a local radius; used by monthly predator fertility without scanning the world.
        public int CountPreyNear(int self, float radius)
        {
            var species = Store.SpeciesOf(self);
            if (species.PreySpecies == null || species.PreySpecies.Length == 0) return 0;
            float2 p = Store.Pos[self];
            float rr = radius * radius;
            Index.CellRange(p, radius, out int x0, out int y0, out int x1, out int y1);
            int count = 0;
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * Index.CellsX + cx;
                    for (int k = Index.CellStart[cell]; k < Index.CellStart[cell + 1]; k++)
                    {
                        int other = Index.Sorted[k];
                        if (other == self || Store.State[other] != UnitStore.StateAlive ||
                            !species.CanEatSpecies(Store.Species[other]) || math.distancesq(p, Store.Pos[other]) > rr) continue;
                        int2 preyTile = (int2)math.floor(Store.Pos[other]);
                        if (Map.IsSpeciesProtectedAt(Store.Species[other], preyTile.x, preyTile.y)) continue; // same rule as Prey()
                        count++;
                    }
                }
            return count;
        }

        // Counts one species in a zone using the tick's spatial index; callers run after UnitIndexSystem.
        public int CountSpeciesInZone(int species, int zone)
        {
            if ((uint)zone >= (uint)Index.CellKind.Length) return 0;
            int count = 0;
            for (int k = Index.CellStart[zone]; k < Index.CellStart[zone + 1]; k++)
            {
                int i = Index.Sorted[k];
                if (Store.State[i] == UnitStore.StateAlive && Store.Species[i] == species && Store.City[i] < 0) count++;
            }
            return count;
        }

        // Counts a predator's legal prey in one zone, reusing the stable per-tick spatial index.
        public int CountPreyInZone(int self, int zone)
        {
            if ((uint)zone >= (uint)Index.CellKind.Length) return 0;
            var species = Store.SpeciesOf(self);
            if (species.PreySpecies == null || species.PreySpecies.Length == 0) return 0;
            int count = 0;
            for (int k = Index.CellStart[zone]; k < Index.CellStart[zone + 1]; k++)
            {
                int i = Index.Sorted[k];
                if (Store.State[i] == UnitStore.StateAlive && species.CanEatSpecies(Store.Species[i])) count++;
            }
            return count;
        }

        public bool SameMobilityIsland(int x0, int y0, int x1, int y1, Mobility mobility)
        {
            if (mobility == Mobility.Fly || mobility == Mobility.Amphibious) return true;
            var cls = mobility == Mobility.Water ? MoveClass.Water : MoveClass.Land;
            return _regions.SameIsland(x0, y0, x1, y1, cls);
        }

        // --- combat (BÃ¶lÃ¼m 3.9) ---

        public bool MeleeHit(int attacker, int target, ref SimRandom rng, long tick)
        {
            if (!rng.Chance(1f - math.saturate(Store.Stat(target, StatId.Dodge) / 100f))) return false;
            float dmg = Store.Stat(attacker, StatId.Dmg) * rng.Range(0.85f, 1.15f);
            if (rng.Chance(Store.Stat(attacker, StatId.Crit) / 100f)) dmg *= 2f;
            CombatImpact(target, dmg, attacker, DeathCause.Killed, tick, CombatImpactKind.Melee);

            // knockback: up and away from the attacker
            float size = Store.Stat(target, StatId.Size);
            float kb = 2f * (Store.Stat(attacker, StatId.Dmg) / math.max(1f, size * 10f)) * (1f - math.saturate(Store.Stat(target, StatId.KnockbackResist)));
            if (Store.Neuron(attacker, NeuronId.Charge)) kb *= 2f; // sst.charge
            Store.VZ[target] += math.min(kb, 6f);
            float2 away = Store.Pos[target] - Store.Pos[attacker];
            float len = math.length(away);
            if (len > 1e-4f) Store.Vel[target] += away / len * math.min(kb, 6f) * 0.3f; // BÃ¶lÃ¼m 3.9: outward push while airborne

            // onHit statuses from attacker traits
            var traits = Store.Traits[attacker];
            if (!traits.IsEmpty)
                for (int t = 0; t < _content.UnitTraits.Count; t++)
                {
                    if (!traits.Has(t)) continue;
                    var def = _content.UnitTraits[t];
                    if (def.OnHitStatus >= 0 && rng.Chance(def.OnHitChance)) AddStatus(target, def.OnHitStatus);
                }
            // BÃ¶lÃ¼m 4.8 bite spread; zombies pass rotbite (zombie_rules.json)
            int bite = Store.Disease[attacker] - 1;
            if (bite >= 0 && (_content.Diseases[bite].SpreadMask & SpreadFlags.Bite) != 0 && rng.Chance(_content.Diseases[bite].Contagion))
                Diseases.Infect(this, target, bite, tick);
            else if (Store.Has(attacker, UnitFlags.Undead) && DisRotbite >= 0 && IsZombie(attacker))
            {
                if (Store.State[target] == UnitStore.StateAlive) { if (rng.Chance(_content.ZombieRules.BitePerHit)) Diseases.Infect(this, target, DisRotbite, tick); }
                else if (Store.State[target] == UnitStore.StateDying && !IsUndead(target) && rng.Chance(_content.ZombieRules.BiteOnKill))
                    Store.Disease[target] = (byte)(DisRotbite + 1); // the killing bite marks the corpse to rise (UnitStore.FlushDeaths)
            }
            else if (Store.Species[attacker] == SpMosquito && SpMosquito >= 0) // BÃ¶lÃ¼m 4.8 mosquito spread
                for (int d = 0; d < _content.Diseases.Count; d++)
                    if ((_content.Diseases[d].SpreadMask & SpreadFlags.Mosquito) != 0 && rng.Chance(_content.Diseases[d].Contagion)) { Diseases.Infect(this, target, d, tick); break; }
            float steal = Store.Stat(attacker, StatId.Lifesteal);
            if (steal > 0f) Heal(attacker, dmg * steal / 100f);
            return true;
        }

        public void Damage(int target, float amount, int attacker, DeathCause cause, long tick)
        {
            if (Store.State[target] != UnitStore.StateAlive) return;
            if (IsZombie(target)) amount *= Weakness(attacker, cause);
            float armor = Store.Stat(target, StatId.Armor);
            float final = math.max(1f, amount * (1f - armor / (armor + 50f)) * Store.Stat(target, StatId.DmgTaken));
            Store.Hp[target] -= final;
            if (attacker >= 0)
            {
                Store.LastAttacker[target] = Store.IdOf(attacker);
                Store.LastAttackedTick[target] = tick;
            }
            if (StSleeping >= 0 && Store.HasStatus(target, StSleeping)) Store.RemoveStatus(target, StSleeping);
            if (Store.Hp[target] > 0f)
            {
                // BÃ¶lÃ¼m 4.3: surviving below 10% scars; a blow of 30%+ maims 5% of the time (hash roll, no rng draw)
                float max = Store.Stat(target, StatId.Hp);
                if (Store.Hp[target] < max * 0.1f) GainTrait(target, TrScarred);
                if (attacker < 0 && Store.Hp[target] < max * 0.5f && (cause == DeathCause.Explosion || cause == DeathCause.Crushed || cause == DeathCause.Divine)) GainTrait(target, TrSurvivor);
                if (final >= max * 0.3f && (uint)(Store.Uid[target] * 31 + tick) % 100u < 5u) GainTrait(target, (Store.Uid[target] + tick) % 2 == 0 ? TrLame : TrOneEye);
                return;
            }

            Store.Kill(target, cause, attacker);
            if (attacker >= 0 && Store.State[attacker] == UnitStore.StateAlive) GainKill(attacker, target);
        }

        public void CombatImpact(int target, float amount, int attacker, DeathCause cause, long tick, CombatImpactKind kind)
        {
            if (Store.State[target] != UnitStore.StateAlive) return;
            float2 position = Store.Pos[target];
            Damage(target, amount, attacker, cause, tick);
            Events?.Publish(new UnitHitEvent(kind, position));
        }

        public void Heal(int i, float amount) => Store.Hp[i] = math.min(Store.Stat(i, StatId.Hp), Store.Hp[i] + amount);

        void GainKill(int killer, int victim)
        {
            Store.Kills[killer] = (ushort)math.min(ushort.MaxValue, Store.Kills[killer] + 1);
            GainXp(killer, Store.Level[victim] * 10 + 5);
            int weapon = Store.Equip[killer * UnitStore.EquipSlots + (int)EquipSlot.Weapon]; // the blade remembers (BÃ¶lÃ¼m 5.7)
            if (Civ != null && weapon >= 0 && weapon < Civ.Items.Length) { var item = Civ.Items[weapon]; item.Kills++; Civ.Items[weapon] = item; }
            if (Store.Kills[killer] >= 10) GainTrait(killer, TrVeteran);
            if (Store.Kills[killer] >= 100 && Store.Level[killer] >= 20) GainTrait(killer, TrHero);
            if (Meta != null)
                for (int k = 0; k < Meta.Kingdoms.Count; k++)
                    if (Meta.KingdomAlive(k) && Meta.Kingdom(k).King == Store.IdOf(victim)) { GainTrait(killer, TrKingslayer); break; }
        }

        // Adds a trait once (its opposite goes), refreshes stats and reports it (BÃ¶lÃ¼m 3.11, 4.3).
        public void GainTrait(int i, int trait)
        {
            if (trait < 0 || Store.Traits[i].Has(trait)) return;
            var t = Store.Traits[i];
            UnitLife.AddTrait(_content, ref t, trait);
            Store.Traits[i] = t;
            Store.StatsDirty[i] = 1;
            Discover(trait);
            Events?.Publish(new UnitTraitGainedEvent(Store.Uid[i], (ushort)trait));
        }

        public void Discover(int trait)
        {
            if (trait < 0 || Discovered.Has(trait)) return;
            Discovered.Set(trait, true);
            Events?.Publish(new TraitDiscoveredEvent((ushort)trait));
        }

        public void GainXp(int i, float amount)
        {
            Store.Xp[i] += (int)(amount * (1f + Store.Stat(i, StatId.Xp) / 100f));
            while (Store.Level[i] < UnitStats.MaxLevel && Store.Xp[i] >= 100f * math.pow(Store.Level[i], 1.5f))
            {
                Store.Level[i]++;
                Store.StatsDirty[i] = 1;
                Events?.Publish(new UnitLevelUpEvent(Store.Uid[i], Store.Level[i]));
            }
        }

        public EventBus Events;

        public bool LaunchProjectile(int owner, float2 to, float damage, float area, bool ignite,
            CombatImpactKind impactKind = CombatImpactKind.Ranged)
        {
            if (Projectiles.Length >= MaxProjectiles) return false;
            float2 from = Store.Pos[owner];
            float dist = math.distance(from, to);
            Projectiles.Add(new Projectile
            {
                From = from, To = to, Pos = from, Owner = owner, OwnerUid = Store.Uid[owner],
                Duration = math.max(0.2f, dist / 12f), Damage = damage, Area = area, Ignite = (byte)(ignite ? 1 : 0), ImpactKind = impactKind,
            });
            return true;
        }

        // --- movement (BÃ¶lÃ¼m 3.5) ---

        public MoveResult StartPath(int i, int2 dest)
        {
            var u = Store;
            Paths.Release(u.PathHandle[i]);
            u.PathHandle[i] = -1;
            u.PathStep[i] = 0;
            var status = Paths.Request(u.Tile(i), dest, PathService.MobilityOf(u, i), out int handle);
            switch (status)
            {
                case PathStatus.Ready:
                    u.PathHandle[i] = handle;
                    u.TargetTile[i] = Paths.Point(handle, Paths.Length(handle) - 1);
                    return MoveResult.Moving;
                case PathStatus.OverBudget: return MoveResult.Waiting;
                default:
                    u.PathFails[i]++;
                    return MoveResult.Failed;
            }
        }

        public void ClearPath(int i)
        {
            Paths.Release(Store.PathHandle[i]);
            Store.PathHandle[i] = -1;
            Store.PathStep[i] = 0;
        }

        // Follows the unit's path one tick; re-requests when the next tile became impassable.
        public MoveResult FollowPath(int i, float dt)
        {
            var u = Store;
            int h = u.PathHandle[i];
            if (h < 0) return MoveResult.Arrived;
            int len = Paths.Length(h);
            if (u.PathStep[i] >= len)
            {
                ClearPath(i);
                return MoveResult.Arrived;
            }
            var mob = PathService.MobilityOf(u, i);
            int2 next = Paths.Point(h, u.PathStep[i]);
            if (!Paths.CanStand(next.x, next.y, mob))
            {
                var dest = Paths.Point(h, len - 1);
                if (u.PathFails[i] >= 3) return MoveResult.Failed;
                u.PathFails[i]++;
                return StartPath(i, dest);
            }
            // Each unit wobbles around the tile centre (DECISIONS #110): a group no longer files along one line and
            // the last waypoint is a personal spot, so arriving units do not collapse onto one pixel.
            bool lastPoint = u.PathStep[i] >= len - 1;
            float2 waypoint = (float2)next + 0.5f + UnitSpacingSystem.SlotOffset(u.Uid[i], lastPoint ? ArrivalSlotRadius : WaypointWobble);
            if (StepToward(i, waypoint, dt, lastPoint ? ArrivalTolerance : 0f))
            {
                u.PathStep[i]++;
                if (u.PathStep[i] >= len)
                {
                    ClearPath(i);
                    u.PathFails[i] = 0;
                    return MoveResult.Arrived;
                }
            }
            return MoveResult.Moving;
        }

        public const float ArrivalSlotRadius = 0.35f, WaypointWobble = 0.15f, ArrivalTolerance = 0.3f;

        // Moves toward a point; true when reached this tick (or, with `arrive`, when already that close).
        public bool StepToward(int i, float2 point, float dt, float arrive = 0f)
        {
            var u = Store;
            if (u.Has(i, UnitFlags.Immobile) || u.Has(i, UnitFlags.Stunned) || u.Z[i] > 0.05f) return false;
            float2 pos = u.Pos[i];
            float2 d = point - pos;
            float dist = math.length(d);
            if (dist <= arrive) return true;
            float speed = SpeedAt(i, pos) * dt;
            if (dist <= speed || dist < 1e-4f)
            {
                u.Pos[i] = point;
                return true;
            }
            u.Pos[i] = pos + d / dist * speed;
            if (math.abs(d.x) > 0.01f) u.Facing[i] = (byte)(d.x < 0f ? 1 : 0);
            return false;
        }

        // tiles / second = speed * 1.5 / moveCost * (swimming 0.5)
        // Starts or continues a path to `dest`; true when arrived. Failure ends the task.
        public bool MoveTo(int i, int2 dest, float dt, out bool failed)
        {
            var u = Store;
            failed = false;
            if (u.PathHandle[i] < 0)
            {
                if (math.all(u.Tile(i) == dest)) return StepToward(i, (float2)dest + 0.5f + UnitSpacingSystem.SlotOffset(u.Uid[i], ArrivalSlotRadius), dt, ArrivalTolerance);
                var r = StartPath(i, dest);
                if (r == MoveResult.Failed) { failed = true; return false; }
                if (r == MoveResult.Waiting) return false;
            }
            var m = FollowPath(i, dt);
            if (m == MoveResult.Failed) failed = true;
            return m == MoveResult.Arrived;
        }

        public float SpeedAt(int i, float2 pos)
        {
            var u = Store;
            float speed = u.Stat(i, StatId.Speed) * 1.5f;
            if (u.Has(i, UnitFlags.Fly)) return speed;
            speed *= Season.MovementMul;
            int2 t = (int2)math.floor(pos);
            if (!_map.InBounds(t.x, t.y)) return speed;
            int ti = _map.Index(t.x, t.y);
            ushort flags = _map.Flags[ti];
            if ((flags & (ushort)TileFlags.Walkable) == 0) return speed * 0.5f * u.Stat(i, StatId.SwimSpeed); // swimming
            float cost = math.max(0.1f, _map.Tables.MoveCost[_map.Ground[ti]]);
            if ((flags & (ushort)TileFlags.Road) != 0) speed *= 1.3f;
            return speed / cost;
        }

        public void Dispose()
        {
            Store.Dispose();
            Subspecies.Dispose();
            Index.Dispose();
            Paths.Dispose();
            if (Projectiles.IsCreated) Projectiles.Dispose();
        }
    }
}
