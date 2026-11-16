using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ShapeSpritesTests
    {
        public static void DiscIsWhiteInsideDarkBorderClearOutside()
        {
            var img = ShapeSprites.Disc(32, 0.12f);
            Check.Equal(new Rgba(255, 255, 255, 255), img.GetPixel(16, 16), "center");
            Check.Equal(new Rgba(20, 20, 20, 255), img.GetPixel(16, 1), "border");
            Check.Equal((byte)0, img.GetPixel(0, 0).A, "corner alpha");
        }

        public static void RingIsHollow()
        {
            var img = ShapeSprites.Ring(64, 0.05f);
            Check.Equal((byte)0, img.GetPixel(32, 32).A, "center alpha");
            Check.Equal((byte)255, img.GetPixel(32, 1).A, "rim alpha");
        }

        public static void ArrowPointsUp()
        {
            var img = ShapeSprites.Arrow(32);
            Check.Equal(new Rgba(255, 255, 255, 255), img.GetPixel(16, 16), "body");
            Check.True(img.GetPixel(16, 29).A > 0, "tip near the top");
            Check.Equal((byte)0, img.GetPixel(16, 2).A, "notch at the bottom");
        }

        public static void BezelIsAShadedRingWithAnOpenCenter()
        {
            var img = ShapeSprites.Bezel(200);
            Check.Equal((byte)0, img.GetPixel(100, 100).A, "open center");
            Check.Equal((byte)0, img.GetPixel(2, 2).A, "clear outside the circle");
            Rgba top = img.GetPixel(100, 193), bottom = img.GetPixel(100, 6);
            Check.Equal((byte)255, top.A, "band is opaque");
            Check.True(top.R > bottom.R && top.G > bottom.G, $"lit from above: top {top}, bottom {bottom}");
            Rgba shadow = img.GetPixel(100, 178);
            Check.True(shadow.A > 0 && shadow.A < 255 && shadow.R < 40, $"soft dark inner shadow: {shadow}");
        }

        public static void DiamondHasClearCorners()
        {
            var img = ShapeSprites.Diamond(32, 0.1f);
            Check.Equal(new Rgba(255, 255, 255, 255), img.GetPixel(16, 16), "center");
            Check.Equal((byte)0, img.GetPixel(1, 1).A, "corner alpha");
        }
    }
}
