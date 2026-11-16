using System;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ColorMatchTests
    {
        static PixelBuffer Gradient()
        {
            var img = new PixelBuffer(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    img.SetPixel(x, y, new Rgba((byte)(x * 12 + 20), (byte)(y * 10 + 30), (byte)((x + y) * 5 + 40)));
            return img;
        }

        static PixelBuffer Fill(int w, int h, Rgba c)
        {
            var img = new PixelBuffer(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    img.SetPixel(x, y, c);
            return img;
        }

        public static void MeasuresMeanAndSpread()
        {
            var s = ColorMatch.Measure(Fill(4, 4, new Rgba(100, 150, 200)), null, 0f);
            Check.Equal(16, s.Count, "count");
            Check.Near(100f, s.R.Mean, "r mean");
            Check.Near(150f, s.G.Mean, "g mean");
            Check.Near(200f, s.B.Mean, "b mean");
            Check.Near(0f, s.R.Std, "r spread");
        }

        public static void SkipsTransparentPixels()
        {
            var img = Fill(4, 4, new Rgba(100, 100, 100));
            img.SetPixel(0, 0, new Rgba(255, 255, 255, 0));
            Check.Equal(15, ColorMatch.Measure(img, null, 0f).Count, "transparent pixel skipped");
        }

        public static void IncludeFilterLimitsThePixelsMeasured()
        {
            var img = Fill(4, 4, new Rgba(200, 200, 200));
            for (int y = 0; y < 4; y++) { img.SetPixel(0, y, new Rgba(50, 50, 50)); img.SetPixel(1, y, new Rgba(50, 50, 50)); }
            var s = ColorMatch.Measure(img, (x, y) => x < 2, 0f);
            Check.Near(50f, s.R.Mean, "left half only");
        }

        public static void TrimIgnoresBrightOutliers()
        {
            var img = Fill(20, 20, new Rgba(100, 100, 100));
            for (int i = 0; i < 8; i++) img.SetPixel(i, 0, new Rgba(255, 255, 255));
            var s = ColorMatch.Measure(img, null, 0.05f);
            Check.Near(100f, s.R.Mean, "outliers trimmed", 0.5f);
        }

        public static void TransferUndoesAFogBlend()
        {
            var clear = Gradient();
            var foggy = new PixelBuffer(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Rgba c = clear.GetPixel(x, y);
                    foggy.SetPixel(x, y, new Rgba(
                        (byte)Math.Round(0.4 * c.R + 0.6 * 120),
                        (byte)Math.Round(0.4 * c.G + 0.6 * 190),
                        (byte)Math.Round(0.4 * c.B + 0.6 * 180)));
                }

            var fixedImg = ColorMatch.Transfer(foggy, ColorMatch.Measure(foggy, null, 0f), ColorMatch.Measure(clear, null, 0f));
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    Rgba want = clear.GetPixel(x, y), got = fixedImg.GetPixel(x, y);
                    Check.True(Math.Abs(want.R - got.R) <= 2 && Math.Abs(want.G - got.G) <= 2 && Math.Abs(want.B - got.B) <= 2,
                        $"pixel ({x},{y}): want {want}, got {got}");
                }
        }

        public static void TransferClampsAndKeepsAlpha()
        {
            var img = new PixelBuffer(1, 1);
            img.SetPixel(0, 0, new Rgba(250, 10, 128, 77));
            var from = new ColorStats(new ChannelStats(100f, 10f), new ChannelStats(100f, 10f), new ChannelStats(128f, 10f), 1);
            var to = new ColorStats(new ChannelStats(200f, 40f), new ChannelStats(200f, 40f), new ChannelStats(128f, 10f), 1);
            Check.Equal(new Rgba(255, 0, 128, 77), ColorMatch.Transfer(img, from, to).GetPixel(0, 0), "clamped, alpha kept");
        }
    }
}
