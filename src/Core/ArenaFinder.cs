using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// Separates the arena from NavMesh that reaches past its walls. In The Spell Brigade the NavMesh extends
    /// beyond the arena's rock wall (enemies come in from out there), so the flood leaks through gaps in the
    /// wall. This grows every non-walkable cell by <c>radius</c>, which seals gaps up to 2 x radius wide, then
    /// floods inward from just inside the NavMesh's outer edge and grows the result back by the same radius.
    /// What that reaches is the band beyond the walls. Pockets inside the arena that only connect through
    /// narrow openings (a ruin, a stone circle) are never reached from outside, so they stay part of the arena.
    /// </summary>
    public static class ArenaFinder
    {
        /// <summary>The arena must keep this share of the walkable cells for the result to count.</summary>
        public const float MinKept = 0.25f, MaxKept = 0.95f;

        /// <summary>
        /// Returns a per-cell flag for walkable cells beyond the arena walls, or null when the map doesn't look
        /// like an enclosed arena (no wall, an opening too wide to seal, the start point would be trimmed, or
        /// almost nothing kept). <paramref name="kept"/> is the share of walkable cells left in the arena.
        /// </summary>
        public static bool[] FindOuterBand(WalkableFlood f, int radius, out float kept)
        {
            kept = 1f;
            if (f.WalkableCount == 0 || f.SeedIndex < 0 || radius < 1) return null;
            int n = f.Size;

            // Work in a window around the walkable area; everything beyond it is non-walkable anyway.
            int pad = radius + 2;
            int x0 = Math.Max(0, f.MinX - pad), x1 = Math.Min(n - 1, f.MaxX + pad);
            int y0 = Math.Max(0, f.MinY - pad), y1 = Math.Min(n - 1, f.MaxY + pad);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;

            var blocked = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    blocked[y * w + x] = !f.IsWalkable(x0 + x, y0 + y);
            var sealedOff = Dilate(blocked, w, h, radius, outsideValue: true);

            // Start from a ring just inside the walkable area's bounding box, where NavMesh beyond the walls
            // (if there is any) runs along the edge.
            int inset = radius + 1;
            int bx0 = f.MinX - x0 + inset, bx1 = f.MaxX - x0 - inset;
            int by0 = f.MinY - y0 + inset, by1 = f.MaxY - y0 - inset;
            if (bx1 <= bx0 || by1 <= by0) return null;

            var outer = new bool[w * h];
            var queue = new Queue<int>();
            void Reach(int x, int y)
            {
                if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
                int i = y * w + x;
                if (outer[i] || sealedOff[i]) return;
                outer[i] = true;
                queue.Enqueue(i);
            }
            for (int x = bx0; x <= bx1; x++) { Reach(x, by0); Reach(x, by1); }
            for (int y = by0; y <= by1; y++) { Reach(bx0, y); Reach(bx1, y); }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int cx = i % w, cy = i / w;
                Reach(cx - 1, cy);
                Reach(cx + 1, cy);
                Reach(cx, cy - 1);
                Reach(cx, cy + 1);
            }
            var grown = Dilate(outer, w, h, radius, outsideValue: false);

            var band = new bool[n * n];
            int walkable = 0, bandCells = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (blocked[i]) continue;
                    walkable++;
                    if (!grown[i]) continue;
                    bandCells++;
                    band[(y0 + y) * n + x0 + x] = true;
                }
            kept = walkable > 0 ? (float)(walkable - bandCells) / walkable : 1f;
            if (band[f.SeedIndex]) return null;
            return kept >= MinKept && kept <= MaxKept ? band : null;
        }

        /// <summary>Square (Chebyshev) dilation in two passes; cells beyond the window read as <paramref name="outsideValue"/>.</summary>
        static bool[] Dilate(bool[] src, int w, int h, int r, bool outsideValue)
        {
            var rows = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool any = false;
                    for (int dx = -r; dx <= r && !any; dx++)
                    {
                        int xx = x + dx;
                        any = (uint)xx < (uint)w ? src[y * w + xx] : outsideValue;
                    }
                    rows[y * w + x] = any;
                }
            var result = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool any = false;
                    for (int dy = -r; dy <= r && !any; dy++)
                    {
                        int yy = y + dy;
                        any = (uint)yy < (uint)h ? rows[yy * w + x] : outsideValue;
                    }
                    result[y * w + x] = any;
                }
            return result;
        }
    }
}
