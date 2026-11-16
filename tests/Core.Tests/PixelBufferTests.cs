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

        public static void ClearResetsPixels()
        {
            var b = new PixelBuffer(3, 3);
            b.SetPixel(1, 1, Red);
            b.Clear();
            Check.Equal(Rgba.Clear, b.GetPixel(1, 1), "(1,1)");
        }
    }
}
