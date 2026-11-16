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

        public static void DiamondHasClearCorners()
        {
            var img = ShapeSprites.Diamond(32, 0.1f);
            Check.Equal(new Rgba(255, 255, 255, 255), img.GetPixel(16, 16), "center");
            Check.Equal((byte)0, img.GetPixel(1, 1).A, "corner alpha");
        }
    }
}
