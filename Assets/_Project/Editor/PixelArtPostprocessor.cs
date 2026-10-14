using System.IO;
using UnityEditor;
using UnityEngine;

namespace PG.EditorTools
{
    // Enforces the bulletproof pixel art import pipeline across all Art/ and StreamingAssets/Art/ textures.
    // Reference: unity-2d-visual-dev playbook.
    public sealed class PixelArtPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("Art/") && !assetPath.Contains("StreamingAssets/Art/")) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        [MenuItem("PixelGenesis/Tools/Reimport All Pixel Art")]
        public static void ReimportAllPixelArt()
        {
            int count = 0;
            string[] searchFolders = { "Assets/StreamingAssets/Art", "Assets/_Project" };
            foreach (var folder in searchFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".png") || path.EndsWith(".jpg"))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        count++;
                    }
                }
            }
            Debug.Log($"[PixelArtPostprocessor] Reimported {count} pixel art textures.");
        }
    }
}
