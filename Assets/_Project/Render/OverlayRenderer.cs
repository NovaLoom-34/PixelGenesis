using System.Collections.Generic;
using PG.Sim;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Render
{
    public enum OverlayMode : byte { Kingdom, City, Alliance, Culture, Language, Religion, Subspecies, Clan, Temperature, Biome, Population, Disease, Moisture, Season, CropYield, Protection }

    // Bölüm 8.7: zone-resolution overlay (2x2 px per zone: 35% fill, full-alpha outer pixels on meta borders).
    // Several active modes alternate once a second. Reads the simulation only.
    public sealed class OverlayRenderer : MonoBehaviour
    {
        public const int Order = SortingLayers.OrderOverlay, PxPerZone = 2;
        public const float RefreshSeconds = 0.5f;
        const byte FillAlpha = 90, BorderAlpha = 230;
        static readonly Color32[] ProtectionColors =
        {
            new Color32(64, 224, 184, 255), new Color32(112, 184, 255, 255),
            new Color32(235, 184, 69, 255), new Color32(211, 122, 235, 255), new Color32(245, 122, 107, 255),
        };
        static readonly Color32[] ColorBlindIdentityColors =
        {
            new Color32(230, 159, 0, 255), new Color32(86, 180, 233, 255),
            new Color32(0, 158, 115, 255), new Color32(240, 228, 66, 255),
            new Color32(0, 114, 178, 255), new Color32(213, 94, 0, 255),
            new Color32(204, 121, 167, 255), new Color32(245, 245, 245, 255),
        };

        public readonly List<OverlayMode> Modes = new List<OverlayMode>();
        public bool UseColorBlindIdentityPalette { get; set; }
        public bool HighContrast { get; set; }

        SimWorld _sim;
        Texture2D _texture;
        Material _material;
        NativeArray<Color32> _pixels;
        int[] _zoneKey, _voteKey, _voteCount;
        float _next;
        GameObject _quad;

        public void Bind(SimWorld sim)
        {
            Unbind();
            _sim = sim;
            var map = sim.World;
            int zx = map.ZonesX, zy = map.ZonesY, zones = zx * zy;
            _zoneKey = new int[zones]; _voteKey = new int[zones]; _voteCount = new int[zones];
            _texture = new Texture2D(zx * PxPerZone, zy * PxPerZone, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Overlay" };
            _pixels = new NativeArray<Color32>(_texture.width * _texture.height, Allocator.Persistent);
            if (_material == null) _material = new Material(Resources.Load<Shader>("PG_Sprite")) { name = "Overlay" };
            _material.mainTexture = _texture;
            if (_quad == null)
            {
                _quad = new GameObject("OverlayQuad");
                _quad.transform.SetParent(transform, false);
                _quad.AddComponent<MeshFilter>();
                var mr = _quad.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _material;
                mr.sortingLayerName = SortingLayers.WorldUI;
                mr.sortingOrder = Order;
            }
            float w = zx * WorldMap.ZoneSize, h = zy * WorldMap.ZoneSize;
            var mesh = new Mesh { name = "OverlayQuad" };
            mesh.SetVertices(new List<Vector3> { new Vector3(0, 0), new Vector3(w, 0), new Vector3(w, h), new Vector3(0, h) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetColors(new List<Color32> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            var mf = _quad.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = mesh;
            _next = 0f;
        }

        public void Unbind()
        {
            _sim = null;
            if (_pixels.IsCreated) _pixels.Dispose();
            if (_texture != null) Destroy(_texture);
            _texture = null;
        }

        public OverlayMode Current => Modes.Count == 0 ? OverlayMode.Kingdom : Modes[(int)Time.unscaledTime % Modes.Count];

        public void Toggle(OverlayMode mode)
        {
            if (!Modes.Remove(mode)) Modes.Add(mode);
            _next = 0f;
        }

        void LateUpdate()
        {
            if (_quad != null) _quad.SetActive(_sim != null && Modes.Count > 0);
            if (_sim == null || Modes.Count == 0 || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshSeconds;
            Fill(Current);
            Paint(Current);
        }

        // Zone key: meta index for meta modes (-1 = none), a bucket for scalar modes.
        void Fill(OverlayMode mode)
        {
            var map = _sim.World;
            int zones = _zoneKey.Length;
            switch (mode)
            {
                case OverlayMode.Kingdom:
                case OverlayMode.City:
                case OverlayMode.Alliance:
                    var civ = _sim.Civ;
                    for (int z = 0; z < zones; z++)
                    {
                        int c = map.Zones[z].OwnerCity;
                        int key = c >= 0 && civ != null && civ.CityAlive(c) ? c : -1;
                        if (key >= 0 && mode != OverlayMode.City) key = civ.KingdomOf(key);
                        if (key >= 0 && mode == OverlayMode.Alliance) key = _sim.Meta.Kingdoms.IsAlive(key) ? _sim.Meta.Kingdoms.Items[key].Alliance : -1;
                        _zoneKey[z] = key;
                    }
                    break;
                case OverlayMode.Temperature:
                    for (int z = 0; z < zones; z++) _zoneKey[z] = math.clamp((map.Zones[z].TemperatureC + 30) / 5, 0, 15);
                    break;
                case OverlayMode.Biome:
                    for (int z = 0; z < zones; z++) _zoneKey[z] = map.Zones[z].DominantBiome == 0 ? -1 : map.Zones[z].DominantBiome;
                    break;
                case OverlayMode.Moisture:
                    for (int z = 0; z < zones; z++)
                    {
                        var zone = map.Zones[z];
                        _zoneKey[z] = zone.Drought != 0 ? 16 : zone.Moisture * 15 / 255;
                    }
                    break;
                case OverlayMode.Season:
                    int season = (int)_sim.Nature.Season.Current;
                    for (int z = 0; z < zones; z++) _zoneKey[z] = season;
                    break;
                case OverlayMode.CropYield:
                    for (int z = 0; z < zones; z++)
                    {
                        var zone = map.Zones[z];
                        int city = zone.OwnerCity;
                        float potential = city >= 0 && _sim.Civ.CityAlive(city)
                            ? CropClimate.YieldFactor(_sim.Civ, city, zone)
                            : CropClimate.GrowthFactor(zone, false);
                        _zoneKey[z] = (int)math.round(math.saturate(potential / 1.85f) * 15f);
                    }
                    break;
                case OverlayMode.Protection:
                    for (int z = 0; z < zones; z++)
                    {
                        ushort protectedSpecies = map.Zones[z].ProtectedSpecies;
                        _zoneKey[z] = protectedSpecies == 0 ? -1 : protectedSpecies - 1;
                    }
                    break;
                default:
                    Vote(mode);
                    break;
            }
        }

        // Boyer-Moore majority per zone over living units (exact when a majority exists); scalar modes count instead.
        void Vote(OverlayMode mode)
        {
            var u = _sim.Units.Store;
            var map = _sim.World;
            for (int z = 0; z < _voteKey.Length; z++) { _voteKey[z] = -1; _voteCount[z] = 0; }
            for (int n = 0; n < u.Alive.Length; n++)
            {
                int i = u.Alive[n];
                int2 t = (int2)math.floor(u.Pos[i]);
                if (!map.InBounds(t.x, t.y)) continue;
                int z = map.ZoneIndexOf(t.x, t.y);
                int key;
                switch (mode)
                {
                    case OverlayMode.Culture: key = u.Culture[i]; break;
                    case OverlayMode.Language: key = u.Language[i]; break;
                    case OverlayMode.Religion: key = u.Religion[i]; break;
                    case OverlayMode.Subspecies: key = u.Subspecies[i]; break;
                    case OverlayMode.Clan: key = u.Clan[i]; break;
                    case OverlayMode.Population: _voteCount[z]++; continue;
                    case OverlayMode.Disease: if (u.Disease[i] != 0) _voteCount[z]++; continue;
                    default: continue;
                }
                if (key < 0) continue;
                if (_voteCount[z] == 0) { _voteKey[z] = key; _voteCount[z] = 1; }
                else if (_voteKey[z] == key) _voteCount[z]++;
                else _voteCount[z]--;
            }
            bool scalar = mode == OverlayMode.Population || mode == OverlayMode.Disease;
            for (int z = 0; z < _voteKey.Length; z++)
                _zoneKey[z] = scalar ? (_voteCount[z] == 0 ? -1 : math.min(15, 1 + _voteCount[z] / 4)) : _voteCount[z] > 0 ? _voteKey[z] : -1;
        }

        void Paint(OverlayMode mode)
        {
            var map = _sim.World;
            int zx = map.ZonesX, zy = map.ZonesY, w = zx * PxPerZone;
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < zy; y++)
                for (int x = 0; x < zx; x++)
                {
                    int z = y * zx + x, key = _zoneKey[z];
                    Color32 c = key < 0 ? clear : ColorOf(mode, key);
                    for (int py = 0; py < PxPerZone; py++)
                        for (int px = 0; px < PxPerZone; px++)
                        {
                            var p = c;
                            if (key >= 0)
                            {
                                // outer pixels of the zone turn opaque where the neighbour on that side differs
                                bool edge = (px == 0 && x > 0 && _zoneKey[z - 1] != key) || (px == PxPerZone - 1 && x < zx - 1 && _zoneKey[z + 1] != key) ||
                                            (py == 0 && y > 0 && _zoneKey[z - zx] != key) || (py == PxPerZone - 1 && y < zy - 1 && _zoneKey[z + zx] != key);
                                p.a = edge ? (HighContrast ? byte.MaxValue : BorderAlpha) : (HighContrast ? (byte)150 : FillAlpha);
                            }
                            _pixels[(y * PxPerZone + py) * w + x * PxPerZone + px] = p;
                        }
                }
            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
        }

        Color32 ColorOf(OverlayMode mode, int key)
        {
            if (UseColorBlindIdentityPalette && IsIdentityMode(mode)) return ColorBlindColorForKey(key);
            uint rgba = 0;
            var meta = _sim.Meta;
            switch (mode)
            {
                case OverlayMode.Kingdom: if (key < meta.Kingdoms.Count) rgba = meta.Kingdoms.Headers[key].Color; break;
                case OverlayMode.City: if (key < _sim.Civ.Cities.Count) rgba = _sim.Civ.Cities.Headers[key].Color; break;
                case OverlayMode.Alliance: if (key < meta.Alliances.Count) rgba = meta.Alliances.Headers[key].Color; break;
                case OverlayMode.Culture: if (key < meta.Cultures.Count) rgba = meta.Cultures.Headers[key].Color; break;
                case OverlayMode.Language: if (key < meta.Languages.Count) rgba = meta.Languages.Headers[key].Color; break;
                case OverlayMode.Religion: if (key < meta.Religions.Count) rgba = meta.Religions.Headers[key].Color; break;
                case OverlayMode.Clan: if (key < meta.Clans.Count) rgba = meta.Clans.Headers[key].Color; break;
                case OverlayMode.Subspecies: if (key < _sim.Units.Subspecies.Meta.Count) rgba = _sim.Units.Subspecies.Header(key).Color; break;
                case OverlayMode.Temperature: return Color.Lerp(new Color(0.2f, 0.4f, 1f), new Color(1f, 0.25f, 0.1f), key / 15f);
                case OverlayMode.Population: return Color.Lerp(new Color(1f, 1f, 0.4f), new Color(0.9f, 0.1f, 0.6f), key / 15f);
                case OverlayMode.Disease: return Color.Lerp(new Color(0.6f, 0.9f, 0.3f), new Color(0.3f, 0.5f, 0.05f), key / 15f);
                case OverlayMode.Moisture:
                    if (key == 16) return new Color(0.92f, 0.16f, 0.12f);
                    return Color.Lerp(new Color(0.72f, 0.42f, 0.20f), new Color(0.18f, 0.78f, 0.88f), key / 15f);
                case OverlayMode.Season:
                    switch ((Season)key)
                    {
                        case Season.Spring: return new Color(0.52f, 0.84f, 0.34f);
                        case Season.Summer: return new Color(0.95f, 0.72f, 0.24f);
                        case Season.Autumn: return new Color(0.90f, 0.42f, 0.18f);
                        default: return new Color(0.48f, 0.72f, 0.92f);
                    }
                case OverlayMode.CropYield: return Color.Lerp(new Color(0.86f, 0.18f, 0.16f), new Color(0.25f, 0.82f, 0.32f), key / 15f);
                case OverlayMode.Protection:
                    return UseColorBlindIdentityPalette ? ColorBlindColorForKey(key) : ProtectionColors[key % ProtectionColors.Length];
                case OverlayMode.Biome:
                    var biomes = _sim.Content.Biomes;
                    for (int b = 0; b < biomes.Count; b++) if (biomes[b].MapValue == key) return biomes[b].ParsedColors[0];
                    break;
            }
            if (rgba == 0) rgba = math.hash(new int2(key, (int)mode)) | 0xFFu;
            return new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), 255);
        }

        public static Color32 ColorBlindColorForKey(int key)
        {
            int index = key % ColorBlindIdentityColors.Length;
            if (index < 0) index += ColorBlindIdentityColors.Length;
            return ColorBlindIdentityColors[index];
        }

        static bool IsIdentityMode(OverlayMode mode) => mode == OverlayMode.Kingdom || mode == OverlayMode.City ||
            mode == OverlayMode.Alliance || mode == OverlayMode.Culture || mode == OverlayMode.Language ||
            mode == OverlayMode.Religion || mode == OverlayMode.Subspecies || mode == OverlayMode.Clan;

        void OnDestroy()
        {
            Unbind();
            if (_material != null) Destroy(_material);
        }
    }
}
