using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;
using EntityId = PG.Core.EntityId;

namespace PG.Tests.EditMode
{
    public sealed class UnitCourtshipTests
    {
        ContentDB _db;
        SimWorld _sim;
        SimRandom _rng;

        [OneTimeSetUp] public void Content()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }
        [SetUp] public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(103));
            _rng = _sim.Rng.Get(RngStream.UnitAI);
        }
        [TearDown] public void Cleanup() => _sim.Dispose();

        int Adult(int sex)
        {
            int i = _sim.Units.Spawn(new UnitSpawnRequest {
                Species = _db.Species.IdOf("sp.human"), AgeYears = 25, Sex = sex,
                Pos = new float2(32,32), Subspecies = -1, Mother = EntityId.None, Father = EntityId.None
            }, _sim.Clock.Tick, ref _rng).Index;
            _sim.Units.Store.Wants[i] = 1;
            return i;
        }

        [Test] public void OneAdultCanInitiateCourtshipAndExistingLivingPartnerIsRespected()
        {
            int a = Adult(0), b = Adult(1), c = Adult(1);
            var u = _sim.Units.Store;
            Assert.IsTrue(UnitCourtship.CanPair(_sim.Units, a, b));
            u.Wants[b] = 0;
            Assert.IsTrue(UnitCourtship.CanPair(_sim.Units, a, b));
            u.Wants[b] = 1;
            u.Age[b] = (byte)AgeStage.Child;
            Assert.IsFalse(UnitCourtship.CanPair(_sim.Units, a, b));
            u.Age[b] = (byte)AgeStage.Adult;
            u.Mate[a] = u.IdOf(b); u.Mate[b] = u.IdOf(a);
            Assert.IsTrue(UnitCourtship.CanPair(_sim.Units, a, b));
            Assert.IsFalse(UnitCourtship.CanPair(_sim.Units, a, c));
        }

        [Test] public void ParentsAndSiblingsAreExcluded()
        {
            int a = Adult(0), b = Adult(1), parent = Adult(0);
            var u = _sim.Units.Store;
            u.Mother[a] = u.IdOf(parent); u.Mother[b] = u.IdOf(parent);
            Assert.IsFalse(UnitCourtship.CanPair(_sim.Units, a, b));
            Assert.IsFalse(UnitCourtship.CanPair(_sim.Units, parent, b));
        }

        [Test] public void CitySpeciesAreNotStoppedByTheWildlifeLocalCrowdLimit()
        {
            int first = -1;
            for (int n = 0; n < UnitLifeSystem.CrowdLimit; n++)
            {
                int unit = Adult(n & 1);
                if (first < 0) first = unit;
            }
            _sim.Units.Index.Rebuild(_sim.Units.Store);

            Assert.IsFalse(UnitLifeSystem.IsBlockedByLocalCarryingCapacity(_sim.Units, first));
        }

        [Test] public void WildlifeStillUsesTheLocalCrowdLimit()
        {
            int first = -1;
            int species = _db.Species.IdOf("sp.monkey");
            for (int n = 0; n < UnitLifeSystem.CrowdLimit; n++)
            {
                int unit = _sim.Units.Spawn(new UnitSpawnRequest {
                    Species = species, AgeYears = 15, Sex = n & 1,
                    Pos = new float2(32,32), Subspecies = -1,
                    Mother = EntityId.None, Father = EntityId.None
                }, _sim.Clock.Tick, ref _rng).Index;
                if (first < 0) first = unit;
            }
            _sim.Units.Index.Rebuild(_sim.Units.Store);

            Assert.IsTrue(UnitLifeSystem.IsBlockedByLocalCarryingCapacity(_sim.Units, first));
        }

        [Test] public void CourtshipDoesNotInterruptDangerOrReserveAnOldOneWayMateLink()
        {
            int a = Adult(0), b = Adult(1), c = Adult(1);
            var u = _sim.Units.Store;
            u.Task[b] = (byte)UnitTask.Flee;
            Assert.IsFalse(UnitCourtship.CanPair(_sim.Units, a, b));
            u.Task[b] = (byte)UnitTask.None;
            u.Mate[a] = u.IdOf(c); // a legacy one-way pregnancy link, not a reciprocal bond
            Assert.IsFalse(UnitCourtship.HasLivingBond(u, a));
            Assert.IsTrue(UnitCourtship.CanPair(_sim.Units, a, b));
        }

        [Test] public void CourtshipCompletesWhenOnlyInitiatorHasTheInitialUrge()
        {
            int a = Adult(0), b = Adult(1);
            var u = _sim.Units.Store;
            u.Wants[b] = 0; // the initiating adult can recruit a willing partner into the reciprocal courtship task
            u.Pos[b] = new float2(32.8f, 32);
            u.Task[a] = (byte)UnitTask.Mate;
            u.Target[a] = u.IdOf(b);
            var actions = new UnitActSystem();
            actions.Tick(_sim.Context);
            Assert.AreEqual((byte)UnitTask.Mate, u.Task[b]);
            Assert.AreEqual(u.IdOf(a), u.Target[b]);
            Assert.IsFalse(u.HasStatus(a, _sim.Units.StPregnant));
            for (int t = 0; t < UnitCourtship.DurationTicks + 2; t++)
            {
                _sim.Clock.AdvanceTick();
                actions.Tick(_sim.Context);
            }
            Assert.IsTrue(u.HasStatus(a, _sim.Units.StPregnant));
            Assert.AreEqual(0, u.Wants[a] & 1);
            Assert.AreEqual(0, u.Wants[b] & 1);
            Assert.AreEqual((byte)UnitTask.None, u.Task[a]);
            Assert.AreEqual((byte)UnitTask.None, u.Task[b]);
        }

        [Test] public void ConceptionIsReciprocalAndChildInheritsBothParents()
        {
            int a = Adult(0), b = Adult(1);
            var w = _sim.Units; var u = w.Store;
            UnitLife.Conceive(w, a, b, _sim.Clock.Tick, ref _rng);
            Assert.AreEqual(u.IdOf(b), u.Mate[a]);
            Assert.AreEqual(u.IdOf(a), u.Mate[b]);
            Assert.IsTrue(u.HasStatus(a, w.StPregnant));
            Assert.IsFalse(UnitCourtship.CanPair(w, a, b));
            int child = UnitLife.Birth(w, a, b, _sim.Clock.Tick, ref _rng);
            Assert.AreEqual(u.IdOf(a), u.Mother[child]);
            Assert.AreEqual(u.IdOf(b), u.Father[child]);
            Assert.AreEqual((byte)AgeStage.Baby, u.Age[child]);
            Assert.LessOrEqual(u.Sex[child], 1);
        }
    }
}
