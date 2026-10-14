using UnityEngine;

namespace PG.Render
{
    // Tiny pixel props are rasterized into the same indexed atlas as units, preserving one batched mesh.
    public static class UnitPropAtlas
    {
        public const int Size = 16;
        const byte Outline = 28, Body = 84, BodyLight = 112, Wood = 140, Gold = 196, Steel = 224;

        public static Texture2D Create(UnitProp prop)
        {
            if (prop <= UnitProp.None || prop >= UnitProp.Count) return null;
            var pixels = new Color32[Size * Size];
            switch (prop)
            {
                case UnitProp.Sword:
                    Stroke(pixels, 5, 3, 13, 13, Steel);
                    Stroke(pixels, 4, 3, 8, 7, Gold);
                    Stroke(pixels, 3, 2, 6, 5, Wood);
                    break;
                case UnitProp.Axe:
                    Stroke(pixels, 6, 2, 10, 12, Wood);
                    Rect(pixels, 8, 10, 5, 4, Outline);
                    Rect(pixels, 9, 11, 3, 2, Steel);
                    Set(pixels, 12, 11, Gold);
                    break;
                case UnitProp.Spear:
                    Stroke(pixels, 4, 2, 11, 13, Wood);
                    Stroke(pixels, 10, 11, 13, 15, Steel);
                    Set(pixels, 13, 14, Gold);
                    break;
                case UnitProp.Bow:
                    Stroke(pixels, 7, 2, 11, 5, Wood);
                    Stroke(pixels, 11, 5, 12, 10, Wood);
                    Stroke(pixels, 12, 10, 9, 14, Wood);
                    Stroke(pixels, 7, 8, 14, 8, Gold);
                    break;
                case UnitProp.Crossbow:
                    Stroke(pixels, 5, 7, 14, 7, Wood);
                    Stroke(pixels, 5, 11, 13, 11, Steel);
                    Stroke(pixels, 6, 12, 12, 10, Wood);
                    Stroke(pixels, 9, 3, 9, 10, Gold);
                    break;
                case UnitProp.Hammer:
                    Stroke(pixels, 6, 2, 10, 10, Wood);
                    Rect(pixels, 4, 10, 9, 4, Outline);
                    Rect(pixels, 5, 11, 7, 2, Steel);
                    Set(pixels, 10, 12, Gold);
                    break;
                case UnitProp.Dagger:
                    Stroke(pixels, 6, 4, 12, 12, Steel);
                    Stroke(pixels, 5, 3, 9, 7, Gold);
                    Stroke(pixels, 4, 2, 6, 4, Wood);
                    break;
                case UnitProp.Staff:
                    Stroke(pixels, 5, 2, 10, 12, Wood);
                    Rect(pixels, 7, 11, 5, 4, Outline);
                    Rect(pixels, 8, 12, 3, 2, Gold);
                    Set(pixels, 9, 13, Steel);
                    break;
                case UnitProp.Club:
                    Stroke(pixels, 5, 2, 10, 10, Wood);
                    Rect(pixels, 8, 9, 5, 5, Outline);
                    Rect(pixels, 9, 10, 3, 3, Gold);
                    break;
                case UnitProp.Trident:
                    Stroke(pixels, 5, 2, 9, 12, Wood);
                    Stroke(pixels, 7, 12, 7, 15, Steel);
                    Stroke(pixels, 9, 12, 9, 15, Steel);
                    Stroke(pixels, 11, 12, 11, 15, Steel);
                    Stroke(pixels, 7, 12, 11, 12, Gold);
                    break;
                case UnitProp.Sling:
                    Stroke(pixels, 5, 3, 11, 10, Wood);
                    Stroke(pixels, 10, 10, 14, 12, Gold);
                    Set(pixels, 14, 13, Steel);
                    break;
                case UnitProp.Pickaxe:
                    Stroke(pixels, 5, 2, 10, 11, Wood);
                    Stroke(pixels, 4, 13, 13, 11, Steel);
                    Set(pixels, 4, 12, Gold);
                    Set(pixels, 13, 10, Gold);
                    break;
                case UnitProp.Scythe:
                    Stroke(pixels, 5, 2, 8, 12, Wood);
                    Stroke(pixels, 8, 11, 11, 14, Steel);
                    Stroke(pixels, 11, 14, 14, 14, Steel);
                    Stroke(pixels, 8, 11, 12, 11, Gold);
                    break;
                case UnitProp.Musket:
                    Stroke(pixels, 3, 6, 14, 9, Wood);
                    Stroke(pixels, 7, 8, 15, 10, Steel);
                    Rect(pixels, 4, 5, 4, 3, Gold);
                    break;
                case UnitProp.UnarmedStrike:
                    Rect(pixels, 5, 6, 9, 6, Outline);
                    Rect(pixels, 6, 7, 7, 4, Body);
                    Rect(pixels, 11, 9, 4, 3, Outline);
                    Rect(pixels, 12, 10, 2, 1, BodyLight);
                    Stroke(pixels, 5, 7, 8, 5, Body);
                    break;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            { name = "UnitProp_" + prop, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        public static Texture2D CreateHeart()
        {
            string[] rows = { "01100110", "11111111", "11111111", "01111110", "00111100", "00011000" };
            var pixels = new Color32[8 * 8];
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < 8; x++)
                if (rows[y][x] == '1') pixels[(6 - y) * 8 + x] = new Color32(Steel, Steel, Steel, 255);
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            { name = "CourtshipHeart", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static void Stroke(Color32[] pixels, int x0, int y0, int x1, int y1, byte color)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Set(pixels, x0, y0, Outline);
                Set(pixels, x0 + 1, y0, Outline);
                Set(pixels, x0, y0 + 1, Outline);
                Set(pixels, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        static void Rect(Color32[] pixels, int x, int y, int width, int height, byte color)
        {
            for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++) Set(pixels, px, py, color);
        }

        static void Set(Color32[] pixels, int x, int y, byte gray)
        {
            if ((uint)x >= Size || (uint)y >= Size) return;
            pixels[y * Size + x] = new Color32(gray, gray, gray, 255);
        }
    }
}
