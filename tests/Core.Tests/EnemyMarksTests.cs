using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class EnemyMarksTests
    {
        public static void EachRankHasItsOwnShape()
        {
            Check.Equal(EnemyShape.Circle, EnemyMarks.ShapeOf(EnemyRank.Small), "small");
            Check.Equal(EnemyShape.Triangle, EnemyMarks.ShapeOf(EnemyRank.Big), "big");
            Check.Equal(EnemyShape.Square, EnemyMarks.ShapeOf(EnemyRank.Heavy), "heavy");
            Check.Equal(EnemyShape.Star, EnemyMarks.ShapeOf(EnemyRank.MiniBoss), "mini-boss");
        }

        public static void TriangleStandsOnItsBaseWithItsTipUp()
        {
            Check.True(EnemyMarks.Inside(EnemyShape.Triangle, 0f, 0.9f, 1f, 0f), "near the tip");
            Check.True(!EnemyMarks.Inside(EnemyShape.Triangle, 0f, -0.9f, 1f, 0f), "below the base (base sits at -0.5)");
            Check.True(EnemyMarks.Inside(EnemyShape.Triangle, 0.7f, -0.45f, 1f, 0f), "base corner side");
        }

        public static void SquareIsUpright()
        {
            Check.True(EnemyMarks.Inside(EnemyShape.Square, 0.68f, 0.68f, 1f, 0f), "corner");
            Check.True(!EnemyMarks.Inside(EnemyShape.Square, 0.75f, 0f, 1f, 0f), "past the side");
        }

        public static void StarHasPointsAndNotches()
        {
            Check.True(EnemyMarks.Inside(EnemyShape.Star, 0f, 0.95f, 1f, 0f), "top point");
            // Between the top point and the next one (72 degrees on) lies a notch at radius StarInner.
            float a = (90f + 36f) * (float)System.Math.PI / 180f;
            float x = (float)System.Math.Cos(a), y = (float)System.Math.Sin(a);
            Check.True(!EnemyMarks.Inside(EnemyShape.Star, x * 0.7f, y * 0.7f, 1f, 0f), "notch is empty");
            Check.True(EnemyMarks.Inside(EnemyShape.Star, x * 0.4f, y * 0.4f, 1f, 0f), "inside the notch radius");
            Check.True(EnemyMarks.Inside(EnemyShape.Star, 0f, 0f, 1f, 0f), "centre");
        }

        public static void GrowingAddsAnOutlineRoundedAtThePoints()
        {
            Check.True(!EnemyMarks.Inside(EnemyShape.Star, 0f, 1.15f, 1f, 0f), "past the tip without outline");
            Check.True(EnemyMarks.Inside(EnemyShape.Star, 0f, 1.15f, 1f, 0.2f), "outline covers just past the tip");
            Check.True(!EnemyMarks.Inside(EnemyShape.Star, 0f, 1.3f, 1f, 0.2f), "no long spike beyond the outline");
            Check.True(EnemyMarks.Inside(EnemyShape.Circle, 1.1f, 0f, 1f, 0.2f), "circle grows too");
        }

        public static void EdgeIconIsWhiteWithADarkOutline()
        {
            var img = ShapeSprites.EnemyMark(48, EnemyShape.Star);
            Check.Equal((byte)255, img.GetPixel(24, 24).R, "white centre (tinted by the UI)");
            Check.Equal((byte)0, img.GetPixel(2, 2).A, "clear corner");
            Check.True(img.GetPixel(24, 45).R < 80 && img.GetPixel(24, 45).A > 0, "dark outline just past the top point");
        }

        public static void PillIsRoundAtTheEndsAndSolidInTheMiddle()
        {
            var pill = ShapeSprites.Pill(40, 18);
            Check.Equal((byte)255, pill.GetPixel(20, 9).A, "middle");
            Check.Equal((byte)255, pill.GetPixel(20, 0).A, "flat top and bottom edge in the middle");
            Check.Equal((byte)0, pill.GetPixel(0, 0).A, "rounded corner");
            Check.True(pill.GetPixel(0, 9).A > 0, "left end reaches the edge at mid height");
        }

        public static void FillDrawsTheShapeIntoTheBuffer()
        {
            var buf = new PixelBuffer(16, 16);
            var red = new Rgba(255, 0, 0);
            EnemyMarks.Fill(buf, EnemyShape.Square, 8f, 8f, 4f, 0f, red);
            Check.Equal(red.R, buf.GetPixel(8, 8).R, "centre is filled");
            Check.Equal((byte)0, buf.GetPixel(1, 8).A, "far left stays empty");
            EnemyMarks.Fill(buf, EnemyShape.Star, -50f, -50f, 4f, 1f, red); // off the buffer: no throw
        }
    }
}
