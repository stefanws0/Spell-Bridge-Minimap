using System;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class MapPainterTests
    {
        static readonly MapPalette Palette = new MapPalette(
            ground: new Rgba(1, 1, 1),
            groundEdge: new Rgba(2, 2, 2),
            obstacle: new Rgba(3, 3, 3),
            obstacleEdge: new Rgba(4, 4, 4));

        static WalkableFlood Island() => WalkableFloodTests.Run(WalkableFloodTests.Island);

        static CellClass ClassAt(WalkableFlood f, CellClass[] cls, float x, float z) => cls[f.IndexOf(x, z)];

        static Rgba PixelAt(PaintedMap map, float x, float z)
        {
            int px = (int)Math.Floor((x - map.OriginX) / map.WorldWidth * map.Image.Width);
            int py = (int)Math.Floor((z - map.OriginZ) / map.WorldHeight * map.Image.Height);
            return map.Image.GetPixel(px, py);
        }

        public static void ClassifiesGroundObstaclesAndOutside()
        {
            var f = Island();
            var cls = MapPainter.Classify(f);
            Check.Equal(CellClass.Walkable, ClassAt(f, cls, -5f, 0f), "ground");
            Check.Equal(CellClass.Obstacle, ClassAt(f, cls, 5.2f, 0.2f), "middle of the hole");
            Check.Equal(CellClass.Obstacle, ClassAt(f, cls, 2.2f, 0.2f), "rim of the hole");
            Check.Equal(CellClass.Outside, ClassAt(f, cls, 10.2f, 0.2f), "just past the island edge");
            Check.Equal(CellClass.Outside, ClassAt(f, cls, 15f, 0f), "far outside");
        }

        public static void PaintCropsToTheGroundPlusPadding()
        {
            var f = Island();
            var map = MapPainter.Paint(f, MapPainter.Classify(f), Palette, pad: 1);
            // Walkable cells span indices 10..29 on both axes; one cell of padding makes 9..30.
            Check.Equal(22, map.Image.Width, "width");
            Check.Equal(22, map.Image.Height, "height");
            Check.Near(f.OriginX + 9f, map.OriginX, "origin x");
            Check.Near(f.OriginZ + 9f, map.OriginZ, "origin z");
            Check.Near(22f, map.WorldWidth, "world width");
        }

        public static void PaintColorsEachClass()
        {
            var f = Island();
            var map = MapPainter.Paint(f, MapPainter.Classify(f), Palette, pad: 1);
            Check.Equal(Palette.Ground, PixelAt(map, -5f, 0f), "ground");
            Check.Equal(Palette.GroundEdge, PixelAt(map, 9.2f, 0.2f), "island edge");
            Check.Equal(Palette.Obstacle, PixelAt(map, 5.2f, 0.2f), "middle of the hole");
            Check.Equal(Palette.ObstacleEdge, PixelAt(map, 2.2f, 0.2f), "rim of the hole");
            Check.Equal(Palette.Ground, PixelAt(map, 1.2f, 0.2f), "ground beside the hole is not an edge");
            Check.Equal(Rgba.Clear, PixelAt(map, -10.8f, -10.8f), "outside corner");
        }

        public static void PreviewWithoutClassesShowsOnlyGround()
        {
            var f = Island();
            var map = MapPainter.Paint(f, null, Palette, pad: 1);
            Check.Equal(Palette.Ground, PixelAt(map, -5f, 0f), "ground");
            Check.Equal(Palette.GroundEdge, PixelAt(map, 9.2f, 0.2f), "island edge");
            Check.Equal(Rgba.Clear, PixelAt(map, 5.2f, 0.2f), "hole not highlighted yet");
        }

        public static void NothingWalkableGivesNull()
        {
            var f = new WalkableFlood(10, 1f, 0f, 0f);
            Check.True(MapPainter.Paint(f, MapPainter.Classify(f), Palette, pad: 1) == null, "null map");
        }
    }
}
