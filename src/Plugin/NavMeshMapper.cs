using System;
using System.Diagnostics;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.AI;

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
    /// outward within a small time budget each frame. The game build strips Unity's NavMesh triangle export
    /// (CalculateTriangulation); SamplePosition is the NavMesh query it does ship.
    ///
    /// Pixelated style: the moment mapping finishes (usually while the player is still dropping in, so before
    /// the landing splash), a top-down picture of the level is taken. Its colours are matched to the game
    /// camera's view once the player has landed, and only then is the picture shown.
    /// </summary>
    internal sealed class NavMeshMapper
    {
        const int GridSize = 768;            // cells per side
        const float CellSize = 1f;           // world units per cell
        const float ProbeRange = 3f;         // how far from the cell center (mostly vertically) to look for NavMesh
        const float MaxCellOffset = 0.6f;    // a hit further than this sideways belongs to another cell
        const float SeedSearchRange = 60f;   // finds the ground below a player who is still in the air
        const float GroundedTolerance = 1.5f;
        const int NavMeshAllAreas = -1;
        const double FrameBudgetMs = 2.0;
        const int ProbesPerSlice = 64;
        const int Padding = 2;
        const int ArtPadding = 12;           // cells of surroundings kept around the level in the picture
        const float PreviewSeconds = 1f;
        const float RetrySeconds = 2f;
        const int MaxRetries = 10;
        const int GameViewWidth = 480;
        const float Trim = 0.05f;
        const int MinObstacleCells = 8;      // smaller holes (pebbles, props) are drawn as ground

        // Colours follow the game's HUD: near-black outlines and a cream highlight.
        static readonly MapPalette Palette = new MapPalette(
            ground: new Rgba(58, 66, 74),
            groundEdge: new Rgba(253, 248, 206),
            obstacle: new Rgba(34, 37, 42),
            obstacleEdge: new Rgba(16, 16, 18));

        static readonly GroundProbe Probe = ProbeNavMesh;

        WalkableFlood flood;
        CellClass[] classes;
        PixelBuffer rawArt;
        CropRect artCrop;
        float floodStarted, nextPreview, nextRetry;
        int retries, probes;
        double busyMs;
        bool failed, radarLogged, artFailed, artShown;

        public BakedMap Map { get; private set; }

        public void Reset()
        {
            flood = null;
            classes = null;
            rawArt = null;
            retries = 0;
            failed = radarLogged = artFailed = artShown = false;
            Map?.Destroy();
            Map = null;
        }

        /// <summary>
        /// Starts or extends mapping from the NavMesh ground nearest to <paramref name="near"/> (the player,
        /// even mid-air, or a level spawn point). Cheap to call every frame: one probe.
        /// </summary>
        public void SeedNear(float now, Vector3 near)
        {
            if (failed) return;
            try
            {
                if (!NavMesh.SamplePosition(near, out NavMeshHit hit, SeedSearchRange, NavMeshAllAreas)) return;
                Vector3 g = hit.position;
                if (flood == null)
                {
                    flood = new WalkableFlood(GridSize, CellSize, g.x, g.z);
                    floodStarted = now;
                    nextRetry = now + RetrySeconds;
                    probes = 0;
                    busyMs = 0;
                }
                if (flood.StateAt(g.x, g.z) == CellState.Unknown) flood.Seed(g.x, g.z, g.y);
            }
            catch (Exception e)
            {
                failed = true;
                LogOnce.Error("reading the NavMesh (showing radar mode)", e);
            }
        }

        /// <summary>True when the point stands on mapped ground (not dropping in, not mid-jump).</summary>
        public bool IsOnGround(Vector3 p)
            => flood != null && flood.TryGroundHeight(p.x, p.z, out float y) && Math.Abs(p.y - y) <= GroundedTolerance;

        /// <summary>
        /// Advances mapping within this frame's budget; when <paramref name="viewSettled"/> (the player has
        /// landed and the game camera shows normal gameplay) shows the colour-matched level picture.
        /// Returns true when <see cref="Map"/> changed.
        /// </summary>
        public bool Tick(float now, bool viewSettled)
        {
            if (failed || flood == null) return false;
            try
            {
                if (!flood.HasWork)
                {
                    if (flood.WalkableCount == 0)
                    {
                        RetryOrGiveUp(now);
                        return false;
                    }
                    if (rawArt != null && !artShown && viewSettled)
                    {
                        artShown = true;
                        return ShowArt();
                    }
                    return false;
                }

                var sw = Stopwatch.StartNew();
                while (flood.HasWork && sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
                    probes += flood.Step(ProbesPerSlice, Probe);
                busyMs += sw.Elapsed.TotalMilliseconds;

                if (!flood.HasWork)
                {
                    classes = MapPainter.Classify(flood, MinObstacleCells);
                    bool changed = Replace(MapPainter.Paint(flood, classes, Palette, Padding), FilterMode.Bilinear);
                    LogFinished(now);
                    TakePicture();
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
            flood = null; // the next SeedNear starts a fresh grid
        }

        /// <summary>Takes the top-down picture now (kept hidden until its colours are matched).</summary>
        void TakePicture()
        {
            rawArt = null;
            artShown = false;
            if (artFailed || Mod.Settings.Style.Value != MapStyle.Pixelated) return;
            if (!MapPainter.TryCrop(flood, ArtPadding, out artCrop)) return;
            try
            {
                var sw = Stopwatch.StartNew();
                rawArt = LevelSnapshot.CaptureLevel(
                    flood.OriginX + artCrop.X * CellSize, flood.OriginZ + artCrop.Y * CellSize,
                    artCrop.Width, artCrop.Height, CellSize, flood.MaxHeight);
                Mod.Log.LogInfo($"Captured level picture: {artCrop.Width} x {artCrop.Height} pixels in {sw.ElapsedMilliseconds} ms.");
                DebugImages.Save("1-level-picture", rawArt);
            }
            catch (Exception e)
            {
                artFailed = true;
                LogOnce.Error("taking the level picture (keeping the flat map)", e);
            }
        }

        /// <summary>Matches the picture's colours to the game camera's view and swaps it in for the flat map.</summary>
        bool ShowArt()
        {
            PixelBuffer art = rawArt;
            try
            {
                var view = LevelSnapshot.CaptureGameView(GameViewWidth);
                DebugImages.Save("2-game-view", view);
                if (view != null)
                {
                    // A ring around the screen center, upper 70% only: skips the player in the middle, the
                    // vignette at the edges, and the skill bar and health bar along the bottom (row 0 = bottom).
                    float cx = view.Width * 0.5f, cy = view.Height * 0.5f;
                    float rMin = view.Height * 0.15f, rMax = view.Height * 0.45f, bottom = view.Height * 0.3f;
                    var game = ColorMatch.Measure(view, (x, y) =>
                    {
                        if (y < bottom) return false;
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy, d2 = dx * dx + dy * dy;
                        return d2 >= rMin * rMin && d2 <= rMax * rMax;
                    }, Trim);
                    int n = flood.Size;
                    var picture = ColorMatch.Measure(rawArt,
                        (x, y) => classes[(artCrop.Y + y) * n + artCrop.X + x] != CellClass.Outside, Trim);
                    if (game.Count > 100 && picture.Count > 100)
                    {
                        art = ColorMatch.Transfer(rawArt, picture, game);
                        Mod.Log.LogInfo($"Matched level picture colours. Picture: {picture}. Game view: {game}.");
                        DebugImages.Save("3-level-picture-matched", art);
                    }
                }
            }
            catch (Exception e) { LogOnce.Error("matching the level picture's colours (showing it unmatched)", e); }

            try
            {
                var painted = MapPainter.PaintArt(flood, classes, art, Palette, artCrop, Mod.Settings.HighlightObstacles.Value);
                DebugImages.Save("4-minimap", painted.Image);
                return Replace(painted, FilterMode.Point);
            }
            catch (Exception e)
            {
                artFailed = true;
                LogOnce.Error("showing the level picture (keeping the flat map)", e);
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
