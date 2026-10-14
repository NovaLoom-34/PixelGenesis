using System;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;
using PG.Content;
using PG.EditorTools;
using PG.Sim;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class HistoryMetadataMigrationTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void HistorySidecarVersionOneLoadsDetailsWithoutLaterBiographyAndSummarySections()
        {
            using (var source = new MetaWorld(_db))
            {
                source.Record(HistoryKind.KingdomFounded, 12, 77);
                byte[] legacy = Write(source);
                int magicOffset = FindHistorySidecar(legacy);
                Assert.That(magicOffset, Is.GreaterThanOrEqualTo(0));

                Buffer.BlockCopy(BitConverter.GetBytes(1), 0, legacy, magicOffset + sizeof(int), sizeof(int));
                int detailsCount = BitConverter.ToInt32(legacy, magicOffset + sizeof(int) * 4);
                int detailsEnd = magicOffset + sizeof(int) * 2 + sizeof(long) +
                    sizeof(int) + detailsCount * Marshal.SizeOf(typeof(HistoryMetadata));
                Array.Resize(ref legacy, detailsEnd); // v1 ends after history details

                using (var stream = new MemoryStream(legacy))
                using (var reader = new BinaryReader(stream))
                using (var restored = new MetaWorld(_db))
                {
                    restored.Read(reader);
                    Assert.That(restored.History.Length, Is.EqualTo(1));
                    Assert.That(restored.HistoryDetails.Length, Is.EqualTo(1));
                    Assert.That(restored.HistoryDetails[0].EventId, Is.EqualTo(1));
                    Assert.That(restored.BiographyTracks.Length, Is.Zero);
                    Assert.That(restored.WorldSummaries.Length, Is.Zero);
                }
            }
        }

        static byte[] Write(MetaWorld meta)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) meta.Write(writer);
                return stream.ToArray();
            }
        }

        static int FindHistorySidecar(byte[] bytes)
        {
            for (int i = 0; i <= bytes.Length - sizeof(int); i++)
                if (BitConverter.ToInt32(bytes, i) == 0x48535431) return i;
            return -1;
        }
    }
}
