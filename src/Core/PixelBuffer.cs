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

        /// <summary>
        /// Shrinks an RGBA32 image by averaging each <paramref name="factor"/> x <paramref name="factor"/> block
        /// into one opaque pixel (a box filter). Partial blocks at the right and top edges are dropped.
        /// </summary>
        public static PixelBuffer Downsample(byte[] rgba, int width, int height, int factor)
        {
            if (factor < 1) throw new ArgumentOutOfRangeException(nameof(factor));
            if (rgba.Length < width * height * 4) throw new ArgumentException("buffer smaller than width x height", nameof(rgba));
            var dst = new PixelBuffer(width / factor, height / factor);
            int n = factor * factor;
            for (int y = 0; y < dst.Height; y++)
                for (int x = 0; x < dst.Width; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int sy = 0; sy < factor; sy++)
                    {
                        int row = ((y * factor + sy) * width + x * factor) * 4;
                        for (int sx = 0; sx < factor; sx++)
                        {
                            int i = row + sx * 4;
                            r += rgba[i];
                            g += rgba[i + 1];
                            b += rgba[i + 2];
                        }
                    }
                    dst.SetPixel(x, y, new Rgba((byte)(r / n), (byte)(g / n), (byte)(b / n), 255));
                }
            return dst;
        }

        /// <summary>
        /// For each <paramref name="factor"/> x <paramref name="factor"/> block of an RGBA32 image rendered over a
        /// flat <paramref name="key"/> colour, the share of pixels that differ from the key by more than
        /// <paramref name="tolerance"/> in some channel (i.e. where something was drawn). Row 0 is the bottom.
        /// </summary>
        public static float[] CoverageOfNonKey(byte[] rgba, int width, int height, int factor, Rgba key, int tolerance)
        {
            if (factor < 1) throw new ArgumentOutOfRangeException(nameof(factor));
            int w = width / factor, h = height / factor;
            var cover = new float[w * h];
            float n = factor * factor;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int drawn = 0;
                    for (int sy = 0; sy < factor; sy++)
                        for (int sx = 0; sx < factor; sx++)
                        {
                            int i = ((y * factor + sy) * width + x * factor + sx) * 4;
                            if (Math.Abs(rgba[i] - key.R) > tolerance || Math.Abs(rgba[i + 1] - key.G) > tolerance
                                || Math.Abs(rgba[i + 2] - key.B) > tolerance) drawn++;
                        }
                    cover[y * w + x] = drawn / n;
                }
            return cover;
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
