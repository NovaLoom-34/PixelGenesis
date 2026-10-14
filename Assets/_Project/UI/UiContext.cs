using System;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;

namespace PG.UI
{
    // What windows need from the UI shell: host, window manager, camera, possession, profile and the openers.
    public sealed class UiContext
    {
        public IGameHost Host;
        public WindowManager Windows;
        public CameraController Camera;
        public Profile Profile;
        public WorldGenSettings WorldSettings;
        public Action<string> Toast;
        public Action<int> Possess;
        public Action SettingsChanged;
        public Func<string, bool> SelectPower;
        public Action<float2, string> ShowHistoryMarker;
        public int Followed = -1; // unit the camera follows, -1 = none
        public long PossessedUid;  // 0 = nobody possessed
        public int LawsOpened, CityInspected; // tutorial conditions

        public SimWorld Sim => Host.Sim;

        public void OpenUnit(int unit)
        {
            var u = Sim.Units.Store;
            if (unit < 0 || unit >= u.HighWater || u.State[unit] == UnitStore.StateFree) return;
            long uid = u.Uid[unit];
            Windows.Show(("unit", uid), () => new UnitWindow(this, unit));
        }

        public void OpenMeta(MetaKind kind, int index)
        {
            if (index < 0) return;
            if (kind == MetaKind.City) CityInspected++;
            Windows.Show(("meta", kind, index), () => new MetaWindow(this, kind, index));
        }

        public void OpenTraitEditor(MetaKind kind, int target, bool unit)
        {
            if (target < 0) return;
            Windows.Show(("traits", unit, kind, target), () => new TraitEditorWindow(this, kind, target, unit));
        }

        public void GoTo(float2 at) => Camera?.Focus(at);
    }
}
