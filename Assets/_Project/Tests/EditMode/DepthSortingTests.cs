using NUnit.Framework;
using PG.Render;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class DepthSortingTests
    {
        [Test] public void SouthIsNearerAndZNeverNegative()
        {
            Assert.That(SortingLayers.DepthZ(10f), Is.LessThan(SortingLayers.DepthZ(11f)), "A larger Y is further from the camera.");
            Assert.That(SortingLayers.DepthZ(0f), Is.GreaterThanOrEqualTo(0f), "Flat Z = 0 layers must stay in front of every object.");
        }

        [Test] public void OneTileIsFarAboveDepthBufferPrecision()
        {
            // Camera near 0.3 / far 100: a 24-bit buffer resolves ~6e-6 there; one tile must be far larger.
            Assert.That(SortingLayers.DepthPerTile, Is.GreaterThan(1e-3f));
            Assert.That(SortingLayers.DepthZ(4096f), Is.LessThan(50f), "The tallest map must stay inside the far plane.");
        }

        [TestCase("PG_Sprite")]
        [TestCase("PG_Unit")]
        public void DepthSortedShadersExposeZWrite(string shaderName)
        {
            var shader = Resources.Load<Shader>(shaderName);
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_ZWrite"), Is.True, "Renderers enable depth writes through _ZWrite.");
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f), "Clouds, overlays and FX keep depth writes off by default.");
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
