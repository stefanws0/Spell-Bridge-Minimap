using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// Turns a coloured game icon into a white copy that the UI can tint any colour, keeping its light and dark:
    /// each pixel's brightness, relative to the icon's brightest, becomes a grey level between <c>floor</c> and
    /// white (raised to <c>gamma</c>, so the game's dark lines stay clearly darker), and its alpha stays. Tinted
    /// blue, the icon's light parts turn blue and its dark lines stay dark. <see cref="Outline"/> then puts a dark
    /// edge around it, so it reads on any ground at minimap size.
    /// </summary>
    public static class IconMask
    {
        const int SolidAlpha = 64;
        const int ShapeAlpha = 128;

        public static PixelBuffer ToWhite(PixelBuffer src, float floor = 0.12f, float gamma = 1.6f)
        {
            float max = 0f;
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var c = src.GetPixel(x, y);
                    if (c.A >= SolidAlpha) max = Math.Max(max, Luma(c));
                }
            var dst = new PixelBuffer(src.Width, src.Height);
            if (max <= 0f) max = 1f;
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var c = src.GetPixel(x, y);
                    if (c.A == 0) continue;
                    float t = Math.Min(1f, Luma(c) / max);
                    float v = floor + (1f - floor) * (float)Math.Pow(t, gamma);
                    byte g = (byte)Math.Round(v * 255f);
                    dst.SetPixel(x, y, new Rgba(g, g, g, c.A));
                }
            return dst;
        }

        /// <summary>
        /// The icon with a <paramref name="ring"/>-coloured edge <paramref name="width"/> pixels wide around its shape
        /// (its pixels at least half opaque), soft at the outer side. The result is <paramref name="width"/> + 1
        /// pixels bigger on every side, with the icon in the middle. A dark ring stays dark when the UI tints it.
        /// </summary>
        public static PixelBuffer Outline(PixelBuffer src, int width, Rgba ring)
        {
            int pad = Math.Max(0, width) + 1;
            int w = src.Width + 2 * pad, h = src.Height + 2 * pad;
            // Distance from each pixel to the nearest shape pixel: two chamfer passes (1 straight, 1.414 diagonal).
            var dist = new float[w * h];
            const float Far = 1e9f, Diag = 1.41421356f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = x - pad, sy = y - pad;
                    bool shape = sx >= 0 && sy >= 0 && sx < src.Width && sy < src.Height && src.GetPixel(sx, sy).A >= ShapeAlpha;
                    dist[y * w + x] = shape ? 0f : Far;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = dist[y * w + x];
                    if (x > 0) d = Math.Min(d, dist[y * w + x - 1] + 1f);
                    if (y > 0)
                    {
                        d = Math.Min(d, dist[(y - 1) * w + x] + 1f);
                        if (x > 0) d = Math.Min(d, dist[(y - 1) * w + x - 1] + Diag);
                        if (x < w - 1) d = Math.Min(d, dist[(y - 1) * w + x + 1] + Diag);
                    }
                    dist[y * w + x] = d;
                }
            for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    float d = dist[y * w + x];
                    if (x < w - 1) d = Math.Min(d, dist[y * w + x + 1] + 1f);
                    if (y < h - 1)
                    {
                        d = Math.Min(d, dist[(y + 1) * w + x] + 1f);
                        if (x < w - 1) d = Math.Min(d, dist[(y + 1) * w + x + 1] + Diag);
                        if (x > 0) d = Math.Min(d, dist[(y + 1) * w + x - 1] + Diag);
                    }
                    dist[y * w + x] = d;
                }

            var dst = new PixelBuffer(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ringA = Math.Max(0f, Math.Min(1f, width + 0.5f - dist[y * w + x])) * (ring.A / 255f);
                    int sx = x - pad, sy = y - pad;
                    Rgba c = sx >= 0 && sy >= 0 && sx < src.Width && sy < src.Height ? src.GetPixel(sx, sy) : Rgba.Clear;
                    float a = c.A / 255f;
                    float outA = a + ringA * (1f - a);                                   // the icon over the ring
                    if (outA <= 0f) continue;
                    dst.SetPixel(x, y, new Rgba(Mix(c.R, ring.R, a, ringA, outA), Mix(c.G, ring.G, a, ringA, outA),
                        Mix(c.B, ring.B, a, ringA, outA), (byte)Math.Round(outA * 255f)));
                }
            return dst;
        }

        static byte Mix(byte top, byte under, float a, float ringA, float outA) =>
            (byte)Math.Round((top * a + under * ringA * (1f - a)) / outA);

        /// <summary>A copy of a white icon multiplied by <paramref name="c"/> (alpha kept).</summary>
        public static PixelBuffer Tint(PixelBuffer white, Rgba c)
        {
            var dst = new PixelBuffer(white.Width, white.Height);
            for (int y = 0; y < white.Height; y++)
                for (int x = 0; x < white.Width; x++)
                {
                    var p = white.GetPixel(x, y);
                    if (p.A == 0) continue;
                    dst.SetPixel(x, y, new Rgba((byte)(p.R * c.R / 255), (byte)(p.G * c.G / 255), (byte)(p.B * c.B / 255), p.A));
                }
            return dst;
        }

        /// <summary>
        /// Fills the icon's holes (see-through pixels the outside can't reach without crossing the shape) with
        /// <paramref name="c"/>, in place. Returns how many pixels were filled.
        /// </summary>
        public static int FillHoles(PixelBuffer img, Rgba c)
        {
            int w = img.Width, h = img.Height;
            var outside = new bool[w * h];
            var stack = new Stack<int>();
            void Push(int x, int y)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                int i = y * w + x;
                if (outside[i] || img.GetPixel(x, y).A >= ShapeAlpha) return;
                outside[i] = true;
                stack.Push(i);
            }
            for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
            for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                Push(x + 1, y); Push(x - 1, y); Push(x, y + 1); Push(x, y - 1);
            }
            int filled = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (!outside[y * w + x] && img.GetPixel(x, y).A < ShapeAlpha)
                    {
                        img.SetPixel(x, y, c);
                        filled++;
                    }
            return filled;
        }

        /// <summary>The box around the icon's shape (pixels at least half opaque); false if it has none.</summary>
        public static bool ShapeBounds(PixelBuffer img, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = minY = int.MaxValue;
            maxX = maxY = -1;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    if (img.GetPixel(x, y).A < ShapeAlpha) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            return maxX >= 0;
        }

        /// <summary>
        /// The game's petrified-enemy icon (a statue with a crown-shaped hole) as a grey stone statue with small
        /// coloured details: the hole filled with <paramref name="detail"/>, and two eyes on the head. The eyes sit
        /// at fixed spots of the shape's box (rows count up from the bottom, as in <see cref="PixelBuffer"/>).
        /// </summary>
        public static PixelBuffer Statue(PixelBuffer original, Rgba stone, Rgba detail)
        {
            var img = Tint(ToWhite(original), stone);
            FillHoles(img, detail);
            if (ShapeBounds(img, out int x0, out int y0, out int x1, out int y1))
            {
                float bw = x1 - x0 + 1, bh = y1 - y0 + 1, r = EyeRadius * bw;
                foreach (float fx in new[] { 0.36f, 0.64f })
                    Disc(img, x0 + fx * bw, y0 + EyeHeight * bh, r, detail);
            }
            return img;
        }

        const float EyeRadius = 0.095f, EyeHeight = 0.74f;

        static void Disc(PixelBuffer img, float cx, float cy, float r, Rgba c)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                {
                    if (x < 0 || y < 0 || x >= img.Width || y >= img.Height) continue;
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) img.SetPixel(x, y, c);
                }
        }

        /// <summary>The average colour of the icon's solid pixels (for the log), or clear if it has none.</summary>
        public static Rgba AverageColour(PixelBuffer src)
        {
            long r = 0, g = 0, b = 0, n = 0;
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var c = src.GetPixel(x, y);
                    if (c.A < SolidAlpha) continue;
                    r += c.R;
                    g += c.G;
                    b += c.B;
                    n++;
                }
            return n == 0 ? Rgba.Clear : new Rgba((byte)(r / n), (byte)(g / n), (byte)(b / n));
        }

        static float Luma(Rgba c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
    }
}
