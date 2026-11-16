using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    public enum CellClass : byte { Outside = 0, Walkable = 1, Obstacle = 2 }

    public readonly struct MapPalette
    {
        public readonly Rgba Ground, GroundEdge, Obstacle, ObstacleEdge;

        public MapPalette(Rgba ground, Rgba groundEdge, Rgba obstacle, Rgba obstacleEdge)
        {
            Ground = ground;
            GroundEdge = groundEdge;
            Obstacle = obstacle;
            ObstacleEdge = obstacleEdge;
        }
    }

    /// <summary>A painted layout image and the world rectangle it covers.</summary>
    public sealed class PaintedMap
    {
        public readonly PixelBuffer Image;
        public readonly float OriginX, OriginZ, WorldWidth, WorldHeight;

        public PaintedMap(PixelBuffer image, float originX, float originZ, float worldWidth, float worldHeight)
        {
            Image = image;
            OriginX = originX;
            OriginZ = originZ;
            WorldWidth = worldWidth;
            WorldHeight = worldHeight;
        }
    }

    /// <summary>A rectangle of grid cells.</summary>
    public readonly struct CropRect
    {
        public readonly int X, Y, Width, Height;

        public CropRect(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }
    }

    public static class MapPainter
    {
        /// <summary>The walkable area's bounding box grown by <paramref name="pad"/> cells, clipped to the grid.</summary>
        public static bool TryCrop(WalkableFlood f, int pad, out CropRect crop)
        {
            crop = default;
            if (f.WalkableCount == 0) return false;
            int n = f.Size;
            int x0 = Math.Max(0, f.MinX - pad), x1 = Math.Min(n - 1, f.MaxX + pad);
            int y0 = Math.Max(0, f.MinY - pad), y1 = Math.Min(n - 1, f.MaxY + pad);
            crop = new CropRect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return true;
        }

        /// <summary>
        /// Composes captured level art (one pixel per cell of <paramref name="crop"/>) with the cell classes:
        /// ground and obstacles show the art, ground next to the outside is blended halfway to the edge colour,
        /// obstacle rims get the obstacle edge colour when <paramref name="highlightObstacles"/> is on, and
        /// everything outside the level is dimmed to 45%.
        /// </summary>
        public static PaintedMap PaintArt(WalkableFlood f, CellClass[] cls, PixelBuffer art, in MapPalette palette, CropRect crop, bool highlightObstacles)
        {
            if (art.Width != crop.Width || art.Height != crop.Height)
                throw new ArgumentException("art must be one pixel per cell of the crop", nameof(art));
            int n = f.Size;
            var img = new PixelBuffer(crop.Width, crop.Height);

            CellClass At(int cx, int cy)
                => (uint)cx >= (uint)n || (uint)cy >= (uint)n ? CellClass.Outside : cls[cy * n + cx];

            bool Touches(int cx, int cy, CellClass c)
                => At(cx - 1, cy) == c || At(cx + 1, cy) == c || At(cx, cy - 1) == c || At(cx, cy + 1) == c;

            for (int y = 0; y < crop.Height; y++)
                for (int x = 0; x < crop.Width; x++)
                {
                    int cx = crop.X + x, cy = crop.Y + y;
                    Rgba a = art.GetPixel(x, y);
                    Rgba c;
                    switch (At(cx, cy))
                    {
                        case CellClass.Walkable:
                            c = Touches(cx, cy, CellClass.Outside) ? Mix(a, palette.GroundEdge) : a;
                            break;
                        case CellClass.Obstacle:
                            c = highlightObstacles && Touches(cx, cy, CellClass.Walkable) ? palette.ObstacleEdge : a;
                            break;
                        default:
                            c = new Rgba((byte)(a.R * 45 / 100), (byte)(a.G * 45 / 100), (byte)(a.B * 45 / 100), 255);
                            break;
                    }
                    img.SetPixel(x, y, c);
                }

            return new PaintedMap(img,
                f.OriginX + crop.X * f.CellSize, f.OriginZ + crop.Y * f.CellSize,
                crop.Width * f.CellSize, crop.Height * f.CellSize);
        }

        static Rgba Mix(Rgba a, Rgba b)
            => new Rgba((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2), 255);

        /// <summary>
        /// Walkable cells stay walkable. A non-walkable cell is Outside if it connects to the grid border
        /// through other non-walkable cells (4-neighbour steps), and an Obstacle if walkable ground encloses it.
        /// </summary>
        public static CellClass[] Classify(WalkableFlood f)
        {
            int n = f.Size;
            var cls = new CellClass[n * n];
            var outside = new bool[n * n];
            var queue = new Queue<int>();

            void Reach(int cx, int cy)
            {
                if ((uint)cx >= (uint)n || (uint)cy >= (uint)n) return;
                int i = cy * n + cx;
                if (outside[i] || f.IsWalkable(cx, cy)) return;
                outside[i] = true;
                queue.Enqueue(i);
            }

            for (int k = 0; k < n; k++)
            {
                Reach(k, 0);
                Reach(k, n - 1);
                Reach(0, k);
                Reach(n - 1, k);
            }
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int cx = i % n, cy = i / n;
                Reach(cx - 1, cy);
                Reach(cx + 1, cy);
                Reach(cx, cy - 1);
                Reach(cx, cy + 1);
            }

            for (int cy = 0; cy < n; cy++)
                for (int cx = 0; cx < n; cx++)
                {
                    int i = cy * n + cx;
                    cls[i] = f.IsWalkable(cx, cy) ? CellClass.Walkable
                           : outside[i] ? CellClass.Outside
                           : CellClass.Obstacle;
                }
            return cls;
        }

        /// <summary>
        /// Paints the walkable area's bounding box plus <paramref name="pad"/> cells. With classes, ground next to
        /// the outside gets an edge color and obstacles are filled, with an edge where they meet ground. Without
        /// classes (a preview while mapping), only ground is drawn, with an edge next to anything not walkable.
        /// Returns null when nothing walkable has been found.
        /// </summary>
        public static PaintedMap Paint(WalkableFlood f, CellClass[] cls, in MapPalette palette, int pad)
        {
            if (!TryCrop(f, pad, out var crop)) return null;
            int n = f.Size;
            int x0 = crop.X, y0 = crop.Y, w = crop.Width, h = crop.Height;
            int x1 = x0 + w - 1, y1 = y0 + h - 1;
            var img = new PixelBuffer(w, h);

            CellClass At(int cx, int cy)
            {
                if ((uint)cx >= (uint)n || (uint)cy >= (uint)n) return CellClass.Outside;
                if (cls != null) return cls[cy * n + cx];
                return f.IsWalkable(cx, cy) ? CellClass.Walkable : CellClass.Outside;
            }

            bool Touches(int cx, int cy, CellClass c)
                => At(cx - 1, cy) == c || At(cx + 1, cy) == c || At(cx, cy - 1) == c || At(cx, cy + 1) == c;

            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    switch (At(cx, cy))
                    {
                        case CellClass.Walkable:
                            img.SetPixel(cx - x0, cy - y0, Touches(cx, cy, CellClass.Outside) ? palette.GroundEdge : palette.Ground);
                            break;
                        case CellClass.Obstacle:
                            img.SetPixel(cx - x0, cy - y0, Touches(cx, cy, CellClass.Walkable) ? palette.ObstacleEdge : palette.Obstacle);
                            break;
                    }
                }

            return new PaintedMap(img,
                f.OriginX + x0 * f.CellSize, f.OriginZ + y0 * f.CellSize,
                w * f.CellSize, h * f.CellSize);
        }
    }
}
