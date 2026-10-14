using PG.Content;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.2 mini harita: chunk verisinin düşük çözünürlüklü örneklemesi + krallık tonu, kamera dikdörtgeni,
    // tıklayınca oraya git. Render katmanına dokunmadan doğrudan World/Civ/Meta verisini okur.
    public sealed class Minimap
    {
        const int Size = 160;
        const float RebuildSeconds = 4f; // terrain rarely changes fast enough to need more

        readonly UiContext _ui;
        readonly VisualElement _panel;
        readonly Image _image;
        readonly VisualElement _overlay;
        Texture2D _tex;
        int _texW = -1, _texH = -1;
        float _nextBuild;

        public Minimap(UiContext ui, VisualElement layer)
        {
            _ui = ui;
            _panel = new VisualElement();
            _panel.AddToClassList("pg-minimap");
            _image = new Image { scaleMode = ScaleMode.StretchToFill, pickingMode = PickingMode.Ignore };
            _panel.Add(_image);
            _overlay = new VisualElement();
            _overlay.AddToClassList("pg-minimap-overlay");
            _overlay.generateVisualContent += DrawFrustum;
            _panel.Add(_overlay);
            _panel.RegisterCallback<PointerDownEvent>(OnClick);
            layer.Add(_panel);
        }

        public void Update()
        {
            var sim = _ui.Sim;
            if (sim == null) { _panel.style.display = DisplayStyle.None; return; }
            _panel.style.display = DisplayStyle.Flex;
            if (Time.unscaledTime >= _nextBuild) { _nextBuild = Time.unscaledTime + RebuildSeconds; Rebuild(sim); }
            _overlay.MarkDirtyRepaint();
        }

        void Rebuild(SimWorld sim)
        {
            var map = sim.World;
            int stride = Mathf.Max(1, map.Width / Size);
            int w = Mathf.Max(1, map.Width / stride), h = Mathf.Max(1, map.Height / stride);
            if (_tex == null || _texW != w || _texH != h)
            {
                if (_tex != null) Object.Destroy(_tex);
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                _texW = w; _texH = h;
                _image.image = _tex;
            }
            var db = sim.Content;
            var civ = sim.Civ;
            var meta = sim.Meta;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int mx = Mathf.Min(map.Width - 1, x * stride), my = Mathf.Min(map.Height - 1, y * stride);
                    int i = map.Index(mx, my);
                    byte b = map.Biome[i];
                    Color32 c = b != 0 ? db.Biomes[b - 1].ParsedColors[0] : db.Tiles[map.Ground[i]].ParsedColors[0];
                    int city = civ != null ? map.Zones[map.ZoneIndexOf(mx, my)].OwnerCity : -1;
                    if (city >= 0 && civ.CityAlive(city))
                    {
                        int k = civ.KingdomOf(city);
                        if (k >= 0 && meta.KingdomAlive(k))
                        {
                            uint rgba = meta.Kingdoms.Headers[k].Color;
                            var kingdomColor = new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), 255);
                            c = Color.Lerp(c, kingdomColor, 0.6f);
                        }
                    }
                    pixels[(h - 1 - y) * w + x] = c; // flip: texture row 0 is the map's south edge
                }
            // kingdom capitals as bright 2x2 markers (city centre → map pixel space, south flipped)
            for (int k = 0; k < meta.Kingdoms.Count; k++)
            {
                if (!meta.KingdomAlive(k)) continue;
                int cap = meta.Kingdom(k).Capital;
                if (cap < 0 || cap >= civ.CityCount || !civ.CityAlive(cap)) continue;
                var center = civ.Cities.Items[cap].Center;
                int mx = Mathf.Clamp((int)(center.x / map.Width * w), 0, w - 2);
                int my = Mathf.Clamp((int)(center.y / map.Height * h), 0, h - 2);
                for (int dy2 = 0; dy2 < 2; dy2++)
                    for (int dx2 = 0; dx2 < 2; dx2++)
                        pixels[(h - 1 - (my + dy2)) * w + mx + dx2] = new Color32(255, 240, 200, 255);
            }
            _tex.SetPixels32(pixels);
            _tex.Apply(false);
        }

        void DrawFrustum(MeshGenerationContext ctx)
        {
            var sim = _ui.Sim;
            var cam = _ui.Camera != null ? _ui.Camera.Camera : null;
            if (sim == null || cam == null) return;
            var rect = _overlay.contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            var map = sim.World;
            var pos = cam.transform.position;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 ToLocal(float wx, float wy) => new Vector2(wx / map.Width * rect.width, rect.height - wy / map.Height * rect.height);
            var a = ToLocal(pos.x - halfW, pos.y - halfH);
            var b = ToLocal(pos.x + halfW, pos.y + halfH);
            var p = ctx.painter2D;
            p.strokeColor = UiTheme.Frustum;
            p.lineWidth = 1.5f;
            p.BeginPath();
            p.MoveTo(new Vector2(a.x, b.y));
            p.LineTo(new Vector2(b.x, b.y));
            p.LineTo(new Vector2(b.x, a.y));
            p.LineTo(new Vector2(a.x, a.y));
            p.ClosePath();
            p.Stroke();
        }

        void OnClick(PointerDownEvent e)
        {
            var sim = _ui.Sim;
            if (sim == null) return;
            var local = _overlay.WorldToLocal(e.position);
            var rect = _overlay.contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            float wx = local.x / rect.width * sim.World.Width;
            float wy = (1f - local.y / rect.height) * sim.World.Height;
            _ui.GoTo(new float2(wx, wy));
        }
    }
}
