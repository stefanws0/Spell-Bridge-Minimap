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

        static readonly Rgba ArtColor = new Rgba(100, 150, 50);

        static PaintedMap ArtMap(bool highlight)
        {
            var f = Island();
            Check.True(MapPainter.TryCrop(f, 3, out var crop), "crop");
            var art = new PixelBuffer(crop.Width, crop.Height);
            for (int y = 0; y < art.Height; y++)
                for (int x = 0; x < art.Width; x++)
                    art.SetPixel(x, y, ArtColor);
            return MapPainter.PaintArt(f, MapPainter.Classify(f), art, Palette, crop, highlight);
        }

        public static void CropCoversGroundPlusPadding()
        {
            var f = Island();
            Check.True(MapPainter.TryCrop(f, 3, out var crop), "found ground");
            Check.Equal(7, crop.X, "x");
            Check.Equal(7, crop.Y, "y");
            Check.Equal(26, crop.Width, "width");
            Check.Equal(26, crop.Height, "height");
            Check.True(!MapPainter.TryCrop(new WalkableFlood(10, 1f, 0f, 0f), 3, out _), "nothing walkable");
        }

        public static void ArtShowsThroughOnGroundAndObstacles()
        {
            var map = ArtMap(highlight: true);
            Check.Near(-13f, map.OriginX, "origin x");
            Check.Equal(ArtColor, PixelAt(map, -5f, 0f), "ground");
            Check.Equal(ArtColor, PixelAt(map, 5.2f, 0.2f), "middle of an obstacle");
            Check.Equal(new Rgba(51, 76, 26), PixelAt(map, 9.2f, 0.2f), "level edge is half art, half edge colour");
        }

        public static void ArtObstacleRimFollowsTheHighlightSetting()
        {
            Check.Equal(Palette.ObstacleEdge, PixelAt(ArtMap(highlight: true), 2.2f, 0.2f), "highlighted rim");
            Check.Equal(ArtColor, PixelAt(ArtMap(highlight: false), 2.2f, 0.2f), "plain art when off");
        }

        public static void ArtOutsideTheLevelIsLeftEmpty()
        {
            Check.Equal(Rgba.Clear, PixelAt(ArtMap(highlight: true), -12.5f, -12.5f), "outside the arena");
            Check.Equal(Rgba.Clear, PixelAt(ArtMap(highlight: true), 10.2f, 0.2f), "just past the arena edge");
        }

        public static void ArtMustMatchTheCrop()
        {
            var f = Island();
            MapPainter.TryCrop(f, 3, out var crop);
            bool threw = false;
            try { MapPainter.PaintArt(f, MapPainter.Classify(f), new PixelBuffer(5, 5), Palette, crop, true); }
            catch (ArgumentException) { threw = true; }
            Check.True(threw, "wrong size rejected");
        }

        public static void NothingWalkableGivesNull()
        {
            var f = new WalkableFlood(10, 1f, 0f, 0f);
            Check.True(MapPainter.Paint(f, MapPainter.Classify(f), Palette, pad: 1) == null, "null map");
        }
    }
}
