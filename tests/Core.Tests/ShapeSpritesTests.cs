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

        public static void BezelLooksLikeTheGameHudRings()
        {
            var img = ShapeSprites.Bezel(200);
            Check.Equal((byte)0, img.GetPixel(100, 100).A, "open center");
            Check.Equal((byte)0, img.GetPixel(2, 2).A, "clear outside the circle");
            Rgba ring = img.GetPixel(100, 195);
            Check.True(ring.A == 255 && ring.R < 30, $"opaque near-black ring: {ring}");
            Rgba lightBottom = img.GetPixel(100, 11), lightTop = img.GetPixel(100, 188);
            Check.True(lightBottom.R > 150 && lightTop.R < 60, $"cream rim light along the bottom only: bottom {lightBottom}, top {lightTop}");
            Rgba shadow = img.GetPixel(100, 182);
            Check.True(shadow.A > 0 && shadow.A < 255 && shadow.R < 40, $"soft dark inner shadow: {shadow}");
        }

        public static void BurstIsAYellowStarWithADeeperCenter()
        {
            var img = ShapeSprites.Burst(64);
            Check.Equal((byte)0, img.GetPixel(2, 2).A, "clear corner");
            Rgba center = img.GetPixel(32, 32);
            Check.True(center.A == 255 && center.R > 200 && center.G > 120 && center.G < 170 && center.B < 60, $"deep yellow center: {center}");
            Rgba ray = img.GetPixel(39, 49);
            Check.True(ray.A == 255 && ray.R > 240 && ray.G > 190, $"yellow ray: {ray}");
            Check.Equal((byte)0, img.GetPixel(32, 58).A, "clear between two rays");
        }

        public static void BurstCanBeDrawnInOtherColours()
        {
            var img = ShapeSprites.Burst(64, new Rgba(150, 152, 158), new Rgba(112, 114, 120));
            Check.Equal(new Rgba(112, 114, 120), img.GetPixel(32, 32), "grey center");
            Check.Equal(new Rgba(150, 152, 158), img.GetPixel(39, 49), "grey ray");
        }

        public static void PortalIsAnOvalRingWithAFaintCenter()
        {
            var img = ShapeSprites.Portal(64);
            Check.Equal((byte)0, img.GetPixel(2, 32).A, "clear at the sides");
            Rgba center = img.GetPixel(32, 32);
            Check.True(center.A > 0 && center.A < 160, $"see-through center: {center}");
            Rgba ring = img.GetPixel(32, 57);
            Check.True(ring.A == 255 && ring.R > 200, $"white ring at the top: {ring}");
        }

        public static void TornadoIsAFunnelWideAtTheTop()
        {
            var img = ShapeSprites.Tornado(64);
            Check.Equal((byte)0, img.GetPixel(2, 2).A, "clear bottom corner");
            Rgba top = img.GetPixel(14, 54), tip = img.GetPixel(14, 10);
            Check.True(top.A == 255 && top.R > 150, $"wide light funnel at the top: {top}");
            Check.Equal((byte)0, tip.A, "narrow at the bottom");
        }

        public static void DiamondHasClearCorners()
        {
            var img = ShapeSprites.Diamond(32, 0.1f);
            Check.Equal(new Rgba(255, 255, 255, 255), img.GetPixel(16, 16), "center");
            Check.Equal((byte)0, img.GetPixel(1, 1).A, "corner alpha");
        }
    }
}
