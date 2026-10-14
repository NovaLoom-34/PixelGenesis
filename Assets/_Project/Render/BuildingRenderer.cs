using System.Collections.Generic;
using PG.Content;
using PG.Sim;
using PG.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 5 render: building sprites ({style}_{building}_{construction|complete|ruin}, EkC 6) in one mesh rebuilt when
    // CivWorld.Version changes, plus translucent city-colored zone tiles under everything placed on the map.
    // Sprites are gray-index (EkC 4.1) and go through the unit palette shader: indices 1-4 the style's material,
    // 5-6 the roof in the owner kingdom's colour, 7 the style trim, 8 warm window light.
    // Final *_color.png art bypasses the palette via UV1.y while retaining era and status tints.
    public sealed class BuildingRenderer : MonoBehaviour
    {
        public const int SortingOrder = SortingLayers.OrderBuildings, BorderSortingOrder = SortingLayers.OrderCityBorders, ShadowSortingOrder = SortingLayers.OrderBuildingShadows;
        public const byte BorderAlpha = 40;
        // Depth key sits half a tile above the sprite base so units at the door line stay in front.
        const float FootBias = 0.5f;
        // Compact visual scale; saved placement and navigation footprints stay intact.
        public const float VisualScale = 0.4f;
        static readonly string[] States = { "construction", "complete", "ruin" };
        static readonly Color32 BurnTint = new Color32(255, 150, 110, 255);
        static readonly Color WindowLight = new Color(1f, 0.87f, 0.55f);

        public Camera ViewCamera;

        CivWorld _civ;
        WorldMap _map;
        SpriteLibrary _sprites;
        Material _material, _borderMaterial;
        Texture2D _palette;
        Color32[] _styleColors; // [style * 3 + (accent, wall, trim)]
        float[] _roofMix;
        readonly Dictionary<long, int> _rows = new Dictionary<long, int>();
        readonly List<Vector2> _r = new List<Vector2>(2048);
        Mesh _mesh, _borderMesh, _shadowMesh;
        GameObject _root;
        Rect[] _uv;          // [style, def, state]
        Vector2[] _size;
        bool[] _fullColor;
        int _defs, _version = -1;
        readonly List<int> _order = new List<int>(256);
        readonly List<Vector3> _v = new List<Vector3>(2048);
        readonly List<Vector2> _t = new List<Vector2>(2048);
        readonly List<Color32> _c = new List<Color32>(2048);
        readonly List<int> _i = new List<int>(3072);

        public void Bind(SimWorld sim)
        {
            Unbind();
            _civ = sim.Civ;
            _map = sim.World;
            var db = sim.Content;
            _defs = db.Buildings.Count;
            int styles = db.BuildingStyles.Count;
            if (_sprites == null)
            {
                var ids = new List<string>(styles * _defs * 3);
                for (int s = 0; s < styles; s++)
                    for (int b = 0; b < _defs; b++)
                        foreach (var state in States) ids.Add(Key(db.BuildingStyles[s].ShortId, db.Buildings[b].ShortId, state));
                _sprites = SpriteLibrary.Load("buildings", ids);
                _material = new Material(Resources.Load<Shader>("PG_Unit")) { name = "Buildings" };
                _material.SetTexture("_MainTex", _sprites.Atlas);
                _material.SetFloat("_ZWrite", 1f);
                _borderMaterial = new Material(Resources.Load<Shader>("PG_Sprite")) { name = "CityBorders", mainTexture = Texture2D.whiteTexture };
                _borderMaterial.SetFloat("_Soft", 1f);
            }
            _styleColors = new Color32[styles * 3];
            _roofMix = new float[styles];
            for (int s = 0; s < styles; s++) _roofMix[s] = db.BuildingStyles[s].RoofMix;
            for (int s = 0; s < styles; s++)
                for (int c = 0; c < 3; c++) _styleColors[s * 3 + c] = db.BuildingStyles[s].ParsedColors[c];
            _uv = new Rect[styles * _defs * 3];
            _size = new Vector2[_uv.Length];
            _fullColor = new bool[_uv.Length];
            for (int s = 0; s < styles; s++)
                for (int b = 0; b < _defs; b++)
                    for (int st = 0; st < 3; st++)
                    {
                        _sprites.TryGet(Key(db.BuildingStyles[s].ShortId, db.Buildings[b].ShortId, States[st]), out var e);
                        int k = (s * _defs + b) * 3 + st;
                        _uv[k] = e.Uv;
                        _size[k] = e.Size;
                        _fullColor[k] = e.FullColor;
                    }

            _root = new GameObject("BuildingMeshes");
            _root.transform.SetParent(transform, false);
            _mesh = MakeLayer("Buildings", _material, SortingLayers.Buildings, SortingOrder);
            _borderMesh = MakeLayer("CityBorders", _borderMaterial, SortingLayers.Terrain, BorderSortingOrder);
            _shadowMesh = MakeLayer("BuildingShadows", _borderMaterial, SortingLayers.Decals, ShadowSortingOrder);
            _version = -1;
        }

        static string Key(string style, string building, string state) => "b." + style + "_" + building + "_" + state;

        Mesh MakeLayer(string name, Material material, string layerName, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingLayerName = layerName;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mesh;
        }

        public void Unbind()
        {
            _civ = null;
            _map = null;
            Release(_mesh);
            Release(_borderMesh);
            Release(_shadowMesh);
            if (_root != null) Release(_root);
            _root = null;
        }

        public void FlushAll()
        {
            if (_civ != null) Rebuild();
        }

        void LateUpdate()
        {
            if (_civ == null || _civ.Version == _version) return;
            Rebuild();
        }

        void Rebuild()
        {
            _version = _civ.Version;
            BuildBuildings();
            BuildShadows();
            BuildBorders();
        }

        void BuildShadows()
        {
            // A single layer below buildings and vegetation. Geometry only changes with Civ.Version.
            // Two rings give a small contact core and a feathered edge without extra textures or objects.
            const int segments = 12;
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            foreach (int b in _order)
            {
                var d = _civ.Buildings[b];
                float insetX = d.W * (1f - VisualScale) * .5f;
                float insetY = d.H * (1f - VisualScale) * .5f;
                float cx = d.Origin.x + insetX + d.W * 0.55f * VisualScale;
                float cy = d.Origin.y + insetY + d.H * 0.12f * VisualScale;
                float rx = d.W * 0.55f * VisualScale, ry = d.H * 0.22f * VisualScale;
                byte alpha = (d.Flags & BuildingFlags.Ruin) != 0 ? (byte)28 : (byte)55;
                int start = _v.Count;
                _v.Add(new Vector3(cx, cy, 0));
                _t.Add(new Vector2(0.5f, 0.5f));
                _c.Add(new Color32(21, 25, 30, alpha));
                for (int ring = 0; ring < 2; ring++)
                    for (int p = 0; p < segments; p++)
                    {
                        float angle = p * (Mathf.PI * 2f / segments);
                        float radius = ring == 0 ? 0.7f : 1f;
                        _v.Add(new Vector3(cx + Mathf.Cos(angle) * rx * radius, cy + Mathf.Sin(angle) * ry * radius, 0));
                        _t.Add(new Vector2(0.5f, 0.5f));
                        _c.Add(new Color32(21, 25, 30, ring == 0 ? alpha : (byte)0));
                    }
                for (int p = 0; p < segments; p++)
                {
                    int a = start + 1 + p, next = start + 1 + (p + 1) % segments;
                    _i.Add(start); _i.Add(next); _i.Add(a);
                    _i.Add(a); _i.Add(next); _i.Add(next + segments);
                    _i.Add(a); _i.Add(next + segments); _i.Add(a + segments);
                }
            }
            Apply(_shadowMesh);
        }

        void BuildBuildings()
        {
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear(); _order.Clear(); _r.Clear(); _rows.Clear();
            var buildings = _civ.Buildings;
            for (int b = 0; b < buildings.Length; b++)
                if (buildings[b].Alive) _order.Add(b);
            // north first so buildings further south overlap the ones behind them
            _order.Sort((a, b) => buildings[b].Origin.y != buildings[a].Origin.y ? buildings[b].Origin.y.CompareTo(buildings[a].Origin.y) : a.CompareTo(b));
            var white = new Color32(255, 255, 255, 255);
            foreach (int b in _order)
            {
                var d = buildings[b];
                int state = (d.Flags & BuildingFlags.Ruin) != 0 ? 2 : d.BuildProgress >= 1f ? 1 : 0;
                int k = (d.Style * _defs + d.Def) * 3 + state;
                if (k < 0 || k >= _uv.Length) continue;
                Rect uv = _uv[k];
                Vector2 size = _size[k];
                float w = d.W * VisualScale, h = size.y * (w / Mathf.Max(1f, size.x));
                float left = d.Origin.x + (d.W - w) * .5f;
                float bottom = d.Origin.y + d.H * (1f - VisualScale) * .5f;
                float z = SortingLayers.DepthZ(bottom + FootBias);
                var tint = (d.Flags & BuildingFlags.Burning) != 0 ? BurnTint : white;
                var row = new Vector2(RowOf(d.Style, RoofColor(d.City)), _fullColor[k] ? 1f : 0f);
                // Cut the atlas image on the 3x3-tile building cell grid. Each piece has its own UV rectangle
                // and geometry; placement, tint and state remain those of the original building.
                // Rows are emitted north to south like building draw order.
                const float cell = BuildingDef.CellTiles;
                for (int ty = Mathf.CeilToInt(h / cell) - 1; ty >= 0; ty--)
                    for (int tx = 0; tx < Mathf.CeilToInt(w / cell); tx++)
                    {
                        float x0 = tx * cell, x1 = Mathf.Min(w, (tx + 1f) * cell);
                        float y0 = ty * cell, y1 = Mathf.Min(h, (ty + 1f) * cell);
                        if (x1 <= x0 || y1 <= y0) continue;
                        int v = _v.Count;
                        _v.Add(new Vector3(left + x0, bottom + y0, z));
                        _v.Add(new Vector3(left + x1, bottom + y0, z));
                        _v.Add(new Vector3(left + x1, bottom + y1, z));
                        _v.Add(new Vector3(left + x0, bottom + y1, z));
                        float u0 = uv.xMin + uv.width * (x0 / w), u1 = uv.xMin + uv.width * (x1 / w);
                        float v0 = uv.yMin + uv.height * (y0 / h), v1 = uv.yMin + uv.height * (y1 / h);
                        _t.Add(new Vector2(u0, v0)); _t.Add(new Vector2(u1, v0));
                        _t.Add(new Vector2(u1, v1)); _t.Add(new Vector2(u0, v1));
                        for (int j = 0; j < 4; j++) { _c.Add(tint); _r.Add(row); }
                        _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
                        _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
                    }
            }
            WritePalette();
            Apply(_mesh);
            float rows = Mathf.Max(1, _palette.height);
            for (int k = 0; k < _r.Count; k++) _r[k] = new Vector2((_r[k].x + 0.5f) / rows, _r[k].y);
            _mesh.SetUVs(1, _r);
        }

        uint RoofColor(int city)
        {
            if (city < 0 || city >= _civ.CityCount) return 0;
            int kingdom = _civ.KingdomOf(city);
            var meta = _civ.Meta;
            return meta != null && meta.KingdomAlive(kingdom) ? meta.Kingdoms.Headers[kingdom].Color : _civ.Cities.Headers[city].Color;
        }

        int RowOf(int style, uint roof)
        {
            long key = ((long)style << 32) | roof;
            if (!_rows.TryGetValue(key, out int row)) _rows[key] = row = _rows.Count;
            return row;
        }

        void WritePalette()
        {
            int need = Mathf.Max(1, _rows.Count);
            if (_palette == null || _palette.height < need)
            {
                Release(_palette);
                _palette = new Texture2D(8, Mathf.NextPowerOfTwo(need), TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BuildingPalette" };
                _material.SetTexture("_Palette", _palette);
            }
            foreach (var kv in _rows)
            {
                int style = (int)(kv.Key >> 32);
                uint rgba = (uint)(kv.Key & 0xFFFFFFFF);
                Color accent = _styleColors[style * 3], wall = _styleColors[style * 3 + 1], trim = _styleColors[style * 3 + 2];
                Color roof = rgba == 0 ? accent : Color.Lerp(new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), 255), accent, _roofMix[style]);
                int y = kv.Value;
                _palette.SetPixel(0, y, wall * 0.3f);                         // 1 outline
                _palette.SetPixel(1, y, wall * 0.62f);                        // 2 shadow / beams
                _palette.SetPixel(2, y, wall * 0.85f);                        // 3 wall
                _palette.SetPixel(3, y, Color.Lerp(wall, Color.white, 0.18f)); // 4 lit wall
                _palette.SetPixel(4, y, roof * 0.62f);                        // 5 roof shadow
                _palette.SetPixel(5, y, roof);                                // 6 roof
                _palette.SetPixel(6, y, trim);                                // 7 trim
                _palette.SetPixel(7, y, WindowLight);                         // 8 light
            }
            _palette.Apply(false);
        }

        void BuildBorders()
        {
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            var zones = _map.Zones;
            for (int z = 0; z < zones.Length; z++)
            {
                int city = zones[z].OwnerCity;
                if (city < 0 || city >= _civ.CityCount) continue;
                int kingdom = _civ.KingdomOf(city);
                var meta = _civ.Meta;
                uint rgba = meta != null && meta.KingdomAlive(kingdom) ? meta.Kingdoms.Headers[kingdom].Color : _civ.Cities.Headers[city].Color; // Bölüm 6.4 kingdom borders
                var color = new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), BorderAlpha);
                float x = (z % _map.ZonesX) * WorldMap.ZoneSize, y = (z / _map.ZonesX) * WorldMap.ZoneSize;
                int v = _v.Count;
                _v.Add(new Vector3(x, y, 0f));
                _v.Add(new Vector3(x + WorldMap.ZoneSize, y, 0f));
                _v.Add(new Vector3(x + WorldMap.ZoneSize, y + WorldMap.ZoneSize, 0f));
                _v.Add(new Vector3(x, y + WorldMap.ZoneSize, 0f));
                for (int k = 0; k < 4; k++) { _t.Add(new Vector2(0.5f, 0.5f)); _c.Add(color); }
                _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
                _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
            }
            Apply(_borderMesh);
        }

        void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = _v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _t);
            mesh.SetColors(_c);
            mesh.SetTriangles(_i, 0);
            mesh.bounds = new Bounds(new Vector3(_map.Width * 0.5f, _map.Height * 0.5f, 0f), new Vector3(_map.Width + 32f, _map.Height + 32f, 1f));
        }

        static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            Unbind();
            _sprites?.Dispose();
            Release(_material);
            Release(_borderMaterial);
            Release(_palette);
        }
    }
}
