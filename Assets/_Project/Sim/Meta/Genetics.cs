using PG.Content;
using PG.Core;
using Unity.Collections;

namespace PG.Sim
{
    // Bölüm 4.4: one genome per subspecies; slot = gene index + 1, 0 = empty.
    public struct GeneGenome
    {
        public byte ChromosomeCount, SlotsPerChromosome;
        public FixedList128Bytes<ushort> Slots;

        public int Length => ChromosomeCount * SlotsPerChromosome;
        public int Gene(int slot) => slot < Slots.Length ? Slots[slot] - 1 : -1;

        public bool Has(int gene)
        {
            for (int k = 0; k < Slots.Length; k++) if (Slots[k] == gene + 1) return true;
            return false;
        }
    }

    public static class Genetics
    {
        public const float FillChance = 0.5f; // new root genome: each slot holds a random gene half the time (DECISIONS #71)

        public static GeneGenome Random(ContentDB db, ref SimRandom rng)
        {
            var r = db.GeneRules;
            var g = new GeneGenome
            {
                ChromosomeCount = (byte)System.Math.Clamp(r.ChromosomeCount, 1, r.ChromosomeMax),
                SlotsPerChromosome = (byte)System.Math.Clamp(r.SlotsPerChromosome, 1, r.SlotsMax),
            };
            int n = System.Math.Min(g.Length, g.Slots.Capacity);
            for (int k = 0; k < n; k++)
                g.Slots.Add(db.Genes.Count > 0 && rng.Chance(FillChance) ? (ushort)(rng.Range(0, db.Genes.Count) + 1) : (ushort)0);
            return g;
        }

        // Gene effects plus synergies whose genes sit side by side on one chromosome (any order); add[] goes into stats, pct accumulates.
        public static void ApplyMods(ContentDB db, in GeneGenome g, NativeArray<float> stats, int o, ref FixedList512Bytes<float> pct)
        {
            for (int k = 0; k < g.Slots.Length; k++)
            {
                int gene = g.Slots[k] - 1;
                if (gene < 0 || gene >= db.Genes.Count) continue;
                foreach (var m in db.Genes[gene].Mods) { stats[o + (int)m.Stat] += m.Add; pct[(int)m.Stat] += m.Pct; }
            }
            for (int s = 0; s < db.GeneSynergies.Count; s++)
            {
                var syn = db.GeneSynergies[s];
                if (!HasSynergy(g, syn.RequiredGenes)) continue;
                foreach (var m in syn.Mods) { stats[o + (int)m.Stat] += m.Add; pct[(int)m.Stat] += m.Pct; }
            }
        }

        public static bool HasSynergy(in GeneGenome g, int[] required)
        {
            int n = required.Length;
            if (n == 0 || g.SlotsPerChromosome < n) return false;
            for (int c = 0; c < g.ChromosomeCount; c++)
                for (int start = 0; start + n <= g.SlotsPerChromosome; start++)
                {
                    bool all = true;
                    for (int q = 0; q < n && all; q++)
                    {
                        bool found = false;
                        for (int k = 0; k < n && !found; k++)
                        {
                            int slot = c * g.SlotsPerChromosome + start + k;
                            found = slot < g.Slots.Length && g.Slots[slot] == required[q] + 1;
                        }
                        all = found;
                    }
                    if (all) return true;
                }
            return false;
        }

        // One mutation operation (gene_rules.json mutationOps).
        public static void Mutate(ContentDB db, ref GeneGenome g, ref SimRandom rng)
        {
            var ops = db.GeneRules.MutationOps;
            if (g.Slots.Length == 0 || ops.Length == 0) return;
            int a = rng.Range(0, g.Slots.Length);
            int b = rng.Range(0, g.Slots.Length);
            switch (ops[rng.Range(0, ops.Length)])
            {
                case "swap_gene": (g.Slots[a], g.Slots[b]) = (g.Slots[b], g.Slots[a]); break;
                case "replace_random": if (db.Genes.Count > 0) g.Slots[a] = (ushort)(rng.Range(0, db.Genes.Count) + 1); break;
                case "duplicate":
                    int next = a % g.SlotsPerChromosome + 1 < g.SlotsPerChromosome ? a + 1 : a - 1;
                    if (next >= 0 && next < g.Slots.Length) g.Slots[next] = g.Slots[a];
                    break;
                case "delete_to_empty": g.Slots[a] = 0; break;
            }
        }

        // Bölüm 4.5: biomeBias phenotypes weigh x4.
        public static short PickPhenotype(ContentDB db, int biome, ref SimRandom rng)
        {
            int count = db.Phenotypes.Count;
            if (count == 0) return -1;
            int total = 0;
            for (int p = 0; p < count; p++) total += Weight(db.Phenotypes[p], biome);
            int roll = rng.Range(0, total);
            for (int p = 0; p < count; p++)
            {
                roll -= Weight(db.Phenotypes[p], biome);
                if (roll < 0) return (short)p;
            }
            return (short)(count - 1);
        }

        static int Weight(PhenotypeDef def, int biome)
        {
            if (biome >= 0) foreach (int b in def.BiasBiomes) if (b == biome) return 4;
            return 1;
        }
    }
}
