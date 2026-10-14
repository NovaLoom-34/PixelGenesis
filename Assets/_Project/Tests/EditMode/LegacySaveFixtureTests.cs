using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.EditorTools;
using PG.Persistence;
using PG.Sim;
using UnityEngine;
using DataSync = PG.EditorTools.DataSync;

namespace PG.Tests.EditMode
{
    public sealed class LegacySaveFixtureTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [Test]
        public void FormatOneFixtureLoadsIntoTheCurrentWorldState()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fixture = Path.Combine(projectRoot, "Tests", "SaveFixtures", "format_1.pxgn");
            Assert.That(File.Exists(fixture), Is.True, "The checked-in legacy save fixture must be available to EditMode tests.");

            var state = SaveSystem.LoadFile(fixture, _db);
            using (var sim = new SimWorld(_db, state.World, state.Rng, state.Clock, state.WorldName))
            {
                Assert.That(state.WorldName, Is.EqualTo("Diagnostics"));
                Assert.That(state.World.Width, Is.EqualTo(256));
                Assert.That(state.World.Height, Is.EqualTo(256));
                Assert.That(state.Clock.Year, Is.EqualTo(0));
                state.ApplyTo(sim);
                Assert.That(sim.Population, Is.EqualTo(115));
                Assert.That(sim.ScenarioId, Is.Empty, "Legacy saves without the optional scenario section remain sandbox worlds.");
                Assert.That(sim.ScenarioOutcome, Is.EqualTo(ScenarioStatus.Sandbox));
            }
        }
    }
}
