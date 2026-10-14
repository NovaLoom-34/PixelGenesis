using System;
using System.Collections.Generic;
using NUnit.Framework;
using PG.Render;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class DetailedUnitArtTests
    {
        [TestCase("human")]
        [TestCase("orc")]
        [TestCase("dwarf")]
        [TestCase("elf")]
        [TestCase("sheep")]
        [TestCase("wolf")]
        [TestCase("bear")]
        [TestCase("deer")]
        [TestCase("dragon")]
        [TestCase("slime")]
        [TestCase("skeleton")]
        [TestCase("fairy")]
        [TestCase("zombie")]
        [TestCase("fire_elemental")]
        [TestCase("ember_imp")]
        [TestCase("sandworm")]
        [TestCase("walking_tree")]
        [TestCase("ghost")]
        [TestCase("water_elemental")]
        [TestCase("earth_golem")]
        [TestCase("kraken")]
        [TestCase("rot_crawler")]
        [TestCase("candy_golem")]
        [TestCase("gummy_bear")]
        [TestCase("shroomling")]
        [TestCase("crystal_beetle")]
        [TestCase("ash_crawler")]
        [TestCase("bone_crawler")]
        [TestCase("clock_crab")]
        [TestCase("void_moth")]
        [TestCase("flesh_mound")]
        [TestCase("devourer")]
        [TestCase("zombie_runner")]
        [TestCase("zombie_brute")]
        [TestCase("zombie_bloater")]
        [TestCase("zombie_crawler")]
        [TestCase("zombie_beast")]
        [TestCase("zombie_dragon")]
        [TestCase("zombie_lord")]
        [TestCase("necromancer")]
        [TestCase("bloodsucker")]
        [TestCase("sky_whale_calf")]
        [TestCase("dark_mage")]
        [TestCase("white_mage")]
        [TestCase("druid")]
        [TestCase("snowman")]
        [TestCase("storm_spirit")]
        [TestCase("cat")]
        [TestCase("dog")]
        [TestCase("rabbit")]
        [TestCase("cow")]
        [TestCase("chicken")]
        [TestCase("rat")]
        [TestCase("monkey")]
        [TestCase("fox")]
        [TestCase("boar")]
        [TestCase("goat")]
        [TestCase("horse_wild")]
        [TestCase("reindeer")]
        [TestCase("penguin")]
        [TestCase("crab")]
        [TestCase("scorpion")]
        [TestCase("lizard")]
        [TestCase("crocodile")]
        [TestCase("snake")]
        [TestCase("turtle")]
        [TestCase("seal")]
        [TestCase("hyena")]
        [TestCase("lion")]
        [TestCase("rhino")]
        [TestCase("buffalo")]
        [TestCase("camel")]
        [TestCase("polar_bear")]
        [TestCase("salamander")]
        [TestCase("frog")]
        [TestCase("bee")]
        [TestCase("ant")]
        [TestCase("beetle")]
        [TestCase("owl")]
        [TestCase("crow")]
        [TestCase("eagle")]
        [TestCase("parrot")]
        [TestCase("snail")]
        [TestCase("fish")]
        [TestCase("butterfly")]
        [TestCase("caterpillar")]
        [TestCase("piranha")]
        [TestCase("mosquito_swarm")]
        [TestCase("sky_visitor")]
        [TestCase("colossus")]
        [TestCase("bandit")]
        [TestCase("cultist")]
        public void AuthoredSheetsHaveTransparentGuttersAndDistinctWalkPoses(string species)
        {
            Assert.IsTrue(DetailedUnitSheet.TryLoad("sp." + species, out var sheet, out var size, out var foot, out var fullColor));
            try
            {
                Assert.AreEqual(160, sheet.width);
                Assert.AreEqual(240, sheet.height);
                Assert.AreEqual(new Vector2(2, 2), size);
                Assert.That(foot, Is.EqualTo(.2f).Within(.0001f));
                var pixels = sheet.GetPixels32();
                var poses = new HashSet<string>();
                for (int row = 0; row < 6; row++)
                for (int col = 0; col < 4; col++)
                {
                    var pose = new byte[1600];
                    int opaque = 0;
                    for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 40; x++)
                    {
                        var c = pixels[(239 - row * 40 - y) * 160 + col * 40 + x];
                        if (x == 0 || x == 39 || y == 0 || y == 39) Assert.AreEqual(0, c.a, "Frame bleed");
                        if (c.a == 0) continue;
                        opaque++;
                        Assert.AreEqual(255, c.a);
                        if (!fullColor)
                        {
                            Assert.AreEqual(c.r, c.g); Assert.AreEqual(c.r, c.b);
                            Assert.That(c.r, Is.InRange(28, 224)); Assert.AreEqual(0, c.r % 28);
                        }
                        pose[y * 40 + x] = c.r;
                    }
                    Assert.Greater(opaque, 30, "Empty pose");
                    if (row == 1) poses.Add(Convert.ToBase64String(pose));
                }
                Assert.GreaterOrEqual(poses.Count, 3);
            }
            finally { UnityEngine.Object.DestroyImmediate(sheet); }
        }

        [Test]
        public void MotionUsesDisplacementAndSurvivesPauseAndReusedSlots()
        {
            var sample = new UnitAnimation.Motion();
            UnitAnimation.Observe(ref sample, 12, 100, new float2(2, 3), true);
            UnitAnimation.Observe(ref sample, 12, 101, new float2(2, 3), true);
            Assert.IsFalse(sample.Moving, "A blocked path must not walk in place");
            UnitAnimation.Observe(ref sample, 12, 102, new float2(3, 3), false);
            Assert.IsTrue(sample.Moving);
            UnitAnimation.Observe(ref sample, 12, 102, new float2(3, 3), false);
            Assert.IsTrue(sample.Moving, "Repeated render of paused tick keeps pose");
            UnitAnimation.Observe(ref sample, 13, 103, new float2(8, 8), false);
            Assert.IsFalse(sample.Moving, "Reused unit slot must not inherit displacement");
        }

        [Test]
        public void AttackPlaysOnceThenReturnsToRest()
        {
            Assert.AreEqual(0, UnitAnimation.AttackFrame(20, 1));
            Assert.AreEqual(1, UnitAnimation.AttackFrame(18, 1));
            Assert.AreEqual(2, UnitAnimation.AttackFrame(16, 1));
            Assert.AreEqual(-1, UnitAnimation.AttackFrame(14, 1));
            Assert.AreEqual(-1, UnitAnimation.AttackFrame(0, 1));
            Assert.AreNotEqual(UnitAnimation.Frame(2000000000, 0, 1), UnitAnimation.Frame(2000000003, 0, 1));
        }

        [Test]
        public void PoseMaskExcludesNeighbourInsideItsRectangle()
        {
            var source = new Texture2D(4, 4);
            Texture2D result = null;
            try
            {
                var white = new Color32[16];
                for (int i = 0; i < white.Length; i++) white[i] = new Color32(224, 224, 224, 255);
                source.SetPixels32(white); source.Apply();
                var mask = new byte[16]; mask[5] = 1;
                var frames = new DetailedUnitSheet.Frame[24];
                for (int i = 0; i < frames.Length; i++) frames[i] = new DetailedUnitSheet.Frame { width = 4, height = 4, mask = Convert.ToBase64String(mask) };
                var layout = new DetailedUnitSheet.Layout { version = 1, frameSize = 40, baseline = 36, scale = 1, worldSize = 2, frames = frames };
                result = DetailedUnitSheet.Build(source, layout);
                int opaque = 0;
                foreach (var p in result.GetPixels32()) if (p.a > 0) opaque++;
                Assert.AreEqual(24, opaque, "Only the chosen component may appear in each frame");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
            }
        }

        [TestCase(196, 216, .125f)]
        [TestCase(185, 149, .12162162f)]
        public void SourceFallbackUsesTheMaskBakersMidpointRounding(int frameWidth, int frameHeight, float scale)
        {
            int width = Mathf.Max(1, Mathf.FloorToInt(frameWidth * scale + .5f));
            int height = Mathf.Max(1, Mathf.FloorToInt(frameHeight * scale + .5f));
            var source = new Texture2D(frameWidth, frameHeight);
            Texture2D result = null;
            try
            {
                var pixels = new Color32[frameWidth * frameHeight];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(224, 224, 224, 255);
                source.SetPixels32(pixels);
                source.Apply();

                var mask = new byte[width * height];
                for (int i = 0; i < mask.Length; i++) mask[i] = 1;
                var frames = new DetailedUnitSheet.Frame[24];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = new DetailedUnitSheet.Frame
                    {
                        width = frameWidth,
                        height = frameHeight,
                        mask = Convert.ToBase64String(mask),
                    };
                var layout = new DetailedUnitSheet.Layout
                {
                    version = 1,
                    frameSize = 40,
                    baseline = 36,
                    scale = scale,
                    worldSize = 2,
                    frames = frames,
                };

                result = DetailedUnitSheet.Build(source, layout);
                Assert.AreEqual(160, result.width);
                Assert.AreEqual(240, result.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (result != null) UnityEngine.Object.DestroyImmediate(result);
            }
        }

        [Test]
        public void InvalidArtCannotReadOutsideSource()
        {
            var source = new Texture2D(4, 4);
            try
            {
                var frames = new DetailedUnitSheet.Frame[24];
                for (int i = 0; i < frames.Length; i++) frames[i] = new DetailedUnitSheet.Frame { x = 3, y = 0, width = 2, height = 2 };
                var layout = new DetailedUnitSheet.Layout { version = 1, frameSize = 40, baseline = 36, scale = 1, worldSize = 2, frames = frames };
                Assert.Throws<ArgumentException>(() => DetailedUnitSheet.Build(source, layout));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
