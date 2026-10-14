using System.Collections.Generic;
using PG.Powers;
using PG.Render;
using UnityEngine;

namespace PG.UI
{
    // Bölüm 1.13.4: outline of the tiles the brush will touch, as a thick quad strip plus a gently
    // pulsing fill (no LineRenderer). Tint follows the selected power's tab (UiRoot.SetTint).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BrushCursor : MonoBehaviour
    {
        public const string ShaderResource = "PG_Line";
        public Color OutlineColor = new Color(1f, 1f, 1f, 0.9f);
        const float Thickness = 0.14f; // tile fractions, centred on the boundary edge

        Mesh _mesh;
        Material _material;
        int _radius = -1;
        BrushShape _shape;
        int _fillStart = -1;
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _indices = new List<int>();

        void Awake()
        {
            _mesh = new Mesh { name = "BrushCursor" };
            _mesh.MarkDynamic();
            _material = new Material(Resources.Load<Shader>(ShaderResource)) { name = "BrushCursor" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.sortingLayerName = SortingLayers.WorldUI;
            mr.sortingOrder = 90; // above the map and world UI
        }

        void Update()
        {
            if (_fillStart < 0 || !gameObject.activeInHierarchy) return;
            float pulse = 22 + Mathf.Sin(Time.unscaledTime * 4f) * 8f;
            var fill = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, pulse / 255f);
            for (int i = _fillStart; i < _colors.Count; i++) _colors[i] = fill;
            _mesh.SetColors(_colors);
        }

        public void SetTint(Color c)
        {
            if (c == OutlineColor) return;
            OutlineColor = c;
            if (_radius >= 0) Rebuild(_radius, _shape);
        }

        public void Show(Vector2Int tile, int radius, BrushShape shape)
        {
            if (radius != _radius || shape != _shape) Rebuild(radius, shape);
            transform.position = new Vector3(tile.x, tile.y, -1f);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // Boundary edges of the brush tile set, in tile-local coordinates (tile (0,0) spans 0..1),
        // then one translucent quad per tile for the fill (vertices after _fillStart pulse in Update).
        void Rebuild(int radius, BrushShape shape)
        {
            _radius = radius;
            _shape = shape;
            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (!Brushes.Contains(shape, radius, dx, dy)) continue;
                    if (!Inside(dx - 1, dy)) Segment(dx, dy, dx, dy + 1);
                    if (!Inside(dx + 1, dy)) Segment(dx + 1, dy, dx + 1, dy + 1);
                    if (!Inside(dx, dy - 1)) Segment(dx, dy, dx + 1, dy);
                    if (!Inside(dx, dy + 1)) Segment(dx, dy + 1, dx + 1, dy + 1);
                }
            }
            _fillStart = _vertices.Count;
            var fill = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 22f / 255f);
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (Brushes.Contains(shape, radius, dx, dy))
                        Quad(dx, dy, dx + 1, dy + 1, fill);
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_indices, 0);
            _mesh.RecalculateBounds();
        }

        bool Inside(int dx, int dy) =>
            dx >= -_radius && dx <= _radius && dy >= -_radius && dy <= _radius && Brushes.Contains(_shape, _radius, dx, dy);

        void Segment(float x0, float y0, float x1, float y1)
        {
            bool vertical = Mathf.Approximately(x0, x1);
            float hx = vertical ? Thickness * 0.5f : 0f, hy = vertical ? 0f : Thickness * 0.5f;
            Quad(x0 - hx, y0 - hy, x1 + hx, y1 + hy, OutlineColor);
        }

        void Quad(float x0, float y0, float x1, float y1, Color color)
        {
            int v = _vertices.Count;
            _vertices.Add(new Vector3(x0, y0, 0f));
            _vertices.Add(new Vector3(x1, y0, 0f));
            _vertices.Add(new Vector3(x1, y1, 0f));
            _vertices.Add(new Vector3(x0, y1, 0f));
            _colors.Add(color); _colors.Add(color); _colors.Add(color); _colors.Add(color);
            _indices.Add(v); _indices.Add(v + 2); _indices.Add(v + 1);
            _indices.Add(v); _indices.Add(v + 3); _indices.Add(v + 2);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
