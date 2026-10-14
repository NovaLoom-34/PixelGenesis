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
using EntityId = PG.Core.EntityId;

namespace PG.EditorTools
{
    // Depth-sorting fixture: units stand in front of, beside and behind trees and a house; the screenshots show
    // whether the shared depth buffer hides the ones behind. Run: Tools/run_unity.ps1 -Mode DepthPreview.
    public static class DepthPreview
    {
        const string Active = "PG.DepthPreview.Active", Frame = "PG.DepthPreview.Frame";
        const string Forest = "PG.DepthPreview.Forest", Crowd = "PG.DepthPreview.Crowd";
        const string Prefix = "Logs/depth";

        [MenuItem("PixelGenesis/Preview Depth Sorting")]
        public static void Run() => Start(false);

        // 40 humans against 40 orcs dropped like a spawn power: shows how the groups spread out and fight.
        [MenuItem("PixelGenesis/Preview Crowd Spacing")]
        public static void RunCrowd() => Start(false, true);

        // Generated world: dense forest with people walking among the trees.
        [MenuItem("PixelGenesis/Preview Depth In Forest")]
        public static void RunForest() => Start(true);

        static void Start(bool forest, bool crowd = false)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening the depth preview.");
            DataSync.Sync();
            EditorSceneManager.OpenScene("Assets/Scenes/GameBootstrap.unity");
            SessionState.SetBool(Active, true);
            SessionState.SetInt(Frame, 0);
            SessionState.SetBool(Forest, forest);
            SessionState.SetBool(Crowd, crowd);
            Hook();
            EditorApplication.EnterPlaymode();
        }

