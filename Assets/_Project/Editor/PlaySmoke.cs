using PG.Boot;
using PG.Persistence;
using PG.Powers;
using PG.UI;
using PG.Sim;
using System.IO;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace PG.EditorTools
{
    // Plays the GameBootstrap scene for a few hundred frames, drives brush/save/load through the host
    // and counts console errors. Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.PlaySmoke.Run
    [InitializeOnLoad]
    public static class PlaySmoke
    {
        const string ActiveKey = "PG.PlaySmoke.Active";
        const string FramesKey = "PG.PlaySmoke.Frames";
        const string ErrorsKey = "PG.PlaySmoke.Errors";
        const string ScenePath = "Assets/Scenes/GameBootstrap.unity";
        const int SmokeSlot = 901;
        const int FramesToRun = 400;
        static Task _saveTask, _loadTask;
        static PanelSettings _hudPanel;
        static RenderTexture _hudTarget;

        static PlaySmoke()
        {
            if (SessionState.GetBool(ActiveKey, false)) Hook();
        }

        [MenuItem("PixelGenesis/Play Smoke Run")]
        public static void Run()
        {
            DataSync.Sync();
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(FramesKey, 0);
            SessionState.SetInt(ErrorsKey, 0);
            _saveTask = _loadTask = null;
            Hook();
            EditorApplication.EnterPlaymode();
        }

        static void Hook()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetInt(ErrorsKey, SessionState.GetInt(ErrorsKey, 0) + 1);
        }

        static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            int frame = SessionState.GetInt(FramesKey, 0) + 1;
            SessionState.SetInt(FramesKey, frame);

            var host = Object.FindFirstObjectByType<GameBootstrap>();
            if (host != null && host.Sim != null)
            {
                if (frame == 30) host.Sim.Clock.SetSpeed(6);
                if (System.Environment.GetEnvironmentVariable("PG_HUD_CAPTURE") == "1")
                {
                    if (frame == 32) BeginHudCapture();
                    if (frame == 60) SaveHudCapture();
                }
                if (frame >= 40 && frame < 120 && frame % 4 == 0) Brush(host, frame);
                if (frame == 100 && host.Renderer != null) host.Renderer.ShowRegions = true;
                if (frame == 125) Fx(host); // Bölüm 7 renderers: drops, objects, flashes, automata
                if (frame == 150) _saveTask = host.SaveToSlot(SmokeSlot);
                if (frame >= 250 && _loadTask == null && _saveTask != null && _saveTask.IsCompleted) _loadTask = host.LoadSlot(SmokeSlot);
                if (frame >= 160 && frame <= 240) Windows(frame, host);
            }

            if (frame < FramesToRun || (frame < 2000 && (_loadTask == null || !_loadTask.IsCompleted))) return;
            if (_saveTask == null || !_saveTask.IsCompletedSuccessfully || _loadTask == null || !_loadTask.IsCompletedSuccessfully)
                Debug.LogError("[PlaySmoke] Save/load did not complete successfully");
            int errors = SessionState.GetInt(ErrorsKey, 0);
            SessionState.SetBool(ActiveKey, false);
            SaveSystem.Delete(SmokeSlot);
            Debug.Log(errors == 0
                ? $"[PlaySmoke] OK: {frame} frames, tick {host?.Sim?.Clock.Tick}, regions {host?.Sim?.Regions.RegionCount}"
                : $"[PlaySmoke] {errors} error(s) logged");
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        static void BeginHudCapture()
        {
            var ui = Object.FindFirstObjectByType<UiRoot>();
            _hudPanel = ui != null ? ui.GetComponent<UIDocument>()?.panelSettings : null;
            if (_hudPanel == null) { Debug.LogError("[PlaySmoke] HUD panel is missing"); return; }
            _hudTarget = new RenderTexture(1465, 744, 24, RenderTextureFormat.ARGB32);
            _hudTarget.Create();
            _hudPanel.targetTexture = _hudTarget;
        }

        static void SaveHudCapture()
        {
            if (_hudTarget == null) return;
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderTexture.active = _hudTarget;
                pixels = new Texture2D(_hudTarget.width, _hudTarget.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, _hudTarget.width, _hudTarget.height), 0, 0);
                pixels.Apply();
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/hud-redesign-ui.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Debug.Log("[PlaySmoke] HUD screenshot: " + path);
                SaveWorldCapture(Path.Combine(Path.GetDirectoryName(path), "hud-redesign-world.png"));
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.Destroy(pixels);
                _hudPanel.targetTexture = null;
                _hudTarget.Release();
                Object.Destroy(_hudTarget);
                _hudTarget = null;
                _hudPanel = null;
            }
        }

        static void SaveWorldCapture(string path)
        {
            var camera = Camera.main;
            if (camera == null) throw new IOException("[PlaySmoke] World camera is missing");
            var target = new RenderTexture(1465, 744, 24);
            var pixels = new Texture2D(1465, 744, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            float aspect = camera.aspect;
            try
            {
                camera.aspect = 1465f / 744f;
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new IOException("[PlaySmoke] Render pipeline cannot capture the world camera");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1465, 744), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.aspect = aspect;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        static void Windows(int frame, GameBootstrap host)
        {
            var root = Object.FindFirstObjectByType<UiRoot>();
            var ui = root?.Context;
            if (ui == null) { Debug.LogError("[PlaySmoke] UI context is missing"); return; }
            int unit = host.Sim.Units.Store.Alive.Length > 0 ? host.Sim.Units.Store.Alive[0] : -1;
            if (frame == 160 && unit >= 0) ui.OpenUnit(unit);
            if (frame == 164 && unit >= 0) ui.OpenTraitEditor(MetaKind.Family, unit, true);
            if (frame == 168 && unit >= 0) ui.Windows.Show("smoke-item", () => new ItemEditorWindow(ui, unit));
            if (frame == 172 && unit >= 0)
            {
                int sub = host.Sim.Units.Store.Subspecies[unit];
                if (host.Sim.Units.Subspecies.IsAlive(sub)) { ui.Windows.Show("smoke-gene", () => new GeneEditorWindow(ui, sub)); ui.OpenMeta(MetaKind.Subspecies, sub); }
            }
            if (frame == 176) ui.Windows.Show("smoke-laws", () => new LawsWindow(ui));
            if (frame == 180) ui.Windows.Show("smoke-history", () => new HistoryWindow(ui));
            if (frame == 184) ui.Windows.Show("smoke-stats", () => new StatsWindow(ui));
            if (frame == 188) ui.Windows.Show("smoke-settings", () => new SettingsWindow(ui));
            if (frame == 192) ui.Windows.Show("smoke-eras", () => new EraClockWindow(ui));
            if (frame == 196) ui.Windows.Show("smoke-graphs", () => new GraphsWindow(ui, new StatsRecorder()));
            if (frame == 200) ui.Windows.Show("smoke-ach", () => new AchievementsWindow(ui));
            if (frame == 204) ui.Windows.Show("smoke-overlays", () => new OverlayWindow(ui));
            if (frame >= 206 && frame < 206 + 12 && host.Overlay != null) { host.Overlay.Modes.Clear(); host.Overlay.Toggle((PG.Render.OverlayMode)(frame - 206)); }
            if (frame == 220)
            {
                var meta = host.Sim.Meta;
                for (int k = 0; k < meta.Kingdoms.Count; k++)
                    if (meta.KingdomAlive(k)) { ui.OpenMeta(MetaKind.Kingdom, k); int kk = k; ui.Windows.Show("smoke-control", () => new MetaControlWindow(ui, kk)); break; }
                for (int c = 0; c < host.Sim.Civ.CityCount; c++) if (host.Sim.Civ.CityAlive(c)) { ui.OpenMeta(MetaKind.City, c); break; }
                if (meta.Cultures.Count > 0) ui.OpenMeta(MetaKind.Culture, 0);
            }
            if (frame == 222 && unit >= 0)
            {
                ui.Windows.CloseAll();
                Vector2 at = host.Sim.Units.Store.Pos[unit];
                foreach (var p in host.Content.Powers.All)
                    if (HudView.IsUiPower(p) && p.ParamNumber("possess", 0f) <= 0f && p.ParamString("window") != "save" && p.ParamString("window") != "load" && p.ParamString("window") != "worldgen")
                        root.UseUiPower(p, at);
                ui.Windows.CloseAll();
                Debug.Log("[PlaySmoke] UI powers used");
            }
            if (frame == 224 && unit >= 0 && host.Sim.Units.Store.IsAlive(unit)) ui.Possess?.Invoke(unit);
            if (frame == 235) ScreenCapture.CaptureScreenshot("Logs/continue-ui.png");
            if (frame == 240) { ui.Windows.CloseAll(); host.Overlay?.Modes.Clear(); Debug.Log("[PlaySmoke] UI windows opened and closed"); }
        }

        static void Fx(GameBootstrap host)
        {
            var db = host.Content;
            var map = host.Sim.World;
            int n = 0;
            foreach (var id in new[] { "pw.bomb", "pw.tornado", "pw.life_game", "pw.ant", "pw.black_hole", "pw.cluster", "pw.volcano" })
            {
                if (!db.Powers.TryGet(id, out var p)) continue;
                var at = new int2(map.Width / 3 + n * 14, map.Height / 3);
                host.Commands.Enqueue(new PowerCommand { PowerId = (ushort)p.Index, From = at, To = at, Brush = new BrushSpec(BrushShape.Circle, 3), Tick = host.Sim.Clock.Tick });
                n++;
            }
        }

        static void Brush(GameBootstrap host, int frame)
        {
            var db = host.Content;
            int power = -1;
            for (int i = 0; i < db.Powers.Count && power < 0; i++)
                if (PowerOps.IsSupported(db.Powers[i], db) && (frame / 4) % 3 == i % 3) power = i;
            if (power < 0) return;
            var map = host.Sim.World;
            var at = new int2(map.Width / 2 + frame % 40, map.Height / 2);
            host.Commands.Enqueue(new PowerCommand
            {
                PowerId = (ushort)power,
                From = at,
                To = at + new int2(12, 6),
                Brush = new BrushSpec(BrushShape.Circle, 5),
                Tick = host.Sim.Clock.Tick,
            });
        }
    }
}
