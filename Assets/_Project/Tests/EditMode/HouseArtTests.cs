using NUnit.Framework;
using PG.EditorTools;
using PG.Render;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class HouseArtTests
    {
        [Test] public void SixCulturesLoadAllFortyOneBuildingTypesAndThreeStates()
        {
            DataSync.Sync();
            var ids = new System.Collections.Generic.List<string>();
            foreach (string race in new[] { "human", "orc", "dwarf", "elf", "beast", "insect" })
                foreach (string building in new[] { "house", "watchtower", "smithy", "hall_1", "granary", "market", "hall_2", "hall_3", "storage", "farm_shed", "mine", "lumber_camp", "bakery", "pasture", "beehive", "bonfire", "tent", "hut", "well", "windmill", "inn", "manor", "stable", "barracks", "fishing_hut", "docks", "shipyard", "library", "school", "temple", "grand_temple", "healers_house", "graveyard", "statue_god", "monument", "pyre", "wall", "gate", "nest", "hive", "ruins" })
                    foreach (string state in new[] { "construction", "complete", "ruin" })
                        ids.Add("b." + race + "_" + building + "_" + state);
            var lib = SpriteLibrary.Load("buildings", ids);
            try
            {
                foreach (string id in ids)
                {
                    Assert.IsTrue(lib.TryGet(id, out var entry), id);
                    Assert.IsTrue(entry.FullColor, id + " fell back to placeholder");
                    Assert.LessOrEqual(entry.Size.x * SpriteLibrary.PixelsPerTile, 64.01f);
                    Assert.LessOrEqual(entry.Size.y * SpriteLibrary.PixelsPerTile, 64.01f);
                    int x0 = Mathf.RoundToInt(entry.Uv.x * lib.Atlas.width);
                    int y0 = Mathf.RoundToInt(entry.Uv.y * lib.Atlas.height);
                    int w = Mathf.RoundToInt(entry.Uv.width * lib.Atlas.width);
                    int h = Mathf.RoundToInt(entry.Uv.height * lib.Atlas.height);
                    int solid = 0, empty = 0;
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                        if (lib.Atlas.GetPixel(x0 + x, y0 + y).a > .5f) solid++; else empty++;
                    Assert.Greater(solid, 100, id);
                    Assert.Greater(empty, 20, id);
                }
            }
            finally { lib.Dispose(); }
        }

        [Test]
        public void VillageArtLoadsColorWithoutReplacingOtherStyles()
        {
            DataSync.Sync();
            var ids = new[] { "b.human_house_complete", "b.human_hut_complete", "b.human_house_construction", "b.human_house_ruin", "b.human_hut_construction", "b.human_hut_ruin", "b.human_hall_1_complete", "b.human_well_complete", "b.human_windmill_complete", "b.beast_house_complete", "b.human_hall_1_construction" };
            var lib = SpriteLibrary.Load("buildings", ids);
            try
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.That(lib.TryGet(ids[i], out var entry), Is.True);
                    // Every listed id is final color now: hall_1 construction is cut from the human settlement sheet (row 0).
                    Assert.That(entry.FullColor, Is.True, ids[i]);
                    Assert.That(entry.Size.x * SpriteLibrary.PixelsPerTile, Is.LessThanOrEqualTo(64));
                    Assert.That(entry.Uv.width, Is.GreaterThan(0));
                }
                Assert.That(lib.Atlas.filterMode, Is.EqualTo(FilterMode.Point));
                lib.TryGet(ids[0], out var house);
                int red = 0, blue = 0, transparent = 0;
                int x0 = Mathf.RoundToInt(house.Uv.x * lib.Atlas.width);
                int y0 = Mathf.RoundToInt(house.Uv.y * lib.Atlas.height);
                int w = Mathf.RoundToInt(house.Uv.width * lib.Atlas.width);
                int h = Mathf.RoundToInt(house.Uv.height * lib.Atlas.height);
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++)
                    {
                        Color c = lib.Atlas.GetPixel(x, y);
                        if (c.a < 0.5f) { transparent++; continue; }
                        if (c.r > c.g * 1.4f && c.r > c.b * 1.4f) red++;
                        if (c.b > c.r * 1.4f) blue++;
                    }
                Assert.That(red, Is.GreaterThan(100), "Roof must retain its terracotta color.");
                Assert.That(blue, Is.GreaterThan(5), "Windows must survive atlas sampling.");
                Assert.That(transparent, Is.GreaterThan(100), "The sprite must not have an opaque background.");
            }
            finally { lib.Dispose(); }
        }
    }
}
