using System.Collections.Generic;
using NUnit.Framework;
using PG.Render;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class AccessibilityPaletteTests
    {
        [Test]
        public void ColorBlindIdentityPaletteUsesStableDistinctColorsForFirstEightEntries()
        {
            var colors = new HashSet<Color32>();
            for (int i = 0; i < 8; i++) colors.Add(OverlayRenderer.ColorBlindColorForKey(i));

            Assert.That(colors, Has.Count.EqualTo(8));
            Assert.That(OverlayRenderer.ColorBlindColorForKey(9), Is.EqualTo(OverlayRenderer.ColorBlindColorForKey(1)));
        }
    }
}
