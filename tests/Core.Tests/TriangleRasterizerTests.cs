using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class TriangleRasterizerTests
    {
        // A 10x10 square at the origin, split along its diagonal into two triangles.
        static readonly float[] SquareX = { 0f, 10f, 10f, 0f };
        static readonly float[] SquareZ = { 0f, 0f, 10f, 10f };
        static readonly int[] SquareTris = { 0, 1, 2, 0, 2, 3 };

        public static void EmptyInputGivesNull()
        {
            Check.True(TriangleRasterizer.Rasterize(new float[0], new float[0], new int[0], 64) == null, "empty");
            Check.True(TriangleRasterizer.Rasterize(SquareX, SquareZ, new[] { 0, 1 }, 64) == null, "fewer than 3 indices");
        }

        public static void GridFitsLongestSideToMaxSize()
        {
            var g = TriangleRasterizer.Rasterize(SquareX, SquareZ, SquareTris, 64);
            Check.Equal(64, g.Width, "width");
            Check.Equal(64, g.Height, "height");
            Check.Near(6f, g.PixelsPerUnit, "pixels per unit");
            Check.Near(-2f / 6f, g.OriginX, "origin x includes padding");
            Check.Near(64f / 6f, g.WorldWidth, "world width");
        }

        public static void SquareIsFilledWithoutDiagonalSeam()
        {
            var g = TriangleRasterizer.Rasterize(SquareX, SquareZ, SquareTris, 64);
            for (int y = 2; y <= 61; y++)
                for (int x = 2; x <= 61; x++)
                    Check.True(g.IsWalkable(x, y), $"cell ({x},{y}) should be walkable");
        }

        public static void PaddingStaysEmpty()
        {
            var g = TriangleRasterizer.Rasterize(SquareX, SquareZ, SquareTris, 64);
            Check.True(!g.IsWalkable(0, 0), "bottom-left padding");
            Check.True(!g.IsWalkable(63, 63), "top-right padding");
            Check.True(!g.IsWalkable(0, 32), "left padding");
        }

        public static void NonSquareBoundsKeepAspect()
        {
            var g = TriangleRasterizer.Rasterize(new[] { 0f, 20f, 20f, 0f }, new[] { 0f, 0f, 5f, 5f }, SquareTris, 104);
            Check.Equal(104, g.Width, "width");
            Check.Equal(29, g.Height, "height");
        }

        public static void BadIndicesAreSkipped()
        {
            var g = TriangleRasterizer.Rasterize(SquareX, SquareZ, new[] { 0, 1, 99, 0, 2, 3 }, 64);
            Check.True(g.IsWalkable(10, 50), "second triangle still drawn");
            Check.True(!g.IsWalkable(50, 10), "first triangle skipped");
        }

        public static void ImageOutlinesTheEdge()
        {
            var g = new WalkableGrid(5, 5, 0f, 0f, 1f);
            for (int y = 1; y <= 3; y++)
                for (int x = 1; x <= 3; x++)
                    g.Cells[y * 5 + x] = 1;
            var fill = new Rgba(10, 20, 30);
            var line = new Rgba(200, 200, 200);
            var img = TriangleRasterizer.ToImage(g, fill, line);
            Check.Equal(fill, img.GetPixel(2, 2), "inside");
            Check.Equal(line, img.GetPixel(1, 1), "corner edge");
            Check.Equal(line, img.GetPixel(3, 2), "side edge");
            Check.Equal(Rgba.Clear, img.GetPixel(0, 0), "outside");
        }
    }
}
