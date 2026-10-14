using System.Collections.Generic;
using System.IO;
using System.Text;
using PG.Content;
using PG.Sim;

namespace PG.Persistence
{
    // Section 11: cities, buildings, inventories, items (raw lists) with id maps; applied after the units (section 10).
    static class CivSave
    {
        public static byte[] Write(SimWorld sim)
        {
            var db = sim.Content;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                Keys(w, db.Buildings);
                Keys(w, db.BuildingStyles);
                Keys(w, db.Resources);
                Keys(w, db.Jobs);
                Keys(w, db.EquipmentTypes);
                Keys(w, db.Materials);
                Keys(w, db.ItemQualities);
                Keys(w, db.HappinessEvents);
                sim.Civ.Write(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            var civ = sim.Civ;
            var u = sim.Units.Store;
            if (raw != null)
            {
                var db = sim.Content;
                using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
                {
                    var buildings = Remap(r, db.Buildings, warnings);
                    var styles = Remap(r, db.BuildingStyles, warnings);
                    var resources = Remap(r, db.Resources, warnings);
                    var jobs = Remap(r, db.Jobs, warnings);
                    var equipment = Remap(r, db.EquipmentTypes, warnings);
                    var materials = Remap(r, db.Materials, warnings);
                    var qualities = Remap(r, db.ItemQualities, warnings);
                    var happiness = Remap(r, db.HappinessEvents, warnings);
                    civ.Read(r);

                    for (int c = 0; c < civ.CityCount; c++)
                    {
                        ref var city = ref civ.City(c);
                        city.Style = (ushort)Map(styles, city.Style, 0);
                    }
                    var ruins = civ.BldRuins;
                    for (int b = 0; b < civ.Buildings.Length; b++)
                    {
                        var d = civ.Buildings[b];
                        int def = Map(buildings, d.Def, -1);
                        if (def < 0 && d.Alive) { d.Def = (ushort)System.Math.Max(0, ruins); d.Flags |= BuildingFlags.Ruin; }
                        else d.Def = (ushort)System.Math.Max(0, def);
                        d.Style = (ushort)Map(styles, d.Style, 0);
                        civ.Buildings[b] = d;
                    }

                    // inventory columns follow the current resource list
                    int oldCount = resources.Length;
                    var old = civ.Inventory.ToArray(Unity.Collections.Allocator.Temp);
                    civ.Inventory.Resize(civ.CityCount * civ.ResCount, Unity.Collections.NativeArrayOptions.ClearMemory);
                    for (int k = 0; k < civ.Inventory.Length; k++) civ.Inventory[k] = 0;
                    for (int c = 0; c < civ.CityCount; c++)
                        for (int res = 0; res < oldCount; res++)
                        {
                            int to = resources[res];
                            int from = c * oldCount + res;
                            if (to >= 0 && from < old.Length) civ.Inventory[c * civ.ResCount + to] += old[from];
                        }
                    old.Dispose();

                    for (int it = 0; it < civ.Items.Length; it++)
                    {
                        var item = civ.Items[it];
                        int type = Map(equipment, item.Type, -1);
                        if (type < 0) item.Dead = 1;
                        item.Type = (ushort)System.Math.Max(0, type);
                        item.Material = (ushort)Map(materials, item.Material, 0);
                        item.Quality = (ushort)Map(qualities, item.Quality, 0);
                        civ.Items[it] = item;
                    }

                    for (int i = 0; i < u.HighWater; i++)
                    {
                        if (u.State[i] == UnitStore.StateFree) continue;
                        u.Job[i] = (byte)(Map(jobs, u.Job[i] - 1, -1) + 1);
                        for (int s = 0; s < UnitStore.HapSlots; s++)
                        {
                            int k = i * UnitStore.HapSlots + s;
                            u.HapEvent[k] = (ushort)(Map(happiness, u.HapEvent[k] - 1, -1) + 1);
                        }
                        for (int s = 0; s < UnitStore.EquipSlots; s++)
                        {
                            int k = i * UnitStore.EquipSlots + s;
                            if (u.Equip[k] >= civ.Items.Length || (u.Equip[k] >= 0 && civ.Items[u.Equip[k]].Dead != 0)) u.Equip[k] = -1;
                        }
                        u.StatsDirty[i] = 1;
                    }
                }
            }
            civ.RebuildCaches(u);
            for (int i = 0; i < u.HighWater; i++)
                if (u.State[i] != UnitStore.StateFree) UnitStats.Recompute(u, i); // equipment is derived
        }

        internal static int Map(int[] map, int value, int fallback) => value >= 0 && value < map.Length && map[value] >= 0 ? map[value] : fallback;

        internal static void Keys<T>(BinaryWriter w, Registry<T> registry) where T : ContentDef
        {
            w.Write(registry.Count);
            for (int i = 0; i < registry.Count; i++) w.Write(registry[i].Id);
        }

        internal static int[] Remap<T>(BinaryReader r, Registry<T> registry, List<string> warnings) where T : ContentDef
        {
            var map = new int[r.ReadInt32()];
            for (int i = 0; i < map.Length; i++)
            {
                string key = r.ReadString();
                map[i] = registry.IdOrDefault(key);
                if (map[i] < 0) warnings.Add($"{registry.Category} '{key}' -> removed");
            }
            return map;
        }
    }

