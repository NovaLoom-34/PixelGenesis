using System;
using System.IO;
using PG.Boot;
using PG.Content;
using PG.Core;
using PG.Render;
using PG.Sim;
using PG.UI;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using EntityId = PG.Core.EntityId;

namespace PG.EditorTools
{
    // Reproducible visual fixture in a fresh, unsaved Play Mode world. Never touches save slots.
    [InitializeOnLoad]
    public static class HousePreview
    {
        const string Active = "PG.HousePreview.Active", Frame = "PG.HousePreview.Frame";
        const string Village = "PG.HousePreview.Village";
        const string Gallery = "PG.HousePreview.Gallery";
        const string Trees = "PG.HousePreview.Trees";
        const string Savanna = "PG.HousePreview.Savanna";
        const string Ground = "PG.HousePreview.Ground";
        const string Units = "PG.HousePreview.Units";
        const string Professions = "PG.HousePreview.Professions";
        const string Architecture = "PG.HousePreview.Architecture";
        static HousePreview() { if (SessionState.GetBool(Active, false)) Hook(); }

        [MenuItem("PixelGenesis/Preview House Model")]
        public static void Run() => Start(false);

        [MenuItem("PixelGenesis/Preview Village Graphics")]
        public static void RunVillage() => Start(true);

        [MenuItem("PixelGenesis/Preview All Building Styles")]
        public static void RunStyleGallery() => Start(false, true);

        [MenuItem("PixelGenesis/Preview All Trees")]
        public static void RunTreeGallery() => Start(false, false, true);

        [MenuItem("PixelGenesis/Preview Savanna Trees")]
        public static void RunSavannaTrees() => Start(false, false, false, true);

        [MenuItem("PixelGenesis/Preview Ground Materials")]
        public static void RunGroundMaterials() => Start(false, false, false, false, true);

        [MenuItem("PixelGenesis/Preview Four Races")]
        public static void RunFourRaces() => Start(false, false, false, false, false, true);

        [MenuItem("PixelGenesis/Preview Professions")]
        public static void RunProfessions() => Start(false, false, false, false, false, false, true);

        [MenuItem("PixelGenesis/Preview Architecture States")]
        public static void RunArchitectureStates() => Start(false, false, false, false, false, false, false, true);

        static void Start(bool village, bool gallery = false, bool trees = false, bool savanna = false, bool ground = false, bool units = false, bool professions = false, bool architecture = false)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening the house preview.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            DataSync.Sync();
            EditorSceneManager.OpenScene("Assets/Scenes/GameBootstrap.unity");
            SessionState.SetBool(Active, true);
            SessionState.SetInt(Frame, 0);
            SessionState.SetBool(Village, village);
            SessionState.SetBool(Gallery, gallery);
            SessionState.SetBool(Trees, trees);
            SessionState.SetBool(Savanna, savanna);
            SessionState.SetBool(Ground, ground);
            SessionState.SetBool(Units, units);
            SessionState.SetBool(Professions, professions);
            SessionState.SetBool(Architecture, architecture);
            Hook();
            EditorApplication.EnterPlaymode();
        }

        static void Hook() { EditorApplication.update -= Update; EditorApplication.update += Update; }

