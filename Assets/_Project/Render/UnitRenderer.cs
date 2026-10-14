using System;
using System.Collections.Generic;
using System.IO;
using PG.Content;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 3.10: units as palette-mapped sprite quads in one dynamic mesh, rebuilt per frame for the visible area only.
    // Sheets are 4 columns x 6 rows (idle, walk, attack, swim, sleep, death) as written by Tools/placeholder_art.py.
    // ponytail: dynamic mesh instead of RenderMeshInstanced; fine for thousands of quads, switch when profiling says so.
    public sealed class UnitRenderer : MonoBehaviour
    {
        public const int SortingOrder = SortingLayers.OrderUnits;
        public const int Columns = 4, Rows = 6;
        public const float PixelsPerTile = SpriteLibrary.PixelsPerTile; // same scale-down as features/buildings (DECISIONS #87)
        const int AnimIdle = 0, AnimWalk = 1, AnimAttack = 2, AnimSwim = 3, AnimSleep = 4;

        [Tooltip("Above this orthographic size units draw as 1-tile dots (LOD 2); features/clouds hide at a similar zoom.")]
        public float DotAboveOrthoSize = 120f;
        public float HideAboveOrthoSize = 400f;
        public Camera ViewCamera;

        SimWorld _sim;
        Texture2D _atlas, _palette;
        Material _material;
        Mesh _mesh, _projMesh;
        MeshRenderer _renderer, _projRenderer;
        Rect[] _sheet;         // per species
        Rect[] _propUv;        // held tools/weapons, packed into the same indexed atlas
        Vector2[] _frameSize;  // per species, world units
        bool[] _detailed;
        bool[] _fullColor;
        float[] _footOffset;
        UnitAnimation.Motion[] _motion = Array.Empty<UnitAnimation.Motion>();
        UnitAnimation.Injury[] _injury = Array.Empty<UnitAnimation.Injury>();
        struct BodyLook { public int Sprite, Row; public float Scale; public bool Flip; public long Seen; public Vector2 Pos; }
        readonly Dictionary<long, BodyLook> _bodyLooks = new Dictionary<long, BodyLook>();
        readonly List<long> _expiredLooks = new List<long>();
        Rect _dot, _heart;
        int _paletteRows, _projectileRow, _phenotypeRow;
        int _kingdomRow, _kingdomRowsAllocated, _lastKingdomCount = -1;
        readonly List<Vector3> _v = new List<Vector3>(8192);
        readonly List<Vector2> _t = new List<Vector2>(8192);
        readonly List<Vector2> _r = new List<Vector2>(8192);
        readonly List<Color32> _c = new List<Color32>(8192);
        readonly List<int> _i = new List<int>(12288);
        // Projectiles live on their own mesh + sorting layer (playbook #6) so arrows always draw above units.
        readonly List<Vector3> _pv = new List<Vector3>(256);
        readonly List<Vector2> _pt = new List<Vector2>(256);
        readonly List<Vector2> _pr = new List<Vector2>(256);
        readonly List<Color32> _pc = new List<Color32>(256);
        readonly List<int> _pi = new List<int>(384);
        float _z; // depth of the unit being emitted; every quad of one unit shares it
        int[] _order = Array.Empty<int>();
        float[] _keys = Array.Empty<float>();

        public int LastDrawn { get; private set; }

        public void Bind(SimWorld sim)
        {
            _sim = sim;
            _bodyLooks.Clear();
            Array.Clear(_injury, 0, _injury.Length);
            Array.Clear(_motion, 0, _motion.Length);
            _lastKingdomCount = -1; // new world: kingdom palette rows must be rewritten on the first Rebuild
            if (_material != null) return;
            var db = sim.Content;
            Build(db);
            _material = new Material(Resources.Load<Shader>("PG_Unit")) { name = "Units" };
            _material.SetFloat("_ZWrite", 1f);
            _material.SetTexture("_MainTex", _atlas);
            _material.SetTexture("_Palette", _palette);
            _mesh = new Mesh { name = "Units", indexFormat = IndexFormat.UInt32 };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.sortingLayerName = SortingLayers.Units;
            _renderer.sortingOrder = SortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            _projMesh = new Mesh { name = "Projectiles", indexFormat = IndexFormat.UInt32 };
            var projGo = new GameObject("ProjectileMesh");
            projGo.transform.SetParent(transform, false);
            projGo.AddComponent<MeshFilter>().sharedMesh = _projMesh;
            _projRenderer = projGo.AddComponent<MeshRenderer>();
            _projRenderer.sharedMaterial = _material;
            _projRenderer.sortingLayerName = SortingLayers.Projectiles;
            _projRenderer.sortingOrder = SortingLayers.OrderProjectiles;
            _projRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _projRenderer.receiveShadows = false;
        }

        public void Unbind() { _sim = null; _bodyLooks.Clear(); if (_mesh != null) _mesh.Clear(); if (_projMesh != null) _projMesh.Clear(); }

        void Build(ContentDB db)
        {
            int n = db.Species.Count;
            int propCount = (int)UnitProp.Count - 1;
            int sheetCount = n * 2;
            int propStart = sheetCount + 1;
            var textures = new Texture2D[propStart + propCount + 1];
            _frameSize = new Vector2[sheetCount];
            _detailed = new bool[sheetCount];
            _fullColor = new bool[sheetCount];
            _footOffset = new float[sheetCount];
            for (int s = 0; s < n; s++)
            {
                _detailed[s] = DetailedUnitSheet.TryLoad(db.Species[s].Id, out textures[s], out _frameSize[s], out _footOffset[s], out _fullColor[s]);
                if (!_detailed[s])
                {
                    textures[s] = LoadSheet(db.Species[s].Id);
                    _frameSize[s] = new Vector2(textures[s].width / (float)Columns, textures[s].height / (float)Rows) / PixelsPerTile;
                }
                _detailed[n + s] = DetailedUnitSheet.TryLoad(db.Species[s].Id + "_female", out textures[n + s], out _frameSize[n + s], out _footOffset[n + s], out _fullColor[n + s]);
                if (!_detailed[n + s])
                {
                    textures[n + s] = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    textures[n + s].SetPixel(0, 0, Color.clear);
                    textures[n + s].Apply();
                }
            }
            var dot = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            dot.SetPixel(0, 0, new Color32(84, 84, 84, 255)); // index 3: body main color
            dot.Apply();
            textures[sheetCount] = dot;
            for (int p = 1; p < (int)UnitProp.Count; p++) textures[propStart + p - 1] = UnitPropAtlas.Create((UnitProp)p);
            textures[textures.Length - 1] = UnitPropAtlas.CreateHeart();

            _atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "UnitAtlas" };
            var rects = _atlas.PackTextures(textures, 1, 4096, false);
            _atlas.filterMode = FilterMode.Point;
            _atlas.wrapMode = TextureWrapMode.Clamp;
            _sheet = new Rect[sheetCount];
            for (int s = 0; s < sheetCount; s++)
            {
                _sheet[s] = rects[s];
            }
            _dot = rects[sheetCount];
            _heart = rects[rects.Length - 1];
            _propUv = new Rect[(int)UnitProp.Count];
            for (int p = 1; p < (int)UnitProp.Count; p++) _propUv[p] = rects[propStart + p - 1];
            foreach (var t in textures) Release(t);

            // Palette rows: species, projectiles, phenotypes (Bölüm 4.5), then one row per kingdom
            // (cloth slots in the banner colour — EK C 4.1); kingdom rows grow lazily in Rebuild.
            _phenotypeRow = n + 1;
            _projectileRow = n;
            _kingdomRow = n + 1 + db.Phenotypes.Count;
            _kingdomRowsAllocated = 16;
            _paletteRows = _kingdomRow + _kingdomRowsAllocated;
            _palette = new Texture2D(8, _paletteRows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "UnitPalette" };
            WriteBaseRows(db);
            _palette.Apply();
        }

        void WriteBaseRows(ContentDB db)
        {
            int n = db.Species.Count;
            for (int s = 0; s < n; s++) WriteRow(_palette, s, BaseColor(db.Species[s]));
            WriteRow(_palette, _projectileRow, new Color(0.35f, 0.3f, 0.25f));
            for (int p = 0; p < db.Phenotypes.Count; p++) WriteRow(_palette, _phenotypeRow + p, (Color)db.Phenotypes[p].Color);
        }

        // Kingdom palette rows: body slots from the kingdom's species, cloth slots from the banner colour.
        // Rewritten when the kingdom count changes (kingdoms form mid-game); colours are founding-time constants.
        void RefreshKingdomRows(int kingdoms)
        {
            if (kingdoms > _kingdomRowsAllocated)
            {
                _kingdomRowsAllocated = math.ceilpow2(math.max(16, kingdoms));
                _paletteRows = _kingdomRow + _kingdomRowsAllocated;
                var old = _palette;
                _palette = new Texture2D(8, _paletteRows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "UnitPalette" };
                WriteBaseRows(_sim.Content);
                _material.SetTexture("_Palette", _palette);
                Release(old);
            }
            var meta = _sim.Meta;
            var db = _sim.Content;
            for (int k = 0; k < kingdoms; k++)
            {
                int sp = meta.Kingdom(k).Species;
                Color body = sp >= 0 && sp < db.Species.Count ? BaseColor(db.Species[sp]) : new Color(0.75f, 0.65f, 0.5f);
                uint rgba = meta.Kingdoms.Headers[k].Color;
                var cloth = new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), 255);
                WriteKingdomRow(_palette, _kingdomRow + k, body, cloth);
            }
            _palette.Apply();
        }

        static Texture2D LoadSheet(string id)
        {
            string name = id.Substring(id.IndexOf('.') + 1);
            foreach (string tier in new[] { "Final", "Placeholder" })
            {
                string path = Path.Combine(SpriteLibrary.ArtRoot, tier, "units", name + ".png");
                if (!PG.Content.ContentFiles.Exists(path)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(PG.Content.ContentFiles.ReadAllBytes(path))) return tex;
                Release(tex);
            }
            // missing art: a 3x3 body block in every frame
            var fallback = new Texture2D(Columns * 3, Rows * 3, TextureFormat.RGBA32, false);
            var px = new Color32[fallback.width * fallback.height];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(84, 84, 84, 255);
            fallback.SetPixels32(px);
            fallback.Apply();
            return fallback;
        }

        // Placeholder colouring by species keyword, otherwise a hue from the id (real phenotypes arrive in Bölüm 4).
        static Color BaseColor(SpeciesDef sp)
        {
            string id = sp.Id;
            (string key, Color c)[] table =
            {
                ("sheep", new Color(0.92f, 0.9f, 0.85f)), ("cow", new Color(0.55f, 0.4f, 0.3f)), ("wolf", new Color(0.5f, 0.5f, 0.52f)),
                ("bear", new Color(0.4f, 0.28f, 0.18f)), ("fox", new Color(0.85f, 0.45f, 0.15f)), ("rabbit", new Color(0.75f, 0.68f, 0.58f)),
                ("deer", new Color(0.62f, 0.44f, 0.28f)), ("boar", new Color(0.35f, 0.27f, 0.22f)), ("chicken", new Color(0.95f, 0.93f, 0.88f)),
                ("frog", new Color(0.35f, 0.65f, 0.3f)), ("snake", new Color(0.4f, 0.55f, 0.25f)), ("crocodile", new Color(0.3f, 0.45f, 0.28f)),
                ("human", new Color(0.87f, 0.7f, 0.56f)), ("elf", new Color(0.93f, 0.85f, 0.7f)), ("dwarf", new Color(0.8f, 0.6f, 0.48f)),
                ("orc", new Color(0.45f, 0.6f, 0.3f)), ("zombie", new Color(0.5f, 0.62f, 0.45f)), ("skeleton", new Color(0.9f, 0.88f, 0.8f)),
                ("dragon", new Color(0.7f, 0.2f, 0.15f)), ("fairy", new Color(0.9f, 0.6f, 0.9f)), ("slime", new Color(0.4f, 0.8f, 0.4f)),
                ("imp", new Color(0.85f, 0.3f, 0.1f)), ("bee", new Color(0.95f, 0.8f, 0.2f)), ("crow", new Color(0.2f, 0.2f, 0.25f)),
            };
            foreach (var (key, c) in table)
                if (id.Contains(key)) return c;
            uint h = (uint)id.GetHashCode();
            return Color.HSVToRGB((h % 360) / 360f, 0.45f, 0.7f);
        }

        static void WriteRow(Texture2D palette, int row, Color body)
        {
            palette.SetPixel(0, row, body * 0.35f);           // 1 outline
            palette.SetPixel(1, row, body * 0.7f);            // 2 body shadow
            palette.SetPixel(2, row, body);                   // 3 body
            palette.SetPixel(3, row, Color.Lerp(body, Color.white, 0.3f)); // 4 light
            palette.SetPixel(4, row, body * 0.55f);           // 5 cloth shadow — wild units read as fur/skin tones
            palette.SetPixel(5, row, body * 0.8f);            // 6 cloth — kingdom rows override with banner colours
            palette.SetPixel(6, row, new Color(0.95f, 0.85f, 0.3f)); // 7 accent
            palette.SetPixel(7, row, new Color(0.96f, 0.96f, 0.92f)); // 8 shine
        }

        static void WriteKingdomRow(Texture2D palette, int row, Color body, Color cloth)
        {
            palette.SetPixel(0, row, body * 0.35f);           // 1 outline
            palette.SetPixel(1, row, body * 0.7f);            // 2 body shadow
            palette.SetPixel(2, row, body);                   // 3 body
            palette.SetPixel(3, row, Color.Lerp(body, Color.white, 0.3f)); // 4 light
            palette.SetPixel(4, row, cloth * 0.7f);           // 5 cloth shadow
            palette.SetPixel(5, row, cloth);                  // 6 cloth = kingdom banner colour (EK C 4.1)
            palette.SetPixel(6, row, new Color(0.95f, 0.85f, 0.3f)); // 7 accent
            palette.SetPixel(7, row, new Color(0.96f, 0.96f, 0.92f)); // 8 shine
        }

        void LateUpdate()
        {
            if (_sim == null || _mesh == null) return;
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            float ortho = cam != null && cam.orthographic ? cam.orthographicSize : 50f;
            bool show = ortho <= HideAboveOrthoSize;
            _renderer.enabled = show;
            _projRenderer.enabled = show;
            if (!show) return;
            Rebuild(cam, ortho > DotAboveOrthoSize);
        }

        public void Rebuild(Camera cam, bool dots)
        {
            var u = _sim.Units.Store;
            if (_motion.Length < u.Pos.Length) Array.Resize(ref _motion, u.Pos.Length);
            if (_injury.Length < u.Pos.Length) Array.Resize(ref _injury, u.Pos.Length);
            int kingdoms = _sim.Meta.Kingdoms.Count;
            if (kingdoms != _lastKingdomCount) { RefreshKingdomRows(kingdoms); _lastKingdomCount = kingdoms; }
            float2 min = new float2(-1e6f), max = new float2(1e6f);
            if (cam != null && cam.orthographic)
            {
                var p = cam.transform.position;
                float h = cam.orthographicSize + 12f, w = cam.orthographicSize * cam.aspect + 12f;
                min = new float2(p.x - w, p.y - h);
                max = new float2(p.x + w, p.y + h);
            }

            int n = u.Alive.Length;
            if (_order.Length < n)
            {
                _order = new int[math.ceilpow2(n)];
                _keys = new float[_order.Length];
            }
            int visible = 0;
            for (int k = 0; k < n; k++)
            {
                int i = u.Alive[k];
                float2 pos = u.Pos[i];
                if (math.any(pos < min) || math.any(pos > max)) continue;
                _order[visible] = i;
                _keys[visible] = -pos.y; // north first, so southern units overlap them
                visible++;
            }
            Array.Sort(_keys, _order, 0, visible);

            _v.Clear(); _t.Clear(); _r.Clear(); _c.Clear(); _i.Clear();
            _pv.Clear(); _pt.Clear(); _pr.Clear(); _pc.Clear(); _pi.Clear();
            long tick = _sim.Clock.Tick;
            _expiredLooks.Clear();
            foreach (var pair in _bodyLooks)
                if (tick < pair.Value.Seen || tick - pair.Value.Seen > UnitAnimation.CorpseLifetime) _expiredLooks.Add(pair.Key);
            foreach (long uid in _expiredLooks) _bodyLooks.Remove(uid);
            // Existing corpse records also disappear when raised, buried or consumed.
            if (!dots) for (int c = 0; c < u.Corpses.Length; c++)
            {
                var corpse = u.Corpses[c];
                long age = tick - corpse.Tick;
                int deathFrame = UnitAnimation.DeathFrame(age);
                if (deathFrame < 0) continue;
                if (!_bodyLooks.TryGetValue(corpse.Uid, out var look))
                    look = new BodyLook { Sprite = corpse.Species, Row = corpse.Species, Scale = 1,
                        Pos = new Vector2(corpse.Tile % _sim.World.Width + .5f, corpse.Tile / _sim.World.Width + .5f) };
                if (look.Pos.x < min.x || look.Pos.y < min.y || look.Pos.x > max.x || look.Pos.y > max.y) continue;
                _z = SortingLayers.DepthZ(look.Pos.y + 1f); // flat on the ground: behind any living unit on the same spot
                Rect sheet = _sheet[look.Sprite];
                var uv = new Rect(sheet.xMin + deathFrame * sheet.width / Columns, sheet.yMin, sheet.width / Columns, sheet.height / Rows);
                Vector2 size = _frameSize[look.Sprite] * look.Scale;
                Quad(look.Pos - new Vector2(size.x * .5f, .3f + _footOffset[look.Sprite] * look.Scale), size, uv,
                    look.Flip, (look.Row + .5f) / _paletteRows, new Color32(210, 210, 210, (byte)(255 * UnitAnimation.CorpseAlpha(age))), _fullColor[look.Sprite]);
            }
            for (int k = 0; k < visible; k++)
            {
                int i = _order[k];
                int species = u.Species[i];
                int rowIndex = species;
                int sub = u.Subspecies[i];
                var subs = _sim.Units.Subspecies;
                if (sub >= 0 && sub < subs.Count && subs.Meta.Items[sub].Phenotype >= 0) rowIndex = _phenotypeRow + subs.Meta.Items[sub].Phenotype;
                int city = u.City[i]; // kingdom membership goes through the city (MetaWindow.Member)
                if (city >= 0 && _sim.Civ != null && _sim.Civ.CityAlive(city))
                {
                    int kd = _sim.Civ.KingdomOf(city);
                    if (kd >= 0 && kd < kingdoms) rowIndex = _kingdomRow + kd; // citizens wear the banner, phenotype included
                }
                int former = u.FormerSpecies[i] - 1; // a zombie beast keeps its old body in zombie colours (Bölüm 4.9)
                int sprite = former >= 0 && _sim.Content.Species[species].BaseStats[(int)StatId.Hp] <= 0f ? former : species;
                string speciesId = _sim.Content.Species[sprite].Id;
                int femaleSlot = _sim.Content.Species.Count + sprite;
                bool femaleSprite = u.Sex[i] == 0 && _detailed[femaleSlot];
                if (femaleSprite) sprite = femaleSlot;
                float row = (rowIndex + 0.5f) / _paletteRows;
                float2 pos = u.Pos[i];
                _z = u.Has(i, UnitFlags.Fly) ? 0f : SortingLayers.DepthZ(pos.y - 0.3f); // flyers stay above trees and roofs
                var tint = Tint(u, i);
                float injury = UnitAnimation.ObserveInjury(ref _injury[i], u.Uid[i], tick, u.Hp[i]);
                if (injury > 0) tint = Color32.Lerp(tint, new Color32(255, 110, 100, tint.a), injury * .7f);
                float ageScale = u.Age[i] == (byte)AgeStage.Baby ? .6f : u.Age[i] == (byte)AgeStage.Child ? .8f : 1f;
                _bodyLooks[u.Uid[i]] = new BodyLook { Sprite = sprite, Row = rowIndex, Scale = ageScale,
                    Flip = u.Facing[i] == 1, Seen = tick, Pos = new Vector2(pos.x, pos.y) };
                if (dots)
                {
                    Quad(new Vector2(pos.x - 0.5f, pos.y - 0.5f), new Vector2(1f, 1f), _dot, false, row, tint);
                    continue;
                }
                UnitAnimation.Observe(ref _motion[i], u.Uid[i], tick, pos, u.PathHandle[i] >= 0);
                int attackFrame = UnitAnimation.AttackFrame(u.AttackCooldown[i], u.Stat(i, StatId.AtkSpd));
                string equipmentId = WeaponId(u, i);
                string jobId = JobId(u, i);
                bool doingWork = (UnitTask)u.Task[i] == UnitTask.Work && u.Action[i] != CivJobs.ActCarry;
                bool authoredAttack = _detailed[sprite] && (femaleSprite
                    ? string.IsNullOrEmpty(equipmentId)
                    : UnitAppearance.CanUseRaceAttackPose(speciesId, equipmentId));
                int requestedAnim = Anim(u, i, _motion[i].Moving, attackFrame);
                bool replaceWeaponAttack = requestedAnim == AnimAttack && _detailed[sprite] && !authoredAttack;
                bool unarmedAttack = replaceWeaponAttack && string.IsNullOrEmpty(equipmentId);
                UnitProp prop = UnitAppearance.Resolve(jobId, equipmentId, doingWork, unarmedAttack);
                bool courting = _sim.Content.Species[species].IsCiv && (UnitTask)u.Task[i] == UnitTask.Mate &&
                    u.Action[i] == 1 && u.IsAlive(u.Target[i]) && math.distance(pos, u.Pos[u.Target[i].Index]) <= 1.2f;
                if (courting) prop = UnitProp.None;
                int anim = replaceWeaponAttack ? AnimWalk : requestedAnim;
                int frame = anim == AnimAttack ? attackFrame
                    : replaceWeaponAttack ? attackFrame % 4
                    : UnitAnimation.Frame(tick, u.Uid[i], anim);
                Vector2 size = _frameSize[sprite];
                var sheet = _sheet[sprite];
                float fw = sheet.width / Columns, fh = sheet.height / Rows;
                var uv = new Rect(sheet.xMin + frame * fw, sheet.yMax - (anim + 1) * fh, fw, fh);
                float scale = u.Age[i] == (byte)AgeStage.Baby ? 0.6f : u.Age[i] == (byte)AgeStage.Child ? 0.8f : 1f;
                Vector2 s = size * scale;
                if (_detailed[sprite] && anim == AnimIdle) s = Vector2.Scale(s, UnitAnimation.Breath(tick, u.Uid[i]));
                float foot = _footOffset[sprite] * s.y / size.y;
                bool workStroke = doingWork && u.Action[i] == CivJobs.ActWork && !_motion[i].Moving;
                float workPhase = ((tick % 20 + u.Uid[i] % 20 * 7) % 20) / 19f;
                float dip = workStroke ? Mathf.Sin(workPhase * Mathf.PI) * .08f * scale : 0;
                Vector2 bodyOrigin = new Vector2(pos.x - s.x * 0.5f + (u.Facing[i] == 1 ? 1 : -1) * injury * .09f,
                    pos.y - 0.3f - foot + u.Z[i] - dip);
                Quad(bodyOrigin, s, uv, u.Facing[i] == 1, row, tint, _fullColor[sprite]);
                if (injury > 0f) DrawHitSparks(pos, bodyOrigin, s, row, injury, tick - _injury[i].HitTick);
                if (prop != UnitProp.None && anim != AnimSleep && anim != AnimSwim && !(anim == AnimAttack && authoredAttack))
                {
                    bool facingLeft = u.Facing[i] == 1;
                    bool fist = prop == UnitProp.UnarmedStrike;
                    Vector2 propSize = (fist ? new Vector2(.42f, .3f) : new Vector2(.8f, .8f)) * scale;
                    Vector2 anchor = UnitPropMotion.Hand(speciesId, anim, frame, _detailed[sprite], femaleSprite);
                    if (facingLeft) anchor.x = 1f - anchor.x;
                    Vector2 hand = bodyOrigin + Vector2.Scale(anchor, s);
                    bool working = workStroke;
                    float attackPhase = requestedAnim == AnimAttack
                        ? UnitAnimation.AttackPhase(u.AttackCooldown[i], u.Stat(i, StatId.AtkSpd)) : -1f;
                    float angle = UnitPropMotion.Angle(prop, attackPhase, working, tick, u.Uid[i], frame);
                    float propRow = ((fist ? rowIndex : _projectileRow) + 0.5f) / _paletteRows;
                    HeldPropQuad(hand, propSize, UnitPropMotion.Grip(prop), angle, _propUv[(int)prop], facingLeft, propRow, tint);
                    if (prop == UnitProp.Bow)
                    {
                        float pull = attackPhase < 0 ? 0 : attackPhase < .5f ? attackPhase * 2 : 1 - (attackPhase - .5f) * 2;
                        Vector2 upper = UnitPropMotion.Corner(hand, propSize, UnitPropMotion.Grip(prop), new Vector2(.56f,.86f), angle, facingLeft);
                        Vector2 lower = UnitPropMotion.Corner(hand, propSize, UnitPropMotion.Grip(prop), new Vector2(.47f,.18f), angle, facingLeft);
                        Vector2 draw = UnitPropMotion.Corner(hand, propSize, UnitPropMotion.Grip(prop), new Vector2(.53f - pull * .3f,.53f), angle, facingLeft);
                        StrokeQuad(upper, draw, .025f * scale, propRow, tint);
                        StrokeQuad(draw, lower, .025f * scale, propRow, tint);
                        Quad(draw - Vector2.one * .035f * scale, Vector2.one * .07f * scale, _dot, false, row, tint);
                    }
                    // Fingers cover the handle, instead of the tool covering the whole hand.
                    if (!fist) Quad(hand - Vector2.one * .045f * scale, Vector2.one * .09f * scale, _dot, false, row, tint);
                }
                if (courting)
                {
                    float pulse = Mathf.Sin((tick % 40) * Mathf.PI / 10f);
                    float heartSize = (.35f + pulse * .035f) * scale;
                    var heartTint = new Color32(255, 100, 145, tint.a);
                    float heartRow = (_projectileRow + .5f) / _paletteRows;
                    Quad(new Vector2(pos.x - heartSize * .5f, bodyOrigin.y + s.y * .9f + .1f * pulse),
                        Vector2.one * heartSize, _heart, false, heartRow, heartTint);
                }
            }

            var proj = _sim.Units.Projectiles;
            float prow = (_projectileRow + 0.5f) / _paletteRows;
            for (int k = 0; k < proj.Length; k++)
            {
                var p = proj[k];
                if (math.any(p.Pos < min) || math.any(p.Pos > max)) continue;
                string projectileWeapon = u.IsAlive(p.Owner) && u.Uid[p.Owner] == p.OwnerUid ? WeaponId(u, p.Owner) : null;
                bool arrow = projectileWeapon == "eq.bow" || projectileWeapon == "eq.crossbow";
                ProjQuad(new Vector2(p.Pos.x, p.Pos.y + p.Z), new Vector2(p.To.x - p.From.x, p.To.y - p.From.y), prow, arrow);
            }

            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetUVs(0, _t);
            _mesh.SetUVs(1, _r);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_i, 0);
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));

            _projMesh.Clear();
            _projMesh.SetVertices(_pv);
            _projMesh.SetUVs(0, _pt);
            _projMesh.SetUVs(1, _pr);
            _projMesh.SetColors(_pc);
            _projMesh.SetTriangles(_pi, 0);
            _projMesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
            LastDrawn = visible;
        }

        // Projectile quads go to their own mesh/layer; one white palette-indexed dot each.
        void ProjQuad(Vector2 origin, Vector2 direction, float row, bool arrow)
        {
            int v = _pv.Count;
            Vector2 axis = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
            Vector2 side = new Vector2(-axis.y, axis.x) * (arrow ? .045f : .12f);
            Vector2 tail = origin - axis * (arrow ? .42f : .12f), tip = origin + axis * (arrow ? .18f : .12f);
            _pv.Add(tail - side); _pv.Add(tip - side); _pv.Add(tip + side); _pv.Add(tail + side);
            _pt.Add(new Vector2(_dot.xMin, _dot.yMin));
            _pt.Add(new Vector2(_dot.xMax, _dot.yMin));
            _pt.Add(new Vector2(_dot.xMax, _dot.yMax));
            _pt.Add(new Vector2(_dot.xMin, _dot.yMax));
            var r = new Vector2(row, 0f);
            _pr.Add(r); _pr.Add(r); _pr.Add(r); _pr.Add(r);
            var white = new Color32(255, 255, 255, 255);
            _pc.Add(white); _pc.Add(white); _pc.Add(white); _pc.Add(white);
            _pi.Add(v); _pi.Add(v + 2); _pi.Add(v + 1);
            _pi.Add(v); _pi.Add(v + 3); _pi.Add(v + 2);
        }

        void DrawHitSparks(float2 pos, Vector2 bodyOrigin, Vector2 bodySize, float row, float injury, long ageTicks)
        {
            // HP loss is sampled once per simulation tick. The same short burst
            // remains frozen while paused and is replaced by the next actual hit.
            float age = Mathf.Clamp01(ageTicks / 5f);
            float radius = bodySize.x * (.12f + age * .18f);
            float pixel = bodySize.x * (.075f - age * .025f);
            var center = new Vector2(pos.x, bodyOrigin.y + bodySize.y * .64f);
            var red = new Color32(255, 90, 72, (byte)(injury * 210f));
            var amber = new Color32(255, 187, 94, (byte)(injury * 185f));
            Quad(center + new Vector2(-radius, radius * .65f), Vector2.one * pixel, _dot, false, row, red);
            Quad(center + new Vector2(radius * .9f, radius * .8f), Vector2.one * pixel, _dot, false, row, amber);
            Quad(center + new Vector2(radius * .2f, -radius * .8f), Vector2.one * pixel * .7f, _dot, false, row, red);
        }

        int Anim(UnitStore u, int i, bool moving, int attackFrame)
        {
            if (u.Has(i, UnitFlags.Asleep)) return AnimSleep;
            int2 t = u.Tile(i);
            var map = _sim.World;
            if (!u.Has(i, UnitFlags.Fly) && map.InBounds(t.x, t.y) && !map.IsWalkable(t.x, t.y)) return AnimSwim;
            var task = (UnitTask)u.Task[i];
            if ((task == UnitTask.Attack || task == UnitTask.Hunt) && !moving && u.PathHandle[i] < 0 && attackFrame >= 0) return AnimAttack;
            return moving ? AnimWalk : AnimIdle;
        }

        string JobId(UnitStore u, int i)
        {
            int job = u.Job[i] - 1;
            var jobs = _sim.Content.Jobs;
            return (uint)job < (uint)jobs.Count ? jobs[job].Id : null;
        }

        string WeaponId(UnitStore u, int i)
        {
            var civ = _sim.Civ;
            if (civ == null) return null;
            int itemIndex = u.Equip[i * UnitStore.EquipSlots + (int)EquipSlot.Weapon];
            if ((uint)itemIndex >= (uint)civ.Items.Length) return null;
            ushort type = civ.Items[itemIndex].Type;
            var equipment = _sim.Content.EquipmentTypes;
            return type < equipment.Count ? equipment[type].Id : null;
        }

        Color32 Tint(UnitStore u, int i)
        {
            var w = _sim.Units;
            if (w.StFrozen >= 0 && u.HasStatus(i, w.StFrozen)) return new Color32(170, 210, 255, 255);
            if (w.StBurning >= 0 && u.HasStatus(i, w.StBurning)) return (_sim.Clock.Tick / 3 + i) % 2 == 0 ? new Color32(255, 170, 90, 255) : new Color32(255, 120, 60, 255);
            if (u.Has(i, UnitFlags.Untargetable)) return new Color32(255, 255, 255, 110);
            return new Color32(255, 255, 255, 255);
        }

        void Quad(Vector2 origin, Vector2 size, Rect uv, bool flip, float row, Color32 color, bool fullColor = false)
        {
            int v = _v.Count;
            _v.Add(new Vector3(origin.x, origin.y, _z));
            _v.Add(new Vector3(origin.x + size.x, origin.y, _z));
            _v.Add(new Vector3(origin.x + size.x, origin.y + size.y, _z));
            _v.Add(new Vector3(origin.x, origin.y + size.y, _z));
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
            _t.Add(new Vector2(u0, uv.yMin));
            _t.Add(new Vector2(u1, uv.yMin));
            _t.Add(new Vector2(u1, uv.yMax));
            _t.Add(new Vector2(u0, uv.yMax));
            var r = new Vector2(row, fullColor ? 1f : 0f);
            _r.Add(r); _r.Add(r); _r.Add(r); _r.Add(r);
            _c.Add(color); _c.Add(color); _c.Add(color); _c.Add(color);
            _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
            _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
        }

        void StrokeQuad(Vector2 from, Vector2 to, float width, float row, Color32 color)
        {
            Vector2 delta = to - from;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            HeldPropQuad(from, new Vector2(delta.magnitude, width), new Vector2(0, .5f), angle, _dot, false, row, color);
        }

        void HeldPropQuad(Vector2 hand, Vector2 size, Vector2 grip, float angle, Rect uv, bool flip, float row, Color32 color)
        {
            int start = _v.Count;
            Quad(Vector2.zero, Vector2.one, uv, false, row, color);
            _v[start] = At(UnitPropMotion.Corner(hand, size, grip, Vector2.zero, angle, flip));
            _v[start + 1] = At(UnitPropMotion.Corner(hand, size, grip, Vector2.right, angle, flip));
            _v[start + 2] = At(UnitPropMotion.Corner(hand, size, grip, Vector2.one, angle, flip));
            _v[start + 3] = At(UnitPropMotion.Corner(hand, size, grip, Vector2.up, angle, flip));
        }

        Vector3 At(Vector2 p)
        {
            return new Vector3(p.x, p.y, _z);
        }

        void OnDestroy()
        {
            Release(_material);
            Release(_atlas);
            Release(_palette);
            Release(_mesh);
            Release(_projMesh);
        }

        static void Release(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
