using PG.Content;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 3.2: final = (base + sum add) * (1 + sum pct / 100), then age stage and level multipliers and floors.
    public static class UnitStats
    {
        public const int MaxLevel = 30;

        public static AgeStage StageFor(SpeciesDef sp, float ageYears)
        {
            float lifespan = sp.BaseStats[(int)StatId.Lifespan];
            if (lifespan <= 0f) return AgeStage.Adult;
            float t = ageYears / lifespan;
            return t < 0.08f ? AgeStage.Baby : t < 0.18f ? AgeStage.Child : t <= 0.8f ? AgeStage.Adult : AgeStage.Elder;
        }

        // Bölüm 6.7-6.10: members carry their culture, religion and clan trait mods.
        static uint MetaMods<T>(MetaStore<T> store, int index, Registry<MetaTraitDef> defs, NativeArray<float> stats, int o, ref FixedList512Bytes<float> pct) where T : unmanaged
        {
            if (index < 0 || index >= store.Count) return 0;
            var traits = store.Headers[index].Traits;
            if (traits.IsEmpty) return 0;
            uint flags = 0;
            for (int t = 0; t < defs.Count; t++)
            {
                if (!traits.Has(t)) continue;
                var def = defs[t];
                flags |= (uint)def.FlagBits;
                foreach (var m in def.Mods)
                {
                    stats[o + (int)m.Stat] += m.Add;
                    pct[(int)m.Stat] += m.Pct;
                }
            }
            return flags;
        }

        // Innate civil stats 0-4 from the uid (DECISIONS #67): stable across saves, no extra storage or rng draw.
        static void Innate(long uid, NativeArray<float> stats, int o)
        {
            uint h = math.hash(new int2((int)uid, (int)(uid >> 32)));
            stats[o + (int)StatId.Intel] += h % 5u;
            stats[o + (int)StatId.Diplo] += (h >> 3) % 5u;
            stats[o + (int)StatId.Warfare] += (h >> 6) % 5u;
            stats[o + (int)StatId.Steward] += (h >> 9) % 5u;
        }

        public static void Recompute(UnitStore u, int i)
        {
            var content = u.Content;
            var sp = content.Species[u.Species[i]];
            int o = i * UnitStore.StatCount;
            var stats = u.Stats;

            // add[] in the output slots, pct accumulated in a stack buffer
            var pct = new FixedList512Bytes<float>();
            // a species without stats of its own (zombie beast) wears its former body at x0.8, speed x0.7 (Bölüm 4.9)
            int former = u.FormerSpecies[i] - 1;
            bool borrowed = sp.BaseStats[(int)StatId.Hp] <= 0f && former >= 0;
            var baseStats = borrowed ? content.Species[former].BaseStats : sp.BaseStats;
            for (int s = 0; s < UnitStore.StatCount; s++)
            {
                stats[o + s] = borrowed ? baseStats[s] * (s == (int)StatId.Speed ? 0.7f : s == (int)StatId.Lifespan ? 0f : 0.8f) : baseStats[s];
                pct.Add(0f);
            }

            uint flags = (uint)sp.Flags;
            ulong neurons = NeuronKeys.Core;
            var traits = u.Traits[i];
            if (!traits.IsEmpty)
                for (int t = 0; t < content.UnitTraits.Count; t++)
                {
                    if (!traits.Has(t)) continue;
                    var def = content.UnitTraits[t];
                    flags |= (uint)def.Flags;
                    neurons |= def.NeuronBits;
                    foreach (var m in def.Mods)
                    {
                        stats[o + (int)m.Stat] += m.Add;
                        pct[(int)m.Stat] += m.Pct;
                    }
                }
            Innate(u.Uid[i], stats, o);
            var meta = u.SubspeciesMeta;
            int sub = u.Subspecies[i];
            if (meta != null && sub >= 0 && sub < meta.Count)
            {
                Genetics.ApplyMods(content, meta.Data(sub).Genome, stats, o, ref pct);
                var sst = meta.Traits(sub);
                for (int t = 0; t < content.SubspeciesTraits.Count; t++)
                {
                    if (!sst.Has(t)) continue;
                    var def = content.SubspeciesTraits[t];
                    flags |= (uint)def.Flags;
                    neurons |= def.NeuronBits;
                    foreach (var m in def.Mods)
                    {
                        stats[o + (int)m.Stat] += m.Add;
                        pct[(int)m.Stat] += m.Pct;
                    }
                }
            }
            var civ = u.Civ;
            if (civ != null)
                for (int s = 0; s < UnitStore.EquipSlots; s++)
                {
                    int item = u.Equip[i * UnitStore.EquipSlots + s];
                    if (item < 0 || item >= civ.Items.Length) continue;
                    var data = civ.Items[item];
                    var eq = content.EquipmentTypes[data.Type];
                    float mul = content.ItemQualities[data.Quality].Multiplier * (eq.UsesMaterial ? content.Materials[data.Material].Multiplier : 1f);
                    foreach (var m in eq.Mods)
                    {
                        stats[o + (int)m.Stat] += m.Add * mul;
                        pct[(int)m.Stat] += m.Pct * mul;
                    }
                }
            var world = u.Meta;
            if (world != null)
            {
                flags |= MetaMods(world.Cultures, u.Culture[i], content.CultureTraits, stats, o, ref pct);
                flags |= MetaMods(world.Religions, u.Religion[i], content.ReligionTraits, stats, o, ref pct);
                flags |= MetaMods(world.Clans, u.Clan[i], content.ClanTraits, stats, o, ref pct);
            }
            int read = u.BooksRead[i];
            for (int b = 0; read != 0 && b < content.BookTypes.Count; b++)
                if ((read & (1 << b)) != 0)
                    foreach (var m in content.BookTypes[b].Mods) { stats[o + (int)m.Stat] += m.Add; pct[(int)m.Stat] += m.Pct; }
            u.BaseFlags[i] = flags;
            if (meta != null && sub >= 0 && sub < meta.Count) neurons &= ~meta.Data(sub).DisabledNeurons;
            u.NeuronMask[i] = neurons;

            for (int s = 0; s < UnitStore.StatusSlots; s++)
            {
                int id = u.StatusId[i * UnitStore.StatusSlots + s] - 1;
                if (id < 0) continue;
                var def = content.StatusEffects[id];
                flags |= (uint)def.Flags;
                foreach (var m in def.Mods)
                {
                    stats[o + (int)m.Stat] += m.Add;
                    pct[(int)m.Stat] += m.Pct;
                }
            }
            u.Flags[i] = flags | (u.Flags[i] & (uint)UnitFlags.Aboard);

            for (int s = 0; s < UnitStore.StatCount; s++) stats[o + s] *= 1f + pct[s] / 100f;

            switch ((AgeStage)u.Age[i])
            {
                case AgeStage.Baby: Mul(stats, o, StatId.Dmg, 0.3f); Mul(stats, o, StatId.Hp, 0.3f); Mul(stats, o, StatId.Speed, 0.6f); break;
                case AgeStage.Child: Mul(stats, o, StatId.Dmg, 0.6f); Mul(stats, o, StatId.Hp, 0.6f); break;
                case AgeStage.Elder: Mul(stats, o, StatId.Dmg, 0.8f); Mul(stats, o, StatId.Speed, 0.8f); stats[o + (int)StatId.Intel] += 1f; break;
            }

            int level = math.min((int)u.Level[i], MaxLevel) - 1;
            if (level > 0)
            {
                Mul(stats, o, StatId.Hp, 1f + 0.03f * level);
                Mul(stats, o, StatId.Dmg, 1f + 0.02f * level);
            }

            stats[o + (int)StatId.Hp] = math.max(1f, stats[o + (int)StatId.Hp]);
            stats[o + (int)StatId.Armor] = math.max(0f, stats[o + (int)StatId.Armor]);
            stats[o + (int)StatId.Size] = math.max(1f, stats[o + (int)StatId.Size]);
            stats[o + (int)StatId.AtkSpd] = math.max(0.1f, stats[o + (int)StatId.AtkSpd]);
            if (sp.BaseStats[(int)StatId.Speed] > 0f) stats[o + (int)StatId.Speed] = math.max(0.05f, stats[o + (int)StatId.Speed]);
            u.StatsDirty[i] = 0;
        }

        static void Mul(NativeArray<float> stats, int o, StatId s, float f) => stats[o + (int)s] *= f;
    }
}
