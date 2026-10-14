using System;
using System.Diagnostics;
using System.IO;
using PG.Boot;
using PG.Content;
using PG.Core;
using PG.Persistence;
using PG.Powers;
using PG.Render;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 1.16 checks without play mode: every template/size generates, land ratio, determinism, save round trip.
    // Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.DiagnosticsMenu.Run -logFile Logs/diag.log
    public static class DiagnosticsMenu
    {
        const int DiagnosticSlot = 900;

        [MenuItem("PixelGenesis/Run Diagnostics")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += GenerateAll(db);
                if (!Diagnostics.DeterminismCheck(db, new WorldGenSettings { Seed = 42, Size = MapSizePreset.Small }, 1000)) failures++;
                failures += SaveRoundTrip(db);
                failures += SaveFixtures(db);
                failures += LocalizationCheck(db);
                failures += PowerSmoke(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }

            Debug.Log(failures == 0 ? "[Diagnostics] ALL OK" : $"[Diagnostics] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // Renders a generated world (map, features, clouds, era tint) into Logs/snapshot*.png (needs a graphics device).
        [MenuItem("PixelGenesis/Render Snapshot")]
        public static void RenderSnapshot()
        {
            int failures = 0;
            GameObject camGo = null, mapGo = null, featGo = null, natureGo = null, unitGo = null, bldGo = null;
            RenderTexture rt = null;
            SimWorld sim = null;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                var map = WorldGenerator.Generate(new WorldGenSettings { Seed = 2026, Size = MapSizePreset.Medium }, db);
                sim = new SimWorld(db, map, new SimRandomProvider(2026));
                sim.SpawnInitialAnimals();
                var rng = new SimRandom(3, 3);
                for (int k = 0; k < 4; k++)
                    sim.Nature.SpawnCloud(k % 2, new Unity.Mathematics.float2(60 + k * 45, 80 + k * 30), ref rng);
                for (int i = 0; i < 120; i++) sim.Tick();
                var regions = sim.Regions;

                camGo = new GameObject("SnapshotCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = map.Height * 0.5f;
                cam.transform.position = new Vector3(map.Width * 0.5f, map.Height * 0.5f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;

                mapGo = new GameObject("SnapshotMap", typeof(MeshFilter), typeof(MeshRenderer));
                var renderer = mapGo.AddComponent<MapRenderer>();
                renderer.ViewCamera = cam;
                renderer.Bind(map, regions);
                renderer.FlushAll();
                featGo = new GameObject("SnapshotFeatures");
                var features = featGo.AddComponent<FeatureRenderer>();
                features.ViewCamera = cam;
                features.HideAboveOrthoSize = 1e6f;
                features.Bind(map);
                features.FlushAll();
                natureGo = new GameObject("SnapshotNature");
                var nature = natureGo.AddComponent<NatureRenderer>();
                nature.ViewCamera = cam;
                nature.HideAboveOrthoSize = 1e6f;
                nature.Bind(sim.Nature);
                nature.SendMessage("LateUpdate");
                unitGo = new GameObject("SnapshotUnits");
                var units = unitGo.AddComponent<UnitRenderer>();
                units.ViewCamera = cam;
                units.Bind(sim);
                units.Rebuild(cam, true);

                rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                Capture(cam, rt, "Logs/snapshot.png");

                // Zoomed view on the densest land chunk: sprite scale and stages.
                int best = 0, bestCount = -1;
                for (int c = 0; c < map.ChunkCount; c++)
                {
                    int count = 0, x0 = (c % map.ChunksX) * WorldMap.ChunkSize, y0 = (c / map.ChunksX) * WorldMap.ChunkSize;
                    for (int y = y0; y < y0 + WorldMap.ChunkSize; y++)
                        for (int x = x0; x < x0 + WorldMap.ChunkSize; x++)
                            if (map.Feature[map.Index(x, y)] != 0) count++;
                    if (count > bestCount) { bestCount = count; best = c; }
                }
                cam.orthographicSize = 24f;
                cam.transform.position = new Vector3((best % map.ChunksX + 0.5f) * WorldMap.ChunkSize, (best / map.ChunksX + 0.5f) * WorldMap.ChunkSize, -10f);
                Capture(cam, rt, "Logs/snapshot_zoom.png");

                // Units close up: centre on the densest unit cluster.
                var u = sim.Units.Store;
                if (u.Count > 0)
                {
                    var p = u.Pos[u.Alive[u.Count / 2]];
                    cam.orthographicSize = 16f;
                    cam.transform.position = new Vector3(p.x, p.y, -10f);
                    units.Rebuild(cam, false);
                    Capture(cam, rt, "Logs/snapshot_units.png");
                }

                // A village after 40 years (Bölüm 5 render): buildings, fields, roads, borders.
                if (CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), 10, 9))
                {
                    for (int t = 0; t < 40 * SimConst.TicksPerYear; t++) sim.Tick();
                    bldGo = new GameObject("SnapshotBuildings");
                    var buildings = bldGo.AddComponent<BuildingRenderer>();
                    buildings.ViewCamera = cam;
                    buildings.Bind(sim);
                    buildings.FlushAll();
                    renderer.FlushAll();
                    features.FlushAll();
                    int best2 = -1, pop = -1;
                    for (int c = 0; c < sim.Civ.CityCount; c++)
                        if (sim.Civ.CityAlive(c) && sim.Civ.City(c).Population > pop) { pop = sim.Civ.City(c).Population; best2 = c; }
                    if (best2 >= 0)
                    {
                        var center = sim.Civ.City(best2).Center;
                        cam.orthographicSize = 30f;
                        cam.transform.position = new Vector3(center.x, center.y + 6f, -10f);
                        units.Rebuild(cam, false);
                        Capture(cam, rt, "Logs/snapshot_civ.png");
                    }
                }

                cam.orthographicSize = map.Height * 0.5f;
                cam.transform.position = new Vector3(map.Width * 0.5f, map.Height * 0.5f, -10f);
                renderer.ShowRegions = true;
                renderer.FlushAll();
                Capture(cam, rt, "Logs/snapshot_regions.png");
                cam.targetTexture = null;
                renderer.Unbind();
                features.Unbind();
                nature.Unbind();
                Debug.Log($"[Snapshot] Saved Logs/snapshot.png, snapshot_zoom.png, snapshot_regions.png (regions {regions.RegionCount}, islands {regions.IslandCount}, clouds {sim.Nature.Clouds.Length}, units {sim.Units.Store.Count}, cities {sim.Civ.CityCount}, buildings {sim.Civ.Buildings.Length})");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            finally
            {
                if (mapGo != null) UnityEngine.Object.DestroyImmediate(mapGo);
                if (featGo != null) UnityEngine.Object.DestroyImmediate(featGo);
                if (natureGo != null) UnityEngine.Object.DestroyImmediate(natureGo);
                if (unitGo != null) UnityEngine.Object.DestroyImmediate(unitGo);
                if (bldGo != null) UnityEngine.Object.DestroyImmediate(bldGo);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                sim?.Dispose();
            }
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void Capture(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static int GenerateAll(ContentDB db)
        {
            int failures = 0;
            for (int t = 0; t < db.WorldGenTemplates.Count; t++)
            {
                var template = db.WorldGenTemplates[t];
                if (template.Mask.Type == "image") continue; // needs a user PNG
                for (int s = 0; s < MapSizes.Count; s++)
                {
                    var settings = new WorldGenSettings { Seed = 1234 + (ulong)s, Size = (MapSizePreset)s };
                    settings.ApplyTemplateDefaults(template);
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using (var map = WorldGenerator.Generate(settings, db))
                        {
                            double ms = sw.Elapsed.TotalMilliseconds;
                            int water = 0;
                            for (int i = 0; i < map.TileCount; i++)
                                if ((map.Flags[i] & (ushort)TileFlags.Water) != 0) water++;
                            float land = 1f - water / (float)map.TileCount;
                            bool heightBased = template.Mask.Type != "full" && template.Mask.Type != "empty";
                            bool ratioOk = !heightBased || Mathf.Abs(land - settings.LandRatio) <= 0.03f;
                            long workingSetMiB = (long)Math.Ceiling(WorldGenerator.EstimatePeakWorkingSetBytes(settings.Size) / (1024d * 1024d));
                            string line = $"[WorldGen] {template.Id} {settings.Size} {map.Width}x{map.Height}: {ms:0} ms, tile-array estimate ~{workingSetMiB} MiB, land {land:0.000} (target {settings.LandRatio:0.00})";
                            if (ratioOk) Debug.Log(line);
                            else Debug.LogWarning(line + " outside ±0.03");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WorldGen] {template.Id} {settings.Size} failed: {e}");
                        failures++;
                    }
                }
            }
            return failures;
        }

        // First unit / building that differs between two worlds that should be identical.
        static void Divergence(SimWorld a, SimWorld b)
        {
            var ma = a.World;
            var mb = b.World;
            for (int i = 0; i < ma.TileCount; i++)
            {
                string what = ma.Ground[i] != mb.Ground[i] ? "ground" : ma.Biome[i] != mb.Biome[i] ? "biome" : ma.Flags[i] != mb.Flags[i] ? "flags" :
                              ma.Feature[i] != mb.Feature[i] ? "feature" : ma.FeatureState[i] != mb.FeatureState[i] ? "featureState" : ma.Fire[i] != mb.Fire[i] ? "fire" :
                              ma.Building[i] != mb.Building[i] ? "building" : ma.Variant[i] != mb.Variant[i] ? "variant" : null;
                if (what == null) continue;
                Debug.LogError($"[Save] map {what} differs at ({i % ma.Width},{i / ma.Width}): flags {(TileFlags)ma.Flags[i]} vs {(TileFlags)mb.Flags[i]} feature {ma.Feature[i]}/{ma.FeatureState[i]} vs {mb.Feature[i]}/{mb.FeatureState[i]} building {ma.Building[i]} vs {mb.Building[i]} ground {ma.Ground[i]} vs {mb.Ground[i]} fire {ma.Fire[i]} vs {mb.Fire[i]}");
                break;
            }
            for (int z = 0; z < ma.Zones.Length; z++)
                if (!ma.Zones[z].Equals(mb.Zones[z])) { Debug.LogError($"[Save] zone {z} differs: owner {ma.Zones[z].OwnerCity} vs {mb.Zones[z].OwnerCity} temp {ma.Zones[z].TemperatureC} vs {mb.Zones[z].TemperatureC} trees {ma.Zones[z].TreeCount} vs {mb.Zones[z].TreeCount} plants {ma.Zones[z].PlantCount} vs {mb.Zones[z].PlantCount}"); break; }
            var ua = a.Units.Store;
            var ub = b.Units.Store;
            if (ua.Alive.Length != ub.Alive.Length) Debug.LogError($"[Save] alive {ua.Alive.Length} vs {ub.Alive.Length}");
            for (int k = 0; k < Math.Min(ua.Alive.Length, ub.Alive.Length); k++)
            {
                int i = ua.Alive[k], j = ub.Alive[k];
                if (i == j && ua.Pos[i].Equals(ub.Pos[j]) && ua.Hp[i] == ub.Hp[j] && ua.Task[i] == ub.Task[j] && ua.City[i] == ub.City[j] && ua.Job[i] == ub.Job[j]) continue;
                Debug.LogError($"[Save] unit #{i}/{j} {a.Content.Species[ua.Species[i]].Id}: pos {ua.Pos[i]} vs {ub.Pos[j]} hp {ua.Hp[i]} vs {ub.Hp[j]} task {(UnitTask)ua.Task[i]}.{ua.Action[i]} vs {(UnitTask)ub.Task[j]}.{ub.Action[j]} " +
                               $"target {ua.TargetTile[i]} vs {ub.TargetTile[j]} path {ua.PathHandle[i]} vs {ub.PathHandle[j]} city {ua.City[i]} vs {ub.City[j]} job {ua.Job[i]} vs {ub.Job[j]} timer {ua.Timer[i]} vs {ub.Timer[j]} next {ua.NextThink[i]} vs {ub.NextThink[j]}");
                return;
            }
            var ca = a.Civ;
            var cb = b.Civ;
            for (int k = 0; k < Math.Min(ca.Buildings.Length, cb.Buildings.Length); k++)
                if (!ca.Buildings[k].Equals(cb.Buildings[k])) { Debug.LogError($"[Save] building {k} differs: {a.Content.Buildings[ca.Buildings[k].Def].Id} progress {ca.Buildings[k].BuildProgress} vs {cb.Buildings[k].BuildProgress}"); return; }
            for (int k = 0; k < Math.Min(ca.Inventory.Length, cb.Inventory.Length); k++)
                if (ca.Inventory[k] != cb.Inventory[k]) { Debug.LogError($"[Save] inventory slot {k}: {ca.Inventory[k]} vs {cb.Inventory[k]}"); return; }
        }

        // Bölüm 7.8 #1: every non-UI power once on a settled map; no exceptions, none unsupported.
        static int PowerSmoke(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 91, Size = MapSizePreset.Small };
            var map = WorldGenerator.Generate(settings, db);
            int failures = 0, applied = 0;
            var missing = new System.Text.StringBuilder();
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "PowerSmoke"))
            {
                sim.SpawnInitialAnimals();
                var queue = new PowerCommandQueue();
                PowerSystems.Install(sim, queue, false);
                CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), 12, 5, default, 0f, out int2 site);
                for (int i = 0; i < 3 * PG.Core.SimConst.TicksPerYear; i++) sim.Tick();
                for (int p = 0; p < db.Powers.Count; p++)
                {
                    var def = db.Powers[p];
                    if (UiOnly(def)) continue;
                    if (!PowerOps.IsSupported(def, db)) { missing.Append(def.Id).Append(' '); failures++; continue; }
                    try
                    {
                        queue.Enqueue(new PowerCommand { PowerId = (ushort)p, From = site, To = site + new int2(p % 7 - 3, p % 5 - 2), Brush = new BrushSpec(BrushShape.Circle, 3), Tick = sim.Clock.Tick });
                        for (int i = 0; i < 20; i++) sim.Tick();
                        applied++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Powers] {def.Id}: {e}");
                        failures++;
                    }
                }
                for (int i = 0; i < 900; i++) sim.Tick(); // objects finish, water recedes
                failures += Acceptance(sim, queue, site);
                Debug.Log($"[Powers] applied {applied}, units {sim.Units.Store.Count}, objects {sim.Powers.Objects.Length}, drops {sim.Powers.Drops.Length}, temp tiles {sim.Powers.TempTiles.Length}, life {sim.Powers.LifeCells}");
            }
            if (missing.Length > 0) Debug.LogError($"[Powers] unsupported: {missing}");
            if (failures == 0) Debug.Log("[Powers] smoke OK");
            return failures;
        }

        // Bölüm 7.8 #2 (atomic bomb over a city) and #3 (antimatter leaves no land and no units).
        static int Acceptance(SimWorld sim, PowerCommandQueue queue, int2 site)
        {
            var db = sim.Content;
            var map = sim.World;
            var civ = sim.Civ;
            int failures = 0;
            int city = civ.CityAtTile(site.x, site.y);
            for (int c = 0; city < 0 && c < civ.CityCount; c++) if (civ.CityAlive(c)) city = c;
            if (city >= 0 && db.Powers.TryGet("pw.atomic", out var atomic))
            {
                var at = CityPlanner.Below(civ.City(city).Center);
                queue.Enqueue(new PowerCommand { PowerId = (ushort)atomic.Index, From = at, To = at, Brush = new BrushSpec(BrushShape.Circle, 0), Tick = sim.Clock.Tick });
                for (int i = 0; i < 60; i++) sim.Tick();
                int standing = 0, irradiated = 0;
                foreach (int b in civ.CityBuildings[city]) if (civ.Buildings[b].Complete) standing++;
                for (int y = -10; y <= 10; y++)
                    for (int x = -10; x <= 10; x++)
                        if (map.InBounds(at.x + x, at.y + y) && map.HasFlag(at.x + x, at.y + y, TileFlags.Irradiated)) irradiated++;
                bool ok = standing == 0 && irradiated > 0;
                if (!ok) { Debug.LogError($"[Powers] atomic: {standing} buildings standing, {irradiated} irradiated tiles"); failures++; }
                else Debug.Log($"[Powers] atomic OK ({irradiated} irradiated tiles near the center)");
            }
            if (db.Powers.TryGet("pw.antimatter", out var anti))
            {
                var at = new int2(math.clamp(site.x + 60, 60, map.Width - 61), math.clamp(site.y, 60, map.Height - 61));
                queue.Enqueue(new PowerCommand { PowerId = (ushort)anti.Index, From = at, To = at, Brush = new BrushSpec(BrushShape.Circle, 0), Tick = sim.Clock.Tick });
                for (int i = 0; i < 30; i++) sim.Tick();
                int land = 0, units = 0;
                for (int y = -55; y <= 55; y++)
                    for (int x = -55; x <= 55; x++)
                        if (x * x + y * y <= 55 * 55 && map.InBounds(at.x + x, at.y + y) && !map.IsWater(at.x + x, at.y + y)) land++;
                var u = sim.Units.Store;
                for (int k = 0; k < u.Alive.Length; k++) if (math.distance(u.Pos[u.Alive[k]], (float2)at) < 50f && !u.Has(u.Alive[k], UnitFlags.Swim)) units++;
                bool ok = land == 0 && units == 0;
                if (!ok) { Debug.LogError($"[Powers] antimatter: {land} land tiles, {units} walkers left"); failures++; }
                else Debug.Log("[Powers] antimatter OK");
            }
            return failures;
        }

        // Bölüm 8 handles windows, camera, possession, inspection and highlights.
        static bool UiOnly(PowerDef def) =>
            def.Type == "window" || def.Type == "toggle" || def.ParamString("camera") != null || def.ParamString("open") != null ||
            def.ParamString("control") != null || def.ParamNumber("possess", 0f) > 0f;

        static string FixtureFolder => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tests", "SaveFixtures");

        static int SaveFixtures(ContentDB db)
        {
            if (!Directory.Exists(FixtureFolder)) return 0;
            int failures = 0;
            foreach (var file in Directory.GetFiles(FixtureFolder, "*.pxgn"))
            {
                try
                {
                    var state = SaveSystem.LoadFile(file, db);
                    using (var sim = new SimWorld(db, state.World, state.Rng, state.Clock, state.WorldName))
                    {
                        PowerSystems.Install(sim, new PowerCommandQueue(), false);
                        state.ApplyTo(sim);
                        for (int i = 0; i < 120; i++) sim.Tick();
                        Debug.Log($"[Save] fixture {Path.GetFileName(file)} OK (units {sim.Units.Store.Count}, warnings {state.Warnings.Count})");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Save] fixture {Path.GetFileName(file)} failed: {e}");
                    failures++;
                }
            }
            return failures;
        }

        // Bölüm 8.16 #7: switching to English must not show a missing key.
        static int LocalizationCheck(ContentDB db)
        {
            int missing = 0;
            foreach (var s in db.UiStrings.All)
                if (string.IsNullOrEmpty(s.En) || string.IsNullOrEmpty(s.Tr)) { Debug.LogError($"[Loc] {s.Id} lacks a translation"); missing++; }
            Debug.Log(missing == 0 ? $"[Loc] {db.UiStrings.Count} strings, TR and EN complete" : $"[Loc] {missing} incomplete string(s)");
            return missing == 0 ? 0 : 1;
        }

        static int SaveRoundTrip(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 77, Size = MapSizePreset.Medium };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Diagnostics"))
            {
                sim.SpawnInitialAnimals();
                var queue = new PowerCommandQueue();
                PowerSystems.Install(sim, queue, false);
                CivSoak.SpawnSettlers(sim, db.Species.IdOf("sp.human"), 12, 5, default, 0f, out int2 site);
                for (int i = 0; i < 250; i++) sim.Tick();
                // Bölüm 7 state in flight at save time: a tornado, a Langton ant, a life patch and falling meteors
                foreach (var id in new[] { "pw.tornado", "pw.ant", "pw.life_game", "pw.meteor", "pw.black_hole" })
                    if (db.Powers.TryGet(id, out var p))
                        queue.Enqueue(new PowerCommand { PowerId = (ushort)p.Index, From = site + new int2(12, 0), To = site + new int2(12, 0), Brush = new BrushSpec(BrushShape.Circle, 3), Tick = sim.Clock.Tick });
                for (int i = 0; i < 5; i++) sim.Tick();
                ulong hash = sim.World.ComputeHash() ^ Diagnostics.UnitHash(sim);
                long tick = sim.Clock.Tick;
                var rng = sim.Rng.GetStream(0);

                SaveSystem.Save(DiagnosticSlot, sim);
                var state = SaveSystem.Load(DiagnosticSlot, db);
                using (var loaded = new SimWorld(db, state.World, state.Rng, state.Clock, state.WorldName))
                {
                    PowerSystems.Install(loaded, new PowerCommandQueue(), false);
                    state.ApplyTo(loaded);
                    bool ok = (loaded.World.ComputeHash() ^ Diagnostics.UnitHash(loaded)) == hash && loaded.Clock.Tick == tick &&
                              loaded.Rng.GetStream(0).State == rng.State && loaded.Regions.IslandCount == sim.Regions.IslandCount &&
                              state.Warnings.Count == 0;
                    // Continuing both worlds must stay identical (nature lists, era, clouds restored in order).
                    for (int i = 0; i < 600; i++)
                    {
                        sim.Tick();
                        loaded.Tick();
                        bool w = sim.World.ComputeHash() == loaded.World.ComputeHash();
                        bool c = Diagnostics.CivHash(sim) == Diagnostics.CivHash(loaded);
                        bool un = Diagnostics.UnitHash(sim) == Diagnostics.UnitHash(loaded);
                        if (w && c && un) continue;
                        Debug.LogError($"[Save] first divergence {i + 1} ticks after load (tick {sim.Clock.Tick}): map {(w ? "ok" : "DIFF")}, civ {(c ? "ok" : "DIFF")}, units {(un ? "ok" : "DIFF")}");
                        Divergence(sim, loaded);
                        break;
                    }
                    bool same = sim.World.ComputeHash() == loaded.World.ComputeHash() && Diagnostics.UnitHash(sim) == Diagnostics.UnitHash(loaded);
                    if (!same) Debug.LogError("[Save] Worlds diverge after load");
                    ok &= same;
                    long size = new FileInfo(SaveSystem.PathFor(DiagnosticSlot)).Length;
                    // Bölüm 9.2: keep one fixture per format version; every later build must still load it.
                    string fixture = Path.Combine(FixtureFolder, $"format_{SaveFormat.FormatVersion}.pxgn");
                    if (ok && !File.Exists(fixture)) { Directory.CreateDirectory(FixtureFolder); File.Copy(SaveSystem.PathFor(DiagnosticSlot), fixture); Debug.Log("[Save] fixture written: " + fixture); }
                    SaveSystem.Delete(DiagnosticSlot);
                    if (ok) Debug.Log($"[Save] Round trip OK ({size / 1024} KB, {map.Width}x{map.Height}, units {sim.Units.Store.Count}, cities {sim.Civ.CityCount}, buildings {sim.Civ.Buildings.Length})");
                    else Debug.LogError("[Save] Round trip mismatch");
                    return ok ? 0 : 1;
                }
            }
        }
    }
}
