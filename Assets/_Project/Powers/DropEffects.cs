using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using Unity.Mathematics;

namespace PG.Powers
{
    public static class DropSystemConst
    {
        public const float StartZ = 40f, Gravity = 60f; // Bölüm 7.2: ~1.15 s fall
    }

    // What a falling power does when it lands (Bölüm 7.2-7.5).
    public interface IDropEffect
    {
        void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng);
    }

    public static class DropEffects
    {
        static readonly Dictionary<PowerDef, IDropEffect> _cache = new Dictionary<PowerDef, IDropEffect>();

        public static int DelayOf(PowerDef def) => (int)def.ParamNumber("delay", 0f);

        public static IDropEffect Create(PowerDef def, ContentDB db)
        {
            if (def.Type != "drop") return null;
            if (_cache.TryGetValue(def, out var cached)) return cached;
            var effect = Build(def, db);
            _cache[def] = effect;
            return effect;
        }

        static IDropEffect Build(PowerDef def, ContentDB db)
        {
            string explosion = def.ParamString("explosion");
            if (explosion != null)
            {
                var size = explosion == "medium" ? ExplosionSize.Medium : explosion == "huge" ? ExplosionSize.Huge : explosion == "void" ? ExplosionSize.Void : ExplosionSize.Small;
                var spec = ExplosionSpec.Preset(size);
                spec.Radius = def.ParamNumber("radius", spec.Radius);
                if (def.ParamNumber("fire", 0f) > 0f) spec.FireChance = math.min(1f, spec.FireChance + def.ParamNumber("fire", 0f) / 255f);
                if (def.ParamNumber("irradiate", 0f) > 0f) spec.Irradiate = true;
                string tile = def.ParamString("tile");
                if (tile != null) spec.CenterTile = db.Tiles.IdOrDefault("tile." + tile);
                string leaves = def.ParamString("leaves");
                ushort feature = leaves != null && db.Features.TryGet("feat." + leaves, out var f) ? f.MapValue : (ushort)0;
                return new ExplosionEffect(spec, (int)def.ParamNumber("count", 0f), def.ParamNumber("spread", 0f), feature);
            }
            if (def.ParamString("freeze") != null) return new FreezeEffect(ArgRadius(def.ParamString("freeze"), 10));
            if (def.ParamString("knockback") != null) return new ShockwaveEffect(ArgRadius(def.ParamString("knockback"), 15));
            if (def.ParamString("vfx") != null) return new VfxEffect(def.ParamNumber("happiness", 0f));
            if (def.ParamNumber("crushBuildings", 0f) > 0f) return new StompEffect(def.ParamNumber("radius", 2f));
            if (def.ParamNumber("stun", 0f) > 0f)
                return new LightningEffect(def.ParamNumber("dmg", 150f), (int)def.ParamNumber("stun", 10f), def.ParamNumber("fire", 60f) / 100f,
                                           db.Biomes.TryGet("bio.celestial", out var sky) ? sky.MapValue : (byte)0);
            if (def.ParamNumber("meltTile", 0f) > 0f) return new AcidEffect(def.ParamNumber("dmg", 30f));
            if (def.ParamNumber("fire", 0f) > 0f && def.ParamNumber("radius", 0f) > 0f) return new NapalmEffect(def.ParamNumber("radius", 8f));
            return null;
        }

        static int ArgRadius(string v, int fallback)
        {
            int colon = v.IndexOf(':');
            return colon >= 0 && int.TryParse(v.Substring(colon + 1), out int n) ? n : fallback;
        }

        static float2 Center(in DropData d) => d.Pos;

        sealed class ExplosionEffect : IDropEffect
        {
            readonly ExplosionSpec _spec;
            readonly int _count;
            readonly float _spread;
            readonly ushort _leaves;

            public ExplosionEffect(ExplosionSpec spec, int count, float spread, ushort leaves)
            {
                _spec = spec;
                _count = count;
                _spread = spread;
                _leaves = leaves;
            }

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                if (_count > 0 && drop.Child == 0)
                {
                    // Bölüm 7.2 cluster: 8 small drops within the spread, starting low
                    var map = ctx.World;
                    for (int n = 0; n < _count && ctx.Powers.Drops.Length < PowerWorld.MaxDrops; n++)
                    {
                        float2 p = math.clamp(drop.Pos + new float2(rng.Range(-_spread, _spread), rng.Range(-_spread, _spread)), 0.5f, new float2(map.Width - 0.5f, map.Height - 0.5f));
                        ctx.Powers.Drops.Add(new DropData { Power = drop.Power, Pos = p, Z = 8f, Child = 1 });
                    }
                    return;
                }
                Explosions.Explode(ctx, drop.Pos, _spec, ref rng);
                int2 t = (int2)math.floor(drop.Pos);
                if (_leaves != 0 && ctx.World.InBounds(t.x, t.y) && !ctx.World.IsWater(t.x, t.y)) ctx.World.SetFeature(t.x, t.y, _leaves, ChangeSource.Power);
            }
        }

        sealed class FreezeEffect : IDropEffect
        {
            readonly int _radius;
            public FreezeEffect(int radius) => _radius = radius;

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                var map = ctx.World;
                int2 c = (int2)math.floor(drop.Pos);
                for (int dy = -_radius; dy <= _radius; dy++)
                    for (int dx = -_radius; dx <= _radius; dx++)
                    {
                        if (dx * dx + dy * dy > _radius * _radius || !map.InBounds(c.x + dx, c.y + dy)) continue;
                        ctx.Nature.Freeze(c.x + dx, c.y + dy);
                        ctx.Nature.Douse(c.x + dx, c.y + dy);
                    }
                WorldObjects.ForUnits(ctx, drop.Pos, _radius, (w, i, f) => w.AddStatus(i, w.StFrozen));
            }
        }

        sealed class ShockwaveEffect : IDropEffect
        {
            readonly int _radius;
            public ShockwaveEffect(int radius) => _radius = radius;

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                var u = ctx.Units.Store;
                float2 c = drop.Pos;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    float2 d = u.Pos[i] - c;
                    float dist = math.length(d);
                    if (dist > _radius || u.State[i] != UnitStore.StateAlive) continue;
                    float f = 1f - dist / _radius;
                    Explosions.Launch(u, i, 10f * f, dist > 0.01f ? d / dist * 12f * f : float2.zero);
                }
                ctx.Events.Publish(new ExplosionEvent(c, _radius));
            }
        }

        sealed class StompEffect : IDropEffect
        {
            readonly float _radius;
            public StompEffect(float radius) => _radius = radius;

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                WorldObjects.ForUnits(ctx, drop.Pos, _radius, (w, i, f) => w.Store.Kill(i, DeathCause.Crushed));
                Explosions.DamageBuildings(ctx, drop.Pos, _radius, float.MaxValue);
            }
        }

        sealed class LightningEffect : IDropEffect
        {
            readonly float _damage, _fire;
            readonly int _stun;
            readonly byte _celestial;

            public LightningEffect(float damage, int stun, float fire, byte celestial)
            {
                _damage = damage;
                _stun = stun;
                _fire = fire;
                _celestial = celestial;
            }

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                var map = ctx.World;
                int2 t = (int2)math.floor(drop.Pos);
                if (!map.InBounds(t.x, t.y) || _celestial != 0 && map.Biome[map.Index(t.x, t.y)] == _celestial) return;
                var w = ctx.Units;
                var u = w.Store;
                long tick = ctx.Clock.Tick;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (u.State[i] != UnitStore.StateAlive || math.distancesq(u.Pos[i], drop.Pos) > 1.5f * 1.5f || UnitLifeSystem.Immune(w, i, "lightning")) continue;
                    w.Damage(i, _damage, -1, DeathCause.Divine, tick);
                    if (u.State[i] == UnitStore.StateAlive && w.StStunned >= 0) u.AddStatus(i, w.StStunned, _stun);
                }
                if (rng.Chance(_fire)) ctx.Nature.Ignite(t.x, t.y, NatureState.IgniteDefault, !map.IsWater(t.x, t.y), NatureState.IgniteLightning);
                Explosions.DamageBuildings(ctx, drop.Pos, 1.5f, _damage);
            }
        }

        sealed class AcidEffect : IDropEffect
        {
            readonly float _damage;
            public AcidEffect(float damage) => _damage = damage;

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                var map = ctx.World;
                int2 t = (int2)math.floor(drop.Pos);
                if (!map.InBounds(t.x, t.y)) return;
                map.SetFeature(t.x, t.y, 0, ChangeSource.Power);
                map.LowerLevel(t.x, t.y, ChangeSource.Power);
                long tick = ctx.Clock.Tick;
                var u = ctx.Units.Store;
                for (int k = 0; k < u.Alive.Length; k++)
                    if (math.distancesq(u.Pos[u.Alive[k]], drop.Pos) <= 2.25f) ctx.Units.Damage(u.Alive[k], _damage, -1, DeathCause.Burned, tick);
                Explosions.DamageBuildings(ctx, drop.Pos, 1.5f, _damage);
            }
        }

        sealed class NapalmEffect : IDropEffect
        {
            readonly float _radius;
            public NapalmEffect(float radius) => _radius = radius;

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                var map = ctx.World;
                int r = (int)_radius;
                int2 c = (int2)math.floor(drop.Pos);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (dx * dx + dy * dy <= r * r && map.InBounds(c.x + dx, c.y + dy) && !map.IsWater(c.x + dx, c.y + dy))
                            ctx.Nature.Ignite(c.x + dx, c.y + dy, 255, true, NatureState.IgnitePower);
                WorldObjects.ForUnits(ctx, drop.Pos, _radius, (w, i, f) => w.AddStatus(i, w.StBurning));
            }
        }

        sealed class VfxEffect : IDropEffect
        {
            readonly float _happiness;
            public VfxEffect(float happiness) => _happiness = happiness;

            public void Land(in SimContext ctx, ref DropData drop, ref SimRandom rng)
            {
                ctx.Events.Publish(new ExplosionEvent(drop.Pos, 0f)); // particles only
                if (_happiness <= 0f) return;
                var u = ctx.Units.Store;
                for (int k = 0; k < u.Alive.Length; k++)
                {
                    int i = u.Alive[k];
                    if (math.distancesq(u.Pos[i], drop.Pos) <= 36f) u.Happiness[i] = (sbyte)math.min(100, u.Happiness[i] + (int)_happiness);
                }
            }
        }
    }
}
