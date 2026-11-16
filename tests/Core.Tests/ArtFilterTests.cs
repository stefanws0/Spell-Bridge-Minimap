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

        public static void OverlayUsesTheTopPictureWhereItHasPixels()
        {
            var under = Fill(2, new Rgba(10, 20, 30));
            var over = new PixelBuffer(2, 2);
            over.SetPixel(1, 1, new Rgba(200, 190, 180));
            var got = ArtFilter.Overlay(under, over);
            Check.Equal(new Rgba(200, 190, 180), got.GetPixel(1, 1), "top picture where drawn");
            Check.Equal(new Rgba(10, 20, 30), got.GetPixel(0, 0), "bottom picture elsewhere");
        }

        static float Luma(Rgba c) => c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;

        /// <summary>Mostly light ground with a few near-black patches (shadows, crevices).</summary>
        static PixelBuffer GroundWithDarkPatches()
        {
            var img = Fill(20, new Rgba(150, 140, 120));
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 4; x++)
                    img.SetPixel(x, y, new Rgba(20, 18, 15));
            return img;
        }

        public static void LiftShadowsBrightensDarkGroundTowardTheLightGround()
        {
            var got = ArtFilter.LiftShadows(GroundWithDarkPatches(), (x, y) => true, 0.8f, 0.3f);
            Rgba dark = got.GetPixel(1, 5);
            Check.True(Luma(dark) > 75f, $"near-black patch lifted: {dark}");
            Check.True(dark.R >= dark.G && dark.G >= dark.B, $"takes the ground's warm colour: {dark}");
            Check.Equal(new Rgba(150, 140, 120), got.GetPixel(10, 10), "light ground unchanged");
        }

        public static void LiftShadowsLeavesExcludedPixelsAlone()
        {
            var img = GroundWithDarkPatches();
            img.SetPixel(2, 2, new Rgba(16, 16, 18));
            var got = ArtFilter.LiftShadows(img, (x, y) => !(x == 2 && y == 2), 0.8f, 0.3f);
            Check.Equal(new Rgba(16, 16, 18), got.GetPixel(2, 2), "obstacle pixel stays black");
        }

        public static void LiftShadowsLeavesEvenGroundAlone()
        {
            var flat = Fill(12, new Rgba(90, 130, 60));
            var got = ArtFilter.LiftShadows(flat, (x, y) => true, 0.8f, 0.3f);
            Check.Equal(new Rgba(90, 130, 60), got.GetPixel(6, 6), "even ground");
        }

        public static void TintColoursCoveredPixels()
        {
            var img = Fill(5, new Rgba(120, 130, 150));
            var cover = new float[25];
            cover[2 * 5 + 2] = 1f;
            var got = ArtFilter.Tint(img, cover, 0.3f, new Rgba(200, 50, 60), 0.9f, null);
            Rgba c = got.GetPixel(2, 2);
            Check.True(c.R > 150 && c.G < 90 && c.B < 100, $"covered pixel turns red: {c}");
            Check.Equal(new Rgba(120, 130, 150), got.GetPixel(0, 0), "uncovered pixel unchanged");
        }

        public static void TintDrawsTheEdgeColourAlongTheCoveredArea()
        {
            var img = Fill(7, new Rgba(60, 60, 90));
            var cover = new float[49];
            for (int y = 1; y <= 5; y++)
                for (int x = 1; x <= 5; x++)
                    cover[y * 7 + x] = 1f;
            var bank = new Rgba(220, 210, 240);
            var got = ArtFilter.Tint(img, cover, 0.3f, new Rgba(130, 120, 190), 0.6f, bank);
            Check.Equal(bank, got.GetPixel(1, 3), "left bank");
            Check.Equal(bank, got.GetPixel(3, 5), "top bank");
            Check.True(!got.GetPixel(3, 3).Equals(bank) && got.GetPixel(3, 3).B > 100, $"water inside: {got.GetPixel(3, 3)}");
            Check.Equal(new Rgba(60, 60, 90), got.GetPixel(0, 0), "outside unchanged");
        }

        /// <summary>
        /// A 9x9 map: a visible river along row 4, and a hidden stretch of the same line (underground) down
        /// column 4 that only the river-alone render shows.
        /// </summary>
        public static void VisibleOnlyDropsRiverPartsThePictureDoesNotShow()
        {
            const int n = 9;
            var cover = new float[n * n];
            var with = Fill(n, new Rgba(70, 70, 120));
            var without = Fill(n, new Rgba(70, 70, 120));
            for (int i = 0; i < n; i++)
            {
                cover[4 * n + i] = 1f;              // the river
                with.SetPixel(i, 4, new Rgba(120, 115, 190));
                cover[i * n + 4] = 1f;              // the hidden stretch
            }
            var got = ArtFilter.VisibleOnly(cover, with, without, 0.3f, 4);
            Check.True(got[4 * n + 2] >= 0.3f, "visible river kept");
            Check.True(got[4 * n + 4] >= 0.3f, "crossing kept");
            Check.True(got[2 * n + 4] < 0.3f, "hidden stretch dropped");
            Check.True(got[5 * n + 4] < 0.3f, "hidden stretch right next to the river dropped");
        }

        public static void VisibleOnlyFillsSinglePixelGapsInTheRiver()
        {
            const int n = 7;
            var cover = new float[n * n];
            var with = Fill(n, new Rgba(70, 70, 120));
            var without = Fill(n, new Rgba(70, 70, 120));
            for (int y = 2; y <= 4; y++)
                for (int x = 0; x < n; x++)
                {
                    cover[y * n + x] = 1f;
                    if (!(x == 3 && y == 3)) with.SetPixel(x, y, new Rgba(120, 115, 190));
                }
            var got = ArtFilter.VisibleOnly(cover, with, without, 0.3f, 4);
            Check.True(got[3 * n + 3] >= 0.3f, "water that happens to match the ground is still river");
        }

        public static void KeepsAlpha()
        {
            var got = ArtFilter.CalmGrass(Fill(5, new Rgba(90, 130, 60, 77)), 1, 0.8f, 0.7f).GetPixel(2, 2);
            Check.Equal((byte)77, got.A, "alpha");
        }
    }
}
