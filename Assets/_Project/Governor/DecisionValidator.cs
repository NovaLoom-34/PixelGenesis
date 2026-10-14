using System.Collections.Generic;
using PG.Core;
using PG.Powers;
using PG.Sim;

namespace PG.Governor
{
    public sealed class ValidatedCommands
    {
        public readonly List<MetaCommand> Metas = new List<MetaCommand>();
        public readonly List<PolicyCommand> Policies = new List<PolicyCommand>();
    }

    // Re-checks every action against the live simulation on the main thread before it reaches
    // the queue. The sim re-validates too (EditCommandSystem.Meta); this keeps garbage out of
    // the log. Peace and alliance always become proposals (both sides must accept, Bölüm 10).
    public static class DecisionValidator
    {
        public const int MaxActions = 8;

        public static ValidatedCommands Validate(GovernorDecision decision, SimWorld sim, List<string> rejected)
        {
            var result = new ValidatedCommands();
            if (decision == null || sim == null || !sim.Meta.KingdomAlive(decision.Kingdom)) return result;
            var meta = sim.Meta;
            long tick = sim.Clock.Tick;
            ref var kd = ref meta.Kingdom(decision.Kingdom);

            var seen = new HashSet<string>();
            foreach (var a in decision.Actions)
            {
                if (result.Metas.Count + result.Policies.Count >= MaxActions) break;
                switch (a.Kind)
                {
                    case GovernorActionKind.Wait:
                        continue; // no command, but not "rejected"
                    case GovernorActionKind.DeclareWar:
                        if (!AliveOther(meta, decision.Kingdom, a.Other, rejected, a)) continue;
                        if (meta.AtWar(decision.Kingdom, a.Other)) { Reject(rejected, a, "already at war"); continue; }
                        if (tick - kd.LastWarEndTick < WarSystem.PeaceBanYears * SimConst.TicksPerYear)
                        { Reject(rejected, a, "peace ban"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.DeclareWar, Kingdom = decision.Kingdom, Other = a.Other });
                        break;
                    case GovernorActionKind.ProposePeace:
                        if (!AliveOther(meta, decision.Kingdom, a.Other, rejected, a)) continue;
                        if (!meta.AtWar(decision.Kingdom, a.Other)) { Reject(rejected, a, "not at war"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.ProposePeace, Kingdom = decision.Kingdom, Other = a.Other });
                        break;
                    case GovernorActionKind.ProposeAlliance:
                        if (!AliveOther(meta, decision.Kingdom, a.Other, rejected, a)) continue;
                        if (meta.HasVassalage(decision.Kingdom, a.Other, tick)) { Reject(rejected, a, "vassal relationship"); continue; }
                        if (kd.Alliance >= 0 && kd.Alliance == meta.Kingdom(a.Other).Alliance)
                        { Reject(rejected, a, "already allied"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.ProposeAlliance, Kingdom = decision.Kingdom, Other = a.Other });
                        break;
                    case GovernorActionKind.ProposeTrade:
                    case GovernorActionKind.ProposeNonAggression:
                    case GovernorActionKind.ProposeVassalage:
                    case GovernorActionKind.ProposeTradeEmbargo:
                    {
                        if (!AliveOther(meta, decision.Kingdom, a.Other, rejected, a)) continue;
                        bool trade = a.Kind == GovernorActionKind.ProposeTrade;
                        bool nonAggression = a.Kind == GovernorActionKind.ProposeNonAggression;
                        bool embargo = a.Kind == GovernorActionKind.ProposeTradeEmbargo;
                        var treaty = trade ? TreatyKind.TradeAgreement : nonAggression ? TreatyKind.NonAggression
                            : embargo ? TreatyKind.TradeEmbargo : TreatyKind.Vassalage;
                        var proposal = trade ? ProposalKind.TradeAgreement : nonAggression ? ProposalKind.NonAggression
                            : embargo ? ProposalKind.TradeEmbargo : ProposalKind.Vassalage;
                        if (meta.AtWar(decision.Kingdom, a.Other)) { Reject(rejected, a, "at war"); continue; }
                        if (meta.HasTreaty(treaty, decision.Kingdom, a.Other, tick)) { Reject(rejected, a, "treaty already active"); continue; }
                        if (meta.HasProposal(proposal, decision.Kingdom, a.Other)) { Reject(rejected, a, "proposal already pending"); continue; }
                        if (a.Kind == GovernorActionKind.ProposeVassalage && meta.VassalOf(decision.Kingdom, tick) >= 0)
                        { Reject(rejected, a, "already a vassal"); continue; }
                        Add(result, seen, new MetaCommand
                        {
                            Order = trade ? MetaOrder.ProposeTradeAgreement : nonAggression ? MetaOrder.ProposeNonAggression
                                : embargo ? MetaOrder.ProposeTradeEmbargo : MetaOrder.ProposeVassalage,
                            Kingdom = decision.Kingdom, Other = a.Other,
                        });
                        break;
                    }
                    case GovernorActionKind.AcceptProposal:
                    case GovernorActionKind.RejectProposal:
                    {
                        if (!meta.FindProposal(a.Other, out var p) || p.To != decision.Kingdom)
                        { Reject(rejected, a, "no such proposal"); continue; }
                        Add(result, seen, new MetaCommand
                        {
                            Order = a.Kind == GovernorActionKind.AcceptProposal ? MetaOrder.AcceptProposal : MetaOrder.RejectProposal,
                            Kingdom = decision.Kingdom,
                            Other = a.Other,
                        });
                        break;
                    }
                    case GovernorActionKind.SetCapital:
                        if (sim.Civ == null || !sim.Civ.CityAlive(a.Other) || sim.Civ.KingdomOf(a.Other) != decision.Kingdom)
                        { Reject(rejected, a, "not our city"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.SetCapital, Kingdom = decision.Kingdom, Other = a.Other });
                        break;
                    case GovernorActionKind.SetHeir:
                    {
                        var u = sim.Units.Store;
                        if (!u.IsAlive(a.Other) || sim.Civ == null || sim.Civ.KingdomOf(u.City[a.Other]) != decision.Kingdom)
                        { Reject(rejected, a, "not our citizen"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.SetHeir, Kingdom = decision.Kingdom, Other = a.Other });
                        break;
                    }
                    case GovernorActionKind.ArmyTarget:
                        if (a.Other < 0 || sim.Civ == null || !sim.Civ.CityAlive(a.Other))
                        { Reject(rejected, a, "no such city"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.ArmyTarget, Kingdom = decision.Kingdom, Other = a.Other });
                        break;
                    case GovernorActionKind.SetPolicy:
                        Add(result, seen, new PolicyCommand
                        {
                            Kingdom = decision.Kingdom,
                            Food = a.Food, Housing = a.Housing, Production = a.Production, Defense = a.Defense, Culture = a.Culture, Tax = a.Tax,
                        });
                        break;
                    case GovernorActionKind.LeaveAlliance:
                        if (kd.Alliance < 0) { Reject(rejected, a, "not in an alliance"); continue; }
                        Add(result, seen, new MetaCommand { Order = MetaOrder.LeaveAlliance, Kingdom = decision.Kingdom });
                        break;
                }
            }
            return result;
        }

        static void Add(ValidatedCommands result, HashSet<string> seen, MetaCommand c)
        {
            string key = $"m:{c.Order}:{c.Other}";
            if (seen.Add(key)) result.Metas.Add(c);
        }

        static void Add(ValidatedCommands result, HashSet<string> seen, PolicyCommand c)
        {
            if (seen.Add("policy")) result.Policies.Add(c);
        }

        static bool AliveOther(MetaWorld meta, int kingdom, int other, List<string> rejected, GovernorAction a)
        {
            if (other < 0 || other == kingdom || !meta.KingdomAlive(other))
            {
                Reject(rejected, a, "bad target kingdom");
                return false;
            }
            return true;
        }

        static void Reject(List<string> rejected, GovernorAction a, string why) =>
            rejected?.Add($"{a} — {why}");
    }
}
