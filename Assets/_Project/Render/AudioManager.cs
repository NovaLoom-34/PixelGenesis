using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PG.Content;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;

namespace PG.Render
{
    public enum SoundCategory { Master, Sfx, Ui, Ambient, Music }

    // Bölüm 9.1: 32 pooled 2D sources (24 at once), stereo pan from the camera, volume by LOD and distance, per-sound
    // instance caps and cooldowns (overflowing hits/deaths fall back to the crowd sound). Events become sounds here
    // (SoundEventMap); the simulation never plays audio. Ambience layers follow what the camera sees; music has calm and
    // tense layers per era. Files load from Mods/<id>/Audio or StreamingAssets/Audio; missing files use a synth placeholder.
    public sealed class AudioManager : MonoBehaviour
    {
        public const int PoolSize = 32, MaxVoices = 24, SampleRate = 22050;
        public const float AmbientFade = 2f, LayerFade = 4f, EraFade = 8f, AmbientSeconds = 0.5f;
        static readonly float[] LodVolume = { 1f, 0.7f, 0.4f, 0.2f };
        static readonly string[] AmbientKeys = { "ocean", "forest", "city", "fire", "rain", "wind" };

        public readonly float[] Volumes = { 1f, 1f, 1f, 0.7f, 0.6f }; // by SoundCategory
        public Camera ViewCamera;
        public Func<int> Lod = () => 0;

        ContentDB _db;
        SimWorld _sim;
        readonly Dictionary<string, List<int>> _byTrigger = new Dictionary<string, List<int>>();
        AudioClip[] _clips;
        readonly List<AudioClip> _ownedClips = new List<AudioClip>();
        readonly HashSet<AudioClip> _pendingClips = new HashSet<AudioClip>();
        float[] _nextAllowed;
        int[] _active;
        AudioSource[] _pool;
        int[] _poolSound;
        readonly AudioSource[] _ambient = new AudioSource[AmbientKeys.Length];
        readonly float[] _ambientTarget = new float[AmbientKeys.Length];
        readonly AudioSource[] _music = new AudioSource[4]; // [0,1] current calm/tense, [2,3] fading out
        int _musicEra = -1;
        float _eraFade = 1f, _tension, _nextAmbient;
        float _musicStartedAt;
        int[] _rainClouds = Array.Empty<int>();
        System.Random _variety = new System.Random(7); // presentation only, not simulation