        static DepthPreview() { if (SessionState.GetBool(Active, false)) Hook(); }

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
                if (SessionState.GetBool(Crowd, false)) { CrowdFrame(host, frame); return; }
                bool forest = SessionState.GetBool(Forest, false);
                string prefix = forest ? Prefix + "-forest" : Prefix;
                if (frame == 1) { if (forest) SetupForest(host); else Setup(host); }
                if (frame == 60) HousePreview.Capture(Path.GetTempFileName(), 10f, ForestFocus(forest)); // warm-up: the unit mesh needs one render at the capture camera
                if (frame == 80) HousePreview.Capture(prefix + "-wide.png", forest ? 14f : 18f, ForestFocus(forest));
                if (frame == 100) HousePreview.Capture(prefix + "-close.png", forest ? 7f : 5f, ForestFocus(forest));
                if (frame < 120) return;
                SessionState.SetBool(Active, false);
                EditorApplication.update -= Update;
                if (!File.Exists(prefix + "-wide.png") || !File.Exists(prefix + "-close.png")) throw new IOException("Depth screenshots were not created.");
                Debug.Log("[DepthPreview] OK: screenshots " + prefix + "-*.png");
                if (Application.isBatchMode) { EditorApplication.ExitPlaymode(); EditorApplication.Exit(0); }
            }
            catch (Exception e)
            {
                SessionState.SetBool(Active, false);
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Focus(GameBootstrap host, float dx, float zoom)
        {
            var map = host.Sim.World;
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Focus(new float2(map.Width / 2 + dx, map.Height / 2));
            controller.ZoomTo(zoom);
        }

        static void CrowdFrame(GameBootstrap host, int frame)
        {
            var map = host.Sim.World;
            var focus = new float2(map.Width / 2, map.Height / 2);
            if (frame == 1) SetupCrowd(host);
            // Each capture is preceded by a throw-away one: the unit mesh needs a render at the capture camera first.
            foreach (int at in new[] { 60, 100, 140 })
            {
                int ticks = at == 60 ? 0 : at == 100 ? 120 : 200;
                if (frame == at - 5) for (int t = 0; t < ticks; t++) host.Sim.Tick();
                if (frame == at - 2) HousePreview.Capture(Path.GetTempFileName(), 9f, focus);
                if (frame == at) HousePreview.Capture(Prefix + "-crowd-" + (at == 60 ? "0" : at == 100 ? "120" : "320") + ".png", 9f, focus);
            }
            if (frame < 150) return;
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Update;
            Debug.Log("[DepthPreview] OK: crowd screenshots " + Prefix + "-crowd-*.png");
            if (Application.isBatchMode) { EditorApplication.ExitPlaymode(); EditorApplication.Exit(0); }
        }

        static void SetupCrowd(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20261006, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            using (var batch = map.BeginBatch(ChangeSource.WorldGen))
                for (int y = cy - 24; y <= cy + 24; y++)
                    for (int x = cx - 40; x <= cx + 40; x++)
                    {
                        batch.SetGround(x, y, soil);
                        batch.SetBiome(x, y, grass);
                        batch.SetFeature(x, y, 0);
                        batch.SetFlag(x, y, TileFlags.SnowCover, false);
                        batch.SetFlag(x, y, TileFlags.Burning, false);
                        map.SetRoad(x, y, false);
                    }
            var rng = SimRandom.Derive(20261006, 779, 0);
            for (int side = 0; side < 2; side++)
            {
                int species = host.Content.Species.IdOf(side == 0 ? "sp.human" : "sp.orc");
                float life = host.Content.Species[species].BaseStats[(int)StatId.Lifespan];
                SpawnSpread.Begin();
                for (int n = 0; n < 40; n++)
                    host.Sim.Units.Store.Spawn(new UnitSpawnRequest { Species = species, AgeYears = life * .3f, Subspecies = -1, Sex = n % 2,
                        Mother = EntityId.None, Father = EntityId.None,
                        Pos = SpawnSpread.Next(host.Sim.Units, Mobility.Land, new float2(cx - 3f + 6f * side, cy + .5f), 40, 3f, ref rng) }, host.Sim.Clock.Tick, ref rng);
            }
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(cx, cy));
            controller.ZoomTo(9f);
        }

        static float2? ForestFocus(bool forest) =>
            forest ? new float2(SessionState.GetFloat(Forest + "X", 0f), SessionState.GetFloat(Forest + "Y", 0f)) : (float2?)null;

        static void SetupForest(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20260927, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            // Densest 24x24 window of trees.
            int bestX = map.Width / 2, bestY = map.Height / 2, best = -1;
            for (int y = 24; y < map.Height - 48; y += 6)
                for (int x = 24; x < map.Width - 48; x += 6)
                {
                    int n = 0;
                    for (int dy = 0; dy < 24; dy++)
                        for (int dx = 0; dx < 24; dx++)
                            if (map.Feature[(y + dy) * map.Width + x + dx] != 0 && map.IsWalkable(x + dx, y + dy)) n++;
                    if (n > best) { best = n; bestX = x + 12; bestY = y + 12; }
                }
            SessionState.SetFloat(Forest + "X", bestX); SessionState.SetFloat(Forest + "Y", bestY);
            var rng = SimRandom.Derive(20260927, 778, 0);
            int human = host.Content.Species.IdOf("sp.human");
            for (int i = 0; i < 30; i++)
                host.Sim.Units.Store.Spawn(new UnitSpawnRequest { Species = human, AgeYears = 25, Subspecies = -1, Sex = -1,
                    Pos = new float2(bestX - 10 + (i * 7) % 21 + .5f, bestY - 10 + (i * 11) % 21 + .5f),
                    Mother = EntityId.None, Father = EntityId.None }, host.Sim.Clock.Tick, ref rng);
            map.MarkAllDirty(DirtyMask.All);
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            controller.Focus(new float2(bestX, bestY));
            controller.ZoomTo(14f);
            Debug.Log($"[DepthPreview] Forest window at {bestX},{bestY} with {best} trees");
        }

        static void Setup(GameBootstrap host)
        {
            host.StartNewWorld(new WorldGenSettings { Seed = 20261006, Size = MapSizePreset.Small, SpawnAnimals = false });
            host.Sim.Clock.SetSpeed(0);
            var map = host.Sim.World;
            int cx = map.Width / 2, cy = map.Height / 2;
            byte soil = (byte)host.Content.Tiles.IdOf("tile.soil_low");
            byte grass = host.Content.Biomes[host.Content.Biomes.IdOf("bio.grassland")].MapValue;
            using (var batch = map.BeginBatch(ChangeSource.WorldGen))
                for (int y = cy - 20; y <= cy + 20; y++)
                    for (int x = cx - 30; x <= cx + 30; x++)
                    {
                        batch.SetGround(x, y, soil);
                        batch.SetBiome(x, y, grass);
                        batch.SetFeature(x, y, 0);
                        batch.SetFlag(x, y, TileFlags.SnowCover, false);
                        batch.SetFlag(x, y, TileFlags.Burning, false);
                        map.SetRoad(x, y, false);
                    }
            string[] treeIds = { "feat.tree_oak", "feat.tree_pine", "feat.tree_oak" };
            for (int t = 0; t < treeIds.Length; t++)
                map.SetFeature(cx - 8 + t * 5, cy, host.Content.Features[host.Content.Features.IdOf(treeIds[t])].MapValue, 2, ChangeSource.WorldGen);

            var civ = host.Sim.Civ;
            var lowCenters = new float2[3];
            civ.PlaceBuilding(-1, host.Content.Buildings.IdOf("bld.house"), new int2(cx + 4, cy - 3), 1f, 0);
            // Low structures units may stand in or walk through: a unit at the footprint centre must not vanish behind them.
            string[] lowBuildings = { "bld.pasture", "bld.gate", "bld.docks" };
            for (int b = 0; b < lowBuildings.Length; b++)
            {
                var def = host.Content.Buildings[host.Content.Buildings.IdOf(lowBuildings[b])];
                civ.PlaceBuilding(-1, host.Content.Buildings.IdOf(lowBuildings[b]), new int2(cx - 14 + b * 14, cy - 14), 1f, 0);
                lowCenters[b] = new float2(cx - 14 + b * 14 + def.TileW * .5f, cy - 14 + def.TileH * .5f);
            }
            civ.Version++;

            var rng = SimRandom.Derive(20261006, 777, 0);
            int human = host.Content.Species.IdOf("sp.human");
            // Per tree: one unit in front (south), one on the tree's own tile, one behind (north).
            for (int t = 0; t < treeIds.Length; t++)
            {
                float x = cx - 8 + t * 5 + .5f;
                Spawn(x - 1.2f, cy - 1.4f); Spawn(x, cy + .5f); Spawn(x + 1.2f, cy + 1.6f);
            }
            // Around the house: in front of the door line, inside the roof's screen area, and far behind it.
            Spawn(cx + 7.5f, cy - 4.5f); Spawn(cx + 7.5f, cy + 1.5f); Spawn(cx + 7.5f, cy + 4.5f);
            foreach (var center in lowCenters) { Spawn(center.x, center.y); Spawn(center.x + 1.5f, center.y - 1f); }

            map.MarkAllDirty(DirtyMask.All);
            UnityEngine.Object.FindFirstObjectByType<BuildingRenderer>().FlushAll();
            host.Renderer.FlushAll();
            UnityEngine.Object.FindFirstObjectByType<FeatureRenderer>().FlushAll();
            var controller = Camera.main.GetComponent<CameraController>();
            controller.Settings.EdgePan = false;
            Focus(host, -3f, 9f);

            void Spawn(float x, float y) =>
                host.Sim.Units.Store.Spawn(new UnitSpawnRequest { Species = human, AgeYears = 25, Pos = new float2(x, y), Subspecies = -1, Sex = 1,
                    Mother = EntityId.None, Father = EntityId.None }, host.Sim.Clock.Tick, ref rng);
        }
    }
}
