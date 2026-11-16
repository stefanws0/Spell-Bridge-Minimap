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
    /// Pictures for the minimap: a top-down picture of the level (players, enemies and effects hidden for
    /// that one render), and a small render of what the game camera sees, used to match colours.
    /// </summary>
    internal static class LevelSnapshot
    {
        const int Supersample = 4;          // render pixels per cell side, averaged down to one
        const int MaxTextureSide = 2048;
        const float CameraHeight = 12f;     // above the highest ground; low, because fog grows with distance
        const int UiLayer = 5;
        static bool fogLogged;

        /// <summary>Renders the world rectangle straight down and returns one averaged pixel per cell.</summary>
        public static PixelBuffer CaptureLevel(float originX, float originZ, int cellsW, int cellsH, float cellSize, float groundY)
        {
            if (!fogLogged)
            {
                fogLogged = true;
                try { Mod.Log.LogInfo($"Scene fog is {(RenderSettings.fog ? "on" : "off")}."); } catch { }
            }

            int ss = Supersample;
            while (ss > 1 && (cellsW * ss > MaxTextureSide || cellsH * ss > MaxTextureSide)) ss--;
            int w = cellsW * ss, h = cellsH * ss;
            float worldW = cellsW * cellSize, worldH = cellsH * cellSize;

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("SpellBrigadeMinimap_Snapshot");
            Camera cam = null;
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
                return PixelBuffer.Downsample(ReadBack(rt, w, h), w, h, ss);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                UObject.Destroy(go);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Renders the game camera once into a small texture (no overlay UI). Null without a main camera.</summary>
        public static PixelBuffer CaptureGameView(int width)
        {
            var main = Camera.main;
            if (main == null) return null;
            float aspect = main.pixelHeight > 0 ? (float)main.pixelWidth / main.pixelHeight : 16f / 9f;
            int w = width, h = Math.Max(1, (int)(width / aspect));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var previousTarget = main.targetTexture;
            try
            {
                main.targetTexture = rt;
                main.Render();
                main.targetTexture = previousTarget;
                return PixelBuffer.Downsample(ReadBack(rt, w, h), w, h, 1);
            }
            finally
            {
                main.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static byte[] ReadBack(RenderTexture rt, int w, int h)
        {
            var previousActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
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
                return bytes;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (tex != null) UObject.Destroy(tex);
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
