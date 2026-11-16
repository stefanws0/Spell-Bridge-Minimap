using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// Answers "is there walkable ground at this cell center?". <paramref name="yHint"/> is the height to
    /// search near (the neighbouring cell's ground height); <paramref name="y"/> returns the ground height found.
    /// </summary>
    public delegate bool GroundProbe(float x, float z, float yHint, out float y);

    public enum CellState : byte { Unknown = 0, Queued = 1, Walkable = 2, Blocked = 3 }

    /// <summary>
    /// Discovers the walkable area by flood-filling outward from seed points, one probe per cell.
    /// The grid is Size x Size cells of CellSize world units; cell (0, 0) starts at (OriginX, OriginZ).
    /// Cells inside obstacles or outside the level are never probed unless they border walkable ground.
    /// Work is done in slices (<see cref="Step"/>) so it can be spread over frames.
    /// </summary>
    public sealed class WalkableFlood
    {
        public readonly int Size;
        public readonly float CellSize, OriginX, OriginZ;

        readonly CellState[] states;
        readonly float[] heights;
        readonly Queue<int> queue = new Queue<int>();

        public int WalkableCount { get; private set; }
        public int MinX { get; private set; } = int.MaxValue;
        public int MinY { get; private set; } = int.MaxValue;
        public int MaxX { get; private set; } = -1;
        public int MaxY { get; private set; } = -1;
        /// <summary>Highest ground height found so far (float.MinValue until something is walkable).</summary>
        public float MaxHeight { get; private set; } = float.MinValue;
        /// <summary>True once walkable ground reached the grid's edge (the level may be larger than the grid).</summary>
        public bool TouchedBorder { get; private set; }
        public bool HasWork => queue.Count > 0;
        /// <summary>Cell index of the first seed (where mapping started, e.g. the player's spawn); -1 before seeding.</summary>
        public int SeedIndex { get; private set; } = -1;

        /// <summary>Creates a grid centered on (centerX, centerZ).</summary>
        public WalkableFlood(int size, float cellSize, float centerX, float centerZ)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            Size = size;
            CellSize = cellSize;
            OriginX = centerX - size * cellSize * 0.5f;
            OriginZ = centerZ - size * cellSize * 0.5f;
            states = new CellState[size * size];
            heights = new float[size * size];
        }

        public bool IsWalkable(int cx, int cy)
            => (uint)cx < (uint)Size && (uint)cy < (uint)Size && states[cy * Size + cx] == CellState.Walkable;

        /// <summary>Index of the cell containing the world point, or -1 outside the grid.</summary>
        public int IndexOf(float x, float z)
        {
            int cx = (int)Math.Floor((x - OriginX) / CellSize);
            int cy = (int)Math.Floor((z - OriginZ) / CellSize);
            if ((uint)cx >= (uint)Size || (uint)cy >= (uint)Size) return -1;
            return cy * Size + cx;
        }

        /// <summary>State of the cell containing the world point. Points outside the grid read as Blocked.</summary>
        public CellState StateAt(float x, float z)
        {
            int i = IndexOf(x, z);
            return i < 0 ? CellState.Blocked : states[i];
        }

        /// <summary>Ground height of the walkable cell containing the point; false if that cell isn't walkable.</summary>
        public bool TryGroundHeight(float x, float z, out float y)
        {
            int i = IndexOf(x, z);
            if (i < 0 || states[i] != CellState.Walkable)
            {
                y = 0f;
                return false;
            }
            y = heights[i];
            return true;
        }

        /// <summary>Queues the unexplored cells in the 3x3 block around a point, searching near height <paramref name="y"/>.</summary>
        public void Seed(float x, float z, float y)
        {
            int i = IndexOf(x, z);
            if (i < 0) return;
            if (SeedIndex < 0) SeedIndex = i;
            int cx = i % Size, cy = i / Size;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Enqueue(cx + dx, cy + dy, y);
        }

        /// <summary>Probes up to <paramref name="maxProbes"/> queued cells. Returns how many were probed.</summary>
        public int Step(int maxProbes, GroundProbe probe)
        {
            int done = 0;
            while (done < maxProbes && queue.Count > 0)
            {
                int i = queue.Dequeue();
                if (states[i] != CellState.Queued) continue;
                done++;

                int cx = i % Size, cy = i / Size;
                float wx = OriginX + (cx + 0.5f) * CellSize;
                float wz = OriginZ + (cy + 0.5f) * CellSize;
                if (!probe(wx, wz, heights[i], out float y))
                {
                    states[i] = CellState.Blocked;
                    continue;
                }

                states[i] = CellState.Walkable;
                heights[i] = y;
                WalkableCount++;
                if (y > MaxHeight) MaxHeight = y;
                if (cx < MinX) MinX = cx;
                if (cx > MaxX) MaxX = cx;
                if (cy < MinY) MinY = cy;
                if (cy > MaxY) MaxY = cy;
                if (cx == 0 || cy == 0 || cx == Size - 1 || cy == Size - 1) TouchedBorder = true;

                Enqueue(cx - 1, cy, y);
                Enqueue(cx + 1, cy, y);
                Enqueue(cx, cy - 1, y);
                Enqueue(cx, cy + 1, y);
            }
            return done;
        }

        void Enqueue(int cx, int cy, float yHint)
        {
            if ((uint)cx >= (uint)Size || (uint)cy >= (uint)Size) return;
            int i = cy * Size + cx;
            if (states[i] != CellState.Unknown) return;
            states[i] = CellState.Queued;
            heights[i] = yHint;
            queue.Enqueue(i);
        }
    }
}
