using System;
using System.Collections.Generic;

namespace PG.Governor
{
    // What the governor may order. War stays unilateral; peace and alliance go through
    // proposals that the other side must accept (Bölüm 10, DECISIONS #98).
    public enum GovernorActionKind : byte
    {
        Wait, DeclareWar, ProposePeace, ProposeAlliance, ProposeTrade, ProposeNonAggression, ProposeVassalage, AcceptProposal, RejectProposal,
        SetCapital, SetHeir, ArmyTarget, SetPolicy, LeaveAlliance,
        ProposeTradeEmbargo,
    }

    public readonly struct GovernorAction
    {
        public readonly GovernorActionKind Kind;
        public readonly int Other;   // kingdom index, city / unit index, or proposal id
        public readonly float Food, Housing, Production, Defense, Culture, Tax; // policy values (0..100)

        public GovernorAction(GovernorActionKind kind, int other = -1)
        {
            Kind = kind;
            Other = other;
            Food = Housing = Production = Defense = Culture = Tax = 0f;
        }

        public GovernorAction(float food, float housing, float production, float defense, float culture, float tax = 50f)
            : this(GovernorActionKind.SetPolicy)
        {
            Food = food; Housing = housing; Production = production; Defense = defense; Culture = culture; Tax = tax;
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case GovernorActionKind.SetPolicy:
                    return $"SetPolicy(food {Food:0}, housing {Housing:0}, production {Production:0}, defense {Defense:0}, culture {Culture:0}, tax {Tax:0})";
                case GovernorActionKind.Wait:
                    return "Wait";
                default:
                    return $"{Kind}({Other})";
            }
        }
    }

    // One yearly (or urgent) decision of a kingdom brain. Produced off the main thread; validated on it.
    public sealed class GovernorDecision
    {
        public int Kingdom;
        public string Thought = "";
        public readonly List<string> Advisors = new List<string>();      // requested specialist domains
        public readonly List<GovernorAction> Actions = new List<GovernorAction>();
        public readonly List<(string Name, string Thought)> AdvisorOpinions = new List<(string, string)>();
    }

    public interface IKingdomBrain
    {
        // Runs on a background thread; must not touch Unity or simulation state.
        GovernorDecision Decide(KingdomSnapshot snapshot);
    }

    // Browser transports must stay on Unity's main thread and use UnityWebRequest.
    public interface IAsyncKingdomBrain
    {
        System.Threading.Tasks.Task<GovernorDecision> DecideAsync(KingdomSnapshot snapshot);
    }

    // Deterministic brain for tests, headless runs and "no API configured": replays a fixed script.
    public sealed class ScriptedBrain : IKingdomBrain
    {
        readonly Func<KingdomSnapshot, GovernorDecision> _next;
        public int Calls;

        public ScriptedBrain(Func<KingdomSnapshot, GovernorDecision> next) => _next = next;

        public GovernorDecision Decide(KingdomSnapshot snapshot)
        {
            Calls++;
            return _next(snapshot);
        }

        public static ScriptedBrain Wait() => new ScriptedBrain(_ => new GovernorDecision { Kingdom = _.KingdomIndex, Thought = "wait" });
    }
}
