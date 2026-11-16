using System;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ArtFilterTests
    {
        static PixelBuffer Fill(int size, Rgba c)
        {
            var img = new PixelBuffer(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    img.SetPixel(x, y, c);
            return img;
        }

        public static void FlatColoursStayTheSame()
        {
            foreach (var c in new[] { new Rgba(90, 130, 60), new Rgba(180, 165, 110) })
            {
                var got = ArtFilter.CalmGrass(Fill(9, c), 2, 0.8f, 0.7f).GetPixel(4, 4);
                Check.Equal(c, got, $"flat {c}");
            }
        }

        public static void CalmsBlotchyGrass()
        {
            var img = new PixelBuffer(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    img.SetPixel(x, y, (x + y) % 2 == 0 ? new Rgba(80, 130, 50) : new Rgba(110, 160, 70));
            var got = ArtFilter.CalmGrass(img, 2, 1f, 0.7f);
            int diff = Math.Abs(got.GetPixel(7, 7).G - got.GetPixel(8, 7).G);
            Check.True(diff < 6, $"neighbouring grass pixels now differ by {diff} (was 30)");
        }

        public static void SharpensCobblestoneAndStone()
        {
            var img = Fill(17, new Rgba(180, 165, 110));
            img.SetPixel(8, 8, new Rgba(120, 110, 80));
            var got = ArtFilter.CalmGrass(img, 2, 0.8f, 1f);
            Check.True(got.GetPixel(8, 8).R < 100, $"the dark stone gets darker: {got.GetPixel(8, 8)}");
            Check.Equal(new Rgba(180, 165, 110), got.GetPixel(0, 0), "plain cobblestone away from it is unchanged");
        }

        public static void KeepsAlpha()
        {
            var got = ArtFilter.CalmGrass(Fill(5, new Rgba(90, 130, 60, 77)), 1, 0.8f, 0.7f).GetPixel(2, 2);
            Check.Equal((byte)77, got.A, "alpha");
        }
    }
}
