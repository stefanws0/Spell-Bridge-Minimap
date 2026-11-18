using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class IconMaskTests
    {
        static PixelBuffer GreenMushroom()
        {
            var img = new PixelBuffer(4, 1);
            img.SetPixel(0, 0, new Rgba(90, 200, 60));        // bright green cap
            img.SetPixel(1, 0, new Rgba(20, 40, 15));         // dark outline
            img.SetPixel(2, 0, new Rgba(90, 200, 60, 128));   // soft edge
            return img;                                       // pixel 3 stays clear
        }

        public static void BrightestPartBecomesWhite()
        {
            var w = IconMask.ToWhite(GreenMushroom());
            var c = w.GetPixel(0, 0);
            Check.Equal((byte)255, c.R, "red");
            Check.Equal((byte)255, c.G, "green");
            Check.Equal((byte)255, c.B, "blue");
        }

        public static void DarkLinesStayDarkButNotBlack()
        {
            var c = IconMask.ToWhite(GreenMushroom(), 0.3f, 1f).GetPixel(1, 0);
            Check.True(c.R == c.G && c.G == c.B, "grey");
            Check.True(c.R > 76 && c.R < 120, "between the floor (0.3) and mid grey, was " + c.R);
        }

        public static void DefaultContrastMakesDarkLinesClearlyDark()
        {
            var w = IconMask.ToWhite(GreenMushroom());
            Check.Equal((byte)255, w.GetPixel(0, 0).R, "bright part stays white");
            byte line = w.GetPixel(1, 0).R;
            Check.True(line > 30 && line < 70, "dark line well below mid grey but not black, was " + line);
        }

        static PixelBuffer WhiteSquare()
        {
            var img = new PixelBuffer(10, 10);
            for (int y = 3; y < 7; y++)
                for (int x = 3; x < 7; x++) img.SetPixel(x, y, new Rgba(255, 255, 255));
            return img;
        }

        public static void OutlinePadsAndRingsTheShape()
        {
            var o = IconMask.Outline(WhiteSquare(), 2, new Rgba(20, 20, 20));
            Check.Equal(16, o.Width, "padded by width + 1 on each side");
            Check.Equal(16, o.Height, "padded height");
            Check.Equal((byte)255, o.GetPixel(7, 7).R, "the icon itself stays white");
            Check.Equal((byte)255, o.GetPixel(7, 7).A, "and opaque");
            var edge = o.GetPixel(5, 7);                                   // 1 px left of the square
            Check.Equal((byte)20, edge.R, "dark ring beside the shape");
            Check.Equal((byte)255, edge.A, "solid ring");
            Check.Equal((byte)0, o.GetPixel(1, 7).A, "clear further out");
            Check.Equal((byte)0, o.GetPixel(0, 0).A, "clear corner");
        }

        public static void OutlineKeepsSoftIconEdgesOverTheRing()
        {
            var img = new PixelBuffer(5, 1);
            img.SetPixel(2, 0, new Rgba(255, 255, 255));
            img.SetPixel(3, 0, new Rgba(255, 255, 255, 128));           // half see-through: the ring shows under it
            var o = IconMask.Outline(img, 1, new Rgba(0, 0, 0));
            var soft = o.GetPixel(3 + 2, 2);
            Check.Equal((byte)255, soft.A, "ring fills in under the soft edge");
            Check.True(soft.R > 100 && soft.R < 160, "half white over black, was " + soft.R);
        }

        public static void AlphaIsKept()
        {
            var w = IconMask.ToWhite(GreenMushroom());
            Check.Equal((byte)128, w.GetPixel(2, 0).A, "soft edge");
            Check.Equal((byte)0, w.GetPixel(3, 0).A, "clear stays clear");
        }

        static PixelBuffer RingWithAHole()
        {
            // A 9 x 9 green square with a 3 x 3 see-through hole in the middle.
            var img = new PixelBuffer(11, 11);
            for (int y = 1; y < 10; y++)
                for (int x = 1; x < 10; x++)
                    if (!(x >= 4 && x <= 6 && y >= 4 && y <= 6)) img.SetPixel(x, y, new Rgba(90, 200, 60));
            return img;
        }

        public static void FillHolesFillsOnlyEnclosedPixels()
        {
            var img = RingWithAHole();
            int n = IconMask.FillHoles(img, new Rgba(0, 0, 255));
            Check.Equal(9, n, "the 3 x 3 hole");
            Check.Equal((byte)255, img.GetPixel(5, 5).B, "hole filled blue");
            Check.Equal((byte)0, img.GetPixel(0, 0).A, "outside stays clear");
        }

        public static void TintMultiplies()
        {
            var w = new PixelBuffer(1, 1);
            w.SetPixel(0, 0, new Rgba(255, 128, 255, 200));
            var c = IconMask.Tint(w, new Rgba(100, 200, 50)).GetPixel(0, 0);
            Check.Equal((byte)100, c.R, "red");
            Check.Equal((byte)100, c.G, "green halved");
            Check.Equal((byte)50, c.B, "blue");
            Check.Equal((byte)200, c.A, "alpha kept");
        }

        public static void StatueIsGreyWithColouredHoleAndEyes()
        {
            var stone = new Rgba(168, 172, 182);
            var blue = new Rgba(110, 180, 255);
            var s = IconMask.Statue(RingWithAHole(), stone, blue);
            var body = s.GetPixel(1, 1);
            Check.True(body.R > 140 && body.B > body.R && body.B - body.R < 30, $"grey body, was {body}");
            Check.Equal(blue, s.GetPixel(5, 5), "hole filled with the detail colour");
            // Box 1..9: eyes at x 1 + 0.36 * 9 = 4.2 and 7.8, y 1 + 0.74 * 9 = 7.7.
            Check.Equal(blue, s.GetPixel(4, 7), "left eye");
            Check.Equal(blue, s.GetPixel(7, 7), "right eye");
        }

        public static void AverageColourIgnoresSeeThroughPixels()
        {
            var img = new PixelBuffer(2, 1);
            img.SetPixel(0, 0, new Rgba(100, 200, 50));
            img.SetPixel(1, 0, new Rgba(255, 0, 0, 10));
            var c = IconMask.AverageColour(img);
            Check.Equal((byte)100, c.R, "red");
            Check.Equal((byte)200, c.G, "green");
        }
    }
}
