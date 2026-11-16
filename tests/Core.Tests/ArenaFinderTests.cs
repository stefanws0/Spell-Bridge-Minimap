using System;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ArenaFinderTests
    {
        // A square of ground (|x|, |z| < 18) with a 2-cell-thick wall ring at 10 <= max(|x|, |z|) < 12,
        // broken by a gap on the +X side where |z| < gapHalfWidth. The flood leaks through the gap into
        // the band outside the wall, the way the game's NavMesh reaches enemy spawn areas.
        static GroundProbe Walled(float gapHalfWidth) => (float x, float z, float yHint, out float y) =>
        {
            y = 0f;
            float m = Math.Max(Math.Abs(x), Math.Abs(z));
            if (m >= 18f) return false;
            bool wall = m >= 10f && m < 12f;
            bool gap = x > 0f && Math.Abs(z) < gapHalfWidth;
            return !wall || gap;
        };

        static WalkableFlood Run(GroundProbe probe)
        {
            var f = new WalkableFlood(48, 1f, 0f, 0f);
            f.Seed(0.2f, 0.2f, 0f);
            while (f.HasWork) f.Step(5000, probe);
            return f;
        }

        static bool At(WalkableFlood f, bool[] band, float x, float z) => band[f.IndexOf(x, z)];

        public static void FloodRemembersWhereItStarted()
        {
            var f = Run(Walled(1f));
            Check.Equal(f.IndexOf(0.2f, 0.2f), f.SeedIndex, "first seed cell");
        }

        public static void ClosesASmallGapAndMarksTheBandOutsideTheWall()
        {
            var f = Run(Walled(1f));
            Check.Equal(CellState.Walkable, f.StateAt(15.2f, 0.2f), "the flood did leak outside the wall");
            var band = ArenaFinder.FindOuterBand(f, 2, out float kept);
            Check.True(band != null, "band found");
            Check.True(At(f, band, 15.2f, 0.2f), "outside the wall is band");
            Check.True(At(f, band, -15.2f, -15.2f), "far corner is band");
            Check.True(!At(f, band, 0.2f, 0.2f), "arena center is kept");
            Check.True(!At(f, band, 8.2f, 8.2f), "arena near the wall is kept");
            Check.True(kept > 0.25f && kept < 0.95f, $"kept fraction {kept}");
        }

        public static void LeavesOpenMapsAlone()
        {
            var f = Run((float x, float z, float yHint, out float y) => { y = 0f; return Math.Max(Math.Abs(x), Math.Abs(z)) < 18f; });
            Check.True(ArenaFinder.FindOuterBand(f, 2, out _) == null, "no wall, no band");
        }

        public static void LeavesWideOpeningsAlone()
        {
            var f = Run(Walled(4f)); // an 8-cell opening is a real entrance, not a crack
            Check.True(ArenaFinder.FindOuterBand(f, 2, out _) == null, "too wide to close");
        }

        public static void ClassifyTreatsTheBandAsOutside()
        {
            var f = Run(Walled(1f));
            var band = ArenaFinder.FindOuterBand(f, 2, out _);
            var cls = MapPainter.Classify(f, 1, band);
            Check.Equal(CellClass.Outside, cls[f.IndexOf(15.2f, 0.2f)], "band is outside");
            Check.Equal(CellClass.Outside, cls[f.IndexOf(10.5f, 5.5f)], "the wall itself is outside, not an obstacle");
            Check.Equal(CellClass.Walkable, cls[f.IndexOf(0.2f, 0.2f)], "arena is ground");
        }
    }
}
