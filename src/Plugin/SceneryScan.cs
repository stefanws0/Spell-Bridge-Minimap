using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SpellBrigadeMinimap.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// One pass over what lies in the mapped area, sorting meshes into:
    /// solid scenery, i.e. rocks and walls you can't walk through (at least <see cref="MinSolidHeight"/> tall, and either
    /// a solid collider on the object or a name from the obstacle keywords: many rocks keep their collider elsewhere),
    /// and ground effects to hide from pictures (respawn circles, gore, tornadoes, ...).
    /// Optionally writes a report of everything it saw.
    /// </summary>
    internal sealed class SceneryScan
    {
        const float MinSolidHeight = 1f;
        static readonly string[] EffectWords = { "respawn", "gore", "splash", "crosshair", "blood", "tornado", "hazard", "objectivemask" };
        static readonly string[] NeverSolidWords = { "pebble", "grass", "bush", "flower", "tree", "foliage", "dandilion", "dandelion" };

        public readonly List<Renderer> Solid = new List<Renderer>();
        public readonly List<Renderer> Effects = new List<Renderer>();
        /// <summary>Scenery to colour on the map (e.g. red death bushes), one group per tint rule.</summary>
        public readonly List<TintGroup> Tints = new List<TintGroup>();
        /// <summary>The river's renderers (the game draws a river as a line along a spline).</summary>
        public readonly List<Renderer> Rivers = new List<Renderer>();

        public sealed class TintGroup
        {
            public string Word;
            public Rgba Color;
            public readonly List<Renderer> Renderers = new List<Renderer>();
        }

        sealed class Row
        {
            public int Count, Layer;
            public float Area, Tallest;
            public string Kind, Collider;
        }

        public static SceneryScan Run(float originX, float originZ, float maxX, float maxZ, string obstacleKeywordList, string tintList, bool writeReport)
        {
            var scan = new SceneryScan();
            string[] obstacleKeys = (obstacleKeywordList ?? "").Split(',').Select(k => k.Trim().ToLowerInvariant()).Where(k => k.Length > 0).ToArray();
            scan.Tints.AddRange(ParseTints(tintList));
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

                string kind = "";
                Collider col = null;
                if (Matches(EffectWords, mat, mesh, obj)) { scan.Effects.Add(r); kind = "effect"; }
                else
                {
                    if (b.size.y >= MinSolidHeight && !Matches(NeverSolidWords, mat, mesh, obj))
                    {
                        if (Matches(obstacleKeys, mat, mesh, obj)) kind = "SOLID";
                        else if ((col = SolidCollider(r.gameObject)) != null) kind = "SOLID";
                        if (kind == "SOLID") scan.Solid.Add(r);
                    }
                    foreach (var t in scan.Tints)
                        if (Matches(new[] { t.Word }, mat, mesh, obj))
                        {
                            t.Renderers.Add(r);
                            kind = kind.Length > 0 ? kind + "+tint" : "tint";
                            break;
                        }
                }

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

            try
            {
                var rivers = UObject.FindObjectsOfType<River>();
                for (int i = 0; i < rivers.Length; i++)
                {
                    var lr = rivers[i] != null ? rivers[i].riverRenderer : null;
                    if (lr != null && lr.enabled) scan.Rivers.Add(lr);
                }
            }
            catch (Exception e) { LogOnce.Error("finding the river", e); }

            Mod.Log.LogInfo($"Scenery: {scan.Solid.Count} rocks and walls, {scan.Effects.Count} ground effects hidden from pictures, "
                + $"{scan.Tints.Sum(t => t.Renderers.Count)} tinted, {scan.Rivers.Count} river(s).");

            if (rows != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Objects in the mapped area, {originX:0},{originZ:0} to {maxX:0},{maxZ:0}.");
                sb.AppendLine($"Obstacle keywords: {string.Join(", ", obstacleKeys)}.");
                sb.AppendLine("kind | material | mesh | object  ->  count, footprint m2, tallest, layer, collider(type@layer)");
                foreach (var kv in rows.OrderByDescending(kv => kv.Value.Area).Take(300))
                    sb.AppendLine($"{kv.Value.Kind,-6} | {kv.Key}  ->  {kv.Value.Count}, {kv.Value.Area:0}, {kv.Value.Tallest:0.0}, {kv.Value.Layer}, {kv.Value.Collider}");
                DebugImages.SaveText("scene-objects", sb.ToString());
            }
            return scan;
        }

        /// <summary>"deathbush:C8343C, lava:FF8800" as (word, colour) rules; bad entries are skipped with a warning.</summary>
        static IEnumerable<TintGroup> ParseTints(string list)
        {
            foreach (var entry in (list ?? "").Split(','))
            {
                if (entry.Trim().Length == 0) continue;
                var parts = entry.Split(':');
                string word = parts.Length == 2 ? parts[0].Trim().ToLowerInvariant() : "";
                string hex = parts.Length == 2 ? parts[1].Trim().TrimStart('#') : "";
                if (word.Length == 0 || hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
                {
                    Mod.Log.LogWarning($"Ignoring tint '{entry.Trim()}' (expected word:RRGGBB).");
                    continue;
                }
                yield return new TintGroup { Word = word, Color = new Rgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb) };
            }
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
