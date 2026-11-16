using System.Numerics;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class RoadMaskTests
    {
        // A 10x10-cell mask, 1 world unit per cell, starting at world (0, 0).
        static float[] Mask() => new float[100];

        public static void AxisAlignedRectangleCoversItsCells()
        {
            var m = Mask();
            RoadMask.AddRectangle(m, 10, 10, 0f, 0f, 1f, new Vector2(5f, 5f), new Vector2(1f, 0f), new Vector2(0f, 1f), 2f, 1f);
            Check.Equal(1f, m[5 * 10 + 5], "center cell");
            Check.Equal(1f, m[5 * 10 + 3], "within half-width 2 (cell center x 3.5)");
            Check.Equal(0f, m[5 * 10 + 2], "beyond half-width (cell center x 2.5)");
            Check.Equal(0f, m[7 * 10 + 5], "beyond half-height (cell center z 7.5)");
        }

        public static void RotatedRectangleFollowsItsAxes()
        {
            var m = Mask();
            // Rotated 90 degrees: the long side now runs along Z.
            RoadMask.AddRectangle(m, 10, 10, 0f, 0f, 1f, new Vector2(5f, 5f), new Vector2(0f, 1f), new Vector2(-1f, 0f), 2f, 1f);
            Check.Equal(1f, m[3 * 10 + 5], "along Z within 2");
            Check.Equal(0f, m[5 * 10 + 3], "along X beyond 1");
        }

        public static void CellsOutsideTheMaskAreIgnored()
        {
            var m = Mask();
            RoadMask.AddRectangle(m, 10, 10, 0f, 0f, 1f, new Vector2(-50f, -50f), new Vector2(1f, 0f), new Vector2(0f, 1f), 3f, 3f);
            foreach (float v in m) Check.Equal(0f, v, "untouched");
        }
    }
}
