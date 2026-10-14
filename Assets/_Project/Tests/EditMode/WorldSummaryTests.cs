using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Sim;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class WorldSummaryTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            PG.EditorTools.DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void CenturyAndEraSummariesPersistInChronologicalOrder()
        {
            using var source = new MetaWorld(_db);
            source.RecordWorldSummary(new WorldSummary
            {
                Kind = WorldSummaryKind.Century, StartYear = 0, EndYear = 100,
                Population = 120, Cities = 4, Kingdoms = 2, Wars = 1,
                LargestKingdomUid = 7, LargestKingdomPopulation = 80,
                DeadliestWarUid = 9, DeadliestWarCasualties = 24,
                FamousPersonUid = 11, FamousScore = 45, CulturesFounded = 2, CulturesEnded = 1,
            }, new WorldSummaryPopulationSplit { Civilization = 75, Wildlife = 45 });
            source.RecordWorldSummary(new WorldSummary
            {
                Kind = WorldSummaryKind.EraTransition, StartYear = 100, EndYear = 130,
                EraFrom = 1, EraTo = 4, Population = 150,
            });

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
            stream.Position = 0;
            using var restored = new MetaWorld(_db);
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) restored.Read(reader);

            Assert.That(restored.WorldSummaries.Length, Is.EqualTo(2));
            Assert.That(restored.WorldSummaries[0].DeadliestWarCasualties, Is.EqualTo(24));
            Assert.That(restored.WorldSummaries[0].FamousPersonUid, Is.EqualTo(11));
            Assert.That(restored.WorldSummaryPopulationSplits[0].Civilization, Is.EqualTo(75));
            Assert.That(restored.WorldSummaryPopulationSplits[0].Wildlife, Is.EqualTo(45));
            Assert.That(restored.WorldSummaryPopulationSplits[1].Civilization, Is.EqualTo(-1));
            Assert.That(restored.WorldSummaries[1].EraTo, Is.EqualTo(4));
        }

        [Test]
        public void VersionThreeSummariesLoadWithUnknownPopulationSplit()
        {
            using var source = new MetaWorld(_db);
            source.RecordWorldSummary(new WorldSummary { Kind = WorldSummaryKind.Century, StartYear = 0, EndYear = 100, Population = 120 },
                new WorldSummaryPopulationSplit { Civilization = 75, Wildlife = 45 });
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
            byte[] bytes = stream.ToArray();
            int magicOffset = FindHistoryMagic(bytes);
            Assert.That(magicOffset, Is.GreaterThanOrEqualTo(0));
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(3), 0, bytes, magicOffset + sizeof(int), sizeof(int));
            System.Array.Resize(ref bytes, bytes.Length - sizeof(int) - System.Runtime.InteropServices.Marshal.SizeOf<WorldSummaryPopulationSplit>());
            using var legacy = new MemoryStream(bytes);
            using var restored = new MetaWorld(_db);
            using (var reader = new BinaryReader(legacy, System.Text.Encoding.UTF8, true)) restored.Read(reader);

            Assert.That(restored.WorldSummaries.Length, Is.EqualTo(1));
            Assert.That(restored.WorldSummaries[0].Population, Is.EqualTo(120));
            Assert.That(restored.WorldSummaryPopulationSplits.Length, Is.EqualTo(1));
            Assert.That(restored.WorldSummaryPopulationSplits[0].Civilization, Is.EqualTo(-1));
            Assert.That(restored.WorldSummaryPopulationSplits[0].Wildlife, Is.EqualTo(-1));
        }

        [Test]
        public void GraphSeedsCivilizationAndWildlifeSeriesFromSavedSummary()
        {
            using var meta = new MetaWorld(_db);
            meta.RecordWorldSummary(new WorldSummary { Kind = WorldSummaryKind.Century, StartYear = 0, EndYear = 100, Population = 120 },
                new WorldSummaryPopulationSplit { Civilization = 75, Wildlife = 45 });
            var recorder = new PG.UI.StatsRecorder();
            recorder.SeedWorldSummaries(meta);

            Assert.That(recorder.Series["civ_population"][0], Is.EqualTo(75));
            Assert.That(recorder.Series["wildlife_population"][0], Is.EqualTo(45));
        }

        [Test]
        public void GraphDoesNotInventZeroSplitForLegacySummary()
        {
            using var meta = new MetaWorld(_db);
            meta.RecordWorldSummary(new WorldSummary { Kind = WorldSummaryKind.Century, StartYear = 0, EndYear = 100, Population = 120 });
            var recorder = new PG.UI.StatsRecorder();
            recorder.SeedWorldSummaries(meta);

            Assert.That(float.IsNaN(recorder.Series["civ_population"][0]), Is.True);
            Assert.That(float.IsNaN(recorder.Series["wildlife_population"][0]), Is.True);
        }

        [Test]
        public void DuplicatePeriodSummaryIsIgnored()
        {
            using var meta = new MetaWorld(_db);
            var report = new WorldSummary { Kind = WorldSummaryKind.Century, StartYear = 100, EndYear = 200, Population = 90 };
            meta.RecordWorldSummary(report);
            meta.RecordWorldSummary(report);

            Assert.That(meta.WorldSummaries.Length, Is.EqualTo(1));
        }

        [Test]
        public void BiographySidecarVersionTwoLoadsWithoutWorldSummaries()
        {
            using var source = new MetaWorld(_db);
            source.TrackBiography(77, 25, 2, -1, 0, 0);
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
            byte[] bytes = stream.ToArray();
            int magicOffset = FindHistoryMagic(bytes);
            Assert.That(magicOffset, Is.GreaterThanOrEqualTo(0));
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(2), 0, bytes, magicOffset + sizeof(int), sizeof(int));
            stream.SetLength(0);
            stream.Write(bytes, 0, bytes.Length - sizeof(int));
            stream.Position = 0;

            using var restored = new MetaWorld(_db);
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) restored.Read(reader);

            Assert.That(restored.BiographyTracks.Length, Is.EqualTo(1));
            Assert.That(restored.WorldSummaries.Length, Is.EqualTo(0));
        }

        static int FindHistoryMagic(byte[] bytes)
        {
            for (int i = 0; i <= bytes.Length - sizeof(int); i++)
                if (System.BitConverter.ToInt32(bytes, i) == 0x48535431) return i;
            return -1;
        }
    }
}
