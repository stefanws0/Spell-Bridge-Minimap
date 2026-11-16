using System;
using System.Numerics;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>Helpers for the per-cell road coverage used by <see cref="MapPainter.PaintArt"/>.</summary>
    public static class RoadMask
    {
        /// <summary>
        /// Marks every cell whose center lies inside a rectangle on the ground (a decal's footprint) as fully
        /// covered. The mask is <paramref name="width"/> x <paramref name="height"/> cells starting at world
        /// (originX, originZ). <paramref name="right"/> and <paramref name="up"/> are the rectangle's axes on the
        /// ground (X, Z); <paramref name="halfWidth"/> and <paramref name="halfHeight"/> are its half extents along them.
        /// </summary>
        public static void AddRectangle(float[] mask, int width, int height, float originX, float originZ, float cellSize,
            Vector2 center, Vector2 right, Vector2 up, float halfWidth, float halfHeight)
        {
            float rl = right.LengthSquared(), ul = up.LengthSquared();
            if (rl < 1e-6f || ul < 1e-6f) return;
            float reach = (float)Math.Sqrt(halfWidth * halfWidth * rl + halfHeight * halfHeight * ul);
            int x0 = Math.Max(0, (int)Math.Floor((center.X - reach - originX) / cellSize));
            int x1 = Math.Min(width - 1, (int)Math.Ceiling((center.X + reach - originX) / cellSize));
            int y0 = Math.Max(0, (int)Math.Floor((center.Y - reach - originZ) / cellSize));
            int y1 = Math.Min(height - 1, (int)Math.Ceiling((center.Y + reach - originZ) / cellSize));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var d = new Vector2(originX + (x + 0.5f) * cellSize, originZ + (y + 0.5f) * cellSize) - center;
                    float u = Vector2.Dot(d, right) / rl, v = Vector2.Dot(d, up) / ul;
                    if (Math.Abs(u) <= halfWidth && Math.Abs(v) <= halfHeight) mask[y * width + x] = 1f;
                }
        }
    }
}
