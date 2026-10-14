namespace PG.Render
{
    // Centralized 2D Sorting Layer names and in-layer Z/order constants.
    // Configured in TagManager.asset and Renderer2D.asset (Top-down CustomAxis Y-sorting).
    public static class SortingLayers
    {
        // 2D Sorting Layer Names (TagManager.asset)
        public const string Default = "Default";
        public const string Water = "Water";
        public const string Terrain = "Terrain";
        public const string Decals = "Decals";
        public const string Features = "Features";
        public const string Buildings = "Buildings";
        public const string Units = "Units";
        public const string Projectiles = "Projectiles";
        public const string Weather = "Weather";
        public const string DayNightLight = "DayNightLight";
        public const string WorldUI = "WorldUI";

        // Sorting Orders within layers
        public const int OrderTerrain = 0;
        public const int OrderCityBorders = 5;
        public const int OrderBuildingShadows = 9;
        public const int OrderFeatures = 10;
        public const int OrderBuildings = 12;
        public const int OrderUnits = 20;
        public const int OrderProjectiles = 22;
        public const int OrderOverlay = 26;
        public const int OrderFxOverlay = 27;
        public const int OrderPowerShadow = 28;
        public const int OrderCloudShadow = 29;
        public const int OrderClouds = 60;
        public const int OrderDayNightLight = 65;
        public const int OrderPowerFx = 70;

        // Depth sorting: trees, buildings, units and vehicles write their foot-line Y into world Z (south = nearer the
        // camera) and share the depth buffer, so a unit behind a tree is hidden by it and one in front draws over it.
        // Z stays >= 0 so every flat layer at Z = 0 (overlays, FX, projectiles) always passes the depth test.
        public const float DepthPerTile = 0.004f;
        public static float DepthZ(float footY) => footY * DepthPerTile;
    }
}
