using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// Separates the arena from NavMesh that reaches past its walls. In The Spell Brigade the NavMesh extends
    /// beyond the arena's rock wall (enemies come in from out there), so the flood leaks through gaps in the
    /// wall. This seals gaps up to 2 x radius cells wide by growing every non-walkable cell by that radius,
    /// floods from where mapping started, then grows the result back by the same radius. Walkable cells left
    /// out are the band beyond the walls.
    /// </summary>
    public static class ArenaFinder
    {
        /// <summary>The arena must keep this share of the walkable cells for the result to count.</summary>
        public const float MinKept = 0.25f, MaxKept = 0.95f;

        /// <summary>
        /// Returns a per-cell flag for walkable cells outside the arena, or null when the result doesn't look like
        /// an enclosed arena (no wall, an opening too wide to seal, or almost nothing kept). <paramref name="kept"/>
        /// is the share of walkable cells inside the arena.
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

            int seedX = f.SeedIndex % n - x0, seedY = f.SeedIndex / n - y0;
            int start = NearestFree(sealedOff, w, h, seedX, seedY, radius + 2);
            if (start < 0) return null;

            var inside = new bool[w * h];
            var queue = new Queue<int>();
            inside[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int cx = i % w, cy = i / w;
                void Visit(int x, int y)
                {
                    if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;
                    int j = y * w + x;
                    if (inside[j] || sealedOff[j]) return;
                    inside[j] = true;
                    queue.Enqueue(j);
                }
                Visit(cx - 1, cy);
                Visit(cx + 1, cy);
                Visit(cx, cy - 1);
                Visit(cx, cy + 1);
            }
            var arena = Dilate(inside, w, h, radius, outsideValue: false);

            var band = new bool[n * n];
            int walkable = 0, keptCells = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (blocked[i]) continue;
                    walkable++;
                    if (arena[i]) keptCells++;
                    else band[(y0 + y) * n + x0 + x] = true;
                }
            kept = walkable > 0 ? (float)keptCells / walkable : 1f;
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

        static int NearestFree(bool[] sealedOff, int w, int h, int sx, int sy, int maxDistance)
        {
            int best = -1, bestD2 = int.MaxValue;
            for (int dy = -maxDistance; dy <= maxDistance; dy++)
                for (int dx = -maxDistance; dx <= maxDistance; dx++)
                {
                    int x = sx + dx, y = sy + dy;
                    if ((uint)x >= (uint)w || (uint)y >= (uint)h || sealedOff[y * w + x]) continue;
                    int d2 = dx * dx + dy * dy;
                    if (d2 < bestD2) { bestD2 = d2; best = y * w + x; }
                }
            return best;
        }
    }
}
