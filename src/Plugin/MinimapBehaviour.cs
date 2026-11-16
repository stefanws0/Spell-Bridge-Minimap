using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellBrigadeMinimap
{
    /// <summary>Drives the minimap each frame: keys, run detection, map baking, scanning and drawing.</summary>
    public class MinimapBehaviour : MonoBehaviour
    {
        public MinimapBehaviour(IntPtr ptr) : base(ptr) { }

        const int MaxBakeTries = 10;
        const float BakeRetrySeconds = 2f;
        const float RebakeCooldownSeconds = 30f;
        const float OutsideMapPad = 10f;
        const float ZoomStep = 1.25f;

        MinimapView view;
        readonly WorldScanner scanner = new WorldScanner();
        readonly MoreEventsBridge bridge = new MoreEventsBridge();
        readonly List<EventMarker> markers = new List<EventMarker>();
        BakedMap map;
        IntPtr runPtr;
        int bakeTries;
        float nextBake, radius;
        bool hidden, radarLogged;

        void Start()
        {
            radius = Mod.Settings.Radius.Value;
            try
            {
                view = new MinimapView(Mod.Settings);
                view.SetVisible(false);
            }
            catch (Exception e) { LogOnce.Error("building the minimap UI", e); }
        }

        void LateUpdate()
        {
            if (view == null) return;
            try { Tick(); }
            catch (Exception e)
            {
                LogOnce.Error("drawing the minimap", e);
                view.SetVisible(false);
            }
        }

        void Tick()
        {
            var cfg = Mod.Settings;
            HandleKeys(cfg);

            var rm = RunManager.Instance;
            if (rm == null || !rm.runHasStarted)
            {
                if (runPtr != IntPtr.Zero) EndRun();
                view.SetVisible(false);
                return;
            }
            if (rm.Pointer != runPtr) StartRun(rm);

            if (!cfg.Enabled.Value || hidden || Time.timeScale == 0f)
            {
                view.SetVisible(false);
                return;
            }

            scanner.Tick(Time.unscaledTime, cfg.ShowEnemies.Value, cfg.ShowObjectives.Value || cfg.ShowPickups.Value);
            if (!scanner.HasCenter)
            {
                view.SetVisible(false);
                return;
            }
            TryBake(Time.unscaledTime);

            if (cfg.ShowMoreEvents.Value) bridge.Collect(markers);
            else markers.Clear();

            view.SetVisible(true);
            view.Render(scanner, markers, cfg.RotateWithCamera.Value ? CameraYaw() : 0f, radius);
        }

        void StartRun(RunManager rm)
        {
            EndRun();
            runPtr = rm.Pointer;
            scanner.ResetRun(rm);
            bakeTries = 0;
            nextBake = 0f;
            radarLogged = false;
            var cfg = Mod.Settings;
            radius = Mathf.Clamp(cfg.Radius.Value, cfg.MinRadius.Value, cfg.MaxRadius.Value);
        }

        void EndRun()
        {
            runPtr = IntPtr.Zero;
            view.SetMap(null);
            map?.Destroy();
            map = null;
        }

        void TryBake(float now)
        {
            bool needed = map == null
                ? bakeTries < MaxBakeTries
                : !map.Covers(scanner.Center.X, scanner.Center.Y, OutsideMapPad);
            if (!needed || now < nextBake) return;

            bakeTries++;
            nextBake = now + (map == null ? BakeRetrySeconds : RebakeCooldownSeconds);
            BakedMap fresh = null;
            try { fresh = MapBaker.Bake(); }
            catch (Exception e) { LogOnce.Error("reading the NavMesh", e); }

            if (fresh != null)
            {
                view.SetMap(fresh);
                map?.Destroy();
                map = fresh;
                bakeTries = 0;
                Mod.Log.LogInfo($"Baked level layout: {fresh.TriangleCount} triangles, {fresh.WorldWidth:0} x {fresh.WorldHeight:0} world units.");
            }
            else if (map == null && bakeTries >= MaxBakeTries && !radarLogged)
            {
                radarLogged = true;
                Mod.Log.LogWarning("No NavMesh found for this level; showing radar mode (dots without a layout).");
            }
        }

        void HandleKeys(MinimapConfig cfg)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (Pressed(kb, cfg.ToggleKey.Value)) hidden = !hidden;
            if (Pressed(kb, cfg.ZoomInKey.Value)) radius = Mathf.Max(cfg.MinRadius.Value, radius / ZoomStep);
            if (Pressed(kb, cfg.ZoomOutKey.Value)) radius = Mathf.Min(cfg.MaxRadius.Value, radius * ZoomStep);
        }

        static bool Pressed(Keyboard kb, string keyName)
        {
            if (string.IsNullOrEmpty(keyName) || !Enum.TryParse(keyName, true, out Key key) || key == Key.None) return false;
            var control = kb[key];
            return control != null && control.wasPressedThisFrame;
        }

        static float CameraYaw()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.eulerAngles.y : 0f;
        }
    }
}
