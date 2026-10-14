using System.IO;
using NUnit.Framework;
using PG.Boot;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Persistence;
using PG.Sim;
using PG.World;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class SimulationDetailLodTests
    {
        const int SaveSlot = 898;
        ContentDB _db;
        SimWorld _sim;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup() => _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(1701));

        [TearDown]
        public void Cleanup()
        {
            _sim?.Dispose();
            _sim = null;
            SaveSystem.Delete(SaveSlot);
        }

        [Test]
        public void UnitThinkingIsEvenlyAndDeterministicallyPartitioned()
        {
            for (int divisions = 1; divisions <= 4; divisions++)
            {
                var perTick = new int[divisions];
                for (long uid = 1; uid <= 32; uid++)
                {
                    int scheduled = 0;
                    for (long tick = 0; tick < divisions; tick++)
                    {
                        if (!UnitThinkSystem.IsScheduledForThink(tick, uid, divisions)) continue;
                        scheduled++;
                        perTick[tick]++;
                    }
                    Assert.That(scheduled, Is.EqualTo(1), $"uid {uid} should have one slice in {divisions} ticks");
                    for (long tick = 0; tick < divisions * 4; tick++)
                        Assert.That(UnitThinkSystem.IsScheduledForThink(tick, uid, divisions),
                            Is.EqualTo(UnitThinkSystem.IsScheduledForThink(tick + divisions, uid, divisions)));
                }

                int min = perTick[0], max = perTick[0];
                for (int tick = 1; tick < divisions; tick++)
                {
                    min = System.Math.Min(min, perTick[tick]);
                    max = System.Math.Max(max, perTick[tick]);
                }
                Assert.That(max - min, Is.LessThanOrEqualTo(1), "stable UIDs should be evenly spread across slices");
            }
        }

        [Test]
        public void SimulationDetailDivisionValueRoundsAndClamps()
        {
            Assert.That(UnitThinkSystem.Divisions(1f), Is.EqualTo(1));
            Assert.That(UnitThinkSystem.Divisions(2.6f), Is.EqualTo(3));
            Assert.That(UnitThinkSystem.Divisions(0f), Is.EqualTo(1));
            Assert.That(UnitThinkSystem.Divisions(9f), Is.EqualTo(4));
        }

        [Test]
        public void SimulationDetailSettingRoundTripsWithWorldLaws()
        {
            int law = _sim.Nature.LawSimulationDetail;
            Assert.That(law, Is.GreaterThanOrEqualTo(0));
            _sim.Nature.Laws.Set(law, true, 4f);
            SaveSystem.Save(SaveSlot, _sim);

            var state = SaveSystem.Load(SaveSlot, _db);
            using (var restored = new SimWorld(_db, state.World, state.Rng, state.Clock, state.WorldName))
            {
                state.ApplyTo(restored);
                Assert.That(restored.Nature.Laws.IsOn(restored.Nature.LawSimulationDetail), Is.True);
                Assert.That(restored.Nature.Laws.Value(restored.Nature.LawSimulationDetail), Is.EqualTo(4f));
            }
        }

        [Test]
        public void FourWayUnitThinkingRemainsDeterministicForSameSeedAndSetting()
        {
            Assert.That(Diagnostics.DeterminismCheck(_db,
                new PG.WorldGen.WorldGenSettings { Seed = 5831, Size = PG.WorldGen.MapSizePreset.Small },
                1200, 4), Is.True);
        }
    }
}
