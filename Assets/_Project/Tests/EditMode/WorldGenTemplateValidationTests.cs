using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PG.Content;
using PG.EditorTools;
using UnityEngine;

namespace PG.Tests.EditMode
{
    public sealed class WorldGenTemplateValidationTests
    {
        [OneTimeSetUp]
        public void SyncContent() => DataSync.Sync();

        [TestCase("amplitude", -0.01f, "warp.amplitude")]
        [TestCase("amplitude", 0.26f, "warp.amplitude")]
        [TestCase("frequencyMul", 0f, "warp.frequencyMul")]
        [TestCase("frequencyMul", 8.01f, "warp.frequencyMul")]
        public void RejectsOutOfRangeWarpOverrides(string field, float value, string errorField)
        {
            string modRoot = Path.Combine(Path.GetTempPath(), "pg-warp-validation-" + Guid.NewGuid().ToString("N"));
            string dataFolder = Path.Combine(modRoot, "Data");
            Directory.CreateDirectory(dataFolder);
            try
            {
                File.WriteAllText(Path.Combine(modRoot, "manifest.json"),
                    "{\"id\":\"test.warp_validation\",\"name\":\"Warp validation test\",\"version\":\"1.0.0\",\"contentSchemaVersion\":1}");

                var warp = new JObject { [field] = value };
                var patch = new JObject
                {
                    ["category"] = "worldgen_templates",
                    ["items"] = new JArray(new JObject
                    {
                        ["id"] = "wgt.continents",
                        ["warp"] = warp,
                    }),
                };
                File.WriteAllText(Path.Combine(dataFolder, "worldgen_templates.json"), patch.ToString());

                var exception = Assert.Throws<ContentLoadException>(() =>
                    ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), new[] { modRoot }));
                StringAssert.Contains("wgt.continents." + errorField, exception.Message);
            }
            finally
            {
                if (Directory.Exists(modRoot)) Directory.Delete(modRoot, true);
            }
        }
    }
}
