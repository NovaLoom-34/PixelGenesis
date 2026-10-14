using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.3: draggable floating window with a title bar (pin, close), optional tabs, refreshed 4 times a second.
    public abstract class WindowBase
    {
        public const float RefreshSeconds = 0.25f;

        public readonly VisualElement Root;
        protected readonly VisualElement Body;
        readonly Label _title;
        readonly Button _pin;
        readonly VisualElement _tabBar;
        readonly List<Button> _tabs = new List<Button>();
        Vector2 _dragStart, _posStart;
        bool _dragging;
        float _next;

        public WindowManager Manager { get; internal set; }
        public object Key { get; internal set; }
        public bool Pinned { get; private set; }
        public void SetPinned(bool value) { Pinned = value; _pin.EnableInClassList("pg-button--selected", value); }
        protected IGameHost Host { get; }
        protected int Tab { get; private set; }

        protected WindowBase(IGameHost host, string title)
        {
            Host = host;
            Root = new VisualElement();
            Root.AddToClassList("pg-float");
            var bar = new VisualElement();
            bar.AddToClassList("pg-titlebar");
            Root.Add(bar);
            _title = new Label(title);
            _title.AddToClassList("pg-window-title");
            _title.style.flexGrow = 1;
            bar.Add(_title);
            _pin = Small("ui.pin", () => { Pinned = !Pinned; _pin.EnableInClassList("pg-button--selected", Pinned); });
            bar.Add(_pin);
            var close = Small("ui.close_x", () => Manager?.Close(this));
            close.AddToClassList("pg-button--close");
            bar.Add(close);
            bar.RegisterCallback<PointerDownEvent>(OnDown);
            bar.RegisterCallback<PointerMoveEvent>(OnMove);
            bar.RegisterCallback<PointerUpEvent>(OnUp);
            _tabBar = new VisualElement();
            _tabBar.AddToClassList("pg-row");
            Root.Add(_tabBar);
            var scroll = new ScrollView();
            scroll.AddToClassList("pg-float-body");
            Root.Add(scroll);
            Body = scroll.contentContainer;
        }

        protected void SetTitle(string text) => _title.text = text;

        protected void SetTabs(params string[] keys)
        {
            _tabBar.Clear();
            _tabs.Clear();
            for (int t = 0; t < keys.Length; t++)
            {
                int index = t;
                var b = Small(keys[t], () => ShowTab(index));
                b.AddToClassList("pg-tab");
                _tabBar.Add(b);
                _tabs.Add(b);
            }
            ShowTab(0);
        }

        protected void ShowTab(int index)
        {
            Tab = index;
            for (int t = 0; t < _tabs.Count; t++) _tabs[t].EnableInClassList("pg-button--selected", t == index);
            Rebuild();
        }

        // Full rebuild of the body (tab switch, open); Refresh() may update in place or rebuild.
        protected void Rebuild()
        {
            Body.Clear();
            if (Host.Sim != null) Build();
        }

        protected abstract void Build();
        protected virtual void Refresh()
        {
            var focus = Root.panel?.focusController?.focusedElement as VisualElement;
            if (focus != null && Root.Contains(focus)) return;
            Rebuild();
        }

        internal void Tick(float now)
        {
            if (now < _next || Host?.Sim == null) return;
            _next = now + RefreshSeconds;
            Refresh();
        }

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0 || e.target is Button) return;
            _dragging = true;
            _dragStart = e.position;
            _posStart = new Vector2(Root.resolvedStyle.left, Root.resolvedStyle.top);
            Root.BringToFront();
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!_dragging) return;
            Vector2 d = (Vector2)e.position - _dragStart;
            Root.style.left = Mathf.Clamp(_posStart.x + d.x, 0, Mathf.Max(0, Root.parent.resolvedStyle.width - Root.resolvedStyle.width));
            Root.style.top = Mathf.Clamp(_posStart.y + d.y, 0, Mathf.Max(0, Root.parent.resolvedStyle.height - 44));
        }

        void OnUp(PointerUpEvent e)
        {
            _dragging = false;
            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
        }

        // --- small builders shared by windows ---

        public static Button Small(string key, Action onClick, string iconName = null)
        {
            var b = new Button(onClick);
            b.AddToClassList("pg-button");
            string icon = iconName ?? PgIcons.MenuForUiKey(key);
            PgIcons.AddToButton(b, Loc.T(key), icon == null ? null : PgIcons.Menu(icon));
            return b;
        }

        public static Button Raw(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("pg-button");
            return b;
        }

        protected Label Line(string text, VisualElement parent = null)
        {
            var l = new Label(text);
            l.AddToClassList("pg-line");
            (parent ?? Body).Add(l);
            return l;
        }

        protected Label Heading(string key)
        {
            var l = new Label(Loc.T(key));
            l.AddToClassList("pg-heading");
            Body.Add(l);
            return l;
        }

        protected VisualElement Row(VisualElement parent = null)
        {
            var r = new VisualElement();
            r.AddToClassList("pg-row");
            r.style.flexWrap = Wrap.Wrap;
            (parent ?? Body).Add(r);
            return r;
        }

        protected VisualElement Bar(string label, float value01, Color color)
        {
            var row = Row();
            var l = new Label(label);
            l.AddToClassList("pg-bar-label");
            row.Add(l);
            var back = new VisualElement();
            back.AddToClassList("pg-meter");
            var fill = new VisualElement();
            fill.AddToClassList("pg-meter-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(value01) * 100f);
            fill.style.backgroundColor = color;
            back.Add(fill);
            row.Add(back);
            return row;
        }
    }

    public sealed class WindowManager
    {
        public const int MaxOpen = 4;

        readonly VisualElement _layer;
        readonly List<WindowBase> _open = new List<WindowBase>();
        int _cascade;

        public WindowManager(VisualElement layer) => _layer = layer;
        public Action<bool> Sound; // true = opened, false = closed

        public int Count => _open.Count;
        public bool AnyOpen => _open.Count > 0;
        public IReadOnlyList<WindowBase> Open => _open;

        // Same key: bring the window forward; otherwise create it (the oldest unpinned window gives way past four).
        public T Show<T>(object key, Func<T> create) where T : WindowBase
        {
            for (int i = 0; i < _open.Count; i++)
                if (_open[i] is T typed && Equals(typed.Key, key))
                { typed.Root.BringToFront(); _open.RemoveAt(i); _open.Add(typed); return typed; }
            if (_open.Count >= MaxOpen)
            {
                var victim = _open.Find(w => !w.Pinned);
                if (victim == null) return null;
                Close(victim);
            }
            var win = create();
            win.Manager = this;
            win.Key = key;
            win.Root.style.left = 120 + 36 * (_cascade % 6);
            win.Root.style.top = 70 + 30 * (_cascade % 6);
            _cascade++;
            _layer.Add(win.Root);
            _open.Add(win);
            win.Tick(0f);
            Sound?.Invoke(true);
            return win;
        }

        public void Close(WindowBase w)
        {
            w.Root.RemoveFromHierarchy();
            if (_open.Remove(w)) Sound?.Invoke(false);
        }

        public void CloseTop()
        {
            if (_open.Count > 0) Close(_open[_open.Count - 1]);
        }

        public void CloseAll()
        {
            for (int i = _open.Count - 1; i >= 0; i--) Close(_open[i]);
        }

        public void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                try { _open[i].Tick(now); }
                catch (Exception e) { Debug.LogException(e); Close(_open[i]); } // a broken window must not take the game down
            }
        }
    }
}
