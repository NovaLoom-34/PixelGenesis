using System.IO;
using System.Reflection;
using NUnit.Framework;
using PG.Persistence;

namespace PG.Tests.EditMode
{
    public sealed class SaveAtomicityTests
    {
        [Test]
        public void FailedPrimaryReplacementKeepsCurrentSaveAndRecoveryBackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "pg-save-atomic-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "slot.pxgn");
            string backup = path + ".bak";
            File.WriteAllText(path, "current-primary");
            File.WriteAllText(backup, "known-good-backup");

            var commit = typeof(SaveSystem).GetMethod("CommitTemporarySave", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(commit, Is.Not.Null);
            try
            {
                Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    commit.Invoke(null, new object[] { Path.Combine(directory, "missing.tmp"), path }));

                Assert.That(File.ReadAllText(path), Is.EqualTo("current-primary"));
                Assert.That(File.ReadAllText(backup), Is.EqualTo("known-good-backup"));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(backup)) File.Delete(backup);
                Directory.Delete(directory);
            }
        }
    }
}
