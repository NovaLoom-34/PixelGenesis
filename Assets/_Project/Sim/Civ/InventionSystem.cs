using System.Collections.Generic;
using PG.Content;
using PG.Core;
using Unity.Mathematics;

namespace PG.Sim
{
    public readonly struct InventionDiscoveredEvent : ISimEvent
    {
        public readonly int Kingdom;
        public readonly int Invention;

        public InventionDiscoveredEvent(int kingdom, int invention)
        {
            Kingdom = kingdom;
            Invention = invention;
        }
    }

    // Bölüm 5.12: Inventions & Technologies.
    // Monthly: each kingdom generates research points from libraries, city halls, population and printing presses.
    // When points reach the invention's cost, it unlocks bonuses, vehicle types, new tools and craft recipes.
    public sealed class InventionSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.Meta;
        public int Order => 15;

        public void Tick(in SimContext ctx)
        {
            if (!ctx.Clock.IsMonthStart) return;
            var civ = ctx.Civ;
            var meta = ctx.Meta;
            var db = ctx.Content;
            if (civ == null || meta == null || db.Inventions.Count == 0) return;

            long tick = ctx.Clock.Tick;
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                UpdateKingdomResearch(ctx, civ, meta, k, tick);
            }
        }

        static void UpdateKingdomResearch(in SimContext ctx, CivWorld civ, MetaWorld meta, int k, long tick)
        {
            var db = ctx.Content;
            ref var kd = ref meta.Kingdom(k);

            // 1. Pick an invention to research if none is currently selected
            if (!civ.CurrentResearch.TryGetValue(k, out int current) || current < 0 || current >= db.Inventions.Count || civ.HasInvention(k, current))
            {
                current = PickNextResearch(civ, db, k);
                civ.CurrentResearch[k] = current;
                if (current < 0) return; // all available inventions unlocked!
            }

            var inv = db.Inventions[current];

            // 2. Calculate research points produced this month
            int points = 1 + kd.Cities;
            int population = 0, scholars = 0;
            for (int c = 0; c < civ.CityCount; c++)
            {
                if (!civ.CityAlive(c) || civ.KingdomOf(c) != k) continue;
                points += civ.City(c).HallTier * 2;
                population += civ.City(c).Population;
                if (civ.BldLibrary >= 0)
                    points += civ.CountBuildings(c, civ.BldLibrary, true) * 6;
                if (civ.JobScholar >= 0)
                    foreach (int resident in civ.CityResidents[c])
                        if (ctx.Units.Store.Job[resident] - 1 == civ.JobScholar) scholars++;
            }

            // Larger populations provide a broad knowledge base; trained scholars add
            // focused output on top of the library building's research capacity.
            points += population / 20 + scholars * 2;

            if (civ.HasInvention(k, "inv.printing_press")) points = (int)(points * 1.5f);
            if (kd.Culture >= 0 && meta.Cultures.IsAlive(kd.Culture))
            {
                if (meta.CultureHas(kd.Culture, meta.CulScholars)) points = (int)(points * 1.3f);
            }

            civ.ResearchProgress.TryGetValue(k, out int progress);
            progress += math.max(1, points);

            // 3. Completion check
            if (progress >= inv.Cost)
            {
                civ.UnlockInvention(k, inv.Index);
                progress -= inv.Cost;
                ctx.Events.Publish(new InventionDiscoveredEvent(k, inv.Index));
                meta.Record(HistoryKind.InventionDiscovered, tick, meta.Kingdoms.Headers[k].Uid, 0, 0, inv.Index);

                // Pick next research immediately
                int next = PickNextResearch(civ, db, k);
                civ.CurrentResearch[k] = next;
            }
            civ.ResearchProgress[k] = progress;
        }

        public static int PickNextResearch(CivWorld civ, ContentDB db, int k)
        {
            for (int i = 0; i < db.Inventions.Count; i++)
            {
                if (civ.HasInvention(k, i)) continue;
                var def = db.Inventions[i];
                bool reqsMet = true;
                if (def.ReqIndices != null)
                {
                    for (int r = 0; r < def.ReqIndices.Length; r++)
                    {
                        if (!civ.HasInvention(k, def.ReqIndices[r]))
                        {
                            reqsMet = false;
                            break;
                        }
                    }
                }
                if (reqsMet) return i;
            }
            return -1;
        }
    }
}