        static void Update()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying) return;
            var host = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            if (host == null || host.Sim == null) return;
            int frame = SessionState.GetInt(Frame, 0) + 1;
            SessionState.SetInt(Frame, frame);
            try
            {
                bool village = SessionState.GetBool(Village, false);
                bool gallery = SessionState.GetBool(Gallery, false);
                bool trees = SessionState.GetBool(Trees, false);
                bool savanna = SessionState.GetBool(Savanna, false);
                bool ground = SessionState.GetBool(Ground, false);
                bool units = SessionState.GetBool(Units, false);
                bool professions = SessionState.GetBool(Professions, false);
                bool architecture = SessionState.GetBool(Architecture, false);
                string prefix = architecture ? "Logs/architecture-states" : professions ? "Logs/professions" : units ? "Logs/four-races" : ground ? "Logs/ground-materials" : savanna ? "Logs/savanna-trees" : trees ? "Logs/all-trees" : gallery ? "Logs/all-styles" : village ? "Logs/village" : "Logs/house";
                if (frame == 1) { if (architecture) SetupArchitectureStates(host); else if (professions) SetupProfessions(host); else if (units) SetupFourRaces(host); else if (ground) SetupGroundMaterials(host); else if (savanna) SetupSavannaTrees(host); else if (trees) SetupTreeGallery(host); else if (gallery) SetupStyleGallery(host); else if (village) SetupVillage(host); else Setup(host); }
                if (frame == 60 && (ground || units || professions || architecture)) Capture(prefix + "-wide.png", ground ? 50f : architecture ? 40f : 35f);
                if (frame == 90) Capture(prefix + "-in-game.png", architecture ? 35f : units || professions ? 20f : ground ? 35f : trees || savanna ? 45f : gallery ? 60f : 80f);
                if (frame == 100)
                {
                    var controller = Camera.main.GetComponent<CameraController>();
                    controller.Focus(new float2(host.Sim.World.Width / 2, host.Sim.World.Height / 2));
                    controller.ZoomTo(units || professions ? 10f : architecture || ground ? 20f : trees || savanna ? 28f : gallery ? 40f : village ? 28f : 18f);
                }
                if (units && (frame == 105 || frame == 125 || frame == 145)) SetAdultAttackPose(host, (frame - 105) / 20);
                if (units && (frame == 120 || frame == 140 || frame == 160))
                    Capture(prefix + "-attack-" + ((frame - 120) / 20 + 1) + ".png", 10f);
                if (units && frame == 165) SetAdultAttackPose(host, -1);
                if (frame == 180) Capture(prefix + "-closeup.png", units || professions ? 10f : architecture || ground ? 20f : trees || savanna ? 28f : gallery ? 40f : village ? 28f : 18f);
                if (frame < 200) return;
                SessionState.SetBool(Active, false);
                EditorApplication.update -= Update;
                if (!File.Exists(prefix + "-in-game.png") || !File.Exists(prefix + "-closeup.png") ||
                    ((ground || units || professions || architecture) && !File.Exists(prefix + "-wide.png")) ||
                    (units && (!File.Exists(prefix + "-attack-1.png") || !File.Exists(prefix + "-attack-2.png") || !File.Exists(prefix + "-attack-3.png"))))
                    throw new IOException("House preview screenshots were not created.");
                Debug.Log($"[HousePreview] OK: actual game render; village={village}; trees={trees}; ground={ground}; units={units}; professions={professions}; architecture={architecture}; screenshots {prefix}.");
                if (Application.isBatchMode) { EditorApplication.ExitPlaymode(); EditorApplication.Exit(0); }
            }
            catch (Exception e)
            {
                SessionState.SetBool(Active, false);
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void SetAdultAttackPose(GameBootstrap host, int attackFrame)
        {
            var u = host.Sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.Age[i] != (byte)AgeStage.Adult) continue;
                if (attackFrame < 0)
                {
                    u.Task[i] = (byte)UnitTask.None;
                    u.AttackCooldown[i] = 0;
                    continue;
                }
                short remaining = 0;
                for (short candidate = 1; candidate < 256; candidate++)
                    if (UnitAnimation.AttackFrame(candidate, u.Stat(i, StatId.AtkSpd)) == attackFrame)
                    {
                        remaining = candidate;
                        break;
                    }
                if (remaining == 0) throw new InvalidOperationException("Attack frame " + attackFrame + " is unreachable for " + host.Content.Species[u.Species[i]].Id);
                u.Task[i] = (byte)UnitTask.Attack;
                u.PathHandle[i] = -1;
                u.AttackCooldown[i] = remaining;
            }
        }

        static void SetupProfessions(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20261006, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            using (var batch = map.BeginBatch(ChangeSource.WorldGen))
                for (int y = cy - 38; y <= cy + 38; y++)
                    for (int x = cx - 66; x <= cx + 66; x++)
                    {
                        batch.SetGround(x, y, soil);
                        batch.SetBiome(x, y, grass);
                        batch.SetFeature(x, y, 0);
                        batch.SetFlag(x, y, TileFlags.SnowCover, false);
                        batch.SetFlag(x, y, TileFlags.Burning, false);
                        map.SetRoad(x, y, false);
                    }
            string[] jobs = { "job.farmer", "job.lumberjack", "job.miner", "job.builder" };
            int species = host.Content.Species.IdOf("sp.human");
            float lifespan = host.Content.Species[species].BaseStats[(int)StatId.Lifespan];
            var rng = SimRandom.Derive(20261006, 4260, 0);
            for (int column = 0; column < jobs.Length; column++)
            {
                int job = host.Content.Jobs.IdOf(jobs[column]);
                for (int row = 0; row < 2; row++)
                {
                    EntityId id = host.Sim.Units.Store.Spawn(new UnitSpawnRequest {
                        Species = species, Pos = new float2(cx - 12 + column * 8 + .5f, cy + 3 - row * 6 + .5f),
                        AgeYears = lifespan * .30f, Sex = row, Subspecies = -1,
                        Mother = EntityId.None, Father = EntityId.None
                    }, host.Sim.Clock.Tick, ref rng);
                    var u = host.Sim.Units.Store;
                    u.Job[id.Index] = (byte)(job + 1);
                    u.Task[id.Index] = (byte)UnitTask.Work;
                    u.Action[id.Index] = CivJobs.ActWork;
                    u.PathHandle[id.Index] = -1;
                    u.Facing[id.Index] = 0;
                }
            }
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(20f);
            Debug.Log("[HousePreview] Professions left to right: farmer/scythe, lumberjack/axe, miner/pickaxe, builder/hammer. Female above, male below; paused, no save slots.");
        }

        static void SetupFourRaces(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20261006, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            using (var batch = map.BeginBatch(ChangeSource.WorldGen))
                for (int y = cy - 38; y <= cy + 38; y++)
                    for (int x = cx - 66; x <= cx + 66; x++)
                    {
                        batch.SetGround(x, y, soil);
                        batch.SetBiome(x, y, grass);
                        batch.SetFeature(x, y, 0);
                        batch.SetFlag(x, y, TileFlags.SnowCover, false);
                        batch.SetFlag(x, y, TileFlags.Burning, false);
                        map.SetRoad(x, y, false);
                    }
            string[] raceIds = { "sp.human", "sp.orc", "sp.dwarf", "sp.elf" };
            string[] weaponIds = { "eq.sword", "eq.axe", "eq.hammer", "eq.sword" };
            uint[] bannerColors = { 0xB65D4AFFu, 0x8C6A47FFu, 0x587FABFFu, 0x638D59FFu };
            var rng = SimRandom.Derive(20261006, 4242, 0);
            for (int race = 0; race < raceIds.Length; race++)
            {
                int species = host.Content.Species.IdOf(raceIds[race]);
                float lifespan = host.Content.Species[species].BaseStats[(int)StatId.Lifespan];
                float[] ageFractions = { .30f, .30f, .12f, .04f };
                int[] members = new int[ageFractions.Length];
                EntityId adultMale = EntityId.None;
                for (int row = 0; row < ageFractions.Length; row++)
                {
                    EntityId id = host.Sim.Units.Store.Spawn(new UnitSpawnRequest {
                        Species = species,
                        Pos = new float2(cx - 12 + race * 8 + .5f, cy + 6 - row * 4 + .5f),
                        AgeYears = lifespan * ageFractions[row], Sex = row == 0 || row == 2 ? 0 : 1, Subspecies = -1,
                        Mother = EntityId.None, Father = EntityId.None
                    }, host.Sim.Clock.Tick, ref rng);
                    members[row] = id.Index;
                    if (row == 1) adultMale = id;
                }
                int city = host.Sim.Civ.CreateCity(new int2(cx - 12 + race * 8, cy + 70), species, -1, 0,
                    host.Sim.Clock.Tick, host.Sim.Units.Store.Uid[adultMale.Index]);
                int kingdom = host.Sim.Meta.CreateKingdom(city, species, adultMale,
                    host.Sim.Units.Store.Uid[adultMale.Index], host.Sim.Clock.Tick, ref rng);
                host.Sim.Civ.City(city).Kingdom = kingdom;
                host.Sim.Civ.City(city).Loyalty = 100;
                host.Sim.Meta.Kingdoms.Header(kingdom).Color = bannerColors[race];
                foreach (int member in members) host.Sim.Units.Store.City[member] = city;
                int weapon = host.Sim.Civ.CreateItem(host.Content.EquipmentTypes.IdOf(weaponIds[race]), 0, 1,
                    host.Sim.Clock.Tick, host.Sim.Units.Store.Uid[adultMale.Index], city);
                var item = host.Sim.Civ.Items[weapon];
                item.Owner = adultMale.Index;
                host.Sim.Civ.Items[weapon] = item;
                host.Sim.Units.Store.Equip[adultMale.Index * UnitStore.EquipSlots + (int)EquipSlot.Weapon] = weapon;
            }
            host.Sim.Civ.Version++;
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(20f);
            Debug.Log("[HousePreview] Four races left to right: human, orc, dwarf, elf. Rows top to bottom: unarmed adult female, race-weapon adult male, child, baby; four banner palettes; paused, no save slots.");
        }

        static void SetupGroundMaterials(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20261006, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte sand = (byte)host.Content.Tiles.IdOf("tile.sand");
            byte shallow = (byte)host.Content.Tiles.IdOf("tile.shallow");
            byte ocean = (byte)host.Content.Tiles.IdOf("tile.ocean");
            byte deep = (byte)host.Content.Tiles.IdOf("tile.deep_ocean");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            byte forest = host.Content.Biomes[host.Content.Biomes.IdOf("bio.forest")].MapValue;
            byte maple = host.Content.Biomes[host.Content.Biomes.IdOf("bio.maple")].MapValue;
            using (var batch = map.BeginBatch(ChangeSource.WorldGen))
                for (int y = cy - 56; y <= cy + 56; y++)
                    for (int x = cx - 92; x <= cx + 92; x++)
                    {
                        // A continuous material transect, not a generated island.
                        int bend = Mathf.RoundToInt(Mathf.Sin((y - cy) * .16f) * 2f);
                        int band = x - cx - bend;
                        byte tile = band < -56 ? soil : band < -31 ? soil : band < -6 ? soil
                            : band < 19 ? sand : band < 44 ? shallow : band < 69 ? ocean : deep;
                        byte biome = band < -56 ? grass : band < -31 ? forest : band < -6 ? maple : (byte)0;
                        batch.SetGround(x, y, tile);
                        batch.SetBiome(x, y, biome);
                        batch.SetFeature(x, y, 0);
                        batch.SetFlag(x, y, TileFlags.SnowCover, false);
                        batch.SetFlag(x, y, TileFlags.Burning, false);
                        map.SetRoad(x, y, false);
                    }
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(35f);
            Debug.Log("[HousePreview] Ground materials: meadow, forest, maple, sand, shallow/ocean/deep water; paused, no save slots.");
        }

        static void SetupSavannaTrees(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20260927, Size = MapSizePreset.Small });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte savanna = host.Content.Biomes[host.Content.Biomes.IdOf("bio.savanna")].MapValue;
            byte forest = host.Content.Biomes[host.Content.Biomes.IdOf("bio.forest")].MapValue;
            for (int y = cy - 27; y <= cy + 27; y++)
                for (int x = cx - 47; x <= cx + 47; x++)
                {
                    map.SetGround(x, y, soil, ChangeSource.WorldGen);
                    map.SetBiome(x, y, x < cx ? savanna : forest, ChangeSource.WorldGen);
                    map.SetFeature(x, y, 0, ChangeSource.WorldGen);
                }
            ushort acacia = host.Content.Features[host.Content.Features.IdOf("feat.tree_acacia")].MapValue;
            ushort palm = host.Content.Features[host.Content.Features.IdOf("feat.tree_palm")].MapValue;
            ushort oak = host.Content.Features[host.Content.Features.IdOf("feat.tree_oak")].MapValue;
            ushort pine = host.Content.Features[host.Content.Features.IdOf("feat.tree_pine")].MapValue;
            for (int i = 0; i < 28; i++)
            {
                int y = cy - 21 + (i * 19) % 43;
                map.SetFeature(cx - 43 + (i * 17) % 38, y, i % 6 == 0 ? palm : acacia, i % 5 == 0 ? 1 : 2, ChangeSource.WorldGen);
                map.SetFeature(cx + 5 + (i * 13) % 38, y, i % 3 == 0 ? pine : oak, i % 5 == 0 ? 1 : 2, ChangeSource.WorldGen);
            }
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(45f);
            Debug.Log("[HousePreview] Savanna/forest comparison: new acacia and palm versus oak and pine; seed 20260927, paused, no save slots.");
        }

        static void SetupTreeGallery(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20260927, Size = MapSizePreset.Small });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            for (int y = cy - 29; y <= cy + 29; y++)
                for (int x = cx - 45; x <= cx + 45; x++)
                {
                    map.SetGround(x, y, soil, ChangeSource.WorldGen);
                    map.SetBiome(x, y, grass, ChangeSource.WorldGen);
                    map.SetFeature(x, y, 0, ChangeSource.WorldGen);
                }
            int tree = 0;
            for (int f = 0; f < host.Content.Features.Count; f++)
            {
                var def = host.Content.Features[f];
                if (def.KindCode != FeatureDef.KindTree) continue;
                int col = tree % 6, row = tree / 6;
                map.SetFeature(cx - 35 + col * 14, cy + 17 - row * 12, def.MapValue, 2, ChangeSource.WorldGen);
                tree++;
            }
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(45f);
            Debug.Log($"[HousePreview] Tree gallery: {tree} content tree kinds; seed 20260927, paused, no save slots.");
        }

        static void SetupArchitectureStates(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20261007, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            using (var batch = map.BeginBatch(ChangeSource.WorldGen))
                for (int y = cy - 43; y <= cy + 43; y++)
                    for (int x = cx - 73; x <= cx + 73; x++)
                    {
                        batch.SetGround(x, y, soil);
                        batch.SetBiome(x, y, grass);
                        batch.SetFeature(x, y, 0);
                        batch.SetFlag(x, y, TileFlags.SnowCover, false);
                        batch.SetFlag(x, y, TileFlags.Burning, false);
                        map.SetRoad(x, y, false);
                    }
            string[] styles = { "style.human", "style.orc", "style.dwarf", "style.elf" };
            string[] types = { "bld.house", "bld.watchtower", "bld.smithy" };
            var civ = host.Sim.Civ;
            for (int styleRow = 0; styleRow < styles.Length; styleRow++)
            {
                int style = host.Content.BuildingStyles.IdOf(styles[styleRow]);
                for (int type = 0; type < types.Length; type++)
                {
                    int def = host.Content.Buildings.IdOf(types[type]);
                    for (int state = 0; state < 3; state++)
                    {
                        int column = type * 3 + state;
                        int b = civ.PlaceBuilding(-1, def, new int2(cx - 49 + column * 12, cy + 21 - styleRow * 14),
                            state == 0 ? .5f : 1f, host.Sim.Clock.Tick);
                        if (b < 0) throw new InvalidOperationException("Architecture fixture could not place " + styles[styleRow] + " " + types[type] + " state " + state);
                        var data = civ.Buildings[b];
                        data.Style = (ushort)style;
                        civ.Buildings[b] = data;
                        if (state == 2) civ.Ruin(b, host.Sim.Clock.Tick);
                    }
                }
            }
            civ.Version++;
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            UnityEngine.Object.FindFirstObjectByType<BuildingRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(35f);
            Debug.Log("[HousePreview] Architecture rows: human, orc, dwarf, elf. Columns: house, watchtower, smithy; each construction, complete, ruin. 36 buildings; paused, no save slots.");
        }

        static void SetupStyleGallery(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20260927, Size = MapSizePreset.Small });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            for (int y = cy - 40; y <= cy + 40; y++)
                for (int x = cx - 66; x <= cx + 66; x++)
                {
                    map.SetGround(x, y, soil, ChangeSource.WorldGen);
                    map.SetBiome(x, y, grass, ChangeSource.WorldGen);
                    map.SetFeature(x, y, 0, ChangeSource.WorldGen);
                }
            var civ = host.Sim.Civ;
            string[] columns = { "house", "hall_1", "tent", "windmill", "well" };
            int styles = host.Content.BuildingStyles.Count;
            for (int s = 0; s < styles; s++)
                for (int c = 0; c < columns.Length; c++)
                {
                    int def = host.Content.Buildings.IdOf("bld." + columns[c]);
                    int b = civ.PlaceBuilding(-1, def, new int2(cx - 43 + c * 22, cy + 25 - s * 10), 1f, 0);
                    var data = civ.Buildings[b];
                    data.Style = (ushort)s;
                    civ.Buildings[b] = data;
                }
            string[] features = { "feat.tree_oak", "feat.tree_pine", "feat.rock" };
            for (int i = 0; i < features.Length; i++)
            {
                ushort value = host.Content.Features[host.Content.Features.IdOf(features[i])].MapValue;
                map.SetFeature(cx - 55 + i * 7, cy - 32, value, 2, ChangeSource.WorldGen);
                map.SetFeature(cx + 42 + i * 7, cy - 32, value, 2, ChangeSource.WorldGen);
            }
            civ.Version++;
            UnityEngine.Object.FindFirstObjectByType<BuildingRenderer>().FlushAll();
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(60f);
            Debug.Log($"[HousePreview] Style gallery: {styles} styles x {columns.Length} building types; seed 20260927, paused, no save slots.");
        }

        internal static void Capture(string path, float ortho, float2? focus = null)
        {
            // Batch editors have no visible Game View to service ScreenCapture requests.
            // Ask the active render pipeline to draw the real game camera to an offscreen target.
            var cam = Camera.main;
            var host = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            // Mouse-anchored zoom otherwise moves the fixture toward the batch editor's (0,0) pointer.
            cam.GetComponent<CameraController>().Focus(focus ?? new float2(host.Sim.World.Width / 2, host.Sim.World.Height / 2));
            cam.orthographicSize = ortho;
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            float aspect = cam.aspect;
            try
            {
                cam.aspect = 1600f / 900f;
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(cam, request)) throw new InvalidOperationException("Render pipeline does not support camera capture.");
                RenderPipeline.SubmitRenderRequest(cam, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                cam.aspect = aspect;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        static void SetupVillage(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20260927, Size = MapSizePreset.Small });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            for (int y = -32; y <= 32; y++)
                for (int x = -48; x <= 48; x++)
                {
                    if (x * x / (48f * 48f) + y * y / (32f * 32f) > 1f) continue;
                    map.SetGround(cx + x, cy + y, soil, ChangeSource.WorldGen);
                    map.SetBiome(cx + x, cy + y, grass, ChangeSource.WorldGen);
                    map.SetFeature(cx + x, cy + y, 0, ChangeSource.WorldGen);
                }
            for (int x = -34; x <= 38; x++) map.SetRoad(cx + x, cy - 3, true);
            for (int y = -19; y <= 10; y++) map.SetRoad(cx, cy + y, true);
            var civ = host.Sim.Civ;
            Place("hall_1", -5, 9, 1f);
            Place("windmill", 21, 11, 1f);
            Place("well", 8, -1, 1f);
            Place("house", -26, 9, 1f);
            Place("house", -29, -14, 1f);
            Place("hut", -13, -14, 1f);
            Place("house", 16, -16, 0.4f);
            int ruin = Place("house", 30, -16, 1f);
            var data = civ.Buildings[ruin]; data.Flags = BuildingFlags.Ruin; civ.Buildings[ruin] = data;
            byte field = (byte)host.Content.Tiles.IdOf("tile.field");
            for (int y = 1; y <= 6; y++)
                for (int x = 18; x <= 31; x++) map.SetGround(cx + x, cy + y, field, ChangeSource.WorldGen);
            string[] sceneFeatures = { "feat.tree_oak", "feat.tree_pine", "feat.rock", "feat.tree_oak" };
            int2[] featureSpots = { new int2(-39, 18), new int2(38, 18), new int2(-39, -22), new int2(38, -22) };
            for (int i = 0; i < sceneFeatures.Length; i++)
                map.SetFeature(cx + featureSpots[i].x, cy + featureSpots[i].y,
                    host.Content.Features[host.Content.Features.IdOf(sceneFeatures[i])].MapValue, 2, ChangeSource.WorldGen);
            var rng = SimRandom.Derive(20260927, 123, 0);
            for (int i = 0; i < 7; i++)
                host.Sim.Units.Store.Spawn(new UnitSpawnRequest { Species = host.Content.Species.IdOf("sp.human"), AgeYears = 20, Pos = new float2(cx - 20 + i * 8, cy - 4), Subspecies = -1, Sex = -1 }, 0, ref rng);
            civ.Version++;
            UnityEngine.Object.FindFirstObjectByType<BuildingRenderer>().FlushAll();
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(80f);
            Debug.Log("[HousePreview] Village fixture: deterministic seed, 8 structures, simulation paused; no save slots written.");

            int Place(string id, int x, int y, float progress) => civ.PlaceBuilding(-1, host.Content.Buildings.IdOf("bld." + id), new int2(cx + x, cy + y), progress, 0);
        }

        static void Setup(GameBootstrap host)
        {
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            for (int y = cy - 18; y <= cy + 22; y++)
                for (int x = cx - 26; x <= cx + 30; x++)
                {
                    map.SetGround(x, y, soil, ChangeSource.WorldGen);
                    map.SetFeature(x, y, 0, ChangeSource.WorldGen);
                }
            var civ = host.Sim.Civ;
            int house = host.Content.Buildings.IdOf("bld.house");
            int hut = host.Content.Buildings.IdOf("bld.hut");
            int a = civ.PlaceBuilding(-1, house, new int2(cx - 12, cy + 2), 1f, 0);
            int b = civ.PlaceBuilding(-1, hut, new int2(cx + 2, cy + 2), 1f, 0);
            civ.PlaceBuilding(-1, house, new int2(cx - 12, cy - 11), 0.4f, 0);
            int ruin = civ.PlaceBuilding(-1, house, new int2(cx + 2, cy - 11), 1f, 0);
            var data = civ.Buildings[ruin]; data.Flags = PG.Sim.BuildingFlags.Ruin; civ.Buildings[ruin] = data;
            civ.Version++;
            UnityEngine.Object.FindFirstObjectByType<BuildingRenderer>().FlushAll();
            map.MarkAllDirty(DirtyMask.All);
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy - 8));
            controller.ZoomTo(80f);
            Debug.Log($"[HousePreview] Placed house {a} and hut {b}; simulation paused, no saves written.");
        }
    }
}
