using System;
using System.Numerics;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ViewPainterTests
    {
        static readonly Rgba Red = new Rgba(200, 30, 30), Blue = new Rgba(30, 30, 200);

        // A camera looking straight down: clip (x, y, z) maps to world (10x, 10 - 10z, 10y), so the near plane is at
        // height 20, the far plane at 0, and the view covers world x and z from -10 to 10.
        static readonly Matrix4x4 StraightDown = new Matrix4x4(
            10, 0, 0, 0,
            0, 0, 10, 0,
            0, -10, 0, 0,
            0, 10, 0, 1);

        // The same, but tilted: rays also move +Z as they go down (like a camera looking north at an angle).
        // clip (x, y, z) -> world (10x, 10 - 10z, 10y + 5z).
        static readonly Matrix4x4 Tilted = new Matrix4x4(
            10, 0, 0, 0,
            0, 0, 10, 0,
            0, -10, 5, 0,
            0, 10, 0, 1);

        static PixelBuffer HalfAndHalf()
        {
            var view = new PixelBuffer(20, 20);
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    view.SetPixel(x, y, x < 10 ? Red : Blue);
            return view;
        }

        public static void PaintsWhatTheCameraSeesWhereItLands()
        {
            var p = new ViewPainter(20, 20, -10f, -10f, 1f);
            int added = p.AddView(HalfAndHalf(), StraightDown, 0f, null);
            Check.Equal(400, added, "every pixel lands on the ground");
            var map = p.Result(1f);
            Check.Equal(Red, map.GetPixel(3, 10), "left of the view is red");
            Check.Equal(Blue, map.GetPixel(16, 10), "right of the view is blue");
        }

        public static void AveragesOverlappingViews()
        {
            var p = new ViewPainter(20, 20, -10f, -10f, 1f);
            var red = new PixelBuffer(20, 20);
            var blue = new PixelBuffer(20, 20);
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++) { red.SetPixel(x, y, Red); blue.SetPixel(x, y, Blue); }
            p.AddView(red, StraightDown, 0f, null);
            p.AddView(blue, StraightDown, 0f, null);
            Check.Equal(new Rgba(115, 30, 115), p.Result(1f).GetPixel(5, 5), "average of both views");
        }

        public static void SkipsExcludedPixelsAndUnderSampledCells()
        {
            var p = new ViewPainter(20, 20, -10f, -10f, 1f);
            p.AddView(HalfAndHalf(), StraightDown, 0f, (x, y) => x >= 10);
            var map = p.Result(1f);
            Check.Equal((byte)0, map.GetPixel(3, 10).A, "excluded pixels paint nothing");
            Check.Equal(Blue, map.GetPixel(16, 10), "the rest still paints");
            Check.Equal((byte)0, new ViewPainter(20, 20, -10f, -10f, 1f).Result(1f).GetPixel(5, 5).A, "no samples, no colour");
        }

        public static void DropsPixelsThatPassOverAnObstacle()
        {
            // With the tilted camera a ray reaching the ground at z travels from z - 2 (height 4) down to z.
            // An obstacle cell at z in [0, 1) hides the ground just beyond it.
            bool Blocked(int cx, int cy) => cy == 10; // world z from 0 to 1
            var view = new PixelBuffer(20, 20);
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    view.SetPixel(x, y, Red);

            var free = new ViewPainter(20, 20, -10f, -10f, 1f);
            free.AddView(view, Tilted, 0f, null);
            var guarded = new ViewPainter(20, 20, -10f, -10f, 1f);
            guarded.AddView(view, Tilted, 0f, null, Blocked, occluderHeight: 4f);

            Check.True(free.Result(1f).GetPixel(10, 12).A > 0, "without the guard the cell behind the obstacle is painted");
            Check.Equal((byte)0, guarded.Result(1f).GetPixel(10, 12).A, "with it, rays that crossed the obstacle are dropped");
            Check.True(guarded.Result(1f).GetPixel(10, 16).A > 0, "cells further away are still painted");
        }
    }
}
