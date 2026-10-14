using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PG.Governor
{
    // The LLM kingdom brain, two-phase (Bölüm 10, DECISIONS #98):
    //   1) king call — reads the kingdom snapshot, may request up to MaxAdvisors specialists;
    //   2) advisor calls (parallel) — suggestions only, they cannot start agents or change state;
    //   3) final king call — sees the advisor opinions and returns the ONE structured decision.
    // Runs on a worker thread only.
    public sealed class GovernorBrain : IKingdomBrain, IAsyncKingdomBrain
    {
        public const int MaxAdvisors = 3;
        static readonly string[] KnownAdvisors = { "war", "economy", "diplomacy" };

        readonly ILlmClient _client;
        readonly GovernorSettings _settings;
        int _tokensUsed;

        public ILlmClient Client => _client;
        public int TokensUsed => System.Threading.Volatile.Read(ref _tokensUsed);
        public int LastCalls; // diagnostics: completions used by the latest Decide

        public GovernorBrain(ILlmClient client, GovernorSettings settings)
        {
            _client = client;
            _settings = settings;
        }

        public GovernorDecision Decide(KingdomSnapshot snapshot)
        {
            int calls = 0;
            string user = "Kingdom state:\n" + snapshot.ToJson();
            string kingSystem = Prompts.KingSystemPrompt(snapshot.MaxAdvisors);

            var answer = Complete(kingSystem, user, ref calls, true);
            if (!DecisionJson.TryParse(answer, snapshot.KingdomIndex, out var decision))
            {
                answer = Complete(kingSystem, user + "\n\nYour previous answer was not valid JSON. Return ONLY the JSON object.", ref calls, true);
                if (!DecisionJson.TryParse(answer, snapshot.KingdomIndex, out decision))
                    return new GovernorDecision { Kingdom = snapshot.KingdomIndex, Thought = "unparseable answer" };
            }

            // Phase 2: the king requested specialists; they only advise.
            var wanted = new List<string>();
            foreach (var a in decision.Advisors)
                if (wanted.Count < MaxAdvisors && Array.IndexOf(KnownAdvisors, a) >= 0 && !wanted.Contains(a))
                    wanted.Add(a);
            if (wanted.Count > 0)
            {
                var advisorResults = new string[wanted.Count];
                Parallel.For(0, wanted.Count, i =>
                {
                    advisorResults[i] = Complete(Prompts.AdvisorSystemPrompt(wanted[i]), user, ref calls, false);
                });
                for (int i = 0; i < wanted.Count; i++)
                {
                    string thought = DecisionJson.TryParse(advisorResults[i], snapshot.KingdomIndex, out var sub) ? sub.Thought : "(unreadable opinion)";
                    decision.AdvisorOpinions.Add((wanted[i], thought));
                    if (sub != null) decision.Actions.AddRange(sub.Actions);
                }

                // Phase 3: the main model gives the final structured decision, seeing the opinions.
                var opinions = new StringBuilder();
                foreach (var (name, thought) in decision.AdvisorOpinions)
                    opinions.Append("[").Append(name).Append("]: ").AppendLine(thought);
                string finalUser = user + "\n\nAdvisor opinions (suggestions only — you decide):\n" + opinions +
                                   "\nReturn ONLY the final JSON decision.";
                if (!DecisionJson.TryParse(Complete(kingSystem, finalUser, ref calls, true), snapshot.KingdomIndex, out var final))
                    final = decision; // keep the pre-advisor decision when the final pass fails
                final.Kingdom = snapshot.KingdomIndex;
                final.Advisors.Clear();
                final.Advisors.AddRange(decision.Advisors);
                final.AdvisorOpinions.Clear();
                final.AdvisorOpinions.AddRange(decision.AdvisorOpinions);
                if (final.Actions.Count > DecisionValidator.MaxActions)
                    final.Actions.RemoveRange(DecisionValidator.MaxActions, final.Actions.Count - DecisionValidator.MaxActions);
                decision = final;
            }

            LastCalls = calls;
            return decision;
        }

        public async Task<GovernorDecision> DecideAsync(KingdomSnapshot snapshot)
        {
            int calls = 0;
            string user = "Kingdom state:\n" + snapshot.ToJson();
            string system = Prompts.KingSystemPrompt(snapshot.MaxAdvisors);
            var answer = await CompleteAsync(system, user, true);
            calls++;
            if (!DecisionJson.TryParse(answer, snapshot.KingdomIndex, out var decision))
            {
                answer = await CompleteAsync(system, user + "\n\nYour previous answer was not valid JSON. Return ONLY the JSON object.", true);
                calls++;
                if (!DecisionJson.TryParse(answer, snapshot.KingdomIndex, out decision))
                {
                    LastCalls = calls;
                    return new GovernorDecision { Kingdom = snapshot.KingdomIndex, Thought = "unparseable answer" };
                }
            }
            var wanted = new List<string>();
            foreach (var advisor in decision.Advisors)
                if (wanted.Count < MaxAdvisors && Array.IndexOf(KnownAdvisors, advisor) >= 0 && !wanted.Contains(advisor)) wanted.Add(advisor);
            if (wanted.Count > 0)
            {
                // Sequential awaits keep the WebGL path on Unity's main thread; advisors remain suggestions only.
                foreach (var advisor in wanted)
                {
                    string text = await CompleteAsync(Prompts.AdvisorSystemPrompt(advisor), user, false);
                    calls++;
                    string thought = DecisionJson.TryParse(text, snapshot.KingdomIndex, out var opinion) ? opinion.Thought : "(unreadable opinion)";
                    decision.AdvisorOpinions.Add((advisor, thought));
                    if (opinion != null) decision.Actions.AddRange(opinion.Actions);
                }
                var opinions = new StringBuilder();
                foreach (var (name, thought) in decision.AdvisorOpinions) opinions.Append("[").Append(name).Append("]: ").AppendLine(thought);
                string finalUser = user + "\n\nAdvisor opinions (suggestions only — you decide):\n" + opinions + "\nReturn ONLY the final JSON decision.";
                string finalText = await CompleteAsync(system, finalUser, true);
                calls++;
                if (!DecisionJson.TryParse(finalText, snapshot.KingdomIndex, out var final)) final = decision;
                final.Kingdom = snapshot.KingdomIndex;
                final.Advisors.Clear(); final.Advisors.AddRange(decision.Advisors);
                final.AdvisorOpinions.Clear(); final.AdvisorOpinions.AddRange(decision.AdvisorOpinions);
                if (final.Actions.Count > DecisionValidator.MaxActions) final.Actions.RemoveRange(DecisionValidator.MaxActions, final.Actions.Count - DecisionValidator.MaxActions);
                decision = final;
            }
            LastCalls = calls;
            return decision;
        }

        async Task<string> CompleteAsync(string system, string user, bool jsonFormat)
        {
            if (!(_client is IAsyncLlmClient asyncClient)) throw new InvalidOperationException("AI istemcisi asenkron çağrıyı desteklemiyor.");
            var response = await asyncClient.CompleteAsync(new LlmRequest
            {
                System = system, User = user, Temperature = _settings.Temperature,
                MaxTokens = _settings.MaxTokens, JsonFormat = jsonFormat,
            });
            if (response.TotalTokens > 0) System.Threading.Interlocked.Add(ref _tokensUsed, response.TotalTokens);
            return response.Content;
        }

        string Complete(string system, string user, ref int calls, bool jsonFormat)
        {
            calls++;
            var resp = _client.Complete(new LlmRequest
            {
                System = system,
                User = user,
                Temperature = _settings.Temperature,
                MaxTokens = _settings.MaxTokens,
                JsonFormat = jsonFormat,
            });
            if (resp.TotalTokens > 0) System.Threading.Interlocked.Add(ref _tokensUsed, resp.TotalTokens);
            return resp.Content;
        }
    }

    public static class Prompts
    {
        const string ActionList =
            "- {\"kind\":\"Wait\"}\n" +
            "- {\"kind\":\"DeclareWar\",\"other\":<kingdomIndex>} — unilateral after peaceBanYearsLeft reaches 0; an active NonAggression status is a deliberate treaty breach, cancels the pact and costs the target 30 opinion, so only do this when the strategic gain justifies it\n" +
            "- {\"kind\":\"ProposePeace\",\"other\":<kingdomIndex>} — the enemy must accept\n" +
            "- {\"kind\":\"ProposeAlliance\",\"other\":<kingdomIndex>} — the other kingdom must accept\n" +
            "- {\"kind\":\"ProposeTrade\",\"other\":<kingdomIndex>} / {\"kind\":\"ProposeNonAggression\",\"other\":<kingdomIndex>} — bilateral treaties; the other kingdom must accept\n" +
            "- {\"kind\":\"ProposeTradeEmbargo\",\"other\":<kingdomIndex>} — proposes suspending trade with a hostile realm; the other kingdom must accept\n" +
            "- {\"kind\":\"ProposeVassalage\",\"other\":<kingdomIndex>} — proposes becoming that kingdom's overlord; the other must accept and weaker kingdoms may refuse\n" +
            "- {\"kind\":\"AcceptProposal\",\"id\":<proposalId>} / {\"kind\":\"RejectProposal\",\"id\":<proposalId>} — pendingProposals in the state\n" +
            "- {\"kind\":\"SetCapital\",\"city\":<cityIndex>} — one of YOUR cities\n" +
            "- {\"kind\":\"SetHeir\",\"unit\":<unitIndex>} — only if a citizen unit index is provided\n" +
            "- {\"kind\":\"ArmyTarget\",\"city\":<cityIndex>} — where your armies march\n" +
            "- {\"kind\":\"SetPolicy\",\"policy\":{\"food\":0-100,\"housing\":0-100,\"production\":0-100,\"defense\":0-100,\"culture\":0-100,\"tax\":0-100}} — city development priorities and tax burden (50 is balanced)\n" +
            "- {\"kind\":\"LeaveAlliance\"} — exit your current alliance if relations with allies have soured";

        public static string KingSystemPrompt(int maxAdvisors) =>
            "You govern a kingdom in a simulated sandbox world (a god-game). Each year you receive a compact " +
            "JSON state of your kingdom and answer with your strategic orders. You may spend at most " + maxAdvisors +
            " advisor sub-agents (war / economy / diplomacy) to double-check a domain; they only advise, " +
            "you give the final decision. Valid actions (return at most 8, in priority order):\n" + ActionList + "\n" +
            "Think like a medieval ruler: protect food and happiness first, avoid wars you cannot win, " +
            "propose peace when casualties mount, ally with strong neighbours of positive opinion, and adjust " +
            "the city policy toward what your realm lacks. If nothing needs changing, return a single Wait action. " +
            "Respond ONLY with minified JSON: " +
            "{\"thought\":\"one short sentence\",\"advisors\":[\"war\"|\"economy\"|\"diplomacy\"],\"actions\":[...]}.";

        public static string AdvisorSystemPrompt(string domain) => domain switch
        {
            "war" => "You are the war council. Judge army strength, ongoing wars and casualty trends. " +
                     "Recommend only military actions (DeclareWar / ProposePeace / ArmyTarget / Wait).",
            "economy" => "You are the economy steward. Judge food vs storage, famine months, happiness and treasury. " +
                         "Recommend only actions that improve the realm's stability (SetPolicy / SetCapital / ArmyTarget / Wait).",
            "diplomacy" => "You are the envoy. Judge opinions, alliances and war weariness. " +
                           "Recommend only diplomatic actions (ProposeAlliance / ProposePeace / ProposeTrade / ProposeNonAggression / ProposeTradeEmbargo / ProposeVassalage / DeclareWar / AcceptProposal / RejectProposal / Wait).",
            _ => "You are an advisor to the ruler. Recommend only valid actions.",
        } + "\n" + ActionList + "\nRespond ONLY with minified JSON: " +
          "{\"thought\":\"one short sentence\",\"advisors\":[],\"actions\":[...]}.";
    }
}
