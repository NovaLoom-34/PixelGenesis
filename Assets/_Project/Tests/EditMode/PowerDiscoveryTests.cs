using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.UI;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class PowerDiscoveryTests
    {
        ContentDB _db;

        [SetUp]
        public void SetUp() => _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);

        [Test]
        public void EarnedAchievementRevealsItsPowersOnlyOnce()
        {
            var profile = new Profile();
            profile.Earn("ach.first_mine");

            int firstCount = PowerDiscovery.RevealEarned(profile, _db.Powers, "ach.first_mine", out var firstId);
            int repeatedCount = PowerDiscovery.RevealEarned(profile, _db.Powers, "ach.first_mine", out var repeatedId);

            Assert.That(firstCount, Is.EqualTo(2));
            Assert.That(firstId, Is.EqualTo("pw.ore_skyiron"));
            Assert.That(repeatedCount, Is.Zero);
            Assert.That(repeatedId, Is.Null);
            Assert.That(profile.IsDiscovered("pw.ore_skyiron"), Is.True);
            Assert.That(profile.IsDiscovered("pw.ore_starore"), Is.True);
        }

        [Test]
        public void ProfileReconciliationDoesNotRevealUnearnedPowers()
        {
            var profile = new Profile();

            int count = PowerDiscovery.RevealEarned(profile, _db.Powers, null, out var firstId);

            Assert.That(count, Is.Zero);
            Assert.That(firstId, Is.Null);
            Assert.That(profile.IsDiscovered("pw.ore_skyiron"), Is.False);
        }
    }
}
