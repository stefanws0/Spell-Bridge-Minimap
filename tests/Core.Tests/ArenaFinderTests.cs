using System;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class ArenaFinderTests
    {
        // A square of ground (|x|, |z| < 18) with a 2-cell-thick wall ring at 10 <= max(|x|, |z|) < 12,
        // broken by a gap on the +X side where |z| < gapHalfWidth. The flood leaks through the gap into
        // the band outside the wall, the way the game's NavMesh reaches enemy spawn areas.
        // Inside the arena sits a small walled room around (4, 4) with a 2-cell door on its +Z side.
        static GroundProbe Walled(float gapHalfWidth) => (float x, float z, float yHint, out float y) =>
        {
            y = 0f;
            float m = Math.Max(Math.Abs(x), Math.Abs(z));
            if (m >= 18f) return false;
            bool wall = m >= 10f && m < 12f;
            bool gap = x > 0f && Math.Abs(z) < gapHalfWidth;
            float rm = Math.Max(Math.Abs(x - 4f), Math.Abs(z - 4f));
            bool roomWall = rm >= 2f && rm < 3f;
            bool door = z > 4f && Math.Abs(x - 4f) < 1f;
            return !(wall && !gap) && !(roomWall && !door);
        };

        static WalkableFlood Run(GroundProbe probe, float seedX = 0.2f, float seedZ = 0.2f)
        {
            var f = new WalkableFlood(48, 1f, 0f, 0f);
            f.Seed(seedX, seedZ, 0f);
            while (f.HasWork) f.Step(5000, probe);
            return f;
        }

        static bool At(WalkableFlood f, bool[] band, float x, float z) => band[f.IndexOf(x, z)];

        // The fire world: a small walled arena (6 <= max(|x|, |z|) < 8, gap on +X) inside NavMesh that reaches
        // much further out (|x|, |z| < 23), so the arena is well under a quarter of the walkable cells.
        static readonly GroundProbe SmallArenaInWideNavMesh = (float x, float z, float yHint, out float y) =>
        {
            y = 0f;
            float m = Math.Max(Math.Abs(x), Math.Abs(z));
            if (m >= 23f) return false;
            bool wall = m >= 6f && m < 8f;
            bool gap = x > 0f && Math.Abs(z) < 1f;
            return !(wall && !gap);
        };

        public static void KeepsAnArenaThatIsSmallNextToTheNavMeshButLargeInItself()
        {
            var f = Run(SmallArenaInWideNavMesh);
            var band = ArenaFinder.FindOuterBand(f, 2, out float kept, null, minArenaCells: 100);
            Check.True(kept < ArenaFinder.MinKept, $"arena share {kept} is under the old limit");
            Check.True(band != null, "band found: the arena is big enough on its own");
            Check.True(!At(f, band, 0.2f, 0.2f), "arena center kept");
            Check.True(At(f, band, -20.2f, -20.2f), "far out is band");
        }

        public static void StillRejectsSmallPocketsAroundTheStart()
        {
            var f = Run(SmallArenaInWideNavMesh);
            Check.True(ArenaFinder.FindOuterBand(f, 2, out _) == null, "a 140-cell pocket is too small to be the arena");
        }

        public static void FloodRemembersWhereItStarted()
        {
            var f = Run(Walled(1f));
            Check.Equal(f.IndexOf(0.2f, 0.2f), f.SeedIndex, "first seed cell");
        }

        public static void MarksTheBandBeyondTheWall()
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

        public static void KeepsPocketsInsideTheArena()
        {
            var f = Run(Walled(1f));
            var band = ArenaFinder.FindOuterBand(f, 2, out _);
            Check.True(band != null, "band found");
            Check.Equal(CellState.Walkable, f.StateAt(4.2f, 4.2f), "the room is ground");
            Check.True(!At(f, band, 4.2f, 4.2f), "a room with a narrow door stays part of the arena");
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

        public static void GivesUpWhenMappingStartedOutsideTheWall()
        {
            var f = Run(Walled(1f), seedX: 15.2f, seedZ: 0.2f);
            Check.True(ArenaFinder.FindOuterBand(f, 2, out _) == null, "the start point would be trimmed");
        }

        public static void ExtraBlockedCellsCountAsWalls()
        {
            // An open map whose wall ring is only known from elsewhere (e.g. solid scenery the NavMesh ignores).
            var f = Run((float x, float z, float yHint, out float y) => { y = 0f; return Math.Max(Math.Abs(x), Math.Abs(z)) < 18f; });
            Check.True(ArenaFinder.FindOuterBand(f, 2, out _) == null, "no band without the extra walls");
            var walls = new bool[f.Size * f.Size];
            for (int cy = 0; cy < f.Size; cy++)
                for (int cx = 0; cx < f.Size; cx++)
                {
                    float x = f.OriginX + cx + 0.5f, z = f.OriginZ + cy + 0.5f, m = Math.Max(Math.Abs(x), Math.Abs(z));
                    walls[cy * f.Size + cx] = m >= 10f && m < 12f;
                }
            var band = ArenaFinder.FindOuterBand(f, 2, out _, walls);
            Check.True(band != null && At(f, band, 15.2f, 0.2f) && !At(f, band, 0.2f, 0.2f), "band found with the extra walls");
        }

        public static void ClassifyTreatsTheBandAsOutside()
        {
            var f = Run(Walled(1f));
            var band = ArenaFinder.FindOuterBand(f, 2, out _);
            var cls = MapPainter.Classify(f, 1, band);
            Check.Equal(CellClass.Outside, cls[f.IndexOf(15.2f, 0.2f)], "band is outside");
            Check.Equal(CellClass.Outside, cls[f.IndexOf(10.5f, 5.5f)], "the wall itself is outside, not an obstacle");
            Check.Equal(CellClass.Walkable, cls[f.IndexOf(0.2f, 0.2f)], "arena is ground");
            Check.Equal(CellClass.Obstacle, cls[f.IndexOf(6.5f, 4.2f)], "the room's wall is an obstacle");
        }
    }
}
