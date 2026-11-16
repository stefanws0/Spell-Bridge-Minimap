using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// Keeps the minimap clear of the game's HUD (and other mods' panels) in its corner, e.g. the collected
    /// artifacts: finds visible UI pieces on screen-space canvases near the minimap and moves it past the ones
    /// that stay there (see <see cref="HudClearance"/>).
    /// </summary>
    internal sealed class HudAvoider
    {
        const float RefreshSeconds = 3f;       // re-list the UI pieces near the corner
        const float ScanSeconds = 0.25f;       // re-measure them
        const float MaxPieceShare = 0.4f;      // bigger than this share of the screen: a backdrop, not a panel
        const float MoveSpeed = 900f;          // canvas units per second
        const int MaxLogs = 20;

        readonly HudClearance clearance = new HudClearance(persistSeconds: 1f, lowerDelaySeconds: 1.5f, gap: 0f, maxLift: float.MaxValue);
        readonly List<Graphic> candidates = new List<Graphic>();
        readonly List<(long, ScreenBox)> boxes = new List<(long, ScreenBox)>();
        readonly Il2CppStructArray<Vector3> corners = new Il2CppStructArray<Vector3>(4);
        readonly HashSet<string> logged = new HashSet<string>();
        float nextRefresh, nextScan, target, current;
        int logs;

        public void Reset()
        {
            clearance.Reset();
            candidates.Clear();
            nextRefresh = nextScan = target = current = 0f;
        }

        /// <summary>
        /// How far (canvas units) to move the minimap away from its corner's edge this frame.
        /// <paramref name="home"/> is the minimap's spot in screen pixels without moving; <paramref name="own"/> is
        /// its canvas (skipped), <paramref name="scale"/> pixels per canvas unit, <paramref name="gap"/> the space
        /// to keep (canvas units), <paramref name="maxMove"/> the most it may move (canvas units).
        /// </summary>
        public float Tick(float now, float dt, ScreenBox home, bool bottomCorner, Canvas own, float scale, float gap, float maxMove)
        {
            try
            {
                if (now >= nextRefresh)
                {
                    nextRefresh = now + RefreshSeconds;
                    Refresh(home, own);
                }
                if (now >= nextScan)
                {
                    nextScan = now + ScanSeconds;
                    boxes.Clear();
                    foreach (var g in candidates)
                        if (Visible(g) && TryBox(g, out var box)) boxes.Add((g.Pointer.ToInt64(), box));
                    float px = clearance.Update(now, home, bottomCorner, boxes);
                    float wanted = px > 0f ? Math.Min(px / scale + gap, maxMove) : 0f;
                    if (wanted > target + 0.5f) LogCause(home, bottomCorner, wanted);
                    target = wanted;
                }
            }
            catch (Exception e)
            {
                LogOnce.Error("keeping the minimap clear of the game's HUD", e);
                target = 0f;
            }
            current = Mathf.MoveTowards(current, target, MoveSpeed * dt);
            return current;
        }

        /// <summary>Lists the UI pieces on other screen-space canvases that sit within reach of the minimap's corner.</summary>
        void Refresh(in ScreenBox home, Canvas own)
        {
            candidates.Clear();
            float reach = (home.YMax - home.YMin) * 1.5f;
            var near = new ScreenBox(home.XMin - reach, home.YMin - reach, home.XMax + reach, home.YMax + reach);
            var canvases = UObject.FindObjectsOfType<Canvas>();
            for (int c = 0; c < canvases.Length; c++)
            {
                var canvas = canvases[c];
                if (canvas == null || !canvas.isActiveAndEnabled || !canvas.isRootCanvas) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (own != null && canvas.Pointer == own.Pointer) continue;
                var graphics = canvas.GetComponentsInChildren<Graphic>(false);
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    if (g == null || !TryBox(g, out var box) || !box.Overlaps(near)) continue;
                    if (g.GetComponentInParent<ScreenPointer>() != null) continue; // off-screen pointers glide along the edges
                    candidates.Add(g);
                }
            }
        }

        static bool Visible(Graphic g)
        {
            if (g == null || !g.isActiveAndEnabled || g.color.a < 0.1f) return false;
            var cr = g.canvasRenderer;
            if (cr != null && cr.cull) return false;
            // Panels are often hidden by fading a CanvasGroup to 0 rather than switching them off.
            var groups = g.GetComponentsInParent<CanvasGroup>();
            float alpha = 1f;
            for (int i = 0; i < groups.Length; i++)
            {
                var cg = groups[i];
                if (cg == null || !cg.enabled) continue;
                alpha *= cg.alpha;
                if (cg.ignoreParentGroups) break;
            }
            return alpha >= 0.1f;
        }

        bool TryBox(Graphic g, out ScreenBox box)
        {
            box = default;
            var canvas = g.canvas;
            if (canvas == null) return false;
            g.rectTransform.GetWorldCorners(corners);
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = cam != null ? RectTransformUtility.WorldToScreenPoint(cam, corners[i]) : (Vector2)corners[i];
                xMin = Math.Min(xMin, p.x); xMax = Math.Max(xMax, p.x);
                yMin = Math.Min(yMin, p.y); yMax = Math.Max(yMax, p.y);
            }
            float w = xMax - xMin, h = yMax - yMin;
            if (w < 4f || h < 4f || w > Screen.width * MaxPieceShare || h > Screen.height * MaxPieceShare) return false;
            box = new ScreenBox(xMin, yMin, xMax, yMax);
            return true;
        }

        void LogCause(in ScreenBox home, bool bottomCorner, float units)
        {
            if (logs >= MaxLogs) return;
            var names = new List<string>();
            foreach (var g in candidates)
            {
                if (!Visible(g) || !TryBox(g, out var box) || !box.Overlaps(home)) continue;
                if (bottomCorner ? box.CenterY > home.CenterY : box.CenterY < home.CenterY) continue;
                var t = g.transform;
                string name = t.parent != null ? $"{t.parent.name}/{t.name}" : t.name;
                if (!names.Contains(name)) names.Add(name);
            }
            string key = string.Join(", ", names);
            if (names.Count == 0 || !logged.Add(key)) return;
            logs++;
            Mod.Log.LogInfo($"Moving the minimap {units:0} units {(bottomCorner ? "up" : "down")} to clear: {key}.");
        }
    }
}
