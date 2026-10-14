using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Sim;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class HistoryImportanceTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            PG.EditorTools.DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void EveryHistoryKindHasBoundedContentImportance()
        {
            Assert.That(_db.HistoryKinds.Count, Is.EqualTo(HistoryKeys.Keys.Length));
            foreach (var item in _db.HistoryKinds.All)
                Assert.That(item.Importance, Is.InRange(0, 100), item.Id);
        }

        [Test]
        public void ImportanceScoreAddsScaleAndClampsDeterministically()
        {
            Assert.That(HistoryImportanceScore.Calculate(50, 0), Is.EqualTo(50));
            Assert.That(HistoryImportanceScore.Calculate(50, 8), Is.EqualTo(54));
            Assert.That(HistoryImportanceScore.Calculate(95, 20), Is.EqualTo(100));
            Assert.That(HistoryImportanceScore.Calculate(-5, -2), Is.EqualTo(0));
        }

        [Test]
        public void WarArmyScaleIncreasesWithSoldiersAndHasABoundedEffect()
        {
            Assert.That(HistoryImportanceScore.ArmyScale(0), Is.EqualTo(0));
            Assert.That(HistoryImportanceScore.ArmyScale(20), Is.GreaterThan(HistoryImportanceScore.ArmyScale(2)));
            Assert.That(HistoryImportanceScore.ArmyScale(1000), Is.EqualTo(20));
            Assert.That(HistoryImportanceScore.CalculateWithScales(50, 6, 7), Is.EqualTo(56));
            Assert.That(HistoryImportanceScore.CalculateWithScales(95, 15, 15), Is.EqualTo(100));
        }

        [Test]
        public void TrackedOrdinaryDeathIsLessImportantThanKingDeath()
        {
            int person = _db.HistoryKinds.IdOf("history.person_died");
            int king = _db.HistoryKinds.IdOf("history.king_died");

            Assert.That(person, Is.GreaterThanOrEqualTo(0));
            Assert.That(king, Is.GreaterThanOrEqualTo(0));
            Assert.That(_db.HistoryKinds[person].Importance, Is.EqualTo(5));
            Assert.That(_db.HistoryKinds[king].Importance, Is.EqualTo(70));
            Assert.That(HistoryRecorder.DeathHistoryKind(false), Is.EqualTo(HistoryKind.PersonDied));
            Assert.That(HistoryRecorder.DeathHistoryKind(true), Is.EqualTo(HistoryKind.KingDied));
        }

        [Test]
        public void NewKingLinksToRecentDeathOfTheSameKingdom()
        {
            using (var meta = new MetaWorld(_db))
            {
                meta.Record(HistoryKind.KingDied, 10, 101, 501);
                meta.Record(HistoryKind.NewKing, 20, 101, 502);
                meta.Record(HistoryKind.NewKing, 30, 202, 503);

                Assert.That(meta.HistoryDetails[1].CauseEventId, Is.EqualTo(meta.HistoryDetails[0].EventId));
                Assert.That(meta.HistoryDetails[2].CauseEventId, Is.Zero);
            }
        }

        [Test]
        public void KingdomFallLinksToRecentCityLoss()
        {
            using (var meta = new MetaWorld(_db))
            {
                meta.Record(HistoryKind.CityCaptured, 10, 501, 101, 202);
                meta.Record(HistoryKind.KingdomFell, 20, 101);

                Assert.That(meta.HistoryDetails[1].CauseEventId, Is.EqualTo(meta.HistoryDetails[0].EventId));
            }
        }

        [Test]
        public void KingdomFallDoesNotLinkToUnrelatedOrOldCityLoss()
        {
            using (var meta = new MetaWorld(_db))
            {
                meta.Record(HistoryKind.CityCaptured, 10, 501, 202, 101);
                meta.Record(HistoryKind.CityRuined, 20, 502, 303);
                meta.Record(HistoryKind.KingdomFell, 20, 101);
                Assert.That(meta.HistoryDetails[2].CauseEventId, Is.Zero);

                meta.Record(HistoryKind.CityRuined, 30, 503, 101);
                meta.Record(HistoryKind.KingdomFell, 30 + 4L * PG.Core.SimConst.TicksPerYear, 101);
                Assert.That(meta.HistoryDetails[4].CauseEventId, Is.Zero);
            }
        }

        [Test]
        public void WarOutcomeCauseAndImportanceSurviveMetaSaveRoundTrip()
        {
            using (var source = new MetaWorld(_db))
            using (var target = new MetaWorld(_db))
            using (var stream = new MemoryStream())
            {
                source.Record(HistoryKind.WarDeclared, 10, 101, 202, 777);
                source.Record(HistoryKind.Peace, 20, 777);
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
                stream.Position = 0;
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) target.Read(reader);

                Assert.That(target.HistoryDetails.Length, Is.EqualTo(2));
                Assert.That(target.HistoryDetails[1].CauseEventId, Is.EqualTo(target.HistoryDetails[0].EventId));
                Assert.That(target.HistoryDetails[0].Importance, Is.EqualTo(_db.HistoryKinds[_db.HistoryKinds.IdOf("history.war_declared")].Importance));
                Assert.That(target.NextHistoryEventId, Is.EqualTo(3));
            }
        }

        [Test]
        public void ResourceShortageWarLinksToRecentFamineForEitherParticipant()
        {
            using (var meta = new MetaWorld(_db))
            {
                meta.Record(HistoryKind.Famine, 10, 501, 101, 0, (int)FamineCause.Drought);
                meta.Record(HistoryKind.Famine, 20, 502, 303, 0, (int)FamineCause.Cold);
                meta.Record(HistoryKind.PopulationMigrated, 25, 501, 503, 101, 4);
                meta.Record(HistoryKind.WarDeclared, 30, 101, 202, 777,
                    unchecked((int)0x40000000) | ((int)WarCause.ResourceShortage << 8) | (int)WarGoal.CityConquest);

                Assert.That(meta.HistoryDetails[2].CauseEventId, Is.EqualTo(meta.HistoryDetails[0].EventId));
                Assert.That(meta.HistoryDetails[3].CauseEventId, Is.EqualTo(meta.HistoryDetails[2].EventId));

                meta.Record(HistoryKind.WarDeclared, 40, 303, 202, 778,
                    unchecked((int)0x40000000) | ((int)WarCause.BorderDispute << 8) | (int)WarGoal.CityConquest);
                Assert.That(meta.HistoryDetails[4].CauseEventId, Is.Zero);
            }
        }

        [Test]
        public void DisasterEvacuationLinksToTheRecentDisasterAtItsSourceCity()
        {
            using (var meta = new MetaWorld(_db))
            {
                meta.Record(HistoryKind.Disaster, 10, 501, 101, 0, 0);
                Assert.That(meta.HasRecentDisasterForCity(501, 20, 10), Is.True);
                Assert.That(meta.HasRecentDisasterForCity(502, 20, 10), Is.False);

                meta.RecordWithMetadata(HistoryKind.PopulationMigrated, 20, 501, 503, 101, 4, 0,
                    HistoryMetadataFlags.DisasterMigration);
                Assert.That(meta.HistoryDetails[1].CauseEventId, Is.EqualTo(meta.HistoryDetails[0].EventId));
                Assert.That(meta.HistoryDetails[1].Flags, Is.EqualTo((ushort)HistoryMetadataFlags.DisasterMigration));
            }
        }

        [Test]
        public void DisasterEvacuationCauseFlagSurvivesMetaSaveRoundTrip()
        {
            using (var source = new MetaWorld(_db))
            using (var target = new MetaWorld(_db))
            using (var stream = new MemoryStream())
            {
                source.Record(HistoryKind.Disaster, 10, 501, 101, 0, 0);
                source.RecordWithMetadata(HistoryKind.PopulationMigrated, 20, 501, 503, 101, 4, 0,
                    HistoryMetadataFlags.DisasterMigration);
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) source.Write(writer);
                stream.Position = 0;
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) target.Read(reader);

                Assert.That(target.HistoryDetails[1].Flags, Is.EqualTo((ushort)HistoryMetadataFlags.DisasterMigration));
                Assert.That(target.HistoryDetails[1].CauseEventId, Is.EqualTo(target.HistoryDetails[0].EventId));
            }
        }
    }
}
