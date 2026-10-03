using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BloonsVR
{
    /// <summary>
    /// A tiny 5x7 bitmap font, rendered into a <see cref="Texture2D"/> at runtime.
    ///
    /// This exists because there is no way to get a font at runtime in this game:
    /// <c>Resources.GetBuiltinResource&lt;T&gt;</c> is one of the generic methods IL2CPP stripped, and
    /// TextMeshPro's settings object does not expose a usable static font asset. IMGUI is unavailable too,
    /// because it needs a MonoBehaviour to host OnGUI and AddComponent&lt;T&gt;() is stripped. So the label
    /// is drawn by hand — verified glyph-for-glyph by tools/font_preview.py.
    /// </summary>
    internal static class PixelFont
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int GlyphAdvance = 6;

        // Each glyph is five columns; bit N of a column is row N, top down.
        private static readonly byte[][] Glyphs = BuildGlyphs();

        /// <summary>Render <paramref name="text"/> as a texture. Returns null for empty text.</summary>
        public static Texture2D Render(string text, int scale, Color32 foreground, Color32 background)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            text = text.ToUpperInvariant();

            int columns = text.Length * GlyphAdvance - 1;
            int width = columns * scale;
            int height = GlyphHeight * scale;

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = background;

            for (int index = 0; index < text.Length; index++)
            {
                var glyph = Glyph(text[index]);
                if (glyph == null)
                    continue;

                for (int col = 0; col < GlyphWidth; col++)
                {
                    byte bits = glyph[col];
                    for (int row = 0; row < GlyphHeight; row++)
                    {
                        if ((bits & (1 << row)) == 0)
                            continue;

                        Paint(pixels, width, ((index * GlyphAdvance) + col) * scale + scale,
                            row * scale, scale, foreground);
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(new Il2CppStructArray<Color32>(pixels));
            texture.Apply();
            return texture;
        }

        /// <summary>Pixel width a string will occupy at a given scale.</summary>
        public static int MeasureWidth(string text, int scale)
            => string.IsNullOrEmpty(text) ? 0 : (text.Length * GlyphAdvance - 1) * scale;

        private static void Paint(Color32[] pixels, int width, int x0, int y0, int size, Color32 colour)
        {
            for (int dx = 0; dx < size; dx++)
            {
                for (int dy = 0; dy < size; dy++)
                {
                    int x = x0 + dx;
                    int y = y0 + dy;
                    if (x < 0 || y < 0 || x >= width || y >= GlyphHeight * size)
                        continue;

                    pixels[(y * width) + x] = colour;
                }
            }
        }

        private static byte[] Glyph(char c)
        {
            switch (c)
            {
                case 'A': return new byte[] { 0x7E, 0x11, 0x11, 0x11, 0x7E };
                case 'B': return new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x36 };
                case 'C': return new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x22 };
                case 'D': return new byte[] { 0x7F, 0x41, 0x41, 0x22, 0x1C };
                case 'E': return new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x41 };
                case 'F': return new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x01 };
                case 'G': return new byte[] { 0x3E, 0x41, 0x49, 0x49, 0x7A };
                case 'H': return new byte[] { 0x7F, 0x08, 0x08, 0x08, 0x7F };
                case 'I': return new byte[] { 0x00, 0x41, 0x7F, 0x41, 0x00 };
                case 'J': return new byte[] { 0x20, 0x40, 0x41, 0x3F, 0x01 };
                case 'K': return new byte[] { 0x7F, 0x08, 0x14, 0x22, 0x41 };
                case 'L': return new byte[] { 0x7F, 0x40, 0x40, 0x40, 0x40 };
                case 'M': return new byte[] { 0x7F, 0x02, 0x0C, 0x02, 0x7F };
                case 'N': return new byte[] { 0x7F, 0x04, 0x08, 0x10, 0x7F };
                case 'O': return new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x3E };
                case 'P': return new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x06 };
                case 'Q': return new byte[] { 0x3E, 0x41, 0x51, 0x21, 0x5E };
                case 'R': return new byte[] { 0x7F, 0x09, 0x19, 0x29, 0x46 };
                case 'S': return new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 };
                case 'T': return new byte[] { 0x01, 0x01, 0x7F, 0x01, 0x01 };
                case 'U': return new byte[] { 0x3F, 0x40, 0x40, 0x40, 0x3F };
                case 'V': return new byte[] { 0x1F, 0x20, 0x40, 0x20, 0x1F };
                case 'W': return new byte[] { 0x3F, 0x40, 0x38, 0x40, 0x3F };
                case 'X': return new byte[] { 0x63, 0x14, 0x08, 0x14, 0x63 };
                case 'Y': return new byte[] { 0x07, 0x08, 0x70, 0x08, 0x07 };
                case 'Z': return new byte[] { 0x61, 0x51, 0x49, 0x45, 0x43 };
                case '0': return new byte[] { 0x3E, 0x51, 0x49, 0x45, 0x3E };
                case '1': return new byte[] { 0x00, 0x42, 0x7F, 0x40, 0x00 };
                case '2': return new byte[] { 0x42, 0x61, 0x51, 0x49, 0x46 };
                case '3': return new byte[] { 0x21, 0x41, 0x45, 0x4B, 0x31 };
                case '4': return new byte[] { 0x18, 0x14, 0x12, 0x7F, 0x10 };
                case '5': return new byte[] { 0x27, 0x45, 0x45, 0x45, 0x39 };
                case '6': return new byte[] { 0x3C, 0x4A, 0x49, 0x49, 0x30 };
                case '7': return new byte[] { 0x01, 0x71, 0x09, 0x05, 0x03 };
                case '8': return new byte[] { 0x36, 0x49, 0x49, 0x49, 0x36 };
                case '9': return new byte[] { 0x06, 0x49, 0x49, 0x29, 0x1E };
                case '-': return new byte[] { 0x08, 0x08, 0x08, 0x08, 0x08 };
                case ':': return new byte[] { 0x00, 0x36, 0x36, 0x00, 0x00 };
                case '.': return new byte[] { 0x00, 0x60, 0x60, 0x00, 0x00 };
                case '/': return new byte[] { 0x20, 0x10, 0x08, 0x04, 0x02 };
                case '+': return new byte[] { 0x08, 0x08, 0x3E, 0x08, 0x08 };
                case ' ': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00 };
                default: return null;
            }
        }

        private static byte[][] BuildGlyphs()
        {
            // Kept as a function so the switch above stays the single source of truth; this exists only to
            // document the table's shape for anyone poking at it in a debugger.
            return System.Array.Empty<byte[]>();
        }
    }
}