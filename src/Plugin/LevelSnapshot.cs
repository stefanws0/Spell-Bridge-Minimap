using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.VFX;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// Takes one top-down picture of the level with a temporary orthographic camera and returns it as one
    /// averaged pixel per map cell. Players, enemies and effects are hidden for that single render only.
    /// </summary>
    internal static class LevelSnapshot
    {
        const int Supersample = 4;          // render pixels per cell side, averaged down to one
        const int MaxTextureSide = 2048;
        const float CameraHeight = 40f;     // above the highest ground, roughly the game camera's distance
        const int UiLayer = 5;

        public static PixelBuffer Capture(float originX, float originZ, int cellsW, int cellsH, float cellSize, float groundY)
        {
            int ss = Supersample;
            while (ss > 1 && (cellsW * ss > MaxTextureSide || cellsH * ss > MaxTextureSide)) ss--;
            int w = cellsW * ss, h = cellsH * ss;
            float worldW = cellsW * cellSize, worldH = cellsH * cellSize;

            var previousActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("SpellBrigadeMinimap_Snapshot");
            Camera cam = null;
            Texture2D tex = null;
            var hidden = new List<Renderer>();
            try
            {
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = worldH * 0.5f;
                cam.aspect = worldW / worldH;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = CameraHeight + 80f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = ~(1 << UiLayer);
                cam.targetTexture = rt;
                // Looking straight down with +Z up in the image and +X to the right, matching the map grid.
                go.transform.position = new Vector3(originX + worldW * 0.5f, groundY + CameraHeight, originZ + worldH * 0.5f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                HideMovingThings(hidden);
                try { cam.Render(); }
                finally
                {
                    foreach (var r in hidden)
                        if (r != null) r.enabled = true;
                }

                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
                RenderTexture.active = previousActive;

                var pixels = tex.GetPixels32();
                if (pixels.Length < w * h) throw new InvalidOperationException($"expected {w * h} pixels, got {pixels.Length}");
                var bytes = new byte[w * h * 4];
                // An IL2CPP array's elements start after its 4-pointer header (class, monitor, bounds, length);
                // Color32 is 4 bytes laid out r, g, b, a, the same as our RGBA buffer.
                Marshal.Copy(IntPtr.Add(pixels.Pointer, 4 * IntPtr.Size), bytes, 0, bytes.Length);
                return PixelBuffer.Downsample(bytes, w, h, ss);
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cam != null) cam.targetTexture = null;
                if (tex != null) UObject.Destroy(tex);
                UObject.Destroy(go);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static void HideMovingThings(List<Renderer> hidden)
        {
            void HideUnder(Component root)
            {
                if (root == null) return;
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) Hide(renderers[i]);
            }

            void Hide(Renderer r)
            {
                if (r == null || !r.enabled) return;
                r.enabled = false;
                hidden.Add(r);
            }

            void HideAll<T>() where T : Renderer
            {
                try
                {
                    var all = UObject.FindObjectsOfType<T>();
                    for (int i = 0; i < all.Length; i++) Hide(all[i]);
                }
                catch (Exception e) { LogOnce.Error("hiding " + typeof(T).Name + " for the level picture", e); }
            }

            try
            {
                var mgr = GameplayPlayerManager.Instance;
                if (mgr != null)
                {
                    var alive = mgr.AlivePlayers;
                    if (alive != null) for (int i = 0; i < alive.Count; i++) HideUnder(alive[i]);
                    var ghosts = mgr.PlayerGhosts;
                    if (ghosts != null) for (int i = 0; i < ghosts.Count; i++) HideUnder(ghosts[i]);
                }
                var enemies = Enemy.AllEnemies;
                if (enemies != null) for (int i = 0; i < enemies.Count; i++) HideUnder(enemies[i]);
            }
            catch (Exception e) { LogOnce.Error("hiding players and enemies for the level picture", e); }

            HideAll<ParticleSystemRenderer>();
            HideAll<TrailRenderer>();
            HideAll<LineRenderer>();
            HideAll<VFXRenderer>();
        }
    }
}
