using System;
using System.Collections.Generic;
using PG.Core;
using PG.Powers;
using PG.Sim;
using PG.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.2 HUD, basic Faz 1 version: top bar (date, speed, step, menu), power bar with tabs, brush panel, toasts.
    public sealed class HudView
    {
        static readonly string[] TabOrder = { "world", "civ", "creatures", "nature", "destruction", "other" };
        const int MaxToasts = 6;
        const float ToastSeconds = 4f;

        readonly IGameHost _host;
        readonly PowerTool _tool;
        readonly Label _date, _era, _pop, _kingdoms;
        readonly Button[] _speed = new Button[GameClock.SpeedLevels.Length];
        readonly Button _super, _step, _undoButton;
        readonly VisualElement _tabs, _powers, _toasts;
        readonly ScrollView _powerScroll;
        readonly Label _brushSize;
        readonly Button _shape;
        static readonly int[] PresetRadiusIndices = { 0, 1, 3, 5, 7 }; // Diameters: 1, 3, 7, 17, 41
        readonly Button[] _presetButtons = new Button[PresetRadiusIndices.Length];
        readonly List<string> _visibleTabs = new List<string>();
        readonly List<Button> _powerButtons = new List<Button>();
        readonly List<int> _powerIds = new List<int>();
        readonly List<ToastItem> _toastItems = new List<ToastItem>();
        int _tab;
        long _shownTick = -1;
        int _shownSpeed = -1;
        bool _shownSuper;
        Profile _profile;
        string _search = "";
        readonly VisualElement _favorites;
        readonly VisualElement _recent;
        readonly TextField _searchField;

        public HudView(VisualElement layer, IGameHost host, PowerTool tool)
        {
            _host = host;
            _tool = tool;

            var top = new VisualElement();
            top.AddToClassList("pg-bar");
            top.AddToClassList("pg-topbar");
            layer.Add(top);

            var status = new VisualElement();
            status.AddToClassList("pg-status");
            top.Add(status);
            var playback = new VisualElement();
            playback.AddToClassList("pg-playback");
            top.Add(playback);
            var actions = new VisualElement();
            actions.AddToClassList("pg-top-actions");
            top.Add(actions);

            var searchLabel = new Label(Loc.T("ui.search"));
            searchLabel.AddToClassList("pg-search-label");
            actions.Add(searchLabel);
            _searchField = new TextField { tooltip = Loc.T("ui.search") };
            _searchField.AddToClassList("pg-search");
            _searchField.RegisterValueChangedCallback(e => { _search = e.newValue; ShowTab(_tab); });
            actions.Add(_searchField);
            _favorites = new VisualElement();
            _favorites.AddToClassList("pg-favorites");
            layer.Add(_favorites);
            _recent = new VisualElement();
            _recent.AddToClassList("pg-recent-powers");
            _recent.style.display = DisplayStyle.None;
            layer.Add(_recent);

            _date = new Label();
            _date.AddToClassList("pg-label");
            _date.AddToClassList("pg-date");
            _date.AddToClassList("pg-date-primary");
            status.Add(_date);

            _era = new Label();
            _era.AddToClassList("pg-label");
            _era.AddToClassList("pg-date");
            status.Add(_era);

            _pop = new Label { pickingMode = PickingMode.Ignore };
            _pop.AddToClassList("pg-label");
            _pop.AddToClassList("pg-stat-chip");
            status.Add(_pop);

            _kingdoms = new Label { pickingMode = PickingMode.Ignore };
            _kingdoms.AddToClassList("pg-label");
            _kingdoms.AddToClassList("pg-stat-chip");
            status.Add(_kingdoms);

            for (int i = 0; i < _speed.Length; i++)
            {
                int index = i;
                _speed[i] = MakeButton(i == 0 ? Loc.T("ui.pause") : Loc.F("ui.speed_fmt", GameClock.SpeedLevels[i]), () =>
                {
                    var clock = _host.Sim?.Clock;
                    if (clock == null) return;
                    clock.SuperSpeed = false;
                    clock.SetSpeed(index);
                });
                playback.Add(_speed[i]);
            }

            _super = MakeButton(Loc.T("ui.super_speed"), () =>
            {
                var clock = _host.Sim?.Clock;
                if (clock == null) return;
                clock.SuperSpeed = !clock.SuperSpeed;
                if (clock.SuperSpeed && clock.Paused) clock.TogglePause();
            });
            playback.Add(_super);

            _step = MakeButton(Loc.T("ui.step"), () => _host.Sim?.Clock.StepOnce());
            playback.Add(_step);

            actions.Add(MakeButton(Loc.T("ui.new_world"), () => NewWorldClicked?.Invoke(), PgIcons.Menu("new_world")));
            actions.Add(MakeButton(Loc.T("ui.save_load"), () => SavesClicked?.Invoke(), PgIcons.Menu("save_load")));
            _undoButton = MakeButton(Loc.T("ui.undo"), () => UndoClicked?.Invoke(), PgIcons.Menu("undo"));
            _undoButton.tooltip = Loc.T("ui.undo");
            actions.Add(_undoButton);

            var bottom = new VisualElement();
            bottom.AddToClassList("pg-bottom");
            layer.Add(bottom);

            _tabs = new VisualElement();
            _tabs.AddToClassList("pg-row");
            _tabs.AddToClassList("pg-tab-strip");
            bottom.Add(_tabs);

            var row = new VisualElement();
            row.AddToClassList("pg-power-area");
            bottom.Add(row);

            _powerScroll = new ScrollView(ScrollViewMode.Vertical);
            _powerScroll.AddToClassList("pg-power-scroll");
            row.Add(_powerScroll);
            _powers = new VisualElement();
            _powers.AddToClassList("pg-row");
            _powers.AddToClassList("pg-power-grid");
            _powerScroll.Add(_powers);

            var brush = new VisualElement();
            brush.AddToClassList("pg-brush-panel");
            row.Add(brush);
            var brushLabel = new Label(Loc.T("ui.brush_size"));
            brushLabel.AddToClassList("pg-brush-label");
            brush.Add(brushLabel);
            var brushControls = new VisualElement();
            brushControls.AddToClassList("pg-brush-controls");
            brush.Add(brushControls);
            brushControls.Add(MakeButton("-", () => ChangeBrush(-1)));
            _brushSize = new Label();
            _brushSize.AddToClassList("pg-label");
            brushControls.Add(_brushSize);
            brushControls.Add(MakeButton("+", () => ChangeBrush(1)));
            _shape = MakeButton("", ToggleShape);
            _shape.AddToClassList("pg-brush-shape");
            brushControls.Add(_shape);

            var presets = new VisualElement();
            presets.AddToClassList("pg-brush-presets");
            brush.Add(presets);
            for (int k = 0; k < PresetRadiusIndices.Length; k++)
            {
                int rIdx = PresetRadiusIndices[k];
                int diameter = Brushes.Radii[rIdx] * 2 + 1;
                _presetButtons[k] = MakeButton(diameter.ToString(), () => { _tool.RadiusIndex = rIdx; RefreshBrush(); });
                _presetButtons[k].AddToClassList("pg-preset-btn");
                presets.Add(_presetButtons[k]);
            }

            _toasts = new VisualElement { pickingMode = PickingMode.Ignore }; // cards themselves take clicks
            _toasts.AddToClassList("pg-toasts");
            layer.Add(_toasts);

            RefreshBrush();
        }

        public event Action NewWorldClicked;
        public event Action<PG.Content.PowerDef> UiPowerClicked;
        public event Action UndoClicked;

        // Bölüm 8.2 window / target / toggle powers: handled by the UI, never sent to the simulation.
        public static bool IsUiPower(PG.Content.PowerDef p) =>
            p.ParamString("window") != null || p.ParamString("open") != null || p.ParamString("camera") != null ||
            p.ParamString("control") != null || p.ParamString("highlight") != null || p.ParamNumber("possess", 0f) > 0f;

        bool Shown(PG.Content.PowerDef p) => PowerOps.IsSupported(p, _host.Content) || IsUiPower(p);

        void Pick(int id)
        {
            var p = _host.Content.Powers[id];
            if (IsUiPower(p)) { _tool.SelectedPower = -1; UiPowerClicked?.Invoke(p); }
            else
            {
                _tool.SelectedPower = _tool.SelectedPower == id ? -1 : id;
                _profile?.RememberPower(p.Id);
                RefreshRecent();
            }
            RefreshSelection();
        }

        // Locked powers stay visible with the unlocking achievement in the tooltip (Bölüm 8.2 icon states).
        void Decorate(Button b, PG.Content.PowerDef p)
        {
            var laws = _host.Sim?.Nature.Laws;
            bool locked = laws != null && !PowerLocks.CanUse(p, laws);
            bool scenarioBlocked = _host.Sim != null && !string.IsNullOrEmpty(_host.Sim.ScenarioId)
                && !IsUiPower(p) && !ScenarioPowerAllowed(p);
            b.SetEnabled(!locked && !scenarioBlocked);
            if (scenarioBlocked) { b.tooltip = Loc.T("ui.scenario_power_blocked"); return; }
            if (!locked)
            {
                string description = string.IsNullOrWhiteSpace(p.Description) ? Loc.Name(p) : Loc.T(p.Description);
                b.tooltip = EffectFormatter.WithEffects(description, p.Params);
                return;
            }
            var ach = _host.Content.Achievements.TryGet(p.UnlockedBy, out var a) ? a : null;
            string unlockName = ach != null ? Loc.Name(ach) : p.UnlockedBy;
            string condition = ach?.Condition ?? "";
            if (_profile != null && !_profile.IsDiscovered(p.Id))
            {
                b.AddToClassList("pg-power-button--undiscovered");
                b.tooltip = Loc.F("ui.power_undiscovered_fmt", unlockName, condition);
            }
            else b.tooltip = Loc.F("ui.locked_fmt", unlockName, condition);
        }

        bool ScenarioPowerAllowed(PG.Content.PowerDef power) =>
            !_host.Content.Scenarios.TryGet(_host.Sim.ScenarioId, out var scenario) || ScenarioRules.CanUsePower(scenario, power.Id);
        public event Action SavesClicked;
        public void FocusSearch() => _searchField.Focus();
        public void AttachProfile(Profile profile) { _profile = profile; RefreshFavorites(); RefreshRecent(); }
        void RefreshFavorites()
        {
            _favorites.Clear();
            if (_profile == null) return;
            foreach (var key in _profile.FavoritePowers)
            {
                if (!_host.Content.Powers.TryGet(key, out var def)) continue;
                int id = def.Index;
                var b = MakeButton("★ " + Loc.Name(def), () => Pick(id), PgIcons.Power(def.Id));
                Decorate(b, def);
                b.RegisterCallback<PointerDownEvent>(e => { if (e.button == 1) { _profile.ToggleFavorite(key); _profile.Save(); RefreshFavorites(); e.StopPropagation(); } });
                _favorites.Add(b);
            }
        }

        void RefreshRecent()
        {
            _recent.Clear();
            if (_profile?.RecentPowers == null || _profile.RecentPowers.Count == 0)
            {
                _recent.style.display = DisplayStyle.None;
                return;
            }
            _recent.style.display = DisplayStyle.Flex;
            var title = new Label(Loc.T("ui.recent_powers"));
            title.AddToClassList("pg-recent-powers__label");
            _recent.Add(title);
            for (int i = 0; i < _profile.RecentPowers.Count; i++)
            {
                string key = _profile.RecentPowers[i];
                if (!_host.Content.Powers.TryGet(key, out var def) || IsUiPower(def)) continue;
                int id = def.Index;
                var button = MakeButton(Loc.Name(def), () => Pick(id), PgIcons.Power(def.Id));
                button.AddToClassList("pg-recent-powers__button");
                button.tooltip = Loc.Name(def);
                Decorate(button, def);
                _recent.Add(button);
            }
        }

        public void RebuildPowers()
        {
            var db = _host.Content;
            _visibleTabs.Clear();
            foreach (var tab in TabOrder)
            {
                for (int i = 0; i < db.Powers.Count; i++)
                {
                    var p = db.Powers[i];
                    if (p.Tab == tab && Shown(p))
                    {
                        _visibleTabs.Add(tab);
                        break;
                    }
                }
            }

            _tabs.Clear();
            for (int t = 0; t < _visibleTabs.Count; t++)
            {
                int index = t;
                var b = MakeButton(Loc.T("ui.tab." + _visibleTabs[t]), () => ShowTab(index), PgIcons.Menu("tab_" + _visibleTabs[t]));
                b.AddToClassList("pg-tab");
                b.AddToClassList("pg-category-tab");
                _tabs.Add(b);
            }
            ShowTab(Mathf.Clamp(_tab, 0, Mathf.Max(0, _visibleTabs.Count - 1)));
            RefreshFavorites();
        }

        public void SelectTab(int delta)
        {
            if (_visibleTabs.Count == 0) return;
            ShowTab((_tab + delta + _visibleTabs.Count) % _visibleTabs.Count);
        }

        void ShowTab(int index)
        {
            _tab = index;
            _powerScroll.scrollOffset = Vector2.zero;
            for (int t = 0; t < _tabs.childCount; t++) _tabs[t].EnableInClassList("pg-button--selected", t == index);

            _powers.Clear();
            _powerButtons.Clear();
            _powerIds.Clear();
            if (index >= _visibleTabs.Count) return;

            var db = _host.Content;
            string tab = _visibleTabs[index];
            for (int i = 0; i < db.Powers.Count; i++)
            {
                var p = db.Powers[i];
                if ((string.IsNullOrWhiteSpace(_search) ? p.Tab != tab : !MatchesSearch(p, _search)) || !Shown(p)) continue;
                int id = i;
                var b = MakeButton(Loc.Name(p), () => Pick(id), PgIcons.Power(p.Id));
                b.AddToClassList("pg-power-button");
                Decorate(b, p);
                b.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 1 || _profile == null) return;
                    _profile.ToggleFavorite(db.Powers[id].Id); _profile.Save(); RefreshFavorites(); e.StopPropagation();
                });
                _powers.Add(b);
                _powerButtons.Add(b);
                _powerIds.Add(id);
            }
            if (_powerIds.Count == 0)
            {
                bool searching = !string.IsNullOrWhiteSpace(_search);
                var empty = new Label(Loc.T(searching ? "ui.power_search_empty" : "ui.power_tab_empty"));
                empty.AddToClassList("pg-line");
                _powers.Add(empty);
                if (searching) _powers.Add(MakeButton(Loc.T("ui.power_search_clear"), ClearSearch));
            }
            RefreshSelection();
        }

        void ClearSearch()
        {
            _search = "";
            _searchField.SetValueWithoutNotify("");
            ShowTab(_tab);
        }

        static bool MatchesSearch(PG.Content.PowerDef power, string query)
        {
            query = query?.Trim();
            if (string.IsNullOrEmpty(query)) return true;
            string description = string.IsNullOrWhiteSpace(power.Description) ? "" : Loc.T(power.Description);
            if (Contains(Loc.Name(power), query) || Contains(description, query) ||
                Contains(Loc.T("ui.tab." + power.Tab), query)) return true;

            // IDs keep concise English aliases even when the displayed name is localized.
            string idTerms = (power.Id ?? "").Replace("pw.", "").Replace('_', ' ').Replace('.', ' ');
            return Contains(idTerms, query);
        }

        static bool Contains(string source, string query) =>
            !string.IsNullOrEmpty(source) && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        public void RefreshSelection()
        {
            for (int k = 0; k < _powerButtons.Count; k++)
                _powerButtons[k].EnableInClassList("pg-button--selected", _powerIds[k] == _tool.SelectedPower);
        }

        public bool TrySelectPower(string powerId)
        {
            if (string.IsNullOrWhiteSpace(powerId) || _host?.Sim == null) return false;
            var db = _host.Content;
            for (int i = 0; i < db.Powers.Count; i++)
            {
                var power = db.Powers[i];
                if (power.Id != powerId || !Shown(power)) continue;
                var laws = _host.Sim.Nature.Laws;
                if (laws != null && !PowerLocks.CanUse(power, laws)) return false;
                if (!IsUiPower(power) && !ScenarioPowerAllowed(power)) return false;
                int tab = _visibleTabs.IndexOf(power.Tab);
                if (tab < 0) return false;
                _search = "";
                _searchField.SetValueWithoutNotify("");
                ShowTab(tab);
                if (_tool.SelectedPower != i) Pick(i);
                else RefreshSelection();
                return true;
            }
            return false;
        }

        public void ChangeBrush(int delta)
        {
            _tool.ChangeSize(delta);
            RefreshBrush();
        }

        void ToggleShape()
        {
            _tool.Shape = _tool.Shape == BrushShape.Circle ? BrushShape.Square : BrushShape.Circle;
            RefreshBrush();
        }

        void RefreshBrush()
        {
            _brushSize.text = (_tool.Radius * 2 + 1).ToString();
            _shape.text = Loc.T(_tool.Shape == BrushShape.Circle ? "ui.shape.circle" : "ui.shape.square");
            for (int k = 0; k < _presetButtons.Length; k++)
            {
                if (_presetButtons[k] != null)
                    _presetButtons[k].EnableInClassList("pg-button--selected", _tool.RadiusIndex == PresetRadiusIndices[k]);
            }
        }

        // Bölüm 8.2 notification feed: same-category cards within GroupSeconds merge into one with a counter;
        // a card with a click action moves the camera / opens the related window.
        public const float GroupSeconds = 3f;
        public Func<string, bool> Muted = _ => false;
        public Action Notified;

        public void Toast(string text) => Notify("misc", text, null);

        public void Notify(string category, string text, Action onClick)
        {
            if (Muted(category)) return;
            float now = Time.unscaledTime;
            for (int i = _toastItems.Count - 1; i >= 0; i--)
            {
                var t = _toastItems[i];
                if (t.category != category || category == "misc" || now - t.born > GroupSeconds) continue;
                t.count++;
                t.label.text = text + "  ×" + t.count;
                t.until = now + ToastSeconds;
                t.onClick = onClick ?? t.onClick;
                _toastItems[i] = t;
                return;
            }
            if (category != "misc") Notified?.Invoke();
            var label = new Label(text);
            label.AddToClassList("pg-toast");
            if (onClick != null) label.AddToClassList("pg-toast--link");
            var item = new ToastItem { label = label, until = now + ToastSeconds, born = now, category = category, count = 1, onClick = onClick };
            label.RegisterCallback<PointerDownEvent>(e =>
            {
                int k = _toastItems.FindIndex(x => x.label == label);
                if (k >= 0) { _toastItems[k].onClick?.Invoke(); RemoveToast(k); }
                e.StopPropagation();
            });
            _toasts.Add(label);
            _toastItems.Add(item);
            while (_toastItems.Count > MaxToasts) RemoveToast(0);
        }

        void RemoveToast(int index)
        {
            _toastItems[index].label.RemoveFromHierarchy();
            _toastItems.RemoveAt(index);
        }

        struct ToastItem
        {
            public Label label;
            public float until, born;
            public string category;
            public int count;
            public Action onClick;
        }

        public void Update()
        {
            float now = Time.unscaledTime;
            _undoButton.SetEnabled(_host.CanUndo);
            for (int i = _toastItems.Count - 1; i >= 0; i--)
                if (_toastItems[i].until <= now) RemoveToast(i);

            var clock = _host.Sim?.Clock;
            if (clock == null) return;
            long month = clock.Tick / SimConst.TicksPerMonth;
            if (month != _shownTick)
            {
                _shownTick = month;
                _date.text = Loc.F("ui.date_fmt", clock.Year + 1, clock.Month + 1);
                var era = _host.Sim.Nature.Era;
                string eraName = Loc.Name(_host.Content.Eras[era.CurrentEra]);
                _era.text = era.Frozen ? Loc.F("ui.era_frozen_fmt", eraName)
                                       : Loc.F("ui.era_fmt", eraName, Mathf.Max(0, era.StartedYear + era.DurationYears - clock.Year));

                var sim = _host.Sim;
                int pop = sim != null ? sim.Population : 0;
                int aliveKingdoms = 0;
                if (sim != null && sim.Meta != null)
                {
                    for (int k = 0; k < sim.Meta.Kingdoms.Count; k++)
                        if (sim.Meta.KingdomAlive(k)) aliveKingdoms++;
                }
                _pop.text = $"👥 {pop:N0}";
                _kingdoms.text = $"👑 {aliveKingdoms}";
            }
            if (clock.SpeedIndex != _shownSpeed || clock.SuperSpeed != _shownSuper)
            {
                _shownSpeed = clock.SpeedIndex;
                _shownSuper = clock.SuperSpeed;
                for (int i = 0; i < _speed.Length; i++)
                    _speed[i].EnableInClassList("pg-button--selected", !clock.SuperSpeed && i == clock.SpeedIndex);
                _super.EnableInClassList("pg-button--selected", clock.SuperSpeed);
            }
            _step.SetEnabled(clock.Paused);
        }

        public void ResetCachedState()
        {
            _shownTick = -1;
            _shownSpeed = -1;
        }

        static Button MakeButton(string text, Action onClick, Texture2D icon = null)
        {
            var b = new Button(onClick);
            b.AddToClassList("pg-button");
            PgIcons.AddToButton(b, text, icon);
            return b;
        }
    }
}
