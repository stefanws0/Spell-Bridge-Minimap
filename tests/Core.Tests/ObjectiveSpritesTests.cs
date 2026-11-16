using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ObjectiveSpritesTests
    {
        public static void MushroomHasARedCapWithWhiteSpotsAndACreamStem()
        {
            var img = ShapeSprites.Mushroom(64);
            Check.Equal((byte)0, img.GetPixel(2, 2).A, "clear corner");
            Rgba cap = img.GetPixel(40, 44), spot = img.GetPixel(21, 40), stem = img.GetPixel(32, 16);
            Check.True(cap.R > 180 && cap.G < 90, $"red cap: {cap}");
            Check.True(spot.R > 230 && spot.G > 230, $"white spot: {spot}");
            Check.True(stem.R > 220 && stem.G > 210 && stem.B > 180, $"cream stem: {stem}");
        }

        public static void CrateIsAWoodenBoxWithACross()
        {
            var img = ShapeSprites.Crate(64);
            Check.Equal((byte)0, img.GetPixel(1, 1).A, "clear corner");
            Rgba wood = img.GetPixel(32, 20), plank = img.GetPixel(32, 32);
            Check.True(wood.R > 160 && wood.G > 110 && wood.B < 100, $"wood: {wood}");
            Check.True(plank.R < 140 && plank.R > 90, $"darker cross plank in the middle: {plank}");
        }

        public static void PawHasAPadAndFourToes()
        {
            var img = ShapeSprites.Paw(64);
            Check.True(img.GetPixel(32, 23).R > 220, "pad");
            Check.True(img.GetPixel(17, 38).R > 220 && img.GetPixel(46, 38).R > 220, "outer toes");
            Check.Equal((byte)0, img.GetPixel(32, 60).A, "gap above the middle toes");
        }

        public static void StoneIsGreyWithACrack()
        {
            var img = ShapeSprites.Stone(64);
            Check.Equal((byte)0, img.GetPixel(1, 1).A, "clear corner");
            Rgba grey = img.GetPixel(42, 32);
            Check.True(grey.R > 120 && grey.R < 190 && System.Math.Abs(grey.R - grey.B) < 20, $"grey stone: {grey}");
            Check.True(img.GetPixel(33, 32).R < 60, "dark crack through the middle");
        }

        public static void CrosshairIsARedRingWithTicksAndAnOpenMiddle()
        {
            var img = ShapeSprites.Crosshair(64);
            Rgba ring = img.GetPixel(32 + 20, 32), tick = img.GetPixel(32, 61);
            Check.True(ring.R > 220 && ring.G < 110, $"red ring: {ring}");
            Check.True(tick.A > 0 && tick.R > 200, $"tick at the top: {tick}");
            Check.Equal((byte)0, img.GetPixel(32, 32).A, "open middle, so the enemy dot shows");
            Check.Equal((byte)0, img.GetPixel(4, 4).A, "clear corner");
        }

        public static void PixelFontWritesCountsOnAPill()
        {
            var fg = new Rgba(253, 248, 206);
            var bg = new Rgba(24, 24, 26, 235);
            var img = PixelFont.Render("3/8", scale: 2, fg, bg, padX: 2, padY: 1, padLeft: 0);
            // 3 glyphs of 3 blocks + 2 spaces = 11 blocks wide, plus 2 x 2 padding = 15 blocks; 5 + 2 x 1 = 7 high.
            Check.Equal(30, img.Width, "width");
            Check.Equal(14, img.Height, "height");
            Check.Equal(fg, img.GetPixel(2 * 2, (1 + 4) * 2), "top-left block of the 3");
            Check.Equal(bg, img.GetPixel(2 * 3, (1 + 3) * 2), "the 3's open middle-left");
            Check.Equal((byte)0, img.GetPixel(0, 0).A, "rounded pill corner");
        }

        public static void PixelFontSkipsUnknownCharacters()
        {
            var img = PixelFont.Render("1?2", 1, new Rgba(255, 255, 255), new Rgba(0, 0, 0, 255), 0, 0, 0);
            Check.Equal(7, img.Width, "only the 1 and the 2");
        }
    }
}
