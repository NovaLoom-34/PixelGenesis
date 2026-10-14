using UnityEngine;

namespace PG.UI
{
    // "Koyu Zarif" theme tokens (DECISIONS #99). Mirror of the :root block in Resources/PG_Hud.uss —
    // change the two together. Data-driven colors (kingdom banners, biome tiles) never come from here.
    public static class UiTheme
    {
        public static readonly Color Gold = new Color32(246, 210, 138, 255);
        public static readonly Color GoldBorder = new Color32(168, 134, 72, 255);
        public static readonly Color Select = new Color32(62, 114, 176, 255);
        public static readonly Color Danger = new Color32(156, 62, 52, 255);
        public static readonly Color Success = new Color32(94, 178, 114, 255);
        public static readonly Color Needle = new Color32(240, 244, 248, 255);
        public static readonly Color Frustum = new Color(0.94f, 0.97f, 1f, 0.9f);

        // unit / city meters
        public static readonly Color BarHp = new Color32(206, 78, 72, 255);
        public static readonly Color BarMana = new Color32(84, 122, 226, 255);
        public static readonly Color BarStamina = new Color32(226, 196, 88, 255);
        public static readonly Color BarSaturation = new Color32(128, 186, 92, 255);
        public static readonly Color BarEnergy = new Color32(158, 132, 216, 255);
        public static readonly Color BarLoyalty = Success;
        public static readonly Color OpinionPos = Success;
        public static readonly Color OpinionNeg = BarHp;
        public static readonly Color SpeciesBar = new Color32(110, 150, 210, 255);

        // era dial
        public static readonly Color DialActive = Gold;
        public static readonly Color DialSet = new Color32(88, 110, 144, 255);
        public static readonly Color DialIdle = new Color32(38, 45, 58, 255);

        // graph series palette (max 8 lines visible)
        public static readonly Color[] GraphPalette =
        {
            new Color32(240, 200, 96, 255), new Color32(96, 156, 240, 255), new Color32(232, 108, 92, 255),
            new Color32(108, 200, 120, 255), new Color32(190, 128, 236, 255), new Color32(240, 152, 76, 255),
            new Color32(104, 210, 214, 255), new Color32(214, 220, 230, 255),
        };
    }
}
