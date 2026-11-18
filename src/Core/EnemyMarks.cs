using System;

namespace SpellBrigadeMinimap.Core
{
    public enum EnemyShape { Circle, Triangle, Square, Star }

    /// <summary>
    /// The shape of each enemy rank on the minimap, so ranks differ by outline as well as colour: the swarm a
    /// circle, big enemies a triangle, heavy ones a square, mini-bosses a five-pointed star. Shapes stand upright
    /// and are measured by their circumradius <c>r</c>. An outline is the shape grown by <c>grow</c>, rounded at
    /// the corners so the star's points don't turn into long spikes.
    /// </summary>
    public static class EnemyMarks
    {
        /// <summary>How deep the star's notches are, as a share of its radius (a classic star is 0.38).</summary>
        public const float StarInner = 0.46f;

        static readonly float[] TriangleUnit = Polygon(3, 1f, 1f);
        static readonly float[] SquareUnit = Square();
        static readonly float[] StarUnit = Polygon(10, 1f, StarInner);

        public static EnemyShape ShapeOf(EnemyRank rank)
        {
            switch (rank)
            {
                case EnemyRank.Small: return EnemyShape.Circle;
                case EnemyRank.Big: return EnemyShape.Triangle;
                case EnemyRank.Heavy: return EnemyShape.Square;
                default: return EnemyShape.Star;
            }
        }

        /// <summary>
        /// True if the point (<paramref name="dx"/>, <paramref name="dy"/>) from the centre is inside the shape of
        /// radius <paramref name="r"/> grown by <paramref name="grow"/>.
        /// </summary>
        public static bool Inside(EnemyShape shape, float dx, float dy, float r, float grow)
        {
            if (shape == EnemyShape.Circle)
            {
                float rr = r + grow;
                return dx * dx + dy * dy <= rr * rr;
            }
            float[] unit = shape == EnemyShape.Triangle ? TriangleUnit : shape == EnemyShape.Square ? SquareUnit : StarUnit;
            // In unit space: the point, and the grow scaled the same way.
            float ux = dx / r, uy = dy / r, g = grow / r;
            if (InPolygon(unit, ux, uy)) return true;
            return g > 0f && DistanceToEdges(unit, ux, uy) <= g;
        }

        /// <summary>Sets every pixel whose centre is inside the shape (grown by <paramref name="grow"/>) at (cx, cy).</summary>
        public static void Fill(PixelBuffer buf, EnemyShape shape, float cx, float cy, float r, float grow, Rgba c)
        {
            float reach = r + grow;
            int x0 = (int)Math.Floor(cx - reach), x1 = (int)Math.Ceiling(cx + reach);
            int y0 = (int)Math.Floor(cy - reach), y1 = (int)Math.Ceiling(cy + reach);
            for (int y = y0; y <= y1; y++)
            {
                if ((uint)y >= (uint)buf.Height) continue;
                float dy = y + 0.5f - cy;
                for (int x = x0; x <= x1; x++)
                {
                    if ((uint)x >= (uint)buf.Width) continue;
                    if (Inside(shape, x + 0.5f - cx, dy, r, grow)) buf.SetPixel(x, y, c);
                }
            }
        }

        /// <summary>Vertices (x0, y0, x1, y1, ...) of a regular polygon or star, first point straight up.</summary>
        static float[] Polygon(int corners, float outer, float inner)
        {
            var v = new float[corners * 2];
            for (int i = 0; i < corners; i++)
            {
                double a = Math.PI / 2 + i * 2 * Math.PI / corners;
                float rad = i % 2 == 0 ? outer : inner;
                v[2 * i] = (float)(Math.Cos(a) * rad);
                v[2 * i + 1] = (float)(Math.Sin(a) * rad);
            }
            return v;
        }

        static float[] Square()
        {
            float h = (float)(1 / Math.Sqrt(2));
            return new[] { -h, -h, h, -h, h, h, -h, h };
        }

        /// <summary>Even-odd point-in-polygon test (works for the star too).</summary>
        static bool InPolygon(float[] v, float x, float y)
        {
            bool inside = false;
            int n = v.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = v[2 * i], yi = v[2 * i + 1], xj = v[2 * j], yj = v[2 * j + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        static float DistanceToEdges(float[] v, float x, float y)
        {
            float best = float.MaxValue;
            int n = v.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ax = v[2 * j], ay = v[2 * j + 1], bx = v[2 * i], by = v[2 * i + 1];
                float ex = bx - ax, ey = by - ay;
                float t = Math.Max(0f, Math.Min(1f, ((x - ax) * ex + (y - ay) * ey) / (ex * ex + ey * ey)));
                float px = ax + ex * t - x, py = ay + ey * t - y;
                best = Math.Min(best, px * px + py * py);
            }
            return (float)Math.Sqrt(best);
        }
    }
}
