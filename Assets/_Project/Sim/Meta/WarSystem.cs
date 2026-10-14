using System;
using PG.Content;
using Unity.Collections;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.5: yearly declarations and peace, monthly armies and city conquest.
    public sealed class WarSystem : ISimSystem
    {
        public const int PeaceBanYears = 10;
        public const int TreatyBreachOpinionPenalty = 30;
        public const float DeclareOpinion = -20f;
        public const float DefenderRadius = 10f, CaptureRadius = 3f, SiegeRadius = 14f;
        public const float RetreatShare = 0.3f;
        public const int MinArmy = 2;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 10;

        int _lawWars = -2;
        int[] _defenders = new int[64];     // per city: living soldiers (monthly)
        int[] _armyCount = new int[16];     // per army: living members (monthly)

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 3) return;
            var meta = ctx.Meta;
            var civ = ctx.Civ;
            if (_lawWars == -2) _lawWars = ctx.Nature.Laws.IndexOf("law.wars");
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            if (tick % SimConst.TicksPerYear == 3)
            {
                Peace(ctx, meta, tick, ref rng);
                if (_lawWars < 0 || ctx.Nature.Laws.IsOn(_lawWars)) Declarations(ctx, civ, meta, tick, ref rng);
            }
            CensusSoldiers(civ, meta, ctx.Units.Store);
            Armies(ctx, civ, meta, tick);
        }

        // --- declarations ---

        void Declarations(in SimContext ctx, CivWorld civ, MetaWorld meta, long tick, ref SimRandom rng)
        {
            var u = ctx.Units.Store;
            int n = meta.Kingdoms.Count;
            for (int a = 0; a < n; a++)
            {
                if (!meta.KingdomAlive(a)) continue;
                ref var ka = ref meta.Kingdom(a);
                if (ka.Capital < 0 || ka.Power < MinArmy || tick - ka.LastWarEndTick < PeaceBanYears * SimConst.TicksPerYear || OffensiveWar(meta, a)) continue;
                if ((meta.Kingdoms.Headers[a].Flags & MetaFlags.PlayerControlled) != 0) continue; // Bölüm 6.14: the player decides
                int best = -1;
                float bestScore = float.MinValue;
                var capA = CityPlanner.Below(civ.City(ka.Capital).Center);
                for (int b = 0; b < n; b++)
                {
                    if (b == a || !meta.KingdomAlive(b) || meta.AtWar(a, b)
                        || meta.HasTreaty(TreatyKind.NonAggression, a, b, tick) || meta.HasVassalage(a, b, tick)
                        || meta.Opinion(a, b) >= DeclareOpinion) continue;
                    ref var kb = ref meta.Kingdom(b);
                    if (kb.Capital < 0 || ka.Alliance >= 0 && ka.Alliance == kb.Alliance) continue;
                    var capB = CityPlanner.Below(civ.City(kb.Capital).Center);
                    if (!ctx.Regions.SameIsland(capA.x, capA.y, capB.x, capB.y, MoveClass.Land)) continue; // ships: Bölüm 5.10
                    float score = -meta.Opinion(a, b) + 30f * (ka.Power / math.max(1f, kb.Power) - 1f) - math.distance(capA, capB) / 10f;
                    if (u.IsAlive(ka.King) && meta.TrBloodthirsty >= 0 && u.Traits[ka.King.Index].Has(meta.TrBloodthirsty)) score += 20f;
                    if (meta.ReligionHas(ka.Religion, meta.RelHolyWar) && kb.Religion >= 0 && kb.Religion != ka.Religion) score += 30f; // Bölüm 6.9
                    if (score > bestScore) { bestScore = score; best = b; }
                }
                if (best < 0) continue;
                float chance = 1f / (1f + math.exp(-(bestScore - 40f) / 15f)) * (1f + meta.TraitValue(meta.TraitWarChance, a) / 100f);
                if (meta.CultureHas(ka.Culture, meta.CulPacifist)) chance *= 0.2f;
                if (!rng.Chance(chance)) continue;
                bool holy = meta.ReligionHas(ka.Religion, meta.RelHolyWar) && meta.Kingdom(best).Religion >= 0 && meta.Kingdom(best).Religion != ka.Religion;
                Declare(ctx, meta, a, best, holy ? meta.WarReligious : meta.WarConquest, -1, tick);
            }
        }

        static bool OffensiveWar(MetaWorld meta, int k)
        {
            for (int w = 0; w < meta.Wars.Count; w++)
                if (meta.Wars.IsAlive(w) && meta.Wars.Items[w].Attacker == k) return true;
            return false;
        }

        public static int Declare(in SimContext ctx, MetaWorld meta, int attacker, int defender, int type, int targetCity, long tick)
        {
            if (!meta.KingdomAlive(attacker) || !meta.KingdomAlive(defender) || attacker == defender
                || meta.AtWar(attacker, defender)
                || meta.HasVassalage(attacker, defender, tick) || type < 0 || type >= meta.Content.WarTypes.Count) return -1;
            bool treatyBreach = meta.HasTreaty(TreatyKind.NonAggression, attacker, defender, tick);
            if (treatyBreach)
            {
                meta.BreakTreaty(TreatyKind.NonAggression, attacker, defender, tick);
                meta.SetOpinion(defender, attacker, meta.Opinion(defender, attacker) - TreatyBreachOpinionPenalty);
            }
            var typeDef = meta.Content.WarTypes[type];
            WarCause cause = treatyBreach ? WarCause.TreatyBreach : DetermineCause(ctx.Civ, meta, attacker, defender, typeDef, tick);
            WarGoal goal = DetermineGoal(meta, attacker, defender, targetCity, cause, typeDef);
            var d = new WarData { Type = (ushort)type, Attacker = attacker, Defender = defender, StartTick = tick, TargetCity = targetCity, Result = WarResult.Ongoing };
            d.AttackerSide.Add(attacker);
            d.DefenderSide.Add(defender);
            // Bölüm 6.6: allies join when they dislike the other side
            JoinAllies(meta, ref d.AttackerSide, attacker, defender, tick);
            JoinAllies(meta, ref d.DefenderSide, defender, attacker, tick);
            int w = meta.Wars.Create(d, 0, tick);
            meta.Wars.Header(w).Color = meta.Kingdoms.Headers[attacker].Color;
            meta.SetWarPlan(w, cause, goal);
            meta.RebuildWarPairs();
            ctx.Events.Publish(new WarDeclaredEvent(w, attacker, defender, cause, goal));
            return w;
        }

        static WarCause DetermineCause(CivWorld civ, MetaWorld meta, int attacker, int defender, WarTypeDef type, long tick)
        {
            WarCause cause;
            if (type.Id == "war.rebellion" || type.Id == "war.independence") cause = WarCause.RebellionSuppression;
            else if (type.Id == "war.religious") cause = WarCause.ReligiousDifference;
            else if (type.Id == "war.world_war") cause = WarCause.AllianceCall;
            else if (type.Id == "war.succession") cause = WarCause.SuccessionDispute;
            else if (RecentlyLostWar(meta, attacker, defender, tick)) cause = WarCause.Revenge;
            else if (meta.Kingdom(attacker).Religion >= 0 && meta.Kingdom(defender).Religion >= 0
                && meta.Kingdom(attacker).Religion != meta.Kingdom(defender).Religion
                && meta.ReligionHas(meta.Kingdom(attacker).Religion, meta.RelHolyWar)) cause = WarCause.ReligiousDifference;
            else if (meta.Kingdom(attacker).Culture >= 0 && meta.Kingdom(defender).Culture >= 0
                && meta.Kingdom(attacker).Culture != meta.Kingdom(defender).Culture) cause = WarCause.CulturalDifference;
            else if (HasFamine(civ, attacker) || HasFamine(civ, defender)) cause = WarCause.ResourceShortage;
            else cause = WarCause.BorderDispute;

            if (Allows(type.Causes, cause)) return cause;
            if (type.Causes != null)
                foreach (string allowed in type.Causes)
                    if (TryCause(allowed, out var fallback)) return fallback;
            return WarCause.BorderDispute;
        }

        static WarGoal DetermineGoal(MetaWorld meta, int attacker, int defender, int targetCity, WarCause cause, WarTypeDef type)
        {
            WarGoal goal = targetCity >= 0 ? WarGoal.CityConquest
                : cause == WarCause.Revenge ? WarGoal.Tribute
                : cause == WarCause.ReligiousDifference ? WarGoal.Plunder
                : cause == WarCause.RebellionSuppression || cause == WarCause.SuccessionDispute ? WarGoal.CityConquest
                : meta.Kingdom(attacker).Power >= meta.Kingdom(defender).Power * 1.75f ? WarGoal.Vassalize
                : WarGoal.CityConquest;
            if (Allows(type.Goals, goal)) return goal;
            if (type.Goals != null)
                foreach (string allowed in type.Goals)
                    if (TryGoal(allowed, out var fallback)) return fallback;
            return WarGoal.CityConquest;
        }

        static bool Allows(string[] values, WarCause cause) => values != null && Array.IndexOf(values, CauseId(cause)) >= 0;
        static bool Allows(string[] values, WarGoal goal) => values != null && Array.IndexOf(values, GoalId(goal)) >= 0;

        static string CauseId(WarCause cause) => cause switch
        {
            WarCause.ResourceShortage => "resource_shortage", WarCause.ReligiousDifference => "religious_difference",
            WarCause.CulturalDifference => "cultural_difference", WarCause.Revenge => "revenge", WarCause.AllianceCall => "alliance_call",
            WarCause.RebellionSuppression => "rebellion_suppression", WarCause.SuccessionDispute => "succession_dispute",
            WarCause.TreatyBreach => "treaty_breach", _ => "border_dispute",
        };

        static bool TryCause(string id, out WarCause cause)
        {
            switch (id)
            {
                case "border_dispute": cause = WarCause.BorderDispute; return true;
                case "resource_shortage": cause = WarCause.ResourceShortage; return true;
                case "religious_difference": cause = WarCause.ReligiousDifference; return true;
                case "cultural_difference": cause = WarCause.CulturalDifference; return true;
                case "revenge": cause = WarCause.Revenge; return true;
                case "alliance_call": cause = WarCause.AllianceCall; return true;
                case "rebellion_suppression": cause = WarCause.RebellionSuppression; return true;
                case "succession_dispute": cause = WarCause.SuccessionDispute; return true;
                case "treaty_breach": cause = WarCause.TreatyBreach; return true;
                default: cause = WarCause.BorderDispute; return false;
            }
        }

        static string GoalId(WarGoal goal) => goal switch
        {
            WarGoal.Tribute => "tribute", WarGoal.Vassalize => "vassalize", WarGoal.Destruction => "destruction",
            WarGoal.Plunder => "plunder", _ => "city_conquest",
        };

        static bool TryGoal(string id, out WarGoal goal)
        {
            switch (id)
            {
                case "city_conquest": goal = WarGoal.CityConquest; return true;
                case "tribute": goal = WarGoal.Tribute; return true;
                case "vassalize": goal = WarGoal.Vassalize; return true;
                case "destruction": goal = WarGoal.Destruction; return true;
                case "plunder": goal = WarGoal.Plunder; return true;
                default: goal = WarGoal.CityConquest; return false;
            }
        }

        static bool HasFamine(CivWorld civ, int kingdom)
        {
            for (int c = 0; c < civ.CityCount; c++)
                if (civ.CityAlive(c) && civ.City(c).Kingdom == kingdom && civ.City(c).FamineMonths >= 2) return true;
            return false;
        }

        static bool RecentlyLostWar(MetaWorld meta, int attacker, int defender, long tick)
        {
            long lookback = 25L * SimConst.TicksPerYear;
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                if (meta.Wars.IsAlive(w)) continue;
                var past = meta.Wars.Items[w];
                if (past.EndTick <= 0 || tick - past.EndTick > lookback) continue;
                if (past.Attacker == attacker && past.Defender == defender && past.Result == WarResult.DefenderWon
                    || past.Attacker == defender && past.Defender == attacker && past.Result == WarResult.AttackerWon) return true;
            }
            return false;
        }

        public static float WarWeariness(in WarData war, MetaWorld meta, long tick)
        {
            float years = math.max(0f, (tick - war.StartTick) / (float)SimConst.TicksPerYear);
            int soldiers = 0;
            foreach (int k in war.AttackerSide) if (meta.KingdomAlive(k)) soldiers += meta.Kingdom(k).Soldiers;
            foreach (int k in war.DefenderSide) if (meta.KingdomAlive(k)) soldiers += meta.Kingdom(k).Soldiers;
            int losses = math.max(0, war.CasualtiesA) + math.max(0, war.CasualtiesD);
            float lossPressure = 65f * losses / math.max(1f, losses + soldiers);
            return math.clamp(years * 1.25f + lossPressure + math.min(20, war.CapturedCities * 5), 0f, 100f);
        }

        static void OfferPeace(in SimContext ctx, MetaWorld meta, int warIndex, WarData war, long tick)
        {
            int from = war.CasualtiesA > war.CasualtiesD ? war.Attacker : war.Defender;
            int to = from == war.Attacker ? war.Defender : war.Attacker;
            if (!meta.KingdomAlive(from) || !meta.KingdomAlive(to)
                || meta.HasProposal(ProposalKind.Peace, from, to) || meta.HasProposal(ProposalKind.Peace, to, from)) return;
            var terms = new PeaceTerms { NonAggressionYears = PeaceBanYears, CityToTransfer = -1 };
            bool attackerGoalEnforced = from == war.Defender && to == war.Attacker;
            if (attackerGoalEnforced && meta.TryGetWarPlan(warIndex, out var plan))
            {
                if (plan.Goal == WarGoal.Vassalize) terms.VassalizeProposer = true;
                if (plan.Goal == WarGoal.Tribute)
                {
                    long totalGold = 0;
                    for (int c = 0; c < ctx.Civ.CityCount; c++)
                        if (ctx.Civ.CityAlive(c) && ctx.Civ.KingdomOf(c) == from)
                            totalGold += Math.Max(0, ctx.Civ.City(c).Gold);
                    terms.TributeGold = (int)Math.Min(int.MaxValue, totalGold / 10);
                }
                if (plan.Goal == WarGoal.CityConquest && war.TargetCity >= 0
                    && ctx.Civ.CityAlive(war.TargetCity) && ctx.Civ.KingdomOf(war.TargetCity) == from)
                    terms.CityToTransfer = war.TargetCity;
            }
            var proposal = meta.AddPeaceProposal(from, to, tick, terms);
            ctx.Events.Publish(new DiplomacyProposalEvent(proposal.Id, from, to, ProposalKind.Peace));
        }

        static void JoinAllies(MetaWorld meta, ref Unity.Collections.FixedList64Bytes<int> side, int member, int enemy, long tick)
        {
            int al = meta.Kingdom(member).Alliance;
            if (al < 0 || !meta.Alliances.IsAlive(al)) return;
            var ks = meta.Alliances.Data(al).Kingdoms;
            foreach (int k in ks)
            {
                if (k == member || k == enemy || !meta.KingdomAlive(k) || side.Length >= side.Capacity
                    || meta.HasTreaty(TreatyKind.NonAggression, k, enemy, tick) || meta.HasVassalage(k, enemy, tick)) continue;
                if (meta.Opinion(k, enemy) < 0 && !side.Contains(k)) side.Add(k);
            }
        }

        // --- peace ---

        void Peace(in SimContext ctx, MetaWorld meta, long tick, ref SimRandom rng)
        {
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                if (!meta.Wars.IsAlive(w)) continue;
                var d = meta.Wars.Items[w];
                if (!meta.KingdomAlive(d.Attacker) || !meta.KingdomAlive(d.Defender))
                {
                    EndWar(ctx, meta, w, meta.KingdomAlive(d.Attacker) ? WarResult.AttackerWon : WarResult.DefenderWon, tick);
                    continue;
                }
                if (WarWeariness(d, meta, tick) >= 80f) OfferPeace(ctx, meta, w, d, tick);
                float years = (tick - d.StartTick) / (float)SimConst.TicksPerYear;
                int soldiers = meta.Kingdom(d.Attacker).Soldiers + meta.Kingdom(d.Defender).Soldiers;
                int lost = d.CasualtiesA + d.CasualtiesD;
                float chance = 0.05f + years * 0.02f + 0.3f * lost / math.max(1f, lost + soldiers);
                if (meta.Kingdom(d.Attacker).SuccessionTick > d.StartTick || meta.Kingdom(d.Defender).SuccessionTick > d.StartTick) chance *= 2f;
                if (rng.Chance(chance)) EndWar(ctx, meta, w, WarResult.Peace, tick);
            }
        }

        public static void EndWar(in SimContext ctx, MetaWorld meta, int w, WarResult result, long tick)
        {
            ref var d = ref meta.Wars.Data(w);
            d.Result = result;
            d.EndTick = tick;
            meta.Wars.MarkDead(w, tick);
            foreach (int k in d.AttackerSide) if (meta.KingdomAlive(k)) meta.Kingdom(k).LastWarEndTick = tick;
            foreach (int k in d.DefenderSide) if (meta.KingdomAlive(k)) meta.Kingdom(k).LastWarEndTick = tick;
            for (int a = 0; a < meta.Armies.Length; a++)
            {
                ref var army = ref meta.Armies.ElementAt(a);
                if (army.War == w && army.State != ArmyState.Disbanded) { army.State = ArmyState.Returning; army.StateTick = tick; }
            }
            for (int i = meta.Proposals.Count - 1; i >= 0; i--)
            {
                var proposal = meta.Proposals[i];
                bool oppositeSides = d.AttackerSide.Contains(proposal.From) && d.DefenderSide.Contains(proposal.To)
                    || d.AttackerSide.Contains(proposal.To) && d.DefenderSide.Contains(proposal.From);
                if (proposal.Kind != ProposalKind.Peace || !oppositeSides) continue;
                meta.Proposals.RemoveAt(i);
                ctx.Events.Publish(new DiplomacyProposalResolvedEvent(proposal.Id, proposal.From, proposal.To, proposal.Kind, false));
            }
            meta.RebuildWarPairs();
            ctx.Events.Publish(new PeaceEvent(w));
        }

        // --- armies ---

        void CensusSoldiers(CivWorld civ, MetaWorld meta, UnitStore u)
        {
            if (_defenders.Length < civ.CityCount) _defenders = new int[civ.CityCount * 2];
            if (_armyCount.Length < meta.Armies.Length) _armyCount = new int[meta.Armies.Length * 2];
            System.Array.Clear(_defenders, 0, _defenders.Length);
            System.Array.Clear(_armyCount, 0, _armyCount.Length);
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                int a = u.Army[i];
                if (a >= 0)
                {
                    bool valid = a < meta.Armies.Length && meta.Armies[a].State != ArmyState.Disbanded && civ.KingdomOf(u.City[i]) == meta.Armies[a].Kingdom;
                    if (valid) _armyCount[a]++;
                    else u.Army[i] = -1;
                }
                if (u.City[i] < 0 || !IsSoldier(civ, u, i)) continue;
                if (u.Army[i] < 0) _defenders[u.City[i]]++;
            }
        }

        public static bool IsSoldier(CivWorld civ, UnitStore u, int i)
        {
            int j = u.Job[i] - 1;
            return j >= 0 && (j == civ.JobWarrior || j == civ.JobGuard);
        }

        void Armies(in SimContext ctx, CivWorld civ, MetaWorld meta, long tick)
        {
            var u = ctx.Units.Store;
            // update the running ones
            for (int a = 0; a < meta.Armies.Length; a++)
            {
                ref var army = ref meta.Armies.ElementAt(a);
                if (army.State == ArmyState.Disbanded) continue;
                int now = _armyCount[a];
                if (army.War >= 0 && army.War < meta.Wars.Count && now < army.Soldiers)
                {
                    ref var war = ref meta.Wars.Data(army.War);
                    if (war.AttackerSide.Contains(army.Kingdom)) war.CasualtiesA += army.Soldiers - now;
                    else war.CasualtiesD += army.Soldiers - now;
                }
                army.Soldiers = now;
                bool warOn = army.War >= 0 && meta.Wars.IsAlive(army.War);
                if (now == 0 || !civ.CityAlive(army.HomeCity) || civ.City(army.HomeCity).Kingdom != army.Kingdom)
                {
                    Disband(meta, u, a, tick);
                    continue;
                }
                UpdateArmyLogistics(civ, meta, u, a);
                bool targetValid = warOn && civ.CityAlive(army.TargetCity) && meta.AtWar(army.Kingdom, civ.City(army.TargetCity).Kingdom);
                switch (army.State)
                {
                    case ArmyState.Gathering:
                        if (!targetValid) { army.State = ArmyState.Returning; army.StateTick = tick; }
                        else if (tick - army.StateTick >= SimConst.TicksPerMonth) { army.State = ArmyState.Marching; army.StateTick = tick; }
                        break;
                    case ArmyState.Marching:
                    case ArmyState.Sieging:
                        meta.TryGetArmyLogistics(a, out var logistics);
                        float retreatShare = RetreatShare + (100 - logistics.Morale) * 0.003f;
                        if (!targetValid || now < army.StartSoldiers * retreatShare) { army.State = ArmyState.Returning; army.StateTick = tick; break; }
                        if (army.State == ArmyState.Marching && Near(civ, u, a, army.TargetCity, SiegeRadius)) { army.State = ArmyState.Sieging; army.StateTick = tick; }
                        if (army.State == ArmyState.Sieging) TryCapture(ctx, civ, meta, a, ref army, tick);
                        break;
                    case ArmyState.Returning:
                        if (tick - army.StateTick >= 4 * SimConst.TicksPerMonth || Near(civ, u, a, army.HomeCity, 12f)) Disband(meta, u, a, tick);
                        break;
                }
            }
            // new campaigns: every warring kingdom keeps one army per war while it has warriors at home
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                if (!meta.Wars.IsAlive(w)) continue;
                var d = meta.Wars.Items[w];
                TryRaise(ctx, civ, meta, w, d.Attacker, tick);
                if (tick - d.StartTick >= SimConst.TicksPerYear) TryRaise(ctx, civ, meta, w, d.Defender, tick); // defenders counterattack after a year
            }
        }

        bool Near(CivWorld civ, UnitStore u, int army, int city, float radius)
        {
            float2 c = CityPlanner.Below(civ.City(city).Center);
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (u.Army[i] == army && math.distance(u.Pos[i], c) < radius) return true;
            }
            return false;
        }

        static void Disband(MetaWorld meta, UnitStore u, int a, long tick)
        {
            ref var army = ref meta.Armies.ElementAt(a);
            army.State = ArmyState.Disbanded;
            army.StateTick = tick;
            meta.RemoveArmyLogistics(a);
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (u.Army[i] == a) u.Army[i] = -1;
            }
        }

        void TryRaise(in SimContext ctx, CivWorld civ, MetaWorld meta, int w, int k, long tick)
        {
            if (!meta.KingdomAlive(k)) return;
            for (int a = 0; a < meta.Armies.Length; a++)
            {
                var ar = meta.Armies[a];
                if (ar.State != ArmyState.Disbanded && ar.Kingdom == k && ar.War == w) return;
            }
            var u = ctx.Units.Store;
            // target: nearest, weakest enemy city reachable by land (Bölüm 6.5)
            int home = -1, target = -1;
            float best = float.MaxValue;
            for (int e = 0; e < civ.CityCount; e++)
            {
                if (!civ.CityAlive(e) || !meta.AtWar(k, civ.City(e).Kingdom)) continue;
                var te = CityPlanner.Below(civ.City(e).Center);
                for (int c = 0; c < civ.CityCount; c++)
                {
                    if (!civ.CityAlive(c) || civ.City(c).Kingdom != k || _defenders[c] < 1) continue;
                    var tc = CityPlanner.Below(civ.City(c).Center);
                    if (!ctx.Regions.SameIsland(tc.x, tc.y, te.x, te.y, MoveClass.Land)) continue;
                    float s = math.distance(tc, te) + 20f * (e < _defenders.Length ? _defenders[e] : 0);
                    if (s < best) { best = s; home = c; target = e; }
                }
            }
            if (home < 0) return;
            // soldiers: warriors of the kingdom's cities (guards stay home); captain = best warfare
            int slot = -1;
            for (int a = 0; a < meta.Armies.Length; a++)
                if (meta.Armies[a].State == ArmyState.Disbanded && tick - meta.Armies[a].StateTick > SimConst.TicksPerMonth) { slot = a; break; }
            if (slot < 0) { slot = meta.Armies.Length; meta.Armies.Add(default); }
            if (_armyCount.Length <= slot) System.Array.Resize(ref _armyCount, slot * 2 + 2);
            int count = 0, captain = -1;
            float homeDist = 60f * (1f + meta.TraitValue(meta.TraitArmySize, k) / 100f);
            var hc = CityPlanner.Below(civ.City(home).Center);
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (u.Army[i] >= 0 || u.Job[i] - 1 != civ.JobWarrior || civ.KingdomOf(u.City[i]) != k) continue;
                if (math.distance(u.Pos[i], hc) > homeDist) continue;
                u.Army[i] = slot;
                u.Action[i] = 0;
                count++;
                if (captain < 0 || u.Stat(i, StatId.Warfare) > u.Stat(captain, StatId.Warfare)) captain = i;
            }
            if (count < MinArmy)
            {
                for (int n = 0; n < u.Alive.Length; n++) if (u.Army[u.Alive[n]] == slot) u.Army[u.Alive[n]] = -1;
                meta.Armies[slot] = new ArmyData { State = ArmyState.Disbanded, StateTick = tick, War = -1, Kingdom = -1 };
                return;
            }
            meta.Armies[slot] = new ArmyData
            {
                Kingdom = k, War = w, HomeCity = home, TargetCity = target, Captain = u.IdOf(captain), RallyTile = hc,
                State = ArmyState.Gathering, Soldiers = count, StartSoldiers = count, StateTick = tick,
            };
            _armyCount[slot] = count;
            meta.SetArmyLogistics(slot, 0, 100);
        }

        public static byte AdvanceMorale(int morale, int supplied, int soldiers)
        {
            int next = morale + (soldiers <= 0 || supplied >= soldiers ? 2 : -5);
            return (byte)math.clamp(next, 0, 100);
        }

        public static int SiegeMonthsRequired(int fortification, float armyPower)
        {
            float breach = math.clamp(armyPower / 10f, 0.5f, 2f);
            return math.max(2, (int)math.ceil((2f + math.max(0, fortification) * 2f) / breach));
        }

        public static int PlunderGoldAmount(int cityGold) => math.max(0, cityGold) / 4;

        public static int PlunderRationsAmount(int availableFood, int carryingCapacity)
            => math.min(math.max(0, availableFood), math.max(0, carryingCapacity));

        static int CityFortification(CivWorld civ, int city)
        {
            int walls = civ.CountBuildings(city, civ.Content.Buildings.IdOrDefault("bld.wall"), true);
            int towers = civ.CountBuildings(city, civ.BldWatchtower, true);
            int gates = civ.CountBuildings(city, civ.Content.Buildings.IdOrDefault("bld.gate"), true);
            int points = walls + towers * 2 + gates * 3;
            int center = civ.City(city).CenterBuilding;
            if (center >= 0 && center < civ.Buildings.Length)
            {
                ushort hall = civ.Buildings[center].Def;
                for (int tier = 1; tier < civ.Halls.Length; tier++)
                    if (hall == civ.Halls[tier]) points += tier;
            }
            return points;
        }

        static float ArmySiegePower(UnitStore u, int armyIndex)
        {
            float total = 0f;
            int soldiers = 0;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (u.Army[i] != armyIndex) continue;
                total += math.max(0.1f, u.Stat(i, StatId.Dmg) + u.Stat(i, StatId.Armor) * 0.5f + u.Stat(i, StatId.Warfare) * 0.1f);
                soldiers++;
            }
            return soldiers > 0 ? total / soldiers : 10f;
        }

        static void UpdateArmyLogistics(CivWorld civ, MetaWorld meta, UnitStore u, int armyIndex)
        {
            ref var army = ref meta.Armies.ElementAt(armyIndex);
            if (!meta.TryGetArmyLogistics(armyIndex, out var logistics))
                logistics = new ArmyLogistics { Army = armyIndex, Morale = 100 };

            int soldiers = 0;
            float2 armyCenter = float2.zero;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (u.Army[i] != armyIndex) continue;
                soldiers++;
                armyCenter += u.Pos[i];
            }
            if (soldiers > 0) armyCenter /= soldiers;

            const float supplyRadius = 30f;
            int home = army.HomeCity;
            if (soldiers > 0 && civ.CityAlive(home) && math.distance(armyCenter, CityPlanner.Below(civ.City(home).Center)) <= supplyRadius)
            {
                int capacity = soldiers * 2;
                int wanted = math.max(0, capacity - logistics.Rations);
                for (int f = 0; f < civ.FoodByNutrition.Length && wanted > 0; f++)
                {
                    int resource = civ.FoodByNutrition[f];
                    int reserve = math.max(2, civ.City(home).Population / 20);
                    int available = math.max(0, civ.Res(home, resource) - reserve);
                    int taken = math.min(wanted, available);
                    if (taken <= 0 || !civ.TakeRes(home, resource, taken)) continue;
                    logistics.Rations += taken;
                    wanted -= taken;
                }
            }

            int supplied = 0;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (u.Army[i] != armyIndex) continue;
                if (u.Saturation[i] >= 65) { supplied++; continue; }
                if (logistics.Rations <= 0) continue;
                logistics.Rations--;
                u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + 35);
                supplied++;
            }
            logistics.Morale = AdvanceMorale(logistics.Morale, supplied, soldiers);
            meta.SetArmyLogistics(armyIndex, logistics.Rations, logistics.Morale, logistics.SiegeProgress);
        }

        // Bölüm 6.5 conquest: no defender soldier within 10 tiles of the center and an attacker at the center.
        void TryCapture(in SimContext ctx, CivWorld civ, MetaWorld meta, int armyIndex, ref ArmyData army, long tick)
        {
            var u = ctx.Units.Store;
            int c = army.TargetCity;
            int from = civ.City(c).Kingdom;
            float2 center = CityPlanner.Below(civ.City(c).Center);
            bool attackerIn = false, defenderIn = false;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                float d = math.distance(u.Pos[i], center);
                if (d > DefenderRadius) continue;
                int k = civ.KingdomOf(u.City[i]);
                if (k == from && IsSoldier(civ, u, i)) { defenderIn = true; continue; }
                if (k == army.Kingdom && d <= CaptureRadius) attackerIn = true;
            }
            if (!attackerIn) return;
            if (defenderIn) return;
            if (!meta.TryGetArmyLogistics(armyIndex, out var logistics)) logistics = new ArmyLogistics { Army = armyIndex, Morale = 100 };
            logistics.SiegeProgress += 1f;
            int required = SiegeMonthsRequired(CityFortification(civ, c), ArmySiegePower(u, armyIndex));
            meta.SetArmyLogistics(armyIndex, logistics.Rations, logistics.Morale, logistics.SiegeProgress);
            if (logistics.SiegeProgress < required) return;
            if (CompleteDestructionGoal(ctx, civ, meta, armyIndex, c, tick)) return;
            if (army.War >= 0 && army.War < meta.Wars.Count && meta.Wars.IsAlive(army.War)
                && army.Kingdom == meta.Wars.Items[army.War].Attacker
                && meta.TryGetWarPlan(army.War, out var plan) && plan.Goal == WarGoal.Plunder)
            {
                CompletePlunder(ctx, civ, meta, armyIndex, c, tick);
                return;
            }
            if (civ.City(c).Population == 0) { SettlementSystem.RuinCity(ctx, civ, c, tick); return; }
            Capture(ctx, civ, meta, c, army.Kingdom, army.War, tick);
            army.State = ArmyState.Returning;
            army.StateTick = tick;
        }

        public static bool CompleteDestructionGoal(in SimContext ctx, CivWorld civ, MetaWorld meta, int armyIndex, int cityIndex, long tick)
        {
            if ((uint)armyIndex >= (uint)meta.Armies.Length || !civ.CityAlive(cityIndex)) return false;
            var army = meta.Armies[armyIndex];
            if (army.State != ArmyState.Sieging) return false;
            int war = army.War;
            if ((uint)war >= (uint)meta.Wars.Count || !meta.Wars.IsAlive(war)
                || !meta.TryGetWarPlan(war, out var plan) || plan.Goal != WarGoal.Destruction) return false;

            var data = meta.Wars.Items[war];
            int cityKingdom = civ.City(cityIndex).Kingdom;
            if (!data.AttackerSide.Contains(army.Kingdom) || !data.DefenderSide.Contains(cityKingdom)
                || data.TargetCity >= 0 && data.TargetCity != cityIndex) return false;

            SettlementSystem.RuinCity(ctx, civ, cityIndex, tick);
            EndWar(ctx, meta, war, WarResult.AttackerWon, tick);
            return true;
        }

        static void CompletePlunder(in SimContext ctx, CivWorld civ, MetaWorld meta, int armyIndex, int cityIndex, long tick)
        {
            ref var army = ref meta.Armies.ElementAt(armyIndex);
            if (!meta.TryGetArmyLogistics(armyIndex, out var logistics)) logistics = new ArmyLogistics { Army = armyIndex, Morale = 100 };

            ref var city = ref civ.City(cityIndex);
            int gold = PlunderGoldAmount(city.Gold);
            city.Gold -= gold;
            civ.RecordGoldTransferOut(cityIndex, gold);
            if (civ.CityAlive(army.HomeCity))
            {
                ref var home = ref civ.City(army.HomeCity);
                int previousGold = home.Gold;
                home.Gold = (int)Math.Min(int.MaxValue, (long)home.Gold + gold);
                civ.RecordGoldTransferIn(army.HomeCity, (long)home.Gold - previousGold);
            }

            int capacity = math.max(0, army.Soldiers * 2 - logistics.Rations);
            int available = 0;
            int reserve = math.max(2, city.Population / 20);
            for (int f = 0; f < civ.FoodByNutrition.Length; f++)
            {
                int resource = civ.FoodByNutrition[f];
                available += math.max(0, civ.Res(cityIndex, resource) - reserve);
            }
            int wanted = PlunderRationsAmount(available, capacity);
            int rations = 0;
            for (int f = 0; f < civ.FoodByNutrition.Length && wanted > 0; f++)
            {
                int resource = civ.FoodByNutrition[f];
                int take = math.min(wanted, math.max(0, civ.Res(cityIndex, resource) - reserve));
                if (take <= 0 || !civ.TakeRes(cityIndex, resource, take)) continue;
                wanted -= take;
                rations += take;
            }
            logistics.Rations += rations;
            meta.SetArmyLogistics(armyIndex, logistics.Rations, logistics.Morale, logistics.SiegeProgress);
            city.Loyalty = math.max(0, city.Loyalty - 20);
            civ.Version++;
            ctx.Events.Publish(new CityPillagedEvent(cityIndex, army.Kingdom, gold, rations));
            WarSystem.EndWar(ctx, meta, army.War, WarResult.AttackerWon, tick);
        }

        public static void Capture(in SimContext ctx, CivWorld civ, MetaWorld meta, int c, int to, int war, long tick)
        {
            ref var city = ref civ.City(c);
            int from = city.Kingdom;
            city.Kingdom = to;
            city.Loyalty = 0;
            var u = ctx.Units.Store;
            if (civ.HapCityCaptured >= 0)
                foreach (int i in civ.CityResidents[c]) Happiness.Add(u, i, civ.HapCityCaptured);
            if (war >= 0 && war < meta.Wars.Count) meta.Wars.Data(war).CapturedCities++;
            civ.Version++;
            ctx.Events.Publish(new CityCapturedEvent(c, from, to));
            for (int a = 0; a < meta.Armies.Length; a++)
            {
                var armyData = meta.Armies[a];
                if (armyData.War != war || armyData.Kingdom != to || armyData.State == ArmyState.Disbanded) continue;
                if (!meta.TryGetArmyLogistics(a, out var morale)) morale = new ArmyLogistics { Army = a, Morale = 100 };
                meta.SetArmyLogistics(a, morale.Rations, math.min(100, morale.Morale + 12), morale.SiegeProgress);
            }
            if (war >= 0 && war < meta.Wars.Count && meta.Wars.IsAlive(war)
                && meta.TryGetWarPlan(war, out var plan) && plan.Goal == WarGoal.CityConquest)
            {
                var data = meta.Wars.Items[war];
                bool attackerCaptured = data.AttackerSide.Contains(to) && data.DefenderSide.Contains(from);
                if (attackerCaptured && (data.TargetCity < 0 || data.TargetCity == c)) OfferPeace(ctx, meta, war, data, tick);
            }
        }
    }
}
