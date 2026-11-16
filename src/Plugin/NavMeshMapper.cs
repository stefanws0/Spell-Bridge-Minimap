using System;
using System.Diagnostics;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.AI;
using NVec2 = System.Numerics.Vector2;

namespace SpellBrigadeMinimap
{
    /// <summary>A level layout texture and where it sits in the world.</summary>
    internal sealed class BakedMap
    {
        public Texture2D Texture;
        public float OriginX, OriginZ, WorldWidth, WorldHeight;

        public void Destroy()
        {
            if (Texture != null) UnityEngine.Object.Destroy(Texture);
            Texture = null;
        }
    }

    /// <summary>
    /// Builds the level layout by asking the NavMesh "is this cell walkable?" one cell at a time, flood-filling
    /// outward from the player within a small time budget each frame. The game build strips Unity's NavMesh
    /// triangle export (CalculateTriangulation); SamplePosition is the NavMesh query it does ship.
    /// </summary>
    internal sealed class NavMeshMapper
    {
        const int GridSize = 768;            // cells per side
        const float CellSize = 1f;           // world units per cell
        const float ProbeRange = 3f;         // how far from the cell center (mostly vertically) to look for NavMesh
        const float MaxCellOffset = 0.6f;    // a hit further than this sideways belongs to another cell
        const int NavMeshAllAreas = -1;
        const double FrameBudgetMs = 2.0;
        const int ProbesPerSlice = 64;
        const int Padding = 2;
        const float PreviewSeconds = 1f;
        const float RetrySeconds = 2f;
        const int MaxRetries = 10;
        const int ArtPadding = 12;           // cells of surroundings kept around the level in the picture
        const float ArtDelaySeconds = 1f;    // let the run settle (spawn effects) before taking the picture

        static readonly MapPalette Palette = new MapPalette(
            ground: new Rgba(78, 92, 110),
            groundEdge: new Rgba(190, 202, 220),
            obstacle: new Rgba(128, 100, 72),
            obstacleEdge: new Rgba(214, 176, 120));

        static readonly GroundProbe Probe = ProbeNavMesh;

        WalkableFlood flood;
        CellClass[] classes;
        float floodStarted, nextPreview, nextRetry, artDue;
        int retries, probes;
        double busyMs;
        bool failed, radarLogged, artFailed;

        public BakedMap Map { get; private set; }

        public void Reset()
        {
            flood = null;
            classes = null;
            artDue = 0f;
            retries = 0;
            failed = radarLogged = artFailed = false;
            Map?.Destroy();
            Map = null;
        }

        /// <summary>Advances mapping within this frame's budget. Returns true when <see cref="Map"/> changed.</summary>
        public bool Tick(float now, NVec2 center, float centerY)
        {
            if (failed) return false;
            try
            {
                if (flood == null)
                {
                    flood = new WalkableFlood(GridSize, CellSize, center.X, center.Y);
                    floodStarted = now;
                    nextRetry = now + RetrySeconds;
                    probes = 0;
                    busyMs = 0;
                }

                // New ground (spawn, or a teleport into an unexplored area): explore from here.
                if (flood.StateAt(center.X, center.Y) == CellState.Unknown) flood.Seed(center.X, center.Y, centerY);

                if (!flood.HasWork)
                {
                    if (artDue > 0f && now >= artDue)
                    {
                        artDue = 0f;
                        return PaintArt();
                    }
                    if (flood.WalkableCount == 0) RetryOrGiveUp(now);
                    return false;
                }

                var sw = Stopwatch.StartNew();
                while (flood.HasWork && sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
                    probes += flood.Step(ProbesPerSlice, Probe);
                busyMs += sw.Elapsed.TotalMilliseconds;

                if (!flood.HasWork)
                {
                    classes = MapPainter.Classify(flood);
                    bool changed = Replace(MapPainter.Paint(flood, classes, Palette, Padding), FilterMode.Bilinear);
                    LogFinished(now);
                    artDue = Mod.Settings.Style.Value == MapStyle.Pixelated && !artFailed ? now + ArtDelaySeconds : 0f;
                    return changed;
                }
                if (now >= nextPreview)
                {
                    nextPreview = now + PreviewSeconds;
                    return Replace(MapPainter.Paint(flood, null, Palette, Padding), FilterMode.Bilinear);
                }
                return false;
            }
            catch (Exception e)
            {
                failed = true;
                LogOnce.Error("reading the NavMesh (showing radar mode)", e);
                return false;
            }
        }

        void RetryOrGiveUp(float now)
        {
            if (retries >= MaxRetries)
            {
                if (!radarLogged)
                {
                    radarLogged = true;
                    Mod.Log.LogWarning("No NavMesh ground found around the player; showing radar mode (dots without a layout).");
                }
                return;
            }
            if (now < nextRetry) return;
            retries++;
            flood = null; // next Tick starts a fresh grid around the player
        }

        /// <summary>Swaps the flat map for a pixel-art picture of the level. Falls back to the flat map on failure.</summary>
        bool PaintArt()
        {
            if (classes == null || !MapPainter.TryCrop(flood, ArtPadding, out var crop)) return false;
            try
            {
                var sw = Stopwatch.StartNew();
                var art = LevelSnapshot.Capture(
                    flood.OriginX + crop.X * CellSize, flood.OriginZ + crop.Y * CellSize,
                    crop.Width, crop.Height, CellSize, flood.MaxHeight);
                var painted = MapPainter.PaintArt(flood, classes, art, Palette, crop, Mod.Settings.HighlightObstacles.Value);
                bool changed = Replace(painted, FilterMode.Point);
                Mod.Log.LogInfo($"Captured level picture: {crop.Width} x {crop.Height} pixels in {sw.ElapsedMilliseconds} ms.");
                return changed;
            }
            catch (Exception e)
            {
                artFailed = true;
                LogOnce.Error("taking the level picture (keeping the flat map)", e);
                return false;
            }
        }

        bool Replace(PaintedMap painted, FilterMode filter)
        {
            if (painted == null) return false;
            var old = Map;
            Map = new BakedMap
            {
                Texture = SpriteFactory.ToTexture(painted.Image, filter, keepReadable: false),
                OriginX = painted.OriginX,
                OriginZ = painted.OriginZ,
                WorldWidth = painted.WorldWidth,
                WorldHeight = painted.WorldHeight,
            };
            old?.Destroy();
            return true;
        }

        void LogFinished(float now)
        {
            float w = (flood.MaxX - flood.MinX + 1) * CellSize, h = (flood.MaxY - flood.MinY + 1) * CellSize;
            double usPerProbe = probes > 0 ? busyMs * 1000.0 / probes : 0.0;
            Mod.Log.LogInfo($"Mapped level layout: {flood.WalkableCount} walkable cells over {w:0} x {h:0} world units; "
                + $"{probes} probes ({usPerProbe:0.0} us each), {busyMs:0} ms of work spread over {now - floodStarted:0.0} s.");
            if (flood.TouchedBorder)
                LogOnce.Warn("map-grid-border", $"The level is larger than the {GridSize * CellSize:0}-unit mapping grid; its far edges may be cut off.");
        }

        static bool ProbeNavMesh(float x, float z, float yHint, out float y)
        {
            y = yHint;
            if (!NavMesh.SamplePosition(new Vector3(x, yHint, z), out NavMeshHit hit, ProbeRange, NavMeshAllAreas)) return false;
            Vector3 p = hit.position;
            float dx = p.x - x, dz = p.z - z;
            if (dx * dx + dz * dz > MaxCellOffset * MaxCellOffset) return false;
            y = p.y;
            return true;
        }
    }
}
