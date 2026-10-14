using System;
using System.IO;
using PG.Content;
using UnityEngine;

namespace PG.Render
{
    // Import authored poses into the existing indexed 4x6 atlas. Source PNGs remain unchanged.
    public static class DetailedUnitSheet
    {
        [Serializable] public sealed class Frame { public int x, y, width, height; public string mask; }
        [Serializable] public sealed class Layout
        {
            public int version, frameSize, baseline;
            public float worldSize, scale;
            public bool fullColor;
            public Frame[] frames;
        }

        public static bool TryLoad(string id, out Texture2D sheet, out Vector2 worldSize, out float footOffset)
            => TryLoad(id, out sheet, out worldSize, out footOffset, out _);

        public static bool TryLoad(string id, out Texture2D sheet, out Vector2 worldSize, out float footOffset, out bool fullColor)
        {
            sheet = null; worldSize = Vector2.zero; footOffset = 0f; fullColor = false;
            string name = id.Substring(id.IndexOf('.') + 1);
            string path = Path.Combine(SpriteLibrary.ArtRoot, "Final", "units", name + "_detail");
            if (TryLoadRuntime(name, out sheet, out worldSize, out footOffset, out fullColor)) return true;
            if (!ContentFiles.Exists(path + ".png") || !ContentFiles.Exists(path + ".json")) return false;
            Texture2D source = null;
            try
            {
                var layout = JsonUtility.FromJson<Layout>(ContentFiles.ReadAllText(path + ".json"));
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!source.LoadImage(ContentFiles.ReadAllBytes(path + ".png"))) return false;
                sheet = Build(source, layout);
                worldSize = Vector2.one * layout.worldSize;
                footOffset = layout.worldSize * (layout.frameSize - layout.baseline) / layout.frameSize;
                fullColor = layout.fullColor;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnitArt] {name}: {e.Message}; using the legacy sheet.");
                return false;
            }
            finally { Release(source); }
        }

        [Serializable] sealed class RuntimeLayout
        {
            public int version;
            public float worldSize, footOffset;
            public bool fullColor;
        }

        static bool TryLoadRuntime(string name, out Texture2D sheet, out Vector2 worldSize, out float footOffset, out bool fullColor)
        {
            sheet = null; worldSize = Vector2.zero; footOffset = 0f; fullColor = false;
            string path = Path.Combine(SpriteLibrary.ArtRoot, "Final", "units", name + "_detail_runtime");
            if (!ContentFiles.Exists(path + ".png") || !ContentFiles.Exists(path + ".json")) return false;
            Texture2D texture = null;
            try
            {
                var layout = JsonUtility.FromJson<RuntimeLayout>(ContentFiles.ReadAllText(path + ".json"));
                if (layout == null || layout.version != 1 || float.IsNaN(layout.worldSize) || float.IsInfinity(layout.worldSize) ||
                    layout.worldSize <= 0f || layout.worldSize > 4f || float.IsNaN(layout.footOffset) || float.IsInfinity(layout.footOffset))
                    return false;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(ContentFiles.ReadAllBytes(path + ".png")) || texture.width != 160 || texture.height != 240) return false;
                texture.name = "DetailedUnitSheet_" + name;
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                sheet = texture;
                worldSize = Vector2.one * layout.worldSize;
                footOffset = layout.footOffset;
                fullColor = layout.fullColor;
                texture = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnitArt] {name}: runtime atlas invalid ({e.Message}); rebuilding from source.");
                return false;
            }
            finally { Release(texture); }
        }

        public static Texture2D Build(Texture2D source, Layout layout)
        {
            if (layout == null || layout.version != 1 || layout.frameSize < 8 || layout.frameSize > 64 ||
                layout.baseline <= 0 || layout.baseline >= layout.frameSize || layout.scale <= 0f ||
                float.IsNaN(layout.scale) || float.IsInfinity(layout.scale) ||
                float.IsNaN(layout.worldSize) || float.IsInfinity(layout.worldSize) ||
                layout.worldSize <= 0 || layout.worldSize > 4 || layout.frames == null || layout.frames.Length != 24)
                throw new ArgumentException("Invalid detail-sheet layout.");
            int cell = layout.frameSize, width = cell * 4, height = cell * 6;
            var raw = source.GetPixels32();
            var pixels = new Color32[width * height];
            for (int index = 0; index < 24; index++)
            {
                var f = layout.frames[index];
                if (f == null || f.x < 0 || f.y < 0 || f.width <= 0 || f.height <= 0 ||
                    (long)f.x + f.width > source.width || (long)f.y + f.height > source.height)
                    throw new ArgumentException("Pose rectangle is outside the source image.");
                // Keep source fallback sampling identical to the offline atlas baker.
                // Mathf.RoundToInt uses midpoint-to-even, while the authored masks
                // are sampled with midpoint-away-from-zero (floor(value + 0.5)).
                int w = Mathf.Max(1, Mathf.FloorToInt(f.width * layout.scale + 0.5f));
                int h = Mathf.Max(1, Mathf.FloorToInt(f.height * layout.scale + 0.5f));
                if (w > cell - 2 || h > layout.baseline - 1)
                    throw new ArgumentException("Pose would be cropped; reduce the import scale.");
                var mask = Convert.FromBase64String(f.mask ?? "");
                if (mask.Length != w * h) throw new ArgumentException("Pose mask dimensions do not match.");
                int left = index % 4 * cell + (cell - w) / 2;
                int top = index / 4 * cell + layout.baseline - h;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x] == 0) continue;
                    int sx = f.x + Mathf.Min(f.width - 1, (int)((x + .5f) * f.width / w));
                    int sy = f.y + Mathf.Min(f.height - 1, (int)((y + .5f) * f.height / h));
                    Color32 color = raw[(source.height - 1 - sy) * source.width + sx];
                    if (color.a < 128) continue;
                    if (layout.fullColor) pixels[(height - 1 - top - y) * width + left + x] = new Color32(color.r, color.g, color.b, 255);
                    else
                    {
                        byte grey = (byte)(28 * Mathf.Clamp(Mathf.RoundToInt((color.r + color.g + color.b) / 84f), 1, 8));
                        pixels[(height - 1 - top - y) * width + left + x] = new Color32(grey, grey, grey, 255);
                    }
                }
            }
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "DetailedUnitSheet", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            result.SetPixels32(pixels);
            result.Apply();
            return result;
        }

        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
