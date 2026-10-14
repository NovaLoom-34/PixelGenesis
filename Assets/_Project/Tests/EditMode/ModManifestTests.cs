using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PG.Content;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class ModManifestTests
    {
        string _folder;
        string _schema;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "PixelGenesis-ModManifest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            _schema = File.ReadAllText(Path.Combine(projectRoot, "Data", "_schemas", "mod_manifest.schema.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            ModManifest.Warnings.Clear();
        }

        [Test]
        public void LegacyManifestDefaultsToCurrentContentSchema()
        {
            File.WriteAllText(Path.Combine(_folder, "manifest.json"), "{\"id\":\"author.legacy\",\"name\":\"Legacy\",\"version\":\"1.0.0\"}");
            var errors = new List<string>();
            var mods = ModManifest.Order(new[] { _folder }, errors, _schema);

            Assert.That(errors, Is.Empty);
            Assert.That(mods, Has.Count.EqualTo(1));
            Assert.That(mods[0].ContentSchemaVersion, Is.EqualTo(ModManifest.CurrentContentSchemaVersion));
        }

        [Test]
        public void UnsupportedContentSchemaIsSkippedByManifestSchema()
        {
            File.WriteAllText(Path.Combine(_folder, "manifest.json"), "{\"id\":\"author.future\",\"version\":\"1.0.0\",\"contentSchemaVersion\":2}");
            var errors = new List<string>();
            var mods = ModManifest.Order(new[] { _folder }, errors, _schema);

            Assert.That(errors, Is.Empty);
            Assert.That(mods, Is.Empty);
            Assert.That(ModManifest.Warnings.Exists(w => w.Contains("manifest.json") && w.Contains("contentSchemaVersion")), Is.True);
        }

        [Test]
        public void InvalidManifestIsSkippedWithoutFailingBaseContent()
        {
            File.WriteAllText(Path.Combine(_folder, "manifest.json"), "{\"id\":\"author.invalid\",\"name\":\"Invalid\",\"version\":\"1.0.0\",\"unexpected\":true}");
            var errors = new List<string>();

            var mods = ModManifest.Order(new[] { _folder }, errors, _schema);

            Assert.That(errors, Is.Empty);
            Assert.That(mods, Is.Empty);
            Assert.That(ModManifest.Warnings.Exists(w => w.Contains("unknown property 'unexpected'") && w.Contains("mod skipped")), Is.True);
        }

        [Test]
        public void ManifestSchemaRejectsUnknownFieldsAndInvalidIds()
        {
            const string json = "{\"id\":\"bad id\",\"name\":\"Bad\",\"version\":\"1.0\",\"extra\":true}";

            bool valid = ModManifestSchema.TryValidate(json, _schema, out string[] errors);

            Assert.That(valid, Is.False);
            Assert.That(errors, Has.Length.GreaterThanOrEqualTo(2));
            Assert.That(string.Join(" ", errors), Does.Contain("id"));
            Assert.That(string.Join(" ", errors), Does.Contain("extra"));
        }

        [Test]
        public void ManifestSchemaRejectsDuplicateDependenciesAndWrongFieldTypes()
        {
            const string json = "{\"id\":\"author.mod\",\"name\":\"Mod\",\"version\":\"1.0.0\",\"dependencies\":[\"author.lib\",\"author.lib\"],\"hasCode\":\"false\"}";

            bool valid = ModManifestSchema.TryValidate(json, _schema, out string[] errors);

            Assert.That(valid, Is.False);
            Assert.That(string.Join(" ", errors), Does.Contain("dependencies"));
            Assert.That(string.Join(" ", errors), Does.Contain("hasCode"));
        }

        [Test]
        public void ManifestSchemaAcceptsCurrentManifestShape()
        {
            const string json = "{\"id\":\"author.mod\",\"name\":\"Mod\",\"version\":\"1.2.0\",\"contentSchemaVersion\":1,\"dependencies\":[\"author.lib>=1.0\"],\"loadAfter\":[\"author.base\"],\"hasCode\":false}";

            bool valid = ModManifestSchema.TryValidate(json, _schema, out string[] errors);

            Assert.That(valid, Is.True, string.Join("; ", errors));
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ShippedExampleModUsesSupportedManifestSchema()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string exampleManifest = File.ReadAllText(Path.Combine(projectRoot, "Examples", "Mods", "example.world_notes", "manifest.json"));

            bool valid = ModManifestSchema.TryValidate(exampleManifest, _schema, out string[] errors);

            Assert.That(valid, Is.True, string.Join("; ", errors));
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void LoadOrderCycleSkipsCycleAndModsThatRequireItButKeepsIndependentMods()
        {
            string aFolder = Path.Combine(_folder, "a");
            string bFolder = Path.Combine(_folder, "b");
            string dependentFolder = Path.Combine(_folder, "dependent");
            string independentFolder = Path.Combine(_folder, "independent");
            Directory.CreateDirectory(aFolder);
            Directory.CreateDirectory(bFolder);
            Directory.CreateDirectory(dependentFolder);
            Directory.CreateDirectory(independentFolder);
            File.WriteAllText(Path.Combine(aFolder, "manifest.json"),
                "{\"id\":\"author.a\",\"name\":\"A\",\"version\":\"1.0.0\",\"contentSchemaVersion\":1,\"loadAfter\":[\"author.b\"]}");
            File.WriteAllText(Path.Combine(bFolder, "manifest.json"),
                "{\"id\":\"author.b\",\"name\":\"B\",\"version\":\"1.0.0\",\"contentSchemaVersion\":1,\"loadAfter\":[\"author.a\"]}");
            File.WriteAllText(Path.Combine(dependentFolder, "manifest.json"),
                "{\"id\":\"author.dependent\",\"name\":\"Dependent\",\"version\":\"1.0.0\",\"contentSchemaVersion\":1,\"dependencies\":[\"author.a\"]}");
            File.WriteAllText(Path.Combine(independentFolder, "manifest.json"),
                "{\"id\":\"author.independent\",\"name\":\"Independent\",\"version\":\"1.0.0\",\"contentSchemaVersion\":1}");
            var errors = new List<string>();

            var mods = ModManifest.Order(new[] { aFolder, bFolder, dependentFolder, independentFolder }, errors, _schema);

            Assert.That(errors, Is.Empty);
            Assert.That(mods.ConvertAll(mod => mod.Id), Is.EqualTo(new[] { "author.independent" }));
            Assert.That(ModManifest.Warnings.Exists(w => w.Contains("author.a") && w.Contains("load order cycle") && w.Contains("skipped")), Is.True);
            Assert.That(ModManifest.Warnings.Exists(w => w.Contains("author.b") && w.Contains("load order cycle") && w.Contains("skipped")), Is.True);
            Assert.That(ModManifest.Warnings.Exists(w => w.Contains("author.dependent") && w.Contains("needs author.a")), Is.True);
        }
    }
}
