using NUnit.Framework;
using PG.EditorTools;
using PG.Content;
using PG.Render;
using PG.World;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace PG.Tests.EditMode
{
    public sealed class WorldFeatureArtTests
    {
        [Test]
        public void FinalTreeIsCutIntoBoundedWorldPieces()
        {
            DataSync.Sync();
            var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
            var map = new WorldMap(64, 64, db);
            var go = new GameObject("FeaturePieceTest");
            try
            {
                int x = 20, y = 20;
                map.SetGround(x, y, (byte)db.Tiles.IdOf("tile.soil_low"), ChangeSource.WorldGen);
                map.SetFeature(x, y, db.Features[db.Features.IdOf("feat.tree_oak")].MapValue, 2, ChangeSource.WorldGen);
                Assert.That(map.Feature[map.Index(x, y)], Is.GreaterThan(0));
                var renderer = go.AddComponent<FeatureRenderer>();
                renderer.Bind(map); renderer.FlushAll();
                var filters = go.GetComponentsInChildren<MeshFilter>();
                int pieces = 0;
                foreach (var filter in filters) pieces += filter.sharedMesh.vertexCount / 4;
                Assert.That(pieces, Is.GreaterThan(1), "A tree should not remain one image pasted over the map.");
            }
            finally { Object.DestroyImmediate(go); map.Dispose(); }
        }

        [Test]
        public void EveryTreeKindAndRockHaveReadableBoundedSpritesWithNonTreeFallback()
        {
            DataSync.Sync();
            var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
            var ids = new List<string>();
            for (int i = 0; i < db.Features.Count; i++)
                if (db.Features[i].KindCode == FeatureDef.KindTree) ids.Add(db.Features[i].Id);
            Assert.That(ids.Count, Is.EqualTo(24), "Keep the tree art audit in sync with content.");
            ids.Add("feat.rock");
            ids.Add("feat.cactus");
            var lib = SpriteLibrary.Load("features", ids);
            try
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    Assert.That(lib.TryGet(ids[i], out var entry), Is.True);
                    Assert.That(entry.FullColor, Is.True, ids[i]); // rock has _color art; cactus is cut from the feature resource atlas
                    Assert.That(entry.Uv.width, Is.GreaterThan(0), ids[i]);
                    if (i < ids.Count - 1)
                    {
                        Assert.That(Mathf.Max(entry.Size.x, entry.Size.y), Is.GreaterThan(ids[i] == "feat.rock" ? 1.5f : 2f), ids[i]);
                        Assert.That(Mathf.Max(entry.Size.x, entry.Size.y), Is.LessThanOrEqualTo(3f), ids[i]);
                    }
                    else Assert.That(Mathf.Max(entry.Size.x, entry.Size.y), Is.LessThanOrEqualTo(3f), "Non-tree feature art stays small.");
                }
                Assert.That(lib.Atlas.filterMode, Is.EqualTo(FilterMode.Point));
            }
            finally { lib.Dispose(); }
        }
    }
}
