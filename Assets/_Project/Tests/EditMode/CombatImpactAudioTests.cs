using System.Collections.Generic;
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
    public sealed class CombatImpactAudioTests
    {
        ContentDB _db;
        SimWorld _sim;
        SimRandom _rng;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(705));
            _rng = _sim.Rng.Get(RngStream.Combat);
        }

        [TearDown]
        public void Teardown() => _sim.Dispose();

        [Test]
        public void CombatImpactPublishesSoundKindAndWorldPosition()
        {
            int victim = _sim.Units.Spawn(new UnitSpawnRequest
            {
                Species = _db.Species.IdOf("sp.human"),
                Pos = new float2(18.5f, 22.5f),
                AgeYears = 25f,
                Sex = 0,
                Mother = EntityId.None,
                Father = EntityId.None,
                Subspecies = -1,
            }, _sim.Clock.Tick, ref _rng).Index;
            var hits = new List<UnitHitEvent>();
            _sim.Events.Subscribe<UnitHitEvent>(hits.Add);

            _sim.Units.CombatImpact(victim, 10f, -1, DeathCause.Killed, _sim.Clock.Tick, CombatImpactKind.Magic);
            _sim.Tick();

            Assert.That(hits, Has.Count.EqualTo(1));
            Assert.That(hits[0].Kind, Is.EqualTo(CombatImpactKind.Magic));
            Assert.That(hits[0].Position, Is.EqualTo(new float2(18.5f, 22.5f)));
        }
    }
}
