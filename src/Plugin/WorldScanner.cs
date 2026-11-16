using System;
using System.Collections.Generic;
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

    internal enum PoiKind { Objective, Boss, Downed, Danger, Pickup }

    /// <summary>A point of interest the game itself draws an off-screen pointer for.</summary>
    internal sealed class Poi
    {
        public Transform Target;
        public PoiKind Kind;
        public Sprite Sprite;
        public Color Color;

        public bool ClampToEdge => Kind == PoiKind.Objective || Kind == PoiKind.Boss || Kind == PoiKind.Downed;
    }

    /// <summary>Collects what the minimap draws from the game's own registries.</summary>
    internal sealed class WorldScanner
    {
        const float EnemyInterval = 1f / 15f;
        const float PoiInterval = 0.2f;
        const int MaxEnemies = 1500;
        static readonly Color DefaultTeammate = new Color(0.3f, 0.85f, 1f, 1f);

        public bool HasCenter;
        public NVec2 Center;
        public NVec2 Facing = new NVec2(0f, 1f);
        public readonly List<PlayerDot> Players = new List<PlayerDot>();
        public readonly List<EnemyDot> Enemies = new List<EnemyDot>();
        public readonly List<Poi> Pois = new List<Poi>();

        readonly HashSet<int> eliteTypes = new HashSet<int>();
        float nextEnemyScan, nextPoiScan;

        public void ResetRun(RunManager rm)
        {
            eliteTypes.Clear();
            Players.Clear();
            Enemies.Clear();
            Pois.Clear();
            nextEnemyScan = nextPoiScan = 0f;
            HasCenter = false;
            AddElites(rm.Configuration);
            AddElites(rm.verdantMeadowsConfiguration);
            AddElites(rm.frozenAncientsConfiguration);
            AddElites(rm.pyrestormPitConfiguration);
            AddElites(rm.astralPlanesConfiguration);
        }

        public void Tick(float now, bool wantEnemies, bool wantPois)
        {
            ScanPlayers();

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

                var poi = new Poi { Target = target, Kind = kind, Color = Color.white };
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

        static bool TryKind(PointerType t, out PoiKind kind)
        {
            switch (t)
            {
                case PointerType.Objective:
                case PointerType.ObjectiveComponent: kind = PoiKind.Objective; return true;
                case PointerType.Boss: kind = PoiKind.Boss; return true;
                case PointerType.ReviveZone: kind = PoiKind.Downed; return true;
                case PointerType.Danger: kind = PoiKind.Danger; return true;
                case PointerType.ArtifactPickup:
                case PointerType.HealthPickup:
                case PointerType.MagnetPickup: kind = PoiKind.Pickup; return true;
                default: kind = PoiKind.Objective; return false; // Player pointers: players are drawn separately.
            }
        }
    }
}
