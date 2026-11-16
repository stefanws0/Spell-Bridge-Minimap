using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace SpellBrigadeMinimap
{
    internal struct PlayerDot
    {
        public NVec2 Pos;
        public Color Color;
        public bool Ghost;
    }

    internal struct EnemyDot
    {
        public NVec2 Pos;
        public bool Elite;
    }

    internal enum PoiKind { Objective, Boss, Downed, Danger, Pickup, Health }

    internal enum FeatureKind { Trap, Portal }

    /// <summary>A fixed part of the level worth marking: a trap (geyser) or a teleport portal.</summary>
    internal struct FeatureDot
    {
        public NVec2 Pos;
        public FeatureKind Kind;
        /// <summary>Portals: open right now. Traps: always true.</summary>
        public bool Active;
    }

    /// <summary>A point of interest the game itself draws an off-screen pointer for.</summary>
    internal sealed class Poi
    {
        public Transform Target;
        public PoiKind Kind;
        /// <summary>Same on every player's game: the target's NetworkObject id, or its rounded position.</summary>
        public ulong Key;
        public Sprite Sprite;
        public Color Color;

        public bool ClampToEdge => Kind == PoiKind.Objective || Kind == PoiKind.Boss || Kind == PoiKind.Downed;
    }

    /// <summary>Collects what the minimap draws from the game's own registries.</summary>
    internal sealed class WorldScanner
    {
        const float EnemyInterval = 1f / 15f;
        const float PoiInterval = 0.2f;
        const float FeatureInterval = 1f;
        const int MaxEnemies = 1500;
        static readonly Color DefaultTeammate = new Color(0.3f, 0.85f, 1f, 1f);

        public bool HasCenter;
        public NVec2 Center;
        /// <summary>The local player's ground height, used to seed NavMesh mapping.</summary>
        public float CenterY;
        public NVec2 Facing = new NVec2(0f, 1f);
        public readonly List<PlayerDot> Players = new List<PlayerDot>();
        public readonly List<EnemyDot> Enemies = new List<EnemyDot>();
        public readonly List<Poi> Pois = new List<Poi>();
        public readonly List<FeatureDot> Features = new List<FeatureDot>();
        /// <summary>Where each tornado is right now (updated every frame; the list of tornadoes once a second).</summary>
        public readonly List<NVec2> Tornadoes = new List<NVec2>();
        readonly List<Transform> tornadoTransforms = new List<Transform>();

        readonly HashSet<int> eliteTypes = new HashSet<int>();
        float nextEnemyScan, nextPoiScan, nextFeatureScan;
        bool featuresLogged;

        public void ResetRun(RunManager rm)
        {
            eliteTypes.Clear();
            Players.Clear();
            Enemies.Clear();
            Pois.Clear();
            Features.Clear();
            Tornadoes.Clear();
            tornadoTransforms.Clear();
            featuresLogged = false;
            nextEnemyScan = nextPoiScan = nextFeatureScan = 0f;
            HasCenter = false;
            AddElites(rm.Configuration);
            AddElites(rm.verdantMeadowsConfiguration);
            AddElites(rm.frozenAncientsConfiguration);
            AddElites(rm.pyrestormPitConfiguration);
            AddElites(rm.astralPlanesConfiguration);
        }

        public void Tick(float now, bool wantEnemies, bool wantPois, bool wantFeatures)
        {
            ScanPlayers();

            if (!wantFeatures)
            {
                Features.Clear();
                Tornadoes.Clear();
            }
            else
            {
                if (now >= nextFeatureScan)
                {
                    nextFeatureScan = now + FeatureInterval;
                    ScanFeatures();
                }
                TrackTornadoes();
            }

            if (!wantEnemies) Enemies.Clear();
            else if (now >= nextEnemyScan)
            {
                nextEnemyScan = now + EnemyInterval;
                ScanEnemies();
            }

            if (!wantPois) Pois.Clear();
            else if (now >= nextPoiScan)
            {
                nextPoiScan = now + PoiInterval;
                ScanPois();
            }
        }

        void AddElites(RunConfiguration cfg)
        {
            try
            {
                if (cfg == null) return;
                var elites = cfg.EliteEnemies;
                if (elites == null) return;
                for (int i = 0; i < elites.Count; i++)
                {
                    var e = elites[i];
                    if (e == null) continue;
                    var weighted = e.weightedEnemies;
                    if (weighted == null) continue;
                    for (int j = 0; j < weighted.Count; j++)
                    {
                        var w = weighted[j];
                        if (w != null) eliteTypes.Add((int)w.Enemy);
                    }
                }
            }
            catch (Exception ex) { LogOnce.Error("reading elite enemy types", ex); }
        }

        void ScanPlayers()
        {
            Players.Clear();
            var mgr = GameplayPlayerManager.Instance;
            if (mgr == null)
            {
                HasCenter = false;
                return;
            }

            bool found = false;
            var alive = mgr.AlivePlayers;
            if (alive != null)
                for (int i = 0; i < alive.Count; i++)
                {
                    var p = alive[i];
                    if (p == null || !p.isActiveAndEnabled) continue;
                    Vector3 pos = p.GetPosition();
                    if (p.IsOwner)
                    {
                        found = true;
                        Center = new NVec2(pos.x, pos.z);
                        CenterY = pos.y;
                        var vt = p.visualTransform;
                        Vector3 f = (vt != null ? vt : p.transform).forward;
                        if (f.x * f.x + f.z * f.z > 1e-4f) Facing = new NVec2(f.x, f.z);
                    }
                    else
                    {
                        Players.Add(new PlayerDot { Pos = new NVec2(pos.x, pos.z), Color = ColorOf(mgr, p.OwnerClientId) });
                    }
                }

            var ghosts = mgr.PlayerGhosts;
            if (ghosts != null)
                for (int i = 0; i < ghosts.Count; i++)
                {
                    var g = ghosts[i];
                    if (g == null || !g.isActiveAndEnabled) continue;
                    Vector3 pos = g.transform.position;
                    if (g.IsOwner)
                    {
                        if (!found)
                        {
                            found = true;
                            Center = new NVec2(pos.x, pos.z);
                            CenterY = pos.y;
                        }
                    }
                    else
                    {
                        Players.Add(new PlayerDot { Pos = new NVec2(pos.x, pos.z), Color = ColorOf(mgr, g.OwnerClientId), Ghost = true });
                    }
                }

            // While the local player is between "downed" and "ghost spawned", keep the last center.
            HasCenter = found || HasCenter;
        }

        static Color ColorOf(GameplayPlayerManager mgr, ulong clientId)
        {
            try
            {
                var info = mgr.GetPlayerDisplayInfo(clientId);
                if (info != null)
                {
                    Color c = info.Color;
                    c.a = 1f;
                    return c;
                }
            }
            catch (Exception ex) { LogOnce.Error("reading player colors", ex); }
            return DefaultTeammate;
        }

        void ScanEnemies()
        {
            Enemies.Clear();
            var all = Enemy.AllEnemies;
            if (all == null) return;
            int n = Math.Min(all.Count, MaxEnemies);
            for (int i = 0; i < n; i++)
            {
                var e = all[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                Vector3 pos = e.transform.position;
                Enemies.Add(new EnemyDot
                {
                    Pos = new NVec2(pos.x, pos.z),
                    Elite = eliteTypes.Contains((int)e.EnemyType),
                });
            }
        }

        void ScanPois()
        {
            Pois.Clear();
            var spm = ScreenPointerManager.Instance;
            if (spm == null) return;
            var pointers = spm.activePointers;
            if (pointers == null) return;
            foreach (var kv in pointers)
            {
                Transform target = kv.Key;
                ScreenPointer sp = kv.Value;
                if (target == null || sp == null) continue;
                if (!TryKind(sp.pointerType, out PoiKind kind)) continue;

                var poi = new Poi { Target = target, Kind = kind, Color = Color.white, Key = KeyOf(target) };
                var images = sp.iconImages;
                if (images != null)
                    for (int i = 0; i < images.Count; i++)
                    {
                        var img = images[i];
                        if (img != null && img.sprite != null)
                        {
                            poi.Sprite = img.sprite;
                            poi.Color = img.color;
                            break;
                        }
                    }
                Pois.Add(poi);
            }
        }

        /// <summary>Geysers (the ice map's traps) and teleport portals, with which portals are open.</summary>
        void ScanFeatures()
        {
            Features.Clear();
            int traps = 0, portals = 0, open = 0;
            try
            {
                var geysers = UnityEngine.Object.FindObjectsOfType<GeyserController>();
                for (int i = 0; i < geysers.Length; i++)
                {
                    var g = geysers[i];
                    if (g == null || !g.isActiveAndEnabled) continue;
                    Vector3 p = g.transform.position;
                    Features.Add(new FeatureDot { Pos = new NVec2(p.x, p.z), Kind = FeatureKind.Trap, Active = true });
                    traps++;
                }
            }
            catch (Exception e) { LogOnce.Error("finding geysers", e); }

            try
            {
                var orchestrator = UnityEngine.Object.FindObjectOfType<PortalOrchestrator>();
                var list = orchestrator != null ? orchestrator.portals : null;
                var active = orchestrator != null ? orchestrator.activePortalIndices : null;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var portal = list[i];
                        if (portal == null || !portal.gameObject.activeInHierarchy) continue;
                        bool isOpen = active != null && active.Contains(i) && !portal.isClosing;
                        AddPortal(portal, isOpen);
                    }
                }
                else
                {
                    var all = UnityEngine.Object.FindObjectsOfType<Portal>();
                    for (int i = 0; i < all.Length; i++)
                        if (all[i] != null) AddPortal(all[i], true);
                }

                void AddPortal(Portal portal, bool isOpen)
                {
                    Vector3 p = portal.transform.position;
                    Features.Add(new FeatureDot { Pos = new NVec2(p.x, p.z), Kind = FeatureKind.Portal, Active = isOpen });
                    portals++;
                    if (isOpen) open++;
                }
            }
            catch (Exception e) { LogOnce.Error("finding teleport portals", e); }

            tornadoTransforms.Clear();
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<Tornado>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && all[i].isActiveAndEnabled) tornadoTransforms.Add(all[i].transform);
            }
            catch (Exception e) { LogOnce.Error("finding tornadoes", e); }

            if (!featuresLogged && (traps > 0 || portals > 0 || tornadoTransforms.Count > 0))
            {
                featuresLogged = true;
                Mod.Log.LogInfo($"Map features: {traps} geysers, {portals} portals ({open} open), {tornadoTransforms.Count} tornadoes.");
            }
        }

        void TrackTornadoes()
        {
            Tornadoes.Clear();
            for (int i = 0; i < tornadoTransforms.Count; i++)
            {
                try
                {
                    var t = tornadoTransforms[i];
                    if (t == null) continue;
                    Vector3 p = t.position;
                    Tornadoes.Add(new NVec2(p.x, p.z));
                }
                catch { } // destroyed between scans
            }
        }

        static ulong KeyOf(Transform t)
        {
            try
            {
                var net = t.GetComponentInParent<NetworkObject>();
                if (net != null) return net.NetworkObjectId;
            }
            catch (Exception e) { LogOnce.Error("identifying an objective", e); }
            Vector3 p = t.position;
            ulong x = (ulong)((long)Math.Round(p.x) & 0xFFFFFFF), z = (ulong)((long)Math.Round(p.z) & 0xFFFFFFF);
            return 0x8000000000000000UL | (x << 28) | z;
        }

        static bool TryKind(PointerType t, out PoiKind kind)
        {
            switch (t)
            {
                case PointerType.Objective:
                case PointerType.ObjectiveComponent: kind = PoiKind.Objective; return true;
                case PointerType.Boss: kind = PoiKind.Boss; return true;
                case PointerType.ReviveZone: kind = PoiKind.Downed; return true;
                case PointerType.Danger: kind = PoiKind.Danger; return true;
                case PointerType.HealthPickup: kind = PoiKind.Health; return true;
                case PointerType.ArtifactPickup:
                case PointerType.MagnetPickup: kind = PoiKind.Pickup; return true;
                default: kind = PoiKind.Objective; return false; // Player pointers: players are drawn separately.
            }
        }
    }
}
