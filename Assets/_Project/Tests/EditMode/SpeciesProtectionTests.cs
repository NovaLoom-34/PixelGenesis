using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Persistence;
using PG.Powers;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;
using EntityId = PG.Core.EntityId;

namespace PG.Tests.EditMode
{
    public sealed class SpeciesProtectionTests
    {
        const int SaveSlot = 897;
        ContentDB _db;
        SimWorld _sim;
        SimRandom _rng;

        [OneTimeSetUp]
        public void LoadContent()
        {
            PG.EditorTools.DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(8642));
            _rng = _sim.Rng.Get(RngStream.UnitAI);
        }

        [TearDown]
        public void Teardown()
        {
            _sim?.Dispose();
            _sim = null;
            SaveSystem.Delete(SaveSlot);
        }

        [Test]
        public void ProtectionCommandProtectsOnlySelectedSpeciesInTargetZone()
        {
            int species = _db.Species.IdOf("sp.human");
            int zone = _sim.World.ZoneIndexOf(12, 12);
            var edits = new EditCommandQueue();
            EditSystems.Install(_sim, edits);
            edits.Edits.Add(new EditCommand { Kind = EditKind.SetProtectedZone, Target = zone, A = checked((ushort)(species + 1)) });

            _sim.Tick();

            Assert.That(_sim.World.IsSpeciesProtectedAt(species, 12, 12), Is.True);
            Assert.That(_sim.World.IsSpeciesProtectedAt(species + 1, 12, 12), Is.False);
            Assert.That(_sim.World.IsSpeciesProtectedAt(species, 20, 12), Is.False);
        }

        [Test]
        public void ProtectionZoneRoundTripsThroughSaveLoad()
        {
            int species = _db.Species.IdOf("sp.elf");
            int zone = _sim.World.ZoneIndexOf(18, 26);
            ushort speciesIndexPlusOne = checked((ushort)(species + 1));
            _sim.World.SetProtectedSpecies(zone, speciesIndexPlusOne);
            ulong expectedHash = _sim.World.ComputeHash();
            SaveSystem.Save(SaveSlot, _sim);

            var state = SaveSystem.Load(SaveSlot, _db);
            using (var restored = new SimWorld(_db, state.World, state.Rng, state.Clock, state.WorldName))
            {
                state.ApplyTo(restored);
                Assert.That(restored.World.ComputeHash(), Is.EqualTo(expectedHash));
                Assert.That(restored.World.IsSpeciesProtectedAt(species, 18, 26), Is.True);
                Assert.That(restored.World.IsSpeciesProtectedAt(species, 26, 26), Is.False);
            }
        }

        [Test]
        public void ProtectedPreyIsIgnoredByHuntersOnlyInsideItsZone()
        {
            int predatorSpecies = _db.Species.IdOf("sp.piranha");
            int preySpecies = _db.Species.IdOf("sp.fish");
            int predator = Spawn(predatorSpecies, new float2(12.5f, 12.5f));
            int prey = Spawn(preySpecies, new float2(13.5f, 12.5f));
            _sim.Units.Index.Rebuild(_sim.Units.Store);
            Assert.That(_sim.Units.CountPreyNear(predator, 4f), Is.EqualTo(1));

            _sim.World.SetProtectedSpecies(_sim.World.ZoneIndexOf(13, 12), (ushort)(preySpecies + 1));
            Assert.That(_sim.Units.CountPreyNear(predator, 4f), Is.EqualTo(0));

            _sim.Units.Store.Pos[prey] = new float2(21.5f, 12.5f);
            _sim.Units.Index.Rebuild(_sim.Units.Store);
            Assert.That(_sim.Units.CountPreyNear(predator, 12f), Is.EqualTo(1));
        }

        int Spawn(int species, float2 position) => _sim.Units.Spawn(new UnitSpawnRequest
        {
            Species = species,
            AgeYears = 2f,
            Sex = -1,
            Pos = position,
            Subspecies = -1,
            Mother = EntityId.None,
            Father = EntityId.None,
        }, _sim.Clock.Tick, ref _rng).Index;
    }
}
