using System.IO;
using NUnit.Framework;
using PG.Content;
using PG.UI;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class AchievementExportTests
    {
        ContentDB _db;

        [OneTimeSetUp]
        public void LoadContent()
        {
            PG.EditorTools.DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
            Loc.Init(_db);
        }

        [Test]
        public void MarkdownMarksEarnedAndLockedEntries()
        {
            var profile = new Profile();
            Assert.IsTrue(profile.Earn("ach.first_life"));
            string previousLanguage = Loc.Language;
            Loc.Language = "tr";

            try
            {
                string markdown = WorldShareCode.AchievementMarkdown(_db, profile);
                Assert.That(markdown, Does.Contain("# Başarımlar"));
                Assert.That(markdown, Does.Contain("- [x] İlk Nefes — İlk canlıyı yarat"));
                Assert.That(markdown, Does.Contain("- [ ] Kalabalık — Dünya nüfusu 1.000"));
                Assert.That(markdown, Does.Contain("1 / 52 başarım"));
            }
            finally { Loc.Language = previousLanguage; }
        }
    }
}
