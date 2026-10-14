using UnityEngine;

namespace PG.Render
{
    // Coordinates measured on the masked 40x40 detail frames, from the top left.
    // Keep the handle's grip pixel pinned to the hand even when mirroring or swinging.
    public static class UnitPropMotion
    {
        static readonly Vector2[] Human = { new Vector2(25,26), new Vector2(25,26), new Vector2(25,25), new Vector2(26,25), new Vector2(26,25), new Vector2(25,25) };
        static readonly Vector2[] Orc = { new Vector2(27,25), new Vector2(26,25), new Vector2(28,23), new Vector2(29,22), new Vector2(29,23), new Vector2(29,23) };
        static readonly Vector2[] Dwarf = { new Vector2(25,27), new Vector2(25,27), new Vector2(27,25), new Vector2(27,25), new Vector2(27,25), new Vector2(27,25) };
        static readonly Vector2[] Elf = { new Vector2(25,25), new Vector2(26,25), new Vector2(26,24), new Vector2(27,23), new Vector2(27,23), new Vector2(27,23) };
        static readonly Vector2[] HumanFemale = { new Vector2(25,25), new Vector2(25,25), new Vector2(26,24), new Vector2(27,23), new Vector2(26,24), new Vector2(26,24) };
        static readonly Vector2[] OrcFemale = { new Vector2(27,25), new Vector2(27,25), new Vector2(28,22), new Vector2(28,22), new Vector2(28,23), new Vector2(28,23) };
        static readonly Vector2[] DwarfFemale = { new Vector2(25,27), new Vector2(25,27), new Vector2(27,24), new Vector2(27,24), new Vector2(27,24), new Vector2(26,24) };
        static readonly Vector2[] ElfFemale = { new Vector2(26,25), new Vector2(26,25), new Vector2(27,24), new Vector2(28,22), new Vector2(27,23), new Vector2(27,23) };

        public static Vector2 Hand(string species, int animation, int frame, bool detailed, bool female = false)
        {
            if (!detailed) return new Vector2(.7f, .45f);
            Vector2[] points;
            switch (species)
            {
                case "sp.human": points = female ? HumanFemale : Human; break;
                case "sp.orc": points = female ? OrcFemale : Orc; break;
                case "sp.dwarf": points = female ? DwarfFemale : Dwarf; break;
                case "sp.elf": points = female ? ElfFemale : Elf; break;
                default: return new Vector2(.7f, .45f);
            }
            int index = animation == 1 ? 2 + Mathf.Clamp(frame, 0, 3) : Mathf.Clamp(frame, 0, 1);
            Vector2 pixel = points[index];
            return new Vector2((pixel.x + .5f) / 40f, 1f - (pixel.y + .5f) / 40f);
        }

        public static Vector2 Grip(UnitProp prop)
        {
            Vector2 pixel;
            switch (prop)
            {
                case UnitProp.Sword: pixel = new Vector2(5,4); break;
                case UnitProp.Dagger: pixel = new Vector2(5,3); break;
                case UnitProp.Axe: pixel = new Vector2(7,5); break;
                case UnitProp.Hammer: pixel = new Vector2(7,4); break;
                case UnitProp.Bow: pixel = new Vector2(12,8); break;
                case UnitProp.Crossbow: pixel = new Vector2(9,5); break;
                case UnitProp.Musket: pixel = new Vector2(6,6); break;
                case UnitProp.Staff:
                case UnitProp.Pickaxe: pixel = new Vector2(7,6); break;
                case UnitProp.Club:
                case UnitProp.Sling: pixel = new Vector2(7,5); break;
                case UnitProp.UnarmedStrike: pixel = new Vector2(7,7); break;
                default: pixel = new Vector2(6,5); break;
            }
            return (pixel + Vector2.one * .5f) / UnitPropAtlas.Size;
        }

        public static float Angle(UnitProp prop, float attackPhase, bool working, long tick, long uid, int frame)
        {
            if (attackPhase < 0f && working)
            {
                float workPhase = ((tick % 20 + uid % 20 * 7) % 20) / 19f;
                // Tool-specific arcs: harvesting sweeps, chopping and mining
                // strike from above, and building uses a shorter hammer tap.
                float stroke = Mathf.SmoothStep(0f, 1f, workPhase < .55f ? workPhase / .55f : (1f - workPhase) / .45f);
                switch (prop)
                {
                    case UnitProp.Scythe: return Mathf.Lerp(-42f, 24f, stroke);
                    case UnitProp.Axe: return Mathf.Lerp(58f, -72f, stroke);
                    case UnitProp.Pickaxe: return Mathf.Lerp(78f, -64f, stroke);
                    case UnitProp.Hammer: return Mathf.Lerp(34f, -43f, stroke);
                }
            }
            float phase = attackPhase;
            if (phase < 0f) return (frame % 2 == 0 ? -3f : 3f);
            bool ranged = prop == UnitProp.Bow || prop == UnitProp.Crossbow || prop == UnitProp.Musket;
            if (ranged) return phase < .5f ? Mathf.Lerp(0, 8, phase * 2) : Mathf.Lerp(-12, 0, (phase - .5f) * 2);
            // Wind-up, fast contact, then follow-through. The grip never leaves the hand.
            if (phase < .35f) return Mathf.Lerp(10, 65, Mathf.SmoothStep(0, 1, phase / .35f));
            if (phase < .7f) return Mathf.Lerp(65, -85, Mathf.SmoothStep(0, 1, (phase - .35f) / .35f));
            return Mathf.Lerp(-85, -15, Mathf.SmoothStep(0, 1, (phase - .7f) / .3f));
        }

        public static Vector2 Corner(Vector2 hand, Vector2 size, Vector2 grip, Vector2 uv, float angle, bool flip)
        {
            Vector2 p = Vector2.Scale(uv - grip, size);
            float r = angle * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            Vector2 offset = new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
            if (flip) offset.x = -offset.x;
            return hand + offset;
        }
    }
}
