using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Persistence;
using PG.Sim;
using PG.World;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class UndoTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public async Task LastSnapshotRestoresWorldAndIsConsumed()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "pixelgenesis-undo-tests", Guid.NewGuid().ToString("N"));
            var sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(8104));
            var history = new UndoSnapshotHistory(folder);
            GameState restored = null;
            try
            {
                int tile = sim.World.Index(12, 12);
                ulong expectedHash = sim.World.ComputeHash();
                history.Capture(sim);
                Assert.That(history.CanUndo, Is.True);

                sim.World.Ground[tile] = sim.World.Ground[tile] == 0 ? (byte)1 : (byte)0;
                restored = await history.TakeLastAsync(_db);

                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.World.ComputeHash(), Is.EqualTo(expectedHash));
                Assert.That(history.CanUndo, Is.False);
            }
            finally
            {
                restored?.World?.Dispose();
                history.Clear();
                sim.Dispose();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