        public void Init(ContentDB db)
        {
            _db = db;
            int n = db.Sounds.Count;
            _clips = new AudioClip[n];
            _nextAllowed = new float[n];
            _active = new int[n];
            _byTrigger.Clear();
            for (int i = 0; i < n; i++)
            {
                var d = db.Sounds[i];
                if (string.IsNullOrEmpty(d.Trigger)) continue;
                if (!_byTrigger.TryGetValue(d.Trigger, out var list)) _byTrigger[d.Trigger] = list = new List<int>();
                list.Add(i);
            }
            if (_pool == null)
            {
                _pool = new AudioSource[PoolSize];
                _poolSound = new int[PoolSize];
                for (int i = 0; i < PoolSize; i++) { _pool[i] = NewSource("Voice" + i); _poolSound[i] = -1; }
                for (int i = 0; i < _ambient.Length; i++) { _ambient[i] = NewSource("Ambient_" + AmbientKeys[i]); _ambient[i].loop = true; }
                for (int i = 0; i < _music.Length; i++) { _music[i] = NewSource("Music" + i); _music[i].loop = true; }
            }
            for (int i = 0; i < AmbientKeys.Length; i++) StartLoop(_ambient[i], "ambient." + AmbientKeys[i]);
            StartCoroutine(WarmUiEffects());
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        public void Bind(SimWorld sim)
        {
            _sim = sim;
            _musicEra = -1;
            var rain = new List<int>();
            if (_byTrigger.TryGetValue("ambient.rain", out var r))
                foreach (var c in _db.Sounds[r[0]].Clouds) { int id = _db.Clouds.IdOrDefault(c); if (id >= 0) rain.Add(id); }
            _rainClouds = rain.ToArray();
            var ev = sim.Events;
            ev.Subscribe<ExplosionEvent>(e => { Play(e.Radius < 3 ? "explosion.small" : e.Radius < 8 ? "explosion.medium" : e.Radius < 20 ? "explosion.large" : "explosion.huge", e.Pos); Violence(e.Pos, 3f); });
            ev.Subscribe<UnitDiedEvent>(OnDied);
            ev.Subscribe<UnitHitEvent>(OnHit);
            ev.Subscribe<UnitBornEvent>(_ => Play("birth")); // no position in the event; the LOD 0 gate keeps it local
            ev.Subscribe<BuildingCompletedEvent>(e => Play("building.completed", BuildingPos(e.Building)));
            ev.Subscribe<BuildingDestroyedEvent>(e => Play("building.destroyed", BuildingPos(e.Building)));
            ev.Subscribe<WarDeclaredEvent>(_ => Play("war.declared"));
            ev.Subscribe<PeaceEvent>(_ => Play("war.ended"));
            ev.Subscribe<CityCapturedEvent>(_ => Play("war.ended"));
        }

        public void Unbind() => _sim = null;

        void OnDied(UnitDiedEvent e)
        {
            var sp = _db.Species[e.Species];
            var pos = new float2(e.Tile.x + 0.5f, e.Tile.y + 0.5f);
            string key = e.Cause == DeathCause.Drowned ? "death.drowned" : sp.IsUndeadCategory ? "death.undead" : sp.IsMonster ? "death.monster" :
                         sp.IsCiv ? "death.civ" : (sp.Flags & UnitFlags.Fly) != 0 ? "death.flying" :
                         sp.BaseStats != null && sp.BaseStats[(int)StatId.Hp] >= 60f ? "death.large" : "death.small";
            Play(key, pos);
        }

        void OnHit(UnitHitEvent e)
        {
            string trigger = e.Kind == CombatImpactKind.Melee ? "hit.melee" :
                             e.Kind == CombatImpactKind.Magic ? "hit.magic" : "hit.ranged";
            Play(trigger, e.Position);
            Violence(e.Position, e.Kind == CombatImpactKind.Melee ? 1f : 1.5f);
        }

        float2 BuildingPos(int b)
        {
            var civ = _sim?.Civ;
            if (civ == null || b < 0 || b >= civ.Buildings.Length) return float2.zero;
            var d = civ.Buildings[b];
            return new float2(d.Origin.x + d.W * 0.5f, d.Origin.y + d.H * 0.5f);
        }

        // Screen-space sounds (UI, powers, war bells).
        public void Play(string trigger) => Play(trigger, null);

        // Power feedback: a specific sound for the power, else its tab's, else the shared brush sound.
        public void PlayPower(PowerDef p)
        {
            if (_byTrigger.ContainsKey("power:" + p.Id)) Play("power:" + p.Id);
            else if (p.Type == "brush") Play("brush");
            else Play("power.tab." + p.Tab);
        }

        public void Play(string trigger, float2? at)
        {
            if (_db == null || !_byTrigger.TryGetValue(trigger, out var variants)) return;
            int id = variants[_variety.Next(variants.Count)];
            var d = _db.Sounds[id];
            int lod = Mathf.Clamp(Lod(), 0, 3);
            if (d.WorldSpace && (lod < d.MinLod || lod > d.MaxLod)) return;
            float now = Time.unscaledTime;
            if (now < _nextAllowed[id]) return;
            if (_active[id] >= d.MaxInstances || Voices() >= MaxVoices)
            {
                if (trigger.StartsWith("death", StringComparison.Ordinal) || trigger.StartsWith("hit", StringComparison.Ordinal)) Play("crowd", at);
                return;
            }
            float pan = 0f, gain = 1f;
            if (d.WorldSpace && at.HasValue && ViewCamera != null)
            {
                var c = ViewCamera.transform.position;
                float halfH = ViewCamera.orthographicSize, halfW = halfH * ViewCamera.aspect;
                float dx = (at.Value.x - c.x) / Mathf.Max(1f, halfW), dy = (at.Value.y - c.y) / Mathf.Max(1f, halfH);
                float outside = Mathf.Max(0f, Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) - 1f);
                if (outside > 1f) return; // more than a screen away
                pan = Mathf.Clamp(dx, -1f, 1f) * 0.8f;
                gain = LodVolume[lod] * (1f - outside);
            }
            var src = FreeVoice();
            if (src < 0) return;
            _nextAllowed[id] = now + d.CooldownMs / 1000f;
            var s = _pool[src];
            s.clip = Clip(id);
            s.volume = d.Volume * gain * CategoryVolume(d.Category);
            s.panStereo = pan;
            s.pitch = d.Pitch != null && d.Pitch.Length == 2 ? Mathf.Lerp(d.Pitch[0], d.Pitch[1], (float)_variety.NextDouble()) : 1f;
            s.loop = false;
            s.Play();
            _poolSound[src] = id;
            _active[id]++;
        }

