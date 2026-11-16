using System;
using SpellBrigadeMinimap.Core;
using UnityEngine;

namespace SpellBrigadeMinimap
{
    /// <summary>Turns CPU pixel buffers into Unity textures and sprites that survive scene changes.</summary>
    internal static class SpriteFactory
    {
        public static Texture2D ToTexture(PixelBuffer img, FilterMode filter, bool keepReadable)
        {
            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false);
            tex.filterMode = filter;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            Upload(tex, img, keepReadable);
            return tex;
        }

        /// <summary>Copies the buffer straight from pinned managed memory; no IL2CPP array is allocated.</summary>
        public static unsafe void Upload(Texture2D tex, PixelBuffer img, bool keepReadable)
        {
            fixed (byte* p = img.Data) tex.LoadRawTextureData((IntPtr)p, img.Data.Length);
            tex.Apply(false, !keepReadable);
        }

        public static Sprite ToSprite(PixelBuffer img)
        {
            var tex = ToTexture(img, FilterMode.Bilinear, keepReadable: false);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, img.Width, img.Height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
