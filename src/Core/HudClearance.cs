using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>A rectangle in screen pixels, y up.</summary>
    public readonly struct ScreenBox
    {
        public readonly float XMin, YMin, XMax, YMax;

        public ScreenBox(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin; YMin = yMin; XMax = xMax; YMax = yMax;
        }

        public float CenterY => (YMin + YMax) * 0.5f;

        public bool Overlaps(in ScreenBox o) => XMin < o.XMax && o.XMin < XMax && YMin < o.YMax && o.YMin < YMax;
    }

    /// <summary>
    /// How far the minimap must move to clear the game's HUD in its corner. An element counts once it has
    /// overlapped the minimap's home spot for <c>persistSeconds</c> (so pop-ups and damage numbers don't make it
    /// jump) and sits in the half of the minimap nearest the screen edge (below its middle in a bottom corner,
    /// above it in a top corner). The minimap moves away at once and moves back only after nothing has needed it
    /// for <c>lowerDelaySeconds</c>.
    /// </summary>
    public sealed class HudClearance
    {
        readonly float persistSeconds, lowerDelaySeconds, gap, maxLift;
        readonly Dictionary<long, float> firstSeen = new Dictionary<long, float>();
        readonly HashSet<long> seenNow = new HashSet<long>();
        float target, lowerAfter;

        public HudClearance(float persistSeconds, float lowerDelaySeconds, float gap, float maxLift)
        {
            this.persistSeconds = persistSeconds;
            this.lowerDelaySeconds = lowerDelaySeconds;
            this.gap = gap;
            this.maxLift = maxLift;
        }

        public void Reset()
        {
            firstSeen.Clear();
            target = 0f;
            lowerAfter = 0f;
        }

        /// <summary>
        /// Returns how many pixels to move the minimap away from its corner's edge (up in a bottom corner, down in
        /// a top corner). <paramref name="home"/> is where the minimap sits without moving.
        /// </summary>
        public float Update(float now, in ScreenBox home, bool bottomCorner, IEnumerable<(long id, ScreenBox box)> elements)
        {
            float need = 0f;
            seenNow.Clear();
            foreach (var (id, box) in elements)
            {
                if (!box.Overlaps(home)) continue;
                if (bottomCorner ? box.CenterY > home.CenterY : box.CenterY < home.CenterY) continue;
                seenNow.Add(id);
                if (!firstSeen.TryGetValue(id, out float since)) firstSeen[id] = since = now;
                if (now - since < persistSeconds) continue;
                float move = bottomCorner ? box.YMax - home.YMin : home.YMax - box.YMin;
                need = Math.Max(need, move + gap);
            }
            if (firstSeen.Count > seenNow.Count)
            {
                var gone = new List<long>();
                foreach (var id in firstSeen.Keys) if (!seenNow.Contains(id)) gone.Add(id);
                foreach (var id in gone) firstSeen.Remove(id);
            }

            need = Math.Min(need, maxLift);
            if (need >= target)
            {
                target = need;
                lowerAfter = now + lowerDelaySeconds;
            }
            else if (now >= lowerAfter)
            {
                target = need;
            }
            return target;
        }
    }
}
