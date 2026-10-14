using System;
using PG.Content;
using PG.Core;
using PG.World;

namespace PG.Sim
{
    // Everything that makes up one running world. Owned by the game session; disposed when replaced.
    public sealed class SimWorld : IDisposable
    {
        public readonly ContentDB Content;
        public readonly GameClock Clock;
        public readonly EventBus Events;
        public readonly SimRandomProvider Rng;
        public readonly WorldMap World;
        public readonly RegionGraph Regions;
        public readonly NatureState Nature;
        public readonly UnitWorld Units;
        public readonly CivWorld Civ;
        public readonly MetaWorld Meta;
        public readonly PowerWorld Powers; // Bölüm 7 world state (drops, objects, automata)
        public readonly HistoryRecorder History;
        public readonly DisasterSystem Disasters = new DisasterSystem();
        public readonly SimulationPipeline Pipeline = new SimulationPipeline();

        public SimWorld(ContentDB content, WorldMap world, SimRandomProvider rng, GameClock clock = null, string worldName = null)
        {
            Content = content;
            World = world;
            Rng = rng;
            Clock = clock ?? new GameClock();
            Events = new EventBus();
            World.Events = Events;
            WorldName = string.IsNullOrEmpty(worldName) ? "World" : worldName;

            Regions = new RegionGraph(world);
            Regions.RebuildAll();
            Events.ClearPending(); // the initial build is not news

            Nature = new NatureState(world, content, rng.WorldSeed);
            Units = new UnitWorld(world, Regions, content, Nature.Laws) { Events = Events };
            Civ = new CivWorld(world, content);
            Units.Civ = Civ;
            Meta = new MetaWorld(content);
            Units.Meta = Meta;
            Civ.Meta = Meta;
            Powers = new PowerWorld(world);
            Units.Store.Meta = Meta;
            History = new HistoryRecorder(this);
            Units.Store.Civ = Civ;
            Disasters.Population = () => Units.Store.Count;

            Pipeline.Add(new SeasonSystem());
            Pipeline.Add(new BiomeSpreadSystem());
            Pipeline.Add(new FeatureGrowthSystem());
            Pipeline.Add(new FireSystem());
            Pipeline.Add(new LavaSystem());
            Pipeline.Add(new TileRecoverySystem());
            Pipeline.Add(new TemperatureSystem(content));
            Pipeline.Add(new MoistureSystem());
            Pipeline.Add(new WindSystem());
            Pipeline.Add(new CloudSystem(content));
            Pipeline.Add(new EraSystem(content));
            Pipeline.Add(Disasters);
            Pipeline.Add(new UnitIndexSystem());
            Pipeline.Add(new UnitThinkSystem());
            Pipeline.Add(new UnitActSystem());
            Pipeline.Add(new UnitSpacingSystem());
            Pipeline.Add(new ProjectileSystem());
            Pipeline.Add(new UnitLifeSystem());
            Pipeline.Add(new DiseaseSystem());
            Pipeline.Add(new MetamorphosisSystem());
            Pipeline.Add(new ZombieSystem());
            Pipeline.Add(new EvolutionSystem());
            Pipeline.Add(new NeuronLifeSystem());
            Pipeline.Add(new UnitCleanupSystem());
            Pipeline.Add(new AnimalSpawnSystem());
            Pipeline.Add(new SettlementSystem());
            Pipeline.Add(new CityPlannerSystem());
            Pipeline.Add(new BuildingSystem());
            Pipeline.Add(new CityEconomySystem());
            Pipeline.Add(new BoatSystem());
            Pipeline.Add(new VehicleSystem());
            Pipeline.Add(new KingdomSystem());
            Pipeline.Add(new CultureSystem());
            Pipeline.Add(new InventionSystem());
            Pipeline.Add(new PlotSystem());
            Pipeline.Add(new WarSystem());
            Pipeline.Add(new EventsFlushSystem());
            Pipeline.Add(new RegionRebuildSystem());
        }

        public string WorldName { get; set; }
        public string ScenarioId { get; set; } = "";
        public int ScenarioStartYear { get; set; }
        public ScenarioStatus ScenarioOutcome { get; set; } = ScenarioStatus.Sandbox;
        public ulong Seed => Rng.WorldSeed;
        public int Population => Units.Store.Count;

        // Worldgen step 10 (Bölüm 3.13): a derived generator so the shared streams stay as a fresh world expects.
        public int SpawnInitialAnimals()
        {
            var rng = SimRandom.Derive(Rng.WorldSeed, 0xA41A, 0);
            return AnimalSpawnSystem.SpawnInitial(Units, World, Content, Clock.Tick, ref rng);
        }

        public SimContext Context => new SimContext(World, Clock, Events, Content, Rng, Regions, Nature, Units, Civ, Meta, Powers);

        public void Tick()
        {
            Pipeline.Tick(Context);
            History.UpdatePeriodicSummary();
            Clock.AdvanceTick();
        }

        // Player edits while paused: apply queued commands without advancing time.
        public void ApplyInputWhilePaused() => Pipeline.RunPhase(Context, SimPhase.InputCommands);

        public void Dispose()
        {
            Pipeline.Dispose();
            Powers.Dispose();
            Meta.Dispose();
            Civ.Dispose();
            Units.Dispose();
            Nature.Dispose();
            Regions.Dispose();
            World.Dispose();
        }
    }
}
