using System.Collections.Generic;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class HudClearanceTests
    {
        // A 200 px minimap in the bottom-right corner of a 1920 x 1080 screen, 46 px above the bottom.
        static readonly ScreenBox Home = new ScreenBox(1700, 46, 1900, 246);

        static HudClearance New() => new HudClearance(persistSeconds: 1f, lowerDelaySeconds: 1.5f, gap: 8f, maxLift: 500f);

        static List<(long, ScreenBox)> One(ScreenBox b) => new List<(long, ScreenBox)> { (1, b) };
        static readonly List<(long, ScreenBox)> None = new List<(long, ScreenBox)>();

        // Artifact icons sitting low in the corner, under the minimap's lower half.
        static readonly ScreenBox Icons = new ScreenBox(1750, 40, 1900, 120);

        public static void IgnoresPanelsUntilTheyHaveStayedForASecond()
        {
            var h = New();
            Check.Equal(0f, h.Update(0f, Home, true, One(Icons)), "just appeared");
            Check.Equal(0f, h.Update(0.5f, Home, true, One(Icons)), "half a second");
            Check.Equal(120f - 46f + 8f, h.Update(1.1f, Home, true, One(Icons)), "moves up above it, plus the gap");
        }

        public static void FlashingElementsNeverMoveIt()
        {
            var h = New();
            for (int i = 0; i < 10; i++)
            {
                float t = i * 0.3f;
                Check.Equal(0f, h.Update(t, Home, true, i % 2 == 0 ? One(Icons) : None), $"t={t}");
            }
        }

        public static void ElementsBesideOrAboveTheMinimapDontCount()
        {
            var h = New();
            var beside = new ScreenBox(1400, 40, 1650, 120);
            var upper = new ScreenBox(1750, 200, 1900, 300);
            h.Update(0f, Home, true, new List<(long, ScreenBox)> { (1, beside), (2, upper) });
            Check.Equal(0f, h.Update(2f, Home, true, new List<(long, ScreenBox)> { (1, beside), (2, upper) }), "nothing to clear");
        }

        public static void WaitsBeforeMovingBackDown()
        {
            var h = New();
            h.Update(0f, Home, true, One(Icons));
            float up = h.Update(1.1f, Home, true, One(Icons));
            Check.Equal(up, h.Update(1.5f, Home, true, None), "still up right after the panel went away");
            Check.Equal(up, h.Update(2.5f, Home, true, None), "still up within the delay");
            Check.Equal(0f, h.Update(3.1f, Home, true, None), "back down after the delay");
        }

        public static void NeverMovesFurtherThanTheLimit()
        {
            var h = new HudClearance(1f, 1.5f, 8f, maxLift: 50f);
            h.Update(0f, Home, true, One(Icons));
            Check.Equal(50f, h.Update(1.1f, Home, true, One(Icons)), "capped");
        }

        public static void InATopCornerItMovesDown()
        {
            var h = New();
            var top = new ScreenBox(1700, 834, 1900, 1034);
            var banner = new ScreenBox(1720, 980, 1900, 1060);
            h.Update(0f, top, false, One(banner));
            Check.Equal(1034f - 980f + 8f, h.Update(1.1f, top, false, One(banner)), "moves down below it");
        }
    }
}
