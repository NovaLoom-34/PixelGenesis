using PG.Content;
using System;
using Unity.Collections;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.4 (monthly): kingdoms for new cities, names, census, kings and succession, city loyalty and rebellions,
    // opinions between kingdoms; yearly alliances (6.6).
    public sealed class KingdomSystem : ISimSystem
    {
        public const float RebellionChance = 0.05f;
        public const float MaxSuccessionCrisisChance = 0.35f;
        public const int AllianceOpinion = 25, LeaveAllianceOpinion = -30;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 0;

        int _lawRebellions = -2, _stHomesick = -2;

        public void Tick(in SimContext ctx)
        {
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth != 2) return; // after the economy (Bölüm 5 caches are fresh)
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var u = ctx.Units.Store;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            ref var names = ref ctx.Rng.Get(RngStream.Names);
            if (_lawRebellions == -2) _lawRebellions = ctx.Nature.Laws.IndexOf("law.kingdom_rebellions");

            NameUnits(civ, meta, u, ref names);
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                ref var city = ref civ.City(c);
                if (civ.Cities.Headers[c].NameId < 0) civ.Cities.Header(c).NameId = meta.CityName(CityPhonology(civ, meta, c), ref names);
                if (city.Kingdom < 0 || !meta.KingdomAlive(city.Kingdom)) Found(ctx, civ, meta, c, tick, ref names);
            }
            Census(ctx, civ, meta, u, tick);
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                Rulers(ctx, civ, meta, u, k, tick, ref rng);
            }
            Loyalty(ctx, civ, meta, u, tick, ref rng);
            Opinions(civ, meta, u, tick);
            meta.ExpireTreaties(tick);
            EvaluateProposals(ctx, meta, tick);
            if (tick % SimConst.TicksPerYear == 2)
            {
                Alliances(ctx, meta, tick);
                DiplomaticOffers(ctx, meta, tick);
                Migration(ctx, civ, meta, ref rng);
                Trade(ctx, civ, meta, ref rng);
                PayTribute(ctx, civ, meta);
            }
        }

        // Bölüm 10 (DECISIONS #98): the receiving side answers a proposal. AI kingdoms use a
        // deterministic rule; PlayerControlled kingdoms (player or governor) answer themselves.
        // Proposals expire after one year. Iteration order is stable, so this stays deterministic.
        public const int ProposalExpiryTicks = SimConst.TicksPerYear;
        public const int AcceptPeaceMinYears = 2, AcceptPeaceCasualties = 30, AcceptPeaceOpinion = 20;
        public const int TradeTreatyYears = 30, NonAggressionYears = 12;

        public static void EvaluateProposals(in SimContext ctx, MetaWorld meta, long tick)
        {
            for (int i = meta.Proposals.Count - 1; i >= 0; i--)
            {
                var p = meta.Proposals[i];
                if (!meta.KingdomAlive(p.From) || !meta.KingdomAlive(p.To))
                { meta.Proposals.RemoveAt(i); continue; }
                if (tick - p.Tick >= ProposalExpiryTicks)
                {
                    meta.Proposals.RemoveAt(i);
                    ctx.Events.Publish(new DiplomacyProposalResolvedEvent(p.Id, p.From, p.To, p.Kind, false));
                    continue;
                }
                ref var receiverFlags = ref meta.Kingdoms.Header(p.To).Flags;
                if ((receiverFlags & MetaFlags.PlayerControlled) != 0) continue; // player / governor answers

                bool accept;
                float trust = KingTraits(meta, ctx.Units.Store, meta.Kingdom(p.To).King) + meta.TraitValue(meta.TraitOpinion, p.To);
                float powerRatio = meta.Kingdom(p.To).Power / math.max(1f, meta.Kingdom(p.From).Power);
                if (p.Kind == ProposalKind.Peace)
                {
                    int war = meta.WarBetween(p.From, p.To);
                    float years = war >= 0 ? (tick - meta.Wars.Items[war].StartTick) / (float)SimConst.TicksPerYear : 0f;
                    int ourLosses = war >= 0 ? (meta.Wars.Items[war].Attacker == p.To ? meta.Wars.Items[war].CasualtiesA : meta.Wars.Items[war].CasualtiesD) : 0;
                    float concession = PeaceConcessionScore(ctx, meta, p, tick);
                    int opinion = meta.Opinion(p.To, p.From);
                    accept = years >= AcceptPeaceMinYears || ourLosses >= AcceptPeaceCasualties || opinion > AcceptPeaceOpinion
                        || concession >= 10f && opinion + trust >= -15;
                }
                else if (p.Kind == ProposalKind.Alliance)
                {
                    int op = meta.Opinion(p.To, p.From);
                    accept = !meta.AtWar(p.From, p.To) && !meta.HasVassalage(p.From, p.To, tick)
                        && (op + trust >= AllianceOpinion || (CommonEnemy(meta, p.To, p.From) && op + trust >= 0));
                }
                else if (p.Kind == ProposalKind.TradeAgreement)
                {
                    int op = meta.Opinion(p.To, p.From);
                    float threshold = powerRatio > 2f ? 15f : 10f;
                    accept = !meta.AtWar(p.From, p.To) && (op + trust >= threshold || CommonEnemy(meta, p.To, p.From) && op + trust >= 0);
                }
                else if (p.Kind == ProposalKind.TradeEmbargo)
                {
                    int op = meta.Opinion(p.To, p.From);
                    accept = !meta.AtWar(p.From, p.To) && op + trust <= -15;
                }
                else if (p.Kind == ProposalKind.Vassalage)
                {
                    int op = meta.Opinion(p.To, p.From);
                    accept = !meta.AtWar(p.From, p.To) && !meta.HasVassalage(p.From, p.To, tick)
                        && powerRatio <= 0.6f && op + trust >= -10;
                }
                else
                {
                    int op = meta.Opinion(p.To, p.From);
                    float threshold = powerRatio > 2f ? 5f : powerRatio < 0.5f ? -15f : -5f;
                    accept = !meta.AtWar(p.From, p.To) && op + trust >= threshold;
                }

                meta.Proposals.RemoveAt(i);
                if (accept) ApplyAcceptedProposal(ctx, meta, p, tick);
                ctx.Events.Publish(new DiplomacyProposalResolvedEvent(p.Id, p.From, p.To, p.Kind, accept));
            }
        }

        static float PeaceConcessionScore(in SimContext ctx, MetaWorld meta, DiplomacyProposal proposal, long tick)
        {
            var terms = proposal.PeaceTerms;
            float score = 0f;
            if (ctx.Civ != null && terms.TributeGold > 0)
            {
                long available = 0;
                for (int c = 0; c < ctx.Civ.CityCount; c++)
                    if (ctx.Civ.CityAlive(c) && ctx.Civ.KingdomOf(c) == proposal.From)
                        available += Math.Max(0, ctx.Civ.City(c).Gold);
                long payableGold = Math.Min((long)terms.TributeGold, available);
                score += math.min(15f, payableGold / 50f);
            }

            if (ctx.Civ != null && terms.CityToTransfer >= 0 && ctx.Civ.CityAlive(terms.CityToTransfer)
                && ctx.Civ.KingdomOf(terms.CityToTransfer) == proposal.From)
            {
                for (int c = 0; c < ctx.Civ.CityCount; c++)
                    if (c != terms.CityToTransfer && ctx.Civ.CityAlive(c) && ctx.Civ.KingdomOf(c) == proposal.From)
                    { score += 20f; break; }
            }

            if (terms.VassalizeProposer)
            {
                bool safe = true;
                int ancestor = proposal.To;
                for (int depth = 0; depth < meta.Kingdoms.Count && ancestor >= 0; depth++)
                {
                    if (ancestor == proposal.From) { safe = false; break; }
                    ancestor = meta.VassalOf(ancestor, tick);
                }
                if (safe) score += 30f;
            }

            score += math.min(10, math.max(0, terms.NonAggressionYears));
            return score;
        }

        public static void ApplyAcceptedProposal(in SimContext ctx, MetaWorld meta, DiplomacyProposal proposal, long tick)
        {
            if (proposal.Kind == ProposalKind.Peace)
            {
                int war = meta.WarBetween(proposal.From, proposal.To);
                if (war >= 0) WarSystem.EndWar(ctx, meta, war, WarResult.Peace, tick);
                ApplyPeaceTerms(ctx, ctx.Civ, meta, proposal, tick);
            }
            else if (proposal.Kind == ProposalKind.Alliance) Ally(ctx, meta, proposal.From, proposal.To, tick);
            else if (proposal.Kind == ProposalKind.TradeAgreement)
                meta.SetTreaty(TreatyKind.TradeAgreement, proposal.From, proposal.To, tick, TradeTreatyYears * SimConst.TicksPerYear);
            else if (proposal.Kind == ProposalKind.TradeEmbargo)
                meta.SetTreaty(TreatyKind.TradeEmbargo, proposal.From, proposal.To, tick, 10 * SimConst.TicksPerYear);
            else if (proposal.Kind == ProposalKind.NonAggression)
                meta.SetTreaty(TreatyKind.NonAggression, proposal.From, proposal.To, tick, NonAggressionYears * SimConst.TicksPerYear);
            else if (proposal.Kind == ProposalKind.Vassalage)
            {
                int subjectAlliance = meta.Kingdom(proposal.To).Alliance;
                if (subjectAlliance >= 0 && meta.Kingdom(proposal.From).Alliance == subjectAlliance)
                    LeaveAlliance(ctx, meta, proposal.To, tick);
                if (meta.SetVassal(proposal.To, proposal.From, tick, 0.05f))
                    ctx.Events.Publish(new VassalageFormedEvent(proposal.To, proposal.From));
            }
        }

        static void ApplyPeaceTerms(in SimContext ctx, CivWorld civ, MetaWorld meta, DiplomacyProposal proposal, long tick)
        {
            var terms = proposal.PeaceTerms;
            int from = proposal.From, to = proposal.To;
            if (!meta.KingdomAlive(from) || !meta.KingdomAlive(to)) return;

            if (terms.TributeGold > 0)
            {
                int capital = meta.Kingdom(to).Capital;
                if (civ.CityAlive(capital) && civ.KingdomOf(capital) == to)
                {
                    long creditRoom = Math.Max(0L, (long)int.MaxValue - civ.City(capital).Gold);
                    int payment = (int)Math.Min(terms.TributeGold, creditRoom);
                    int remaining = payment;
                    for (int c = 0; c < civ.CityCount && remaining > 0; c++)
                    {
                        if (!civ.CityAlive(c) || civ.KingdomOf(c) != from) continue;
                        int take = Math.Min(remaining, Math.Max(0, civ.City(c).Gold));
                        civ.City(c).Gold -= take;
                        civ.RecordGoldTransferOut(c, take);
                        remaining -= take;
                    }
                    int paid = payment - remaining;
                    civ.City(capital).Gold += paid;
                    civ.RecordGoldTransferIn(capital, paid);
                }
            }

            int transfer = terms.CityToTransfer;
            if (transfer >= 0 && civ.CityAlive(transfer) && civ.KingdomOf(transfer) == from)
            {
                int remainingCities = 0;
                for (int c = 0; c < civ.CityCount; c++)
                    if (c != transfer && civ.CityAlive(c) && civ.KingdomOf(c) == from) remainingCities++;
                if (remainingCities > 0)
                {
                    civ.City(transfer).Kingdom = to;
                    if (meta.Kingdom(from).Capital == transfer)
                    {
                        for (int c = 0; c < civ.CityCount; c++)
                            if (civ.CityAlive(c) && civ.KingdomOf(c) == from)
                            { meta.Kingdom(from).Capital = c; break; }
                    }
                    civ.Version++;
                }
            }

            if (terms.VassalizeProposer && meta.SetVassal(from, to, tick, 0.05f))
                ctx.Events.Publish(new VassalageFormedEvent(from, to));
            if (terms.NonAggressionYears > 0)
                meta.SetTreaty(TreatyKind.NonAggression, from, to, tick,
                    math.clamp(terms.NonAggressionYears, 1, 100) * SimConst.TicksPerYear);
        }

        // AI offers trade first, then embargoes or a non-aggression pact. The proposal is persisted and
        // resolved through the same path as peace/alliance; player-controlled receivers decide.
        static void DiplomaticOffers(in SimContext ctx, MetaWorld meta, long tick)
        {
            for (int a = 0; a < meta.Kingdoms.Count; a++)
            {
                if (!meta.KingdomAlive(a)) continue;
                for (int b = a + 1; b < meta.Kingdoms.Count; b++)
                {
                    if (!meta.KingdomAlive(b) || meta.AtWar(a, b)) continue;
                    bool aPlayer = (meta.Kingdoms.Headers[a].Flags & MetaFlags.PlayerControlled) != 0;
                    bool bPlayer = (meta.Kingdoms.Headers[b].Flags & MetaFlags.PlayerControlled) != 0;
                    if (aPlayer && bPlayer) continue;
                    if (meta.Kingdom(a).Alliance >= 0 && meta.Kingdom(a).Alliance == meta.Kingdom(b).Alliance) continue;
                    if (WantsAlliance(meta, a, b, tick) || meta.HasProposal(ProposalKind.Alliance, a, b) || meta.HasProposal(ProposalKind.Alliance, b, a)) continue;
                    int from = aPlayer ? b : a;
                    int to = aPlayer ? a : b;
                    if (!aPlayer && !bPlayer && meta.Kingdom(b).Power > meta.Kingdom(a).Power) { from = b; to = a; }

                    int mutualOpinion = math.min(meta.Opinion(a, b), meta.Opinion(b, a));
                    ProposalKind kind;
                    if (!meta.HasTreaty(TreatyKind.TradeAgreement, a, b, tick)
                        && !meta.HasTreaty(TreatyKind.TradeEmbargo, a, b, tick) && mutualOpinion >= 10)
                        kind = ProposalKind.TradeAgreement;
                    else if (!meta.HasTreaty(TreatyKind.TradeEmbargo, a, b, tick) && mutualOpinion <= -25)
                        kind = ProposalKind.TradeEmbargo;
                    else if (!meta.HasTreaty(TreatyKind.NonAggression, a, b, tick) && mutualOpinion >= -5
                        && math.max(meta.Opinion(a, b), meta.Opinion(b, a)) >= 0)
                        kind = ProposalKind.NonAggression;
                    else
                    {
                        if (meta.HasVassalage(a, b, tick) || meta.Kingdom(from).Power < meta.Kingdom(to).Power * 1.7f
                            || meta.Opinion(to, from) < -10) continue;
                        kind = ProposalKind.Vassalage;
                    }

                    if (meta.HasProposal(kind, from, to)) continue;
                    int id = meta.AddProposal(kind, from, to, tick);
                    if ((meta.Kingdoms.Headers[to].Flags & MetaFlags.PlayerControlled) != 0)
                        ctx.Events.Publish(new DiplomacyProposalEvent(id, from, to, kind));
                }
            }
        }

        static void PayTribute(in SimContext ctx, CivWorld civ, MetaWorld meta)
        {
            for (int i = 0; i < meta.Treaties.Count; i++)
            {
                var treaty = meta.Treaties[i];
                if (treaty.Kind != TreatyKind.Vassalage || !meta.KingdomAlive(treaty.A) || !meta.KingdomAlive(treaty.B)) continue;
                int subject = treaty.A, overlord = treaty.B;
                int destination = meta.Kingdom(overlord).Capital;
                if (!civ.CityAlive(destination) || civ.KingdomOf(destination) != overlord) continue;
                long available = 0;
                for (int c = 0; c < civ.CityCount; c++)
                    if (civ.CityAlive(c) && civ.KingdomOf(c) == subject) available += math.max(0, civ.City(c).Gold);
                if (available <= 0) continue;

                long rawRequest = (long)(available * math.clamp(treaty.TributeRate, 0.01f, 0.2f));
                if (rawRequest < 1) rawRequest = 1;
                int requested = rawRequest > int.MaxValue ? int.MaxValue : (int)rawRequest;
                int creditRoom = int.MaxValue - math.max(0, civ.City(destination).Gold);
                int payment = math.min(requested, creditRoom);
                int remaining = payment;
                for (int c = 0; c < civ.CityCount && remaining > 0; c++)
                {
                    if (!civ.CityAlive(c) || civ.KingdomOf(c) != subject) continue;
                    ref var city = ref civ.City(c);
                    int taken = math.min(math.max(0, city.Gold), remaining);
                    city.Gold -= taken;
                    civ.RecordGoldTransferOut(c, taken);
                    remaining -= taken;
                }
                int paid = payment - remaining;
                if (paid <= 0) continue;
                civ.City(destination).Gold += paid;
                civ.RecordGoldTransferIn(destination, paid);
                ctx.Events.Publish(new TributePaidEvent(subject, overlord, paid));
            }
        }

        static int CityPhonology(CivWorld civ, MetaWorld meta, int c)
        {
            int k = civ.City(c).Kingdom;
            return k >= 0 && k < meta.Kingdoms.Count ? meta.Kingdom(k).Phonology : meta.SpeciesPhonology[civ.City(c).Species];
        }

        // Sapient units get a name in their kingdom's sound (Bölüm 6.2; before languages every kingdom has a name set).
        static void NameUnits(CivWorld civ, MetaWorld meta, UnitStore u, ref SimRandom rng)
        {
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.NameId[i] >= 0 || u.City[i] < 0) continue;
                u.NameId[i] = meta.UnitName(CityPhonology(civ, meta, u.City[i]), ref rng); // ponytail: string per new citizen, monthly
            }
        }

        static void Found(in SimContext ctx, CivWorld civ, MetaWorld meta, int c, long tick, ref SimRandom rng)
        {
            ref var city = ref civ.City(c);
            var u = ctx.Units.Store;
            var king = u.IsAlive(city.Leader) ? city.Leader : EntityId.None;
            long uid = king.IsNone ? 0 : u.Uid[king.Index];
            int k = meta.CreateKingdom(c, city.Species, king, uid, tick, ref rng);
            city.Kingdom = k;
            city.Loyalty = 100;
            civ.Version++;
            ctx.Events.Publish(new KingdomFoundedEvent(k));
        }

        static void Census(in SimContext ctx, CivWorld civ, MetaWorld meta, UnitStore u, long tick)
        {
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                ref var d = ref meta.Kingdom(k);
                d.Cities = 0; d.Soldiers = 0; d.Power = 0f;
                meta.Kingdoms.Header(k).MemberCount = 0;
            }
            int towers = civ.BldWatchtower;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                int k = civ.City(c).Kingdom;
                if (k < 0 || !meta.KingdomAlive(k)) continue;
                ref var d = ref meta.Kingdom(k);
                d.Cities++;
                meta.Kingdoms.Header(k).MemberCount += civ.City(c).Population;
                if (towers >= 0) d.Power += civ.CountBuildings(c, towers, true) * 5f;
            }
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                int j = u.Job[i] - 1;
                if (j != civ.JobWarrior && j != civ.JobGuard) continue;
                int k = civ.KingdomOf(u.City[i]);
                if (k < 0 || !meta.KingdomAlive(k)) continue;
                ref var d = ref meta.Kingdom(k);
                d.Soldiers++;
                int armed = 0;
                for (int s = 0; s < UnitStore.EquipSlots; s++) if (u.Equip[i * UnitStore.EquipSlots + s] >= 0) armed++;
                d.Power += u.Level[i] * (1f + 0.25f * armed); // Bölüm 6.5 power: soldiers x level x equipment
            }
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                ref var d = ref meta.Kingdom(k);
                if (d.Cities == 0)
                {
                    meta.Kingdoms.MarkDead(k, tick);
                    EndWarsOf(ctx, meta, k, tick);
                    ctx.Events.Publish(new MetaDiedEvent(MetaKind.Kingdom, k));
                    continue;
                }
                if (d.Capital < 0 || !civ.CityAlive(d.Capital) || civ.City(d.Capital).Kingdom != k) d.Capital = LargestCity(civ, k);
            }
        }

        public static int LargestCity(CivWorld civ, int k)
        {
            int best = -1;
            for (int c = 0; c < civ.CityCount; c++)
                if (civ.CityAlive(c) && civ.City(c).Kingdom == k && (best < 0 || civ.City(c).Population > civ.City(best).Population)) best = c;
            return best;
        }

        static void EndWarsOf(in SimContext ctx, MetaWorld meta, int k, long tick)
        {
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                if (!meta.Wars.IsAlive(w)) continue;
                var d = meta.Wars.Items[w];
                if (d.Attacker != k && d.Defender != k) continue;
                WarSystem.EndWar(ctx, meta, w, d.Attacker == k ? WarResult.DefenderWon : WarResult.AttackerWon, tick);
            }
        }

        // Kings die, heirs follow (Bölüm 6.4: primogeniture until cultures bring their succession traits).
        static void Rulers(in SimContext ctx, CivWorld civ, MetaWorld meta, UnitStore u, int k, long tick, ref SimRandom rng)
        {
            ref var d = ref meta.Kingdom(k);
            if (!u.IsAlive(d.King) || civ.KingdomOf(u.City[d.King.Index]) != k)
            {
                EntityId formerKing = d.King;
                bool playerSelectedHeir = (meta.Kingdoms.Headers[k].Flags & MetaFlags.PlayerControlled) != 0
                    && u.IsAlive(d.Heir) && d.Heir != formerKing;
                int next = u.IsAlive(d.Heir) && civ.KingdomOf(u.City[d.Heir.Index]) == k ? d.Heir.Index : Successor(civ, meta, u, k, formerKing, d.Capital, tick);
                d.King = next >= 0 ? u.IdOf(next) : EntityId.None;
                if (next >= 0)
                {
                    d.SuccessionTick = tick;
                    ctx.Events.Publish(new NewKingEvent(k, u.Uid[next]));
                    if (!playerSelectedHeir)
                        TrySuccessionCrisis(ctx, civ, meta, u, k, formerKing, next, tick, ref rng);
                }
            }
            bool chosen = (meta.Kingdoms.Headers[k].Flags & MetaFlags.PlayerControlled) != 0 && u.IsAlive(d.Heir) && d.Heir != d.King; // Bölüm 8.13 heir pick
            int heir = chosen ? d.Heir.Index : u.IsAlive(d.King) ? Successor(civ, meta, u, k, d.King, d.Capital, tick) : -1;
            d.Heir = heir >= 0 && heir != d.King.Index ? u.IdOf(heir) : EntityId.None;
            d.RulerTitle = (ushort)d.Cities;
        }

        public static float SuccessionCrisisChance(int claimantCityLoyalty)
            => math.clamp(0.05f + math.clamp(50 - claimantCityLoyalty, 0, 100) * 0.003f,
                0.05f, MaxSuccessionCrisisChance);

        public static int FamineLoyaltyPenalty(int famineMonths) => math.min(20, math.max(0, famineMonths) * 4);

        static void TrySuccessionCrisis(in SimContext ctx, CivWorld civ, MetaWorld meta, UnitStore u, int kingdom,
            EntityId formerKing, int newKing, long tick, ref SimRandom rng)
        {
            if (formerKing.IsNone || newKing < 0 || meta.WarSuccession < 0) return;
            int claimant = -1;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (i == newKing || (AgeStage)u.Age[i] < AgeStage.Adult || u.Mother[i] != formerKing && u.Father[i] != formerKing) continue;
                int city = u.City[i];
                if (city < 0 || !civ.CityAlive(city) || civ.KingdomOf(city) != kingdom || city == meta.Kingdom(kingdom).Capital) continue;
                if (claimant < 0 || u.BirthTick[i] < u.BirthTick[claimant]) claimant = i;
            }
            if (claimant < 0) return;
            int claimantCity = u.City[claimant];
            if (!rng.Chance(SuccessionCrisisChance(civ.City(claimantCity).Loyalty))) return;

            int rebel = meta.CreateKingdom(claimantCity, civ.City(claimantCity).Species, u.IdOf(claimant), u.Uid[claimant],
                tick, ref ctx.Rng.Get(RngStream.Names));
            meta.Kingdom(rebel).LastWarEndTick = 0;
            meta.Kingdoms.Header(rebel).Banner.Shape = meta.Kingdoms.Headers[kingdom].Banner.Shape;
            civ.City(claimantCity).Kingdom = rebel;
            civ.City(claimantCity).Loyalty = 50;
            civ.Version++;
            WarSystem.Declare(ctx, meta, rebel, kingdom, meta.WarSuccession, claimantCity, tick);
            ctx.Events.Publish(new RebellionEvent(claimantCity, kingdom, rebel));
        }

        // Bölüm 6.4 succession by the kingdom culture's succ_* trait (primogeniture by default); capital leader as the last resort.
        static int Successor(CivWorld civ, MetaWorld meta, UnitStore u, int k, EntityId king, int capital, long tick)
        {
            string rule = SuccessionRule(meta, meta.Kingdom(k).Culture);
            int best = -1;
            switch (rule)
            {
                case "highest_diplo": best = BestSubject(civ, u, k, king, StatId.Diplo); break;
                case "highest_warfare": best = BestSubject(civ, u, k, king, StatId.Warfare); break;
                case "highest_intel": best = BestSubject(civ, u, k, king, StatId.Intel); break;
                case "oldest": break; // eldest adult of the capital below
                case "random":
                    int leaders = 0;
                    for (int c = 0; c < civ.CityCount; c++) if (civ.CityAlive(c) && civ.City(c).Kingdom == k && u.IsAlive(civ.City(c).Leader) && civ.City(c).Leader != king) leaders++;
                    int pick = leaders > 0 ? (int)(tick / SimConst.TicksPerMonth % leaders) : -1; // the lot: deterministic by date
                    for (int c = 0; c < civ.CityCount && pick >= 0; c++)
                        if (civ.CityAlive(c) && civ.City(c).Kingdom == k && u.IsAlive(civ.City(c).Leader) && civ.City(c).Leader != king && pick-- == 0) best = civ.City(c).Leader.Index;
                    break;
                default: best = Child(civ, u, k, king, rule == "youngest_child", rule == "eldest_daughter"); break;
            }
            if (best >= 0 || capital < 0) return best;
            foreach (int i in civ.CityResidents[capital])
            {
                if (king.Index == i || (AgeStage)u.Age[i] < AgeStage.Adult) continue;
                if (best < 0 || u.BirthTick[i] < u.BirthTick[best]) best = i;
            }
            if (best < 0 && u.IsAlive(civ.City(capital).Leader)) best = civ.City(capital).Leader.Index;
            return best;
        }

        static string SuccessionRule(MetaWorld meta, int culture)
        {
            if (!meta.Cultures.IsAlive(culture)) return "eldest_child";
            var traits = meta.Cultures.Headers[culture].Traits;
            var defs = meta.Content.CultureTraits;
            for (int t = 0; !traits.IsEmpty && t < defs.Count; t++)
                if (traits.Has(t) && defs[t].Leader != null) return defs[t].Leader;
            return "eldest_child";
        }

        static int Child(CivWorld civ, UnitStore u, int k, EntityId king, bool youngest, bool daughter)
        {
            if (king.IsNone) return -1;
            int best = -1;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (i == king.Index || u.State[i] != UnitStore.StateAlive || (AgeStage)u.Age[i] < AgeStage.Adult) continue;
                if (u.Mother[i] != king && u.Father[i] != king || daughter && u.Sex[i] != 0) continue;
                if (civ.KingdomOf(u.City[i]) != k) continue;
                if (best < 0 || (youngest ? u.BirthTick[i] > u.BirthTick[best] : u.BirthTick[i] < u.BirthTick[best])) best = i;
            }
            return best;
        }

        static int BestSubject(CivWorld civ, UnitStore u, int k, EntityId king, StatId stat)
        {
            int best = -1;
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                if (i == king.Index || (AgeStage)u.Age[i] < AgeStage.Adult || civ.KingdomOf(u.City[i]) != k) continue;
                if (best < 0 || u.Stat(i, stat) > u.Stat(best, stat)) best = i;
            }
            return best;
        }

        // Bölüm 6.4 loyalty; below 0 with law.kingdom_rebellions the city may break away (war.rebellion).
        void Loyalty(in SimContext ctx, CivWorld civ, MetaWorld meta, UnitStore u, long tick, ref SimRandom rng)
        {
            bool rebellions = _lawRebellions < 0 || ctx.Nature.Laws.IsOn(_lawRebellions);
            int cities = civ.CityCount;
            for (int c = 0; c < cities; c++)
            {
                if (!civ.CityAlive(c)) continue;
                ref var city = ref civ.City(c);
                int k = city.Kingdom;
                if (k < 0 || !meta.KingdomAlive(k)) continue;
                ref var kd = ref meta.Kingdom(k);
                float loyalty = 50f;
                if (u.IsAlive(kd.King)) loyalty += u.Stat(kd.King.Index, StatId.Diplo) * 3f;
                if (kd.Capital == c) loyalty += 100f;
                else if (kd.Capital >= 0) loyalty -= math.distance(city.Center, civ.City(kd.Capital).Center) / WorldMap.ZoneSize;
                loyalty += city.Subspecies == (kd.Capital >= 0 ? civ.City(kd.Capital).Subspecies : city.Subspecies) ? 5f : -10f;
                if (city.Culture >= 0 && kd.Culture >= 0) loyalty += city.Culture == kd.Culture ? 10f : -15f;   // Bölüm 6.7
                if (city.Religion >= 0 && kd.Religion >= 0) loyalty += city.Religion == kd.Religion ? 10f : -10f; // Bölüm 6.9
                if (u.IsAlive(kd.King) && u.IsAlive(city.Leader) && Related(u, kd.King, city.Leader)) loyalty += 10f;
                loyalty += city.Happiness / 5f + city.LoyaltyMod;
                city.LoyaltyMod -= math.clamp(city.LoyaltyMod, -10, 10);
                loyalty -= FamineLoyaltyPenalty(city.FamineMonths);
                loyalty -= math.min(20f, WarYears(meta, k, tick) * 3f);
                if (tick - kd.SuccessionTick < SimConst.TicksPerYear && kd.SuccessionTick > 0) loyalty -= 10f;
                if (loyalty < 0f) loyalty *= 1f - meta.TraitValue(meta.TraitRebelResist, k) / 100f;
                city.Loyalty = (int)loyalty;
                if (city.Loyalty >= 0 || !rebellions || kd.Capital == c || !rng.Chance(RebellionChance)) continue;
                Rebel(ctx, civ, meta, c, k, tick);
            }
        }

        static float WarYears(MetaWorld meta, int k, long tick)
        {
            float years = 0f;
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                if (!meta.Wars.IsAlive(w)) continue;
                var d = meta.Wars.Items[w];
                if (d.AttackerSide.Contains(k) || d.DefenderSide.Contains(k)) years += (tick - d.StartTick) / (float)SimConst.TicksPerYear;
            }
            return years;
        }

        public static int Rebel(in SimContext ctx, CivWorld civ, MetaWorld meta, int c, int from, long tick)
        {
            ref var city = ref civ.City(c);
            var u = ctx.Units.Store;
            var king = u.IsAlive(city.Leader) ? city.Leader : EntityId.None;
            int k = meta.CreateKingdom(c, city.Species, king, king.IsNone ? 0 : u.Uid[king.Index], tick, ref ctx.Rng.Get(RngStream.Names));
            ref var kd = ref meta.Kingdom(k);
            kd.LastWarEndTick = 0;
            // the rebel banner keeps the old shape with new colors (Bölüm 6.3)
            ref var h = ref meta.Kingdoms.Header(k);
            h.Banner.Shape = meta.Kingdoms.Headers[from].Banner.Shape;
            city.Kingdom = k;
            city.Loyalty = 50;
            civ.Version++;
            WarSystem.Declare(ctx, meta, k, from, meta.WarRebellion, c, tick);
            ctx.Events.Publish(new RebellionEvent(c, from, k));
            return k;
        }

        readonly System.Collections.Generic.HashSet<long> _borders = new System.Collections.Generic.HashSet<long>();

        // Bölüm 6.4 opinion: slow approach to a target from shared species, borders, alliances, past wars and king traits.
        void Opinions(CivWorld civ, MetaWorld meta, UnitStore u, long tick)
        {
            Borders(civ);
            int n = meta.Kingdoms.Count;
            for (int a = 0; a < n; a++)
            {
                if (!meta.KingdomAlive(a)) continue;
                for (int b = 0; b < n; b++)
                {
                    if (a == b || !meta.KingdomAlive(b)) continue;
                    ref var ka = ref meta.Kingdom(a);
                    ref var kb = ref meta.Kingdom(b);
                    float target = ka.Species == kb.Species ? 10f : -15f; // other species: softened once cultures and faiths weigh in (DECISIONS #58, #67)
                    if (_borders.Contains(((long)a << 32) | (uint)b)) target -= 10f;
                    if (ka.Alliance >= 0 && ka.Alliance == kb.Alliance) target += 40f;
                    if (meta.AtWar(a, b)) target -= 40f;
                    target -= 15f * RecentWars(meta, a, b, tick);
                    target += meta.TraitValue(meta.TraitOpinion, a);
                    target += KingTraits(meta, u, ka.King);
                    if (ka.Culture >= 0 && ka.Culture == kb.Culture) target += 15f;
                    if (ka.Religion >= 0 && ka.Religion == kb.Religion) target += 20f;
                    else if (ka.Religion >= 0 && kb.Religion >= 0 && meta.ReligionHas(ka.Religion, meta.RelHolyWar) && meta.ReligionHas(kb.Religion, meta.RelHolyWar)) target -= 30f;
                    if (ka.Species != kb.Species && meta.CultureHas(ka.Culture, meta.CulPureBlood)) target -= 30f;
                    if (CommonEnemy(meta, a, b)) target += 20f;
                    // opinion x 0.95 + target x 0.05, at least one point a month so integer rounding cannot stall it
                    int op = meta.Opinion(a, b), goal = (int)math.round(target);
                    int step = (int)math.round((goal - op) * 0.05f);
                    if (step == 0 && goal != op) step = goal > op ? 1 : -1;
                    meta.SetOpinion(a, b, op + step);
                }
            }
        }

        static bool Related(UnitStore u, EntityId a, EntityId b) =>
            a == b || u.Mother[b.Index] == a || u.Father[b.Index] == a || u.Mother[a.Index] == b || u.Father[a.Index] == b ||
            !u.Mother[a.Index].IsNone && u.Mother[a.Index] == u.Mother[b.Index];

        static bool CommonEnemy(MetaWorld meta, int a, int b)
        {
            // A common rival may be represented by separate bilateral wars.
            // Treating only members of one shared war record as an enemy
            // coalition made two kingdoms ignore the same aggressor.
            for (int enemy = 0; enemy < meta.Kingdoms.Count; enemy++)
            {
                if (enemy == a || enemy == b || !meta.KingdomAlive(enemy)) continue;
                if (meta.AtWar(a, enemy) && meta.AtWar(b, enemy)) return true;
            }
            return false;
        }

        static float KingTraits(MetaWorld meta, UnitStore u, EntityId king)
        {
            if (!u.IsAlive(king)) return 0f;
            var t = u.Traits[king.Index];
            float v = 0f;
            if (meta.TrPeaceful >= 0 && t.Has(meta.TrPeaceful)) v += 10f;
            if (meta.TrHonest >= 0 && t.Has(meta.TrHonest)) v += 10f;
            if (meta.TrBloodthirsty >= 0 && t.Has(meta.TrBloodthirsty)) v -= 10f;
            if (meta.TrDeceitful >= 0 && t.Has(meta.TrDeceitful)) v -= 10f;
            return v;
        }

        // ordered kingdom pairs with touching zones
        void Borders(CivWorld civ)
        {
            _borders.Clear();
            var map = civ.Map;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                int a = civ.City(c).Kingdom;
                if (a < 0) continue;
                foreach (int z in civ.CityZones[c])
                {
                    int zx = z % map.ZonesX, zy = z / map.ZonesX;
                    for (int d = 0; d < 4; d++)
                    {
                        int x = zx + (d == 0 ? 1 : d == 1 ? -1 : 0), y = zy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                        int b = civ.KingdomOf(map.Zones[y * map.ZonesX + x].OwnerCity);
                        if (b >= 0 && b != a) { _borders.Add(((long)a << 32) | (uint)b); _borders.Add(((long)b << 32) | (uint)a); }
                    }
                }
            }
        }

        // wars between the two in the last 50 years (Bölüm 6.4)
        static int RecentWars(MetaWorld meta, int a, int b, long tick)
        {
            int n = 0;
            long since = tick - 50L * SimConst.TicksPerYear;
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                var d = meta.Wars.Items[w];
                if (d.Result != WarResult.Ongoing && d.EndTick < since) continue;
                if (d.Attacker == a && d.Defender == b || d.Attacker == b && d.Defender == a) n++;
            }
            return n;
        }

        // Bölüm 6.6 stopgap until plots and powers (Bölüm 6.11, 7): friends above 60 join or found an alliance,
        // members below -30 towards any member leave; a single member dissolves it (DECISIONS #58).
        static void Alliances(in SimContext ctx, MetaWorld meta, long tick)
        {
            int n = meta.Kingdoms.Count;
            for (int al = 0; al < meta.Alliances.Count; al++)
            {
                if (!meta.Alliances.IsAlive(al)) continue;
                ref var d = ref meta.Alliances.Data(al);
                for (int i = d.Kingdoms.Length - 1; i >= 0; i--)
                {
                    int k = d.Kingdoms[i];
                    bool leave = !meta.KingdomAlive(k);
                    for (int j = 0; j < d.Kingdoms.Length && !leave; j++)
                        if (d.Kingdoms[j] != k && meta.Opinion(k, d.Kingdoms[j]) < LeaveAllianceOpinion) leave = true;
                    if (!leave) continue;
                    d.Kingdoms.RemoveAt(i);
                    if (meta.KingdomAlive(k)) meta.Kingdom(k).Alliance = -1;
                }
                if (d.Kingdoms.Length < 2)
                {
                    foreach (int k in d.Kingdoms) if (meta.KingdomAlive(k)) meta.Kingdom(k).Alliance = -1;
                    meta.Alliances.MarkDead(al, tick);
                }
            }
            for (int a = 0; a < n; a++)
            {
                if (!meta.KingdomAlive(a)) continue;
                for (int b = a + 1; b < n; b++)
                {
                    if (!meta.KingdomAlive(b)) continue;
                    if (!WantsAlliance(meta, a, b, tick)) continue;

                    bool aPlayer = (meta.Kingdoms.Headers[a].Flags & MetaFlags.PlayerControlled) != 0;
                    bool bPlayer = (meta.Kingdoms.Headers[b].Flags & MetaFlags.PlayerControlled) != 0;

                    if (aPlayer && bPlayer) continue; // both player-controlled: wait for explicit player/governor order

                    if (bPlayer)
                    {
                        // AI kingdom 'a' proposes alliance to player/governor kingdom 'b'
                        int id = meta.AddProposal(ProposalKind.Alliance, a, b, tick);
                        ctx.Events.Publish(new DiplomacyProposalEvent(id, a, b, ProposalKind.Alliance));
                    }
                    else if (aPlayer)
                    {
                        // AI kingdom 'b' proposes alliance to player/governor kingdom 'a'
                        int id = meta.AddProposal(ProposalKind.Alliance, b, a, tick);
                        ctx.Events.Publish(new DiplomacyProposalEvent(id, b, a, ProposalKind.Alliance));
                    }
                    else
                    {
                        // Both AI: form alliance directly
                        Ally(ctx, meta, a, b, tick);
                    }
                }
            }
        }

        // Returns true if kingdoms a and b desire an alliance with each other.
        public static bool WantsAlliance(MetaWorld meta, int a, int b)
            => WantsAlliance(meta, a, b, 0);

        public static bool WantsAlliance(MetaWorld meta, int a, int b, long tick)
        {
            if (a == b || !meta.KingdomAlive(a) || !meta.KingdomAlive(b) || meta.AtWar(a, b) || meta.HasVassalage(a, b, tick)) return false;
            ref var ka = ref meta.Kingdom(a);
            ref var kb = ref meta.Kingdom(b);
            if (ka.Alliance >= 0 && ka.Alliance == kb.Alliance) return false;
            if (ka.Alliance >= 0 && kb.Alliance >= 0) return false; // both in different alliances

            int opA = meta.Opinion(a, b);
            int opB = meta.Opinion(b, a);

            // Condition 1: High mutual friendship
            if (opA >= AllianceOpinion && opB >= AllianceOpinion) return true;

            // Condition 2: Coalition against a common enemy ("Düşmanımın düşmanı dostumdur")
            if (CommonEnemy(meta, a, b) && opA >= 0 && opB >= 0) return true;

            // Condition 3: Mutual protection - smaller kingdom seeks shelter with friendly stronger neighbor
            bool aShield = ka.Power < kb.Power * 0.7f && opA >= 15 && opB >= 15;
            bool bShield = kb.Power < ka.Power * 0.7f && opB >= 15 && opA >= 15;
            return aShield || bShield;
        }

        // a and b share an alliance: join the existing one or found a new one (false when both already have one)
        public static bool Ally(in SimContext ctx, MetaWorld meta, int a, int b, long tick)
        {
            if (!meta.KingdomAlive(a) || !meta.KingdomAlive(b) || a == b || meta.AtWar(a, b) || meta.HasVassalage(a, b, tick)) return false;
            ref var ka = ref meta.Kingdom(a);
            ref var kb = ref meta.Kingdom(b);
            if (ka.Alliance >= 0 && kb.Alliance >= 0) return ka.Alliance == kb.Alliance;
            int al = ka.Alliance >= 0 ? ka.Alliance : kb.Alliance;
            if (al < 0)
            {
                al = meta.Alliances.Create(new AllianceData { Leader = a }, 0, tick);
                meta.Alliances.Header(al).Color = meta.Kingdoms.Headers[a].Color;
                ctx.Events.Publish(new AllianceFormedEvent(al));
            }
            ref var d = ref meta.Alliances.Data(al);
            if (d.Kingdoms.Length >= d.Kingdoms.Capacity - 1) return false;
            if (!d.Kingdoms.Contains(a)) d.Kingdoms.Add(a);
            if (!d.Kingdoms.Contains(b)) d.Kingdoms.Add(b);
            ka.Alliance = al;
            kb.Alliance = al;
            return true;
        }

        // Removes a kingdom from its alliance; dissolves the alliance if fewer than 2 members remain.
        public static bool LeaveAlliance(in SimContext ctx, MetaWorld meta, int kingdom, long tick)
        {
            if (!meta.KingdomAlive(kingdom)) return false;
            ref var k = ref meta.Kingdom(kingdom);
            int al = k.Alliance;
            if (al < 0 || !meta.Alliances.IsAlive(al)) return false;

            ref var d = ref meta.Alliances.Data(al);
            for (int i = 0; i < d.Kingdoms.Length; i++)
            {
                if (d.Kingdoms[i] == kingdom)
                {
                    d.Kingdoms.RemoveAt(i);
                    break;
                }
            }
            k.Alliance = -1;

            if (d.Kingdoms.Length < 2)
            {
                foreach (int remaining in d.Kingdoms)
                    if (meta.KingdomAlive(remaining)) meta.Kingdom(remaining).Alliance = -1;
                meta.Alliances.MarkDead(al, tick);
            }
            return true;
        }

        public const int MigrationHappiness = -30;
        public const float MigrationChance = 0.1f;
        public const float DisasterEvacuationChance = 0.35f;

        // Bölüm 5.11: unhappy or starving residents move to a happier city of the same kingdom (inns attract x1.5);
        // foreign hospitality waits for cultures (Bölüm 6.7).
        void Migration(in SimContext ctx, CivWorld civ, MetaWorld meta, ref SimRandom rng)
        {
            if (_stHomesick == -2) _stHomesick = ctx.Content.StatusEffects.IdOrDefault("st.homesick");
            var w = ctx.Units;
            var u = w.Store;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c)) continue;
                ref var city = ref civ.City(c);
                bool disasterEvacuation = meta.HasRecentDisasterForCity(civ.Cities.Headers[c].Uid, ctx.Clock.Tick, SimConst.TicksPerYear);
                if (city.Kingdom < 0 || city.Happiness >= MigrationHappiness && city.FamineMonths == 0 && !disasterEvacuation) continue;
                int target = -1;
                float best = disasterEvacuation ? float.NegativeInfinity : city.Happiness;
                for (int o = 0; o < civ.CityCount; o++)
                {
                    if (o == c || !civ.CityAlive(o) || civ.City(o).Kingdom != city.Kingdom || civ.City(o).FamineMonths > 0) continue;
                    if (meta.HasRecentDisasterForCity(civ.Cities.Headers[o].Uid, ctx.Clock.Tick, SimConst.TicksPerYear)) continue;
                    if (civ.City(o).Population >= civ.City(o).Housing) continue;
                    float s = civ.City(o).Happiness * (civ.BldInn >= 0 && civ.CountBuildings(o, civ.BldInn, true) > 0 ? 1.5f : 1f);
                    if (s > best) { best = s; target = o; }
                }
                if (target < 0) continue;
                var residents = civ.CityResidents[c];
                int migrants = 0;
                for (int k = residents.Count - 1; k >= 0; k--)
                {
                    int i = residents[k];
                    if ((AgeStage)u.Age[i] < AgeStage.Adult || u.Army[i] >= 0 || city.Leader.Index == i
                        || !rng.Chance(disasterEvacuation ? DisasterEvacuationChance : MigrationChance)) continue;
                    u.City[i] = target; // CivJobs walks them there (ReturnToCity)
                    u.HomeBuilding[i] = -1;
                    u.Job[i] = 0;
                    u.CarryAmount[i] = 0;
                    u.Task[i] = (byte)UnitTask.None;
                    w.AddStatus(i, _stHomesick);
                    migrants++;
                }
                if (migrants > 0 && disasterEvacuation)
                    ctx.Events.Publish(new DisasterMigrationEvent(c, target, migrants));
                else if (migrants > 0 && city.FamineMonths == CityEconomySystem.FamineMonths)
                    ctx.Events.Publish(new FamineMigrationEvent(c, target, migrants));
            }
        }

        public const int TradeMax = 30, TradeKeep = 50, TradeOpinion = 20;
        public const float TradeAgreementPriceMultiplier = 0.9f;

        public static int TradeAgreementRouteCost(float distance, int amount)
            => (int)math.ceil(TradeRouteCost(distance, amount) * 0.75f);

        public static float TradeDemandPriority(bool isFood, int buyerFoodStock)
        {
            if (!isFood) return 1f;
            if (buyerFoodStock < TradeKeep / 4) return 2.5f;
            if (buyerFoodStock < TradeKeep / 2) return 1.8f;
            if (buyerFoodStock < TradeKeep) return 1.25f;
            return 1f;
        }

        // Bölüm 5.9: route surplus goods to the most profitable reachable market after transport cost.
        static void Trade(in SimContext ctx, CivWorld civ, MetaWorld meta, ref SimRandom rng)
        {
            if (civ.BldMarket < 0) return;
            var u = ctx.Units.Store;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c) || civ.CountBuildings(c, civ.BldMarket, true) == 0) continue;
                int k = civ.City(c).Kingdom;
                if (k < 0 || !meta.KingdomAlive(k)) continue;
                var from = CityPlanner.Below(civ.City(c).Center);
                if (civ.JobTrader < 0)
                {
                    BoatSystem.TryStartTradeRoute(ctx, civ, meta, c);
                    continue;
                }
                int partner = -1, res = -1, amount = 0;
                float bestProfit = 0f;
                for (int o = 0; o < civ.CityCount; o++)
                {
                    if (o == c || !civ.CityAlive(o) || civ.CountBuildings(o, civ.BldMarket, true) == 0) continue;
                    int ko = civ.City(o).Kingdom;
                    if (ko < 0 || !meta.KingdomAlive(ko)) continue;
                    bool friendly = ko == k || !meta.AtWar(k, ko) && !meta.HasTreaty(TreatyKind.TradeEmbargo, k, ko, ctx.Clock.Tick) && (meta.Opinion(k, ko) > TradeOpinion
                        || meta.KingdomAlive(k) && meta.Kingdom(k).Alliance >= 0 && meta.Kingdom(k).Alliance == meta.Kingdom(ko).Alliance
                        || meta.HasTreaty(TreatyKind.TradeAgreement, k, ko, ctx.Clock.Tick)
                        || meta.HasVassalage(k, ko, ctx.Clock.Tick));
                    if (!friendly) continue;
                    var to = CityPlanner.Below(civ.City(o).Center);
                    if (!ctx.Regions.SameIsland(from.x, from.y, to.x, to.y, MoveClass.Land)) continue; // trade ships: Bölüm 5.10
                    float distance = math.distance(from, to);
                    for (int r = 0; r < civ.ResCount; r++)
                    {
                        int surplus = civ.Res(c, r) - TradeKeep;
                        int demand = TradeKeep - civ.Res(o, r);
                        int candidateAmount = math.min(TradeMax, math.min(surplus, demand));
                        if (candidateAmount <= 0) continue;
                        int buyerStock = civ.Res(o, r);
                        bool treaty = k != ko && meta.HasTreaty(TreatyKind.TradeAgreement, k, ko, ctx.Clock.Tick);
                        while (candidateAmount > 0 && TradePayment(civ.Content.Resources[r].Value, candidateAmount, buyerStock, treaty) > civ.City(o).Gold) candidateAmount--;
                        if (candidateAmount <= 0) continue;
                        int gross = TradePayment(civ.Content.Resources[r].Value, candidateAmount, buyerStock, treaty);
                        int routeCost = treaty ? TradeAgreementRouteCost(distance, candidateAmount) : TradeRouteCost(distance, candidateAmount);
                        float profit = (gross - routeCost) * TradeDemandPriority(civ.Content.Resources[r].IsFood, civ.FoodTotal(o));
                        if (profit <= bestProfit) continue;
                        bestProfit = profit;
                        partner = o;
                        res = r;
                        amount = candidateAmount;
                    }
                }
                if (partner < 0)
                {
                    BoatSystem.TryStartTradeRoute(ctx, civ, meta, c);
                    continue;
                }
                int trader = -1;
                foreach (int i in civ.CityResidents[c])
                    if (u.Job[i] - 1 == civ.JobTrader && u.Dest[i].x < 0 && u.CarryAmount[i] == 0) { trader = i; break; }
                if (trader < 0)
                {
                    BoatSystem.TryStartTradeRoute(ctx, civ, meta, c);
                    continue;
                }
                civ.TakeRes(c, res, amount);
                u.CarryRes[trader] = (ushort)res;
                u.CarryAmount[trader] = (byte)amount;
                u.Dest[trader] = CityPlanner.Below(civ.City(partner).Center);
                u.Action[trader] = 0;
            }
        }

        public static float TradePriceMultiplier(int buyerStock)
        {
            float scarcity = (TradeKeep - math.max(0, buyerStock)) / (float)TradeKeep;
            return math.clamp(1f + scarcity * 0.5f, 0.5f, 1.5f);
        }

        public static int TradePayment(float resourceValue, int amount) => TradePayment(resourceValue, amount, TradeKeep);

        public static int TradePayment(float resourceValue, int amount, int buyerStock)
            => math.max(0, (int)(resourceValue * amount * CivJobs.TradeMarkup * TradePriceMultiplier(buyerStock)));

        public static int TradePayment(float resourceValue, int amount, int buyerStock, bool tradeAgreement)
            => tradeAgreement ? (int)(TradePayment(resourceValue, amount, buyerStock) * TradeAgreementPriceMultiplier)
                : TradePayment(resourceValue, amount, buyerStock);

        public static int TradeRouteCost(float distance, int amount)
            => math.max(0, (int)math.ceil(math.max(0f, distance) * math.max(0, amount) * 0.005f));
    }
}
