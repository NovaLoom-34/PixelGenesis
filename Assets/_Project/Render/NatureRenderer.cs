using System.Collections.Generic;
using PG.Core;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 2.7 / 2.12 render: cloud blobs (layer 60) with ground shadows (layer 29), and the global era tint.
    public sealed class NatureRenderer : MonoBehaviour
    {
        public const int CloudOrder = SortingLayers.OrderClouds, ShadowOrder = SortingLayers.OrderCloudShadow;
        public const int BlobVariants = 3, BlobW = 24, BlobH = 12;
        static readonly int EraTintId = Shader.PropertyToID("_EraTint");
        static readonly int DayLightId = Shader.PropertyToID("_DayLight");
        static readonly int SeasonTintId = Shader.PropertyToID("_SeasonTint");
        static readonly int WindDirId = Shader.PropertyToID("_WindDir");

        [Range(0f, 1f)] public float EraTintStrength = 0.35f;
        public float CloudAlpha = 0.8f, ShadowAlpha = 0.22f;
        public Vector2 ShadowOffset = new Vector2(3f, -3f);
        public float HideAboveOrthoSize = 160f;
        public Camera ViewCamera;
        public GameClock ViewClock; // Bölüm 11: the render-only day/night cycle reads the clock (never writes it)

        // Render-only atmosphere switches (set from the settings window; defaults keep the old look).
        public static bool DayNightEnabled = true;
        public static int DayNightMonths = 8; // one light cycle over this many months
        public static int PerformanceQuality = 1; // 0 = low, 1 = balanced, 2 = high

        NatureState _nature;
        Texture2D _blobs;
        Material _material;
        Mesh _cloudMesh, _shadowMesh, _weatherMesh, _smokeMesh;
        MeshRenderer _cloudRenderer, _shadowRenderer, _weatherRenderer, _smokeRenderer;
        readonly List<Vector3> _v = new List<Vector3>(256);
        readonly List<Vector2> _t = new List<Vector2>(256);
        readonly List<Color32> _c = new List<Color32>(256);
        readonly List<int> _i = new List<int>(384);

        public void Bind(NatureState nature)
        {
            _nature = nature;
            Shader.SetGlobalColor(DayLightId, Color.white);
            if (_material != null) return;
            _blobs = MakeBlobs();
            _material = new Material(Resources.Load<Shader>("PG_Sprite")) { name = "Clouds", mainTexture = _blobs };
            _material.SetFloat("_Soft", 1f);
            _cloudMesh = new Mesh { name = "Clouds" };
            _shadowMesh = new Mesh { name = "CloudShadows" };
            _weatherMesh = new Mesh { name = "Weather" };
            _smokeMesh = new Mesh { name = "BurningSmoke" };
            _cloudRenderer = MakeLayer("Clouds", _cloudMesh, SortingLayers.Weather, CloudOrder, -1f);
            _shadowRenderer = MakeLayer("CloudShadows", _shadowMesh, SortingLayers.Decals, ShadowOrder, 0.05f);
            _weatherRenderer = MakeLayer("WeatherParticles", _weatherMesh, SortingLayers.Weather, 45, -0.5f);
            _smokeRenderer = MakeLayer("BurningSmoke", _smokeMesh, SortingLayers.Weather, 44, -0.5f);
        }

        public void Unbind()
        {
            _nature = null;
            Shader.SetGlobalColor(EraTintId, Color.white);
            Shader.SetGlobalColor(DayLightId, Color.white);
            Shader.SetGlobalColor(SeasonTintId, Color.white);
            Shader.SetGlobalVector(WindDirId, Vector4.zero);
        }

        MeshRenderer MakeLayer(string name, Mesh mesh, string layerName, int order, float localZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, localZ);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.sortingLayerName = layerName;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        void LateUpdate()
        {
            if (_nature == null) return;
            UpdateTint();

            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            bool show = cam == null || !cam.orthographic || cam.orthographicSize <= HideAboveOrthoSize;
            int quality = Mathf.Clamp(PerformanceQuality, 0, 2);
            _cloudRenderer.enabled = show;
            _shadowRenderer.enabled = show && quality > 0;
            _weatherRenderer.enabled = _smokeRenderer.enabled = show && quality > 0;
            if (!show) return;
            Build(_cloudMesh, Vector2.zero, CloudAlpha, false);
            if (quality == 0)
            {
                _shadowMesh.Clear(); _weatherMesh.Clear(); _smokeMesh.Clear();
            }
            else
            {
                Build(_shadowMesh, ShadowOffset, ShadowAlpha, true);
                BuildWeather(cam, quality);
                BuildBurningSmoke(cam, quality);
            }
        }

        // Bölüm 11 (G9): rain streaks and snow flakes under their clouds, and (G8) thin smoke
        // rising from burning tiles. Stateless — everything derives from the clock and a hash —
        // so the meshes rebuild every frame with zero allocation.
        void BuildWeather(Camera cam, int quality)
        {
            float t = Time.unscaledTime;
            var content = _nature.Content;
            float cx = 0, cy = 0, hw = 0, hh = 0;
            bool haveCam = cam != null && cam.orthographic;
            if (haveCam)
            {
                var p = cam.transform.position;
                hh = cam.orthographicSize + 8f; hw = cam.orthographicSize * cam.aspect + 8f;
                cx = p.x; cy = p.y;
            }

            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            var clouds = _nature.Clouds;
            for (int k = 0; k < clouds.Length; k++)
            {
                var cloud = clouds[k];
                var def = content.Clouds[cloud.Type];
                string effect = def.EffectCode?.ToLowerInvariant();
                bool rain = effect == "rain", snow = effect == "snow";
                if (!rain && !snow) continue;
                if (haveCam && Mathf.Abs(cloud.Pos.x - cx) > hw + cloud.Radius || Mathf.Abs(cloud.Pos.y - cy) > hh + cloud.Radius) continue;

                int count = Mathf.Clamp((int)(cloud.Radius * 1.6f), 8, quality >= 2 ? 40 : 24);
                for (int n = 0; n < count; n++)
                {
                    uint h = math.hash(new int2(k * 7919 + n, n * 31 + 5));
                    float fx = (h & 1023) / 1023f, fy = ((h >> 10) & 1023) / 1023f;
                    float px = cloud.Pos.x + (fx - 0.5f) * cloud.Radius * 2f;
                    float speed = snow ? 1.6f : 7f;
                    float phase = Mathf.Repeat(t / speed + fy, 1f);
                    float py = cloud.Pos.y + 2f - phase * (cloud.Radius + 6f);
                    if (haveCam && Mathf.Abs(py - cy) > hh) continue;
                    if (snow) px += Mathf.Sin(t * 1.3f + h % 17) * 1.2f;

                    Color32 col = snow
                        ? new Color32(238, 244, 252, 170)
                        : new Color32(170, 195, 235, 120);
                    float w = snow ? 0.7f : 0.28f, hgt = snow ? 0.7f : 1.6f;
                    Quad(px, py, w, hgt, col);
                }
            }
            Apply(_weatherMesh);
        }

        void BuildBurningSmoke(Camera cam, int quality)
        {
            var burning = _nature.Burning;
            if (burning.Length == 0) { _smokeMesh.Clear(); return; }
            var map = _nature.World;
            float t = Time.unscaledTime;
            float cx = 0, cy = 0, hw = 0, hh = 0;
            bool haveCam = cam != null && cam.orthographic;
            if (haveCam)
            {
                var p = cam.transform.position;
                hh = cam.orthographicSize + 6f; hw = cam.orthographicSize * cam.aspect + 6f;
                cx = p.x; cy = p.y;
            }

            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            int budget = quality >= 2 ? 320 : 160;
            int step = Mathf.Max(1, burning.Length / budget); // cap the per-frame cost on large fires
            for (int k = 0; k < burning.Length; k += step)
            {
                int tile = burning[k];
                int x = tile % map.Width, y = tile / map.Width;
                if (haveCam && (Mathf.Abs(x - cx) > hw || Mathf.Abs(y - cy) > hh)) continue;
                uint h = math.hash(new uint2((uint)tile, 7u));
                float phase = Mathf.Repeat(t / 2.2f + (h & 255) / 255f, 1f);
                float px = x + 0.5f + Mathf.Sin(t * 1.1f + ((h >> 8) % 13)) * 0.6f * phase;
                float py = y + 1f + phase * 5f;
                byte a = (byte)((1f - phase) * 90f);
                float size = 1.1f + phase * 1.8f;
                Quad(px, py, size, size, new Color32(70, 70, 74, a));
            }
            Apply(_smokeMesh);
        }

        void Quad(float cx, float cy, float w, float h, Color32 col)
        {
            int v = _v.Count;
            _v.Add(new Vector3(cx - w * 0.5f, cy - h * 0.5f, 0f));
            _v.Add(new Vector3(cx + w * 0.5f, cy - h * 0.5f, 0f));
            _v.Add(new Vector3(cx + w * 0.5f, cy + h * 0.5f, 0f));
            _v.Add(new Vector3(cx - w * 0.5f, cy + h * 0.5f, 0f));
            // Sample only the solid centre of the first blob: particles stay solid squares.
            _t.Add(new Vector2(0.42f, 0.42f)); _t.Add(new Vector2(0.58f, 0.42f));
            _t.Add(new Vector2(0.58f, 0.58f)); _t.Add(new Vector2(0.42f, 0.58f));
            _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
            _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
            _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
        }

        void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _t);
            mesh.SetColors(_c);
            mesh.SetTriangles(_i, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
        }

        void UpdateTint()
        {
            var era = _nature.Era;
            var content = _nature.Content;
            Color from = content.Eras[era.PreviousEra].ParsedTint, to = content.Eras[era.CurrentEra].ParsedTint;
            Color tint = Color.Lerp(from, to, era.TintBlend);
            Shader.SetGlobalColor(EraTintId, Color.Lerp(Color.white, tint, EraTintStrength));

            // Bölüm 11 (G4): render-only day/night light; the clock is read, never written.
            float phase = 0.5f;
            if (DayNightEnabled && ViewClock != null)
            {
                long period = Mathf.Max(1, DayNightMonths) * (long)SimConst.TicksPerMonth;
                phase = (float)(ViewClock.Tick % period) / period;
            }
            Shader.SetGlobalColor(DayLightId, DayLightCurve(phase));

            // G5: seasonal feature tint, only while the seasons law runs; smooth month blend.
            float month = 0f;
            bool seasons = _nature.Laws.IsOn("law.seasons");
            if (seasons && ViewClock != null)
                month = ViewClock.Month + Mathf.Clamp01(ViewClock.Tick % SimConst.TicksPerMonth / (float)SimConst.TicksPerMonth);
            Shader.SetGlobalColor(SeasonTintId, seasons ? SeasonTint(month) : Color.white);

            // G6: the wind feeds the feature sway shader (same values the clouds drift with).
            var wind = _nature.Wind;
            Shader.SetGlobalVector(WindDirId, new Vector4(wind.x, wind.y, 0f, 0f));
        }

        static Color DayLightCurve(float p)
        {
            var night = new Color(0.52f, 0.58f, 0.82f);
            var dawn = new Color(1.06f, 0.88f, 0.72f);
            var day = new Color(1f, 1f, 1f);
            var dusk = new Color(1.08f, 0.82f, 0.66f);
            if (p < 0.22f) return night;
            if (p < 0.32f) return Color.Lerp(night, dawn, (p - 0.22f) / 0.10f);
            if (p < 0.40f) return Color.Lerp(dawn, day, (p - 0.32f) / 0.08f);
            if (p < 0.62f) return day;
            if (p < 0.72f) return Color.Lerp(day, dusk, (p - 0.62f) / 0.10f);
            if (p < 0.82f) return Color.Lerp(dusk, night, (p - 0.72f) / 0.10f);
            return night;
        }

        static Color SeasonTint(float month)
        {
            // Anchor palettes per season start; months wrap around the year.
            int m = Mathf.FloorToInt(month) % 12;
            float f = month - Mathf.FloorToInt(month);
            return Color.Lerp(SeasonFor(m), SeasonFor((m + 1) % 12), f);
        }

        static Color SeasonFor(int month) => month switch
        {
            11 or 0 or 1 => new Color(0.94f, 0.97f, 1.06f),  // winter
            2 or 3 or 4 => new Color(0.93f, 1.06f, 0.90f),   // spring
            5 or 6 or 7 => new Color(1f, 1f, 1f),            // summer
            _ => new Color(1.08f, 0.98f, 0.80f),             // autumn
        };

        void Build(Mesh mesh, Vector2 offset, float alpha, bool shadow)
        {
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            var clouds = _nature.Clouds;
            var content = _nature.Content;
            for (int k = 0; k < clouds.Length; k++)
            {
                var cloud = clouds[k];
                float w = cloud.Radius * 2f, h = cloud.Radius;
                float x = cloud.Pos.x + offset.x - w * 0.5f, y = cloud.Pos.y + offset.y - h * 0.5f;
                int variant = k % BlobVariants;
                float u0 = (float)variant / BlobVariants, u1 = (float)(variant + 1) / BlobVariants;
                Color32 col = shadow ? new Color32(0, 0, 0, 255) : content.Clouds[cloud.Type].ParsedColor;
                col.a = (byte)(alpha * 255f);
                int v = _v.Count;
                _v.Add(new Vector3(x, y, 0f));
                _v.Add(new Vector3(x + w, y, 0f));
                _v.Add(new Vector3(x + w, y + h, 0f));
                _v.Add(new Vector3(x, y + h, 0f));
                _t.Add(new Vector2(u0, 0f)); _t.Add(new Vector2(u1, 0f)); _t.Add(new Vector2(u1, 1f)); _t.Add(new Vector2(u0, 1f));
                _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
                _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
                _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
            }
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _t);
            mesh.SetColors(_c);
            mesh.SetTriangles(_i, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
        }

        // Three pixel blobs side by side: overlapping ellipses, hard edges (pixel art).
        static Texture2D MakeBlobs()
        {
            int w = BlobW * BlobVariants;
            var tex = new Texture2D(w, BlobH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CloudBlobs" };
            var pixels = new Color32[w * BlobH];
            var rng = new System.Random(7); // render-only decoration, not simulation
            for (int b = 0; b < BlobVariants; b++)
            {
                for (int e = 0; e < 4; e++)
                {
                    bool core = e == 0; // one centered ellipse keeps every blob solid in the middle
                    float cx = core ? BlobW * 0.5f : rng.Next(6, BlobW - 6), cy = core ? BlobH * 0.5f : rng.Next(4, BlobH - 4);
                    float rx = core ? 8 : rng.Next(4, 8), ry = core ? 4 : rng.Next(3, 5);
                    for (int y = 0; y < BlobH; y++)
                        for (int x = 0; x < BlobW; x++)
                        {
                            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                            if (dx * dx + dy * dy <= 1f) pixels[y * w + b * BlobW + x] = new Color32(255, 255, 255, 255);
                        }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            Unbind();
            if (_material == null) return;
            Release(_material);
            Release(_blobs);
            Release(_cloudMesh);
            Release(_shadowMesh);
        }

        static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
