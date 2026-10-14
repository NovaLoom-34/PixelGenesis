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
    public sealed class CraftChainTests
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
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(3403));
            _rng = _sim.Rng.Get(RngStream.Civ);
        }

        [TearDown]
        public void Teardown() => _sim?.Dispose();

        [Test]
        public void SmithCanProduceAnItemWithoutMetalStock()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            _sim.Civ.PlaceBuilding(city, _sim.Civ.BldSmithy, new int2(32, 32), 1f, 0);
            var smith = _sim.Units.Spawn(new UnitSpawnRequest
            {
                Species = _db.Species.IdOf("sp.human"),
                AgeYears = 25,
                Pos = new float2(32.5f, 31.5f),
                Subspecies = -1,
                Mother = EntityId.None,
                Father = EntityId.None
            }, 0, ref _rng);
            int smithIndex = smith.Index;
            _sim.Units.Store.City[smithIndex] = city;
            _sim.Units.Store.Job[smithIndex] = (byte)(_sim.Civ.JobSmith + 1);
            _sim.Civ.RecountResidents(_sim.Units.Store);

            Craft.Make(_sim.Units, _sim.Civ, smithIndex, city, 1, ref _rng);

            Assert.That(_sim.Civ.Items.Length, Is.EqualTo(1));
            Assert.That(_sim.Civ.Items[0].City, Is.EqualTo(city));
            Assert.That(_sim.Civ.Items[0].Owner, Is.EqualTo(-1));
        }

        [Test]
        public void EquippedScytheAndPickaxeRaiseTheirMatchingWorkYield()
        {
            var smith = _sim.Units.Spawn(new UnitSpawnRequest
            {
                Species = _db.Species.IdOf("sp.human"),
                AgeYears = 25,
                Pos = new float2(32.5f, 31.5f),
                Subspecies = -1,
                Mother = EntityId.None,
                Father = EntityId.None
            }, 0, ref _rng);
            int unit = smith.Index;
            float farmWithoutTool = CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.farmYield");
            float mineWithoutTool = CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.mineYield");
            Assert.That(farmWithoutTool, Is.EqualTo(1f));
            Assert.That(mineWithoutTool, Is.EqualTo(1f));

            int wood = _db.Materials.IdOf("mat.wood");
            int common = _db.ItemQualities.IdOrDefault("q.common");
            if (common < 0) common = 0;
            int scythe = _sim.Civ.CreateItem(_db.EquipmentTypes.IdOf("eq.scythe"), wood, common, 0, 0, -1);
            _sim.Units.Store.Equip[unit * UnitStore.EquipSlots + (int)EquipSlot.Weapon] = scythe;
            Assert.That(CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.farmYield"), Is.GreaterThan(1f));
            Assert.That(CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.mineYield"), Is.EqualTo(1f));

            int pickaxe = _sim.Civ.CreateItem(_db.EquipmentTypes.IdOf("eq.pickaxe"), wood, common, 0, 0, -1);
            _sim.Units.Store.Equip[unit * UnitStore.EquipSlots + (int)EquipSlot.Weapon] = pickaxe;
            Assert.That(CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.mineYield"), Is.GreaterThan(1f));
            Assert.That(CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.farmYield"), Is.EqualTo(1f));
        }

        [Test]
        public void WornOutToolBreaksAndItsDurabilitySurvivesCitySaveRoundTrip()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            var worker = _sim.Units.Spawn(new UnitSpawnRequest
            {
                Species = _db.Species.IdOf("sp.human"),
                AgeYears = 25,
                Pos = new float2(32.5f, 31.5f),
                Subspecies = -1,
                Mother = EntityId.None,
                Father = EntityId.None
            }, 0, ref _rng);
            int unit = worker.Index;
            int tool = _sim.Civ.CreateItem(_db.EquipmentTypes.IdOf("eq.scythe"), _db.Materials.IdOf("mat.wood"),
                _db.ItemQualities.IdOf("q.common"), 0, _sim.Units.Store.Uid[unit], city);
            _sim.Units.Store.Equip[unit * UnitStore.EquipSlots + (int)EquipSlot.Weapon] = tool;
            _sim.Civ.ItemDurability[tool] = 1;

            Assert.That(CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.farmYield"), Is.GreaterThan(1f));
            CivJobs.WearEquipment(_sim.Units, unit, "pct.farmYield");
            Assert.That(_sim.Civ.ItemDurability[tool], Is.EqualTo(0));
            Assert.That(_sim.Civ.Items[tool].Dead, Is.EqualTo(1));
            Assert.That(_sim.Units.Store.Equip[unit * UnitStore.EquipSlots + (int)EquipSlot.Weapon], Is.EqualTo(-1));
            Assert.That(CivJobs.EquipmentYieldMultiplier(_sim.Units, unit, "pct.farmYield"), Is.EqualTo(1f));

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) _sim.Civ.Write(writer);
            stream.Position = 0;
            var restored = new CivWorld(_sim.World, _db);
            try
            {
                using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
                restored.Read(reader);
                Assert.That(restored.ItemDurability[tool], Is.EqualTo(0));
                Assert.That(restored.Items[tool].Dead, Is.EqualTo(1));
            }
            finally { restored.Dispose(); }
        }

        [Test]
        public void LegacyCitySaveLoadsItemsAtFullDurability()
        {
            int city = _sim.Civ.CreateCity(new int2(32, 32), _db.Species.IdOf("sp.human"), -1, 0, 0, 0);
            int item = _sim.Civ.CreateItem(_db.EquipmentTypes.IdOf("eq.scythe"), _db.Materials.IdOf("mat.wood"),
                _db.ItemQualities.IdOf("q.common"), 0, 0, city);
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) _sim.Civ.Write(writer);
            // Strip the optional DUR1 tail (magic, count, one byte per item) and FLD1 tail (magic, count, 8 bytes per city).
            long legacyTail = sizeof(int) * 2 + _sim.Civ.Items.Length + sizeof(int) * 2 + sizeof(long) * _sim.Civ.CityCount;
            stream.SetLength(stream.Length - legacyTail);
            stream.Position = 0;

            var restored = new CivWorld(_sim.World, _db);
            try
            {
                using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
                restored.Read(reader);
                Assert.That(restored.ItemDurability[item], Is.EqualTo(CivWorld.MaxItemDurability));
            }
            finally { restored.Dispose(); }
        }
    }
}
