using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Sim;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 7 visuals: falling drops with shrinking shadows, world objects, explosion flashes, camera shake,
    // Game of Life cells and Langton ant trails (overlay texture). Reads PowerWorld only.
    public sealed class PowerFxRenderer : MonoBehaviour
    {
        public const int FxOrder = SortingLayers.OrderPowerFx, ShadowOrder = SortingLayers.OrderPowerShadow, OverlayOrder = SortingLayers.OrderFxOverlay;
        public const float FlashSeconds = 0.5f;
        public static Vector2 ShakeOffset; // CameraController adds this
        public static bool ScreenShakeEnabled = true;

        public Camera ViewCamera;

        PowerWorld _pw;
        EventBus _events;
        ContentDB _content;
        int _width, _height;
        Texture2D _dot, _overlay;
        Material _material, _overlayMaterial;
        Mesh _fx, _shadow;
        GameObject _overlayGo;
        NativeArray<Color32> _pixels;
        float _shake, _time;
        int _overlayFrame;
        readonly List<Vector4> _flashes = new List<Vector4>(16); // x, y, radius, start time
        readonly List<Vector3> _bolts = new List<Vector3>(16);   // x, y, start time — lightning strikes
        readonly List<Vector4> _smoke = new List<Vector4>(64);   // x, y, start time, seed — explosion smoke
        readonly List<Vector3> _v = new List<Vector3>(512);
        readonly List<Vector2> _t = new List<Vector2>(512);
        readonly List<Color32> _c = new List<Color32>(512);
        readonly List<int> _i = new List<int>(768);

        static readonly Color32 DropColor = new Color32(60, 50, 50, 255), Shadow = new Color32(0, 0, 0, 90);
        // per-power drop identity: keyword → tint, first match wins; unmatched powers get a hue from the id
        static readonly (string key, Color32 c)[] DropColors =
        {
            ("meteor", new Color32(255,120,40,255)), ("lava", new Color32(255,90,30,255)), ("bomb", new Color32(70,60,55,255)),
            ("lightning", new Color32(255,250,180,255)), ("thunder", new Color32(255,250,180,255)), ("storm", new Color32(190,205,235,255)),
            ("rain", new Color32(120,180,255,255)), ("flood", new Color32(90,160,240,255)), ("tsunami", new Color32(90,160,240,255)),
            ("snow", new Color32(235,245,255,255)), ("ice", new Color32(200,230,255,255)), ("freeze", new Color32(200,230,255,255)), ("blizzard", new Color32(220,235,255,255)),
            ("seed", new Color32(120,200,90,255)), ("grow", new Color32(120,200,90,255)), ("forest", new Color32(90,170,80,255)), ("tree", new Color32(90,170,80,255)), ("grass", new Color32(120,200,90,255)), ("wood", new Color32(110,150,70,255)),
            ("fire", new Color32(255,140,40,255)), ("flame", new Color32(255,140,40,255)), ("burn", new Color32(255,140,40,255)), ("ember", new Color32(255,120,40,255)),
            ("plague", new Color32(150,200,60,255)), ("poison", new Color32(150,200,60,255)), ("curse", new Color32(160,90,200,255)),
            ("heal", new Color32(180,255,180,255)), ("bless", new Color32(255,240,170,255)), ("divine", new Color32(255,240,170,255)), ("holy", new Color32(255,240,170,255)),
            ("zombie", new Color32(120,160,110,255)), ("skeleton", new Color32(225,222,205,255)), ("dragon", new Color32(200,60,40,255)),
            ("sheep", new Color32(240,238,228,255)), ("wolf", new Color32(150,150,155,255)), ("bear", new Color32(110,80,55,255)),
            ("boar", new Color32(100,80,60,255)), ("deer", new Color32(160,120,80,255)), ("rabbit", new Color32(200,180,150,255)),
            ("fox", new Color32(220,130,60,255)), ("cow", new Color32(150,110,85,255)), ("chicken", new Color32(245,240,225,255)), ("horse", new Color32(170,120,80,255)),
            ("fish", new Color32(110,170,230,255)), ("whale", new Color32(80,130,200,255)), ("piranha", new Color32(200,90,80,255)), ("dolphin", new Color32(140,190,235,255)),
            ("human", new Color32(225,190,155,255)), ("elf", new Color32(235,215,170,255)), ("dwarf", new Color32(205,160,120,255)), ("orc", new Color32(130,170,90,255)),
        };
        static readonly Color32 TornadoColor = new Color32(170, 170, 180, 170), HoleColor = new Color32(10, 0, 20, 255), HoleRing = new Color32(120, 40, 160, 140);
        static readonly Color32 GeyserColor = new Color32(200, 230, 255, 200), LureColor = new Color32(255, 210, 60, 255), RiftColor = new Color32(200, 40, 60, 200);
        static readonly Color32 MonolithColor = new Color32(20, 20, 30, 255), AntColor = new Color32(220, 30, 30, 255), LavaGlow = new Color32(255, 110, 30, 160);
        static readonly Color32 LifeColor = new Color32(255, 105, 180, 255), TrailColor = new Color32(40, 40, 60, 150);

        public void Bind(SimWorld sim)
        {
            Unbind();
            _pw = sim.Powers;
            _content = sim.Content;
            _width = sim.World.Width;
            _height = sim.World.Height;
            _events = sim.Events;
            _events.Subscribe<ExplosionEvent>(OnExplosion);
            if (_material == null)
            {
                _dot = MakeDot();
                var shader = Resources.Load<Shader>("PG_Sprite");
                _material = new Material(shader) { name = "PowerFx", mainTexture = _dot };
                _material.SetFloat("_Soft", 1f); // preserve fading smoke, shadows and contact ripples
                _overlayMaterial = new Material(shader) { name = "Automata" };
                _fx = new Mesh { name = "PowerFx" };
                _shadow = new Mesh { name = "PowerFxShadows" };
                Layer("PowerFx", _fx, _material, SortingLayers.Weather, FxOrder);
                Layer("PowerFxShadows", _shadow, _material, SortingLayers.Decals, ShadowOrder);
            }
        }

        public void Unbind()
        {
            if (_events != null) _events.Unsubscribe<ExplosionEvent>(OnExplosion);
            _events = null;
            _pw = null;
            _content = null;
            _flashes.Clear();
            _bolts.Clear();
            _smoke.Clear();
            _shake = 0f;
            ShakeOffset = Vector2.zero;
            if (_overlayGo != null) _overlayGo.SetActive(false);
        }

        void OnExplosion(ExplosionEvent e)
        {
            if (_flashes.Count >= 24) _flashes.RemoveAt(0);
            _flashes.Add(new Vector4(e.Pos.x, e.Pos.y, Mathf.Max(1.5f, e.Radius), _time));
            if (ScreenShakeEnabled) _shake = Mathf.Max(_shake, Mathf.Min(2f, e.Radius / 15f));
            // Bölüm 11 görsel cilası (G8): explosion smoke rises and drifts for a couple of seconds.
            for (int s = 0; s < 5; s++)
            {
                if (_smoke.Count >= 120) _smoke.RemoveAt(0);
                _smoke.Add(new Vector4(e.Pos.x + Mathf.Sin(s * 2.4f) * e.Radius * 0.4f, e.Pos.y + Mathf.Cos(s * 1.7f) * e.Radius * 0.3f, _time + s * 0.08f, (uint)(s * 7919 + 13)));
            }
        }

        void Layer(string name, Mesh mesh, Material material, string layerName, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingLayerName = layerName;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void LateUpdate()
        {
            _time += Time.unscaledDeltaTime;
            _shake = Mathf.Max(0f, _shake - Time.unscaledDeltaTime * 2f);
            ShakeOffset = ScreenShakeEnabled && _shake > 0f ? new Vector2(Mathf.Sin(_time * 71f), Mathf.Cos(_time * 53f)) * _shake : Vector2.zero;
            if (_pw == null) return;
            BuildFx();
            UpdateOverlay();
        }

        void BuildFx()
        {
            Begin();
            var drops = _pw.Drops;
            for (int k = 0; k < drops.Length; k++)
            {
                var d = drops[k];
                var col = DropTint(d.Power);
                Quad(d.Pos.x, d.Pos.y + d.Z * 0.5f, 1.2f, 1.2f, col);
                Quad(d.Pos.x, d.Pos.y + d.Z * 0.5f + 1.1f, 0.7f, 0.7f, new Color32(col.r, col.g, col.b, 90)); // trail
                if (d.Z < 1.5f)
                {
                    string id = PowerId(d.Power);
                    if (id.Contains("rain") || id.Contains("flood") || id.Contains("tsunami") || id.Contains("water") || id.Contains("geyser") || id.Contains("ice"))
                        Ring(d.Pos.x, d.Pos.y, 1.2f + (1.5f - d.Z) * 0.65f, new Color32(180, 228, 250, 115), 6);
                    else if (id.Contains("heal") || id.Contains("bless") || id.Contains("holy") || id.Contains("divine"))
                    {
                        Quad(d.Pos.x, d.Pos.y, 0.45f, 2.5f, new Color32(205, 255, 185, 190));
                        Quad(d.Pos.x, d.Pos.y, 2.5f, 0.45f, new Color32(205, 255, 185, 190));
                    }
                    else if (id.Contains("fire") || id.Contains("flame") || id.Contains("burn") || id.Contains("ember") || id.Contains("lava"))
                        Quad(d.Pos.x, d.Pos.y + 0.6f, 1.2f, 2.1f, new Color32(255, 155, 55, 130));
                    else Quad(d.Pos.x, d.Pos.y, 1.9f, 1.1f, new Color32(col.r, col.g, col.b, 70));
                }
                if (IsLightning(d.Power) && d.Z < 1f) AddBolt(d.Pos.x, d.Pos.y);
            }
            var objects = _pw.Objects;
            for (int k = 0; k < objects.Length; k++)
            {
                var o = objects[k];
                float wobble = Mathf.Sin(_time * 12f + k) * 0.4f;
                switch (o.Kind)
                {
                    case WorldObjectKind.Tornado:
                        for (int s = 0; s < 4; s++) Quad(o.Pos.x + wobble * s * 0.3f, o.Pos.y + s * 1.5f, 2f + s * 1.2f, 1.4f, TornadoColor);
                        break;
                    case WorldObjectKind.BlackHole:
                        float ring = 6f + Mathf.Sin(_time * 4f);
                        Quad(o.Pos.x, o.Pos.y, ring * 2f, ring * 2f, HoleRing);
                        Quad(o.Pos.x, o.Pos.y, 3f, 3f, HoleColor);
                        break;
                    case WorldObjectKind.Geyser: Quad(o.Pos.x, o.Pos.y, 1.5f, 1.5f, GeyserColor); break;
                    case WorldObjectKind.Volcano: if (o.Stage > 0) Quad(o.Pos.x, o.Pos.y + 1f, 5f, 3f, LavaGlow); break;
                    case WorldObjectKind.Monolith: Quad(o.Pos.x, o.Pos.y + 1.5f, 1f, 3f, MonolithColor); break;
                    case WorldObjectKind.Lure: Quad(o.Pos.x, o.Pos.y, 1.5f, 1.2f, LureColor); break;
                    case WorldObjectKind.Rift: Quad(o.Pos.x, o.Pos.y, 3f + wobble, 4f, RiftColor); break;
                    case WorldObjectKind.Ant: Quad(o.Pos.x + 0.5f, o.Pos.y + 0.5f, 1f, 1f, AntColor); break;
                    case WorldObjectKind.Wave:
                        // A short foam crest perpendicular to the moving tsunami front.
                        float sideX = -o.Dir.y, sideY = o.Dir.x;
                        for (int s = -2; s <= 2; s++)
                            Quad(o.Pos.x + sideX * s * 1.2f, o.Pos.y + sideY * s * 1.2f,
                                1.6f, 1.6f, new Color32(195, 235, 250, (byte)(s == 0 ? 205 : 130)));
                        break;
                }
            }
            for (int k = _flashes.Count - 1; k >= 0; k--)
            {
                var f = _flashes[k];
                float age = (_time - f.w) / FlashSeconds;
                if (age >= 1f) { _flashes.RemoveAt(k); continue; }
                float size = f.z * 2f * (0.4f + age * 0.8f);
                Quad(f.x, f.y, size, size, new Color32(255, (byte)(200 - 120 * age), 60, (byte)(220 * (1f - age))));

                // Bölüm 11 görsel cilası (G8): a shockwave ring racing ahead of the fireball.
                if (age < 0.55f)
                {
                    float ring = f.z * (1.0f + age * 2.6f);
                    var rc = new Color32(255, 240, 210, (byte)(Mathf.Clamp01(1f - age * 1.8f) * 150f));
                    for (int s = 0; s < 10; s++)
                    {
                        float a = s * Mathf.PI * 2f / 10f;
                        Quad(f.x + Mathf.Cos(a) * ring, f.y + Mathf.Sin(a) * ring, 1.6f, 1.6f, rc);
                    }
                }
            }
            for (int k = _smoke.Count - 1; k >= 0; k--)
            {
                var s = _smoke[k];
                float age = (_time - s.z) / 2.6f;
                if (age >= 1f) { _smoke.RemoveAt(k); continue; }
                if (age < 0f) continue;
                float wobble = Mathf.Sin(_time * 1.4f + s.w) * 0.8f * age;
                var col = new Color32((byte)Mathf.Lerp(58, 128, age), (byte)Mathf.Lerp(56, 122, age), (byte)Mathf.Lerp(60, 128, age), (byte)((1f - age) * 115f));
                Quad(s.x + wobble, s.y + 1f + age * 4.5f, 1.2f + age * 2.2f, 1.2f + age * 2.2f, col);
            }
            for (int k = _bolts.Count - 1; k >= 0; k--)
            {
                var b = _bolts[k];
                float age = (_time - b.z) / 0.18f;
                if (age >= 1f) { _bolts.RemoveAt(k); continue; }
                var c = new Color32(255, 250, 190, (byte)(230 * (1f - age)));
                float flick = (int)(_time * 60);
                for (int s = 0; s < 5; s++)                                             // jagged bolt: 5 stacked segments
                {
                    float jitter = Mathf.Sin(b.x * 12.9898f + s * 78.233f + flick) * 0.5f;
                    Quad(b.x + jitter, b.y + 1.2f + s * 1.7f, 0.55f - s * 0.07f, 1.8f, c);
                }
                Quad(b.x, b.y, 2.4f, 0.6f, c);                                          // ground flash
            }
            End(_fx);

            Begin();
            for (int k = 0; k < drops.Length; k++)
            {
                var d = drops[k];
                float s = Mathf.Lerp(1.4f, 0.4f, d.Z / 40f);
                Quad(d.Pos.x, d.Pos.y, s, s * 0.6f, Shadow);
            }
            End(_shadow);
        }

        // life cells pink, ant trails dark; refreshed every few frames while automata exist
        void UpdateOverlay()
        {
            bool active = _pw.Automaton.IsCreated;
            if (!active)
            {
                if (_overlayGo != null) _overlayGo.SetActive(false);
                return;
            }
            if (_overlay == null || _overlay.width != _width || _overlay.height != _height) MakeOverlay();
            _overlayGo.SetActive(true);
            if (++_overlayFrame % 4 != 0) return;
            var cells = _pw.Automaton;
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < _pixels.Length; i++)
            {
                byte b = cells[i];
                _pixels[i] = (b & 1) != 0 ? LifeColor : (b & 2) != 0 ? TrailColor : clear;
            }
            _overlay.SetPixelData(_pixels, 0);
            _overlay.Apply(false);
        }

        void MakeOverlay()
        {
            if (_overlay != null) Release(_overlay);
            if (_pixels.IsCreated) _pixels.Dispose();
            _overlay = new Texture2D(_width, _height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Automata" };
            _pixels = new NativeArray<Color32>(_width * _height, Allocator.Persistent);
            _overlayMaterial.mainTexture = _overlay;
            if (_overlayGo == null)
            {
                var mesh = new Mesh { name = "AutomataQuad" };
                _overlayGo = new GameObject("Automata");
                _overlayGo.transform.SetParent(transform, false);
                _overlayGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = _overlayGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _overlayMaterial;
                mr.sortingLayerName = SortingLayers.WorldUI;
                mr.sortingOrder = OverlayOrder;
            }
            var m = _overlayGo.GetComponent<MeshFilter>().sharedMesh;
            m.Clear();
            m.SetVertices(new List<Vector3> { new Vector3(0, 0), new Vector3(_width, 0), new Vector3(_width, _height), new Vector3(0, _height) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            m.SetColors(new List<Color32> { Color.white, Color.white, Color.white, Color.white });
            m.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        }

        Color32 DropTint(ushort power)
        {
            string id = PowerId(power);
            foreach (var (key, c) in DropColors)
                if (id.Contains(key)) return c;
            uint h = (uint)id.GetHashCode();
            return Color.HSVToRGB((h % 360) / 360f, 0.55f, 0.85f);
        }

        bool IsLightning(ushort power)
        {
            string id = PowerId(power);
            return id.Contains("lightning") || id.Contains("thunder") || id.Contains("shock");
        }

        string PowerId(ushort power) => _content != null && power < _content.Powers.Count ? _content.Powers[power].Id : "";

        void Ring(float x, float y, float radius, Color32 color, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Quad(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius * 0.55f, 0.6f, 0.6f, color);
            }
        }

        void AddBolt(float x, float y)
        {
            for (int i = 0; i < _bolts.Count; i++)
                if (Mathf.Abs(_bolts[i].x - x) < 0.5f && Mathf.Abs(_bolts[i].y - y) < 0.5f && _time - _bolts[i].z < 0.14f) return;
            if (_bolts.Count >= 24) _bolts.RemoveAt(0);
            _bolts.Add(new Vector3(x, y, _time));
        }

        void Begin()
        {
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
        }

        void Quad(float cx, float cy, float w, float h, Color32 col)
        {
            int v = _v.Count;
            float x = cx - w * 0.5f, y = cy - h * 0.5f;
            _v.Add(new Vector3(x, y, 0f)); _v.Add(new Vector3(x + w, y, 0f)); _v.Add(new Vector3(x + w, y + h, 0f)); _v.Add(new Vector3(x, y + h, 0f));
            _t.Add(new Vector2(0f, 0f)); _t.Add(new Vector2(1f, 0f)); _t.Add(new Vector2(1f, 1f)); _t.Add(new Vector2(0f, 1f));
            _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
            _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
            _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
        }

        void End(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _t);
            mesh.SetColors(_c);
            mesh.SetTriangles(_i, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
        }

        // a hard-edged round pixel dot, tinted per quad
        static Texture2D MakeDot()
        {
            const int n = 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FxDot" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - n * 0.5f, dy = y + 0.5f - n * 0.5f;
                    px[y * n + x] = dx * dx + dy * dy <= n * n * 0.25f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            Unbind();
            if (_pixels.IsCreated) _pixels.Dispose();
            Release(_material);
            Release(_overlayMaterial);
            Release(_dot);
            Release(_overlay);
            Release(_fx);
            Release(_shadow);
        }

        static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
