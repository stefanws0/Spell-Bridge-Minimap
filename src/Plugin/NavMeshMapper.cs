using System;
using System.Diagnostics;
using System.Linq;
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
    /// Builds the minimap's picture of the level.
    ///
    /// Layout: flood-fills the NavMesh cell by cell (SamplePosition is the NavMesh query this build ships), with a
    /// physics ray per cell for rocks the NavMesh ignores; then renders every solid mesh alone for its exact
    /// footprint, finds the arena inside its walls, and sorts cells into ground, obstacle and outside.
    ///
    /// Look (pixelated style): a top-down picture of the ground layer (terrain, flat ground surfaces, decals)
    /// over a full top-down picture with effects hidden, brightness-matched to the game camera once the player
    /// has landed, then painted over with what the game camera actually shows, every few seconds, as the
    /// player explores.
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
        const int MinObstacleCells = 3;      // smaller holes (pebbles) are drawn as ground
        const int ArenaSealRadius = 2;       // seals wall gaps up to 4 cells when separating the arena
        const float MaxTintShift = 6f;       // the picture's tint is already close; only nudge it
        const int CalmRadius = 5;            // grass-calming filter: neighbourhood, strength, detail lift
        const float CalmStrength = 0.8f, CalmLift = 0.7f;
        const float SolidHeight = 1f;        // static scenery this far above the NavMesh blocks the cell
        const float RayStartHeight = 30f;
        const float MaxSolidShare = 0.6f;    // more cells blocked than this: something odd (a ceiling?), redo without physics
        const int PhysicsMask = ~(1 << 5);   // everything but UI; scenery colliders often sit on "Ignore Raycast"
        const float SolidCoverage = 0.5f;    // a cell at least this covered by a solid mesh is an obstacle
        const float PaintSeconds = 2.5f;     // how often the game view is painted onto the map
        const float PaintMinSamples = 3f;
        const float OccluderHeight = 4f;     // rays crossing an obstacle this close to the ground show its side
        const int DarkGround = 95;           // average ground brightness below which obstacles are drawn light

        static readonly MapPalette FlatPalette = new MapPalette(
            ground: new Rgba(58, 66, 74), groundEdge: new Rgba(253, 248, 206),
            obstacle: new Rgba(34, 37, 42), obstacleEdge: new Rgba(16, 16, 18));
        // Colours follow the game's HUD: near-black outlines and a cream highlight.
        static readonly MapPalette ArtPalette = new MapPalette(
            ground: default, groundEdge: new Rgba(253, 248, 206),
            obstacle: new Rgba(16, 16, 18), obstacleEdge: new Rgba(16, 16, 18));
        // On dark maps (the astral world) black obstacles vanish, so they are drawn as light stone instead.
        static readonly MapPalette DarkGroundPalette = new MapPalette(
            ground: default, groundEdge: new Rgba(253, 248, 206),
            obstacle: new Rgba(200, 196, 184), obstacleEdge: new Rgba(16, 16, 18));

        static readonly GroundProbe Probe = ProbeNavMesh;
        static bool usePhysics = true;
        static int solidCells;
        static readonly int[] solidLayers = new int[32];

        WalkableFlood flood;
        CellClass[] classes;
        SceneryScan scenery;
        PixelBuffer rawArt, baseArt;
        ViewPainter painter;
        CropRect artCrop;
        float floodStarted, nextPreview, nextRetry, nextPaint;
        int retries, probes;
        double busyMs;
        bool failed, radarLogged, artFailed, artShown, savedFinal;

        public BakedMap Map { get; private set; }

        public void Reset()
        {
            flood = null;
            classes = null;
            scenery = null;
            rawArt = baseArt = null;
            painter = null;
            retries = 0;
            failed = radarLogged = artFailed = artShown = savedFinal = false;
            usePhysics = true;
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
                    solidCells = 0;
                    Array.Clear(solidLayers, 0, solidLayers.Length);
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
        /// Advances mapping within this frame's budget. Once <paramref name="viewSettled"/> (the player has landed
        /// and the game camera shows normal gameplay), shows the level picture and keeps painting the game view
        /// onto it. Returns true when <see cref="Map"/> changed.
        /// </summary>
        public bool Tick(float now, bool viewSettled, Vector3 player)
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
                    if (rawArt == null || !viewSettled) return false;
                    if (!artShown)
                    {
                        artShown = true;
                        nextPaint = now + PaintSeconds;
                        return ShowArt(player);
                    }
                    if (painter != null && Mod.Settings.PaintFromGameView.Value && now >= nextPaint)
                    {
                        nextPaint = now + PaintSeconds;
                        return PaintGameView(player);
                    }
                    return false;
                }

                var sw = Stopwatch.StartNew();
                while (flood.HasWork && sw.Elapsed.TotalMilliseconds < FrameBudgetMs)
                    probes += flood.Step(ProbesPerSlice, Probe);
                busyMs += sw.Elapsed.TotalMilliseconds;

                if (!flood.HasWork)
                {
                    if (usePhysics && solidCells > (flood.WalkableCount + solidCells) * MaxSolidShare)
                    {
                        Mod.Log.LogWarning($"{solidCells} of {flood.WalkableCount + solidCells} ground cells are under solid scenery; "
                            + "that looks wrong (an invisible ceiling?), so the map is redone from the NavMesh alone.");
                        usePhysics = false;
                        flood = null; // the next SeedNear starts over
                        return false;
                    }
                    scenery = ScanScenery();
                    classes = ClassifyArena(SolidSceneryCells());
                    bool changed = Replace(MapPainter.Paint(flood, classes, FlatPalette, Padding), FilterMode.Bilinear);
                    LogFinished(now);
                    TakePicture();
                    return changed;
                }
                if (now >= nextPreview)
                {
                    nextPreview = now + PreviewSeconds;
                    return Replace(MapPainter.Paint(flood, null, FlatPalette, Padding), FilterMode.Bilinear);
                }
                return false;
            }
            catch (Exception e)
            {
                failed = true;
                LogOnce.Error("building the minimap (showing radar mode)", e);
                return false;
            }
        }

        // ---- layout -------------------------------------------------------------------------------------------

        SceneryScan ScanScenery()
        {
            if (!MapPainter.TryCrop(flood, ArtPadding, out var crop)) return null;
            try
            {
                float ox = flood.OriginX + crop.X * CellSize, oz = flood.OriginZ + crop.Y * CellSize;
                return SceneryScan.Run(ox, oz, ox + crop.Width * CellSize, oz + crop.Height * CellSize,
                    Mod.Settings.GroundKeywords.Value, Mod.Settings.SaveDebugImages.Value);
            }
            catch (Exception e)
            {
                LogOnce.Error("looking at the scenery", e);
                return null;
            }
        }

        /// <summary>Cells covered by solid meshes (rocks, walls, crystals), from one render of just those meshes.</summary>
        bool[] SolidSceneryCells()
        {
            if (scenery == null || scenery.Solid.Count == 0 || !MapPainter.TryCrop(flood, ArtPadding, out var crop)) return null;
            try
            {
                int layer = LevelSnapshot.FreeLayer();
                if (layer < 0)
                {
                    LogOnce.Warn("no-free-layer", "No unused layer to render scenery on; obstacles come from the NavMesh and physics only.");
                    return null;
                }
                float ox = flood.OriginX + crop.X * CellSize, oz = flood.OriginZ + crop.Y * CellSize;
                var cover = LevelSnapshot.OnLayer(scenery.Solid.Select(r => r.gameObject), layer,
                    () => LevelSnapshot.CaptureCoverage(ox, oz, crop.Width, crop.Height, CellSize, flood.MaxHeight, layer));

                int n = flood.Size, cells = 0;
                var solid = new bool[n * n];
                var img = new PixelBuffer(crop.Width, crop.Height);
                for (int y = 0; y < crop.Height; y++)
                    for (int x = 0; x < crop.Width; x++)
                    {
                        if (cover[y * crop.Width + x] < SolidCoverage) continue;
                        solid[(crop.Y + y) * n + crop.X + x] = true;
                        img.SetPixel(x, y, Rgba.White);
                        cells++;
                    }
                DebugImages.Save("0b-solid-scenery", img);
                Mod.Log.LogInfo($"Solid scenery covers {cells} cells.");
                return solid;
            }
            catch (Exception e)
            {
                LogOnce.Error("finding solid scenery", e);
                return null;
            }
        }

        /// <summary>Ground / obstacle / outside, with solid scenery as obstacles and NavMesh beyond the arena's walls as outside.</summary>
        CellClass[] ClassifyArena(bool[] solid)
        {
            bool[] band = null;
            if (Mod.Settings.TrimOutsideArena.Value)
            {
                band = ArenaFinder.FindOuterBand(flood, ArenaSealRadius, out float kept, solid);
                Mod.Log.LogInfo(band != null
                    ? $"Arena walls found: keeping {kept:P0} of the NavMesh, the rest is beyond the walls."
                    : $"No enclosing arena wall found (arena would keep {kept:P0}); showing all of the NavMesh.");
            }
            bool[] excluded = band;
            if (solid != null)
            {
                excluded = (bool[])solid.Clone();
                if (band != null)
                    for (int i = 0; i < excluded.Length; i++) excluded[i] |= band[i];
            }
            var cls = MapPainter.Classify(flood, MinObstacleCells, excluded);
            SaveCellMap(cls, band);
            return cls;
        }

        /// <summary>Debug picture: grey = arena ground, brown = obstacle, blue = NavMesh beyond the walls.</summary>
        void SaveCellMap(CellClass[] cls, bool[] band)
        {
            if (!Mod.Settings.SaveDebugImages.Value || !MapPainter.TryCrop(flood, Padding, out var crop)) return;
            var img = new PixelBuffer(crop.Width, crop.Height);
            int n = flood.Size;
            for (int y = 0; y < crop.Height; y++)
                for (int x = 0; x < crop.Width; x++)
                {
                    int i = (crop.Y + y) * n + crop.X + x;
                    Rgba c = band != null && band[i] ? new Rgba(60, 110, 200)
                           : cls[i] == CellClass.Walkable ? new Rgba(150, 150, 150)
                           : cls[i] == CellClass.Obstacle ? new Rgba(150, 90, 40)
                           : Rgba.Clear;
                    img.SetPixel(x, y, c);
                }
            DebugImages.Save("0-cells", img);
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

        // ---- look ---------------------------------------------------------------------------------------------

        /// <summary>Takes the top-down pictures now (kept hidden until the player has landed).</summary>
        void TakePicture()
        {
            rawArt = baseArt = null;
            painter = null;
            artShown = savedFinal = false;
            if (artFailed || Mod.Settings.Style.Value != MapStyle.Pixelated) return;
            if (!MapPainter.TryCrop(flood, ArtPadding, out artCrop)) return;
            try
            {
                var sw = Stopwatch.StartNew();
                float ox = flood.OriginX + artCrop.X * CellSize, oz = flood.OriginZ + artCrop.Y * CellSize;
                var full = LevelSnapshot.CaptureLevel(ox, oz, artCrop.Width, artCrop.Height, CellSize, flood.MaxHeight, scenery?.Effects);
                DebugImages.Save("1-level-picture", full);
                var ground = GroundPicture(ox, oz, full);
                rawArt = ground != null ? ArtFilter.Overlay(full, ground) : full;
                Mod.Log.LogInfo($"Captured level picture: {artCrop.Width} x {artCrop.Height} pixels"
                    + $"{(ground != null ? ", ground layer on top" : "")} in {sw.ElapsedMilliseconds} ms.");
            }
            catch (Exception e)
            {
                artFailed = true;
                LogOnce.Error("taking the level picture (keeping the flat map)", e);
            }
        }

        /// <summary>
        /// The ground on its own (terrain, flat ground surfaces, decals): no grass tufts, flowers, shadows from rocks,
        /// spawn effects or tornadoes. Null if there is none, or if it came out unlit.
        /// </summary>
        PixelBuffer GroundPicture(float ox, float oz, PixelBuffer full)
        {
            if (scenery == null || scenery.Ground.Count == 0) return null;
            try
            {
                int layer = LevelSnapshot.FreeLayer();
                if (layer < 0) return null;
                var pic = LevelSnapshot.OnLayer(scenery.Ground, layer,
                    () => LevelSnapshot.CaptureLayerPicture(ox, oz, artCrop.Width, artCrop.Height, CellSize, flood.MaxHeight, layer));
                DebugImages.Save("1a-ground-picture", pic);

                // If the lights skip that layer, the ground comes out dark: then keep the full picture.
                double groundLuma = 0, fullLuma = 0;
                int count = 0;
                for (int y = 0; y < pic.Height; y++)
                    for (int x = 0; x < pic.Width; x++)
                    {
                        Rgba g = pic.GetPixel(x, y);
                        if (g.A == 0) continue;
                        Rgba f = full.GetPixel(x, y);
                        groundLuma += g.R * 0.299 + g.G * 0.587 + g.B * 0.114;
                        fullLuma += f.R * 0.299 + f.G * 0.587 + f.B * 0.114;
                        count++;
                    }
                if (count == 0) return null;
                if (groundLuma < fullLuma * 0.45)
                {
                    Mod.Log.LogWarning("The ground-only picture came out much darker than the full one (unlit?); using the full picture.");
                    return null;
                }
                return pic;
            }
            catch (Exception e)
            {
                LogOnce.Error("taking the ground picture", e);
                return null;
            }
        }

        /// <summary>Matches the picture's brightness to the game camera's view, starts painting the game view on it, and shows it.</summary>
        bool ShowArt(Vector3 player)
        {
            baseArt = rawArt;
            PixelBuffer view = null;
            System.Numerics.Matrix4x4 clipToWorld = default;
            try
            {
                view = LevelSnapshot.CaptureGameView(GameViewWidth, out clipToWorld);
                DebugImages.Save("2-game-view", view);
                if (view != null)
                {
                    var game = ColorMatch.Measure(view, Paintable(view), Trim);
                    int n = flood.Size;
                    var picture = ColorMatch.Measure(rawArt,
                        (x, y) => classes[(artCrop.Y + y) * n + artCrop.X + x] != CellClass.Outside, Trim);
                    if (game.Count > 100 && picture.Count > 100)
                    {
                        baseArt = ColorMatch.Transfer(rawArt, picture, game, MaxTintShift);
                        Mod.Log.LogInfo($"Matched level picture colours. Picture: {picture}. Game view: {game}.");
                    }
                }
            }
            catch (Exception e) { LogOnce.Error("matching the level picture's colours (showing it unmatched)", e); }

            painter = new ViewPainter(artCrop.Width, artCrop.Height,
                flood.OriginX + artCrop.X * CellSize, flood.OriginZ + artCrop.Y * CellSize, CellSize);
            if (view != null && Mod.Settings.PaintFromGameView.Value) PaintInto(view, clipToWorld, player);
            return Recompose();
        }

        bool PaintGameView(Vector3 player)
        {
            try
            {
                var view = LevelSnapshot.CaptureGameView(GameViewWidth, out var clipToWorld);
                if (view == null) return false;
                PaintInto(view, clipToWorld, player);
                return Recompose();
            }
            catch (Exception e)
            {
                painter = null; // stop trying; the picture stays as it is
                LogOnce.Error("painting the game view onto the minimap", e);
                return false;
            }
        }

        void PaintInto(PixelBuffer view, System.Numerics.Matrix4x4 clipToWorld, Vector3 player)
        {
            float groundY = flood.TryGroundHeight(player.x, player.z, out float y) ? y : player.y;
            int n = flood.Size;
            painter.AddView(view, clipToWorld, groundY, Paintable(view),
                (cx, cy) => classes[(artCrop.Y + cy) * n + artCrop.X + cx] != CellClass.Walkable, OccluderHeight);
        }

        /// <summary>
        /// Pixels of a game-view picture worth using: not near the edges (vignette), not around the player in the
        /// middle (their own body and spells), and not vivid (spell effects, flowers, enemies).
        /// </summary>
        static Func<int, int, bool> Paintable(PixelBuffer view)
        {
            float cx = view.Width * 0.5f, cy = view.Height * 0.5f, rMin = view.Height * 0.25f;
            float mx = view.Width * 0.04f, my = view.Height * 0.04f;
            return (x, y) =>
            {
                if (x < mx || x > view.Width - mx || y < my || y > view.Height - my) return false;
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy < rMin * rMin) return false;
                Rgba c = view.GetPixel(x, y);
                int hi = Math.Max(c.R, Math.Max(c.G, c.B)), lo = Math.Min(c.R, Math.Min(c.G, c.B));
                return hi - lo <= 110 && hi <= 235;
            };
        }

        /// <summary>Lays the painted game view over the base picture, calms the grass, and draws the minimap.</summary>
        bool Recompose()
        {
            try
            {
                var art = baseArt;
                if (painter != null) art = ArtFilter.Overlay(art, painter.Result(PaintMinSamples));
                art = ArtFilter.CalmGrass(art, CalmRadius, CalmStrength, CalmLift);
                var palette = GroundIsDark(art) ? DarkGroundPalette : ArtPalette;
                var painted = MapPainter.PaintArt(flood, classes, art, palette, artCrop, Mod.Settings.HighlightObstacles.Value);
                if (!savedFinal)
                {
                    savedFinal = true;
                    DebugImages.Save("3-level-picture-matched", art);
                    DebugImages.Save("4-minimap", painted.Image);
                }
                return Replace(painted, FilterMode.Point);
            }
            catch (Exception e)
            {
                artFailed = true;
                LogOnce.Error("showing the level picture (keeping the flat map)", e);
                return false;
            }
        }

        bool GroundIsDark(PixelBuffer art)
        {
            int n = flood.Size, count = 0;
            double luma = 0;
            for (int y = 0; y < art.Height; y++)
                for (int x = 0; x < art.Width; x++)
                {
                    if (classes[(artCrop.Y + y) * n + artCrop.X + x] != CellClass.Walkable) continue;
                    Rgba c = art.GetPixel(x, y);
                    luma += c.R * 0.299 + c.G * 0.587 + c.B * 0.114;
                    count++;
                }
            return count > 0 && luma / count < DarkGround;
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
            string layers = string.Join(", ", solidLayers.Select((count, layer) => (count, layer)).Where(t => t.count > 0)
                .OrderByDescending(t => t.count).Select(t => $"layer {t.layer}: {t.count}"));
            Mod.Log.LogInfo($"Mapped level layout: {flood.WalkableCount} walkable cells over {w:0} x {h:0} world units; "
                + $"{solidCells} NavMesh cells blocked by physics{(usePhysics ? "" : " (physics check off)")}"
                + $"{(layers.Length > 0 ? $" ({layers})" : "")}; "
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
            if (usePhysics && UnderSolidScenery(x, z, y, out int layer))
            {
                solidCells++;
                solidLayers[layer]++;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Not every rock or wall is cut out of the NavMesh. A ray straight down that hits static scenery more
        /// than <see cref="SolidHeight"/> above the ground means the cell is blocked. Anything with a rigidbody
        /// (players, enemies, projectiles) is ignored.
        /// </summary>
        static bool UnderSolidScenery(float x, float z, float ground, out int layer)
        {
            layer = 0;
            if (!Physics.Raycast(new Vector3(x, ground + RayStartHeight, z), Vector3.down, out RaycastHit hit,
                    RayStartHeight - SolidHeight, PhysicsMask, QueryTriggerInteraction.Ignore))
                return false;
            var col = hit.collider;
            if (col == null || col.attachedRigidbody != null) return false;
            layer = col.gameObject.layer;
            return true;
        }
    }
}
