using System;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>A CPU-side RGBA32 image. Row 0 is the bottom row, matching Unity textures.</summary>
    public sealed class PixelBuffer
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Data;

        public PixelBuffer(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            Data = new byte[width * height * 4];
        }

        public void Clear() => Array.Clear(Data, 0, Data.Length);

        public void SetPixel(int x, int y, Rgba c)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
            int i = (y * Width + x) * 4;
            Data[i] = c.R;
            Data[i + 1] = c.G;
            Data[i + 2] = c.B;
            Data[i + 3] = c.A;
        }

        public Rgba GetPixel(int x, int y)
        {
            int i = (y * Width + x) * 4;
            return new Rgba(Data[i], Data[i + 1], Data[i + 2], Data[i + 3]);
        }

        /// <summary>Sets every pixel whose center lies within <paramref name="radius"/> of (cx, cy).</summary>
        public void FillCircle(float cx, float cy, float radius, Rgba c)
        {
            int x0 = (int)Math.Floor(cx - radius), x1 = (int)Math.Ceiling(cx + radius);
            int y0 = (int)Math.Floor(cy - radius), y1 = (int)Math.Ceiling(cy + radius);
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            {
                if ((uint)y >= (uint)Height) continue;
                float dy = y + 0.5f - cy;
                for (int x = x0; x <= x1; x++)
                {
                    if ((uint)x >= (uint)Width) continue;
                    float dx = x + 0.5f - cx;
                    if (dx * dx + dy * dy <= r2) SetPixel(x, y, c);
                }
            }
        }
    }
}
