using System.Text;
using PG.Content;
using PG.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 1.15 F3 overlay: developer diagnostics (English on purpose; not player-facing content).
    public sealed class DebugOverlay
    {
        const float RefreshSeconds = 0.25f;

        readonly Label _label;
        readonly StringBuilder _sb = new StringBuilder(1024);
        readonly RollingFrameWindow _frameWindow = new RollingFrameWindow();
        float _next;
        float _fps;

        public DebugOverlay(VisualElement layer)
        {
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList("pg-debug");
            _label.style.display = DisplayStyle.None;
            layer.Add(_label);
        }

        public bool Visible
        {
            get => _label.style.display == DisplayStyle.Flex;
            set => _label.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Update(IGameHost host, Vector2 cursorWorld)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);
                _frameWindow.AddSeconds(dt);
            }
            if (!Visible || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshSeconds;

            var sim = host.Sim;
            if (sim == null) return;
            var map = sim.World;
            var regions = sim.Regions;
            var pipeline = sim.Pipeline;

            _sb.Clear();
            _sb.Append("FPS ").Append(_fps.ToString("0")).Append("   ticks/s ").Append(host.TicksLastSecond)
               .Append("   tick ").Append(pipeline.LastTickMs.ToString("0.000")).Append(" ms\n");
            _sb.Append("frame window ").Append(_frameWindow.Count).Append('/').Append(RollingFrameWindow.Capacity)
               .Append(" avg ").Append(_frameWindow.AverageMilliseconds.ToString("0.00")).Append(" ms / ")
               .Append(_frameWindow.AverageFramesPerSecond.ToString("0.0")).Append(" FPS  slowest ")
               .Append(_frameWindow.SlowestMilliseconds.ToString("0.0")).Append(" ms\n")
               .Append(Application.isEditor ? "Editor" : "Player").Append(Debug.isDebugBuild ? " dev" : " release")
               .Append("  ").Append(Application.platform).Append("  ").Append(Application.version).Append(" / Unity ").Append(Application.unityVersion)
               .Append("  ").Append(SystemInfo.deviceModel).Append("  CPU ").Append(SystemInfo.processorType).Append(" x").Append(SystemInfo.processorCount)
               .Append("  ").Append(SystemInfo.graphicsDeviceType).Append("  ").Append(Screen.width).Append('x').Append(Screen.height)
               .Append("  target FPS ").Append(Application.targetFrameRate).Append('\n');
            for (int i = 0; i < pipeline.Count; i++)
                _sb.Append("  ").Append(pipeline[i].Phase).Append(' ').Append(pipeline[i].GetType().Name).Append(' ')
                   .Append(pipeline.LastMs(i).ToString("0.000")).Append(" ms\n");
            _sb.Append("dirty chunks: render ").Append(map.CountDirty(DirtyMask.Render))
               .Append("  regions ").Append(map.CountDirty(DirtyMask.Regions)).Append('\n');
            _sb.Append("regions ").Append(regions.RegionCount).Append("   islands ").Append(regions.IslandCount).Append('\n');
            _sb.Append("map ").Append(map.Width).Append('x').Append(map.Height).Append("   seed ").Append(sim.Seed).Append('\n');
            var nature = sim.Nature;
            _sb.Append("era ").Append(nature.CurrentEra.Id).Append("  wind ").Append((nature.WindAngle * Mathf.Rad2Deg).ToString("0")).Append("° ")
               .Append(nature.WindSpeed.ToString("0.00")).Append("  clouds ").Append(nature.Clouds.Length).Append("  burning ").Append(nature.Burning.Length)
               .Append("  lava ").Append(nature.Lava.Length).Append('\n');
            var units = sim.Units;
            _sb.Append("units ").Append(units.Store.Count).Append("  corpses ").Append(units.Store.Corpses.Length)
               .Append("  subspecies ").Append(units.Subspecies.Count).Append("  projectiles ").Append(units.Projectiles.Length).Append("  paths/tick ").Append(units.Paths.SolvedThisTick).Append('\n');
            var civ = sim.Civ;
            int cities = 0;
            for (int c = 0; c < civ.CityCount; c++) if (civ.CityAlive(c)) cities++;
            _sb.Append("cities ").Append(cities).Append("  buildings ").Append(civ.Buildings.Length - civ.FreeBuildings.Length)
               .Append("  items ").Append(civ.Items.Length).Append('\n');

            int x = Mathf.FloorToInt(cursorWorld.x), y = Mathf.FloorToInt(cursorWorld.y);
            if (map.InBounds(x, y))
            {
                var db = sim.Content;
                int i = map.Index(x, y);
                byte biome = map.Biome[i];
                ushort feature = map.Feature[i];
                _sb.Append("tile (").Append(x).Append(',').Append(y).Append(") ").Append(db.Tiles[map.Ground[i]].Id)
                   .Append("  variant ").Append(map.Variant[i]).Append('\n');
                _sb.Append("biome ").Append(biome == 0 ? "-" : db.Biomes[biome - 1].Id)
                   .Append("  feature ").Append(feature == 0 ? "-" : db.Features[feature - 1].Id).Append('\n');
                _sb.Append("flags ").Append(((TileFlags)map.Flags[i]).ToString()).Append('\n');
                _sb.Append("zone ").Append(map.ZoneIndexOf(x, y)).Append("  ").Append(map.Zones[map.ZoneIndexOf(x, y)].TemperatureC).Append("°C")
                   .Append("  chunk ").Append(map.ChunkIndexOf(x, y)).Append('\n');
                _sb.Append("region land ").Append(regions.RegionAt(x, y, MoveClass.Land))
                   .Append(" water ").Append(regions.RegionAt(x, y, MoveClass.Water))
                   .Append("  island land ").Append(regions.IslandAt(x, y, MoveClass.Land))
                   .Append(" water ").Append(regions.IslandAt(x, y, MoveClass.Water));
                AppendCity(sim, x, y);
                AppendUnitUnderCursor(sim, cursorWorld);
            }
            _label.text = _sb.ToString();
        }

        void AppendCity(PG.Sim.SimWorld sim, int x, int y)
        {
            var civ = sim.Civ;
            int c = civ.CityAtTile(x, y);
            if (c >= 0 && civ.CityAlive(c))
            {
                ref var city = ref civ.City(c);
                _sb.Append("\ncity #").Append(c).Append(' ').Append(sim.Content.Species[city.Species].Id).Append("  pop ").Append(city.Population)
                   .Append('/').Append(city.Housing).Append("  tier ").Append(city.HallTier).Append("  zones ").Append(civ.CityZones[c].Count)
                   .Append("  gold ").Append(city.Gold).Append("  mood ").Append(city.Happiness)
                   .Append("\n  store ").Append(civ.StoredTotal(c)).Append('/').Append(city.StorageCap).Append("  food ").Append(civ.FoodTotal(c))
                   .Append("  wood ").Append(civ.Res(c, civ.ResWood)).Append("  stone ").Append(civ.Res(c, civ.ResStone));
                var meta = sim.Meta;
                _sb.Append("\n  name ").Append(meta.Names.Get(civ.Cities.Headers[c].NameId)).Append("  loyalty ").Append(city.Loyalty);
                _sb.Append("\n  culture ").Append(MetaName(meta, meta.Cultures, city.Culture)).Append("  language ").Append(MetaName(meta, meta.Languages, city.Language))
                   .Append("  religion ").Append(MetaName(meta, meta.Religions, city.Religion));
                if (meta.KingdomAlive(city.Kingdom))
                {
                    ref var k = ref meta.Kingdom(city.Kingdom);
                    _sb.Append("\nkingdom ").Append(meta.KingdomName(city.Kingdom)).Append("  cities ").Append(k.Cities).Append("  soldiers ").Append(k.Soldiers)
                       .Append("  power ").Append(k.Power.ToString("0")).Append("  wars ").Append(meta.ActiveWars(city.Kingdom)).Append("  alliance ").Append(k.Alliance);
                }
            }
            int b = sim.World.Building[sim.World.Index(x, y)];
            if (b >= 0 && b < civ.Buildings.Length)
            {
                var d = civ.Buildings[b];
                _sb.Append("\nbuilding #").Append(b).Append(' ').Append(sim.Content.Buildings[d.Def].Id).Append("  hp ").Append(d.Hp.ToString("0"))
                   .Append('/').Append(d.MaxHp.ToString("0")).Append("  built ").Append((d.BuildProgress * 100f).ToString("0")).Append('%')
                   .Append("  residents ").Append(d.Residents).Append("  ").Append(d.Flags);
            }
        }

        static string MetaName<T>(PG.Sim.MetaWorld meta, PG.Sim.MetaStore<T> store, int index) where T : unmanaged =>
            index >= 0 && index < store.Count ? meta.Names.Get(store.Headers[index].NameId) : "-";

        void AppendUnitUnderCursor(PG.Sim.SimWorld sim, Vector2 cursor)
        {
            var u = sim.Units.Store;
            int best = -1;
            float bestD = 2.25f;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                float d = (new Vector2(u.Pos[i].x, u.Pos[i].y) - cursor).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return;
            _sb.Append("\nunit #").Append(u.Uid[best]).Append(' ').Append(u.SpeciesOf(best).Id)
               .Append("  age ").Append(u.AgeYears(best, sim.Clock.Tick).ToString("0.0")).Append(' ').Append((PG.Sim.AgeStage)u.Age[best])
               .Append("  hp ").Append(u.Hp[best].ToString("0")).Append('/').Append(u.Stat(best, PG.Content.StatId.Hp).ToString("0"))
               .Append("\n  task ").Append((PG.Sim.UnitTask)u.Task[best]).Append('.').Append(u.Action[best])
               .Append("  sat ").Append(u.Saturation[best]).Append("  energy ").Append(u.Energy[best])
               .Append("  stamina ").Append(u.Stamina[best].ToString("0")).Append("  lvl ").Append(u.Level[best]).Append("  kills ").Append(u.Kills[best])
               .Append("\n  city ").Append(u.City[best]).Append("  job ").Append(u.Job[best] > 0 ? sim.Content.Jobs[u.Job[best] - 1].Id : "-")
               .Append("  home ").Append(u.HomeBuilding[best]).Append("  mood ").Append(u.Happiness[best]).Append("  carry ").Append(u.CarryAmount[best])
               .Append("\n  name ").Append(sim.Meta.Names.Get(u.NameId[best])).Append("  army ").Append(u.Army[best])
               .Append("\n  culture ").Append(MetaName(sim.Meta, sim.Meta.Cultures, u.Culture[best])).Append("  religion ").Append(MetaName(sim.Meta, sim.Meta.Religions, u.Religion[best]))
               .Append("  clan ").Append(MetaName(sim.Meta, sim.Meta.Clans, u.Clan[best])).Append("  disease ").Append(u.Disease[best] > 0 ? sim.Content.Diseases[u.Disease[best] - 1].Id : "-");
        }
    }
}
