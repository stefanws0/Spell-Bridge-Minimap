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
    /// Tracks which objectives, and which of their pieces, the party has found, so the minimap only shows those.
    /// Each one counts as found once it has been on someone's screen, or once the game shows its screen-edge
    /// pointer at it.
    /// Discoveries go to the host, which relays them to everyone
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

        const int MaxFoundLogs = 12;         // per run

        readonly DiscoverySet known = new DiscoverySet();
        IntPtr registeredOn;
        float nextRebroadcast;
        int foundLogs;

        public bool IsKnown(ulong key) => known.Contains(key);

        public void Reset()
        {
            known.Clear();
            nextRebroadcast = 0f;
            foundLogs = 0;
        }

        public void Tick(float now, List<Poi> pois, ObjectivePieces pieces)
        {
            current = this;
            NetworkManager nm = null;
            try
            {
                nm = NetworkManager.Singleton;
                EnsureHandler(nm);
            }
            catch (Exception e) { LogOnce.Error("setting up objective sharing", e); }

            // Found once it has been on screen, or once the game itself shows its pointer (at the screen edge),
            // since by then the game has told the player where it is anyway.
            var cam = Camera.main;
            // Pickups (artifacts, magnets, potions) too: the game only points at those for the bird mage.
            foreach (var poi in pois)
            {
                bool counts = poi.Kind == PoiKind.Objective || poi.Kind == PoiKind.Pickup || poi.Kind == PoiKind.Health;
                if (!counts || poi.Target == null || known.Contains(poi.Key)) continue;
                bool seen = cam != null && OnScreen(cam, poi.Target.position);
                if (!seen && !poi.Shown) continue;
                known.Add(poi.Key);
                if (foundLogs++ < MaxFoundLogs)
                    Mod.Log.LogInfo($"{poi.Kind} '{poi.Target.name}' found: {(seen ? "on screen" : "the game points at it")}.");
                Share(nm, poi.Key.ToString("x"));
            }

            // Every objective's pieces (mushrooms, objects to push, llamas, statues, hunt targets) follow the same
            // rule, each one on its own: a llama nobody has seen yet stays off the map even when the rest of its herd
            // is on it.
            foreach (var piece in pieces.Pieces) TryFind(piece, pieces, cam, nm);
            foreach (var piece in pieces.HuntTargets) TryFind(piece, pieces, cam, nm);

            // A push zone or herding pen: found with its objective (same key), or once it has been on screen itself.
            foreach (var zone in pieces.Zones)
            {
                if (known.Contains(zone.Key) || cam == null || !OnScreen(cam, zone.World)) continue;
                known.Add(zone.Key);
                if (foundLogs++ < MaxFoundLogs)
                    Mod.Log.LogInfo($"{(zone.Icon == GameIcons.PushZone ? "Push zone" : "Herding pen")} found: on screen.");
                Share(nm, zone.Key.ToString("x"));
            }

            if (Hosting(nm) && known.Count > 0 && now >= nextRebroadcast)
            {
                nextRebroadcast = now + RebroadcastSeconds;
                Send(nm, known.Serialize(), toServer: false);
            }
        }

        void TryFind(Piece piece, ObjectivePieces pieces, Camera cam, NetworkManager nm)
        {
            if (piece.Target == null || known.Contains(piece.Key)) return;
            bool seen = cam != null && OnScreen(cam, piece.Target.position);
            if (!seen && !pieces.IsPointedAt(piece.Target)) return;
            known.Add(piece.Key);
            if (foundLogs++ < MaxFoundLogs)
                Mod.Log.LogInfo($"{piece.Kind} '{piece.Target.name}' found: {(seen ? "on screen" : "the game points at it")}.");
            Share(nm, piece.Key.ToString("x"));
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
