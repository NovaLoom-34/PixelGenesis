using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Governor;
using PG.Powers;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    // Kingdom governor tests (Bölüm 10, DECISIONS #97/#98): JSON parsing, validation, scripted
    // end-to-end, stale answers, player takeover, counterparty acceptance, the advisor limit and
    // the decision log round trip. No network is touched (fake clients only).
    public sealed class GovernorTests
    {
        ContentDB _db;
        SimWorld _sim;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(97));
        }

        [TearDown]
        public void TearDown()
        {
            _sim.Dispose();
            _sim = null;
        }

        int CreateKingdomWithCity(int2 pos)
        {
            int species = _db.Species.IdOf("sp.human");
            int city = _sim.Civ.CreateCity(pos, species, -1, 0, _sim.Clock.Tick, 0);
            var rng = _sim.Rng.Get(RngStream.Meta);
            int k = _sim.Meta.CreateKingdom(city, species, PG.Core.EntityId.None, 0, _sim.Clock.Tick, ref rng);
            ref var cd = ref _sim.Civ.City(city);
            cd.Kingdom = k;
            cd.Loyalty = 100;
            _sim.Civ.Version++;
            return k;
        }

        // Fresh kingdoms sit inside the 10-year peace ban (WarSystem.PeaceBanYears). Backdate their last war end
        // so offensive decisions are allowed; simulating ten years instead lets the kingdoms die in the sandbox.
        void AdvancePastPeaceBan()
        {
            for (int k = 0; k < _sim.Meta.Kingdoms.Count; k++)
                _sim.Meta.Kingdom(k).LastWarEndTick = -WarSystem.PeaceBanYears * SimConst.TicksPerYear;
        }

        // Decisions run on a worker thread; poll until the in-flight call returns, then apply it once.
        static void DrainDecisions(GovernorRunner runner, EditCommandQueue queue)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (runner.InFlight > 0 && DateTime.UtcNow < deadline)
            {
                runner.Update(queue);
                Thread.Sleep(5);
            }
            runner.Update(queue);
        }

        static GovernorDecision Decision(int kingdom, params GovernorAction[] actions)
        {
            var d = new GovernorDecision { Kingdom = kingdom, Thought = "test" };
            d.Actions.AddRange(actions);
            return d;
        }

        [Test]
        public void DecisionJsonParsesActionsAndAdvisors()
        {
            const string json = "{\"thought\":\"strike now\",\"advisors\":[\"war\"]," +
                                "\"actions\":[{\"kind\":\"DeclareWar\",\"other\":2},{\"kind\":\"Wait\"}]}";
            Assert.That(DecisionJson.TryParse(json, 5, out var d), Is.True);
            Assert.That(d.Kingdom, Is.EqualTo(5));
            Assert.That(d.Thought, Is.EqualTo("strike now"));
            Assert.That(d.Advisors, Does.Contain("war"));
            Assert.That(d.Actions.Count, Is.EqualTo(2));
            Assert.That(d.Actions[0].Kind, Is.EqualTo(GovernorActionKind.DeclareWar));
            Assert.That(d.Actions[0].Other, Is.EqualTo(2));
            Assert.That(DecisionJson.TryParse("not json at all", 5, out _), Is.False);
            // A non-array "actions" field is dropped but the rest still parses.
            Assert.That(DecisionJson.TryParse("{\"actions\": 42 }", 5, out var empty), Is.True);
            Assert.That(empty.Actions.Count, Is.EqualTo(0));
        }

        [Test]
        public void DecisionJsonParsesProposalsAndPolicy()
        {
            const string json = "{\"thought\":\"settle\",\"actions\":[" +
                                "{\"kind\":\"ProposePeace\",\"other\":3},{\"kind\":\"ProposeAlliance\",\"other\":4}," +
                                "{\"kind\":\"AcceptProposal\",\"id\":7},{\"kind\":\"RejectProposal\",\"id\":8}," +
                                "{\"kind\":\"SetPolicy\",\"policy\":{\"food\":40,\"housing\":20,\"production\":20,\"defense\":10,\"culture\":10}}]}";
            Assert.That(DecisionJson.TryParse(json, 1, out var d), Is.True);
            Assert.That(d.Actions.Count, Is.EqualTo(5));
            Assert.That(d.Actions[0].Kind, Is.EqualTo(GovernorActionKind.ProposePeace));
            Assert.That(d.Actions[2].Kind, Is.EqualTo(GovernorActionKind.AcceptProposal));
            Assert.That(d.Actions[2].Other, Is.EqualTo(7));
            var policy = d.Actions[4];
            Assert.That(policy.Kind, Is.EqualTo(GovernorActionKind.SetPolicy));
            Assert.That(policy.Food, Is.EqualTo(40f));
            Assert.That(policy.Defense, Is.EqualTo(10f));
        }

        [Test]
        public void GovernorParsesAndValidatesEveryDiplomaticTreatyProposal()
        {
            int proposer = CreateKingdomWithCity(new int2(16, 16));
            int target = CreateKingdomWithCity(new int2(48, 48));
            string json = "{\"actions\":[" +
                          $"{{\"kind\":\"ProposeTrade\",\"other\":{target}}}," +
                          $"{{\"kind\":\"ProposeNonAggression\",\"other\":{target}}}," +
                          $"{{\"kind\":\"ProposeVassalage\",\"other\":{target}}}," +
                          $"{{\"kind\":\"ProposeTradeEmbargo\",\"other\":{target}}}]}}";

            Assert.That(DecisionJson.TryParse(json, proposer, out var decision), Is.True);
            Assert.That(decision.Actions, Has.Count.EqualTo(4));
            var rejected = new List<string>();
            var commands = DecisionValidator.Validate(decision, _sim, rejected);

            Assert.That(commands.Metas, Has.Count.EqualTo(4), string.Join(";", rejected));
            Assert.That(commands.Metas[0].Order, Is.EqualTo(MetaOrder.ProposeTradeAgreement));
            Assert.That(commands.Metas[1].Order, Is.EqualTo(MetaOrder.ProposeNonAggression));
            Assert.That(commands.Metas[2].Order, Is.EqualTo(MetaOrder.ProposeVassalage));
            Assert.That(commands.Metas[3].Order, Is.EqualTo(MetaOrder.ProposeTradeEmbargo));
            Assert.That(rejected, Is.Empty);
        }

        [Test]
        public void DecisionJsonExtractsFencedJson()
        {
            const string text = "Here is my plan:\n```json\n{\"thought\":\"x\",\"actions\":[{\"kind\":\"ProposeAlliance\",\"other\":3}]}\n```\nDone.";
            Assert.That(DecisionJson.TryParse(text, 1, out var d), Is.True);
            Assert.That(d.Actions.Count, Is.EqualTo(1));
            Assert.That(d.Actions[0].Kind, Is.EqualTo(GovernorActionKind.ProposeAlliance));
        }

        [Test]
        public void ValidatorBuildsCommandsAndRejectsBadTargets()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            AdvancePastPeaceBan(); // otherwise the valid DeclareWar is rejected by the peace ban
            int cityA = _sim.Meta.Kingdom(a).Capital;
            int cityB = _sim.Meta.Kingdom(b).Capital;
            var decision = Decision(a,
                new GovernorAction(GovernorActionKind.DeclareWar, b),
                new GovernorAction(GovernorActionKind.ProposePeace, b),       // not at war -> rejected
                new GovernorAction(GovernorActionKind.ProposeAlliance, b),
                new GovernorAction(GovernorActionKind.SetCapital, cityA),
                new GovernorAction(GovernorActionKind.SetCapital, cityB),     // enemy city -> rejected
                new GovernorAction(GovernorActionKind.ArmyTarget, cityB),
                new GovernorAction(GovernorActionKind.DeclareWar, -3));       // nonsense -> rejected

            var rejected = new List<string>();
            var cmds = DecisionValidator.Validate(decision, _sim, rejected);

            Assert.That(cmds.Metas.Count, Is.EqualTo(4), string.Join(";", rejected));
            Assert.That(cmds.Metas[0].Order, Is.EqualTo(MetaOrder.DeclareWar));
            Assert.That(cmds.Metas[0].Other, Is.EqualTo(b));
            Assert.That(cmds.Metas[1].Order, Is.EqualTo(MetaOrder.ProposeAlliance));
            Assert.That(cmds.Metas[2].Order, Is.EqualTo(MetaOrder.SetCapital));
            Assert.That(cmds.Metas[3].Order, Is.EqualTo(MetaOrder.ArmyTarget));
            Assert.That(rejected.Count, Is.EqualTo(3));
        }

        [Test]
        public void ValidatorBlocksWarDuringPeaceBan()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            _sim.Meta.Kingdom(a).LastWarEndTick = _sim.Clock.Tick; // just made peace
            var rejected = new List<string>();
            var cmds = DecisionValidator.Validate(Decision(a, new GovernorAction(GovernorActionKind.DeclareWar, b)), _sim, rejected);
            Assert.That(cmds.Metas.Count, Is.EqualTo(0));
            Assert.That(rejected.Count, Is.EqualTo(1));
            Assert.That(rejected[0], Does.Contain("peace ban"));
        }

        [Test]
        public void PeaceProposalRequiresAnActiveWarAndBecomesBilateral()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));

            // Not at war: the peace proposal is rejected outright.
            var rejected = new List<string>();
            var none = DecisionValidator.Validate(Decision(a, new GovernorAction(GovernorActionKind.ProposePeace, b)), _sim, rejected);
            Assert.That(none.Metas.Count, Is.EqualTo(0));
            Assert.That(rejected[0], Does.Contain("not at war"));

            // At war: the validator turns the decision into a bilateral proposal command.
            _sim.Meta.Kingdom(a).LastWarEndTick = -10 * SimConst.TicksPerYear;
            WarSystem.Declare(_sim.Context, _sim.Meta, a, b, _sim.Meta.WarConquest, -1, _sim.Clock.Tick);
            var cmds = DecisionValidator.Validate(Decision(a, new GovernorAction(GovernorActionKind.ProposePeace, b)), _sim, new List<string>());
            Assert.That(cmds.Metas.Count, Is.EqualTo(1));
            Assert.That(cmds.Metas[0].Order, Is.EqualTo(MetaOrder.ProposePeace));
        }

        [Test]
        public void RunnerAppliesScriptedDecisionEndToEnd()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            AdvancePastPeaceBan();

            var settings = new GovernorSettings { Enabled = true };
            var runner = new GovernorRunner(settings, new ScriptedBrain(s => Decision(s.KingdomIndex,
                new GovernorAction(GovernorActionKind.DeclareWar, b))));
            runner.Bind(_sim);
            runner.ScheduleYear();

            var queue = new EditCommandQueue();
            EditSystems.Install(_sim, queue);
            DrainDecisions(runner, queue);
            Assert.That(runner.Log.Entries.Count, Is.GreaterThanOrEqualTo(1), "the decision must be logged");
            Assert.That(queue.Metas, Has.None.Matches<MetaCommand>(m => m.Order == MetaOrder.Control),
                "scripted brains must not seize control of kingdoms");
            _sim.Tick(); // Phase 0 processes the queue
            Assert.That(_sim.Meta.AtWar(a, b), Is.True, "war must be declared by the governor");
        }

        [Test]
        public void StaleAnswerIsDropped()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            var runner = new GovernorRunner(new GovernorSettings { Enabled = true },
                new ScriptedBrain(s => Decision(s.KingdomIndex, new GovernorAction(GovernorActionKind.Wait))));
            runner.Bind(_sim);
            runner.ScheduleYear();
            _sim.Meta.Kingdoms.MarkDead(a, _sim.Clock.Tick); // kingdom fell while the answer was in flight
            var queue = new EditCommandQueue();
            DrainDecisions(runner, queue);
            Assert.That(runner.Log.Entries.Count, Is.EqualTo(0), "a stale answer must be dropped, not applied");
        }

        [Test]
        public void PlayerTakenKingdomIsSkipped()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            var runner = new GovernorRunner(new GovernorSettings { Enabled = true }, ScriptedBrain.Wait());
            runner.Bind(_sim);
            runner.SetKingdomAI(a, false); // the player took kingdom A over
            runner.ScheduleYear();
            var queue = new EditCommandQueue();
            DrainDecisions(runner, queue);
            Assert.That(runner.Log.Entries.Count, Is.EqualTo(1));
            Assert.That(runner.Log.Entries[0].Kingdom, Is.EqualTo(b), "only the AI kingdom gets a decision");
        }

        [Test]
        public void CounterpartyMustAcceptPeace()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            var queue = new EditCommandQueue();
            EditSystems.Install(_sim, queue);
            _sim.Meta.Kingdom(a).LastWarEndTick = -10 * SimConst.TicksPerYear;
            queue.Metas.Add(new MetaCommand { Order = MetaOrder.DeclareWar, Kingdom = a, Other = b });
            _sim.Tick();
            Assert.That(_sim.Meta.AtWar(a, b), Is.True);

            // A proposes peace; B is a rule-driven kingdom and refuses while the war is young.
            queue.Metas.Add(new MetaCommand { Order = MetaOrder.ProposePeace, Kingdom = a, Other = b });
            _sim.Tick();
            Assert.That(_sim.Meta.Proposals.Count, Is.EqualTo(1), "peace must wait for the other side");
            RunUntilMonthlyTick();
            Assert.That(_sim.Meta.AtWar(a, b), Is.True, "young war with neutral opinion is not settled");
            Assert.That(_sim.Meta.Proposals.Count, Is.EqualTo(0), "the refused proposal is consumed");

            // With a friendly opinion the same proposal is accepted and the war ends.
            _sim.Meta.SetOpinion(b, a, 50);
            queue.Metas.Add(new MetaCommand { Order = MetaOrder.ProposePeace, Kingdom = a, Other = b });
            _sim.Tick();
            RunUntilMonthlyTick();
            Assert.That(_sim.Meta.AtWar(a, b), Is.False, "accepted peace ends the war");
        }

        [Test]
        public void PolicyCommandNormalizesWeights()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            var queue = new EditCommandQueue();
            EditSystems.Install(_sim, queue);
            queue.Policies.Add(new PolicyCommand { Kingdom = a, Food = 100, Housing = 0, Production = 0, Defense = 0, Culture = 0 });
            _sim.Tick();
            var policy = _sim.Meta.PolicyOf(a);
            Assert.That(policy.Food, Is.EqualTo(1f).Within(0.001f));
            Assert.That(policy.Production, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void GovernorBrainRunsKingAdvisorsThenFinalKing()
        {
            var calls = new Counter();
            string Reply() => calls.Next() switch
            {
                1 => "{\"thought\":\"check war\",\"advisors\":[\"war\",\"economy\",\"diplomacy\",\"religion\"],\"actions\":[]}",
                5 => "{\"thought\":\"final answer\",\"actions\":[{\"kind\":\"ProposePeace\",\"other\":2}]}",
                _ => "{\"thought\":\"advisor view\",\"actions\":[]}",
            };
            var brain = new GovernorBrain(new FakeLlmClient(Reply), new GovernorSettings { Enabled = true });
            var snap = new KingdomSnapshot { KingdomIndex = 1, MaxAdvisors = GovernorBrain.MaxAdvisors };
            var decision = brain.Decide(snap);

            Assert.That(calls.Value, Is.EqualTo(5), "1 king + at most 3 advisors + 1 final king call");
            Assert.That(brain.LastCalls, Is.EqualTo(5));
            Assert.That(decision.Thought, Is.EqualTo("final answer"), "the main model gives the final decision");
            Assert.That(decision.AdvisorOpinions.Count, Is.EqualTo(3), "the fourth domain is dropped");
            Assert.That(decision.Actions.Count, Is.EqualTo(1));
            Assert.That(decision.Actions[0].Kind, Is.EqualTo(GovernorActionKind.ProposePeace));
        }

        [Test]
        public void DecisionLogRoundTripsThroughBytesWithAdvisorsAndAiState()
        {
            var log = new DecisionLog();
            log.Add(720, 1, 3, "expand", new List<string> { "DeclareWar(2)" }, new List<string>());
            var entry = log.Entries[0];
            entry.AddAdvisor("war", "we can win");
            entry.AddAdvisor("economy", "food is fine");
            log.SetGoverned(3, true);
            log.SetPlayerTaken(5, true);

            var restored = new DecisionLog();
            restored.ReadBytes(log.WriteBytes());
            Assert.That(restored.Entries.Count, Is.EqualTo(1));
            Assert.That(restored.Entries[0].Thought, Is.EqualTo("expand"));
            Assert.That(restored.Entries[0].AdvisorNames, Does.Contain("war"));
            Assert.That(restored.Entries[0].AdvisorThoughts, Does.Contain("food is fine"));
            Assert.That(restored.Governed, Does.Contain(3));
            Assert.That(restored.PlayerTaken, Does.Contain(5));
            Assert.That(restored.LastForKingdom(3), Is.SameAs(restored.Entries[0]));

            // v1 payloads (no advisors / AI state) still load.
            var legacy = new DecisionLog();
            legacy.ReadBytes(new byte[] { 1, 0, 0, 0, 0 }); // version 1, zero entries
            Assert.That(legacy.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void PolicySaveRoundTrips()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            _sim.Meta.SetPolicy(a, KingdomPolicy.Normalized(50, 10, 10, 10, 20));
            _sim.Meta.AddProposal(ProposalKind.Peace, a, b, _sim.Clock.Tick);

            byte[] raw = PG.Persistence.PolicySave.Write(_sim);
            _sim.Meta.Policies.Clear();
            _sim.Meta.Proposals.Clear();
            var warnings = new List<string>();
            PG.Persistence.PolicySave.Apply(raw, _sim, warnings);

            Assert.That(warnings.Count, Is.EqualTo(0));
            var policy = _sim.Meta.PolicyOf(a);
            Assert.That(policy.Food, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(policy.Culture, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(_sim.Meta.Proposals.Count, Is.EqualTo(1));
            Assert.That(_sim.Meta.Proposals[0].From, Is.EqualTo(a));
        }

        void RunUntilMonthlyTick()
        {
            // KingdomSystem evaluates proposals on tick % TicksPerMonth == 2; one month suffices.
            for (int i = 0; i < SimConst.TicksPerMonth + 2; i++) _sim.Tick();
        }

        sealed class Counter
        {
            int _v;
            public int Next() => System.Threading.Interlocked.Increment(ref _v);
            public int Value => System.Threading.Volatile.Read(ref _v);
        }

        sealed class FakeLlmClient : ILlmClient
        {
            readonly Func<string> _next;
            public FakeLlmClient(Func<string> next) => _next = next;
            public LlmResponse Complete(LlmRequest request) => new LlmResponse { Content = _next(), TotalTokens = 10 };
        }
    }
}
