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

    public readonly struct ColorStats
    {
        public readonly ChannelStats R, G, B;
        public readonly int Count;

        public ColorStats(ChannelStats r, ChannelStats g, ChannelStats b, int count)
        {
            R = r; G = g; B = b; Count = count;
        }

        public override string ToString()
            => $"mean ({R.Mean:0},{G.Mean:0},{B.Mean:0}) spread ({R.Std:0},{G.Std:0},{B.Std:0}) over {Count} px";
    }

    /// <summary>
    /// Matches one picture's colours to another's by lining up each channel's mean and spread. Fog at a fixed
    /// distance and most colour grading act on each channel as a scale plus an offset, so this undoes them.
    /// </summary>
    public static class ColorMatch
    {
        const float MinScale = 0.25f, MaxScale = 4f;

        /// <summary>
        /// Per-channel mean and standard deviation of the opaque pixels <paramref name="include"/> accepts
        /// (null = all), ignoring the darkest and brightest <paramref name="trim"/> fraction by luminance.
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

            double sr = 0, sg = 0, sb = 0, qr = 0, qg = 0, qb = 0;
            int n = 0;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    if (c.A == 0 || (include != null && !include(x, y))) continue;
                    int l = Luma(c);
                    if (l < lo || l > hi) continue;
                    sr += c.R; sg += c.G; sb += c.B;
                    qr += c.R * c.R; qg += c.G * c.G; qb += c.B * c.B;
                    n++;
                }
            if (n == 0) return new ColorStats(default, default, default, 0);
            return new ColorStats(Channel(sr, qr, n), Channel(sg, qg, n), Channel(sb, qb, n), n);
        }

        /// <summary>Maps each channel value c to (c - from.mean) * (to.std / from.std) + to.mean, clamped. Alpha is kept.</summary>
        public static PixelBuffer Transfer(PixelBuffer img, in ColorStats from, in ColorStats to)
        {
            var dst = new PixelBuffer(img.Width, img.Height);
            float sr = Scale(from.R, to.R), sg = Scale(from.G, to.G), sb = Scale(from.B, to.B);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    Rgba c = img.GetPixel(x, y);
                    dst.SetPixel(x, y, new Rgba(
                        Map(c.R, from.R.Mean, sr, to.R.Mean),
                        Map(c.G, from.G.Mean, sg, to.G.Mean),
                        Map(c.B, from.B.Mean, sb, to.B.Mean),
                        c.A));
                }
            return dst;
        }

        static int Luma(Rgba c) => (c.R * 299 + c.G * 587 + c.B * 114) / 1000;

        static ChannelStats Channel(double sum, double squares, int n)
        {
            double mean = sum / n;
            double variance = Math.Max(0.0, squares / n - mean * mean);
            return new ChannelStats((float)mean, (float)Math.Sqrt(variance));
        }

        static float Scale(ChannelStats from, ChannelStats to)
            => from.Std < 1f ? 1f : Math.Min(MaxScale, Math.Max(MinScale, to.Std / from.Std));

        static byte Map(byte value, float fromMean, float scale, float toMean)
        {
            double v = (value - fromMean) * scale + toMean;
            return (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
        }
    }
}
