using System.Collections.Generic;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 5.10 & 5.12: Renders dynamic water ships (transports, fishing, trade, warships)
    // and land vehicles (carts, steam cars) as animated pixel art quads.
    public sealed class VehicleRenderer : MonoBehaviour
    {
        public Camera ViewCamera;
        public float HideAboveOrthoSize = 250f;

        CivWorld _civ;
        Material _material;
        Mesh _mesh;
        MeshRenderer _renderer;
        Texture2D _dot;

        float _z; // depth of the vehicle being emitted
        readonly List<Vector3> _v = new List<Vector3>(256);
        readonly List<Vector2> _t = new List<Vector2>(256);
        readonly List<Color32> _c = new List<Color32>(256);
        readonly List<int> _i = new List<int>(384);

        static readonly Color32 WoodHull = new Color32(130, 85, 45, 255);
        static readonly Color32 DarkHull = new Color32(70, 50, 35, 255);
        static readonly Color32 SailWhite = new Color32(245, 245, 250, 255);
        static readonly Color32 CartWood = new Color32(145, 100, 55, 255);
        static readonly Color32 WheelColor = new Color32(50, 40, 30, 255);
        static readonly Color32 CarMetal = new Color32(42, 48, 58, 255);
        static readonly Color32 CarBrass = new Color32(215, 165, 55, 255);
        static readonly Color32 Chimney = new Color32(25, 25, 25, 255);
        static readonly Color32 SteamPuff = new Color32(220, 225, 235, 160);

        public void Bind(CivWorld civ)
        {
            _civ = civ;
            if (_material != null) return;

            _dot = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _dot.SetPixel(0, 0, Color.white);
            _dot.Apply();

            _material = new Material(Resources.Load<Shader>("PG_Sprite")) { name = "Vehicles", mainTexture = _dot };
            _material.SetFloat("_ZWrite", 1f);
            _mesh = new Mesh { name = "Vehicles", indexFormat = IndexFormat.UInt32 };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.sortingLayerName = SortingLayers.Units;
            _renderer.sortingOrder = SortingLayers.OrderUnits - 1; // vehicles sit just below standing passenger units
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void Unbind() => _civ = null;

        void LateUpdate()
        {
            if (_civ == null || _mesh == null) return;
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            float ortho = cam != null && cam.orthographic ? cam.orthographicSize : 50f;
            bool show = ortho <= HideAboveOrthoSize;
            _renderer.enabled = show;
            if (!show) return;

            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            float time = Time.unscaledTime;

            // 1. Colony Ships (Boats.cs)
            if (_civ.Boats.IsCreated)
            {
                for (int b = 0; b < _civ.Boats.Length; b++)
                {
                    var boat = _civ.Boats[b];
                    if (boat.State != BoatState.Sailing) continue;
                    _z = SortingLayers.DepthZ(boat.Pos.y);
                    DrawShip(boat.Pos, new float2(1f, 0f), 1.8f, 1.2f, WoodHull, SailWhite, time);
                }
            }

            // 2. Vehicles (Carts, Cars, Fishing Boats, Trade Boats, Warships)
            if (_civ.Vehicles.IsCreated)
            {
                for (int v = 0; v < _civ.Vehicles.Length; v++)
                {
                    var veh = _civ.Vehicles[v];
                    if (veh.State == VehicleState.Done) continue;
                    _z = SortingLayers.DepthZ(veh.Pos.y);

                    switch (veh.Type)
                    {
                        case VehicleType.Cart:
                            DrawCart(veh.Pos, veh.Dir, time);
                            break;
                        case VehicleType.Car:
                            DrawCar(veh.Pos, veh.Dir, time);
                            break;
                        case VehicleType.FishingBoat:
                        case VehicleType.TradeBoat:
                            DrawShip(veh.Pos, veh.Dir, 1.5f, 1.0f, WoodHull, SailWhite, time);
                            break;
                        case VehicleType.TransportShip:
                        case VehicleType.Warship:
                            DrawShip(veh.Pos, veh.Dir, 2.0f, 1.4f, DarkHull, new Color32(230, 220, 200, 255), time);
                            break;
                    }
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetUVs(0, _t);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_i, 0);
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
        }

        void DrawShip(float2 pos, float2 dir, float w, float h, Color32 hullColor, Color32 sailColor, float time)
        {
            // Hull
            float bob = math.sin(time * 3f + pos.x) * 0.08f;
            Quad(pos.x, pos.y + bob - 0.2f, w, h * 0.45f, hullColor);

            // Mast & Sail
            Quad(pos.x, pos.y + bob + 0.3f, 0.15f, h * 0.8f, DarkHull);
            float sailFlutter = math.sin(time * 5f + pos.y) * 0.05f;
            Quad(pos.x + 0.2f, pos.y + bob + 0.35f, w * 0.55f + sailFlutter, h * 0.55f, sailColor);
        }

        void DrawCart(float2 pos, float2 dir, float time)
        {
            // Wooden wagon bed
            Quad(pos.x, pos.y + 0.1f, 1.4f, 0.7f, CartWood);
            // Cargo crate/sack inside cart
            Quad(pos.x, pos.y + 0.45f, 1.0f, 0.45f, new Color32(185, 155, 105, 255));
            // Wheels
            Quad(pos.x - 0.5f, pos.y - 0.2f, 0.35f, 0.35f, WheelColor);
            Quad(pos.x + 0.5f, pos.y - 0.2f, 0.35f, 0.35f, WheelColor);
        }

        void DrawCar(float2 pos, float2 dir, float time)
        {
            // Dark iron steam car body
            Quad(pos.x, pos.y + 0.15f, 1.6f, 0.75f, CarMetal);
            // Brass radiator / cabin window
            Quad(pos.x + 0.2f, pos.y + 0.35f, 0.65f, 0.45f, CarBrass);
            // Chimney
            Quad(pos.x - 0.45f, pos.y + 0.65f, 0.22f, 0.55f, Chimney);
            // Animated steam puff trailing behind
            float puffPhase = math.frac(time * 2.5f);
            Quad(pos.x - 0.65f - puffPhase * 0.5f, pos.y + 0.85f + puffPhase * 0.4f, 0.4f + puffPhase * 0.3f, 0.4f + puffPhase * 0.3f, SteamPuff);
            // Heavy iron wheels
            Quad(pos.x - 0.55f, pos.y - 0.18f, 0.42f, 0.42f, WheelColor);
            Quad(pos.x + 0.55f, pos.y - 0.18f, 0.42f, 0.42f, WheelColor);
        }

        void Quad(float cx, float cy, float w, float h, Color32 color)
        {
            int v = _v.Count;
            float hx = w * 0.5f, hy = h * 0.5f;
            _v.Add(new Vector3(cx - hx, cy - hy, _z));
            _v.Add(new Vector3(cx + hx, cy - hy, _z));
            _v.Add(new Vector3(cx + hx, cy + hy, _z));
            _v.Add(new Vector3(cx - hx, cy + hy, _z));
            _t.Add(new Vector2(0f, 0f));
            _t.Add(new Vector2(1f, 0f));
            _t.Add(new Vector2(1f, 1f));
            _t.Add(new Vector2(0f, 1f));
            _c.Add(color);
            _c.Add(color);
            _c.Add(color);
            _c.Add(color);
            _i.Add(v);
            _i.Add(v + 2);
            _i.Add(v + 1);
            _i.Add(v);
            _i.Add(v + 3);
            _i.Add(v + 2);
        }
    }
}
