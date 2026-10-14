using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Powers;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    // Tests for dynamic alliance formation, common enemy coalitions, proposals to player,
    // leaving alliances, and the God Power (pw.alliance).
    public sealed class AllianceTests
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
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(103));
        }

        [TearDown]
        public void Teardown()
        {
            _sim.Dispose();
            _sim = null;
        }

        int CreateKingdomWithCity(int2 pos)
        {
            int species = _db.Species.IdOf("sp.human");
            int city = _sim.Civ.CreateCity(pos, species, -1, 0, _sim.Clock.Tick, 0);
            _sim.Civ.ClaimZone(city, _sim.World.ZoneIndexOf(pos.x, pos.y)); // powers resolve cities by zone owner
            var rng = _sim.Rng.Get(RngStream.Meta);
            int k = _sim.Meta.CreateKingdom(city, species, PG.Core.EntityId.None, 0, _sim.Clock.Tick, ref rng);
            ref var cd = ref _sim.Civ.City(city);
            cd.Kingdom = k;
            cd.Loyalty = 100;
            _sim.Civ.Version++;
            return k;
        }

        [Test]
        public void KingdomsDesireAllianceWithPositiveOpinion()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));

            // Default neutral opinion: below 25
            _sim.Meta.SetOpinion(a, b, 10);
            _sim.Meta.SetOpinion(b, a, 10);
            Assert.That(KingdomSystem.WantsAlliance(_sim.Meta, a, b), Is.False);

            // Friendly opinion >= 25
            _sim.Meta.SetOpinion(a, b, 30);
            _sim.Meta.SetOpinion(b, a, 30);
            Assert.That(KingdomSystem.WantsAlliance(_sim.Meta, a, b), Is.True);
        }

        [Test]
        public void CommonEnemyEnablesCoalitionAllianceAtNeutralOpinion()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(32, 16));
            int enemy = CreateKingdomWithCity(new int2(48, 48));

            // a and b have neutral opinion (0) of each other
            _sim.Meta.SetOpinion(a, b, 5);
            _sim.Meta.SetOpinion(b, a, 5);
            Assert.That(KingdomSystem.WantsAlliance(_sim.Meta, a, b), Is.False);

            // Enemy declares war on both a and b
            _sim.Meta.Kingdom(enemy).LastWarEndTick = -10 * SimConst.TicksPerYear;
            WarSystem.Declare(_sim.Context, _sim.Meta, enemy, a, _sim.Meta.WarConquest, -1, _sim.Clock.Tick);
            WarSystem.Declare(_sim.Context, _sim.Meta, enemy, b, _sim.Meta.WarConquest, -1, _sim.Clock.Tick);

            // Now a and b share a common enemy!
            Assert.That(KingdomSystem.WantsAlliance(_sim.Meta, a, b), Is.True, "Kingdoms fighting the same enemy must desire an alliance");
        }

        [Test]
        public void AiProposesAllianceToPlayerControlledKingdom()
        {
            int ai = CreateKingdomWithCity(new int2(16, 16));
            int player = CreateKingdomWithCity(new int2(48, 48));

            // Player controls kingdom 'player'
            _sim.Meta.Kingdoms.Header(player).Flags |= MetaFlags.PlayerControlled;

            // Set high mutual opinion so AI wants to ally
            _sim.Meta.SetOpinion(ai, player, 40);
            _sim.Meta.SetOpinion(player, ai, 40);

            // Advance to annual alliance check (tick % TicksPerYear == 2)
            while (_sim.Clock.Tick % SimConst.TicksPerYear != 2) _sim.Tick();
            _sim.Tick(); // executes Alliances()

            // AI should have sent an alliance proposal to player
            bool foundProposal = false;
            foreach (var p in _sim.Meta.Proposals)
            {
                if (p.From == ai && p.To == player && p.Kind == ProposalKind.Alliance)
                {
                    foundProposal = true;
                    break;
                }
            }
            Assert.That(foundProposal, Is.True, "AI kingdom must propose an alliance to the player-controlled kingdom");
        }

        [Test]
        public void ManualPeaceProposalCarriesChosenTermsThroughMetaCommandQueue()
        {
            int proposer = CreateKingdomWithCity(new int2(16, 16));
            int receiver = CreateKingdomWithCity(new int2(48, 48));
            WarSystem.Declare(_sim.Context, _sim.Meta, proposer, receiver, _sim.Meta.WarConquest, -1, _sim.Clock.Tick);
            int cityToTransfer = _sim.Meta.Kingdom(proposer).Capital;
            var terms = new PeaceTerms
            {
                TributeGold = 37,
                CityToTransfer = cityToTransfer,
                VassalizeProposer = true,
                NonAggressionYears = 8,
            };
            var edits = new EditCommandQueue();
            EditSystems.Install(_sim, edits);
            edits.Metas.Add(new MetaCommand
            {
                Order = MetaOrder.ProposePeace,
                Kingdom = proposer,
                Other = receiver,
                HasPeaceTerms = true,
                PeaceTerms = terms,
            });
            edits.Metas.Add(new MetaCommand { Order = MetaOrder.ProposePeace, Kingdom = receiver, Other = proposer });

            _sim.Tick();

            Assert.That(_sim.Meta.Proposals, Has.Count.EqualTo(1));
            var proposal = _sim.Meta.Proposals[0];
            Assert.That(proposal.Kind, Is.EqualTo(ProposalKind.Peace));
            Assert.That(proposal.From, Is.EqualTo(proposer));
            Assert.That(proposal.To, Is.EqualTo(receiver));
            Assert.That(proposal.PeaceTerms.TributeGold, Is.EqualTo(37));
            Assert.That(proposal.PeaceTerms.CityToTransfer, Is.EqualTo(cityToTransfer));
            Assert.That(proposal.PeaceTerms.VassalizeProposer, Is.True);
            Assert.That(proposal.PeaceTerms.NonAggressionYears, Is.EqualTo(8));
        }

        [Test]
        public void VassalageConcessionLetsAiAcceptPeaceBeforeWarWearinessThreshold()
        {
            int proposer = CreateKingdomWithCity(new int2(16, 16));
            int receiver = CreateKingdomWithCity(new int2(48, 48));
            WarSystem.Declare(_sim.Context, _sim.Meta, proposer, receiver, _sim.Meta.WarConquest, -1, _sim.Clock.Tick);
            _sim.Meta.SetOpinion(receiver, proposer, -10);
            var terms = new PeaceTerms { VassalizeProposer = true };
            _sim.Meta.AddPeaceProposal(proposer, receiver, _sim.Clock.Tick, terms);

            KingdomSystem.EvaluateProposals(_sim.Context, _sim.Meta, _sim.Clock.Tick);

            Assert.That(_sim.Meta.WarBetween(proposer, receiver), Is.EqualTo(-1));
            Assert.That(_sim.Meta.VassalOf(proposer, _sim.Clock.Tick), Is.EqualTo(receiver));
        }

        [Test]
        public void DeclaringWarBreaksNonAggressionPactAndRecordsPenaltyAndCause()
        {
            int attacker = CreateKingdomWithCity(new int2(16, 16));
            int defender = CreateKingdomWithCity(new int2(48, 48));
            long tick = _sim.Clock.Tick;
            _sim.Meta.SetTreaty(TreatyKind.NonAggression, attacker, defender, tick, 5 * SimConst.TicksPerYear);
            _sim.Meta.SetOpinion(defender, attacker, 25);

            int invalidWar = WarSystem.Declare(_sim.Context, _sim.Meta, attacker, defender, -1, -1, tick);
            Assert.That(invalidWar, Is.EqualTo(-1));
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.NonAggression, attacker, defender, tick), Is.True);
            Assert.That(_sim.Meta.Opinion(defender, attacker), Is.EqualTo(25));

            int war = WarSystem.Declare(_sim.Context, _sim.Meta, attacker, defender, _sim.Meta.WarConquest, -1, tick);

            Assert.That(war, Is.GreaterThanOrEqualTo(0));
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.NonAggression, attacker, defender, tick), Is.False);
            Assert.That(_sim.Meta.Opinion(defender, attacker), Is.EqualTo(25 - WarSystem.TreatyBreachOpinionPenalty));
            Assert.That(_sim.Meta.TryGetWarPlan(war, out var plan), Is.True);
            Assert.That(plan.Cause, Is.EqualTo(WarCause.TreatyBreach));
        }

        [Test]
        public void LeaveAllianceRemovesKingdomAndDissolvesWhenEmpty()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));

            KingdomSystem.Ally(_sim.Context, _sim.Meta, a, b, _sim.Clock.Tick);
            int al = _sim.Meta.Kingdom(a).Alliance;
            Assert.That(al, Is.GreaterThanOrEqualTo(0));
            Assert.That(_sim.Meta.Kingdom(b).Alliance, Is.EqualTo(al));
            Assert.That(_sim.Meta.Alliances.IsAlive(al), Is.True);

            // A leaves the alliance
            bool left = KingdomSystem.LeaveAlliance(_sim.Context, _sim.Meta, a, _sim.Clock.Tick);
            Assert.That(left, Is.True);
            Assert.That(_sim.Meta.Kingdom(a).Alliance, Is.EqualTo(-1));

            // With only 1 member (b) remaining, the alliance dissolves
            Assert.That(_sim.Meta.Kingdom(b).Alliance, Is.EqualTo(-1));
            Assert.That(_sim.Meta.Alliances.IsAlive(al), Is.False);
        }

        [Test]
        public void GodPowerAllianceDirectlyAlliesTwoKingdomsAndEndsWar()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));

            // Start war between a and b
            _sim.Meta.Kingdom(a).LastWarEndTick = -10 * SimConst.TicksPerYear;
            WarSystem.Declare(_sim.Context, _sim.Meta, a, b, _sim.Meta.WarConquest, -1, _sim.Clock.Tick);
            Assert.That(_sim.Meta.AtWar(a, b), Is.True);

            // Execute pw.alliance god power on a and b
            ushort alliancePow = (ushort)_db.Powers.IdOf("pw.alliance");
            var queue = new PowerCommandQueue();
            PowerSystems.Install(_sim, queue, true);

            int2 cityA = _sim.Civ.City(_sim.Meta.Kingdom(a).Capital).Center;
            int2 cityB = _sim.Civ.City(_sim.Meta.Kingdom(b).Capital).Center;

            queue.Enqueue(new PowerCommand
            {
                PowerId = alliancePow,
                From = cityA,
                To = cityB,
                Brush = new BrushSpec(BrushShape.Circle, 0),
                Tick = _sim.Clock.Tick,
            });

            _sim.Tick(); // Phase 0 executes power commands

            Assert.That(_sim.Meta.AtWar(a, b), Is.False, "Alliance god power must end active war");
            int alA = _sim.Meta.Kingdom(a).Alliance;
            int alB = _sim.Meta.Kingdom(b).Alliance;
            Assert.That(alA, Is.GreaterThanOrEqualTo(0));
            Assert.That(alA, Is.EqualTo(alB), "Both kingdoms must be in the same alliance");
        }

        [Test]
        public void DeclarationAssignsAllowedCauseAndGoalAndPolicySavePersistsThem()
        {
            int attacker = CreateKingdomWithCity(new int2(16, 16));
            int defender = CreateKingdomWithCity(new int2(48, 48));
            int target = _sim.Meta.Kingdom(defender).Capital;
            int war = WarSystem.Declare(_sim.Context, _sim.Meta, attacker, defender, _sim.Meta.WarReligious, target, _sim.Clock.Tick);

            Assert.That(war, Is.GreaterThanOrEqualTo(0));
            Assert.That(_sim.Meta.TryGetWarPlan(war, out var plan), Is.True);
            Assert.That(plan.Cause, Is.EqualTo(WarCause.ReligiousDifference));
            // war.religious allows only plunder and vassalize, so even with a target city the goal falls back to Plunder.
            Assert.That(plan.Goal, Is.EqualTo(WarGoal.Plunder));

            byte[] raw = PG.Persistence.PolicySave.Write(_sim);
            _sim.Meta.WarPlans.Clear();
            var warnings = new System.Collections.Generic.List<string>();
            PG.Persistence.PolicySave.Apply(raw, _sim, warnings);

            Assert.That(warnings, Is.Empty);
            Assert.That(_sim.Meta.TryGetWarPlan(war, out var restored), Is.True);
            Assert.That(restored.Cause, Is.EqualTo(plan.Cause));
            Assert.That(restored.Goal, Is.EqualTo(plan.Goal));
        }

        [Test]
        public void CapturedObjectiveCreatesOnePeaceProposal()
        {
            int attacker = CreateKingdomWithCity(new int2(16, 16));
            int defender = CreateKingdomWithCity(new int2(48, 48));
            int target = _sim.Meta.Kingdom(defender).Capital;
            int war = WarSystem.Declare(_sim.Context, _sim.Meta, attacker, defender, _sim.Meta.WarConquest, target, _sim.Clock.Tick);

            WarSystem.Capture(_sim.Context, _sim.Civ, _sim.Meta, target, attacker, war, _sim.Clock.Tick);

            Assert.That(_sim.Meta.Proposals.Count, Is.EqualTo(1));
            Assert.That(_sim.Meta.Proposals[0].Kind, Is.EqualTo(ProposalKind.Peace));
            Assert.That(_sim.Meta.Proposals[0].From, Is.EqualTo(defender));
            Assert.That(_sim.Meta.Proposals[0].To, Is.EqualTo(attacker));
        }

        [Test]
        public void LegacyPolicySaveFormatThreeLoadsWithoutWarPlans()
        {
            byte[] formatThree = { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            var warnings = new System.Collections.Generic.List<string>();

            PG.Persistence.PolicySave.Apply(formatThree, _sim, warnings);

            Assert.That(warnings, Is.Empty);
            Assert.That(_sim.Meta.WarPlans, Is.Empty);
            Assert.That(KingdomPolicy.Balanced.Tax, Is.EqualTo(0.5f));
        }

        [Test]
        public void AcceptedPeaceTransfersOnlyTheProposedCity()
        {
            int ceding = CreateKingdomWithCity(new int2(16, 16));
            int receiving = CreateKingdomWithCity(new int2(48, 48));
            int species = _db.Species.IdOf("sp.human");
            int city = _sim.Civ.CreateCity(new int2(20, 16), species, -1, 0, _sim.Clock.Tick, 0);
            _sim.Civ.City(city).Kingdom = ceding;
            _sim.Civ.City(city).Loyalty = 100;
            var terms = new PeaceTerms { CityToTransfer = city, NonAggressionYears = WarSystem.PeaceBanYears };
            var proposal = _sim.Meta.AddPeaceProposal(ceding, receiving, _sim.Clock.Tick, terms);

            KingdomSystem.ApplyAcceptedProposal(_sim.Context, _sim.Meta, proposal, _sim.Clock.Tick);

            Assert.That(_sim.Civ.KingdomOf(city), Is.EqualTo(receiving));
            Assert.That(_sim.Civ.KingdomOf(_sim.Meta.Kingdom(receiving).Capital), Is.EqualTo(receiving));
        }

        [Test]
        public void AcceptedPeaceCannotTransferTheProposersLastCity()
        {
            int ceding = CreateKingdomWithCity(new int2(16, 16));
            int receiving = CreateKingdomWithCity(new int2(48, 48));
            int lastCity = _sim.Meta.Kingdom(ceding).Capital;
            var proposal = _sim.Meta.AddPeaceProposal(ceding, receiving, _sim.Clock.Tick,
                new PeaceTerms { CityToTransfer = lastCity });

            KingdomSystem.ApplyAcceptedProposal(_sim.Context, _sim.Meta, proposal, _sim.Clock.Tick);

            Assert.That(_sim.Civ.KingdomOf(lastCity), Is.EqualTo(ceding));
        }

        [Test]
        public void AcceptedPeaceCannotTransferAnotherKingdomsCity()
        {
            int ceding = CreateKingdomWithCity(new int2(16, 16));
            int receiving = CreateKingdomWithCity(new int2(48, 48));
            int thirdKingdom = CreateKingdomWithCity(new int2(32, 48));
            int protectedCity = _sim.Meta.Kingdom(thirdKingdom).Capital;
            var proposal = _sim.Meta.AddPeaceProposal(ceding, receiving, _sim.Clock.Tick,
                new PeaceTerms { CityToTransfer = protectedCity });

            KingdomSystem.ApplyAcceptedProposal(_sim.Context, _sim.Meta, proposal, _sim.Clock.Tick);

            Assert.That(_sim.Civ.KingdomOf(protectedCity), Is.EqualTo(thirdKingdom));
        }

        [Test]
        public void AcceptedPeacePaysTributeAndCreatesTimeLimitedNonAggression()
        {
            int ceding = CreateKingdomWithCity(new int2(16, 16));
            int receiving = CreateKingdomWithCity(new int2(48, 48));
            int cedingCity = _sim.Meta.Kingdom(ceding).Capital;
            int receivingCity = _sim.Meta.Kingdom(receiving).Capital;
            _sim.Civ.City(cedingCity).Gold = 100;
            var terms = new PeaceTerms { TributeGold = 25, NonAggressionYears = 7 };
            var proposal = _sim.Meta.AddPeaceProposal(ceding, receiving, _sim.Clock.Tick, terms);

            KingdomSystem.ApplyAcceptedProposal(_sim.Context, _sim.Meta, proposal, _sim.Clock.Tick);

            Assert.That(_sim.Civ.City(cedingCity).Gold, Is.EqualTo(75));
            Assert.That(_sim.Civ.City(receivingCity).Gold, Is.EqualTo(25));
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.NonAggression, ceding, receiving,
                _sim.Clock.Tick + 6 * SimConst.TicksPerYear), Is.True);
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.NonAggression, ceding, receiving,
                _sim.Clock.Tick + 8 * SimConst.TicksPerYear), Is.False);
        }

        [Test]
        public void AcceptedPeaceCanVassalizeTheProposingKingdom()
        {
            int subject = CreateKingdomWithCity(new int2(16, 16));
            int overlord = CreateKingdomWithCity(new int2(48, 48));
            var terms = new PeaceTerms { VassalizeProposer = true, NonAggressionYears = WarSystem.PeaceBanYears };
            var proposal = _sim.Meta.AddPeaceProposal(subject, overlord, _sim.Clock.Tick, terms);

            KingdomSystem.ApplyAcceptedProposal(_sim.Context, _sim.Meta, proposal, _sim.Clock.Tick);

            Assert.That(_sim.Meta.VassalOf(subject, _sim.Clock.Tick), Is.EqualTo(overlord));
        }

        [Test]
        public void PeaceTermsSurvivePolicySaveRoundTrip()
        {
            int ceding = CreateKingdomWithCity(new int2(16, 16));
            int receiving = CreateKingdomWithCity(new int2(48, 48));
            var terms = new PeaceTerms { TributeGold = 73, CityToTransfer = _sim.Meta.Kingdom(ceding).Capital,
                VassalizeProposer = true, NonAggressionYears = 9 };
            _sim.Meta.AddPeaceProposal(ceding, receiving, _sim.Clock.Tick, terms);

            byte[] raw = PG.Persistence.PolicySave.Write(_sim);
            _sim.Meta.Proposals.Clear();
            PG.Persistence.PolicySave.Apply(raw, _sim, new System.Collections.Generic.List<string>());

            Assert.That(_sim.Meta.Proposals, Has.Count.EqualTo(1));
            var restored = _sim.Meta.Proposals[0].PeaceTerms;
            Assert.That(restored.TributeGold, Is.EqualTo(73));
            Assert.That(restored.CityToTransfer, Is.EqualTo(terms.CityToTransfer));
            Assert.That(restored.VassalizeProposer, Is.True);
            Assert.That(restored.NonAggressionYears, Is.EqualTo(9));
        }

        [Test]
        public void ArmyMoraleFallsWithoutRationsAndRecoversWhenFullySupplied()
        {
            Assert.That(WarSystem.AdvanceMorale(60, 0, 5), Is.EqualTo(55));
            Assert.That(WarSystem.AdvanceMorale(60, 5, 5), Is.EqualTo(62));
            Assert.That(WarSystem.AdvanceMorale(99, 5, 5), Is.EqualTo(100));
        }

        [Test]
        public void ArmyLogisticsSurvivePolicySaveRoundTrip()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            int war = WarSystem.Declare(_sim.Context, _sim.Meta, a, b, _sim.Meta.WarConquest,
                _sim.Meta.Kingdom(b).Capital, _sim.Clock.Tick);
            _sim.Meta.Armies.Add(new ArmyData { Kingdom = a, War = war, HomeCity = _sim.Meta.Kingdom(a).Capital,
                TargetCity = _sim.Meta.Kingdom(b).Capital, State = ArmyState.Marching, Soldiers = 5 });
            _sim.Meta.SetArmyLogistics(0, 14, 73, 1.5f);

            byte[] raw = PG.Persistence.PolicySave.Write(_sim);
            _sim.Meta.ArmyLogistics.Clear();
            PG.Persistence.PolicySave.Apply(raw, _sim, new System.Collections.Generic.List<string>());

            Assert.That(_sim.Meta.TryGetArmyLogistics(0, out var logistics), Is.True);
            Assert.That(logistics.Rations, Is.EqualTo(14));
            Assert.That(logistics.Morale, Is.EqualTo(73));
            Assert.That(logistics.SiegeProgress, Is.EqualTo(1.5f));
        }

        [Test]
        public void SiegeTakesLongerAgainstFortificationsAndLessWithBetterArmyPower()
        {
            int openTown = WarSystem.SiegeMonthsRequired(0, 10f);
            int fortifiedTown = WarSystem.SiegeMonthsRequired(4, 10f);
            int equippedArmy = WarSystem.SiegeMonthsRequired(4, 20f);

            Assert.That(fortifiedTown, Is.GreaterThan(openTown));
            Assert.That(equippedArmy, Is.LessThan(fortifiedTown));
            Assert.That(openTown, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void PlunderLootIsLimitedByAvailableStockAndArmyCapacity()
        {
            Assert.That(WarSystem.PlunderGoldAmount(1000), Is.EqualTo(250));
            Assert.That(WarSystem.PlunderGoldAmount(3), Is.EqualTo(0));
            Assert.That(WarSystem.PlunderRationsAmount(20, 6), Is.EqualTo(6));
            Assert.That(WarSystem.PlunderRationsAmount(4, 12), Is.EqualTo(4));
            Assert.That(WarSystem.PlunderRationsAmount(-4, 12), Is.EqualTo(0));
        }

        [Test]
        public void DestructionGoalRazesOnlyAnEnemyObjectiveAndEndsTheWar()
        {
            int attacker = CreateKingdomWithCity(new int2(16, 16));
            int defender = CreateKingdomWithCity(new int2(48, 48));
            int target = _sim.Meta.Kingdom(defender).Capital;
            int war = WarSystem.Declare(_sim.Context, _sim.Meta, attacker, defender,
                _sim.Meta.WarConquest, target, _sim.Clock.Tick);
            _sim.Meta.SetWarPlan(war, WarCause.BorderDispute, WarGoal.Destruction);
            int army = _sim.Meta.Armies.Length;
            _sim.Meta.Armies.Add(new ArmyData { Kingdom = attacker, War = war, State = ArmyState.Sieging, Soldiers = 2 });

            Assert.That(WarSystem.CompleteDestructionGoal(_sim.Context, _sim.Civ, _sim.Meta,
                army, target, _sim.Clock.Tick), Is.True);
            Assert.That(_sim.Civ.CityAlive(target), Is.False);
            Assert.That(_sim.Meta.Wars.IsAlive(war), Is.False);
            Assert.That(_sim.Meta.Wars.Items[war].Result, Is.EqualTo(WarResult.AttackerWon));
        }

        [Test]
        public void DestructionGoalCannotRazeAThirdKingdomCity()
        {
            int attacker = CreateKingdomWithCity(new int2(16, 16));
            int defender = CreateKingdomWithCity(new int2(32, 16));
            int third = CreateKingdomWithCity(new int2(48, 48));
            int target = _sim.Meta.Kingdom(defender).Capital;
            int war = WarSystem.Declare(_sim.Context, _sim.Meta, attacker, defender,
                _sim.Meta.WarConquest, target, _sim.Clock.Tick);
            _sim.Meta.SetWarPlan(war, WarCause.BorderDispute, WarGoal.Destruction);
            int army = _sim.Meta.Armies.Length;
            _sim.Meta.Armies.Add(new ArmyData { Kingdom = attacker, War = war, State = ArmyState.Sieging, Soldiers = 2 });
            int thirdCity = _sim.Meta.Kingdom(third).Capital;

            Assert.That(WarSystem.CompleteDestructionGoal(_sim.Context, _sim.Civ, _sim.Meta,
                army, thirdCity, _sim.Clock.Tick), Is.False);
            Assert.That(_sim.Civ.CityAlive(thirdCity), Is.True);
            Assert.That(_sim.Meta.Wars.IsAlive(war), Is.True);
        }

        [Test]
        public void TradePriceRisesWithScarcityAndFallsWithSurplus()
        {
            float scarce = KingdomSystem.TradePriceMultiplier(0);
            float balanced = KingdomSystem.TradePriceMultiplier(KingdomSystem.TradeKeep);
            float surplus = KingdomSystem.TradePriceMultiplier(KingdomSystem.TradeKeep * 2);

            Assert.That(scarce, Is.GreaterThan(balanced));
            Assert.That(balanced, Is.EqualTo(1f));
            Assert.That(surplus, Is.LessThan(balanced));
            Assert.That(scarce, Is.LessThanOrEqualTo(1.5f));
            Assert.That(surplus, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(KingdomSystem.TradePayment(1f, 10, 0), Is.GreaterThan(KingdomSystem.TradePayment(1f, 10, KingdomSystem.TradeKeep * 2)));
            Assert.That(KingdomSystem.TradeRouteCost(40f, 20), Is.GreaterThan(KingdomSystem.TradeRouteCost(10f, 20)));
            Assert.That(KingdomSystem.TradeRouteCost(40f, 20), Is.GreaterThan(KingdomSystem.TradeRouteCost(40f, 5)));
        }

        [Test]
        public void SeaTradeRespectsStorageGoldAndRouteProfit()
        {
            int localRoute = BoatSystem.TradeBoatDeliverable(20, 12, 1f, 0, 10f, 1000);
            int unaffordable = BoatSystem.TradeBoatDeliverable(20, 12, 1f, 0, 10f, 1);
            int unprofitable = BoatSystem.TradeBoatDeliverable(20, 12, 1f, 0, 10000f, 1000);

            Assert.That(localRoute, Is.EqualTo(12));
            Assert.That(unaffordable, Is.EqualTo(0));
            Assert.That(unprofitable, Is.EqualTo(0));
        }

        [Test]
        public void TradeAgreementImprovesPriceAndRouteSafetyWhileFaminePrioritizesFood()
        {
            Assert.That(KingdomSystem.TradePayment(10f, 10, KingdomSystem.TradeKeep, true),
                Is.LessThan(KingdomSystem.TradePayment(10f, 10, KingdomSystem.TradeKeep, false)));
            Assert.That(KingdomSystem.TradeAgreementRouteCost(100, 20), Is.LessThan(KingdomSystem.TradeRouteCost(100, 20)));
            Assert.That(KingdomSystem.TradeDemandPriority(true, 0), Is.GreaterThan(KingdomSystem.TradeDemandPriority(false, 0)));
            Assert.That(KingdomSystem.TradeDemandPriority(true, KingdomSystem.TradeKeep), Is.EqualTo(1f));
        }

        [Test]
        public void TradeEmbargoIsMutualAndSurvivesPolicySaveRoundTrip()
        {
            int a = CreateKingdomWithCity(new int2(16, 16));
            int b = CreateKingdomWithCity(new int2(48, 48));
            _sim.Meta.SetTreaty(TreatyKind.TradeEmbargo, a, b, _sim.Clock.Tick, 5 * SimConst.TicksPerYear);

            Assert.That(_sim.Meta.HasTreaty(TreatyKind.TradeEmbargo, b, a, _sim.Clock.Tick), Is.True);
            Assert.That(_sim.Meta.StatusBetween(a, b, _sim.Clock.Tick), Is.EqualTo(DiplomaticStatus.TradeEmbargo));
            byte[] raw = PG.Persistence.PolicySave.Write(_sim);
            _sim.Meta.Treaties.Clear();
            PG.Persistence.PolicySave.Apply(raw, _sim, new System.Collections.Generic.List<string>());

            Assert.That(_sim.Meta.HasTreaty(TreatyKind.TradeEmbargo, a, b, _sim.Clock.Tick), Is.True);
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.TradeEmbargo, a, b,
                _sim.Clock.Tick + 5 * SimConst.TicksPerYear), Is.False);
            _sim.Meta.SetTreaty(TreatyKind.TradeAgreement, a, b, _sim.Clock.Tick, 10 * SimConst.TicksPerYear);
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.TradeEmbargo, a, b, _sim.Clock.Tick), Is.False);
            Assert.That(_sim.Meta.HasTreaty(TreatyKind.TradeAgreement, a, b, _sim.Clock.Tick), Is.True);
        }

        [Test]
        public void SuccessionCrisisChanceRisesAsClaimantCityLoyaltyFalls()
        {
            float stable = KingdomSystem.SuccessionCrisisChance(50);
            float discontent = KingdomSystem.SuccessionCrisisChance(0);
            float revolt = KingdomSystem.SuccessionCrisisChance(-50);

            Assert.That(stable, Is.GreaterThan(0f));
            Assert.That(discontent, Is.GreaterThan(stable));
            Assert.That(revolt, Is.GreaterThan(discontent));
            Assert.That(revolt, Is.LessThanOrEqualTo(KingdomSystem.MaxSuccessionCrisisChance));
        }

        [Test]
        public void FamineAddsCappedRebellionPressure()
        {
            Assert.That(KingdomSystem.FamineLoyaltyPenalty(0), Is.EqualTo(0));
            Assert.That(KingdomSystem.FamineLoyaltyPenalty(3), Is.EqualTo(12));
            Assert.That(KingdomSystem.FamineLoyaltyPenalty(20), Is.EqualTo(20));
        }

        [Test]
        public void TaxPolicyChangesTreasuryIncomeAndCityMoodAroundBalancedLevel()
        {
            Assert.That(CityEconomySystem.TaxRateMultiplier(0f), Is.EqualTo(0.5f));
            Assert.That(CityEconomySystem.TaxRateMultiplier(0.5f), Is.EqualTo(1f));
            Assert.That(CityEconomySystem.TaxRateMultiplier(1f), Is.EqualTo(1.5f));
            Assert.That(CityEconomySystem.TaxMoodModifier(0f), Is.GreaterThan(0));
            Assert.That(CityEconomySystem.TaxMoodModifier(1f), Is.LessThan(0));
        }

        [Test]
        public void TaxPolicySurvivesPolicySaveRoundTrip()
        {
            int kingdom = CreateKingdomWithCity(new int2(16, 16));
            _sim.Meta.SetPolicy(kingdom, KingdomPolicy.Normalized(20, 20, 20, 20, 20, 80));
            byte[] raw = PG.Persistence.PolicySave.Write(_sim);
            _sim.Meta.Policies.Clear();

            PG.Persistence.PolicySave.Apply(raw, _sim, new System.Collections.Generic.List<string>());

            Assert.That(_sim.Meta.PolicyOf(kingdom).Tax, Is.EqualTo(0.8f).Within(0.001f));
        }
    }
}
