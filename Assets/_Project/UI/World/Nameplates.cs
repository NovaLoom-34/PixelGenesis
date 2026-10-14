using System.Collections.Generic;
using PG.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.8 plates as pooled UI labels positioned from world space: LOD 1 city name + population, LOD 2-3 kingdom
    // name over the capital, LOD 0 kings and the followed unit. Content refreshes 4x a second, positions every frame.
    // ponytail: no overlap culling or banners yet; add a priority pass when plates crowd at LOD 1.
    public sealed class Nameplates
    {
        public const int MaxPlates = 300;
        const float TextSeconds = 0.25f;

        readonly UiContext _ui;
        readonly VisualElement _layer;
        readonly List<Label> _pool = new List<Label>();
        readonly List<Vector2> _world = new List<Vector2>();
        int _used;
        float _next;
        public bool Visible = true;
        public bool Eye; // Bölüm 8.9 pw.eye: '?' over units that carry an undiscovered trait (LOD 0-1, on screen)

        public Nameplates(UiContext ui, VisualElement parent)
        {
            _ui = ui;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("pg-plates");
            parent.Insert(0, _layer);
        }

        public void Update()
        {
            var sim = _ui.Sim;
            var cam = _ui.Camera != null ? _ui.Camera.Camera : null;
            if (sim == null || cam == null || !Visible || _layer.panel == null) { Hide(0); return; }
            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + TextSeconds;
                Rebuild(sim, _ui.Camera.Lod);
            }
            var panel = _layer.panel;
            for (int i = 0; i < _used; i++)
            {
                var screen = cam.WorldToScreenPoint(new Vector3(_world[i].x, _world[i].y, 0f));
                var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                var l = _pool[i];
                l.style.left = p.x - l.resolvedStyle.width * 0.5f;
                l.style.top = p.y - 28f;
            }
        }

        void Rebuild(SimWorld sim, int lod)
        {
            _used = 0;
            _world.Clear();
            var civ = sim.Civ;
            var meta = sim.Meta;
            if (lod == 1 && civ != null)
            {
                for (int c = 0; c < civ.CityCount && _used < MaxPlates; c++)
                {
                    if (!civ.CityAlive(c)) continue;
                    var d = civ.Cities.Items[c];
                    Add(Names.City(sim, c) + " · " + civ.Cities.Headers[c].MemberCount, d.Center, "pg-plate--city");
                }
            }
            else if (lod >= 2 && civ != null)
            {
                for (int k = 0; k < meta.Kingdoms.Count && _used < MaxPlates; k++)
                {
                    if (!meta.KingdomAlive(k)) continue;
                    int cap = meta.Kingdoms.Items[k].Capital;
                    if (cap < 0 || !civ.CityAlive(cap)) continue;
                    Add(Names.Kingdom(sim, k), civ.Cities.Items[cap].Center, "pg-plate--kingdom");
                }
            }
            else if (lod == 0)
            {
                var u = sim.Units.Store;
                for (int k = 0; k < meta.Kingdoms.Count && _used < MaxPlates; k++)
                {
                    if (!meta.KingdomAlive(k)) continue;
                    var king = meta.Kingdoms.Items[k].King;
                    if (u.IsAlive(king)) AddUnit(sim, king.Index, "pg-plate--king");
                }
                if (_ui.Followed >= 0 && u.IsAlive(_ui.Followed)) AddUnit(sim, _ui.Followed, "pg-plate--unit");
            }
            if (Eye && lod <= 1) MarkUndiscovered(sim);
            Hide(_used);
        }

        void MarkUndiscovered(SimWorld sim)
        {
            var cam = _ui.Camera.Camera;
            var c = cam.transform.position;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var u = sim.Units.Store;
            var traits = sim.Content.UnitTraits;
            for (int k = 0; k < u.Alive.Length && _used < MaxPlates; k++)
            {
                int i = u.Alive[k];
                var p = u.Pos[i];
                if (Mathf.Abs(p.x - c.x) > w || Mathf.Abs(p.y - c.y) > h) continue;
                var set = u.Traits[i];
                for (int t = 0; t < traits.Count; t++)
                    if (set.Has(t) && !_ui.Profile.IsDiscovered(traits[t].Id)) { AddAt("?", new Vector2(p.x, p.y + 0.8f), "pg-plate--eye"); break; }
            }
        }

        void AddUnit(SimWorld sim, int i, string cls)
        {
            var p = sim.Units.Store.Pos[i];
            AddAt(Names.Unit(sim, i), new Vector2(p.x, p.y + 0.6f), cls);
        }

        void Add(string text, Unity.Mathematics.int2 tile, string cls) => AddAt(text, new Vector2(tile.x + 0.5f, tile.y + 2f), cls);

        void AddAt(string text, Vector2 world, string cls)
        {
            if (_used == _pool.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList("pg-plate");
                _layer.Add(l);
                _pool.Add(l);
            }
            var label = _pool[_used++];
            label.text = text;
            label.ClearClassList();
            label.AddToClassList("pg-plate");
            label.AddToClassList(cls);
            label.style.display = DisplayStyle.Flex;
            _world.Add(world);
        }

        void Hide(int from)
        {
            for (int i = from; i < _pool.Count; i++) _pool[i].style.display = DisplayStyle.None;
            if (from == 0) _used = 0;
        }
    }
}
