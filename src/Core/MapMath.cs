using System;
using System.Numerics;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>What the minimap is looking at this frame.</summary>
    public readonly struct MapView
    {
        /// <summary>World position (X, Z) at the panel's center.</summary>
        public readonly Vector2 Center;
        /// <summary>Camera yaw in degrees. 0 means the camera looks along world +Z.</summary>
        public readonly float YawDegrees;
        /// <summary>World units from the panel center to its edge.</summary>
        public readonly float Radius;
        /// <summary>Half the panel's side length, in UI units.</summary>
        public readonly float HalfSize;

        public MapView(Vector2 center, float yawDegrees, float radius, float halfSize)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            Center = center;
            YawDegrees = yawDegrees;
            Radius = radius;
            HalfSize = halfSize;
        }

        /// <summary>UI units per world unit.</summary>
        public float Scale => HalfSize / Radius;
    }

    /// <summary>
    /// Converts between the world's ground plane (X, Z) and minimap panel space
    /// (origin at the panel center, +X right, +Y up, UI units). The panel is turned
    /// so the camera's forward direction points up.
    /// </summary>
    public static class MapMath
    {
        const double Deg2Rad = Math.PI / 180.0;
        const double Rad2Deg = 180.0 / Math.PI;

        /// <summary>
        /// Rotates the offset from the view center counter-clockwise by the yaw, then scales it.
        /// The world-aligned map image must be rotated by the same yaw (counter-clockwise) to line up.
        /// </summary>
        public static Vector2 WorldToPanel(in MapView view, Vector2 worldXZ)
        {
            Vector2 d = worldXZ - view.Center;
            double a = view.YawDegrees * Deg2Rad;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            return new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c) * view.Scale;
        }

        public static bool IsInside(Vector2 panel, float halfSize)
            => Math.Abs(panel.X) <= halfSize && Math.Abs(panel.Y) <= halfSize;

        /// <summary>
        /// If the point lies beyond the square inset by <paramref name="margin"/>, pulls it back onto
        /// that inset square along the line from the center and returns true. The angle is the
        /// direction of the original point (degrees, counter-clockwise from +X).
        /// </summary>
        public static bool ClampToEdge(Vector2 panel, float halfSize, float margin, out Vector2 clamped, out float angleDegrees)
        {
            float limit = Math.Max(0f, halfSize - margin);
            float m = Math.Max(Math.Abs(panel.X), Math.Abs(panel.Y));
            angleDegrees = (float)(Math.Atan2(panel.Y, panel.X) * Rad2Deg);
            if (m <= limit)
            {
                clamped = panel;
                return false;
            }
            clamped = panel * (limit / m);
            return true;
        }

        /// <summary>
        /// UI rotation (degrees, counter-clockwise, 0 = pointing up) for something facing
        /// <paramref name="forwardXZ"/> in the world, on a panel turned by <paramref name="yawDegrees"/>.
        /// </summary>
        public static float HeadingToPanelAngle(Vector2 forwardXZ, float yawDegrees)
        {
            float heading = (float)(Math.Atan2(forwardXZ.X, forwardXZ.Y) * Rad2Deg);
            return NormalizeDegrees(yawDegrees - heading);
        }

        /// <summary>Wraps an angle into (-180, 180].</summary>
        public static float NormalizeDegrees(float degrees)
        {
            float d = degrees % 360f;
            if (d > 180f) d -= 360f;
            if (d <= -180f) d += 360f;
            return d;
        }
    }
}
