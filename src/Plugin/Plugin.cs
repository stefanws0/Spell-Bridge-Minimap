using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace SpellBrigadeMinimap
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "SpellBrigadeMinimap";
        public const string Name = "Minimap";
        public const string Version = "1.0.0";

        public override void Load()
        {
            Mod.Log = Log;
            Mod.Settings = new MinimapConfig(Config);
            AddComponent<MinimapBehaviour>();
            Log.LogInfo($"{Name} {Version} loaded");
        }
    }
}
