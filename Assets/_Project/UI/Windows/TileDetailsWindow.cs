using PG.World;
using PG.Sim;
using UnityEngine;

namespace PG.UI
{
    // Click-to-inspect details for the selected map tile; this intentionally does no per-frame pointer work.
    public sealed class TileDetailsWindow : WindowBase
    {
        readonly UiContext _ui;
        Vector2Int _tile;
        int _unit = -1;
        long _unitUid;

        public TileDetailsWindow(UiContext ui, Vector2Int tile, int unit) : base(ui.Host, Loc.T("ui.tile_details"))
        {
            _ui = ui;
            Root.AddToClassList("pg-tile-details");
            UpdateTarget(tile, unit);
        }

        public void SetTarget(Vector2Int tile, int unit)
        {
            UpdateTarget(tile, unit);
            Rebuild();
        }

        void UpdateTarget(Vector2Int tile, int unit)
        {
            _tile = tile;
            var store = Host.Sim?.Units?.Store;
            _unit = store != null && unit >= 0 && store.IsAlive(unit) ? unit : -1;
            _unitUid = _unit >= 0 ? store.Uid[_unit] : 0;
            SetTitle(Loc.F("ui.tile_details_fmt", tile.x, tile.y));
        }

        protected override void Build()
        {
            var sim = Host.Sim;
            var map = sim.World;
            if (!map.InBounds(_tile.x, _tile.y)) return;

            int index = _tile.y * map.Width + _tile.x;
            var groundId = map.Ground[index];
            var biomeId = map.Biome[index];
            string ground = groundId < Host.Content.Tiles.Count ? Loc.Name(Host.Content.Tiles[groundId]) : "—";
            string biome = biomeId > 0 && biomeId - 1 < Host.Content.Biomes.Count
                ? Loc.Name(Host.Content.Biomes[biomeId - 1])
                : Loc.T("ui.tile_no_biome");
            int zoneX = _tile.x / WorldMap.ZoneSize;
            int zoneY = _tile.y / WorldMap.ZoneSize;
            int zone = zoneY * map.ZonesX + zoneX;
            int temperature = zone < map.Zones.Length ? map.Zones[zone].TemperatureC : 20;
            ushort protectedSpecies = zone < map.Zones.Length ? map.Zones[zone].ProtectedSpecies : (ushort)0;

            Line(Loc.F("ui.tile_ground_fmt", ground));
            Line(Loc.F("ui.tile_biome_fmt", biome));
            Line(Loc.F("ui.tile_temperature_fmt", temperature));
            if (protectedSpecies > 0 && protectedSpecies - 1 < Host.Content.Species.Count)
                Line(Loc.F("ui.tile_protection_status_fmt", Loc.Name(Host.Content.Species[protectedSpecies - 1])));

            if (sim.Civ != null)
            {
                int city = sim.Civ.CityAtTile(_tile.x, _tile.y);
                if (city >= 0 && sim.Civ.CityAlive(city))
                {
                    int target = city;
                    Body.Add(Raw(Loc.F("ui.tile_city_fmt", Names.City(sim, target)), () => _ui.OpenMeta(MetaKind.City, target)));
                }
            }

            var store = sim.Units?.Store;
            if (store != null && _unit >= 0 && store.IsAlive(_unit) && store.Uid[_unit] == _unitUid)
            {
                int target = _unit;
                string species = store.Species[target] < Host.Content.Species.Count
                    ? Loc.Name(Host.Content.Species[store.Species[target]])
                    : "";
                Body.Add(Raw(Loc.F("ui.tile_unit_fmt", Names.Unit(sim, target), species, (int)store.Hp[target]),
                    () => _ui.OpenUnit(target)));
                if (store.Species[target] < Host.Content.Species.Count)
                {
                    int speciesIndex = store.Species[target];
                    int desired = protectedSpecies == speciesIndex + 1 ? 0 : speciesIndex + 1;
                    string key = desired == 0 ? "ui.tile_protection_remove" : "ui.tile_protection_add_fmt";
                    string label = desired == 0 ? Loc.T(key) : Loc.F(key, species);
                    Body.Add(Raw(label, () => Host.Edits.Edits.Add(new PG.Powers.EditCommand
                    {
                        Kind = PG.Powers.EditKind.SetProtectedZone,
                        Target = zone,
                        A = desired,
                    })));
                }
            }
            else
            {
                Line(Loc.T("ui.tile_no_unit"));
            }
        }
    }
}
