using System;
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
    public sealed class ClimateAgricultureTests
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
        public void Setup() => _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(71204));

        [TearDown]
        public void Cleanup() => _sim?.Dispose();

        [Test]
        public void SeasonCycleUsesDataRulesAndWinterIncreasesHunger()
        {
            _sim.Nature.Laws.Set(_sim.Nature.LawSeasons, true, 1f);
            var spring = SeasonState.From(0, _sim.Nature.Laws, _db.SeasonRules,
                _sim.Nature.LawSeasons, _sim.Nature.LawEternalSummer, _sim.Nature.LawEternalWinter);
            var winter = SeasonState.From(9, _sim.Nature.Laws, _db.SeasonRules,
                _sim.Nature.LawSeasons, _sim.Nature.LawEternalSummer, _sim.Nature.LawEternalWinter);
            var nextSpring = SeasonState.From(12, _sim.Nature.Laws, _db.SeasonRules,
                _sim.Nature.LawSeasons, _sim.Nature.LawEternalSummer, _sim.Nature.LawEternalWinter);

            Assert.That(spring.Current, Is.EqualTo(Season.Spring));
            Assert.That(winter.Current, Is.EqualTo(Season.Winter));
            Assert.That(nextSpring.Current, Is.EqualTo(spring.Current));
            Assert.That(nextSpring.HungerMul, Is.EqualTo(spring.HungerMul));
            Assert.That(winter.HungerMul, Is.GreaterThan(spring.HungerMul));
            Assert.That(winter.CropGrowthMul, Is.LessThan(spring.CropGrowthMul));
        }

        [Test]
        public void FixedClimateControlRestoresZoneClimateBeforeLaterSimulationPhases()
        {
            const int zoneIndex = 0;
            var zone = _sim.World.Zones[zoneIndex];
            zone.TemperatureC = 18;
            zone.Moisture = 150;
            zone.DryMonths = 0;
            zone.Drought = 0;
            _sim.World.Zones[zoneIndex] = zone;
            _sim.Nature.Laws.Set(_sim.Nature.LawSeasons, true, 1f);
            _sim.Nature.Laws.Set(_sim.Nature.LawClouds, false, 0f);
            _sim.Pipeline.Add(new FixedClimateControlSystem(_sim.World));

            _sim.Tick();

            zone = _sim.World.Zones[zoneIndex];
            Assert.That(zone.TemperatureC, Is.EqualTo(18));
            Assert.That(zone.Moisture, Is.EqualTo(150));
            Assert.That(zone.DryMonths, Is.EqualTo(0));
            Assert.That(zone.Drought, Is.EqualTo(0));
        }

        [Test]
        public void RainRechargesMoistureAndHotDryMonthsTriggerThenClearDrought()
        {
            const int zoneIndex = 0;
            var map = _sim.World;
            // Water-dominant zones pin moisture at 255 and ocean neighbours recharge it; the blank test map is deep ocean,
            // so make the whole map land: a hot zone with no water anywhere nearby has to dry out.
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    map.SetGround(x, y, (byte)_db.Tiles.IdOf("tile.soil_low"), ChangeSource.WorldGen);
            map.RebuildDerivedData();
            var zone = map.Zones[zoneIndex];
            zone.Moisture = 20;
            zone.TemperatureC = 40;
            zone.DryMonths = 2;
            zone.Drought = 0;
            map.Zones[zoneIndex] = zone;

            new MoistureSystem().Tick(_sim.Context);

            zone = map.Zones[zoneIndex];
            Assert.That(zone.Moisture, Is.LessThan(20));
            Assert.That(zone.DryMonths, Is.EqualTo(3));
            Assert.That(zone.Drought, Is.EqualTo(1));

            _sim.Nature.AddMoisture(zoneIndex, 300);
            Assert.That(map.Zones[zoneIndex].Moisture, Is.EqualTo(byte.MaxValue));
            new MoistureSystem().Tick(_sim.Context);

            zone = map.Zones[zoneIndex];
            Assert.That(zone.Moisture, Is.GreaterThanOrEqualTo(104));
            Assert.That(zone.DryMonths, Is.EqualTo(0));
            Assert.That(zone.Drought, Is.EqualTo(0));
        }

        [Test]
        public void AutomaticFloodPlacementTargetsLowlandNearARiver()
        {
            var map = _sim.World;
            byte highland = (byte)_db.Tiles.IdOf("tile.mountain");
            byte lowland = (byte)_db.Tiles.IdOf("tile.soil_low");
            byte river = (byte)_db.Tiles.IdOf("tile.river");
            for (int i = 0; i < map.TileCount; i++)
            {
                map.Ground[i] = highland;
                map.Flags[i] = map.Tables.TypeFlags[highland];
            }

            int candidate = map.Index(29, 30);
            map.Ground[candidate] = lowland;
            map.Flags[candidate] = map.Tables.TypeFlags[lowland];
            int stream = map.Index(31, 32);
            map.Ground[stream] = river;
            map.Flags[stream] = (ushort)(map.Tables.TypeFlags[river] | (ushort)TileFlags.River);
            map.RebuildDerivedData();

            var rng = new SimRandom(221UL, 17UL);
            Assert.That(DisasterSystem.TryPlaceFlood(map, ref rng, out var at), Is.True);
            Assert.That(at, Is.EqualTo(new int2(29, 30)));

            map.Flags[stream] &= unchecked((ushort)~(ushort)TileFlags.River);
            rng = new SimRandom(221UL, 17UL);
            Assert.That(DisasterSystem.TryPlaceFlood(map, ref rng, out _), Is.False);
        }

        [Test]
        public void FloodingCityFieldLinksFamineCauseAndSurvivesCivSaveRoundTrip()
        {
            var map = _sim.World;
            int2 field = new int2(8, 8);
            int human = _db.Species.IdOf("sp.human");
            int style = _db.BuildingStyles.IdOf("style.human");
            int city = _sim.Civ.CreateCity(field, human, -1, style, _sim.Clock.Tick, 9001);
            int zoneIndex = map.ZoneIndexOf(field.x, field.y);
            _sim.Civ.ClaimZone(city, zoneIndex);
            var zone = map.Zones[zoneIndex];
            zone.TemperatureC = 20;
            zone.Moisture = byte.MaxValue;
            zone.Drought = 0;
            map.Zones[zoneIndex] = zone;
            int tile = map.Index(field.x, field.y);
            map.Ground[tile] = _sim.Civ.TileField;
            long floodRecedesAt = _sim.Clock.Tick + CivWorld.FloodCropMemoryMonths * SimConst.TicksPerMonth;
            byte shallow = (byte)_db.Tiles.IdOf("tile.shallow");

            WorldObjects.Submerge(_sim.Context, _sim.Powers, field, shallow, floodRecedesAt);

            Assert.That(_sim.Civ.HasRecentFloodCropLoss(city, _sim.Clock.Tick), Is.True);
            Assert.That(CityEconomySystem.ClimateCause(_sim.Civ, city, _sim.Clock.Tick), Is.EqualTo(FamineCause.Flood));
            Assert.That(CityEconomySystem.ClimateCause(_sim.Civ, city,
                floodRecedesAt + CivWorld.FloodCropMemoryMonths * SimConst.TicksPerMonth + 1), Is.EqualTo(FamineCause.None));

            byte[] saved;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) _sim.Civ.Write(writer);
                saved = stream.ToArray();
            }
            var restored = new CivWorld(map, _db);
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(saved))) restored.Read(reader);
                Assert.That(restored.HasRecentFloodCropLoss(city, _sim.Clock.Tick), Is.True);
            }
            finally { restored.Dispose(); }
        }

        [Test]
        public void FloodingUnplantedLandDoesNotLinkFamineCause()
        {
            var map = _sim.World;
            int2 tilePos = new int2(8, 8);
            int human = _db.Species.IdOf("sp.human");
            int style = _db.BuildingStyles.IdOf("style.human");
            int city = _sim.Civ.CreateCity(tilePos, human, -1, style, _sim.Clock.Tick, 9002);
            int zoneIndex = map.ZoneIndexOf(tilePos.x, tilePos.y);
            _sim.Civ.ClaimZone(city, zoneIndex);
            var zone = map.Zones[zoneIndex];
            zone.TemperatureC = 20;
            zone.Moisture = byte.MaxValue;
            zone.Drought = 0;
            map.Zones[zoneIndex] = zone;
            map.Ground[map.Index(tilePos.x, tilePos.y)] = (byte)_db.Tiles.IdOf("tile.soil_low");
            byte shallow = (byte)_db.Tiles.IdOf("tile.shallow");

            WorldObjects.Submerge(_sim.Context, _sim.Powers, tilePos, shallow, _sim.Clock.Tick + SimConst.TicksPerMonth);

            Assert.That(_sim.Civ.HasRecentFloodCropLoss(city, _sim.Clock.Tick), Is.False);
            Assert.That(CityEconomySystem.ClimateCause(_sim.Civ, city, _sim.Clock.Tick), Is.EqualTo(FamineCause.None));
        }

        [Test]
        public void LegacyCivSaveWithoutFloodMemoryLoadsWithNoFloodCause()
        {
            int human = _db.Species.IdOf("sp.human");
            int style = _db.BuildingStyles.IdOf("style.human");
            _sim.Civ.CreateCity(new int2(8, 8), human, -1, style, _sim.Clock.Tick, 9003);
            byte[] saved;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) _sim.Civ.Write(writer);
                saved = stream.ToArray();
            }
            Array.Resize(ref saved, saved.Length - 8 - 8 * _sim.Civ.CityCount); // Strip the optional FLD1 extension.
            var restored = new CivWorld(_sim.World, _db);
            try
            {
                using (var reader = new BinaryReader(new MemoryStream(saved))) restored.Read(reader);
                Assert.That(restored.HasRecentFloodCropLoss(0, _sim.Clock.Tick), Is.False);
            }
            finally { restored.Dispose(); }
        }

        [Test]
        public void DryFieldsReduceYieldAndIrrigationBuffersLowMoisture()
        {
            var dry = new ZoneData { TemperatureC = 20, Moisture = 0 };
            var wet = new ZoneData { TemperatureC = 20, Moisture = byte.MaxValue };

            float dryYield = CropClimate.GrowthFactor(dry, false);
            float irrigatedDryYield = CropClimate.GrowthFactor(dry, true);
            float wetYield = CropClimate.GrowthFactor(wet, false);

            Assert.That(irrigatedDryYield, Is.GreaterThan(dryYield));
            Assert.That(wetYield, Is.GreaterThan(irrigatedDryYield));
        }
    }
}
