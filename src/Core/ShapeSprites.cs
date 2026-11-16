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

        // Bezel rings, as fractions of the diameter, from the outside in. Modelled on the game's HUD circles:
        // a near-black ring with a soft cream light along the inside of its lower half.
        public const float BezelRing = 0.05f, BezelRimLight = 0.016f, BezelShadow = 0.06f;
        /// <summary>How much of the radius the opaque part of the bezel covers, as a fraction of the diameter.</summary>
        public const float BezelThickness = BezelRing;
        static readonly Rgba BezelBlack = new Rgba(20, 20, 22, 255);
        static readonly Rgba RimLight = new Rgba(253, 248, 206, 255);

        /// <summary>
        /// A round minimap border in the style of the game's HUD rings: a near-black ring, a cream rim light
        /// inside its lower half, and a soft shadow fading inward over the map. The center is transparent.
        /// </summary>
        public static PixelBuffer Bezel(int size) => Render(size, (u, v) =>
        {
            float d = Distance(u, v);
            float ringIn = 0.5f - BezelRing;
            if (d > 0.5f) return Rgba.Clear;
            if (d > ringIn) return BezelBlack;

            // Inner shadow, with the rim light laid over it; the light fades smoothly up the sides.
            float t = (ringIn - d) / BezelShadow;
            float shadow = t < 1f ? 0.45f * (1f - t) : 0f;
            float light = 0f;
            if (d > ringIn - BezelRimLight)
            {
                float s = Math.Max(0f, Math.Min(1f, (0.6f - v) / 0.45f));
                light = 0.85f * s * s * (3f - 2f * s);
            }
            float alpha = light + shadow * (1f - light);
            if (alpha <= 0.004f) return Rgba.Clear;
            float k = light / alpha; // share of the light in the mixed colour (the shadow is black)
            return new Rgba((byte)(RimLight.R * k), (byte)(RimLight.G * k), (byte)(RimLight.B * k), (byte)(255 * alpha));
        });

        static readonly Rgba Amber = new Rgba(255, 200, 48, 255);

        /// <summary>
        /// A hazard sign: an amber triangle with a dark border and a dark exclamation mark. Drawn in its own colours
        /// (not tinted), like the game's warning signs.
        /// </summary>
        public static PixelBuffer Warning(int size) => Render(size, (u, v) =>
        {
            if (!InWarning(u, v)) return Rgba.Clear;
            const float t = 0.07f;
            bool inner = InWarning(u + t, v) && InWarning(u - t, v) && InWarning(u, v + t) && InWarning(u, v - t);
            if (!inner) return Border;
            bool bar = Math.Abs(u - 0.5f) <= 0.05f && v >= 0.38f && v <= 0.72f;
            float dx = u - 0.5f, dy = v - 0.25f;
            bool dot = dx * dx + dy * dy <= 0.06f * 0.06f;
            return bar || dot ? Border : Amber;
        });

        /// <summary>
        /// A teleport portal: an upright oval ring (white, so UI tinting colours it) with a dark border and a
        /// faint see-through center.
        /// </summary>
        public static PixelBuffer Portal(int size) => Render(size, (u, v) =>
        {
            float ex = (u - 0.5f) / 0.30f, ey = (v - 0.5f) / 0.46f;
            float e = (float)Math.Sqrt(ex * ex + ey * ey);
            if (e > 1f) return Rgba.Clear;
            if (e > 0.88f) return Border;
            if (e > 0.62f) return Rgba.White;
            if (e > 0.54f) return Border;
            return new Rgba(255, 255, 255, 90);
        });

        // Apex (0.5, 0.94), base corners (0.05, 0.08) and (0.95, 0.08).
        static bool InWarning(float u, float v) => InTriangle(u, v, 0.5f, 0.94f, 0.95f, 0.08f, 0.05f, 0.08f);

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