        int Voices()
        {
            int n = 0;
            for (int i = 0; i < PoolSize; i++) if (_poolSound[i] >= 0) n++;
            return n;
        }

        int FreeVoice()
        {
            for (int i = 0; i < PoolSize; i++) if (_poolSound[i] < 0) return i;
            return -1;
        }

        float CategoryVolume(string category)
        {
            var c = category == "ui" ? SoundCategory.Ui : category == "ambient" ? SoundCategory.Ambient : category == "music" ? SoundCategory.Music : SoundCategory.Sfx;
            return Volumes[(int)SoundCategory.Master] * Volumes[(int)c];
        }

        void Violence(float2 at, float amount)
        {
            if (ViewCamera == null) return;
            var c = ViewCamera.transform.position;
            float h = ViewCamera.orthographicSize, w = h * ViewCamera.aspect;
            if (Mathf.Abs(at.x - c.x) <= w && Mathf.Abs(at.y - c.y) <= h) _tension = Mathf.Min(1f, _tension + amount / 20f);
        }

        void StartLoop(AudioSource s, string trigger)
        {
            if (!_byTrigger.TryGetValue(trigger, out var ids)) { s.Stop(); s.clip = null; return; }
            s.clip = Clip(ids[0]);
            s.volume = 0f;
            s.Play();
        }

        void Update()
        {
            if (_pool == null) return;
            for (int i = 0; i < PoolSize; i++)
                if (_poolSound[i] >= 0 && !_pool[i].isPlaying) { _active[_poolSound[i]]--; _poolSound[i] = -1; }
            if (_sim == null)
            {
                for (int i = 0; i < _ambient.Length; i++) _ambient[i].volume = 0f;
                for (int i = 0; i < _music.Length; i++) _music[i].volume = 0f;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= _nextAmbient) { _nextAmbient = Time.unscaledTime + AmbientSeconds; SampleView(); }
            for (int i = 0; i < _ambient.Length; i++)
            {
                if (!_byTrigger.TryGetValue("ambient." + AmbientKeys[i], out var ids)) continue;
                float target = _ambientTarget[i] * _db.Sounds[ids[0]].Volume * CategoryVolume("ambient");
                if (_pendingClips.Contains(_ambient[i].clip)) target = 0f;
                _ambient[i].volume = Mathf.MoveTowards(_ambient[i].volume, target, dt / AmbientFade);
            }
            Music(dt);
        }

        // Ambience from the visible zones: water share, trees, city population, burning tiles, rain clouds, height (wind).
        void SampleView()
        {
            if (ViewCamera == null) return;
            var map = _sim.World;
            var c = ViewCamera.transform.position;
            float h = ViewCamera.orthographicSize, w = h * ViewCamera.aspect;
            int x0 = Mathf.Clamp((int)(c.x - w), 0, map.Width - 1) >> PG.World.WorldMap.ZoneShift, x1 = Mathf.Clamp((int)(c.x + w), 0, map.Width - 1) >> PG.World.WorldMap.ZoneShift;
            int y0 = Mathf.Clamp((int)(c.y - h), 0, map.Height - 1) >> PG.World.WorldMap.ZoneShift, y1 = Mathf.Clamp((int)(c.y + h), 0, map.Height - 1) >> PG.World.WorldMap.ZoneShift;
            float water = 0, land = 0, trees = 0, zones = 0;
            var cities = new HashSet<int>();
            for (int zy = y0; zy <= y1; zy++)
                for (int zx = x0; zx <= x1; zx++)
                {
                    var z = map.Zones[zy * map.ZonesX + zx];
                    water += z.WaterTiles; land += z.LandTiles; trees += z.TreeCount; zones++;
                    if (z.OwnerCity >= 0) cities.Add(z.OwnerCity);
                }
            int pop = 0;
            if (_sim.Civ != null) foreach (var city in cities) if (_sim.Civ.CityAlive(city)) pop += _sim.Civ.Cities.Headers[city].MemberCount;
            int fire = 0;
            int tx0 = x0 << PG.World.WorldMap.ZoneShift, tx1 = (x1 + 1) << PG.World.WorldMap.ZoneShift, ty0 = y0 << PG.World.WorldMap.ZoneShift, ty1 = (y1 + 1) << PG.World.WorldMap.ZoneShift;
            for (int y = ty0; y < ty1 && y < map.Height; y += 2)
                for (int x = tx0; x < tx1 && x < map.Width; x += 2)
                    if (map.Fire[y * map.Width + x] != 0) fire++;
            float rain = 0f;
            var clouds = _sim.Nature.Clouds;
            for (int i = 0; i < clouds.Length; i++)
            {
                if (Array.IndexOf(_rainClouds, (int)clouds[i].Type) < 0) continue;
                if (Mathf.Abs(clouds[i].Pos.x - c.x) <= w + clouds[i].Radius && Mathf.Abs(clouds[i].Pos.y - c.y) <= h + clouds[i].Radius) rain = 1f;
            }
            float tiles = Mathf.Max(1f, water + land);
            _ambientTarget[0] = water / tiles;
            _ambientTarget[1] = Mathf.Clamp01(trees / Mathf.Max(1f, zones * 6f));
            _ambientTarget[2] = Mathf.Clamp01(pop / 150f);
            _ambientTarget[3] = Mathf.Clamp01(fire / 40f);
            _ambientTarget[4] = rain;
            _ambientTarget[5] = Mathf.Clamp01((Lod() - 1) / 2f);
        }

