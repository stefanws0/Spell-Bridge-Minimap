using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace SpellBrigadeMinimap
{
    /// <summary>Shared state set once by <see cref="Plugin.Load"/>.</summary>
    internal static class Mod
    {
        internal static ManualLogSource Log;
        internal static MinimapConfig Settings;
    }

    /// <summary>Logs each distinct problem once, so a per-frame failure can't flood the log.</summary>
    internal static class LogOnce
    {
        static readonly HashSet<string> seen = new HashSet<string>();

        public static void Warn(string key, string message)
        {
            if (Mod.Log == null || !seen.Add(key)) return;
            Mod.Log.LogWarning(message);
        }

        public static void Error(string where, Exception e)
        {
            string key = where + ":" + e.GetType().FullName + ":" + e.Message;
            if (Mod.Log == null || !seen.Add(key)) return;
            Mod.Log.LogError($"{where}: {e}");
        }
    }
}
