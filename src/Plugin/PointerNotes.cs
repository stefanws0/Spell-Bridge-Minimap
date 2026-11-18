using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// Logs what the game's screen-edge pointers do, so the minimap's reading of them can be checked in game: the
    /// pointer prefabs and objective pointer delays once a run, the first pointer of each kind and target name
    /// (so a wave of identical enemies logs one line), and the first two times each of those starts or stops
    /// showing. Capped per run so it can't flood the log.
    /// </summary>
    internal sealed class PointerNotes
    {
        const int MaxLinesPerRun = 80;

        readonly Dictionary<IntPtr, Seen> seen = new Dictionary<IntPtr, Seen>();
        readonly HashSet<string> labelsLogged = new HashSet<string>();
        readonly Dictionary<string, int> changesLogged = new Dictionary<string, int>();
        const int ChangesPerLabel = 2;
        int lines;
        bool setupLogged, animatorBroken;

        sealed class Seen
        {
            public bool Shown;
            public float Since;
            public string Label;
        }

        public void Reset()
        {
            seen.Clear();
            labelsLogged.Clear();
            changesLogged.Clear();
            lines = 0;
            setupLogged = false;
        }

        /// <summary>One pointer in this scan: <paramref name="role"/> says what the minimap does with it.</summary>
        public void Observe(Transform target, ScreenPointer sp, bool shown, string role, float now)
        {
            if (lines >= MaxLinesPerRun) return;
            try
            {
                IntPtr key = sp.Pointer;
                if (!seen.TryGetValue(key, out var s))
                {
                    Vector3 p = target.position;
                    s = new Seen { Shown = shown, Since = now, Label = $"{sp.pointerType} pointer at '{target.name}'" };
                    seen[key] = s;
                    if (seen.Count > 2048) seen.Clear();
                    if (!labelsLogged.Add(s.Label + "|" + role)) return;
                    Line($"{s.Label} ({p.x:0},{p.z:0}): {role}; off screen {sp.offScreenBehaviour}, on screen {sp.onScreenBehaviour}; " +
                         $"{(shown ? "showing" : "not showing")}{AnimatorNote(sp)}.");
                    return;
                }
                if (s.Shown == shown) return;
                changesLogged.TryGetValue(s.Label, out int n);
                if (n < ChangesPerLabel)
                {
                    changesLogged[s.Label] = n + 1;
                    Line($"{s.Label}: {(shown ? "showing" : "hidden")} after {now - s.Since:0.0} s{AnimatorNote(sp)}.");
                }
                s.Shown = shown;
                s.Since = now;
            }
            catch (Exception e) { LogOnce.Error("noting a screen pointer", e); }
        }

        /// <summary>The pointer prefabs (type and when each shows) and the objective pointer delays, once a run.</summary>
        public void Setup(ScreenPointerManager spm)
        {
            if (setupLogged || spm == null) return;
            setupLogged = true;
            try
            {
                Prefab("objective", spm.objectiveScreenPointerPrefab);
                Prefab("objective component", spm.objectiveComponentScreenPointerPrefab);
                Prefab("objective component hint", spm.objectiveComponentScreenHintPointerPrefab);
                Prefab("boss", spm.bossScreenPointerPrefab);
                Prefab("downed player", spm.defeatedPlayerScreenPointerPrefab);
                Prefab("ping location", spm.pingLocationScreenPointerPrefab);
                Prefab("ping danger", spm.pingDangerScreenPointerPrefab);
            }
            catch (Exception e) { LogOnce.Error("reading the screen pointer prefabs", e); }
            try
            {
                var configs = Resources.FindObjectsOfTypeAll<ObjectivePointerTimingConfiguration>();
                foreach (var c in configs)
                {
                    if (c == null) continue;
                    Mod.Log.LogInfo($"Objective pointer delays: {c.SingleplayerShowAfterDelay:0.#} s alone, {c.MultiplayerShowAfterDelay:0.#} s in a party " +
                        $"({c.MultiplayerShowAfterDelayForFastObjectiveDirectionPointerTrait:0.#} s with the fast-pointer trait); pieces: " +
                        $"herding {Timing(c.HerdingObjectiveComponentPointerTiming)}, push {Timing(c.PushObjectiveComponentPointerTiming)}, " +
                        $"stones {Timing(c.PetrifiedEnemiesObjectiveComponentPointerTiming)}.");
                    break;
                }
            }
            catch (Exception e) { LogOnce.Error("reading the objective pointer delays", e); }
        }

        static string Timing(ObjectiveComponentPointerTiming t) =>
            t == null ? "?" : $"{t.ShowObjectiveComponentDelay:0.#} s ({t.ShowObjectiveComponentDelayFast:0.#} s fast)";

        static void Prefab(string name, GameObject prefab)
        {
            var sp = prefab != null ? prefab.GetComponentInChildren<ScreenPointer>(true) : null;
            if (sp == null)
            {
                Mod.Log.LogInfo($"Pointer prefab '{name}': none.");
                return;
            }
            Mod.Log.LogInfo($"Pointer prefab '{name}': {sp.pointerType}; off screen {sp.offScreenBehaviour}, on screen {sp.onScreenBehaviour}, " +
                $"highlighted off screen {sp.highlightOffScreenBehaviour}, on screen {sp.highlightOnScreenBehaviour}.");
        }

        /// <summary>The pointer animator's own "is shown" flag, as a cross-check on <c>wasShown</c>.</summary>
        string AnimatorNote(ScreenPointer sp)
        {
            if (animatorBroken) return "";
            try
            {
                var ap = sp.animationPlayer;
                var an = ap != null ? ap.animator : null;
                if (an == null || !an.isActiveAndEnabled) return " (no animator)";
                return an.GetBool(ScreenPointerAnimationPlayer.IsShownParameter) ?" (animator: shown)" : " (animator: hidden)";
            }
            catch (Exception e)
            {
                animatorBroken = true;
                LogOnce.Error("reading a pointer's animator", e);
                return "";
            }
        }

        void Line(string message)
        {
            lines++;
            Mod.Log.LogInfo(lines == MaxLinesPerRun ? message + " (pointer notes stop here for this run)" : message);
        }
    }
}
