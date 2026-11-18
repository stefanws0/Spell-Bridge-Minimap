using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// White copies of the game's own icons (see <see cref="IconMask"/>), so the minimap can colour them the way
    /// the game colours the thing itself. The game's textures can't be read directly, so each icon's texture is
    /// copied to a render texture once and read back. One copy per icon, made the first time it's needed, with a
    /// dark edge so it reads on any ground; with SaveDebugImages on, the original and the copy are saved as PNGs.
    /// </summary>
    internal static class IconRecolor
    {
        const float EdgeShare = 0.07f;                       // the dark edge, as a share of the icon's longer side
        static readonly Rgba Edge = new Rgba(22, 22, 22);
        static readonly Rgba Stone = new Rgba(168, 172, 182);
        static readonly Rgba StoneDetail = new Rgba(110, 180, 255);

        static readonly Dictionary<IntPtr, Sprite> white = new Dictionary<IntPtr, Sprite>();
        static readonly Dictionary<IntPtr, Sprite> statue = new Dictionary<IntPtr, Sprite>();
        static bool broken;

        /// <summary>The white copy of <paramref name="icon"/>, or null if it couldn't be made.</summary>
        public static Sprite White(Sprite icon, Transform forWhat)
        {
            if (icon == null || broken) return null;
            if (white.TryGetValue(icon.Pointer, out var copy)) return copy;
            copy = Make(icon, forWhat != null ? $"'{forWhat.name}'" : "the minimap", "white", img => IconMask.ToWhite(img));
            white[icon.Pointer] = copy;
            return copy;
        }

        /// <summary>
        /// The petrified-enemy icon as a grey statue with small blue details (its hole and two eyes), already
        /// coloured: draw it white. Null if it couldn't be made.
        /// </summary>
        public static Sprite Statue(Sprite icon)
        {
            if (icon == null || broken) return null;
            if (statue.TryGetValue(icon.Pointer, out var copy)) return copy;
            copy = Make(icon, "the statues", "statue", img => IconMask.Statue(img, Stone, StoneDetail));
            statue[icon.Pointer] = copy;
            return copy;
        }

        static Sprite Make(Sprite icon, string label, string kind, Func<PixelBuffer, PixelBuffer> recolour)
        {
            try
            {
                var original = ReadIcon(icon);
                if (original == null) return null;
                int edge = Math.Max(1, (int)Math.Round(Math.Max(original.Width, original.Height) * EdgeShare));
                var copy = IconMask.Outline(recolour(original), edge, Edge);
                var c = IconMask.AverageColour(original);
                Mod.Log.LogInfo($"Game icon '{icon.name}' (for {label}): {original.Width} x {original.Height} px, average colour ({c.R}, {c.G}, {c.B}); {kind} copy made.");
                string file = Safe(icon.name);
                DebugImages.Save($"icon-{file}", original);
                DebugImages.Save($"icon-{file}-{kind}", copy);
                return SpriteFactory.ToSprite(copy);
            }
            catch (Exception e)
            {
                broken = true;
                LogOnce.Error("recolouring a game icon (objectives fall back to the minimap's own icons)", e);
                return null;
            }
        }

        static PixelBuffer ReadIcon(Sprite icon)
        {
            var tex = icon.texture;
            if (tex == null) return null;
            Rect r;
            try { r = icon.textureRect; }
            catch { r = icon.rect; } // tightly packed sprites have no texture rect
            int x = (int)r.x, y = (int)r.y, w = Math.Max(1, (int)r.width), h = Math.Max(1, (int)r.height);

            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(x, y, w, h), 0, 0, false);
                RenderTexture.active = previousActive;
                var pixels = copy.GetPixels32();
                if (pixels.Length < w * h) return null;
                var img = new PixelBuffer(w, h);
                // Color32 is laid out r, g, b, a like the buffer; IL2CPP array data starts after a 4-pointer header.
                Marshal.Copy(IntPtr.Add(pixels.Pointer, 4 * IntPtr.Size), img.Data, 0, img.Data.Length);
                return img;
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null) UObject.Destroy(copy);
            }
        }

        static string Safe(string name)
        {
            var chars = (name ?? "icon").ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }
}
