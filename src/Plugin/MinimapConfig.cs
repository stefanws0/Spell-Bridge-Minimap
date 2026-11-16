using BepInEx.Configuration;

namespace SpellBrigadeMinimap
{
    internal enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }
    internal enum MapShape { Round, Square }
    internal enum MapStyle { Pixelated, Flat }

    internal sealed class MinimapConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<string> ToggleKey, ZoomInKey, ZoomOutKey;
        public readonly ConfigEntry<ScreenCorner> Corner;
        public readonly ConfigEntry<MapShape> Shape;
        public readonly ConfigEntry<MapStyle> Style;
        public readonly ConfigEntry<bool> HighlightObstacles, TrimOutsideArena, PaintFromGameView, LiftDarkGround;
        public readonly ConfigEntry<string> ObstacleKeywords, TintKeywords;
        public readonly ConfigEntry<float> Size, Margin, BottomOffset, Opacity;
        public readonly ConfigEntry<float> Radius, MinRadius, MaxRadius;
        public readonly ConfigEntry<bool> RotateWithCamera;
        public readonly ConfigEntry<bool> ShowEnemies, ShowObjectives, ShowPickups, ShowTeammates, ShowMoreEvents, ShowMapFeatures;
        public readonly ConfigEntry<bool> HideUndiscoveredObjectives, SaveDebugImages;

        public MinimapConfig(ConfigFile f)
        {
            Enabled = f.Bind("General", "Enabled", true, "Show the minimap during runs.");
            ToggleKey = f.Bind("General", "ToggleKey", "M", "Key that hides or shows the minimap (Unity Input System key name, e.g. M, Tab, F6).");
            ZoomInKey = f.Bind("General", "ZoomInKey", "Equals", "Key that zooms in.");
            ZoomOutKey = f.Bind("General", "ZoomOutKey", "Minus", "Key that zooms out.");

            Corner = f.Bind("Layout", "Corner", ScreenCorner.BottomRight, "Screen corner for the minimap.");
            Shape = f.Bind("Layout", "Shape", MapShape.Round, "Round or square minimap.");
            Style = f.Bind("Look", "Style", MapStyle.Pixelated, "Pixelated: a pixel-art picture of the level itself, taken at the start of each run. Flat: plain colours for ground and obstacles.");
            HighlightObstacles = f.Bind("Look", "HighlightObstacles", true, "Pixelated style: draw a rim around obstacles (rocks, trees, walls) so they stand out.");
            TrimOutsideArena = f.Bind("Look", "TrimOutsideArena", true, "Hide the ground beyond the arena's walls (where enemies come from). Turn off if a map loses parts it shouldn't.");
            PaintFromGameView = f.Bind("Look", "PaintFromGameView", false,
                "Experimental: recolour the minimap every few seconds with what the game camera shows. Off by default: the map keeps changing while you play.");
            ObstacleKeywords = f.Bind("Look", "ObstacleKeywords", "rock,boulder,border,wall,cliff,pillar,stone,volcano,vulcano",
                "Scenery at least 1 unit tall whose material, mesh or object name contains one of these words is drawn as an obstacle (things you can't walk through), even if the game keeps its collider elsewhere. Pebbles, grass, bushes, flowers and trees never count. Comma-separated.");
            LiftDarkGround = f.Bind("Look", "LiftDarkGround", true,
                "Pixelated style: brighten the darkest patches of ground toward the map's lighter ground, so only obstacles are near-black. Helps on the fire and astral maps.");
            TintKeywords = f.Bind("Look", "TintKeywords", "deathbush:C8343C",
                "Pixelated style: scenery whose material, mesh or object name contains the word is coloured on the map, as word:RRGGBB. Comma-separated (e.g. deathbush:C8343C).");
            Size = f.Bind("Layout", "Size", 220f, new ConfigDescription("Side length in pixels at 1080p.", new AcceptableValueRange<float>(100f, 600f)));
            Margin = f.Bind("Layout", "Margin", 16f, new ConfigDescription("Gap from the screen edges in pixels at 1080p.", new AcceptableValueRange<float>(0f, 200f)));
            BottomOffset = f.Bind("Layout", "BottomOffset", 30f, new ConfigDescription("Extra gap below the minimap in a bottom corner, so it sits above the game's text bar (pixels at 1080p).", new AcceptableValueRange<float>(0f, 400f)));
            Opacity = f.Bind("Layout", "Opacity", 0.85f, new ConfigDescription("Opacity of the whole minimap.", new AcceptableValueRange<float>(0.2f, 1f)));

            Radius = f.Bind("View", "Radius", 40f, new ConfigDescription("World units shown from the center to the edge when a run starts.", new AcceptableValueRange<float>(5f, 500f)));
            MinRadius = f.Bind("View", "MinRadius", 15f, new ConfigDescription("Closest zoom (world units).", new AcceptableValueRange<float>(5f, 500f)));
            MaxRadius = f.Bind("View", "MaxRadius", 120f, new ConfigDescription("Farthest zoom (world units).", new AcceptableValueRange<float>(5f, 1000f)));
            RotateWithCamera = f.Bind("View", "RotateWithCamera", true, "Turn the map so up on the minimap is up on screen. Off = world-fixed.");

            ShowEnemies = f.Bind("Layers", "ShowEnemies", true, "Enemy dots (elites larger and orange).");
            ShowObjectives = f.Bind("Layers", "ShowObjectives", true, "Objectives, the boss, downed teammates and danger pings.");
            ShowPickups = f.Bind("Layers", "ShowPickups", true, "Artifact, health and magnet pickups the game points at.");
            ShowTeammates = f.Bind("Layers", "ShowTeammates", true, "Other players.");
            ShowMoreEvents = f.Bind("Layers", "ShowMoreEvents", true, "MoreEvents targets and zones (only when MoreEvents is installed).");
            ShowMapFeatures = f.Bind("Layers", "ShowMapFeatures", true, "Warning signs on traps (the ice map's geysers) and teleport portals (bright when open).");
            HideUndiscoveredObjectives = f.Bind("Layers", "HideUndiscoveredObjectives", true, "Only show an objective once someone in the party has had it on screen. Discoveries are shared with other players who run this mod, through the host.");

            SaveDebugImages = f.Bind("Debug", "SaveDebugImages", false, "Save the level picture, the game-view colour sample and the finished minimap as PNGs in BepInEx/minimap-debug each run.");
        }
    }
}
