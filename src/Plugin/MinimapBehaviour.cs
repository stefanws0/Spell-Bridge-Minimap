using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellBrigadeMinimap
{
    /// <summary>Drives the minimap each frame: keys, run detection, level mapping, scanning and drawing.</summary>
    public class MinimapBehaviour : MonoBehaviour
    {
        public MinimapBehaviour(IntPtr ptr) : base(ptr) { }

        const float ZoomStep = 1.25f;

        MinimapView view;
        readonly WorldScanner scanner = new WorldScanner();
        readonly NavMeshMapper mapper = new NavMeshMapper();
        readonly MoreEventsBridge bridge = new MoreEventsBridge();
        readonly List<EventMarker> markers = new List<EventMarker>();
        IntPtr runPtr;
        float radius;
        bool hidden;

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
            if (mapper.Tick(Time.unscaledTime, scanner.Center, scanner.CenterY)) view.SetMap(mapper.Map);

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
            var cfg = Mod.Settings;
            radius = Mathf.Clamp(cfg.Radius.Value, cfg.MinRadius.Value, cfg.MaxRadius.Value);
        }

        void EndRun()
        {
            runPtr = IntPtr.Zero;
            view.SetMap(null);
            mapper.Reset();
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
