using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using NVec2 = System.Numerics.Vector2;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// Finds the level's roads. From above, cobblestone barely differs from the grass in colour, so instead this
    /// looks for the objects that make them: flat meshes and URP decals whose material, mesh or object name
    /// contains one of the configured keywords. Matching meshes are moved to an unused layer for one top-down
    /// render (and moved back); decal footprints are marked directly. Optionally writes a list of what lies on
    /// the ground, so the keywords can be tuned.
    /// </summary>
    internal static class RoadFinder
    {
        const float MaxRoadThickness = 2.5f;  // roads are flat; taller objects never count
        const int MaxRoadRenderers = 5000;

        sealed class Row
        {
            public int Count, Layer;
            public float Area, Tallest;
            public bool Road;
        }

        /// <summary>Per-cell road coverage for the rectangle, or null when nothing road-like was found.</summary>
        public static float[] Capture(float originX, float originZ, int cellsW, int cellsH, float cellSize, float groundY,
            string keywordList, bool writeReport)
        {
            string[] keys = (keywordList ?? "").Split(',').Select(k => k.Trim().ToLowerInvariant()).Where(k => k.Length > 0).ToArray();
            float maxX = originX + cellsW * cellSize, maxZ = originZ + cellsH * cellSize;
            var rows = writeReport ? new Dictionary<string, Row>() : null;
            var report = writeReport ? new StringBuilder() : null;

            // Flat meshes.
            var roads = new List<Renderer>();
            var meshes = UObject.FindObjectsOfType<MeshRenderer>();
            for (int i = 0; i < meshes.Length; i++)
            {
                var r = meshes[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                Vector3 c = b.center;
                if (c.x < originX || c.x > maxX || c.z < originZ || c.z > maxZ) continue;
                string mat = NameOf(r.sharedMaterial), mesh = NameOf(r.GetComponent<MeshFilter>()?.sharedMesh), obj = Simplify(r.gameObject.name);
                bool road = b.size.y <= MaxRoadThickness && Matches(keys, mat, mesh, obj);
                if (road) roads.Add(r);
                if (rows != null) Add(rows, $"mesh   | {mat} | {mesh} | {obj}", b.size.x * b.size.z, b.size.y, r.gameObject.layer, road);
            }

            float[] cover = null;
            if (roads.Count > 0 && roads.Count <= MaxRoadRenderers)
            {
                int layer = FreeLayer();
                if (layer < 0) LogOnce.Warn("roads-no-layer", "Roads: no unused layer to render them on; skipping road meshes.");
                else
                {
                    var moved = new List<(GameObject go, int layer)>();
                    try
                    {
                        foreach (var r in roads)
                        {
                            moved.Add((r.gameObject, r.gameObject.layer));
                            r.gameObject.layer = layer;
                        }
                        cover = LevelSnapshot.CaptureCoverage(originX, originZ, cellsW, cellsH, cellSize, groundY, layer);
                    }
                    finally
                    {
                        foreach (var (go, old) in moved)
                            if (go != null) go.layer = old;
                    }
                }
            }

            // Decals (URP decal projectors pointing down).
            int roadDecals = 0;
            var decals = UObject.FindObjectsOfType<DecalProjector>();
            for (int i = 0; i < decals.Length; i++)
            {
                var d = decals[i];
                if (d == null || !d.isActiveAndEnabled) continue;
                var t = d.transform;
                Vector3 p = t.position;
                if (p.x < originX || p.x > maxX || p.z < originZ || p.z > maxZ) continue;
                Vector3 size = d.size;
                string mat = NameOf(d.material), obj = Simplify(d.gameObject.name);
                bool road = t.forward.y < -0.5f && Matches(keys, mat, obj);
                if (road)
                {
                    cover ??= new float[cellsW * cellsH];
                    Vector3 right = t.right, up = t.up;
                    RoadMask.AddRectangle(cover, cellsW, cellsH, originX, originZ, cellSize, new NVec2(p.x, p.z),
                        new NVec2(right.x, right.z), new NVec2(up.x, up.z), size.x * 0.5f, size.y * 0.5f);
                    roadDecals++;
                }
                if (rows != null) Add(rows, $"decal  | {mat} | - | {obj}", size.x * size.y, size.z, d.gameObject.layer, road);
            }

            Mod.Log.LogInfo($"Roads: {roads.Count} road meshes and {roadDecals} road decals matched (keywords: {string.Join(", ", keys)}).");

            if (report != null)
            {
                report.AppendLine($"Objects in the mapped area, {originX:0},{originZ:0} to {maxX:0},{maxZ:0}; ground near y {groundY:0.0}.");
                report.AppendLine($"Road keywords: {string.Join(", ", keys)}. Matched: {roads.Count} meshes, {roadDecals} decals.");
                try
                {
                    var terrains = Terrain.activeTerrains;
                    report.AppendLine($"Terrains: {(terrains == null ? 0 : terrains.Length)}");
                    if (terrains != null)
                        for (int i = 0; i < terrains.Length; i++)
                            if (terrains[i] != null && terrains[i].terrainData != null)
                                report.AppendLine($"  {terrains[i].gameObject.name}: size {terrains[i].terrainData.size}");
                }
                catch (Exception e) { report.AppendLine("Terrains: (could not read: " + e.Message + ")"); }
                report.AppendLine();
                report.AppendLine("kind   | material | mesh | object  ->  count, footprint m2, tallest, layer, road?");
                foreach (var kv in rows.OrderByDescending(kv => kv.Value.Area).Take(250))
                    report.AppendLine($"{kv.Key}  ->  {kv.Value.Count}, {kv.Value.Area:0}, {kv.Value.Tallest:0.0}, {kv.Value.Layer}, {(kv.Value.Road ? "ROAD" : "")}");
                DebugImages.SaveText("scene-objects", report.ToString());
            }
            return cover;
        }

        static void Add(Dictionary<string, Row> rows, string key, float area, float tall, int layer, bool road)
        {
            if (!rows.TryGetValue(key, out var row)) rows[key] = row = new Row { Layer = layer };
            row.Count++;
            row.Area += area;
            row.Tallest = Math.Max(row.Tallest, tall);
            row.Road |= road;
        }

        static bool Matches(string[] keys, params string[] names)
        {
            foreach (var n in names)
            {
                string lower = n.ToLowerInvariant();
                foreach (var k in keys)
                    if (lower.Contains(k)) return true;
            }
            return false;
        }

        static string NameOf(UObject o)
        {
            if (o == null) return "-";
            string n = o.name ?? "-";
            return n.Replace(" (Instance)", "");
        }

        /// <summary>"Rock_Big (12)" and "Path(Clone)" become "Rock_Big" and "Path".</summary>
        static string Simplify(string name)
            => Regex.Replace(name ?? "-", @"(\s*\(\d+\))+$|\(Clone\)", "").Trim();

        /// <summary>The highest layer from 8 up that no renderer in the scene uses.</summary>
        static int FreeLayer()
        {
            var used = new bool[32];
            var all = UObject.FindObjectsOfType<Renderer>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) used[all[i].gameObject.layer] = true;
            for (int layer = 31; layer >= 8; layer--)
                if (!used[layer]) return layer;
            return -1;
        }
    }
}
