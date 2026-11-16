using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class PixelBufferTests
    {
        static readonly Rgba Red = new Rgba(255, 0, 0);

        public static void NewBufferIsTransparent()
        {
            var b = new PixelBuffer(4, 3);
            Check.Equal(4 * 3 * 4, b.Data.Length, "data length");
            Check.Equal(Rgba.Clear, b.GetPixel(3, 2), "corner pixel");
        }

        public static void SetPixelIgnoresOutOfRange()
        {
            var b = new PixelBuffer(2, 2);
            b.SetPixel(-1, 0, Red);
            b.SetPixel(2, 0, Red);
            b.SetPixel(0, 5, Red);
            foreach (var v in b.Data) Check.Equal((byte)0, v, "byte");
        }

        public static void FillCircleCoversPixelCentersInsideRadius()
        {
            var b = new PixelBuffer(10, 10);
            b.FillCircle(5f, 5f, 1.5f, Red);
            Check.Equal(Red, b.GetPixel(4, 4), "(4,4)");
            Check.Equal(Red, b.GetPixel(5, 5), "(5,5)");
            Check.Equal(Red, b.GetPixel(4, 5), "(4,5)");
            Check.Equal(Red, b.GetPixel(5, 4), "(5,4)");
            Check.Equal(Rgba.Clear, b.GetPixel(6, 5), "(6,5) is 1.58 away");
            Check.Equal(Rgba.Clear, b.GetPixel(0, 0), "(0,0)");
        }

        public static void FillCircleClipsAtEdges()
        {
            var b = new PixelBuffer(4, 4);
            b.FillCircle(0f, 0f, 3f, Red);
            Check.Equal(Red, b.GetPixel(0, 0), "(0,0)");
            Check.Equal(Rgba.Clear, b.GetPixel(3, 3), "(3,3)");
        }

        static void Put(byte[] rgba, int width, int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * width + x) * 4;
            rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = a;
        }

        public static void DownsampleAveragesBlocksIntoOpaquePixels()
        {
            // 4x2 source, factor 2: the left block is two red + two blue pixels, the right block is white.
            var src = new byte[4 * 2 * 4];
            Put(src, 4, 0, 0, 255, 0, 0, 0);
            Put(src, 4, 1, 0, 0, 0, 255, 0);
            Put(src, 4, 0, 1, 255, 0, 0, 0);
            Put(src, 4, 1, 1, 0, 0, 255, 0);
            for (int y = 0; y < 2; y++)
                for (int x = 2; x < 4; x++)
                    Put(src, 4, x, y, 255, 255, 255, 255);

            var dst = PixelBuffer.Downsample(src, 4, 2, 2);
            Check.Equal(2, dst.Width, "width");
            Check.Equal(1, dst.Height, "height");
            Check.Equal(new Rgba(127, 0, 127, 255), dst.GetPixel(0, 0), "red and blue average, alpha forced opaque");
            Check.Equal(new Rgba(255, 255, 255, 255), dst.GetPixel(1, 0), "white");
        }

        public static void DownsampleDropsPartialBlocks()
        {
            var dst = PixelBuffer.Downsample(new byte[5 * 5 * 4], 5, 5, 2);
            Check.Equal(2, dst.Width, "width");
            Check.Equal(2, dst.Height, "height");
        }

        public static void CoverageCountsPixelsThatDifferFromTheKey()
        {
            // 4x2 source, factor 2: left block has one of four pixels drawn, right block all four.
            var key = new Rgba(255, 0, 255);
            var src = new byte[4 * 2 * 4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 4; x++)
                    Put(src, 4, x, y, 255, 0, 255, 255);
            Put(src, 4, 1, 1, 90, 90, 90, 255);
            for (int y = 0; y < 2; y++) { Put(src, 4, 2, y, 10, 10, 10, 255); Put(src, 4, 3, y, 250, 5, 250, 255); }
            var cover = PixelBuffer.CoverageOfNonKey(src, 4, 2, 2, key, 24);
            Check.Equal(2, cover.Length, "one value per block");
            Check.Near(0.25f, cover[0], "a quarter drawn");
            Check.Near(0.5f, cover[1], "near-key pixels don't count");
        }

        public static void DownsampleIgnoringKeyAveragesOnlyDrawnPixels()
        {
            // 4x2 source, factor 2: left block = one grey pixel + three key pixels; right block all key.
            var key = new Rgba(255, 0, 255);
            var src = new byte[4 * 2 * 4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 4; x++)
                    Put(src, 4, x, y, 255, 0, 255, 255);
            Put(src, 4, 0, 0, 100, 120, 140, 255);
            var dst = PixelBuffer.DownsampleIgnoringKey(src, 4, 2, 2, key, 24);
            Check.Equal(new Rgba(100, 120, 140, 255), dst.GetPixel(0, 0), "only the drawn pixel counts");
            Check.Equal(Rgba.Clear, dst.GetPixel(1, 0), "nothing drawn: transparent");
        }

        public static void ClearResetsPixels()
        {
            var b = new PixelBuffer(3, 3);
            b.SetPixel(1, 1, Red);
            b.Clear();
            Check.Equal(Rgba.Clear, b.GetPixel(1, 1), "(1,1)");
        }
    }
}
