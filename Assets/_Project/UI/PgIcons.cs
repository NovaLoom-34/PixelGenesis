using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Final tool icons take precedence over the legacy 16x16 placeholders.
    // the 4 Hz window/HUD rebuild must never re-decode PNGs. Missing files cache as null and the
    // button simply renders text-only.
    public static class PgIcons
    {
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D Power(string powerId)
        {
            int dot = powerId.IndexOf('.');
            return Get("powers", dot < 0 ? powerId : powerId.Substring(dot + 1));
        }

        public static Texture2D Menu(string name) => Get("menu", name);

        // Shared window actions use one meaning and silhouette wherever they appear.
        // Header controls (pin/close) stay text-only so compact window chrome keeps its size.
        public static string MenuForUiKey(string key)
        {
            if (key.StartsWith("ui.overlay.")) return "overlays";
            switch (key)
            {
                case "ui.continue": return "continue";
                case "ui.new_world": return "new_world";
                case "ui.load": return "load";
                case "ui.settings": return "settings";
                case "ui.achievements": return "achievements";
                case "ui.play_now": return "play_now";
                case "ui.quit": return "quit";
                case "ui.favorite": return "favorite";
                case "ui.edit_traits": case "ui.edit_genes": case "ui.edit_item": return "edit";
                case "ui.rename": return "rename";
                case "ui.follow": return "follow";
                case "ui.possess": return "possess";
                case "ui.bless": return "bless";
                case "ui.curse": return "curse";
                case "ui.kill": return "kill";
                case "ui.apply": return "apply";
                case "ui.prev": return "prev";
                case "ui.next": return "next";
                case "ui.gov.accept": return "accept";
                case "ui.gov.reject": return "reject";
                case "ui.go_to": return "go_to";
                case "ui.era_freeze": case "ui.era_unfreeze": return "freeze";
                case "ui.overlay_off": return "overlay_off";
                case "ui.tutorial_skip": return "skip";
                case "ui.gov.save_connection": return "connection_save";
                case "ui.gov.test_connection": return "connection_test";
                default: return null;
            }
        }

        public static void AddToButton(Button button, string label, Texture2D icon)
        {
            if (icon == null) { button.text = label; return; }
            button.text = string.Empty;
            button.AddToClassList("pg-iconbtn");
            var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("pg-power-icon");
            var text = new Label(label) { pickingMode = PickingMode.Ignore };
            text.AddToClassList("pg-btnlabel");
            button.Add(image);
            button.Add(text);
        }

        public static void SetButtonLabel(Button button, string label)
        {
            var text = button.Q<Label>(className: "pg-btnlabel");
            if (text != null) text.text = label;
            else button.text = label;
        }

        static Texture2D Get(string category, string name)
        {
            var key = category + "/" + name;
            if (_cache.TryGetValue(key, out var tex)) return tex;
            var path = Path.Combine(Application.streamingAssetsPath, "Art", "Final", "icons", category, name + ".png");
            bool final = PG.Content.ContentFiles.Exists(path);
            if (!final)
                path = Path.Combine(Application.streamingAssetsPath, "Art", "Placeholder", "icons", category, name + ".png");
            if (PG.Content.ContentFiles.Exists(path))
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = final ? FilterMode.Bilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.LoadImage(PG.Content.ContentFiles.ReadAllBytes(path));
            }
            _cache[key] = tex;
            return tex;
        }
    }
}
