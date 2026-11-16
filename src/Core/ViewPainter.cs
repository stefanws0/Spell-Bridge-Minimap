using System;
using System.Numerics;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// Paints the map with what the game camera actually showed. Each pixel of a camera picture is followed along
    /// its view ray to the ground plane, and its colour is added to the map cell it lands in; cells end up with the
    /// average colour of everything seen there. Rays that pass over an obstacle just before reaching the ground
    /// most likely show the obstacle's side rather than the ground, so they can be dropped.
    /// The map is Width x Height cells of CellSize world units, cell (0, 0) starting at (OriginX, OriginZ).
    /// </summary>
    public sealed class ViewPainter
    {
        public readonly int Width, Height;
        public readonly float OriginX, OriginZ, CellSize;
        readonly float[] sr, sg, sb, sn;

        public ViewPainter(int width, int height, float originX, float originZ, float cellSize)
        {
            Width = width;
            Height = height;
            OriginX = originX;
            OriginZ = originZ;
            CellSize = cellSize;
            sr = new float[width * height];
            sg = new float[width * height];
            sb = new float[width * height];
            sn = new float[width * height];
        }

        /// <summary>
        /// Adds a camera picture. <paramref name="clipToWorld"/> maps clip coordinates (x, y, z each -1..1, w = 1;
        /// System.Numerics row-vector convention) to world space. Pixels <paramref name="include"/> rejects are
        /// skipped; with <paramref name="blocked"/> set, so are pixels whose ray crosses a blocked cell within
        /// <paramref name="occluderHeight"/> above the ground. Returns how many pixels were painted.
        /// </summary>
        public int AddView(PixelBuffer view, Matrix4x4 clipToWorld, float groundY, Func<int, int, bool> include,
            Func<int, int, bool> blocked = null, float occluderHeight = 0f)
        {
            int painted = 0;
            for (int y = 0; y < view.Height; y++)
                for (int x = 0; x < view.Width; x++)
                {
                    if (include != null && !include(x, y)) continue;
                    Rgba c = view.GetPixel(x, y);
                    if (c.A == 0) continue;

                    float nx = (x + 0.5f) / view.Width * 2f - 1f, ny = (y + 0.5f) / view.Height * 2f - 1f;
                    Vector4 a = Vector4.Transform(new Vector4(nx, ny, -1f, 1f), clipToWorld);
                    Vector4 b = Vector4.Transform(new Vector4(nx, ny, 1f, 1f), clipToWorld);
                    if (Math.Abs(a.W) < 1e-6f || Math.Abs(b.W) < 1e-6f) continue;
                    var near = new Vector3(a.X, a.Y, a.Z) / a.W;
                    var far = new Vector3(b.X, b.Y, b.Z) / b.W;
                    Vector3 d = far - near;
                    if (d.Y > -1e-5f) continue; // looking level or up: never reaches the ground
                    float t = (groundY - near.Y) / d.Y;
                    if (t < 0f) continue;
                    Vector3 p = near + d * t;

                    if (!Cell(p.X, p.Z, out int cx, out int cy)) continue;
                    if (blocked != null && occluderHeight > 0f && CrossesBlocked(p, d, occluderHeight, blocked)) continue;

                    int i = cy * Width + cx;
                    sr[i] += c.R;
                    sg[i] += c.G;
                    sb[i] += c.B;
                    sn[i] += 1f;
                    painted++;
                }
            return painted;
        }

        /// <summary>The average colour of each cell seen at least <paramref name="minSamples"/> times; other cells transparent.</summary>
        public PixelBuffer Result(float minSamples)
        {
            var img = new PixelBuffer(Width, Height);
            for (int i = 0; i < sn.Length; i++)
            {
                float n = sn[i];
                if (n <= 0f || n < minSamples) continue;
                img.SetPixel(i % Width, i / Width, new Rgba((byte)(sr[i] / n), (byte)(sg[i] / n), (byte)(sb[i] / n), 255));
            }
            return img;
        }

        bool Cell(float wx, float wz, out int cx, out int cy)
        {
            cx = (int)Math.Floor((wx - OriginX) / CellSize);
            cy = (int)Math.Floor((wz - OriginZ) / CellSize);
            return (uint)cx < (uint)Width && (uint)cy < (uint)Height;
        }

        /// <summary>Walks back up the ray from the ground hit to <paramref name="height"/> and reports any blocked cell on the way.</summary>
        bool CrossesBlocked(Vector3 ground, Vector3 dir, float height, Func<int, int, bool> blocked)
        {
            const int Steps = 8;
            Vector3 up = -dir / -dir.Y; // moves 1 unit up along the ray
            for (int k = 1; k <= Steps; k++)
            {
                Vector3 q = ground + up * (height * k / Steps);
                if (Cell(q.X, q.Z, out int cx, out int cy) && blocked(cx, cy)) return true;
            }
            return false;
        }
    }
}
