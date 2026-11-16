using System.Numerics;
using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class MapMathTests
    {
        // Radius 20 world units across a 100-unit half panel: 5 UI units per world unit.
        static MapView View(float yaw) => new MapView(new Vector2(100f, 200f), yaw, 20f, 100f);

        public static void CenterMapsToOrigin()
        {
            var p = MapMath.WorldToPanel(View(37f), new Vector2(100f, 200f));
            Check.Near(0f, p.X, "x");
            Check.Near(0f, p.Y, "y");
        }

        public static void YawZeroKeepsWorldAxes()
        {
            var east = MapMath.WorldToPanel(View(0f), new Vector2(110f, 200f));
            Check.Near(50f, east.X, "+X world -> right");
            Check.Near(0f, east.Y, "east y");
            var north = MapMath.WorldToPanel(View(0f), new Vector2(100f, 210f));
            Check.Near(0f, north.X, "north x");
            Check.Near(50f, north.Y, "+Z world -> up");
        }

        public static void CameraForwardPointsUp()
        {
            // Yaw 90: the camera looks along world +X, so +X must point up on the panel.
            var ahead = MapMath.WorldToPanel(View(90f), new Vector2(110f, 200f));
            Check.Near(0f, ahead.X, "ahead x");
            Check.Near(50f, ahead.Y, "ahead y");
            // World +Z is then on the camera's left.
            var left = MapMath.WorldToPanel(View(90f), new Vector2(100f, 210f));
            Check.Near(-50f, left.X, "left x");
            Check.Near(0f, left.Y, "left y");
        }

        public static void ScaleFollowsRadius()
        {
            var v = new MapView(Vector2.Zero, 0f, 40f, 100f);
            Check.Near(2.5f, v.Scale, "scale");
            Check.Near(100f, MapMath.WorldToPanel(v, new Vector2(40f, 0f)).X, "edge");
        }

        public static void IsInsideUsesSquareBounds()
        {
            Check.True(MapMath.IsInside(new Vector2(99f, -99f), 100f), "near corner is inside");
            Check.True(!MapMath.IsInside(new Vector2(101f, 0f), 100f), "past right edge");
        }

        public static void ClampLeavesInsidePointsAlone()
        {
            bool clamped = MapMath.ClampToEdge(new Vector2(20f, 30f), 100f, 10f, out var p, out _);
            Check.True(!clamped, "not clamped");
            Check.Near(20f, p.X, "x");
            Check.Near(30f, p.Y, "y");
        }

        public static void ClampPullsOutsidePointsToInsetEdge()
        {
            Check.True(MapMath.ClampToEdge(new Vector2(300f, 0f), 100f, 10f, out var right, out float a0), "clamped");
            Check.Near(90f, right.X, "right x");
            Check.Near(0f, right.Y, "right y");
            Check.Near(0f, a0, "angle right");

            MapMath.ClampToEdge(new Vector2(300f, 300f), 100f, 10f, out var corner, out float a1);
            Check.Near(90f, corner.X, "corner x");
            Check.Near(90f, corner.Y, "corner y");
            Check.Near(45f, a1, "angle corner");

            MapMath.ClampToEdge(new Vector2(0f, -500f), 100f, 10f, out var down, out float a2);
            Check.Near(-90f, down.Y, "down y");
            Check.Near(-90f, a2, "angle down");
        }

        public static void CircleInsideUsesTheRadius()
        {
            Check.True(MapMath.IsInsideCircle(new Vector2(60f, 70f), 100f), "92 from center is inside");
            Check.True(!MapMath.IsInsideCircle(new Vector2(80f, 80f), 100f), "a square's corner (113) is outside");
        }

        public static void ClampToCircleLeavesInsidePointsAlone()
        {
            bool clamped = MapMath.ClampToCircle(new Vector2(20f, 30f), 100f, 10f, out var p, out _);
            Check.True(!clamped, "not clamped");
            Check.Near(20f, p.X, "x");
            Check.Near(30f, p.Y, "y");
        }

        public static void ClampToCirclePullsPointsOntoTheInsetRim()
        {
            Check.True(MapMath.ClampToCircle(new Vector2(300f, 300f), 100f, 10f, out var p, out float angle), "clamped");
            Check.Near(90f, p.Length(), "on the inset rim");
            Check.Near(p.X, p.Y, "same direction");
            Check.Near(45f, angle, "angle");
        }

        public static void HeadingAngleIsRelativeToCamera()
        {
            Check.Near(0f, MapMath.HeadingToPanelAngle(new Vector2(0f, 1f), 0f), "facing +Z, yaw 0 -> up");
            Check.Near(-90f, MapMath.HeadingToPanelAngle(new Vector2(1f, 0f), 0f), "facing +X, yaw 0 -> right");
            Check.Near(0f, MapMath.HeadingToPanelAngle(new Vector2(1f, 0f), 90f), "facing camera forward -> up");
            Check.Near(180f, System.Math.Abs(MapMath.HeadingToPanelAngle(new Vector2(0f, -1f), 0f)), "facing -Z -> down");
        }

        public static void NormalizeWrapsIntoHalfOpenRange()
        {
            Check.Near(-170f, MapMath.NormalizeDegrees(190f), "190");
            Check.Near(10f, MapMath.NormalizeDegrees(370f), "370");
            Check.Near(180f, MapMath.NormalizeDegrees(-180f), "-180");
        }
    }
}
