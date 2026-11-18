using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// The font most of the game's on-screen text uses (TextMeshPro), so the minimap's numbers match the HUD.
    /// Looked for every 2 s until found; until then the minimap keeps its own pixel font.
    /// </summary>
    internal static class GameFont
    {
        const float RetrySeconds = 2f, FailRetrySeconds = 30f;

        static TMP_FontAsset font;
        static float nextTry;

        public static TMP_FontAsset Font => font != null ? font : null;

        public static bool TryFind(float now)
        {
            if (font != null) return true;
            if (now < nextTry) return false;
            nextTry = now + RetrySeconds;
            try
            {
                var texts = UObject.FindObjectsOfType<TextMeshProUGUI>();
                var counts = new Dictionary<IntPtr, (TMP_FontAsset font, int n)>();
                int total = 0;
                for (int i = 0; i < texts.Length; i++)
                {
                    var t = texts[i];
                    if (t == null || !t.isActiveAndEnabled) continue;
                    var f = t.font;
                    if (f == null) continue;
                    total++;
                    counts.TryGetValue(f.Pointer, out var entry);
                    counts[f.Pointer] = (f, entry.n + 1);
                }
                if (counts.Count == 0) return false;
                var ranked = counts.Values.OrderByDescending(e => e.n).ToList();
                font = ranked[0].font;
                string others = string.Join(", ", ranked.Skip(1).Take(4).Select(e => $"'{e.font.name}' {e.n}"));
                Mod.Log.LogInfo($"Game font for the minimap's numbers: '{font.name}' ({ranked[0].n} of {total} texts on screen)" +
                    (others.Length > 0 ? $"; also {others}." : "."));
                return true;
            }
            catch (Exception e)
            {
                nextTry = now + FailRetrySeconds;
                LogOnce.Error("finding the game's font", e);
                return false;
            }
        }
    }
}
