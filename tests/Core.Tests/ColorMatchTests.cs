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

        static ColorStats Stats(float y, float ySpread, float cb = 0f, float cr = 0f)
            => new ColorStats(new ChannelStats(y, ySpread), new ChannelStats(cb, 5f), new ChannelStats(cr, 5f), 100);

        public static void MeasuresBrightnessAndTint()
        {
            var s = ColorMatch.Measure(Fill(4, 4, new Rgba(100, 150, 200)), null, 0f);
            Check.Equal(16, s.Count, "count");
            Check.Near(140.75f, s.Y.Mean, "luma", 0.05f);
            Check.Near(33.44f, s.Cb.Mean, "blue tint", 0.05f);
            Check.Near(-29.07f, s.Cr.Mean, "red tint", 0.05f);
            Check.Near(0f, s.Y.Std, "no spread");
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
            Check.Near(50f, ColorMatch.Measure(img, (x, y) => x < 2, 0f).Y.Mean, "left half only", 0.05f);
        }

        public static void TrimIgnoresBrightOutliers()
        {
            var img = Fill(20, 20, new Rgba(100, 100, 100));
            for (int i = 0; i < 8; i++) img.SetPixel(i, 0, new Rgba(255, 255, 255));
            Check.Near(100f, ColorMatch.Measure(img, null, 0.05f).Y.Mean, "outliers trimmed", 0.5f);
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
                    Check.True(Math.Abs(want.R - got.R) <= 3 && Math.Abs(want.G - got.G) <= 3 && Math.Abs(want.B - got.B) <= 3,
                        $"pixel ({x},{y}): want {want}, got {got}");
                }
        }

        public static void ContrastBoostIsCapped()
        {
            var img = Fill(1, 1, new Rgba(110, 110, 110));
            var got = ColorMatch.Transfer(img, Stats(100f, 10f), Stats(100f, 100f)).GetPixel(0, 0);
            Check.Equal(new Rgba(125, 125, 125), got, "contrast x2.5 at most");
        }

        public static void TintShiftIsLimited()
        {
            var img = Fill(1, 1, new Rgba(100, 100, 100));
            var got = ColorMatch.Transfer(img, Stats(100f, 10f), Stats(100f, 10f, cr: 80f)).GetPixel(0, 0);
            Check.Equal(new Rgba(145, 77, 100), got, "red tint moved by 32, not 80");
        }

        public static void TintShiftCanBeTurnedDown()
        {
            var img = Fill(1, 1, new Rgba(100, 100, 100));
            var got = ColorMatch.Transfer(img, Stats(100f, 10f), Stats(100f, 10f, cr: 80f), maxTintShift: 0f).GetPixel(0, 0);
            Check.Equal(new Rgba(100, 100, 100), got, "brightness only, tint untouched");
        }

        public static void TransferClampsAndKeepsAlpha()
        {
            var img = Fill(1, 1, new Rgba(250, 250, 250, 77));
            var got = ColorMatch.Transfer(img, Stats(100f, 10f), Stats(200f, 25f)).GetPixel(0, 0);
            Check.Equal(new Rgba(255, 255, 255, 77), got, "clamped, alpha kept");
        }
    }
}
