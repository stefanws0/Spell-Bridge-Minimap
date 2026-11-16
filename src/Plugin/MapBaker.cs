using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.AI;

namespace SpellBrigadeMinimap
{
    /// <summary>A level layout texture and where it sits in the world.</summary>
    internal sealed class BakedMap
    {
        public Texture2D Texture;
        public float OriginX, OriginZ, WorldWidth, WorldHeight;
        public int TriangleCount;

        public bool Covers(float x, float z, float pad)
            => x >= OriginX - pad && x <= OriginX + WorldWidth + pad
            && z >= OriginZ - pad && z <= OriginZ + WorldHeight + pad;

        public void Destroy()
        {
            if (Texture != null) Object.Destroy(Texture);
            Texture = null;
        }
    }

    internal static class MapBaker
    {
        const int MaxTextureSize = 1024;
        static readonly Rgba Fill = new Rgba(92, 104, 120, 255);
        static readonly Rgba Outline = new Rgba(200, 210, 225, 255);

        /// <summary>
        /// Reads the loaded NavMesh and paints its walkable area. Returns null when no NavMesh is loaded.
        /// Uses NavMesh.Triangulate because the CalculateTriangulation wrapper is stripped in this build.
        /// </summary>
        public static BakedMap Bake()
        {
            NavMesh.Triangulate(out Il2CppStructArray<Vector3> verts, out Il2CppStructArray<int> idx);
            if (verts == null || idx == null || verts.Length == 0 || idx.Length < 3) return null;

            int n = verts.Length;
            var xs = new float[n];
            var zs = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 v = verts[i];
                xs[i] = v.x;
                zs[i] = v.z;
            }
            var indices = new int[idx.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = idx[i];

            var grid = TriangleRasterizer.Rasterize(xs, zs, indices, MaxTextureSize);
            if (grid == null) return null;
            var img = TriangleRasterizer.ToImage(grid, Fill, Outline);
            return new BakedMap
            {
                Texture = SpriteFactory.ToTexture(img, FilterMode.Bilinear, keepReadable: false),
                OriginX = grid.OriginX,
                OriginZ = grid.OriginZ,
                WorldWidth = grid.WorldWidth,
                WorldHeight = grid.WorldHeight,
                TriangleCount = indices.Length / 3,
            };
        }
    }
}
