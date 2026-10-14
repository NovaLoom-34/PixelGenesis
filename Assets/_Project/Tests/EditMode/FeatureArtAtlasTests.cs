using System.Collections.Generic;
using NUnit.Framework;
using PG.Boot;
using PG.EditorTools;
using PG.Render;

namespace PG.Tests.EditMode
{
    public sealed class FeatureArtAtlasTests
    {
        [OneTimeSetUp]
        public void SyncFeatureArtwork() => PG.EditorTools.DataSync.Sync();

        [Test]
        public void ResourceAtlasSuppliesSmallFullColorSpritesWithoutReplacingExistingTrees()
        {
            var names = new[]
            {
                "cactus", "coral", "reed", "herb_patch", "small_mushroom", "garlic_plant", "wheat_crop",
                "ore_copper", "ore_iron", "ore_silver", "ore_gold", "ore_skyiron", "ore_starore", "ore_obsidian",
                "salt_flat", "bones_pile", "tree_oak",
            };
            var ids = new List<string>(names.Length);
            foreach (string name in names) ids.Add("feat." + name);

            var library = SpriteLibrary.Load("features", ids);
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    Assert.IsTrue(library.TryGet(ids[i], out var entry), names[i]);
                    Assert.IsTrue(entry.FullColor, names[i]);
                    Assert.Greater(entry.Size.x, 0.5f, names[i]);
                    Assert.Greater(entry.Size.y, 0.5f, names[i]);
                    if (i < 16)
                    {
                        Assert.LessOrEqual(entry.Size.x, 1.81f, names[i]);
                        Assert.LessOrEqual(entry.Size.y, 1.81f, names[i]);
                    }
                }
            }
            finally
            {
                library.Dispose();
            }
        }
    }
}
