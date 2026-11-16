using System;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>Small anti-aliased icon shapes. Shapes are white so UI tinting colors them.</summary>
    public static class ShapeSprites
    {
        const int Samples = 4;
        static readonly Rgba Border = new Rgba(20, 20, 20, 255);

        /// <summary>A filled disc. <paramref name="border"/> is the border width as a fraction of the size.</summary>
        public static PixelBuffer Disc(int size, float border) => Render(size, (u, v) =>
        {
            float d = Distance(u, v);
            if (d > 0.5f) return Rgba.Clear;
            return d > 0.5f - border ? Border : Rgba.White;
        });

        /// <summary>A hollow white circle, used for zones.</summary>
        public static PixelBuffer Ring(int size, float thickness) => Render(size, (u, v) =>
        {
            float d = Distance(u, v);
            return d <= 0.5f && d >= 0.5f - thickness ? Rgba.White : Rgba.Clear;
        });

        /// <summary>An arrowhead pointing up (+Y) with a dark border.</summary>
        public static PixelBuffer Arrow(int size) => Render(size, (u, v) =>
        {
            if (!InArrow(u, v)) return Rgba.Clear;
            const float t = 0.09f;
            bool inner = InArrow(u + t, v) && InArrow(u - t, v) && InArrow(u, v + t) && InArrow(u, v - t);
            return inner ? Rgba.White : Border;
        });

        /// <summary>A diamond (square rotated 45°) with a dark border.</summary>
        public static PixelBuffer Diamond(int size, float border) => Render(size, (u, v) =>
        {
            float d = Math.Abs(u - 0.5f) + Math.Abs(v - 0.5f);
            if (d > 0.5f) return Rgba.Clear;
            return d > 0.5f - border ? Border : Rgba.White;
        });

        // Bezel rings, as fractions of the diameter, from the outside in.
        public const float BezelOutline = 0.012f, BezelBand = 0.045f, BezelInnerLine = 0.008f, BezelShadow = 0.06f;
        /// <summary>How much of the radius the opaque part of the bezel covers, as a fraction of the diameter.</summary>
        public const float BezelThickness = BezelOutline + BezelBand + BezelInnerLine;
        static readonly Rgba BezelDark = new Rgba(30, 24, 18, 255);
        static readonly Rgba GoldShade = new Rgba(140, 108, 60, 255), GoldLight = new Rgba(240, 218, 165, 255);

        /// <summary>
        /// A round minimap border: dark outline, a gold band lit from above, a dark inner line, and a soft
        /// shadow fading inward over the map. The center is transparent.
        /// </summary>
        public static PixelBuffer Bezel(int size) => Render(size, (u, v) =>
        {
            float d = Distance(u, v);
            float outlineIn = 0.5f - BezelOutline, bandIn = outlineIn - BezelBand, lineIn = bandIn - BezelInnerLine;
            if (d > 0.5f) return Rgba.Clear;
            if (d > outlineIn) return BezelDark;
            if (d > bandIn) return Lerp(GoldShade, GoldLight, v);
            if (d > lineIn) return BezelDark;
            float t = (lineIn - d) / BezelShadow;
            if (t >= 1f) return Rgba.Clear;
            return new Rgba(0, 0, 0, (byte)(115 * (1f - t)));
        });

        static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return new Rgba(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t),
                255);
        }

        static PixelBuffer Render(int size, Func<float, float, Rgba> shade)
        {
            var img = new PixelBuffer(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < Samples; sy++)
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / Samples) / size;
                            float v = (y + (sy + 0.5f) / Samples) / size;
                            Rgba c = shade(u, v);
                            r += c.R * c.A;
                            g += c.G * c.A;
                            b += c.B * c.A;
                            a += c.A;
                        }
                    if (a == 0) continue;
                    img.SetPixel(x, y, new Rgba((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / (Samples * Samples))));
                }
            return img;
        }

        static float Distance(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // Tip (0.5, 0.95), right wing (0.88, 0.08), notch (0.5, 0.32), left wing (0.12, 0.08).
        static bool InArrow(float u, float v)
            => InTriangle(u, v, 0.5f, 0.95f, 0.88f, 0.08f, 0.5f, 0.32f)
            || InTriangle(u, v, 0.5f, 0.95f, 0.5f, 0.32f, 0.12f, 0.08f);

        static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
