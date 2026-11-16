using System;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// A top-down grid of walkable cells. Cell (0, 0) is the bottom-left; its lower-left
    /// corner sits at world (OriginX, OriginZ).
    /// </summary>
    public sealed class WalkableGrid
    {
        public readonly int Width, Height;
        public readonly float OriginX, OriginZ, PixelsPerUnit;
        /// <summary>Row-major, bottom row first. 1 = walkable.</summary>
        public readonly byte[] Cells;

        public WalkableGrid(int width, int height, float originX, float originZ, float pixelsPerUnit)
        {
            Width = width;
            Height = height;
            OriginX = originX;
            OriginZ = originZ;
            PixelsPerUnit = pixelsPerUnit;
            Cells = new byte[width * height];
        }

        public bool IsWalkable(int x, int y)
            => (uint)x < (uint)Width && (uint)y < (uint)Height && Cells[y * Width + x] != 0;

        public float WorldWidth => Width / PixelsPerUnit;
        public float WorldHeight => Height / PixelsPerUnit;
    }

    public static class TriangleRasterizer
    {
        /// <summary>Empty cells kept around the walkable area on every side.</summary>
        public const int Padding = 2;

        /// <summary>
        /// Fills the triangles (vertex X/Z arrays + index triples) into a grid whose longest side is
        /// <paramref name="maxSize"/> cells including padding. Returns null for empty input.
        /// Triangles with out-of-range indices are skipped.
        /// </summary>
        public static WalkableGrid Rasterize(float[] xs, float[] zs, int[] indices, int maxSize)
        {
            if (xs == null || zs == null || indices == null) return null;
            if (xs.Length == 0 || xs.Length != zs.Length || indices.Length < 3) return null;
            if (maxSize <= 2 * Padding + 1) throw new ArgumentOutOfRangeException(nameof(maxSize));

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minX) minX = xs[i];
                if (xs[i] > maxX) maxX = xs[i];
                if (zs[i] < minZ) minZ = zs[i];
                if (zs[i] > maxZ) maxZ = zs[i];
            }

            float span = Math.Max(Math.Max(maxX - minX, maxZ - minZ), 0.001f);
            float ppu = (maxSize - 2 * Padding) / span;
            int w = Math.Min(maxSize, (int)Math.Ceiling((maxX - minX) * ppu) + 2 * Padding);
            int h = Math.Min(maxSize, (int)Math.Ceiling((maxZ - minZ) * ppu) + 2 * Padding);
            var grid = new WalkableGrid(w, h, minX - Padding / ppu, minZ - Padding / ppu, ppu);

            for (int t = 0; t + 2 < indices.Length; t += 3)
            {
                int ia = indices[t], ib = indices[t + 1], ic = indices[t + 2];
                if ((uint)ia >= (uint)xs.Length || (uint)ib >= (uint)xs.Length || (uint)ic >= (uint)xs.Length) continue;
                FillTriangle(grid,
                    (xs[ia] - grid.OriginX) * ppu, (zs[ia] - grid.OriginZ) * ppu,
                    (xs[ib] - grid.OriginX) * ppu, (zs[ib] - grid.OriginZ) * ppu,
                    (xs[ic] - grid.OriginX) * ppu, (zs[ic] - grid.OriginZ) * ppu);
            }
            return grid;
        }

        /// <summary>Paints walkable cells: <paramref name="outline"/> where a 4-neighbour isn't walkable, else <paramref name="fill"/>.</summary>
        public static PixelBuffer ToImage(WalkableGrid g, Rgba fill, Rgba outline)
        {
            var img = new PixelBuffer(g.Width, g.Height);
            for (int y = 0; y < g.Height; y++)
                for (int x = 0; x < g.Width; x++)
                {
                    if (!g.IsWalkable(x, y)) continue;
                    bool edge = !g.IsWalkable(x - 1, y) || !g.IsWalkable(x + 1, y)
                             || !g.IsWalkable(x, y - 1) || !g.IsWalkable(x, y + 1);
                    img.SetPixel(x, y, edge ? outline : fill);
                }
            return img;
        }

        /// <summary>Marks every cell whose center is inside the triangle grown by half a cell. Coordinates are in cells.</summary>
        static void FillTriangle(WalkableGrid g, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-6f)
            {
                Mark(g, ax, ay);
                Mark(g, bx, by);
                Mark(g, cx, cy);
                return;
            }
            if (area < 0f)
            {
                (bx, cx) = (cx, bx);
                (by, cy) = (cy, by);
            }

            float lab = Length(bx - ax, by - ay), lbc = Length(cx - bx, cy - by), lca = Length(ax - cx, ay - cy);
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx)) - 1f));
            int x1 = Math.Min(g.Width - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx)) + 1f));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy)) - 1f));
            int y1 = Math.Min(g.Height - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy)) + 1f));

            for (int y = y0; y <= y1; y++)
            {
                float py = y + 0.5f;
                for (int x = x0; x <= x1; x++)
                {
                    float px = x + 0.5f;
                    float e0 = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) / lab;
                    float e1 = ((cx - bx) * (py - by) - (cy - by) * (px - bx)) / lbc;
                    float e2 = ((ax - cx) * (py - cy) - (ay - cy) * (px - cx)) / lca;
                    if (e0 >= -0.5f && e1 >= -0.5f && e2 >= -0.5f) g.Cells[y * g.Width + x] = 1;
                }
            }
        }

        static void Mark(WalkableGrid g, float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            if ((uint)ix < (uint)g.Width && (uint)iy < (uint)g.Height) g.Cells[iy * g.Width + ix] = 1;
        }

        static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);
    }
}
