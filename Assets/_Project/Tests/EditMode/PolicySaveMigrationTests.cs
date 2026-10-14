using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Persistence;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;
using EntityId = PG.Core.EntityId;

namespace PG.Tests.EditMode
{
    public sealed class PolicySaveMigrationTests
    {
        ContentDB _db;
        SimWorld _sim;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(2024));
        }

        [TearDown]
        public void Teardown()
        {
            _sim.Dispose();
            _sim = null;
        }

        [Test]
        public void FormatsOneThroughEightLoadWithTheirHistoricalFieldLayouts()
        {
            int species = _db.Species.IdOf("sp.human");
            int city = _sim.Civ.CreateCity(new int2(16, 16), species, -1, 0, _sim.Clock.Tick, 0);
            var rng = _sim.Rng.Get(RngStream.Meta);
            int kingdom = _sim.Meta.CreateKingdom(city, species, EntityId.None, 0, _sim.Clock.Tick, ref rng);
            _sim.Civ.City(city).Kingdom = kingdom;

            for (byte format = 1; format <= 8; format++)
            {
                var warnings = new List<string>();
                PolicySave.Apply(CreateLegacySection(format, kingdom), _sim, warnings);

                Assert.That(warnings, Is.Empty, "Historical policy section format " + format + " should parse cleanly.");
                var policy = _sim.Meta.PolicyOf(kingdom);
                Assert.That(policy.Food, Is.EqualTo(0.1f).Within(0.0001f), "format " + format);
                Assert.That(policy.Housing, Is.EqualTo(0.2f).Within(0.0001f), "format " + format);
                Assert.That(policy.Production, Is.EqualTo(0.3f).Within(0.0001f), "format " + format);
                Assert.That(policy.Defense, Is.EqualTo(0.4f).Within(0.0001f), "format " + format);
                Assert.That(policy.Culture, Is.EqualTo(0.5f).Within(0.0001f), "format " + format);
                Assert.That(policy.Tax, Is.EqualTo(0.5f).Within(0.0001f), "Older formats default tax policy to 50%.");
            }
        }

        static byte[] CreateLegacySection(byte format, int kingdom)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(format);
                writer.Write(1); // policies
                writer.Write(kingdom);
                writer.Write(0.1f); writer.Write(0.2f); writer.Write(0.3f);
                writer.Write(0.4f); writer.Write(0.5f);
                writer.Write(0); // proposals (all supported formats)
                if (format >= 2) writer.Write(0); // treaties
                if (format >= 4) writer.Write(0); // war plans
                if (format >= 6) writer.Write(0); // army logistics
                return stream.ToArray();
            }
        }
    }
}