        void Music(float dt)
        {
            int era = _sim.Nature.Era.CurrentEra;
            if (era != _musicEra)
            {
                // current pair fades out on sources 2-3, the new era's pair starts on 0-1
                (_music[0], _music[2]) = (_music[2], _music[0]);
                (_music[1], _music[3]) = (_music[3], _music[1]);
                string eraKey = _db.Eras[era].Id.Substring(_db.Eras[era].Id.IndexOf('.') + 1);
                _musicStartedAt = Time.unscaledTime;
                StartLoop(_music[0], "music.era." + eraKey + ".calm");
                StartLoop(_music[1], "music.era." + eraKey + ".tense");
                _eraFade = _musicEra < 0 ? 1f : 0f;
                _musicEra = era;
            }
            _tension = Mathf.Max(0f, _tension - dt / 30f);
            _eraFade = Mathf.Min(1f, _eraFade + dt / EraFade);
            float vol = CategoryVolume("music") * 0.35f;
            _music[0].volume = Mathf.MoveTowards(_music[0].volume, vol * (1f - _tension * 0.6f) * _eraFade, dt / LayerFade);
            _music[1].volume = Mathf.MoveTowards(_music[1].volume, vol * _tension * _eraFade, dt / LayerFade);
            _music[2].volume = Mathf.MoveTowards(_music[2].volume, 0f, dt / EraFade);
            _music[3].volume = Mathf.MoveTowards(_music[3].volume, 0f, dt / EraFade);
            // The authored loop fades in only after its download completes, never over a placeholder drone.
            for (int i = 0; i < _music.Length; i++)
                if (_pendingClips.Contains(_music[i].clip)) _music[i].volume = 0f;
            for (int i = 2; i < 4; i++) if (_music[i].volume <= 0f && _music[i].isPlaying) _music[i].Stop();
        }

        AudioClip Clip(int id)
        {
            if (_clips[id] != null) return _clips[id];
            var d = _db.Sounds[id];
            _clips[id] = CreateFallback(d);
            string file = FindFile(d);
            if (file != null)
            {
                _pendingClips.Add(_clips[id]);
                StartCoroutine(LoadFile(id, file));
            }
            return _clips[id];
        }

        AudioClip CreateFallback(SoundDef d)
        {
            var clip = Synth(d.Id, d.Synth);
            _ownedClips.Add(clip);
            return clip;
        }

        // Warm only UI sounds. Other one-shots use Clip's on-demand path, avoiding a full sound-library download at startup.
        IEnumerator WarmUiEffects()
        {
            for (int id = 0; id < _db.Sounds.Count; id++)
            {
                var d = _db.Sounds[id];
                if (d.Loop || d.Category != "ui" || _clips[id] != null) continue;
                string file = FindFile(d);
                if (file == null) continue;
                _clips[id] = CreateFallback(d);
                _pendingClips.Add(_clips[id]);
                yield return LoadFile(id, file);
            }
        }

