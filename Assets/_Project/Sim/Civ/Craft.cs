using PG.Content;
using PG.Core;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.7: smiths make what the city lacks (weapons for armed jobs first, then armor pieces),
    // from the best material in store (wood/bone need none), with a quality roll from item_qualities.json.
    public static class Craft
    {
        public const int MaterialCost = 2;

        public static void Make(UnitWorld w, CivWorld civ, int smith, int c, long tick, ref SimRandom rng)
        {
            var db = civ.Content;
            if (db.EquipmentTypes.Count == 0) return;
            int type = PickType(w, civ, c, ref rng);
            var def = db.EquipmentTypes[type];

            int material = -1, materialRes = -1, tier = -1;
            if (def.UsesMaterial)
                for (int r = 0; r < civ.ResCount; r++)
                {
                    int m = civ.MaterialByRes[r];
                    if (m < 0 || civ.Res(c, r) < MaterialCost || db.Materials[m].Tier <= tier) continue;
                    material = m;
                    materialRes = r;
                    tier = db.Materials[m].Tier;
                }
            if (def.UsesMaterial && material < 0)
            {
                // wood and bone items need no stock
                material = db.Materials.IdOrDefault("mat.wood");
                materialRes = -1;
            }
            if (materialRes >= 0) civ.TakeRes(c, materialRes, MaterialCost);

            int quality = RollQuality(db, CraftPoints(w, smith), ref rng);
            int item = civ.CreateItem(type, material, quality, tick, w.Store.Uid[smith], c);
            var meta = civ.Meta;
            string qid = db.ItemQualities.Count > 0 ? db.ItemQualities[quality].Id : "";
            if (meta != null && (qid == "q.unique" || qid == "q.legendary")) // named in the city's tongue and remembered (Bölüm 5.7)
            {
                int lang = civ.City(c).Language;
                int phon = lang >= 0 && meta.Languages.IsAlive(lang) ? meta.Languages.Items[lang].Phonology : meta.SpeciesPhonology[civ.City(c).Species];
                var data = civ.Items[item];
                data.NameId = meta.Names.Add(meta.Word(phon, ref rng), true);
                civ.Items[item] = data;
                meta.Record(HistoryKind.ItemForged, tick, data.Uid, w.Store.Uid[smith], 0, quality);
            }
            w.Events?.Publish(new ItemCraftedEvent(item, (ushort)quality));
        }

        // the smith's craftQuality trait points shift the quality roll (Bölüm 5.7)
        static int CraftPoints(UnitWorld w, int smith)
        {
            var traits = w.Store.Traits[smith];
            var db = w.Content;
            float points = 0f;
            if (!traits.IsEmpty)
                for (int t = 0; t < db.UnitTraits.Count; t++)
                    if (traits.Has(t) && db.UnitTraits[t].Behavior.TryGetValue("craftQuality", out float v)) points += v;
            return math.max(0, (int)math.round(points));
        }

        public const int PickUpRadius = 3, MaxPickUpsPerMonth = 64;

        // Dropped items: inside a city's land they return to its stock, elsewhere a passer-by who can use items takes them.
        public static void PickUp(UnitWorld w, CivWorld civ)
        {
            var items = civ.Items;
            var u = w.Store;
            var map = civ.Map;
            int done = 0;
            for (int it = 0; it < items.Length && done < MaxPickUpsPerMonth; it++)
            {
                var item = items[it];
                if (item.Dead != 0 || item.DroppedTile < 0) continue;
                int x = item.DroppedTile % map.Width, y = item.DroppedTile / map.Width;
                int city = civ.CityAtTile(x, y);
                if (city >= 0 && civ.CityAlive(city))
                {
                    item.City = city;
                    item.DroppedTile = -1;
                    items[it] = item;
                    done++;
                    continue;
                }
                int slot = (int)civ.Content.EquipmentTypes[item.Type].SlotCode;
                float2 p = new float2(x + 0.5f, y + 0.5f);
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (u.State[i] != UnitStore.StateAlive || math.distancesq(u.Pos[i], p) > PickUpRadius * PickUpRadius) continue;
                    if ((w.Abilities(i) & AbilityFlags.CanUseItems) == 0 || u.Equip[i * UnitStore.EquipSlots + slot] >= 0) continue;
                    u.Equip[i * UnitStore.EquipSlots + slot] = it;
                    item.Owner = i;
                    item.City = -1;
                    item.DroppedTile = -1;
                    items[it] = item;
                    w.Events?.Publish(new ItemEquippedEvent(u.Uid[i], item.Uid, item.Quality));
                    u.StatsDirty[i] = 1;
                    done++;
                    break;
                }
            }
        }

        static int PickType(UnitWorld w, CivWorld civ, int c, ref SimRandom rng)
        {
            var db = civ.Content;
            var u = w.Store;
            int armed = 0, weapons = 0;
            foreach (int i in civ.CityResidents[c])
            {
                int j = u.Job[i] - 1;
                if (j != civ.JobWarrior && j != civ.JobGuard) continue;
                armed++;
                if (u.Equip[i * UnitStore.EquipSlots + (int)EquipSlot.Weapon] >= 0) weapons++;
            }
            for (int it = 0; it < civ.Items.Length; it++)
            {
                var item = civ.Items[it];
                if (item.Dead == 0 && item.City == c && db.EquipmentTypes[item.Type].SlotCode == EquipSlot.Weapon) weapons++;
            }
            bool wantWeapon = weapons < armed || armed == 0;
            int n = 0, pick = 0;
            int kingdom = civ.KingdomOf(c);
            for (int t = 0; t < db.EquipmentTypes.Count; t++)
            {
                bool weapon = db.EquipmentTypes[t].SlotCode == EquipSlot.Weapon;
                if (weapon != wantWeapon) continue;
                string id = db.EquipmentTypes[t].Id;
                if (id == "eq.musket" && !civ.HasInvention(kingdom, "inv.gunpowder")) continue;
                if (id == "eq.pickaxe" && !civ.HasInvention(kingdom, "inv.advanced_mining")) continue;
                if (id == "eq.scythe" && !civ.HasInvention(kingdom, "inv.plow")) continue;
                if (rng.Range(0, ++n) == 0) pick = t; // reservoir pick
            }
            return pick;
        }

        // baseChance distribution; each craft point moves one tier of chance upward.
        public static int RollQuality(ContentDB db, int craftPoints, ref SimRandom rng)
        {
            int count = db.ItemQualities.Count;
            if (count == 0) return 0;
            float total = 0f;
            for (int q = 0; q < count; q++) total += db.ItemQualities[q].BaseChance;
            float roll = rng.Value01() * total;
            int result = count - 1;
            for (int q = 0; q < count; q++)
            {
                roll -= db.ItemQualities[q].BaseChance;
                if (roll < 0f) { result = q; break; }
            }
            return math.min(count - 1, result + craftPoints);
        }
    }
}
