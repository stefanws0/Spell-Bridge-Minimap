using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// One pass over what lies in the mapped area, sorting meshes into:
    /// solid scenery (a solid collider and at least <see cref="MinSolidHeight"/> tall: rocks, walls, crystals),
    /// ground surfaces (flat meshes named like ground: lava, ice patches, water, paths), and
    /// ground effects to hide from pictures (respawn circles, gore, tornadoes, ...).
    /// Terrains and ground decals are collected too. Optionally writes a report of everything it saw.
    /// </summary>
    internal sealed class SceneryScan
    {
        const float MinSolidHeight = 1f;
        const float MaxSurfaceHeight = 0.6f;
        static readonly string[] EffectWords = { "respawn", "gore", "splash", "crosshair", "blood", "tornado", "hazard" };

        public readonly List<Renderer> Solid = new List<Renderer>();
        public readonly List<Renderer> Surfaces = new List<Renderer>();
        public readonly List<Renderer> Effects = new List<Renderer>();
        public readonly List<GameObject> Ground = new List<GameObject>(); // terrains, surfaces and decals: the "ground picture"

        sealed class Row
        {
            public int Count, Layer;
            public float Area, Tallest;
            public string Kind, Collider;
        }

        public static SceneryScan Run(float originX, float originZ, float maxX, float maxZ, string groundKeywordList, bool writeReport)
        {
            var scan = new SceneryScan();
            string[] groundKeys = (groundKeywordList ?? "").Split(',').Select(k => k.Trim().ToLowerInvariant()).Where(k => k.Length > 0).ToArray();
            var rows = writeReport ? new Dictionary<string, Row>() : null;

            var meshes = UObject.FindObjectsOfType<MeshRenderer>();
            for (int i = 0; i < meshes.Length; i++)
            {
                var r = meshes[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                Vector3 c = b.center;
                if (c.x < originX || c.x > maxX || c.z < originZ || c.z > maxZ) continue;
                string mat = NameOf(r.sharedMaterial), mesh = NameOf(r.GetComponent<MeshFilter>()?.sharedMesh), obj = Simplify(r.gameObject.name);

                string kind;
                Collider col = null;
                if (Matches(EffectWords, mat, mesh, obj)) { scan.Effects.Add(r); kind = "effect"; }
                else if (b.size.y <= MaxSurfaceHeight && Matches(groundKeys, mat, mesh, obj)) { scan.Surfaces.Add(r); scan.Ground.Add(r.gameObject); kind = "ground"; }
                else if (b.size.y >= MinSolidHeight && (col = SolidCollider(r.gameObject)) != null) { scan.Solid.Add(r); kind = "SOLID"; }
                else kind = "";

                if (rows != null)
                {
                    col ??= SolidCollider(r.gameObject);
                    string key = $"{mat} | {mesh} | {obj}";
                    if (!rows.TryGetValue(key, out var row)) rows[key] = row = new Row { Layer = r.gameObject.layer, Kind = kind, Collider = col == null ? "-" : $"{col.GetIl2CppType().Name}@{col.gameObject.layer}" };
                    row.Count++;
                    row.Area += b.size.x * b.size.z;
                    row.Tallest = Math.Max(row.Tallest, b.size.y);
                }
            }

            int decals = 0;
            try
            {
                var projectors = UObject.FindObjectsOfType<DecalProjector>();
                for (int i = 0; i < projectors.Length; i++)
                {
                    var d = projectors[i];
                    if (d == null || !d.isActiveAndEnabled) continue;
                    Vector3 p = d.transform.position;
                    if (p.x < originX || p.x > maxX || p.z < originZ || p.z > maxZ) continue;
                    if (Matches(EffectWords, NameOf(d.material), d.gameObject.name)) continue;
                    scan.Ground.Add(d.gameObject);
                    decals++;
                }
            }
            catch (Exception e) { LogOnce.Error("listing decals", e); }

            int terrains = 0;
            try
            {
                var all = Terrain.activeTerrains;
                if (all != null)
                    for (int i = 0; i < all.Length; i++)
                        if (all[i] != null) { scan.Ground.Add(all[i].gameObject); terrains++; }
            }
            catch (Exception e) { LogOnce.Error("listing terrains", e); }

            Mod.Log.LogInfo($"Scenery: {scan.Solid.Count} solid meshes, {scan.Surfaces.Count} ground surfaces, {decals} decals, "
                + $"{terrains} terrains, {scan.Effects.Count} ground effects hidden from pictures.");

            if (rows != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Objects in the mapped area, {originX:0},{originZ:0} to {maxX:0},{maxZ:0}.");
                sb.AppendLine($"Ground keywords: {string.Join(", ", groundKeys)}. Terrains: {terrains}. Decals used as ground: {decals}.");
                sb.AppendLine("kind | material | mesh | object  ->  count, footprint m2, tallest, layer, collider(type@layer)");
                foreach (var kv in rows.OrderByDescending(kv => kv.Value.Area).Take(300))
                    sb.AppendLine($"{kv.Value.Kind,-6} | {kv.Key}  ->  {kv.Value.Count}, {kv.Value.Area:0}, {kv.Value.Tallest:0.0}, {kv.Value.Layer}, {kv.Value.Collider}");
                DebugImages.SaveText("scene-objects", sb.ToString());
            }
            return scan;
        }

        /// <summary>An enabled, non-trigger collider on the object, its children or its parents.</summary>
        static Collider SolidCollider(GameObject go)
        {
            try
            {
                var own = go.GetComponents<Collider>();
                for (int i = 0; i < own.Length; i++) if (IsSolid(own[i])) return own[i];
                var below = go.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < below.Length; i++) if (IsSolid(below[i])) return below[i];
                var above = go.GetComponentsInParent<Collider>(true);
                for (int i = 0; i < above.Length; i++) if (IsSolid(above[i])) return above[i];
            }
            catch { }
            return null;
        }

        static bool IsSolid(Collider c) => c != null && c.enabled && !c.isTrigger && c.attachedRigidbody == null;

        static bool Matches(string[] keys, params string[] names)
        {
            foreach (var n in names)
            {
                string lower = (n ?? "").ToLowerInvariant();
                foreach (var k in keys)
                    if (lower.Contains(k)) return true;
            }
            return false;
        }

        static string NameOf(UObject o) => o == null ? "-" : (o.name ?? "-").Replace(" (Instance)", "");

        /// <summary>"Rock_Big (12)" and "Path(Clone)" become "Rock_Big" and "Path".</summary>
        static string Simplify(string name) => Regex.Replace(name ?? "-", @"(\s*\(\d+\))+$|\(Clone\)", "").Trim();
    }
}
