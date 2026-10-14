using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using PG.Content;
using PG.Core;
using PG.Powers;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 9.4 budget run: a big map with animals and several civilizations, per-system ms/tick and heap bytes per tick.
    // Batch: Unity -batchmode -nographics -projectPath . -executeMethod PG.EditorTools.PerfBench.Run
    // PG_PERF_SIZE (default Large), PG_PERF_TARGET_UNITS (default 5000), PG_PERF_BUDGET_MS (default 10),
    // PG_PERF_MAX_GC_BYTES_PER_TICK (default 0),
    // PG_PERF_YEARS measured (default 1), PG_PERF_WARMUP years (default 3).
    public static class PerfBench
    {
        public const double DefaultBudgetMs = 10.0;
        public const int DefaultTargetUnits = 5000, MeasureTicks = 600; // 10 months: units still near the target load

        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                var size = Enum.TryParse(Environment.GetEnvironmentVariable("PG_PERF_SIZE"), true, out MapSizePreset s) ? s : MapSizePreset.Large;
                int years = int.TryParse(Environment.GetEnvironmentVariable("PG_PERF_YEARS"), out int y) ? y : 1;
                int warmup = int.TryParse(Environment.GetEnvironmentVariable("PG_PERF_WARMUP"), out int w) ? w : 3;
                int targetUnits = ParsePositiveInt(Environment.GetEnvironmentVariable("PG_PERF_TARGET_UNITS"), DefaultTargetUnits);
                double budgetMs = ParsePositiveDouble(Environment.GetEnvironmentVariable("PG_PERF_BUDGET_MS"), DefaultBudgetMs);
                int maxGcBytesPerTick = ParseNonNegativeInt(Environment.GetEnvironmentVariable("PG_PERF_MAX_GC_BYTES_PER_TICK"), 0);
                LogEnvironment(targetUnits, budgetMs, maxGcBytesPerTick);
                failures += Bench(db, size, warmup, years, targetUnits, budgetMs, maxGcBytesPerTick);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[Perf] ALL OK" : $"[Perf] {failures} over budget");
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        public static int ParsePositiveInt(string value, int fallback) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0 ? parsed : fallback;

        public static double ParsePositiveDouble(string value, double fallback) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
            parsed > 0d && !double.IsNaN(parsed) && !double.IsInfinity(parsed) ? parsed : fallback;

    public static int ParseNonNegativeInt(string value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0 ? parsed : fallback;

    static void LogEnvironment(int targetUnits, double budgetMs, int maxGcBytesPerTick)
        {
            Debug.Log($"[Perf] config benchmark=editor-batch target-units={targetUnits} budget-ms={budgetMs.ToString("0.###", CultureInfo.InvariantCulture)} " +
                      $"max-gc-bytes-per-tick={maxGcBytesPerTick} " +
                      $"unity={Application.unityVersion} platform={Application.platform} os={SystemInfo.operatingSystem} " +
                      $"device={SystemInfo.deviceModel} cpu={SystemInfo.processorType} cpu-threads={SystemInfo.processorCount} " +
                      $"graphics={SystemInfo.graphicsDeviceType} backend={SystemInfo.graphicsDeviceName}");
        }

        static int Bench(ContentDB db, MapSizePreset size, int warmup, int years, int targetUnits, double budgetMs, int maxGcBytesPerTick)
        {
            var settings = new WorldGenSettings { Seed = 2024, Size = size };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Perf"))
            {
                PowerSystems.Install(sim, new PowerCommandQueue(), false);
                sim.SpawnInitialAnimals();
                foreach (var sp in new[] { "sp.human", "sp.orc", "sp.elf", "sp.dwarf", "sp.human", "sp.human" })
                    if (db.Species.TryGet(sp, out var def)) CivSoak.SpawnSettlers(sim, def.Index, 40, (ulong)(sp.GetHashCode() & 0xffff), default, 70f, out _);
                var trend = new System.Text.StringBuilder($"start {sim.Units.Store.Count}");
                for (int yr = 1; yr <= warmup; yr++)
                {
                    for (int i = 0; i < SimConst.TicksPerYear; i++) sim.Tick();
                    trend.Append($", y{yr} {sim.Units.Store.Count}");
                }
                Debug.Log("[Perf] population " + trend);
                // Bölüm 9.4 target load: top the world up to 5,000 units before measuring
                for (int n = 0; n < 60 && sim.Units.Store.Count < targetUnits; n++) sim.SpawnInitialAnimals();
                for (int i = 0; i < 60; i++) sim.Tick();
                int startUnits = sim.Units.Store.Count;

                var pipe = sim.Pipeline;
                var sum = new double[pipe.Count];
                var worst = new double[pipe.Count];
                int ticks = MeasureTicks;
                double total = 0, worstTick = 0;
                long allocatedBytes = 0, worstGcBytesPerTick = 0;
                var sw = Stopwatch.StartNew();
                for (int t = 0; t < ticks; t++)
                {
                    long beforeTick = GC.GetAllocatedBytesForCurrentThread();
                    sim.Tick();
                    long tickAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeTick;
                    allocatedBytes += tickAllocated;
                    if (tickAllocated > worstGcBytesPerTick) worstGcBytesPerTick = tickAllocated;
                    total += pipe.LastTickMs;
                    worstTick = math.max(worstTick, pipe.LastTickMs);
                    for (int i = 0; i < pipe.Count; i++) { sum[i] += pipe.LastMs(i); worst[i] = math.max(worst[i], pipe.LastMs(i)); }
                }
                sw.Stop();
                var order = new int[pipe.Count];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                Array.Sort(order, (a, b) => sum[b].CompareTo(sum[a]));
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < math.min(12, order.Length); k++)
                {
                    int i = order[k];
                    sb.Append($"\n   {pipe[i].GetType().Name,-28} {sum[i] / ticks,7:0.000} ms avg  {worst[i],7:0.0} ms worst");
                }
                int kingdoms = 0;
                for (int k = 0; k < sim.Meta.Kingdoms.Count; k++) if (sim.Meta.Kingdoms.IsAlive(k)) kingdoms++;
                double avg = total / ticks;
                string avgText = avg.ToString("0.000", CultureInfo.InvariantCulture);
                string worstText = worstTick.ToString("0.0", CultureInfo.InvariantCulture);
                string allocationText = (allocatedBytes / (double)ticks).ToString("0.0", CultureInfo.InvariantCulture);
                Debug.Log($"[Perf] {size} {map.Width}x{map.Height}: units {startUnits}->{sim.Units.Store.Count}, cities {sim.Civ.CityCount}, kingdoms {kingdoms} | " +
                          $"{avgText} ms/tick avg, worst tick {worstText} ms, heap {allocationText} B/tick, gc-total={allocatedBytes} B, gc-worst={worstGcBytesPerTick} B/tick{sb}");
                if (avg > budgetMs) { Debug.LogError($"[Perf] {avg.ToString("0.00", CultureInfo.InvariantCulture)} ms/tick is over the {budgetMs.ToString("0.###", CultureInfo.InvariantCulture)} ms budget"); return 1; }
                if (worstGcBytesPerTick > maxGcBytesPerTick) { Debug.LogError($"[Perf] {worstGcBytesPerTick} GC bytes/tick exceeded the {maxGcBytesPerTick} B/tick allocation budget"); return 1; }
                return 0;
            }
        }
    }
}
