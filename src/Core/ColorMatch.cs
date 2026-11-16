using System;

namespace SpellBrigadeMinimap.Core
{
    public readonly struct ChannelStats
    {
        public readonly float Mean, Std;

        public ChannelStats(float mean, float std)
        {
            Mean = mean;
            Std = std;
        }
    }

    /// <summary>Brightness (Y) and tint (Cb = blue-yellow, Cr = red-green) statistics of a set of pixels.</summary>
    public readonly struct ColorStats
    {
        public readonly ChannelStats Y, Cb, Cr;
        public readonly int Count;

        public ColorStats(ChannelStats y, ChannelStats cb, ChannelStats cr, int count)
        {
            Y = y; Cb = cb; Cr = cr; Count = count;
        }

        public override string ToString()
            => $"brightness {Y.Mean:0} (spread {Y.Std:0}), tint Cb {Cb.Mean:0} Cr {Cr.Mean:0}, {Count} px";
    }

    /// <summary>
    /// Brings one picture's look in line with another's. Fog at a fixed distance blends every pixel toward
    /// one colour by the same amount, which lowers contrast equally in all channels and adds a tint; this
    /// undoes that with a single contrast factor (capped) and a tint shift (limited), so a reference that
    /// happens to contain odd colours can't recolour or over-sharpen the map.
    /// </summary>
    public static class ColorMatch
    {
        public const float MinContrast = 0.5f, MaxContrast = 2.5f, MaxTintShift = 32f;

        /// <summary>
        /// Mean and spread of brightness and tint over the opaque pixels <paramref name="include"/> accepts
        /// (null = all), ignoring the darkest and brightest <paramref name="trim"/> fraction.
        /// </summary>
        public static ColorStats Measure(PixelBuffer img, Func<int, int, bool> include, float trim)
        {
            var histogram = new int[256];
            int total = 0;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    if (c.A == 0 || (include != null && !include(x, y))) continue;
                    histogram[Luma(c)]++;
                    total++;
                }

            int lo = 0, hi = 255;
            if (trim > 0f && total > 0)
            {
                int cut = (int)(total * trim), sum = 0;
                for (lo = 0; lo < 255; lo++) { sum += histogram[lo]; if (sum > cut) break; }
                sum = 0;
                for (hi = 255; hi > 0; hi--) { sum += histogram[hi]; if (sum > cut) break; }
            }

            double sy = 0, sb = 0, sr = 0, qy = 0, qb = 0, qr = 0;
            int n = 0;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    if (c.A == 0 || (include != null && !include(x, y))) continue;
                    int l = Luma(c);
                    if (l < lo || l > hi) continue;
                    ToYCbCr(c, out float yy, out float cb, out float cr);
                    sy += yy; sb += cb; sr += cr;
                    qy += yy * yy; qb += cb * cb; qr += cr * cr;
                    n++;
                }
            if (n == 0) return new ColorStats(default, default, default, 0);
            return new ColorStats(Channel(sy, qy, n), Channel(sb, qb, n), Channel(sr, qr, n), n);
        }

        /// <summary>
        /// Scales every pixel's distance from the picture's mean by one contrast factor (the ratio of brightness
        /// spreads, clamped to [MinContrast, MaxContrast]), moves the brightness to the reference's mean, and
        /// shifts the tint toward the reference's by at most MaxTintShift. Alpha is kept.
        /// </summary>
        public static PixelBuffer Transfer(PixelBuffer img, in ColorStats from, in ColorStats to, float maxTintShift = MaxTintShift)
        {
            float k = from.Y.Std < 1f ? 1f : Math.Min(MaxContrast, Math.Max(MinContrast, to.Y.Std / from.Y.Std));
            float cbShift = Math.Min(maxTintShift, Math.Max(-maxTintShift, to.Cb.Mean - from.Cb.Mean));
            float crShift = Math.Min(maxTintShift, Math.Max(-maxTintShift, to.Cr.Mean - from.Cr.Mean));
            float cbTarget = from.Cb.Mean + cbShift, crTarget = from.Cr.Mean + crShift;

            var dst = new PixelBuffer(img.Width, img.Height);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    ToYCbCr(c, out float yy, out float cb, out float cr);
                    float y2 = (yy - from.Y.Mean) * k + to.Y.Mean;
                    float cb2 = (cb - from.Cb.Mean) * k + cbTarget;
                    float cr2 = (cr - from.Cr.Mean) * k + crTarget;
                    dst.SetPixel(x, y, new Rgba(
                        Byte(y2 + 1.402f * cr2),
                        Byte(y2 - 0.344136f * cb2 - 0.714136f * cr2),
                        Byte(y2 + 1.772f * cb2),
                        c.A));
                }
            return dst;
        }

        static void ToYCbCr(Rgba c, out float y, out float cb, out float cr)
        {
            y = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
            cb = -0.168736f * c.R - 0.331264f * c.G + 0.5f * c.B;
            cr = 0.5f * c.R - 0.418688f * c.G - 0.081312f * c.B;
        }

        static int Luma(Rgba c) => (c.R * 299 + c.G * 587 + c.B * 114) / 1000;

        static ChannelStats Channel(double sum, double squares, int n)
        {
            double mean = sum / n;
            double variance = Math.Max(0.0, squares / n - mean * mean);
            return new ChannelStats((float)mean, (float)Math.Sqrt(variance));
        }

        static byte Byte(float v) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v)));
    }
}
