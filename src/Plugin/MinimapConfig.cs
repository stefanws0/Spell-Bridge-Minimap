using BepInEx.Configuration;

namespace SpellBrigadeMinimap
{
    internal enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    internal sealed class MinimapConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<string> ToggleKey, ZoomInKey, ZoomOutKey;
        public readonly ConfigEntry<ScreenCorner> Corner;
        public readonly ConfigEntry<float> Size, Margin, Opacity;
        public readonly ConfigEntry<float> Radius, MinRadius, MaxRadius;
        public readonly ConfigEntry<bool> RotateWithCamera;
        public readonly ConfigEntry<bool> ShowEnemies, ShowObjectives, ShowPickups, ShowTeammates, ShowMoreEvents;

        public MinimapConfig(ConfigFile f)
        {
            Enabled = f.Bind("General", "Enabled", true, "Show the minimap during runs.");
            ToggleKey = f.Bind("General", "ToggleKey", "M", "Key that hides or shows the minimap (Unity Input System key name, e.g. M, Tab, F6).");
            ZoomInKey = f.Bind("General", "ZoomInKey", "Equals", "Key that zooms in.");
            ZoomOutKey = f.Bind("General", "ZoomOutKey", "Minus", "Key that zooms out.");

            Corner = f.Bind("Layout", "Corner", ScreenCorner.BottomRight, "Screen corner for the minimap.");
            Size = f.Bind("Layout", "Size", 220f, new ConfigDescription("Side length in pixels at 1080p.", new AcceptableValueRange<float>(100f, 600f)));
            Margin = f.Bind("Layout", "Margin", 16f, new ConfigDescription("Gap from the screen edges in pixels at 1080p.", new AcceptableValueRange<float>(0f, 200f)));
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
        }
    }
}
