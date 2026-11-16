using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    public static class ArtFilter
    {
        /// <summary>
        /// From above, the grass is a pattern of light and dark blotches about as big as the cobblestone patches
        /// and stones, which drowns them out. This pulls green pixels toward their blurred neighbourhood (by
        /// <paramref name="strength"/>, weighted by how green they are) and sharpens everything else (cobblestone,
        /// dirt, stone) by <paramref name="lift"/> x (pixel - neighbourhood). Alpha is kept.
        /// </summary>
        public static PixelBuffer CalmGrass(PixelBuffer img, int radius, float strength, float lift)
        {
            int w = img.Width, h = img.Height, stride = w + 1;
            var sr = new long[stride * (h + 1)];
            var sg = new long[stride * (h + 1)];
            var sb = new long[stride * (h + 1)];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    int i = (y + 1) * stride + x + 1, up = y * stride + x + 1, left = (y + 1) * stride + x, diag = y * stride + x;
                    sr[i] = c.R + sr[up] + sr[left] - sr[diag];
                    sg[i] = c.G + sg[up] + sg[left] - sg[diag];
                    sb[i] = c.B + sb[up] + sb[left] - sb[diag];
                }

            var dst = new PixelBuffer(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int x0 = Math.Max(0, x - radius), x1 = Math.Min(w - 1, x + radius);
                    int y0 = Math.Max(0, y - radius), y1 = Math.Min(h - 1, y + radius);
                    float count = (x1 - x0 + 1) * (y1 - y0 + 1);
                    int a = (y1 + 1) * stride + x1 + 1, b = y0 * stride + x1 + 1, c0 = (y1 + 1) * stride + x0, d = y0 * stride + x0;
                    float mr = (sr[a] - sr[b] - sr[c0] + sr[d]) / count;
                    float mg = (sg[a] - sg[b] - sg[c0] + sg[d]) / count;
                    float mb = (sb[a] - sb[b] - sb[c0] + sb[d]) / count;

                    Rgba c = img.GetPixel(x, y);
                    float t = Math.Max(0f, Math.Min(1f, (c.G - Math.Max(c.R, c.B) - 2f) / 12f));
                    float grass = t * t * (3f - 2f * t);
                    float calm = grass * strength, sharpen = (1f - grass) * lift;
                    dst.SetPixel(x, y, new Rgba(
                        Byte(c.R + (mr - c.R) * calm + (c.R - mr) * sharpen),
                        Byte(c.G + (mg - c.G) * calm + (c.G - mg) * sharpen),
                        Byte(c.B + (mb - c.B) * calm + (c.B - mb) * sharpen),
                        c.A));
                }
            return dst;
        }

        /// <summary>A copy of <paramref name="under"/> with every opaque pixel of <paramref name="over"/> laid on top.</summary>
        public static PixelBuffer Overlay(PixelBuffer under, PixelBuffer over)
        {
            if (under.Width != over.Width || under.Height != over.Height)
                throw new ArgumentException("pictures must be the same size", nameof(over));
            var dst = new PixelBuffer(under.Width, under.Height);
            for (int y = 0; y < under.Height; y++)
                for (int x = 0; x < under.Width; x++)
                {
                    Rgba top = over.GetPixel(x, y);
                    dst.SetPixel(x, y, top.A > 0 ? top : under.GetPixel(x, y));
                }
            return dst;
        }

        /// <summary>
        /// Lifts the dark parts of the ground (crevices, shadows, dark blotches) toward the map's lighter ground, so
        /// that only obstacles are near-black. Pixels darker than <paramref name="floorFraction"/> x the 75th
        /// percentile brightness are moved toward the average colour of the lighter ground (60th to 90th
        /// percentile) until they sit at the floor minus <paramref name="keep"/> x how far below it they were.
        /// Only pixels where <paramref name="include"/> is true are measured or changed.
        /// </summary>
        public static PixelBuffer LiftShadows(PixelBuffer img, Func<int, int, bool> include, float floorFraction, float keep)
        {
            int w = img.Width, h = img.Height;
            var dst = new PixelBuffer(w, h);
            var lumas = new List<float>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    dst.SetPixel(x, y, c);
                    if (include(x, y)) lumas.Add(Luma(c));
                }
            if (lumas.Count < 16) return dst;
            lumas.Sort();
            float At(float q) => lumas[(int)(q * (lumas.Count - 1))];
            float floor = floorFraction * At(0.75f), lo = At(0.6f), hi = At(0.9f);

            double r = 0, g = 0, b = 0;
            int n = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!include(x, y)) continue;
                    Rgba c = img.GetPixel(x, y);
                    float l = Luma(c);
                    if (l < lo || l > hi) continue;
                    r += c.R; g += c.G; b += c.B;
                    n++;
                }
            if (n == 0) return dst;
            float rr = (float)(r / n), rg = (float)(g / n), rb = (float)(b / n);
            float refLuma = rr * 0.299f + rg * 0.587f + rb * 0.114f;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!include(x, y)) continue;
                    Rgba c = img.GetPixel(x, y);
                    float l = Luma(c);
                    if (l >= floor || refLuma <= l + 1f) continue;
                    float target = floor - (floor - l) * keep;
                    float t = Math.Max(0f, Math.Min(1f, (target - l) / (refLuma - l)));
                    dst.SetPixel(x, y, new Rgba(Byte(c.R + (rr - c.R) * t), Byte(c.G + (rg - c.G) * t), Byte(c.B + (rb - c.B) * t), c.A));
                }
            return dst;
        }

        /// <summary>
        /// Colours the pixels at least <paramref name="minCoverage"/> covered (one coverage value per pixel, row 0 at
        /// the bottom) toward <paramref name="color"/> by <paramref name="strength"/>, shaded by the pixel's own
        /// brightness so some texture stays. With <paramref name="edge"/>, covered pixels next to uncovered ones
        /// (a river's banks) take that colour instead.
        /// </summary>
        public static PixelBuffer Tint(PixelBuffer img, float[] coverage, float minCoverage, Rgba color, float strength, Rgba? edge)
        {
            int w = img.Width, h = img.Height;
            if (coverage.Length != w * h) throw new ArgumentException("one coverage value per pixel", nameof(coverage));
            bool Covered(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || coverage[y * w + x] >= minCoverage;

            var dst = new PixelBuffer(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    if (coverage[y * w + x] < minCoverage) { dst.SetPixel(x, y, c); continue; }
                    if (edge.HasValue && (!Covered(x - 1, y) || !Covered(x + 1, y) || !Covered(x, y - 1) || !Covered(x, y + 1)))
                    {
                        Rgba e = edge.Value;
                        dst.SetPixel(x, y, new Rgba(e.R, e.G, e.B, c.A));
                        continue;
                    }
                    float shade = 0.75f + 0.5f * Luma(c) / 255f;
                    dst.SetPixel(x, y, new Rgba(
                        Byte(c.R + (color.R * shade - c.R) * strength),
                        Byte(c.G + (color.G * shade - c.G) * strength),
                        Byte(c.B + (color.B * shade - c.B) * strength),
                        c.A));
                }
            return dst;
        }

        /// <summary>
        /// Coverage from a render of one object alone, limited to where the object actually shows in the full
        /// picture: a pixel stays covered only if the picture with the object differs from the picture without it
        /// by at least <paramref name="minDiff"/> in some channel, or if at least two of its four neighbours do
        /// (fills single-pixel gaps where the water matches the ground). Drops parts hidden under the ground.
        /// </summary>
        public static float[] VisibleOnly(float[] cover, PixelBuffer with, PixelBuffer without, float minCoverage, int minDiff)
        {
            int w = with.Width, h = with.Height;
            if (cover.Length != w * h || without.Width != w || without.Height != h)
                throw new ArgumentException("coverage and pictures must be the same size");
            var shows = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (cover[i] < minCoverage) continue;
                    Rgba a = with.GetPixel(x, y), b = without.GetPixel(x, y);
                    int diff = Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
                    shows[i] = diff >= minDiff;
                }
            bool Shows(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && shows[y * w + x];

            var result = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (cover[i] < minCoverage) continue;
                    int around = (Shows(x - 1, y) ? 1 : 0) + (Shows(x + 1, y) ? 1 : 0) + (Shows(x, y - 1) ? 1 : 0) + (Shows(x, y + 1) ? 1 : 0);
                    if (shows[i] || around >= 2) result[i] = cover[i];
                }
            return result;
        }

        static float Luma(Rgba c) => c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;

        static byte Byte(float v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v)));
    }
}