    // Section 12: names, kingdoms, wars, alliances, armies, opinions (Bölüm 6); applied after the cities.
    static class MetaSave
    {
        static void Traits<T>(MetaStore<T> store, int[] map) where T : unmanaged
        {
            for (int k = 0; k < store.Count; k++)
            {
                ref var h = ref store.Header(k);
                var old = h.Traits;
                var mapped = default(TraitSet);
                for (int t = 0; t < map.Length; t++) if (map[t] >= 0 && old.Has(t)) mapped.Set(map[t], true);
                h.Traits = mapped;
            }
        }

        public static byte[] Write(SimWorld sim)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                CivSave.Keys(w, sim.Content.KingdomTraits);
                CivSave.Keys(w, sim.Content.WarTypes);
                CivSave.Keys(w, sim.Content.CultureTraits);
                CivSave.Keys(w, sim.Content.LanguageTraits);
                CivSave.Keys(w, sim.Content.ReligionTraits);
                CivSave.Keys(w, sim.Content.ClanTraits);
                CivSave.Keys(w, sim.Content.Plots);
                CivSave.Keys(w, sim.Content.BookTypes);
                sim.Meta.Write(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            if (raw == null) return; // older saves: KingdomSystem founds kingdoms again
            var meta = sim.Meta;
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
            {
                var traits = CivSave.Remap(r, sim.Content.KingdomTraits, warnings);
                var wars = CivSave.Remap(r, sim.Content.WarTypes, warnings);
                var db = sim.Content;
                var cultures = CivSave.Remap(r, db.CultureTraits, warnings);
                var languages = CivSave.Remap(r, db.LanguageTraits, warnings);
                var religions = CivSave.Remap(r, db.ReligionTraits, warnings);
                var clans = CivSave.Remap(r, db.ClanTraits, warnings);
                var plots = CivSave.Remap(r, db.Plots, warnings);
                var books = CivSave.Remap(r, db.BookTypes, warnings);
                meta.Read(r);
                Traits(meta.Cultures, cultures);
                Traits(meta.Languages, languages);
                Traits(meta.Religions, religions);
                Traits(meta.Clans, clans);
                for (int k = 0; k < meta.Religions.Count; k++) meta.Religions.Data(k).DeityTrait = CivSave.Map(religions, meta.Religions.Items[k].DeityTrait, -1);
                for (int k = 0; k < meta.Plots.Count; k++)
                {
                    int def = CivSave.Map(plots, meta.Plots.Items[k].Def, -1);
                    if (def < 0 && meta.Plots.IsAlive(k)) meta.Plots.MarkDead(k, meta.Plots.Headers[k].FoundedTick);
                    meta.Plots.Data(k).Def = (ushort)System.Math.Max(0, def);
                }
                for (int b = 0; b < meta.Books.Length; b++)
                {
                    var book = meta.Books[b];
                    int type = CivSave.Map(books, book.Type, -1);
                    if (type < 0) book.Dead = 1;
                    book.Type = (ushort)System.Math.Max(0, type);
                    meta.Books[b] = book;
                }
                var u = sim.Units.Store;
                for (int i = 0; i < u.HighWater; i++)
                {
                    int read = u.BooksRead[i], mapped = 0;
                    for (int t = 0; t < books.Length && read != 0; t++) if ((read & (1 << t)) != 0 && books[t] >= 0 && books[t] < 16) mapped |= 1 << books[t];
                    u.BooksRead[i] = (ushort)mapped;
                    if (u.State[i] != UnitStore.StateFree) UnitStats.Recompute(u, i); // culture, religion, clan and book mods
                }
                for (int k = 0; k < meta.Kingdoms.Count; k++)
                {
                    ref var d = ref meta.Kingdom(k);
                    d.Trait = CivSave.Map(traits, d.Trait, -1);
                }
                for (int w = 0; w < meta.Wars.Count; w++)
                {
                    ref var d = ref meta.Wars.Data(w);
                    d.Type = (ushort)CivSave.Map(wars, d.Type, meta.WarConquest);
                }
            }
        }
    }

    // Section 13: drops, world objects, receding water, automata, temperature offsets, blight (Bölüm 7).
    static class PowerSave
    {
        public static byte[] Write(SimWorld sim)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                CivSave.Keys(w, sim.Content.Powers);
                sim.Powers.Write(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            if (raw == null) return;
            var pw = sim.Powers;
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
            {
                var powers = CivSave.Remap(r, sim.Content.Powers, warnings);
                pw.Read(r);
                for (int k = pw.Drops.Length - 1; k >= 0; k--)
                {
                    var d = pw.Drops[k];
                    int p = CivSave.Map(powers, d.Power, -1);
                    if (p < 0) { pw.Drops.RemoveAtSwapBack(k); continue; }
                    d.Power = (ushort)p;
                    pw.Drops[k] = d;
                }
                for (int k = 0; k < pw.Objects.Length; k++)
                {
                    var o = pw.Objects[k];
                    o.Power = (ushort)System.Math.Max(0, CivSave.Map(powers, o.Power, 0));
                    pw.Objects[k] = o;
                }
            }
        }
    }
}
