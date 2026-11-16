using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using SpellBrigadeMinimap.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SpellBrigadeMinimap
{
    /// <summary>
    /// Tracks which objectives the party has found, so the minimap only shows those. An objective counts as
    /// found once it has been on someone's screen. Discoveries go to the host, which relays them to everyone
    /// (and repeats the full list now and then for late joiners). Players without the mod are unaffected; if
    /// the host doesn't run it, each player only sees what they found themselves.
    /// </summary>
    internal sealed class ObjectiveDiscovery
    {
        const string MessageName = "SpellBrigadeMinimap.Discovered";
        const ulong ServerClientId = 0;
        const float ScreenMargin = 0.04f;    // fraction of the screen kept clear at each edge
        const float RebroadcastSeconds = 10f;

        static CustomMessagingManager.HandleNamedMessageDelegate handler; // kept alive for IL2CPP
        static ObjectiveDiscovery current;

        readonly DiscoverySet known = new DiscoverySet();
        IntPtr registeredOn;
        float nextRebroadcast;

        public bool IsKnown(ulong key) => known.Contains(key);

        public void Reset()
        {
            known.Clear();
            nextRebroadcast = 0f;
        }

        public void Tick(float now, List<Poi> pois)
        {
            current = this;
            NetworkManager nm = null;
            try
            {
                nm = NetworkManager.Singleton;
                EnsureHandler(nm);
            }
            catch (Exception e) { LogOnce.Error("setting up objective sharing", e); }

            var cam = Camera.main;
            if (cam != null)
                foreach (var poi in pois)
                {
                    if (poi.Kind != PoiKind.Objective || poi.Target == null || known.Contains(poi.Key)) continue;
                    if (!OnScreen(cam, poi.Target.position)) continue;
                    known.Add(poi.Key);
                    Share(nm, poi.Key.ToString("x"));
                }

            if (Hosting(nm) && known.Count > 0 && now >= nextRebroadcast)
            {
                nextRebroadcast = now + RebroadcastSeconds;
                Send(nm, known.Serialize(), toServer: false);
            }
        }

        static bool OnScreen(Camera cam, Vector3 world)
        {
            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z <= 0f) return false;
            float w = Screen.width, h = Screen.height;
            return s.x >= w * ScreenMargin && s.x <= w * (1f - ScreenMargin)
                && s.y >= h * ScreenMargin && s.y <= h * (1f - ScreenMargin);
        }

        static bool Connected(NetworkManager nm) => nm != null && nm.IsListening && nm.CustomMessagingManager != null;
        static bool Hosting(NetworkManager nm) => Connected(nm) && nm.IsServer;

        static void Share(NetworkManager nm, string payload)
        {
            if (!Connected(nm)) return;
            Send(nm, payload, toServer: !nm.IsServer);
        }

        static void Send(NetworkManager nm, string payload, bool toServer)
        {
            try
            {
                var writer = new FastBufferWriter(payload.Length * 2 + 64, Allocator.Temp, -1);
                try
                {
                    writer.WriteValueSafe(payload, false);
                    var cmm = nm.CustomMessagingManager;
                    if (toServer) cmm.SendNamedMessage(MessageName, ServerClientId, writer, NetworkDelivery.ReliableSequenced);
                    else cmm.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
                finally { writer.Dispose(); }
            }
            catch (Exception e) { LogOnce.Error("sharing found objectives", e); }
        }

        void EnsureHandler(NetworkManager nm)
        {
            if (!Connected(nm))
            {
                registeredOn = IntPtr.Zero;
                return;
            }
            var cmm = nm.CustomMessagingManager;
            if (registeredOn == cmm.Pointer) return;
            handler ??= DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(
                new Action<ulong, FastBufferReader>(OnReceived));
            cmm.RegisterNamedMessageHandler(MessageName, handler);
            registeredOn = cmm.Pointer;
        }

        static void OnReceived(ulong sender, FastBufferReader reader)
        {
            try
            {
                reader.ReadValueSafe(out string payload, false);
                var self = current;
                if (self == null) return;
                int added = self.known.Merge(payload);
                var nm = NetworkManager.Singleton;
                if (added > 0 && Hosting(nm)) Send(nm, payload, toServer: false); // relay to everyone
            }
            catch (Exception e) { LogOnce.Error("receiving found objectives", e); }
        }
    }
}
