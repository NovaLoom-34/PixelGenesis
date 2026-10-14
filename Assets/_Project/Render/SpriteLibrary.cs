using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PG.Render
{
    // Loads sprites by content id from StreamingAssets/Art/{Final,Placeholder}/<category>/<name>.png (EkC; 1 pixel = 1 tile)
    // and packs them into one point-filtered atlas. Missing art gets a small generated block so the game still runs.
    public sealed class SpriteLibrary
    {
        // The art pipeline draws sprites small and chunky (EkC 5.1: a size-2 human is 4x7 px); at a literal 1px = 1 tile
        // read that draws a person several tiles tall and trees so large adjacent ones merge into an unreadable blob.
        // Divide raw pixels by this to get a person roughly 1 tile tall and trees a couple of tiles wide (DECISIONS #87).
        public const float PixelsPerTile = 5f;

        public struct Entry
        {
            public Rect Uv;
            public Vector2 Size; // world tiles (raw sprite pixels / PixelsPerTile)
            public bool FullColor; // Final _color.png art bypasses the index palette where applicable.
        }

        public Texture2D Atlas { get; private set; }
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public static string ArtRoot => Path.Combine(Application.streamingAssetsPath, "Art");

        // ids like "feat.tree_oak" -> <category>/tree_oak.png
        public static SpriteLibrary Load(string category, IReadOnlyList<string> ids)
        {
            var lib = new SpriteLibrary();
            var textures = new Texture2D[ids.Count];
            var fullColor = new bool[ids.Count];
            var buildingSheets = new Dictionary<string, Texture2D>();
            var featureArt = category == "features" ? LoadFeatureResourceAtlas() : null;
            for (int i = 0; i < ids.Count; i++) textures[i] = LoadOne(category, ids[i], out fullColor[i], buildingSheets, featureArt);
            foreach (var source in buildingSheets.Values) if (source != null) Object.DestroyImmediate(source);
            if (featureArt?.Texture != null) Object.DestroyImmediate(featureArt.Texture);

            lib.Atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = category + "Atlas" };
            var rects = lib.Atlas.PackTextures(textures, 1, 2048, false);
            lib.Atlas.filterMode = FilterMode.Point;
            for (int i = 0; i < ids.Count; i++)
            {
                lib._entries[ids[i]] = new Entry { Uv = rects[i], Size = new Vector2(textures[i].width, textures[i].height) / PixelsPerTile, FullColor = fullColor[i] };
                Object.DestroyImmediate(textures[i]);
            }
            return lib;
        }

        public bool TryGet(string id, out Entry entry) => _entries.TryGetValue(id, out entry);

        static Texture2D LoadOne(string category, string id, out bool fullColor, Dictionary<string, Texture2D> buildingSheets,
            FeatureResourceAtlas featureArt)
        {
            fullColor = false;
            string name = id.Substring(id.IndexOf('.') + 1);
            if (category == "buildings" || category == "features")
            {
                string colorPath = Path.Combine(ArtRoot, "Final", category, name + "_color.png");
                if (PG.Content.ContentFiles.Exists(colorPath))
                {
                    var color = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (color.LoadImage(PG.Content.ContentFiles.ReadAllBytes(colorPath)))
                    {
                        fullColor = true;
                        // Keep source art intact; sample to a small pixel sprite before atlas packing.
                        // Large source images must not force PackTextures to shrink the other buildings.
                        // Buildings keep their silhouette detail; dense world features must remain
                        // small enough to read as separate objects rather than cover nearby tiles.
                        int xMin = 0, yMin = 0, xMax = color.width - 1, yMax = color.height - 1;
                        if (category == "features")
                        {
                            // Generated sources have different transparent margins. Size the visible
                            // object, so a shrub and tree use predictable map tiles.
                            var pixels = color.GetPixels32();
                            int loX = color.width, loY = color.height, hiX = -1, hiY = -1;
                            for (int sy = 0; sy < color.height; sy++)
                                for (int sx = 0; sx < color.width; sx++)
                                    if (pixels[sy * color.width + sx].a >= 128)
                                    {
                                        loX = Mathf.Min(loX, sx); loY = Mathf.Min(loY, sy);
                                        hiX = Mathf.Max(hiX, sx); hiY = Mathf.Max(hiY, sy);
                                    }
                            if (hiX >= loX && hiY >= loY) { xMin = loX; yMin = loY; xMax = hiX; yMax = hiY; }
                        }
                        int sourceW = xMax - xMin + 1, sourceH = yMax - yMin + 1;
                        bool smallPlant = name == "berry_bush" || name == "grass_tuft" || name == "flower";
                        float maxEdge = category == "features" ? (name == "rock" ? 9f : smallPlant ? 8f : 15f) : 64f;
                        float scale = Mathf.Min(1f, maxEdge / Mathf.Max(sourceW, sourceH));
                        int w = Mathf.Max(1, Mathf.RoundToInt(sourceW * scale));
                        int h = Mathf.Max(1, Mathf.RoundToInt(sourceH * scale));
                        var small = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                                small.SetPixel(x, y, color.GetPixel(xMin + Mathf.Min(sourceW - 1, (int)((x + 0.5f) * sourceW / w)), yMin + Mathf.Min(sourceH - 1, (int)((y + 0.5f) * sourceH / h))));
                        small.Apply();
                        Object.DestroyImmediate(color);
                        return small;
                    }
                    Object.DestroyImmediate(color);
                }
            }
            if (category == "buildings" && TryBuildingAtlas(name, buildingSheets, out var building))
            {
                fullColor = true;
                return building;
            }
            if (category == "features" && featureArt != null && TryFeatureResource(name, featureArt, out var resource))
            {
                fullColor = true;
                return resource;
            }
            foreach (string tier in new[] { "Final", "Placeholder" })
            {
                string path = Path.Combine(ArtRoot, tier, category, name + ".png");
                if (!PG.Content.ContentFiles.Exists(path)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                if (tex.LoadImage(PG.Content.ContentFiles.ReadAllBytes(path))) return tex;
                Object.DestroyImmediate(tex);
            }
            return Fallback(id);
        }

        sealed class FeatureResourceAtlas
        {
            public Texture2D Texture;
            public Color32[] Pixels;
            public int Columns, Rows;
            public string[] Sprites;
        }

        [System.Serializable] sealed class FeatureResourceLayout
        {
            public int version, columns, rows;
            public string[] sprites;
        }

        static FeatureResourceAtlas LoadFeatureResourceAtlas()
        {
            string root = Path.Combine(ArtRoot, "Final", "features");
            string imagePath = Path.Combine(root, "feature_resources_atlas.png");
            string layoutPath = Path.Combine(root, "feature_resources_atlas.json");
            if (!PG.Content.ContentFiles.Exists(imagePath) || !PG.Content.ContentFiles.Exists(layoutPath)) return null;

            Texture2D texture = null;
            try
            {
                var layout = JsonUtility.FromJson<FeatureResourceLayout>(PG.Content.ContentFiles.ReadAllText(layoutPath));
                if (layout == null || layout.version != 1 || layout.columns < 1 || layout.rows < 1 ||
                    layout.columns > 8 || layout.rows > 8 || layout.sprites == null || layout.sprites.Length != layout.columns * layout.rows)
                    return null;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                if (!texture.LoadImage(PG.Content.ContentFiles.ReadAllBytes(imagePath)) || texture.width < layout.columns * 16 || texture.height < layout.rows * 16)
                {
                    Object.DestroyImmediate(texture);
                    return null;
                }
                texture.name = "FeatureResourceAtlas";
                return new FeatureResourceAtlas
                {
                    Texture = texture,
                    Pixels = texture.GetPixels32(),
                    Columns = layout.columns,
                    Rows = layout.rows,
                    Sprites = layout.sprites,
                };
            }
            catch (System.Exception e)
            {
                if (texture != null) Object.DestroyImmediate(texture);
                Debug.LogWarning("[FeatureArt] Resource atlas invalid; using existing artwork: " + e.Message);
                return null;
            }
        }

        static bool TryFeatureResource(string name, FeatureResourceAtlas atlas, out Texture2D result)
        {
            result = null;
            int index = -1;
            for (int i = 0; i < atlas.Sprites.Length; i++)
                if (atlas.Sprites[i] == name) { index = i; break; }
            if (index < 0) return false;

            var source = atlas.Texture;
            int column = index % atlas.Columns, row = index / atlas.Columns;
            int x0 = column * source.width / atlas.Columns, x1 = (column + 1) * source.width / atlas.Columns;
            int topY0 = row * source.height / atlas.Rows, topY1 = (row + 1) * source.height / atlas.Rows;
            var pixels = atlas.Pixels;
            int minX = x1, maxX = x0 - 1, minY = topY1, maxY = topY0 - 1;
            for (int y = topY0; y < topY1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int sourceIndex = (source.height - 1 - y) * source.width + x;
                    if (pixels[sourceIndex].a < 32) continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            if (maxX < minX || maxY < minY) return false;

            int sourceW = maxX - minX + 1, sourceH = maxY - minY + 1;
            bool smallPlant = name == "cactus" || name == "coral" || name == "reed" || name == "herb_patch" ||
                              name == "small_mushroom" || name == "garlic_plant" || name == "wheat_crop";
            bool smallOre = name.StartsWith("ore_", System.StringComparison.Ordinal) || name == "salt_flat" || name == "bones_pile";
            int maxEdge = smallPlant ? 8 : smallOre ? 9 : 15;
            float scale = Mathf.Min(1f, maxEdge / (float)Mathf.Max(sourceW, sourceH));
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceW * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceH * scale));
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "Feature_" + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cropped = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = minX + Mathf.Min(sourceW - 1, (int)((x + 0.5f) * sourceW / width));
                    int sy = minY + Mathf.Min(sourceH - 1, (int)((y + 0.5f) * sourceH / height));
                    cropped[y * width + x] = pixels[(source.height - 1 - sy) * source.width + sx];
                }
            output.SetPixels32(cropped);
            output.Apply(false, false);
            result = output;
            return true;
        }

        // Authored 3x3 sheets: architecture = house/watchtower/smithy;
        // settlement = hall_1/granary/market; civic = hall_2/hall_3/storage;
        // industry = farm_shed/mine/lumber_camp; food = bakery/pasture/beehive;
        // founding = bonfire/tent/hut; publicworks = well/windmill/inn;
        // advanced = manor/stable/barracks; naval = fishing_hut/docks/shipyard;
        // culture = library/school/temple; religion = grand_temple/healers_house/graveyard;
        // monuments = statue_god/monument/pyre; fortifications = wall/gate;
        // habitats = nest/hive/ruins.
        // Rows are construction/complete/ruin.
        // Individual final sprites still take precedence over either sheet.
        static bool TryBuildingAtlas(string name, Dictionary<string, Texture2D> cache, out Texture2D result)
        {
            result = null;
            string style = null;
            foreach (string candidate in new[] { "human", "orc", "dwarf", "elf", "beast", "insect" })
                if (name.StartsWith(candidate + "_", StringComparison.Ordinal)) { style = candidate; break; }
            if (style == null) return false;

            string remainder = name.Substring(style.Length + 1);
            int stateSeparator = remainder.LastIndexOf('_');
            if (stateSeparator <= 0) return false;
            string state = remainder.Substring(stateSeparator + 1);
            string building = remainder.Substring(0, stateSeparator);
            int row = state == "construction" ? 0 : state == "complete" ? 1 : state == "ruin" ? 2 : -1;
            bool settlement = building == "hall_1" || building == "granary" || building == "market";
            bool civic = building == "hall_2" || building == "hall_3" || building == "storage";
            bool industry = building == "farm_shed" || building == "mine" || building == "lumber_camp";
            bool food = building == "bakery" || building == "pasture" || building == "beehive";
            bool founding = building == "bonfire" || building == "tent" || building == "hut";
            bool publicWorks = building == "well" || building == "windmill" || building == "inn";
            bool advanced = building == "manor" || building == "stable" || building == "barracks";
            bool naval = building == "fishing_hut" || building == "docks" || building == "shipyard";
            bool culture = building == "library" || building == "school" || building == "temple";
            bool religion = building == "grand_temple" || building == "healers_house" || building == "graveyard";
            bool monuments = building == "statue_god" || building == "monument" || building == "pyre";
            bool fortifications = building == "wall" || building == "gate";
            bool habitats = building == "nest" || building == "hive" || building == "ruins";
            int column = settlement
                ? building == "hall_1" ? 0 : building == "granary" ? 1 : 2
                : civic
                    ? building == "hall_2" ? 0 : building == "hall_3" ? 1 : 2
                    : industry
                        ? building == "farm_shed" ? 0 : building == "mine" ? 1 : 2
                        : food
                            ? building == "bakery" ? 0 : building == "pasture" ? 1 : 2
                            : founding
                                ? building == "bonfire" ? 0 : building == "tent" ? 1 : 2
                                : publicWorks
                                    ? building == "well" ? 0 : building == "windmill" ? 1 : 2
                                    : advanced
                                        ? building == "manor" ? 0 : building == "stable" ? 1 : 2
                                        : naval
                                            ? building == "fishing_hut" ? 0 : building == "docks" ? 1 : 2
                                            : culture
                                                ? building == "library" ? 0 : building == "school" ? 1 : 2
                                                : religion
                                                    ? building == "grand_temple" ? 0 : building == "healers_house" ? 1 : 2
                                                    : monuments
                                                        ? building == "statue_god" ? 0 : building == "monument" ? 1 : 2
                                                        : fortifications
                                                            ? building == "wall" ? 0 : 1
                                                            : habitats
                                                                ? building == "nest" ? 0 : building == "hive" ? 1 : 2
                                                                : building == "house" ? 0 : building == "watchtower" ? 1 : building == "smithy" ? 2 : -1;
            if (column < 0 || row < 0) return false;
            string sheet = settlement ? "settlement" : civic ? "civic" : industry ? "industry" : food ? "food" : founding ? "founding" : publicWorks ? "publicworks" : advanced ? "advanced" : naval ? "naval" : culture ? "culture" : religion ? "religion" : monuments ? "monuments" : fortifications ? "fortifications" : habitats ? "habitats" : "architecture";
            string cacheKey = style + "_" + sheet;
            if (!cache.TryGetValue(cacheKey, out var source))
            {
                string path = Path.Combine(ArtRoot, "Final", "buildings", cacheKey + ".png");
                if (PG.Content.ContentFiles.Exists(path))
                {
                    source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!source.LoadImage(PG.Content.ContentFiles.ReadAllBytes(path))) { Object.DestroyImmediate(source); source = null; }
                }
                cache[cacheKey] = source;
            }
            if (source == null || source.width < 192 || source.height < 192) return false;
            string metadata = Path.Combine(ArtRoot, "Final", "buildings", cacheKey + ".json");
            if (!PG.Content.ContentFiles.Exists(metadata)) return false;
            ArchitectureLayout layout;
            try { layout = JsonUtility.FromJson<ArchitectureLayout>(PG.Content.ContentFiles.ReadAllText(metadata)); }
            catch (System.Exception) { return false; }
            int columns = layout?.columns > 0 ? layout.columns : 3;
            if (layout?.frames == null || column >= columns || layout.frames.Length != columns * 3) return false;
            var f = layout.frames[row * columns + column];
            if (f.outputWidth < 1 || f.outputWidth > 64 || f.outputHeight < 1 || f.outputHeight > 64 ||
                f.sampleWidth < 1 || f.sampleWidth > f.outputWidth || f.sampleHeight < 1 || f.sampleHeight > f.outputHeight ||
                f.x < 0 || f.y < 0 || f.width < 1 || f.height < 1 || f.x + f.width > source.width || f.y + f.height > source.height) return false;
            byte[] mask;
            try { mask = System.Convert.FromBase64String(f.mask); }
            catch (System.Exception) { return false; }
            if (mask.Length != f.sampleWidth * f.sampleHeight) return false;
            result = new Texture2D(f.outputWidth, f.outputHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            result.SetPixels32(new Color32[f.outputWidth * f.outputHeight]);
            int left = (f.outputWidth - f.sampleWidth) / 2;
            for (int y = 0; y < f.sampleHeight; y++)
                for (int x = 0; x < f.sampleWidth; x++)
                {
                    if (mask[y * f.sampleWidth + x] == 0) continue;
                    int sx = f.x + (int)((x + .5f) * f.width / f.sampleWidth);
                    int sy = f.y + (int)((y + .5f) * f.height / f.sampleHeight);
                    result.SetPixel(left + x, 2 + f.sampleHeight - 1 - y, source.GetPixel(sx, source.height - 1 - sy));
                }
            result.Apply();
            return true;
        }

        [System.Serializable] sealed class ArchitectureLayout { public int columns; public ArchitectureFrame[] frames; }
        [System.Serializable] sealed class ArchitectureFrame
        {
            public int x, y, width, height, outputWidth, outputHeight, sampleWidth, sampleHeight;
            public string mask;
        }

        static Texture2D Fallback(string id)
        {
            var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            uint h = (uint)id.GetHashCode();
            var c = new Color32((byte)(80 + (h & 127)), (byte)(80 + ((h >> 8) & 127)), (byte)(60 + ((h >> 16) & 63)), 255);
            var pixels = new Color32[9];
            for (int i = 0; i < 9; i++) pixels[i] = (i == 0 || i == 2) ? new Color32(0, 0, 0, 0) : c;
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public void Dispose()
        {
            if (Atlas == null) return;
            if (Application.isPlaying) Object.Destroy(Atlas);
            else Object.DestroyImmediate(Atlas);
            Atlas = null;
        }
    }
}
