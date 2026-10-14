using NUnit.Framework;
using PG.UI;
using UnityEngine.UIElements;

namespace PG.Tests.EditMode
{
    public sealed class UiWorkflowTests
    {
        sealed class TestWindow : WindowBase
        {
            public TestWindow() : base(null, "Test") { }
            protected override void Build() { }
        }

        [Test]
        public void FifthWindowPreservesAllPinnedWindows()
        {
            var manager = new WindowManager(new VisualElement());
            for (int i = 0; i < 4; i++) manager.Show(i, () => new TestWindow()).SetPinned(true);
            Assert.That(manager.Show(5, () => new TestWindow()), Is.Null);
            Assert.That(manager.Count, Is.EqualTo(4));
            foreach (var window in manager.Open) Assert.That(window.Pinned, Is.True);
        }

        [Test]
        public void ReopeningWindowMakesItTheEscapeTarget()
        {
            var manager = new WindowManager(new VisualElement());
            var first = manager.Show(1, () => new TestWindow());
            manager.Show(2, () => new TestWindow());
            Assert.That(manager.Show(1, () => new TestWindow()), Is.SameAs(first));
            manager.CloseTop();
            Assert.That(manager.Count, Is.EqualTo(1));
            Assert.That(manager.Open[0].Key, Is.EqualTo(2));
        }

        [Test]
        public void ProfileDeduplicatesDiscoveriesAndAchievements()
        {
            var profile = new Profile();
            Assert.That(profile.Discover("tr.strong"), Is.True);
            Assert.That(profile.Discover("tr.strong"), Is.False);
            Assert.That(profile.Earn("ach.test"), Is.True);
            Assert.That(profile.Earn("ach.test"), Is.False);
            Assert.That(profile.Discovered.Count, Is.EqualTo(1));
            Assert.That(profile.Achievements.Count, Is.EqualTo(1));
        }
    }
}
