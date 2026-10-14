using PG.Content;
using PG.Powers;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PG.UI
{
    public sealed class PossessionController
    {
        readonly UiContext _ui;
        readonly Label _hud;
        readonly Button _exit; // touch users have no Escape key (Bölüm 1.14)
        int _unit = -1, _speed;
        long _uid;
        bool _super;
        public bool Active => _unit >= 0;
        public PossessionController(UiContext ui, VisualElement layer)
        {
            _ui = ui; _hud = new Label { pickingMode = PickingMode.Ignore }; _hud.AddToClassList("pg-possession"); layer.Add(_hud); _hud.style.display = DisplayStyle.None;
            if (PlatformInfo.IsTouchDevice)
            {
                _exit = new Button(() => Release()) { text = "X" };
                _exit.AddToClassList("pg-possessionexit");
                layer.Add(_exit);
                _exit.style.display = DisplayStyle.None;
            }
        }
        public void Begin(int unit)
        {
            if (!_ui.Sim.Units.Store.IsAlive(unit)) return;
            Release();
            _unit = unit; _uid = _ui.Sim.Units.Store.Uid[unit]; _ui.PossessedUid = _uid;
            _speed = _ui.Sim.Clock.SpeedIndex; _super = _ui.Sim.Clock.SuperSpeed;
            _ui.Sim.Clock.SuperSpeed = false; _ui.Sim.Clock.SetSpeed(1);
            _ui.Camera.ZoomTo(6); _hud.style.display = DisplayStyle.Flex;
            if (_exit != null) _exit.style.display = DisplayStyle.Flex;
        }
        public void Release(bool worldChanged = false)
        {
            if (!Active) return;
            if (!worldChanged)
            {
                _ui.Host.Edits.Edits.Add(new EditCommand { Kind = EditKind.ReleaseControl, Target = _unit, ExpectedUid = _uid });
                _ui.Host.Edits.Control = null;
                _ui.Sim.Clock.SetSpeed(_speed); _ui.Sim.Clock.SuperSpeed = _super;
            }
            _unit = -1; _ui.PossessedUid = 0; _hud.style.display = DisplayStyle.None;
            if (_exit != null) _exit.style.display = DisplayStyle.None;
        }
        public void Update(GameInput input, Vector2 aim, bool blocked)
        {
            if (!Active) return;
            var u = _ui.Sim.Units.Store;
            if (!u.IsAlive(_unit) || u.Uid[_unit] != _uid)
            { int died = _unit; Release(); _ui.Toast(Loc.T("ui.possession_died")); _ui.OpenUnit(died); return; }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) { Release(); return; }
            _ui.Sim.Clock.SetSpeed(1); _ui.Sim.Clock.SuperSpeed = false;
            _ui.Camera.Focus(u.Pos[_unit]);
            _hud.text = Names.Unit(_ui.Sim, _unit) + " · " + Loc.T("ui.hp") + " " + u.Hp[_unit].ToString("0") + " · " + Loc.T("ui.possession_help");
            var old = _ui.Host.Edits.Control;
            _ui.Host.Edits.Control = new ControlCommand
            {
                Unit = _unit, ExpectedUid = _uid, Move = blocked ? float2.zero : (float2)input.Pan.ReadValue<Vector2>(), AimAt = new float2(aim.x, aim.y),
                Attack = !blocked && input.UsePower.IsPressed(),
                Dash = !blocked && (Keyboard.current?.spaceKey.wasPressedThisFrame == true || old?.Dash == true),
                Spell = (sbyte)(!blocked && (Keyboard.current?.digit1Key.wasPressedThisFrame == true || old?.Spell >= 0) ? 0 : -1),
            };
        }
    }
}
