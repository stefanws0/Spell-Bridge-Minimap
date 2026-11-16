using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// Pictures for the minimap: a top-down picture of the level (players, enemies and effects hidden for
    /// that one render, the game's colour grading on), a top-down coverage render of a single layer (used for
    /// roads), and a small render of what the game camera sees, used to match colours.
    /// </summary>
    internal static class LevelSnapshot
    {
        const int Supersample = 4;          // render pixels per cell side, averaged down to one
        const int MaxTextureSide = 2048;
        const float CameraHeight = 12f;     // above the highest ground; low, because fog grows with distance
        const int UiLayer = 5;
        static readonly Rgba CoverageKey = new Rgba(255, 0, 255);
        static bool fogLogged, gradingLogged;

        /// <summary>
        /// Renders the world rectangle straight down and returns one averaged pixel per cell. Players, enemies,
        /// effects and <paramref name="alsoHide"/> (e.g. respawn circles) are hidden for the render.
        /// </summary>
        public static PixelBuffer CaptureLevel(float originX, float originZ, int cellsW, int cellsH, float cellSize, float groundY,
            IList<Renderer> alsoHide = null)
        {
            if (!fogLogged)
            {
                fogLogged = true;
                try { Mod.Log.LogInfo($"Scene fog is {(RenderSettings.fog ? "on" : "off")}."); } catch { }
            }

            int ss = SupersampleFor(cellsW, cellsH);
            var hidden = new List<Renderer>();
            var lensEffectsOff = new List<VolumeComponent>();
            byte[] bytes = RenderTopDown(originX, originZ, cellsW, cellsH, cellSize, groundY, ss, ~(1 << UiLayer), Color.black,
                cam =>
                {
                    HideMovingThings(hidden);
                    if (alsoHide != null)
                        foreach (var r in alsoHide)
                            if (r != null && r.enabled) { r.enabled = false; hidden.Add(r); }
                    DisableLensEffects(lensEffectsOff);
                    UseGameColourGrading(cam);
                },
                () =>
                {
                    foreach (var r in hidden)
                        if (r != null) r.enabled = true;
                    foreach (var c in lensEffectsOff)
                        if (c != null) c.active = true;
                });
            return PixelBuffer.Downsample(bytes, cellsW * ss, cellsH * ss, ss);
        }

        /// <summary>The highest layer from 8 up that no renderer in the scene uses, or -1.</summary>
        public static int FreeLayer()
        {
            var used = new bool[32];
            var all = UObject.FindObjectsOfType<Renderer>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) used[all[i].gameObject.layer] = true;
            for (int layer = 31; layer >= 8; layer--)
                if (!used[layer]) return layer;
            return -1;
        }

        /// <summary>Moves the objects to <paramref name="layer"/>, runs <paramref name="body"/>, and always moves them back.</summary>
        public static T OnLayer<T>(IEnumerable<GameObject> objects, int layer, Func<T> body)
        {
            var moved = new List<(GameObject go, int layer)>();
            try
            {
                foreach (var go in objects)
                {
                    if (go == null) continue;
                    moved.Add((go, go.layer));
                    go.layer = layer;
                }
                return body();
            }
            finally
            {
                foreach (var (go, old) in moved)
                    if (go != null) go.layer = old;
            }
        }

        /// <summary>Renders only <paramref name="layer"/> straight down over a key colour; returns per-cell coverage.</summary>
        public static float[] CaptureCoverage(float originX, float originZ, int cellsW, int cellsH, float cellSize, float groundY, int layer)
        {
            int ss = SupersampleFor(cellsW, cellsH);
            byte[] bytes = RenderTopDown(originX, originZ, cellsW, cellsH, cellSize, groundY, ss, 1 << layer,
                new Color(1f, 0f, 1f, 1f), null, null);
            return PixelBuffer.CoverageOfNonKey(bytes, cellsW * ss, cellsH * ss, ss, CoverageKey, 24);
        }

        static int SupersampleFor(int cellsW, int cellsH)
        {
            int ss = Supersample;
            while (ss > 1 && (cellsW * ss > MaxTextureSide || cellsH * ss > MaxTextureSide)) ss--;
            return ss;
        }

        /// <summary>
        /// One orthographic render looking straight down at the rectangle, +Z up in the image and +X right
        /// (matching the map grid). <paramref name="before"/> runs right before the render, <paramref name="after"/>
        /// right after it (even if it fails).
        /// </summary>
        static byte[] RenderTopDown(float originX, float originZ, int cellsW, int cellsH, float cellSize, float groundY, int ss,
            int cullingMask, Color background, Action<Camera> before, Action after)
        {
            int w = cellsW * ss, h = cellsH * ss;
            float worldW = cellsW * cellSize, worldH = cellsH * cellSize;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("SpellBrigadeMinimap_Snapshot");
            Camera cam = null;
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
                cam.backgroundColor = background;
                cam.cullingMask = cullingMask;
                cam.targetTexture = rt;
                go.transform.position = new Vector3(originX + worldW * 0.5f, groundY + CameraHeight, originZ + worldH * 0.5f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                try
                {
                    before?.Invoke(cam);
                    cam.Render();
                }
                finally { after?.Invoke(); }
                return ReadBack(rt, w, h);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                UObject.Destroy(go);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// Renders the game camera once into a small texture (no overlay UI). Null without a main camera.
        /// <paramref name="clipToWorld"/> maps the picture's clip coordinates back to the world (System.Numerics,
        /// row-vector convention), for projecting it onto the ground.
        /// </summary>
        public static PixelBuffer CaptureGameView(int width, out System.Numerics.Matrix4x4 clipToWorld)
        {
            clipToWorld = System.Numerics.Matrix4x4.Identity;
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
                // Unity: clip = P * V * world (column vectors). Transposed into System.Numerics: clip = world * V^T * P^T.
                var viewProj = ToNumerics(main.worldToCameraMatrix) * ToNumerics(main.projectionMatrix);
                if (!System.Numerics.Matrix4x4.Invert(viewProj, out clipToWorld)) clipToWorld = System.Numerics.Matrix4x4.Identity;
                main.targetTexture = previousTarget;
                return PixelBuffer.Downsample(ReadBack(rt, w, h), w, h, 1);
            }
            finally
            {
                main.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>A Unity matrix (column vectors) as its System.Numerics transpose (row vectors).</summary>
        static System.Numerics.Matrix4x4 ToNumerics(Matrix4x4 m) => new System.Numerics.Matrix4x4(
            m.m00, m.m10, m.m20, m.m30,
            m.m01, m.m11, m.m21, m.m31,
            m.m02, m.m12, m.m22, m.m32,
            m.m03, m.m13, m.m23, m.m33);

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

        /// <summary>
        /// Gives the picture the game's look: post-processing on, with the same volumes as the game camera
        /// (colour grading, tonemapping). Without it, sand comes out pinkish instead of yellow.
        /// </summary>
        static void UseGameColourGrading(Camera cam)
        {
            try
            {
                var data = CameraExtensions.GetUniversalAdditionalCameraData(cam);
                data.renderPostProcessing = true;
                var main = Camera.main;
                if (main != null)
                {
                    var mainData = CameraExtensions.GetUniversalAdditionalCameraData(main);
                    data.volumeLayerMask = mainData.volumeLayerMask;
                    data.volumeTrigger = mainData.volumeTrigger != null ? mainData.volumeTrigger : main.transform;
                    if (!gradingLogged)
                    {
                        gradingLogged = true;
                        Mod.Log.LogInfo($"Game camera post-processing is {(mainData.renderPostProcessing ? "on" : "off")}; using it for the level picture.");
                    }
                }
                CameraExtensions.UpdateVolumeStack(cam, data);
            }
            catch (Exception e) { LogOnce.Error("using the game's colour grading for the level picture", e); }
        }

        /// <summary>Switches off camera-lens effects (vignette, depth of field, ...) in every active volume for one render.</summary>
        static void DisableLensEffects(List<VolumeComponent> disabled)
        {
            try
            {
                var volumes = UObject.FindObjectsOfType<Volume>();
                for (int v = 0; v < volumes.Length; v++)
                {
                    var volume = volumes[v];
                    if (volume == null || !volume.isActiveAndEnabled) continue;
                    var profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                    if (profile == null || profile.components == null) continue;
                    var components = profile.components;
                    for (int i = 0; i < components.Count; i++)
                    {
                        var c = components[i];
                        if (c == null || !c.active || !IsLensEffect(c)) continue;
                        c.active = false;
                        disabled.Add(c);
                    }
                }
            }
            catch (Exception e) { LogOnce.Error("turning off lens effects for the level picture", e); }
        }

        static bool IsLensEffect(VolumeComponent c)
            => c.TryCast<Vignette>() != null || c.TryCast<DepthOfField>() != null || c.TryCast<MotionBlur>() != null
            || c.TryCast<ChromaticAberration>() != null || c.TryCast<LensDistortion>() != null
            || c.TryCast<PaniniProjection>() != null || c.TryCast<FilmGrain>() != null;

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
