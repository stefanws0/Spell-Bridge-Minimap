using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace SpellBrigadeMinimap
{
    internal struct EventMarker
    {
        public NVec2 Pos;
        public float Radius;
        public Color Color;
        /// <summary>True for an area (drawn as a ring), false for a point target (drawn as a diamond).</summary>
        public bool Zone;
    }

    /// <summary>
    /// Reads MoreEvents' active event markers by reflection when that mod is loaded.
    /// Shape in MoreEvents 1.0.6 (all internal): static MoreEvents.Net.Current (EventState) → field Markers
    /// (List&lt;Marker&gt;); Marker fields Kind (Ring=0, Follow=1, Icon=2, Safe=3), Pos, Radius, NetId, Color.
    /// </summary>
    internal sealed class MoreEventsBridge
    {
        const int KindRing = 0, KindFollow = 1, KindSafe = 3;

        bool resolved, available;
        FieldInfo current, markers, kind, pos, radius, netId, color;

        public void Collect(List<EventMarker> output)
        {
            output.Clear();
            if (!resolved) Resolve();
            if (!available) return;
            try
            {
                object state = current.GetValue(null);
                if (state == null) return;
                if (!(markers.GetValue(state) is IList list)) return;
                foreach (object m in list)
                {
                    if (m == null) continue;
                    int k = Convert.ToInt32(kind.GetValue(m));
                    var c = (Color)color.GetValue(m);
                    c.a = 1f;
                    if (k == KindFollow)
                    {
                        if (!TryNetworkObjectPosition((ulong)netId.GetValue(m), out Vector3 p)) continue;
                        output.Add(new EventMarker { Pos = new NVec2(p.x, p.z), Color = c });
                    }
                    else
                    {
                        var p = (Vector3)pos.GetValue(m);
                        output.Add(new EventMarker
                        {
                            Pos = new NVec2(p.x, p.z),
                            Radius = (float)radius.GetValue(m),
                            Color = c,
                            Zone = k == KindRing || k == KindSafe,
                        });
                    }
                }
            }
            catch (Exception e)
            {
                available = false;
                LogOnce.Error("reading MoreEvents markers (event markers turned off)", e);
            }
        }

        static bool TryNetworkObjectPosition(ulong id, out Vector3 position)
        {
            position = default;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return false;
            var spawned = nm.SpawnManager.SpawnedObjects;
            if (spawned == null || !spawned.ContainsKey(id)) return false;
            var obj = spawned[id];
            if (obj == null) return false;
            position = obj.transform.position;
            return true;
        }

        void Resolve()
        {
            resolved = true;
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "MoreEvents");
            if (asm == null)
            {
                Mod.Log.LogInfo("MoreEvents isn't installed; event markers are off.");
                return;
            }
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            current = asm.GetType("MoreEvents.Net")?.GetField("Current", any);
            markers = asm.GetType("MoreEvents.EventState")?.GetField("Markers", any);
            var marker = asm.GetType("MoreEvents.Marker");
            kind = marker?.GetField("Kind", any);
            pos = marker?.GetField("Pos", any);
            radius = marker?.GetField("Radius", any);
            netId = marker?.GetField("NetId", any);
            color = marker?.GetField("Color", any);
            available = current != null && markers != null && kind != null && pos != null
                     && radius != null && netId != null && color != null;
            if (available) Mod.Log.LogInfo("MoreEvents found; showing its event markers.");
            else LogOnce.Warn("moreevents-shape", "MoreEvents is installed but its marker fields changed; event markers are off.");
        }
    }
}
