using System;
using System.Diagnostics;
using System.IO;
using PG.Content;
using PG.Core;
using PG.Render;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Fixed camera/seed visual workload. Measures CPU mesh rebuild and managed allocation;
    // actual frame/GPU targets still require WebGL and Android device profiling.
    public static class VisualBenchmark
    {
        const int Warmup = 30;
        const int Samples = 180;

        [MenuItem("PixelGenesis/Visual Benchmark 1000/5000")]
        public static void Run()
        {
            GameObject cameraObject = null, unitsObject = null;
            SimWorld sim = null;
            int exit = 1;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                var settings = new WorldGenSettings { Seed = 6077, Size = MapSizePreset.Huge };
                var map = WorldGenerator.Generate(settings, db);
                sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed));

                cameraObject = new GameObject("VisualBenchmarkCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 36f;
                camera.aspect = 16f / 9f;
                camera.transform.position = new Vector3(map.Width * .5f, map.Height * .5f, -10f);

                unitsObject = new GameObject("VisualBenchmarkUnits");
                var renderer = unitsObject.AddComponent<UnitRenderer>();
                renderer.ViewCamera = camera;
                renderer.Bind(sim);
                var rng = new SimRandom(6077, 23);
                int[] species = {
                    db.Species.IdOf("sp.human"), db.Species.IdOf("sp.orc"),
                    db.Species.IdOf("sp.dwarf"), db.Species.IdOf("sp.elf"),
                    db.Species.IdOf("sp.sheep"), db.Species.IdOf("sp.wolf")
                };
                Directory.CreateDirectory("Logs");
                string file = Path.Combine("Logs", "visual-benchmark.csv");
                using (var writer = new StreamWriter(file, false))
                {
                    writer.WriteLine("seed,camera_x,camera_y,ortho,units,drawn,avg_ms,max_ms,managed_bytes_per_rebuild");
                    for (int stage = 0; stage < 2; stage++)
                    {
                        int target = stage == 0 ? 1000 : 5000;
                        for (int i = sim.Units.Store.Count; i < target; i++)
                        {
                            float x = map.Width * .5f + rng.Range(-45, 45) + .5f;
                            float y = map.Height * .5f + rng.Range(-26, 26) + .5f;
                            sim.Units.Store.Spawn(new UnitSpawnRequest {
                                Species = species[i % species.Length], Pos = new float2(x, y),
                                AgeYears = 20, Sex = i % 2, Subspecies = -1,
                                Mother = PG.Core.EntityId.None, Father = PG.Core.EntityId.None
                            }, sim.Clock.Tick, ref rng);
                        }
                        for (int i = 0; i < Warmup; i++) renderer.Rebuild(camera, false);
                        GC.Collect();
                        long allocated = GC.GetAllocatedBytesForCurrentThread();
                        var watch = new Stopwatch();
                        double total = 0, max = 0;
                        for (int i = 0; i < Samples; i++)
                        {
                            watch.Restart();
                            renderer.Rebuild(camera, false);
                            watch.Stop();
                            double ms = watch.Elapsed.TotalMilliseconds;
                            total += ms;
                            max = Math.Max(max, ms);
                        }
                        long bytesPerRebuild = (GC.GetAllocatedBytesForCurrentThread() - allocated) / Samples;
                        writer.WriteLine($"6077,{map.Width * .5f},{map.Height * .5f},36,{target},{renderer.LastDrawn},{total / Samples:0.000},{max:0.000},{bytesPerRebuild}");
                        Debug.Log($"[VisualBenchmark] {target} units, {renderer.LastDrawn} drawn: {total / Samples:0.000} ms avg, {max:0.000} ms max, {bytesPerRebuild} managed B/rebuild");
                    }
                }
                Debug.Log("[VisualBenchmark] Wrote " + file);
                exit = 0;
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                if (unitsObject != null) UnityEngine.Object.DestroyImmediate(unitsObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                sim?.Dispose();
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }
    }
}
