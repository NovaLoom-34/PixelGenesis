using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class ChronicleTests
    {
        ContentDB _db;
        SimWorld _sim;
        string _previousLanguage;

        [OneTimeSetUp]
        public void LoadContent()
        {
            PG.EditorTools.DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _previousLanguage = PG.UI.Loc.Language;
            PG.UI.Loc.Init(_db);
            PG.UI.Loc.Language = "tr";
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(9207));
        }

        [TearDown]
        public void Teardown()
        {
            _sim.Dispose();
            _sim = null;
            PG.UI.Loc.Language = _previousLanguage;
        }

        [Test]
        public void EveryHistoryEventHasBilingualNarrativeVariants()
        {
            Assert.That(_db.ChronicleTemplates.Count, Is.EqualTo(HistoryKeys.Keys.Length));
            foreach (var key in HistoryKeys.Keys)
            {
                Assert.That(_db.ChronicleTemplates.TryGet(key, out var item), Is.True, key);
                Assert.That(item.Tr.Length, Is.GreaterThan(0), key);
                Assert.That(item.En.Length, Is.GreaterThan(0), key);
            }
        }

        [Test]
        public void SameEventIsStableCachedAndTranslatedByUiLanguage()
        {
            var entry = new HistoryEntry
            {
                Tick = 0, Template = (ushort)HistoryKind.WarDeclared, A = 101, B = 202, C = 303, Extra = 0x40000000,
            };

            string turkish = PG.UI.HistoryChronicle.Describe(_sim, entry);
            string repeated = PG.UI.HistoryChronicle.Describe(_sim, entry);
            PG.UI.Loc.Language = "en";
            string english = PG.UI.HistoryChronicle.Describe(_sim, entry);

            Assert.That(repeated, Is.EqualTo(turkish));
            Assert.That(turkish, Does.Contain("savaş"));
            Assert.That(english, Does.Contain("war").IgnoreCase);
            Assert.That(english, Is.Not.EqualTo(turkish));
        }

        [Test]
        public void DisasterEvacuationChronicleNamesItsCause()
        {
            _sim.Meta.Record(HistoryKind.Disaster, 10, 501, 101, 0, 0);
            _sim.Meta.RecordWithMetadata(HistoryKind.PopulationMigrated, 20, 501, 503, 101, 4, 0,
                HistoryMetadataFlags.DisasterMigration);
            int index = _sim.Meta.History.Length - 1;

            string text = PG.UI.HistoryChronicle.Describe(_sim, _sim.Meta.History[index], index);

            Assert.That(text, Does.Contain("afet sonrasında"));
        }

        [Test]
        public void KingdomFallChronicleShowsItsRecordedCityLossCause()
        {
            _sim.Meta.Record(HistoryKind.CityCaptured, 10, 501, 101, 202);
            _sim.Meta.Record(HistoryKind.KingdomFell, 20, 101);
            int fallIndex = _sim.Meta.History.Length - 1;

            string text = PG.UI.HistoryChronicle.Describe(_sim, _sim.Meta.History[fallIndex], fallIndex);

            Assert.That(text, Does.Contain("Nedeni:"));
            Assert.That(text, Does.Contain("şehrini"));
        }

        [Test]
        public void KingdomFallCauseLookupReturnsTheRecordedCityEvent()
        {
            _sim.Meta.Record(HistoryKind.CityCaptured, 10, 501, 101, 202);
            _sim.Meta.Record(HistoryKind.KingdomFell, 20, 101);
            int fallIndex = _sim.Meta.History.Length - 1;

            Assert.That(PG.UI.HistoryChronicle.TryGetCauseIndex(_sim.Meta, fallIndex, out int causeIndex), Is.True);
            Assert.That(causeIndex, Is.EqualTo(0));
            Assert.That((HistoryKind)_sim.Meta.History[causeIndex].Template, Is.EqualTo(HistoryKind.CityCaptured));
        }
    }
}
