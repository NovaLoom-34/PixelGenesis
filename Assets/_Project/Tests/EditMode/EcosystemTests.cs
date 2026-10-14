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
    public sealed class EcosystemTests
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
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(71203));
            _rng = _sim.Rng.Get(RngStream.UnitAI);
        }

        [TearDown]
        public void Cleanup() => _sim?.Dispose();

        [Test]
        public void SpeciesDataResolvesAValidFoodWeb()
        {
            int predator = _db.Species.IdOf("sp.piranha");
            int fish = _db.Species.IdOf("sp.fish");
            int rabbit = _db.Species.IdOf("sp.rabbit");
            var piranha = _db.Species[predator];

            Assert.That(piranha.PreySpecies, Is.Not.Null);
            Assert.That(piranha.CanEatSpecies(fish), Is.True);
            Assert.That(piranha.CanEatSpecies(rabbit), Is.False);
            Assert.That(piranha.TrophicLevel, Is.GreaterThan(1f));
            Assert.That(piranha.BodySize, Is.GreaterThan(0f));
            Assert.That(piranha.FoodNeed, Is.GreaterThan(0f));
        }

        [Test]
        public void PredatorCountsOnlyNearbyWhitelistedPrey()
        {
            int piranha = Spawn("sp.piranha", new float2(20.5f, 20.5f));
            int fish = Spawn("sp.fish", new float2(21.5f, 20.5f));
            Spawn("sp.rabbit", new float2(20.8f, 20.5f));
            Spawn("sp.fish", new float2(30.5f, 20.5f));
            _sim.Units.Index.Rebuild(_sim.Units.Store);

            Assert.That(_sim.Units.CountPreyNear(piranha, 4f), Is.EqualTo(1));

            _sim.Units.Store.Pos[fish] = new float2(28.5f, 20.5f);
            _sim.Units.Index.Rebuild(_sim.Units.Store);
            Assert.That(_sim.Units.CountPreyNear(piranha, 4f), Is.EqualTo(0));
        }

        [Test]
        public void ZoneBiomassTracksForageAndRebuildsFromTileState()
        {
            int x = 12, y = 12;
            byte grassland = _db.Biomes[_db.Biomes.IdOf("bio.grassland")].MapValue;
            ushort berries = _db.Features[_db.Features.IdOf("feat.berry_bush")].MapValue;
            int zone = _sim.World.ZoneIndexOf(x, y);

            // The test map starts as deep ocean, where a land biome is rejected; give the tile land first.
            _sim.World.SetGround(x, y, (byte)_db.Tiles.IdOf("tile.soil_low"), ChangeSource.WorldGen);
            _sim.World.SetBiome(x, y, grassland, ChangeSource.WorldGen);
            float grassyBiomass = _sim.World.Zones[zone].FoodBiomass;
            Assert.That(grassyBiomass, Is.GreaterThan(0f));

            _sim.World.SetFeature(x, y, berries, ChangeSource.WorldGen);
            int tile = _sim.World.Index(x, y);
            float berryBiomass = _sim.World.Zones[zone].FoodBiomass;
            Assert.That(berryBiomass, Is.GreaterThan(0f));
            Assert.That(_sim.World.GetFeatureResource(tile), Is.GreaterThan(0));

            _sim.World.SetFeatureResource(x, y, 0);
            float depletedBiomass = _sim.World.Zones[zone].FoodBiomass;
            Assert.That(depletedBiomass, Is.LessThan(berryBiomass));

            _sim.World.RebuildDerivedData();
            Assert.That(_sim.World.Zones[zone].FoodBiomass, Is.EqualTo(depletedBiomass).Within(0.001f));
        }

        [Test]
        public void RiverBanksRegrowFoodFasterThanInlandTiles()
        {
            int riverX = 20, riverY = 20;
            _sim.World.Flags[_sim.World.Index(riverX, riverY)] |= (ushort)TileFlags.River;

            Assert.That(FeatureGrowth.FoodRegrowMultiplier(_sim.World, riverX + 1, riverY + 1),
                Is.EqualTo(FeatureGrowth.FloodplainFoodRegrowMultiplier));
            Assert.That(FeatureGrowth.FoodRegrowMultiplier(_sim.World, riverX + 2, riverY), Is.EqualTo(1f));
        }

        [Test]
        public void EggLayingSpeciesCannotStartAnotherClutchDuringIncubationCooldown()
        {
            int parrot = _db.Species.IdOf("sp.parrot");
            int female = SpawnWithSex(parrot, 0);
            int male = SpawnWithSex(parrot, 1);

            Assert.That(UnitCourtship.CanPair(_sim.Units, female, male), Is.True);
            int before = _sim.Units.Store.Count;
            UnitLife.Conceive(_sim.Units, female, male, _sim.Clock.Tick, ref _rng);
            Assert.That(_sim.Units.Store.Count, Is.GreaterThan(before));
            Assert.That(_sim.Units.Store.HasStatus(female, _sim.Units.StBreedingCooldown), Is.True);
            Assert.That(UnitCourtship.CanPair(_sim.Units, female, male), Is.False);

            _sim.Units.Store.RemoveStatus(female, _sim.Units.StBreedingCooldown);
            Assert.That(UnitCourtship.CanPair(_sim.Units, female, male), Is.True);
        }

        int Spawn(string speciesId, float2 position)
        {
            int species = _db.Species.IdOf(speciesId);
            return _sim.Units.Spawn(new UnitSpawnRequest
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

        float AdultAgeYears(int species) => _db.Species[species].BaseStats[(int)StatId.Lifespan] * 0.5f;

        int SpawnWithSex(int species, byte sex)
        {
            return _sim.Units.Spawn(new UnitSpawnRequest
            {
                Species = species,
                AgeYears = AdultAgeYears(species), // Adult stage is 18–80% of lifespan; the parrot's 2-year age is still a baby
                Sex = sex,
                Pos = new float2(20.5f + sex, 20.5f),
                Subspecies = -1,
                Mother = EntityId.None,
                Father = EntityId.None,
            }, _sim.Clock.Tick, ref _rng).Index;
        }
    }
}
