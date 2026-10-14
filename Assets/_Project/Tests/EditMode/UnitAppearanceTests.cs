using NUnit.Framework;
using PG.Render;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class UnitAppearanceTests
    {
        [Test] public void DeathAndInjuryAreBoundedAndDoNotRepeat()
        {
            Assert.AreEqual(0, UnitAnimation.DeathFrame(0));
            Assert.AreEqual(2, UnitAnimation.DeathFrame(60));
            Assert.AreEqual(-1, UnitAnimation.DeathFrame(UnitAnimation.CorpseLifetime));
            Assert.AreEqual(0, UnitAnimation.CorpseAlpha(UnitAnimation.CorpseLifetime));
            var hit = new UnitAnimation.Injury();
            Assert.AreEqual(0, UnitAnimation.ObserveInjury(ref hit, 1, 0, 100));
            Assert.AreEqual(1, UnitAnimation.ObserveInjury(ref hit, 1, 1, 80));
            Assert.AreEqual(1, UnitAnimation.ObserveInjury(ref hit, 1, 1, 80));
            Assert.AreEqual(0, UnitAnimation.ObserveInjury(ref hit, 1, 6, 80));
            Assert.AreEqual(0, UnitAnimation.ObserveInjury(ref hit, 2, 7, 10));
        }

        [TestCase(false, .6f)]
        [TestCase(true, .6f)]
        [TestCase(false, 1f)]
        [TestCase(true, 1f)]
        public void GripStaysAtHandThroughEntireSwingAndAgeScaling(bool flip, float scale)
        {
            var hand = new Vector2(17.35f, 42.1f);
            for (int p = 1; p < (int)UnitProp.Count; p++)
            for (int angle = -90; angle <= 90; angle += 15)
            {
                Vector2 grip = UnitPropMotion.Grip((UnitProp)p);
                Vector2 actual = UnitPropMotion.Corner(hand, Vector2.one * .8f * scale, grip, grip, angle, flip);
                Assert.That(Vector2.Distance(hand, actual), Is.LessThan(.00001f));
                Vector2 tip = UnitPropMotion.Corner(hand, Vector2.one * .8f * scale, grip, Vector2.one, angle, flip);
                Vector2 other = UnitPropMotion.Corner(hand, Vector2.one * .8f * scale, grip, Vector2.one, angle, !flip);
                Assert.That(tip.x - hand.x, Is.EqualTo(hand.x - other.x).Within(.00001f));
                Assert.That(tip.y, Is.EqualTo(other.y).Within(.00001f));
            }
        }

        [TestCase("sp.human", false)]
        [TestCase("sp.orc", false)]
        [TestCase("sp.dwarf", false)]
        [TestCase("sp.elf", false)]
        [TestCase("sp.human", true)]
        [TestCase("sp.orc", true)]
        [TestCase("sp.dwarf", true)]
        [TestCase("sp.elf", true)]
        public void HandAnchorsLandOnVisibleDetailPixels(string species, bool female)
        {
            Assert.IsTrue(DetailedUnitSheet.TryLoad(species + (female ? "_female" : ""), out var sheet, out _, out _));
            try
            {
                for (int animation = 0; animation <= 1; animation++)
                for (int frame = 0; frame < (animation == 0 ? 2 : 4); frame++)
                {
                    Vector2 hand = UnitPropMotion.Hand(species, animation, frame, true, female);
                    int x = frame * 40 + Mathf.FloorToInt(hand.x * 40);
                    int y = (5 - animation) * 40 + Mathf.FloorToInt(hand.y * 40);
                    Assert.Greater(sheet.GetPixel(x, y).a, .5f, $"{species}: row {animation}, frame {frame}");
                }
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        [Test]
        public void SwingHasWindupContactAndRecoveryWithoutRepeatingDuringCooldown()
        {
            float windup = UnitPropMotion.Angle(UnitProp.Axe, .35f, false, 0, 0, 0);
            float impact = UnitPropMotion.Angle(UnitProp.Axe, .7f, false, 0, 0, 0);
            float recovery = UnitPropMotion.Angle(UnitProp.Axe, 1f, false, 0, 0, 0);
            Assert.Greater(windup, 50);
            Assert.Less(impact, -70);
            Assert.Greater(recovery, impact);
            Assert.AreEqual(-1, UnitAnimation.AttackPhase(1, 1));
            Assert.AreEqual(0, UnitAnimation.AttackPhase(20, 1));
            Assert.Greater(UnitAnimation.AttackPhase(15, 1), .9f);
            Assert.Greater(UnitAnimation.AttackPhase(1, 10), .9f, "Fast attacks must also reach contact.");
            float stopped = UnitPropMotion.Angle(UnitProp.Pickaxe, -1, true, 42, 3, 0);
            Assert.AreEqual(stopped, UnitPropMotion.Angle(UnitProp.Pickaxe, -1, true, 42, 3, 0));
            Assert.AreNotEqual(stopped, UnitPropMotion.Angle(UnitProp.Pickaxe, -1, true, 47, 3, 0));
        }

        [TestCase("job.farmer", UnitProp.Scythe)]
        [TestCase("job.lumberjack", UnitProp.Axe)]
        [TestCase("job.miner", UnitProp.Pickaxe)]
        [TestCase("job.builder", UnitProp.Hammer)]
        public void ActiveWorkersShowTheToolForTheirCurrentJob(string jobId, UnitProp expected)
        {
            Assert.AreEqual(expected, UnitAppearance.Resolve(jobId, "eq.sword", doingWork: true));
        }

        [TestCase("eq.sword", UnitProp.Sword)]
        [TestCase("eq.axe", UnitProp.Axe)]
        [TestCase("eq.bow", UnitProp.Bow)]
        [TestCase("eq.crossbow", UnitProp.Crossbow)]
        [TestCase("eq.pickaxe", UnitProp.Pickaxe)]
        [TestCase("eq.scythe", UnitProp.Scythe)]
        public void EquippedWeaponUsesItsRealEquipmentType(string equipmentId, UnitProp expected)
        {
            Assert.AreEqual(expected, UnitAppearance.Resolve("job.warrior", equipmentId, doingWork: false));
        }

        [Test]
        public void EmptyHandsDoNotInventAWeapon()
        {
            Assert.AreEqual(UnitProp.None, UnitAppearance.Resolve("job.warrior", null, doingWork: false));
            Assert.AreEqual(UnitProp.UnarmedStrike, UnitAppearance.Resolve("job.warrior", null, doingWork: false, unarmedAttacking: true));
            Assert.IsFalse(UnitAppearance.CanUseRaceAttackPose("sp.human", null));
        }

        [TestCase("sp.human", "eq.sword", true)]
        [TestCase("sp.orc", "eq.axe", true)]
        [TestCase("sp.dwarf", "eq.hammer", true)]
        [TestCase("sp.elf", "eq.sword", true)]
        [TestCase("sp.human", "eq.bow", false)]
        [TestCase("sp.orc", "eq.sword", false)]
        [TestCase("sp.dwarf", null, false)]
        public void AuthoredAttackPoseIsUsedOnlyForItsMatchingWeapon(string speciesId, string weaponId, bool expected)
        {
            Assert.AreEqual(expected, UnitAppearance.CanUseRaceAttackPose(speciesId, weaponId));
        }

        [Test]
        public void HeldPropsHaveDistinctIndexedSpritesAndTransparentMargins()
        {
            var sword = UnitPropAtlas.Create(UnitProp.Sword);
            var bow = UnitPropAtlas.Create(UnitProp.Bow);
            try
            {
                Assert.NotNull(sword);
                Assert.NotNull(bow);
                Assert.AreEqual(16, sword.width);
                Assert.AreEqual(16, sword.height);
                Assert.AreEqual(0, sword.GetPixel(0, 0).a);
                CollectionAssert.AreNotEqual(sword.GetPixels32(), bow.GetPixels32());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sword);
                UnityEngine.Object.DestroyImmediate(bow);
            }
        }
    }
}
