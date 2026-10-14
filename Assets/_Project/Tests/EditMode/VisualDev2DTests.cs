using NUnit.Framework;
using PG.Render;
using PG.UI;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    // Verifies 2D visual rules: camera depth locks, clipping planes, Y-axis sorting, and sorting layer hierarchy.
    // Reference: unity-2d-visual-dev playbook.
    public sealed class VisualDev2DTests
    {
        GameObject _go;
        Camera _camera;
        CameraController _controller;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestCamera", typeof(Camera), typeof(CameraController));
            _camera = _go.GetComponent<Camera>();
            _controller = _go.GetComponent<CameraController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void CameraKeepsStrictZDepthAndClipPlanes()
        {
            _controller.Bind(128, 128);

            Assert.That(_camera.orthographic, Is.True, "Camera must be orthographic.");
            Assert.That(_camera.nearClipPlane, Is.EqualTo(0.3f), "Near clip plane must be configured at 0.3f.");
            Assert.That(_camera.farClipPlane, Is.EqualTo(100f), "Far clip plane must be configured at 100f.");
            Assert.That(_camera.transparencySortMode, Is.EqualTo(TransparencySortMode.CustomAxis), "Must use CustomAxis transparency sort mode.");
            Assert.That(_camera.transparencySortAxis, Is.EqualTo(new Vector3(0f, 1f, 0f)), "Must sort along Y axis for top-down perspective.");
            Assert.That(_camera.transform.position.z, Is.EqualTo(CameraSettings.CameraZ), "Camera Z must be locked to CameraZ (-10f).");
        }

        [Test]
        public void CameraFocusPreservesZDepth()
        {
            _controller.Bind(128, 128);
            _controller.Focus(new float2(32.5f, 48.7f));

            Assert.That(_camera.transform.position.z, Is.EqualTo(CameraSettings.CameraZ), "Focus must not alter Z coordinate.");
            Assert.That(_camera.transform.position.x, Is.EqualTo(32.5f).Within(0.1f));
            Assert.That(_camera.transform.position.y, Is.EqualTo(48.7f).Within(0.1f));
        }

        [Test]
        public void ScreenToWorldCorrectlyCalculatesFromCameraZ()
        {
            _controller.Bind(128, 128);
            var centerScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var world = _controller.ScreenToWorld(centerScreen);

            Assert.That(world.x, Is.EqualTo(64f).Within(1f));
            Assert.That(world.y, Is.EqualTo(64f).Within(1f));
        }

        [Test]
        public void SortingLayersAndOrdersAreCoherent()
        {
            Assert.That(SortingLayers.OrderTerrain, Is.LessThan(SortingLayers.OrderCityBorders));
            Assert.That(SortingLayers.OrderCityBorders, Is.LessThan(SortingLayers.OrderBuildingShadows));
            Assert.That(SortingLayers.OrderBuildingShadows, Is.LessThan(SortingLayers.OrderFeatures));
            Assert.That(SortingLayers.OrderFeatures, Is.LessThan(SortingLayers.OrderBuildings));
            Assert.That(SortingLayers.OrderBuildings, Is.LessThan(SortingLayers.OrderUnits));
            Assert.That(SortingLayers.OrderUnits, Is.LessThan(SortingLayers.OrderProjectiles));
            Assert.That(SortingLayers.OrderProjectiles, Is.LessThan(SortingLayers.OrderOverlay));
            Assert.That(SortingLayers.OrderOverlay, Is.LessThan(SortingLayers.OrderClouds));
            Assert.That(SortingLayers.OrderClouds, Is.LessThan(SortingLayers.OrderDayNightLight));
            Assert.That(SortingLayers.OrderDayNightLight, Is.LessThan(SortingLayers.OrderPowerFx));
        }

        [Test]
        public void AllRenderersReferenceSortingLayersConstants()
        {
            Assert.That(NatureRenderer.CloudOrder, Is.EqualTo(SortingLayers.OrderClouds));
            Assert.That(NatureRenderer.ShadowOrder, Is.EqualTo(SortingLayers.OrderCloudShadow));
            Assert.That(PowerFxRenderer.FxOrder, Is.EqualTo(SortingLayers.OrderPowerFx));
            Assert.That(PowerFxRenderer.ShadowOrder, Is.EqualTo(SortingLayers.OrderPowerShadow));
            Assert.That(PowerFxRenderer.OverlayOrder, Is.EqualTo(SortingLayers.OrderFxOverlay));
            Assert.That(BuildingRenderer.SortingOrder, Is.EqualTo(SortingLayers.OrderBuildings));
            Assert.That(BuildingRenderer.BorderSortingOrder, Is.EqualTo(SortingLayers.OrderCityBorders));
            Assert.That(BuildingRenderer.ShadowSortingOrder, Is.EqualTo(SortingLayers.OrderBuildingShadows));
            Assert.That(FeatureRenderer.SortingOrder, Is.EqualTo(SortingLayers.OrderFeatures));
            Assert.That(UnitRenderer.SortingOrder, Is.EqualTo(SortingLayers.OrderUnits));
            Assert.That(OverlayRenderer.Order, Is.EqualTo(SortingLayers.OrderOverlay));
        }

        [Test]
        public void SortingLayerNamesAreValidAndDistinct()
        {
            var layers = new[]
            {
                SortingLayers.Default, SortingLayers.Water, SortingLayers.Terrain,
                SortingLayers.Decals, SortingLayers.Features, SortingLayers.Buildings,
                SortingLayers.Units, SortingLayers.Projectiles, SortingLayers.Weather,
                SortingLayers.DayNightLight, SortingLayers.WorldUI
            };
            var set = new System.Collections.Generic.HashSet<string>(layers);
            Assert.That(set.Count, Is.EqualTo(layers.Length), "All sorting layers must be distinct.");
            foreach (var l in layers)
                Assert.That(string.IsNullOrEmpty(l), Is.False, "Sorting layer name must not be empty.");
        }

        [Test]
        public void GrassGroundMaterialLoadsAsTiledPixelTexture()
        {
            var texture = Resources.Load<Texture2D>("terrain_grass_detail_v7");

            Assert.That(texture, Is.Not.Null, "The terrain ground material must be included in Resources.");
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(512));
            Assert.That(texture.height, Is.EqualTo(texture.width), "The ground material must tile squarely.");
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point), "Pixel clusters must remain crisp.");
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), "The ground material must repeat over continuous terrain.");
        }
    }
}
