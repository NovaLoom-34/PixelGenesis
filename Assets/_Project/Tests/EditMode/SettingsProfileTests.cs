using NUnit.Framework;
using PG.UI;

namespace PG.Tests.EditMode
{
    public sealed class SettingsProfileTests
    {
        [Test]
        public void ResetSettingsRestoresPreferencesAndKeepsPlayerProgress()
        {
            var profile = new Profile
            {
                Language = "en",
                UiScale = 1.5f,
                EdgePan = false,
                TargetFps = 120,
                PerformancePreset = "high",
                HighContrast = true,
                ColorBlindPalette = true,
                DisableScreenShake = true,
            };
            profile.Volumes[0] = 0.25f;
            profile.Earn("ach.first_life");
            profile.Discover("sp.human");
            profile.ToggleFavorite("pw.heal");
            profile.WatchedKingdoms.Add("0000000000000001:64x64:7");

            profile.ResetSettings();

            Assert.AreEqual("tr", profile.Language);
            Assert.AreEqual(1f, profile.UiScale);
            Assert.IsTrue(profile.EdgePan);
            Assert.AreEqual(60, profile.TargetFps);
            Assert.AreEqual("balanced", profile.PerformancePreset);
            Assert.IsFalse(profile.HighContrast);
            Assert.IsFalse(profile.ColorBlindPalette);
            Assert.IsFalse(profile.DisableScreenShake);
            Assert.AreEqual(1f, profile.Volumes[0]);
            Assert.IsTrue(profile.HasAchievement("ach.first_life"));
            Assert.IsTrue(profile.IsDiscovered("sp.human"));
            CollectionAssert.Contains(profile.FavoritePowers, "pw.heal");
            CollectionAssert.Contains(profile.WatchedKingdoms, "0000000000000001:64x64:7");
            Assert.IsTrue(profile.Dirty);
        }
    }
}
