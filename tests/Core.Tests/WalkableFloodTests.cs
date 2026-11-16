using System;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class WalkableFloodTests
    {
        // Ground: a disc of radius 10 around the origin with a round hole of radius 3 centered at (5, 0).
        internal static bool Island(float x, float z, float yHint, out float y)
        {
            y = 0f;
            float hole = (x - 5f) * (x - 5f) + z * z;
            return x * x + z * z <= 100f && hole > 9f;
        }

        internal static WalkableFlood Run(GroundProbe probe, int size = 40)
        {
            var f = new WalkableFlood(size, 1f, 0f, 0f);
            f.Seed(0.2f, 0.2f, 0f);
            while (f.HasWork) f.Step(1000, probe);
            return f;
        }

        public static void GridIsCenteredOnTheGivenPoint()
        {
            var f = new WalkableFlood(40, 1f, 100f, -50f);
            Check.Near(80f, f.OriginX, "origin x");
            Check.Near(-70f, f.OriginZ, "origin z");
        }

        public static void FloodFindsTheConnectedGround()
        {
            var f = Run(Island);
            Check.Equal(CellState.Walkable, f.StateAt(-5f, 0f), "west side");
            Check.Equal(CellState.Walkable, f.StateAt(0f, 8f), "north side");
            Check.Equal(CellState.Walkable, f.StateAt(9.2f, 0.2f), "beyond the hole");
        }

        public static void FloodStopsAtTheEdgeAndAroundHoles()
        {
            var f = Run(Island);
            Check.Equal(CellState.Blocked, f.StateAt(10.2f, 0.2f), "first cell past the edge is probed and blocked");
            Check.Equal(CellState.Unknown, f.StateAt(15f, 0f), "further out is never probed");
            Check.Equal(CellState.Unknown, f.StateAt(5.2f, 0.2f), "middle of the hole is never probed");
        }

        public static void StepRespectsTheProbeBudget()
        {
            var f = new WalkableFlood(40, 1f, 0f, 0f);
            f.Seed(0.2f, 0.2f, 0f);
            Check.Equal(5, f.Step(5, Island), "probes used");
            Check.True(f.HasWork, "work left");
        }

        public static void CountsWalkableCellsAndBounds()
        {
            bool Square(float x, float z, float yHint, out float y)
            {
                y = 0f;
                return Math.Abs(x) < 3f && Math.Abs(z) < 3f;
            }
            var f = Run(Square);
            Check.Equal(36, f.WalkableCount, "6x6 cells");
            Check.Equal(17, f.MinX, "min x");
            Check.Equal(22, f.MaxX, "max x");
            Check.Equal(17, f.MinY, "min y");
            Check.Equal(22, f.MaxY, "max y");
        }

        public static void FollowsSlopesUsingNeighbourHeights()
        {
            // A ramp rising 0.5 per unit along x; the probe only finds ground within 1 unit of the height hint.
            bool Ramp(float x, float z, float yHint, out float y)
            {
                y = x * 0.5f;
                return Math.Abs(z) < 2f && Math.Abs(x) < 15f && Math.Abs(y - yHint) <= 1f;
            }
            var f = new WalkableFlood(40, 1f, 0f, 0f);
            f.Seed(0.2f, 0.2f, 0.25f);
            while (f.HasWork) f.Step(1000, Ramp);
            Check.Equal(CellState.Walkable, f.StateAt(12.2f, 0.2f), "far up the ramp");
            Check.Equal(CellState.Walkable, f.StateAt(-12.2f, 0.2f), "far down the ramp");
            Check.Near(7.25f, f.MaxHeight, "highest ground found (cell center x = 14.5)");
            Check.True(f.TryGroundHeight(12.2f, 0.2f, out float y), "ground under a walkable cell");
            Check.Near(6.25f, y, "its height (cell center x = 12.5)");
            Check.True(!f.TryGroundHeight(12.2f, 5.2f, out _), "no ground off the ramp");
        }

        public static void SeedOutsideTheGridDoesNothing()
        {
            var f = new WalkableFlood(10, 1f, 0f, 0f);
            f.Seed(100f, 100f, 0f);
            Check.True(!f.HasWork, "no work");
            Check.Equal(CellState.Blocked, f.StateAt(100f, 100f), "outside the grid reads as blocked");
        }

        public static void ReseedingExploredGroundAddsNoWork()
        {
            var f = Run(Island);
            f.Seed(0.2f, 0.2f, 0f);
            Check.True(!f.HasWork, "already explored");
        }

        public static void ReachingTheGridEdgeIsReported()
        {
            bool Everywhere(float x, float z, float yHint, out float y)
            {
                y = 0f;
                return true;
            }
            var f = Run(Everywhere, size: 10);
            Check.True(f.TouchedBorder, "touched border");
            Check.Equal(100, f.WalkableCount, "all cells");
        }
    }
}
