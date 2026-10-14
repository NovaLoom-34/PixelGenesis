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
    public sealed class UnitSpacingTests
    {
        ContentDB _db;
        SimWorld _sim;
        SimRandom _rng;
        UnitSpacingSystem _spacing;

        [OneTimeSetUp] public void Content()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp] public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(104));
            _rng = _sim.Rng.Get(RngStream.UnitAI);
            _spacing = new UnitSpacingSystem();
            byte soil = (byte)_db.Tiles.IdOf("tile.soil_low");
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++) _sim.World.SetGround(x, y, soil, ChangeSource.WorldGen);
        }

        [TearDown] public void Cleanup() { _spacing.Dispose(); _sim.Dispose(); }

        int Human(float x, float y)
        {
            int i = _sim.Units.Spawn(new UnitSpawnRequest {
                Species = _db.Species.IdOf("sp.human"), AgeYears = 25, Sex = -1,
                Pos = new float2(x, y), Subspecies = -1, Mother = EntityId.None, Father = EntityId.None
            }, _sim.Clock.Tick, ref _rng).Index;
            _sim.Units.Store.Stat(i, StatId.Size); // settle the stats now so a recompute cannot clear flags set by the test
            return i;
        }

        void Settle(int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                _sim.Units.Index.Rebuild(_sim.Units.Store);
                _spacing.Tick(_sim.Context);
            }
        }

        static float MinDistance(UnitStore u)
        {
            float best = float.MaxValue;
            for (int a = 0; a < u.Alive.Length; a++)
                for (int b = a + 1; b < u.Alive.Length; b++)
                    best = math.min(best, math.distance(u.Pos[u.Alive[a]], u.Pos[u.Alive[b]]));
            return best;
        }

        [Test] public void FortyUnitsOnOnePointSpreadOut()
        {
            for (int n = 0; n < 40; n++) Human(32.5f, 32.5f);
            Settle(120);
            var u = _sim.Units.Store;
            Assert.That(MinDistance(u), Is.GreaterThan(0.4f), "No two units may stay stacked.");
            for (int a = 0; a < u.Alive.Length; a++)
                Assert.That(math.distance(u.Pos[u.Alive[a]], new float2(32.5f, 32.5f)), Is.LessThan(5f), "A crowd must not be flung across the map.");
        }

        [Test] public void SpreadingIsDeterministic()
        {
            var first = Run();
            Cleanup(); Setup();
            var second = Run();
            Assert.That(second, Is.EqualTo(first));

            float2[] Run()
            {
                for (int n = 0; n < 25; n++) Human(32.5f + (n % 3) * .05f, 32.5f);
                Settle(60);
                var u = _sim.Units.Store;
                var result = new float2[u.Alive.Length];
                for (int k = 0; k < result.Length; k++) result[k] = u.Pos[u.Alive[k]];
                return result;
            }
        }

        [Test] public void UnitsAreNeverPushedOntoWater()
        {
            byte ocean = (byte)_db.Tiles.IdOf("tile.deep_ocean");
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 30; x++) _sim.World.SetGround(x, y, ocean, ChangeSource.WorldGen);
            for (int n = 0; n < 30; n++) Human(30.5f, 32.5f);
            Settle(120);
            var u = _sim.Units.Store;
            for (int a = 0; a < u.Alive.Length; a++)
            {
                int2 t = u.Tile(u.Alive[a]);
                Assert.IsTrue(_sim.Units.Paths.CanStand(t.x, t.y, Mobility.Land), $"unit at {u.Pos[u.Alive[a]]} stands on water");
            }
        }

        [Test] public void ImmobileUnitStaysAndOthersMoveAroundIt()
        {
            int rock = Human(32.5f, 32.5f);
            _sim.Units.Store.Flags[rock] |= (uint)UnitFlags.Immobile;
            int other = Human(32.5f, 32.5f);
            Settle(60);
            var u = _sim.Units.Store;
            Assert.That(u.Pos[rock], Is.EqualTo(new float2(32.5f, 32.5f)));
            Assert.That(math.distance(u.Pos[other], u.Pos[rock]), Is.GreaterThan(0.4f));
        }

        [Test] public void CourtingAndFightingPairsAreNotPushedApart()
        {
            int a = Human(30.5f, 30.5f), b = Human(30.7f, 30.5f);
            var u = _sim.Units.Store;
            u.Task[a] = (byte)UnitTask.Mate; u.Target[a] = u.IdOf(b);
            u.Task[b] = (byte)UnitTask.Mate; u.Target[b] = u.IdOf(a);
            int c = Human(40.5f, 40.5f), d = Human(40.7f, 40.5f);
            u.Task[c] = (byte)UnitTask.Attack; u.Target[c] = u.IdOf(d);
            Settle(60);
            Assert.That(math.distance(u.Pos[a], u.Pos[b]), Is.EqualTo(0.2f).Within(0.01f));
            Assert.That(math.distance(u.Pos[c], u.Pos[d]), Is.EqualTo(0.2f).Within(0.01f));
        }

        [Test] public void FlyersAndWalkersDoNotPushEachOther()
        {
            int walker = Human(32.5f, 32.5f);
            int flyer = Human(32.5f, 32.5f);
            _sim.Units.Store.Flags[flyer] |= (uint)UnitFlags.Fly;
            Settle(30);
            var u = _sim.Units.Store;
            Assert.That(u.Pos[walker], Is.EqualTo(new float2(32.5f, 32.5f)));
            Assert.That(u.Pos[flyer], Is.EqualTo(new float2(32.5f, 32.5f)));
        }

        [Test] public void ArrivalSlotsAreStableAndInsideTheirDisc()
        {
            for (long uid = 1; uid <= 200; uid++)
            {
                float2 a = UnitSpacingSystem.SlotOffset(uid, 0.35f), b = UnitSpacingSystem.SlotOffset(uid, 0.35f);
                Assert.That(a, Is.EqualTo(b), "a unit keeps the same spot");
                Assert.That(math.length(a), Is.LessThanOrEqualTo(0.35f + 1e-4f));
            }
        }

        [Test] public void SpreadUsesTheWholeRangeForFleeBearingsAndDistances()
        {
            float lo = 1f, hi = -1f, sum = 0f;
            for (long uid = 1; uid <= 400; uid++)
            {
                float v = UnitSpacingSystem.Spread(uid, 0xF1EE);
                lo = math.min(lo, v); hi = math.max(hi, v); sum += v;
            }
            Assert.That(lo, Is.LessThan(-0.9f));
            Assert.That(hi, Is.GreaterThan(0.9f));
            Assert.That(math.abs(sum / 400f), Is.LessThan(0.15f), "bearings must not lean to one side");
        }

        [Test] public void SpawnedGroupKeepsItsGapAndStaysOnLand()
        {
            byte ocean = (byte)_db.Tiles.IdOf("tile.deep_ocean");
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 31; x++) _sim.World.SetGround(x, y, ocean, ChangeSource.WorldGen);
            var placed = new float2[40];
            SpawnSpread.Begin();
            for (int n = 0; n < placed.Length; n++)
            {
                placed[n] = SpawnSpread.Next(_sim.Units, Mobility.Land, new float2(34.5f, 32.5f), placed.Length, 3f, ref _rng);
                Assert.IsTrue(_sim.Units.Paths.CanStand((int)math.floor(placed[n].x), (int)math.floor(placed[n].y), Mobility.Land), $"{placed[n]} is not land");
            }
            float best = float.MaxValue;
            for (int a = 0; a < placed.Length; a++)
                for (int b = a + 1; b < placed.Length; b++) best = math.min(best, math.distance(placed[a], placed[b]));
            Assert.That(best, Is.GreaterThan(0.4f), "a dropped group must not be a pile");
        }

        // The path service needs region data this fixture does not build, so the approach is a straight walk; the last
        // stage (the destination tile) is the real MoveTo, which is where the arrival spot and tolerance apply.
        readonly System.Collections.Generic.HashSet<int> _arrived = new System.Collections.Generic.HashSet<int>();

        void Walk(int i, int2 dest)
        {
            if (_arrived.Contains(i)) return; // like a real task: once there, the unit works instead of walking
            var u = _sim.Units.Store;
            if (math.all(u.Tile(i) == dest)) { if (_sim.Units.MoveTo(i, dest, 0.05f, out _)) _arrived.Add(i); }
            else _sim.Units.StepToward(i, (float2)dest + 0.5f, 0.05f);
        }

        [Test] public void CrowdWalkingToOneTileEndsSpreadOutAndSettled()
        {
            _arrived.Clear();
            var ids = new int[30];
            for (int n = 0; n < ids.Length; n++) ids[n] = Human(10.5f + (n % 6) * .7f, 10.5f + (n / 6) * .7f);
            var dest = new int2(40, 40);
            var u = _sim.Units.Store;
            for (int tick = 0; tick < 900; tick++)
            {
                _sim.Units.Paths.BeginTick();
                foreach (int i in ids) Walk(i, dest);
                _sim.Units.Index.Rebuild(u);
                _spacing.Tick(_sim.Context);
            }
            float best = float.MaxValue;
            for (int a = 0; a < ids.Length; a++)
            {
                Assert.That(math.distance(u.Pos[ids[a]], new float2(40.5f, 40.5f)), Is.LessThan(4f), "everyone arrives");
                for (int b = a + 1; b < ids.Length; b++) best = math.min(best, math.distance(u.Pos[ids[a]], u.Pos[ids[b]]));
            }
            Assert.That(best, Is.GreaterThan(0.3f), "no pixel-perfect pile at the destination");

            // Settled: another second of simulation moves nobody noticeably (no tug-of-war between arrival and spacing).
            var before = new float2[ids.Length];
            for (int a = 0; a < ids.Length; a++) before[a] = u.Pos[ids[a]];
            for (int tick = 0; tick < 20; tick++)
            {
                _sim.Units.Paths.BeginTick();
                foreach (int i in ids) Walk(i, dest);
                _sim.Units.Index.Rebuild(u);
                _spacing.Tick(_sim.Context);
            }
            for (int a = 0; a < ids.Length; a++) Assert.That(math.distance(before[a], u.Pos[ids[a]]), Is.LessThan(0.1f), "units jitter at the destination");
        }
    }
}
