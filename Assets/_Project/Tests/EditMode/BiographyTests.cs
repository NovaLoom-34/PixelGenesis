using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Sim;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class BiographyTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            PG.EditorTools.DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void BiographyKeepsOnlyTheNewestSixEventsInChronologicalOrder()
        {
            using var meta = new MetaWorld(_db);
            meta.TrackBiography(42, 10, 0, -1, 0, 0);
            for (int i = 0; i < 8; i++)
                meta.AddBiographyEvent(42, 20 + i, BiographyEventKind.Milestone, 0, i);

            Assert.That(meta.TryGetBiography(42, out int index), Is.True);
            Assert.That(meta.BiographyTracks[index].EventCount, Is.EqualTo(6));
            Assert.That(meta.GetBiographyEvent(42, 0).Value, Is.EqualTo(2));
            Assert.That(meta.GetBiographyEvent(42, 5).Value, Is.EqualTo(7));
        }

        [Test]
        public void DeathSummaryAndBiographyRingSurviveMetaSaveRoundTrip()
        {
            using var source = new MetaWorld(_db);
            source.TrackBiography(99, 100, 3, 7, 11, 12);
            source.AddBiographyEvent(99, 140, BiographyEventKind.Office, 500, 2);
            source.SetBiographyDeath(99, 900, 101, DeathCause.Killed, 102, 11, 12, 3, 8, 34, 7, 100);

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
            stream.Position = 0;
            using var restored = new MetaWorld(_db);
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) restored.Read(reader);

            Assert.That(restored.TryGetBiography(99, out int index), Is.True);
            var track = restored.BiographyTracks[index];
            Assert.That(track.DeathTick, Is.EqualTo(900));
            Assert.That(track.KillerUid, Is.EqualTo(101));
            Assert.That(track.KillsAtDeath, Is.EqualTo(34));
            Assert.That(restored.GetBiographyEvent(99, 1).Kind, Is.EqualTo(BiographyEventKind.Death));
        }

        [Test]
        public void OldHistorySidecarVersionLoadsWithAnEmptyBiographyStore()
        {
            using var source = new MetaWorld(_db);
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
            byte[] bytes = stream.ToArray();
            // v4 sidecar tail for an empty world: magic, version, next id (8), then five list counts (20) = 36 bytes.
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(1), 0, bytes, bytes.Length - 32, sizeof(int));
            stream.SetLength(0);
            stream.Write(bytes, 0, bytes.Length - sizeof(int) * 3);
            stream.Position = 0;

            using var restored = new MetaWorld(_db);
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) restored.Read(reader);

            Assert.That(restored.BiographyTracks.Length, Is.EqualTo(0));
            Assert.That(restored.BiographyEvents.Length, Is.EqualTo(0));
        }
    }
}