        string FindFile(SoundDef d)
        {
            if (d.Files == null || d.Files.Count == 0) return null;
            string name = d.Files[_variety.Next(d.Files.Count)];
            for (int m = _db.Mods.Count - 1; m >= 0; m--)
            {
                string p = Path.Combine(_db.Mods[m].Folder, "Audio", name);
                if (PG.Content.ContentFiles.Exists(p)) return p;
            }
            string basePath = Path.Combine(Application.streamingAssetsPath, "Audio", name);
            return PG.Content.ContentFiles.Exists(basePath) ? basePath : null;
        }

        IEnumerator LoadFile(int id, string path)
        {
            var type = path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : AudioType.WAV;
            string url = path.StartsWith("jar:", StringComparison.Ordinal) || path.StartsWith("http", StringComparison.Ordinal)
                ? path : new Uri(Path.GetFullPath(path)).AbsoluteUri;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    InstallLoadedClip(id, DownloadHandlerAudioClip.GetContent(req));
                else
                {
                    _pendingClips.Remove(_clips[id]);
                    Debug.LogWarning($"[Audio] {path}: {req.error}");
                }
            }
        }

        void InstallLoadedClip(int id, AudioClip loaded)
        {
            var previous = _clips[id];
            _pendingClips.Remove(previous);
            if (loaded == null) return;
            _clips[id] = loaded;
            _ownedClips.Add(loaded);
            // AudioSource retains its old clip when the cache entry changes. Rebind only the matching loops;
            // short effects finish naturally and use the new clip on their next trigger.
            foreach (var source in _ambient) ReplaceLoopClip(source, previous, loaded, -1f);
            foreach (var source in _music) ReplaceLoopClip(source, previous, loaded, Time.unscaledTime - _musicStartedAt);
        }

        static void ReplaceLoopClip(AudioSource source, AudioClip previous, AudioClip loaded, float phase)
        {
            if (source == null || source.clip != previous) return;
            bool playing = source.isPlaying;
            float position = phase >= 0f ? phase : source.time;
            source.clip = loaded;
            if (!playing) return;
            source.Play();
            source.time = Mathf.Repeat(position, loaded.length);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var clip in _ownedClips)
            {
                if (clip == null) continue;
                if (Application.isPlaying) Destroy(clip);
                else DestroyImmediate(clip);
            }
            _ownedClips.Clear();
            _pendingClips.Clear();
        }

        // Placeholder synthesis: tone / noise / sweep / chord / pulse (see p6_audio.py).
        static AudioClip Synth(string name, string spec)
        {
            var p = (spec ?? "tone:440:0.1").Split(':');
            float F(int i, float def) => i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : def;
            string kind = p[0];
            float seconds = kind == "sweep" ? F(3, 0.2f) : kind == "tone" || kind == "chord" ? F(2, 0.2f) : kind == "pulse" ? F(2, 4f) : F(1, 0.3f);
            int n = Mathf.Max(64, (int)(seconds * SampleRate));
            var data = new float[n];
            uint seed = math.hash(new int2(name.GetHashCode(), n));
            var rng = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            bool loop = seconds >= 3f;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate, k = i / (float)n;
                float env = loop ? Mathf.Min(1f, Mathf.Min(k, 1f - k) * 20f) : Mathf.Exp(-4f * k) * Mathf.Min(1f, i / 80f);
                float v;
                switch (kind)
                {
                    case "noise":
                        lp += (rng.NextFloat(-1f, 1f) - lp) * Mathf.Clamp(F(2, 0.3f), 0.005f, 1f);
                        v = lp * (loop ? 1.5f : 2f);
                        break;
                    case "sweep":
                        float f = Mathf.Lerp(F(1, 400f), F(2, 200f), k);
                        v = Mathf.Sin(2f * Mathf.PI * f * t);
                        break;
                    case "chord":
                        float r = F(1, 262f);
                        v = (Mathf.Sin(2f * Mathf.PI * r * t) + Mathf.Sin(2f * Mathf.PI * r * 1.26f * t) + Mathf.Sin(2f * Mathf.PI * r * 1.5f * t)) / 3f;
                        break;
                    case "pulse":
                        float rate = F(3, 2f);
                        v = Mathf.Sin(2f * Mathf.PI * F(1, 110f) * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * rate * t));
                        break;
                    default:
                        v = Mathf.Sin(2f * Mathf.PI * F(1, 440f) * t);
                        break;
                }
                data[i] = Mathf.Clamp(v * env * 0.5f, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
