using System.IO;
using NUnit.Framework;
using PG.Boot;
using PG.Content;
using PG.Core;
using PG.EditorTools;
using PG.Render;
using PG.Sim;
using PG.World;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class BuildingVisualTests
    {
        ContentDB _db;
        SimWorld _sim;
        GameObject _go;
        BuildingRenderer _renderer;

        [OneTimeSetUp]
        public void LoadContent()
        {
            DataSync.Sync();
            _db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
        }

        [SetUp]
        public void Setup()
        {
            _sim = new SimWorld(_db, new WorldMap(64, 64, _db), new SimRandomProvider(91));
            _go = new GameObject("BuildingVisualTest");
            _renderer = _go.AddComponent<BuildingRenderer>();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_go);
            _sim.Dispose();
        }

        [Test]
        public void ContactShadowsDisappearWithDeadBuildingsWithoutChangingTheWorld()
        {
            int b = _sim.Civ.PlaceBuilding(-1, _db.Buildings.IdOf("bld.house"), new int2(16, 16), 1f, 0);
            ulong world = _sim.World.ComputeHash(), civ = Diagnostics.CivHash(_sim);
            _renderer.Bind(_sim);
            _renderer.FlushAll();
            var shadow = _go.transform.Find("BuildingMeshes/BuildingShadows");
            Assert.That(shadow, Is.Not.Null, "Buildings need a ground contact shadow layer.");
            var mesh = shadow.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(32), "Keep the per-building shadow cost bounded.");
            Assert.That(shadow.GetComponent<MeshRenderer>().sortingOrder, Is.LessThan(FeatureRenderer.SortingOrder));
            Assert.That(_sim.World.ComputeHash(), Is.EqualTo(world));
            Assert.That(Diagnostics.CivHash(_sim), Is.EqualTo(civ));
            Assert.That(_sim.Clock.Tick, Is.Zero);

            var data = _sim.Civ.Buildings[b]; data.Flags = BuildingFlags.Dead; _sim.Civ.Buildings[b] = data;
            _sim.Civ.Version++;
            _renderer.FlushAll();
            Assert.That(mesh.vertexCount, Is.Zero, "Deleted buildings must not leave a shadow behind.");
        }

        [Test]
        public void RebindingAndUnbindingReleaseAllOldMeshes()
        {
            _sim.Civ.PlaceBuilding(-1, _db.Buildings.IdOf("bld.house"), new int2(16, 16), 1f, 0);
            _renderer.Bind(_sim); _renderer.FlushAll();
            var oldMeshes = _go.GetComponentsInChildren<MeshFilter>();
            var oldResources = new Mesh[oldMeshes.Length];
            for (int i = 0; i < oldMeshes.Length; i++) oldResources[i] = oldMeshes[i].sharedMesh;
            _renderer.Bind(_sim); _renderer.FlushAll();
            foreach (var old in oldMeshes) Assert.That(old == null, Is.True);
            foreach (var old in oldResources) Assert.That(old == null, Is.True, "Native meshes must be released on world changes.");
            Assert.That(_go.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(3));
            _renderer.Unbind();
            Assert.That(_go.transform.childCount, Is.Zero);
            Assert.DoesNotThrow(() => _renderer.FlushAll());
        }

        [Test]
        public void BuildingArtIsSlicedIntoMapAlignedPieces()
        {
            var def = _db.Buildings.IdOf("bld.house");
            var b = _sim.Civ.PlaceBuilding(-1, def, new int2(16, 16), 1f, 0);
            var data = _sim.Civ.Buildings[b];
            _renderer.Bind(_sim); _renderer.FlushAll();
            var mesh = _go.transform.Find("BuildingMeshes/Buildings").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.GreaterThan(4), "One quad is still a single image pasted over the map.");
            Assert.That(mesh.vertexCount % 4, Is.Zero);
            var vertices = mesh.vertices;
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var vertex in vertices) { minX = Mathf.Min(minX, vertex.x); maxX = Mathf.Max(maxX, vertex.x); }
            Assert.That(maxX - minX, Is.EqualTo(data.W * BuildingRenderer.VisualScale).Within(.001f));
            Assert.That((minX + maxX) * .5f, Is.EqualTo(data.Origin.x + data.W * .5f).Within(.001f));
            var uvs = mesh.uv;
            float right = data.Origin.x + data.W;
            for (int q = 0; q < vertices.Length; q += 4)
            {
                float width = vertices[q + 1].x - vertices[q].x;
                float height = vertices[q + 3].y - vertices[q].y;
                Assert.That(width, Is.GreaterThan(0f).And.LessThanOrEqualTo(BuildingDef.CellTiles + 0.001f));
                Assert.That(height, Is.GreaterThan(0f).And.LessThanOrEqualTo(BuildingDef.CellTiles + 0.001f));
                Assert.That(vertices[q].x, Is.GreaterThanOrEqualTo(data.Origin.x));
                Assert.That(vertices[q + 1].x, Is.LessThanOrEqualTo(right + 0.001f));
                Assert.That(uvs[q + 1].x, Is.GreaterThan(uvs[q].x));
            }
        }

        [Test]
        public void ConstructionCompletionRuinAndBurningKeepFootprintAndFullColorArt()
        {
            int def = _db.Buildings.IdOf("bld.house");
            int b = _sim.Civ.PlaceBuilding(-1, def, new int2(12, 16), 0.4f, 0);
            int elf = _sim.Civ.PlaceBuilding(-1, def, new int2(32, 16), 1f, 0);
            var legacy = _sim.Civ.Buildings[elf]; legacy.Style = (ushort)_db.BuildingStyles.IdOf("style.beast"); _sim.Civ.Buildings[elf] = legacy;
            _renderer.Bind(_sim);
            Vector2[] previousUv = null;
            for (int state = 0; state < 3; state++)
            {
                var data = _sim.Civ.Buildings[b];
                data.BuildProgress = state == 0 ? 0.4f : 1f;
                data.Flags = state == 2 ? BuildingFlags.Ruin : BuildingFlags.None;
                _sim.Civ.Buildings[b] = data; _sim.Civ.Version++;
                ulong world = _sim.World.ComputeHash(), civ = Diagnostics.CivHash(_sim);
                _renderer.FlushAll();
                var mesh = _go.transform.Find("BuildingMeshes/Buildings").GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.GreaterThan(8));
                Assert.That(mesh.vertices[1].x - mesh.vertices[0].x, Is.LessThanOrEqualTo(BuildingDef.CellTiles + 1e-4f)); // float rounding
                Assert.That(mesh.vertices[0].y, Is.GreaterThanOrEqualTo(data.Origin.y));
                Assert.That(mesh.uv2[0].y, Is.EqualTo(1f), "Human art uses original colors in every state.");
                int legacyStart = System.Array.FindIndex(mesh.vertices, v => v.x >= 32f);
                Assert.That(legacyStart, Is.GreaterThan(0));
                Assert.That(mesh.uv2[legacyStart].y, Is.EqualTo(1f), "Beast style ships full-color sheets now, so it also uses original colors.");
                if (previousUv != null) Assert.That(mesh.uv[0], Is.Not.EqualTo(previousUv[0]), "Each state must select different art.");
                previousUv = mesh.uv;
                Assert.That(_sim.World.ComputeHash(), Is.EqualTo(world));
                Assert.That(Diagnostics.CivHash(_sim), Is.EqualTo(civ));
            }
            var burning = _sim.Civ.Buildings[b]; burning.Flags = BuildingFlags.Burning; _sim.Civ.Buildings[b] = burning;
            _sim.Civ.Version++; _renderer.FlushAll();
            var burningMesh = _go.transform.Find("BuildingMeshes/Buildings").GetComponent<MeshFilter>().sharedMesh;
            var colors = burningMesh.colors32;
            int other = System.Array.FindIndex(burningMesh.vertices, v => v.x >= 32f);
            Assert.That(colors[0], Is.EqualTo(new Color32(255, 150, 110, 255)));
            Assert.That(colors[other], Is.EqualTo(new Color32(255, 255, 255, 255)));
        }
    }
}
