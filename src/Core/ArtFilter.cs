using System;

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

        static byte Byte(float v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v)));
    }
}
