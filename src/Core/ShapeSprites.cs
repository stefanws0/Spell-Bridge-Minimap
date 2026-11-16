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

        static readonly Rgba BurstYellow = new Rgba(255, 206, 56, 255);
        static readonly Rgba BurstCenter = new Rgba(222, 150, 24, 255);

        /// <summary>
        /// An eruption (geysers): an 8-ray yellow star with a deeper yellow center and a dark border. Drawn in its
        /// own colours (not tinted).
        /// </summary>
        public static PixelBuffer Burst(int size) => Burst(size, BurstYellow, BurstCenter);

        /// <summary>The burst in other colours (e.g. orange while a geyser erupts, grey while it recharges).</summary>
        public static PixelBuffer Burst(int size, Rgba fill, Rgba center) => Render(size, (u, v) =>
        {
            if (!InBurst(u, v)) return Rgba.Clear;
            const float t = 0.06f, d = t * 0.7f;
            bool inner = InBurst(u + t, v) && InBurst(u - t, v) && InBurst(u, v + t) && InBurst(u, v - t)
                && InBurst(u + d, v + d) && InBurst(u - d, v - d) && InBurst(u + d, v - d) && InBurst(u - d, v + d);
            if (!inner) return Border;
            return Distance(u, v) < 0.13f ? center : fill;
        });

        /// <summary>Rays point at 22.5° + k x 45°; the outline runs straight from 0.47 at a ray to 0.25 between rays.</summary>
        static bool InBurst(float u, float v)
        {
            float dx = u - 0.5f, dy = v - 0.5f;
            double sector = Math.PI / 4, a = Math.Atan2(dy, dx) + Math.PI / 8;
            double f = Math.Abs(((a % sector) + sector) % sector / sector - 0.5) * 2; // 1 at a ray, 0 between
            return Distance(u, v) < 0.25f + 0.22f * (float)f;
        }

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

        static readonly Rgba Bone = new Rgba(242, 236, 220, 255);
        static readonly Rgba BadgeDark = new Rgba(24, 24, 26, 255);

        /// <summary>
        /// A boss: a bone-white skull on a dark round badge with a coloured ring (the HUD's badge style, like the
        /// health-potion badge). Drawn in its own colours.
        /// </summary>
        public static PixelBuffer BossSkull(int size, Rgba ring) => Render(size, (u, v) =>
        {
            float d = Distance(u, v);
            if (d > 0.48f) return Rgba.Clear;
            if (d > 0.465f) return Border;
            if (d > 0.4f) return ring;
            float su = 0.5f + (u - 0.5f) / 0.62f, sv = 0.5f + (v - 0.5f) / 0.62f + 0.02f; // skull at 62%, centered
            if (InSkull(su, sv)) return SkullHole(su, sv) ? BadgeDark : Bone;
            return BadgeDark;
        });

        static bool InSkull(float u, float v)
        {
            if (Distance(u, v - 0.08f) < 0.3f) return true;                    // cranium, centered at (0.5, 0.58)
            float cx = Math.Max(0.4f, Math.Min(0.6f, u)), cy = Math.Max(0.27f, Math.Min(0.39f, v));
            float dx = u - cx, dy = v - cy;
            return dx * dx + dy * dy <= 0.07f * 0.07f;                          // jaw: rounded box 0.33-0.67 x 0.2-0.46
        }

        static bool SkullHole(float u, float v)
        {
            float lx = u - 0.385f, rx = u - 0.615f, ey = v - 0.55f;
            if (lx * lx + ey * ey < 0.085f * 0.085f || rx * rx + ey * ey < 0.085f * 0.085f) return true; // eyes
            if (InTriangle(u, v, 0.5f, 0.45f, 0.455f, 0.37f, 0.545f, 0.37f)) return true;                  // nose
            return v > 0.21f && v < 0.31f
                && (Math.Abs(u - 0.44f) < 0.015f || Math.Abs(u - 0.5f) < 0.015f || Math.Abs(u - 0.56f) < 0.015f); // teeth
        }

        /// <summary>
        /// A ghost (a dead player): white, so UI tinting gives it the player's colour, with a dark border, dark
        /// eyes and a wavy hem of three scallops.
        /// </summary>
        public static PixelBuffer Ghost(int size) => Render(size, (u, v) =>
        {
            if (!InGhost(u, v)) return Rgba.Clear;
            const float t = 0.055f, d = t * 0.7f;
            bool inner = InGhost(u + t, v) && InGhost(u - t, v) && InGhost(u, v + t) && InGhost(u, v - t)
                && InGhost(u + d, v + d) && InGhost(u - d, v - d) && InGhost(u + d, v - d) && InGhost(u - d, v + d);
            if (!inner) return Border;
            float lx = (u - 0.38f) / 0.075f, rx = (u - 0.62f) / 0.075f, ey = (v - 0.6f) / 0.105f;
            if (lx * lx + ey * ey <= 1f || rx * rx + ey * ey <= 1f) return Border;
            return Rgba.White;
        });

        static bool InGhost(float u, float v)
        {
            const float left = 0.16f, right = 0.84f, top = 0.56f, hemTop = 0.2f, hemDepth = 0.14f;
            if (u < left || u > right) return false;
            float dx = u - 0.5f, dy = v - top;
            if (v >= top) return dx * dx + dy * dy <= 0.34f * 0.34f;             // round head
            if (v >= hemTop) return true;                                       // body
            // three scallops: each hangs down to hemTop - hemDepth in its middle and rises to hemTop at its edges
            float s = (u - left) / (right - left) * 3f;
            float f = s - (float)Math.Floor(s);
            float bottom = hemTop - hemDepth * (float)Math.Sin(f * Math.PI);
            return v >= bottom;
        }

        // ---- objective pieces -------------------------------------------------------------------------------

        static readonly Rgba CapRed = new Rgba(214, 58, 64, 255);
        static readonly Rgba Cream = new Rgba(240, 232, 210, 255);
        static readonly Rgba Wood = new Rgba(186, 132, 78, 255);
        static readonly Rgba Plank = new Rgba(120, 80, 40, 255);
        static readonly Rgba StoneGrey = new Rgba(150, 152, 160, 255);
        static readonly Rgba HuntRed = new Rgba(255, 70, 60, 255);

        /// <summary>Outlined shape: <paramref name="fill"/> inside, a dark border <paramref name="t"/> wide.</summary>
        static PixelBuffer Outlined(int size, Func<float, float, bool> inside, Func<float, float, Rgba> fill, float t) => Render(size, (u, v) =>
        {
            if (!inside(u, v)) return Rgba.Clear;
            float d = t * 0.7f;
            bool inner = inside(u + t, v) && inside(u - t, v) && inside(u, v + t) && inside(u, v - t)
                && inside(u + d, v + d) && inside(u - d, v - d) && inside(u + d, v - d) && inside(u - d, v + d);
            return inner ? fill(u, v) : Border;
        });

        /// <summary>A collect-objective mushroom: red cap with white spots on a cream stem.</summary>
        public static PixelBuffer Mushroom(int size) => Outlined(size, InMushroom, (u, v) =>
        {
            if (v < 0.42f) return Cream;
            if (Dist(u, v, 0.33f, 0.63f) < 0.07f || Dist(u, v, 0.58f, 0.74f) < 0.06f || Dist(u, v, 0.72f, 0.56f) < 0.055f) return Rgba.White;
            return CapRed;
        }, 0.06f);

        static bool InMushroom(float u, float v)
        {
            float ex = (u - 0.5f) / 0.44f, ey = (v - 0.42f) / 0.42f;
            if (v >= 0.4f && ex * ex + ey * ey <= 1f) return true;                 // dome
            return RoundBox(u, v, 0.36f, 0.08f, 0.64f, 0.46f, 0.06f);              // stem
        }

        /// <summary>A push-objective object: a wooden crate with a cross plank.</summary>
        public static PixelBuffer Crate(int size) => Outlined(size, (u, v) => RoundBox(u, v, 0.12f, 0.12f, 0.88f, 0.88f, 0.1f), (u, v) =>
        {
            float a = Math.Abs((u - 0.5f) - (v - 0.5f)), b = Math.Abs((u - 0.5f) + (v - 0.5f));
            return Math.Min(a, b) < 0.07f ? Plank : Wood;
        }, 0.07f);

        /// <summary>A herding target: a cream paw print.</summary>
        public static PixelBuffer Paw(int size) => Outlined(size, InPaw, (u, v) => Cream, 0.05f);

        static bool InPaw(float u, float v)
        {
            float ex = (u - 0.5f) / 0.23f, ey = (v - 0.34f) / 0.19f;
            if (ex * ex + ey * ey <= 1f) return true;
            return Dist(u, v, 0.25f, 0.6f) < 0.11f || Dist(u, v, 0.41f, 0.75f) < 0.11f
                || Dist(u, v, 0.59f, 0.75f) < 0.11f || Dist(u, v, 0.75f, 0.6f) < 0.11f;
        }

        /// <summary>A petrified enemy to break: a grey stone with a crack.</summary>
        public static PixelBuffer Stone(int size) => Outlined(size, InStone, (u, v) =>
        {
            bool crack = SegmentDistance(u, v, 0.44f, 0.82f, 0.54f, 0.56f) < 0.035f
                      || SegmentDistance(u, v, 0.54f, 0.56f, 0.48f, 0.3f) < 0.035f;
            return crack ? Border : StoneGrey;
        }, 0.06f);

        static bool InStone(float u, float v)
        {
            float dx = Math.Abs(u - 0.5f), dy = Math.Abs(v - 0.5f);
            return Math.Max(dx * 0.866f + dy * 0.5f, dy) <= 0.4f;                  // a hexagon
        }

        /// <summary>A hunt target: a red ring with four ticks and an open middle (the enemy's dot shows through).</summary>
        public static PixelBuffer Crosshair(int size) => Render(size, (u, v) =>
        {
            float r = Distance(u, v);
            float d = Math.Abs(r - 0.3f);
            d = Math.Min(d, SegmentDistance(u, v, 0.5f, 0.66f, 0.5f, 0.95f));
            d = Math.Min(d, SegmentDistance(u, v, 0.5f, 0.05f, 0.5f, 0.34f));
            d = Math.Min(d, SegmentDistance(u, v, 0.05f, 0.5f, 0.34f, 0.5f));
            d = Math.Min(d, SegmentDistance(u, v, 0.66f, 0.5f, 0.95f, 0.5f));
            if (d < 0.035f) return HuntRed;
            if (d < 0.07f) return Border;
            return Rgba.Clear;
        });

        static float Dist(float u, float v, float x, float y) => (float)Math.Sqrt((u - x) * (u - x) + (v - y) * (v - y));

        static bool RoundBox(float u, float v, float x0, float y0, float x1, float y1, float r)
        {
            float cx = Math.Max(x0 + r, Math.Min(x1 - r, u)), cy = Math.Max(y0 + r, Math.Min(y1 - r, v));
            return Dist(u, v, cx, cy) <= r;
        }

        static float SegmentDistance(float u, float v, float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t = Math.Max(0f, Math.Min(1f, ((u - x0) * dx + (v - y0) * dy) / (dx * dx + dy * dy)));
            return Dist(u, v, x0 + t * dx, y0 + t * dy);
        }

        static readonly Rgba WindGrey = new Rgba(200, 204, 212, 255);

        /// <summary>A tornado: a light grey funnel, wide at the top, with a dark border and dark swirl lines.</summary>
        public static PixelBuffer Tornado(int size) => Render(size, (u, v) =>
        {
            if (!InFunnel(u, v)) return Rgba.Clear;
            const float t = 0.045f, d = t * 0.7f;
            bool inner = InFunnel(u + t, v) && InFunnel(u - t, v) && InFunnel(u, v + t) && InFunnel(u, v - t)
                && InFunnel(u + d, v + d) && InFunnel(u - d, v - d) && InFunnel(u + d, v - d) && InFunnel(u - d, v + d);
            if (!inner) return Border;
            foreach (float y in new[] { 0.36f, 0.56f, 0.76f })
            {
                float line = y + (u - 0.5f) * 0.12f;
                if (Math.Abs(v - line) < 0.022f && Math.Abs(u - FunnelCenter(v)) < FunnelHalf(v) * 0.8f) return Border;
            }
            return WindGrey;
        });

        static float FunnelCenter(float v) => 0.5f + 0.06f * (float)Math.Sin(v * 6.5f);
        static float FunnelHalf(float v) => 0.05f + 0.37f * (float)Math.Pow(Math.Max(0f, (v - 0.07f) / 0.86f), 1.4f);
        static bool InFunnel(float u, float v) => v > 0.07f && v < 0.93f && Math.Abs(u - FunnelCenter(v)) <= FunnelHalf(v);

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
