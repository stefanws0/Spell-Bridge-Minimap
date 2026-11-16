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
        const float SettleSeconds = 1f;          // standing on the ground this long counts as landed
        const float SettleFallbackSeconds = 6f;  // after the run starts, show the picture even if landing wasn't seen
        const float SpawnPointRetrySeconds = 1f;

        MinimapView view;
        readonly WorldScanner scanner = new WorldScanner();
        readonly NavMeshMapper mapper = new NavMeshMapper();
        readonly MoreEventsBridge bridge = new MoreEventsBridge();
        readonly ObjectiveDiscovery discovery = new ObjectiveDiscovery();
        readonly List<EventMarker> markers = new List<EventMarker>();
        IntPtr runPtr;
        float radius, runStartedAt = -1f, groundedSince = -1f, nextSpawnPointSearch;
        Vector3? spawnPoint;
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
            float now = Time.unscaledTime;
            HandleKeys(cfg);

            var rm = RunManager.Instance;
            if (rm == null)
            {
                if (runPtr != IntPtr.Zero) EndRun();
                view.SetVisible(false);
                return;
            }
            if (rm.Pointer != runPtr) StartRun(rm);

            scanner.Tick(now, cfg.ShowEnemies.Value, cfg.ShowObjectives.Value || cfg.ShowPickups.Value);
            discovery.Tick(now, scanner.Pois);

            // Map the level as early as possible: from the player (even mid-air while dropping in), or from a
            // spawn point in the level if the player hasn't appeared yet.
            Vector3 player = new Vector3(scanner.Center.X, scanner.CenterY, scanner.Center.Y);
            if (scanner.HasCenter) mapper.SeedNear(now, player);
            else if (TryFindSpawnPoint(now, out Vector3 point)) mapper.SeedNear(now, point);

            bool started = rm.runHasStarted;
            if (started && runStartedAt < 0f) runStartedAt = now;
            if (scanner.HasCenter && mapper.IsOnGround(player)) { if (groundedSince < 0f) groundedSince = now; }
            else groundedSince = -1f;
            bool settled = started && ((groundedSince >= 0f && now - groundedSince >= SettleSeconds)
                                       || now - runStartedAt >= SettleFallbackSeconds);
            if (mapper.Tick(now, settled)) view.SetMap(mapper.Map);

            if (!started || !scanner.HasCenter || !cfg.Enabled.Value || hidden || Time.timeScale == 0f)
            {
                view.SetVisible(false);
                return;
            }

            if (cfg.ShowMoreEvents.Value) bridge.Collect(markers);
            else markers.Clear();

            view.SetVisible(true);
            view.Render(scanner, markers, discovery, cfg.RotateWithCamera.Value ? CameraYaw() : 0f, radius);
        }

        bool TryFindSpawnPoint(float now, out Vector3 point)
        {
            if (spawnPoint == null && now >= nextSpawnPointSearch)
            {
                nextSpawnPointSearch = now + SpawnPointRetrySeconds;
                var sp = UnityEngine.Object.FindObjectOfType<ObjectiveSpawnPoint>();
                if (sp != null) spawnPoint = sp.transform.position;
            }
            point = spawnPoint ?? default;
            return spawnPoint.HasValue;
        }

        void StartRun(RunManager rm)
        {
            EndRun();
            runPtr = rm.Pointer;
            scanner.ResetRun(rm);
            DebugImages.BeginRun();
            runStartedAt = groundedSince = -1f;
            spawnPoint = null;
            nextSpawnPointSearch = 0f;
            var cfg = Mod.Settings;
            radius = Mathf.Clamp(cfg.Radius.Value, cfg.MinRadius.Value, cfg.MaxRadius.Value);
        }

        void EndRun()
        {
            runPtr = IntPtr.Zero;
            view.SetMap(null);
            mapper.Reset();
            discovery.Reset();
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
