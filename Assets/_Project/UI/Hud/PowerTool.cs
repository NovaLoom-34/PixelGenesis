using PG.Core;
using PG.Powers;
using System;
using Unity.Mathematics;
using UnityEngine;

namespace PG.UI
{
    // Turns pointer input into PowerCommands. Held brushes re-apply every `interval` ticks' worth of real time
    // (params.add.interval); direct painting emits whenever the cursor reaches a new tile.
    public sealed class PowerTool
    {
        public int SelectedPower = -1;
        public int RadiusIndex = 2;
        public BrushShape Shape = BrushShape.Circle;

        Vector2Int _last, _first;
        bool _stroking, _blocked, _haveFirst;
        float _nextApply;
        bool _undoCaptured;
        public Action BeforeMajorAction;

        public int Radius => Brushes.Radii[RadiusIndex];
        public bool Active => SelectedPower >= 0;

        public void ChangeSize(int delta) => RadiusIndex = Mathf.Clamp(RadiusIndex + delta, 0, Brushes.Radii.Length - 1);

        public void Cancel()
        {
            SelectedPower = -1;
            _stroking = false;
            _haveFirst = false;
        }

        // Bölüm 7.5 two-click powers: kingdom pairs (friendship, spite) and moving a unit to another city.
        public static bool TwoClick(PG.Content.PowerDef def) =>
            def.ParamString("target") == "kingdom_pair" || def.ParamNumber("changeKingdom", 0f) > 0f;

        public bool WaitingSecondClick => _haveFirst;

        public void Update(IGameHost host, Vector2Int tile, bool held, bool pressedThisFrame, bool pointerOverUi)
        {
            if (!Active || host.Sim == null || !held)
            {
                _stroking = false;
                _blocked = false;
                _undoCaptured = false;
                return;
            }
            if (pressedThisFrame) _blocked = pointerOverUi; // a press that starts on the UI never paints
            if (_blocked) return;

            var def = host.Content.Powers[SelectedPower];
            int ticks = PowerOps.IntervalTicks(def, host.Content);
            float interval = ticks * SimConst.TickDt;
            float now = Time.unscaledTime;

            if (!_stroking && TwoClick(def))
            {
                _stroking = true;
                if (!_haveFirst) { _first = tile; _haveFirst = true; return; }
                _haveFirst = false;
                Emit(host, _first, tile);
                return;
            }
            if (!_stroking)
            {
                Emit(host, tile, tile);
                _stroking = true;
                _nextApply = now + interval;
                return;
            }

            if (ticks < 0) return; // point powers (clouds, seeds): once per press
            bool due = interval <= 0f ? tile != _last : now >= _nextApply;
            if (!due) return;
            Emit(host, _last, tile);
            _nextApply = now + interval;
        }

        void Emit(IGameHost host, Vector2Int from, Vector2Int to)
        {
            var power = host.Content.Powers[SelectedPower];
            if (!_undoCaptured && NeedsUndoSnapshot(power, Radius))
            {
                _undoCaptured = true;
                BeforeMajorAction?.Invoke();
            }
            host.Commands.Enqueue(new PowerCommand
            {
                PowerId = (ushort)SelectedPower,
                From = new int2(from.x, from.y),
                To = new int2(to.x, to.y),
                Brush = new BrushSpec(Shape, Radius),
                Tick = host.Sim.Clock.Tick,
            });
            _last = to;
        }

        static bool NeedsUndoSnapshot(PG.Content.PowerDef power, int radius)
        {
            if (radius >= 8 || power.Tab == "destruction") return true;
            switch (power.Id)
            {
                case "pw.earthquake": case "pw.tsunami": case "pw.volcano": case "pw.tornado":
                case "pw.flood": case "pw.plague": case "pw.storm": case "pw.atomic": case "pw.antimatter":
                case "pw.black_hole": case "pw.ice_bomb": return true;
                default: return false;
            }
        }
    }
}
