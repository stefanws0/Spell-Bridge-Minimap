using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// The game's own objective icons (its screen-edge pointer icons, <c>ui_ping_objective_*</c>), as white copies
    /// with a dark edge that the minimap tints blue (see <see cref="IconRecolor"/>). Icons are picked up from the
    /// game's pointers as they appear and, for one no pointer has shown yet, from the sprites the game has loaded
    /// (looked through at most every few seconds, a few times per level). Null until found; callers then fall
    /// back to the minimap's own drawn icon.
    /// </summary>
    internal static class GameIcons
    {
        public const string Collect = "ui_ping_objective_collect";
        public const string Order = "ui_ping_objective_order";
        public const string Herding = "ui_ping_objective_herding";
        public const string HerdingZone = "ui_ping_objective_herding_zone";
        public const string Push = "ui_ping_objective_push";
        public const string PushZone = "ui_ping_objective_push_zone";
        public const string Petrified = "ui_ping_objective_petrifiedenemies";
        public const string Hunt = "ui_ping_objective_hunttarget";
        public const string Capture = "ui_ping_objective_capture";
        public const string Clean = "ui_ping_objective_clean";
        public const string Sacrifice = "ui_ping_objective_sacrifice";

        const string Prefix = "ui_ping_objective_";
        const float SweepSeconds = 5f;
        const int MaxSweepsPerLevel = 4;

        static readonly Dictionary<string, Sprite> originals = new Dictionary<string, Sprite>();
        static readonly HashSet<IntPtr> offered = new HashSet<IntPtr>();
        static readonly Dictionary<IntPtr, string> names = new Dictionary<IntPtr, string>();
        static float nextSweep;
        static int sweeps;

        /// <summary>The piece icon of each kind (indexed by <see cref="PieceKind"/>).</summary>
        public static string PieceName(PieceKind kind) => kind switch
        {
            PieceKind.Mushroom => Collect,
            PieceKind.Crate => Push,
            PieceKind.Herd => Herding,
            PieceKind.Hunt => Hunt,
            _ => Petrified,
        };

        public static void Reset()
        {
            originals.Clear();
            offered.Clear();
            names.Clear();
            nextSweep = 0f;
            sweeps = 0;
        }

        /// <summary>Remembers a game pointer's icon by name (cheap after the first time per sprite).</summary>
        public static void Offer(Sprite icon)
        {
            if (icon == null) return;
            try
            {
                if (!offered.Add(icon.Pointer)) return;
                if (offered.Count > 512) offered.Clear();
                string name = icon.name;
                if (name != null && name.StartsWith(Prefix, StringComparison.Ordinal) && !originals.ContainsKey(name))
                    originals[name] = icon;
            }
            catch (Exception e) { LogOnce.Error("reading a game icon's name", e); }
        }

        /// <summary>The white copy of the game icon called <paramref name="name"/>, or null if it isn't known (yet).</summary>
        public static Sprite White(string name)
        {
            var original = Original(name);
            return original != null ? IconRecolor.White(original, null) : null;
        }

        /// <summary>The statue icon: the game's petrified-enemy icon in grey with blue details (draw it white), or null.</summary>
        public static Sprite Statue()
        {
            var original = Original(Petrified);
            return original != null ? IconRecolor.Statue(original) : null;
        }

        /// <summary>The name of a game icon (cached, so it's cheap every frame), or null.</summary>
        public static string NameOf(Sprite icon)
        {
            if (icon == null) return null;
            if (names.TryGetValue(icon.Pointer, out var n)) return n;
            try { n = icon.name; }
            catch { n = null; }
            if (names.Count > 512) names.Clear();
            names[icon.Pointer] = n;
            return n;
        }

        static Sprite Original(string name)
        {
            if (originals.TryGetValue(name, out var original) && original != null) return original;
            Sweep();
            return originals.TryGetValue(name, out original) ? original : null;
        }

        static void Sweep()
        {
            float now = Time.unscaledTime;
            if (sweeps >= MaxSweepsPerLevel || now < nextSweep) return;
            nextSweep = now + SweepSeconds;
            sweeps++;
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Sprite>();
                int found = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    var s = all[i];
                    if (s == null) continue;
                    string name = s.name;
                    if (name == null || !name.StartsWith(Prefix, StringComparison.Ordinal) || originals.ContainsKey(name)) continue;
                    originals[name] = s;
                    found++;
                }
                Mod.Log.LogInfo($"Game objective icons: {originals.Count} known after looking through {all.Length} loaded sprites ({found} new).");
            }
            catch (Exception e) { LogOnce.Error("looking for the game's objective icons", e); }
        }
    }
}
