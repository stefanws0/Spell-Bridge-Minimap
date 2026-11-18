using System;
using System.Collections.Generic;
using System.Linq;
using SpellBrigadeMinimap.Core;
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
        public EnemyRank Rank;
    }

    internal enum PoiKind { Objective, Boss, Downed, Danger, Pickup, Health }

    /// <summary>An enemy the game is pointing at from the screen edge, with its rank.</summary>
    internal struct PointedEnemy
    {
        public Transform Target;
        public EnemyRank Rank;
    }

    internal enum FeatureKind { Portal }

    /// <summary>A fixed part of the level worth marking (a teleport portal).</summary>
    internal struct FeatureDot
    {
        public NVec2 Pos;
        public FeatureKind Kind;
        /// <summary>Open right now.</summary>
        public bool Active;
    }

    /// <summary>Ready to go off; building up or erupting; recharging.</summary>
    internal enum GeyserState { Ready, Erupting, Cooldown }

    internal struct GeyserDot
    {
        public NVec2 Pos;
        public GeyserState State;
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
        /// <summary>An order objective's mushroom (coloured by its state).</summary>
        public OrderObjectiveInteractable Pillar;
        /// <summary>The game is showing its pointer at this right now (at the screen edge, or over it on screen).</summary>
        public bool Shown;
        /// <summary>A white copy of the game's icon (with a dark edge), for objectives the minimap colours itself.</summary>
        public Sprite WhiteSprite;

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
        /// <summary>Every geyser and its state right now (updated every frame).</summary>
        public readonly List<GeyserDot> Geysers = new List<GeyserDot>();
        readonly Dictionary<IntPtr, TrackedGeyser> geysers = new Dictionary<IntPtr, TrackedGeyser>();
        int geyserLogs;

        sealed class TrackedGeyser
        {
            public GeyserController Controller;
            public NVec2 Pos;
            public bool WasTriggered;
            public float TriggeredAt = -1f;
            public float EruptSeconds, CycleSeconds;
        }

        readonly HashSet<int> eliteTypes = new HashSet<int>();
        float nextEnemyScan, nextPoiScan, nextFeatureScan;
        bool featuresLogged;

        /// <summary>The running objectives' pieces, zones, counts and hunt targets.</summary>
        public readonly ObjectivePieces Objectives = new ObjectivePieces();
        /// <summary>A log of what the game's screen pointers do (for checking in game).</summary>
        readonly PointerNotes notes = new PointerNotes();
        OrderObjective orderObjective;
        float nextOrderLookup;
        bool orderLogged;
        readonly HashSet<IntPtr> pillarsWithPointers = new HashSet<IntPtr>();
        readonly Dictionary<IntPtr, OrderObjectiveInteractable> pillarOfTarget = new Dictionary<IntPtr, OrderObjectiveInteractable>();
        Vector3 lastGhostPos;
        bool ghostPosValid;
        const float GhostTurnDistance = 0.02f;   // the ghost must move this far in a frame to turn the arrow

        /// <summary>Bosses right now (drawn as their own icon, not a dot).</summary>
        public readonly List<NVec2> Bosses = new List<NVec2>();
        /// <summary>Enemies the game points at from the screen edge right now (drawn by their rank's mark, pinned to the edge off the map).</summary>
        public readonly List<PointedEnemy> PointedEnemies = new List<PointedEnemy>();
        readonly Dictionary<IntPtr, Enemy> enemyOfTarget = new Dictionary<IntPtr, Enemy>();
        readonly Dictionary<IntPtr, bool> pieceObjectTargets = new Dictionary<IntPtr, bool>();
        EnemyRanker ranker = new EnemyRanker(3f, 8f, SwarmMinCount);
        readonly Dictionary<int, EnemyTypeInfo> typeInfo = new Dictionary<int, EnemyTypeInfo>();
        static readonly HashSet<int> loggedTypes = new HashSet<int>();
        readonly List<(Vector3 pos, EnemyTypeInfo info)> scanned = new List<(Vector3, EnemyTypeInfo)>();
        const int SwarmMinCount = 5;

        sealed class EnemyTypeInfo
        {
            public int Type;
            public float BaseHealth;
            public bool Boss, MiniBoss;
        }

        public void ResetRun(RunManager rm)
        {
            eliteTypes.Clear();
            Bosses.Clear();
            orderObjective = null;
            Objectives.Reset();
            GameIcons.Reset();
            notes.Reset();
            nextOrderLookup = 0f;
            orderLogged = false;
            pillarOfTarget.Clear();
            ghostPosValid = false;
            typeInfo.Clear();
            ranker = new EnemyRanker(Mod.Settings.BigEnemyHealth.Value, Mod.Settings.HeavyEnemyHealth.Value, SwarmMinCount);
            PointedEnemies.Clear();
            enemyOfTarget.Clear();
            pieceObjectTargets.Clear();
            Players.Clear();
            Enemies.Clear();
            Pois.Clear();
            Features.Clear();
            Tornadoes.Clear();
            tornadoTransforms.Clear();
            Geysers.Clear();
            geysers.Clear();
            geyserLogs = 0;
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
                Geysers.Clear();
            }
            else
            {
                if (now >= nextFeatureScan)
                {
                    nextFeatureScan = now + FeatureInterval;
                    ScanFeatures();
                }
                TrackTornadoes();
                TrackGeysers();
            }

            if (!wantEnemies) Enemies.Clear();
            else if (now >= nextEnemyScan)
            {
                nextEnemyScan = now + EnemyInterval;
                ScanEnemies();
            }

            if (!wantPois)
            {
                Pois.Clear();
                PointedEnemies.Clear();
            }
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
                        ghostPosValid = false;
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
                            // A ghost doesn't turn like the wizard does, so the arrow follows where it moves.
                            if (ghostPosValid)
                            {
                                float dx = pos.x - lastGhostPos.x, dz = pos.z - lastGhostPos.z;
                                if (dx * dx + dz * dz > GhostTurnDistance * GhostTurnDistance) Facing = new NVec2(dx, dz);
                            }
                            lastGhostPos = pos;
                            ghostPosValid = true;
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

        /// <summary>
        /// Enemies with their rank: bosses (the game's own boss flag), mini-bosses (elite types, the Hydra's
        /// healers, the Hoarder), big (base health several times the swarm's) and small.
        /// </summary>
        void ScanEnemies()
        {
            Enemies.Clear();
            Bosses.Clear();
            var all = Enemy.AllEnemies;
            if (all == null) return;
            int n = Math.Min(all.Count, MaxEnemies);
            scanned.Clear();
            for (int i = 0; i < n; i++)
            {
                var e = all[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                var info = InfoFor(e);
                ranker.Observe(info.Type, info.BaseHealth);
                scanned.Add((e.transform.position, info));
            }
            float swarmBefore = ranker.SwarmHealth;
            ranker.EndScan();
            if (ranker.SwarmHealth != swarmBefore)
                Mod.Log.LogInfo($"Swarm enemies have {ranker.SwarmHealth:0.#} base health; {Mod.Settings.BigEnemyHealth.Value:0.#}x that or more is drawn as a big enemy, {Mod.Settings.HeavyEnemyHealth.Value:0.#}x as a heavy one.");

            foreach (var (pos, info) in scanned)
            {
                var p = new NVec2(pos.x, pos.z);
                var rank = ranker.Rank(info.BaseHealth, info.Boss, info.MiniBoss);
                if (rank == EnemyRank.Boss) Bosses.Add(p);
                else Enemies.Add(new EnemyDot { Pos = p, Rank = rank });
            }
        }

        EnemyTypeInfo InfoFor(Enemy e)
        {
            var type = e.EnemyType;
            int key = (int)type;
            if (typeInfo.TryGetValue(key, out var info)) return info;
            info = new EnemyTypeInfo { Type = key };
            try
            {
                var hc = e.GetComponent<EnemyHealthContainer>() ?? e.GetComponentInChildren<EnemyHealthContainer>();
                if (hc != null)
                {
                    info.BaseHealth = hc.maxHealth;
                    info.Boss = hc.isBoss;
                }
            }
            catch (Exception ex) { LogOnce.Error("reading an enemy's health", ex); }
            string name = type.ToString();
            info.MiniBoss = eliteTypes.Contains(key) || type == EnemyType.HydraHealer || type == EnemyType.Hoarder
                || name.StartsWith("Elite", StringComparison.Ordinal);
            typeInfo[key] = info;
            if (loggedTypes.Add(key))
                Mod.Log.LogInfo($"Enemy {name}: base health {info.BaseHealth:0.#}{(info.Boss ? ", boss" : "")}{(info.MiniBoss ? ", mini-boss" : "")}.");
            return info;
        }

        void ScanPois()
        {
            Pois.Clear();
            PointedEnemies.Clear();
            pillarsWithPointers.Clear();
            RefreshOrderObjective();
            float now = Time.unscaledTime;
            Objectives.Scan(now);
            var spm = ScreenPointerManager.Instance;
            notes.Setup(spm);
            var pointers = spm != null ? spm.activePointers : null;
            if (pointers != null)
                foreach (var kv in pointers)
                {
                    Transform target = kv.Key;
                    ScreenPointer sp = kv.Value;
                    if (target == null || sp == null) continue;
                    bool shown = IsShowing(sp);
                    if (!TryKind(sp.pointerType, out PoiKind kind))
                    {
                        notes.Observe(target, sp, shown, "a player (drawn as a teammate)", now);
                        continue;
                    }
                    Sprite icon = FirstIcon(sp, out Color iconColor);
                    if (kind == PoiKind.Objective) GameIcons.Offer(icon);
                    if (kind == PoiKind.Objective && Objectives.ClaimPointer(target, shown, icon))
                    {
                        notes.Observe(target, sp, shown, "an objective piece (drawn as its piece icon)", now);
                        continue;
                    }
                    if (kind == PoiKind.Objective && IsPieceObject(target))
                    {
                        // A piece whose objective the minimap hasn't picked up yet (it looks objectives up once a
                        // second): skip it rather than draw the game's icon for one moment.
                        notes.Observe(target, sp, shown, "an objective piece not listed yet (skipped)", now);
                        continue;
                    }
                    if (kind == PoiKind.Objective && Objectives.IsHuntTarget(target, shown))
                    {
                        notes.Observe(target, sp, shown, "a hunt target (drawn as the game's hunt icon)", now);
                        continue;
                    }
                    if ((kind == PoiKind.Danger || kind == PoiKind.Boss) && TryEnemyPointer(target, shown, out EnemyRank rank))
                    {
                        // The game's "!" (or boss pointer) on an ordinary enemy: the enemy's own mark shows it instead.
                        notes.Observe(target, sp, shown, $"an enemy ({rank}, drawn as its mark)", now);
                        continue;
                    }
                    notes.Observe(target, sp, shown, kind.ToString(), now);

                    var poi = new Poi
                    {
                        Target = target, Kind = kind, Key = KeyOf(target), Shown = shown,
                        Sprite = icon, Color = icon != null ? iconColor : Color.white,
                    };
                    // Objectives are drawn as a white copy of the game's icon, coloured by the minimap.
                    if (kind == PoiKind.Objective) poi.WhiteSprite = IconRecolor.White(icon, target);
                    if (kind == PoiKind.Objective && orderObjective != null)
                    {
                        var pillar = PillarOf(target);
                        if (pillar != null)
                        {
                            poi.Pillar = pillar;
                            pillarsWithPointers.Add(pillar.Pointer);
                        }
                    }
                    Pois.Add(poi);
                }

            // The order objective's mushrooms the game doesn't point at.
            try
            {
                var list = orderObjective != null ? orderObjective.interactableObjects : null;
                if (list != null)
                    for (int i = 0; i < list.Count; i++)
                    {
                        var pillar = list[i];
                        if (pillar == null || !pillar.isActiveAndEnabled || pillarsWithPointers.Contains(pillar.Pointer)) continue;
                        Pois.Add(new Poi
                        {
                            Target = pillar.transform, Kind = PoiKind.Objective, Color = Color.white,
                            Key = KeyOf(pillar.transform), Pillar = pillar,
                        });
                    }
            }
            catch (Exception e) { LogOnce.Error("reading the order objective's mushrooms", e); }
        }

        /// <summary>Looks up the running order objective (once a second); null if there is none or it despawned.</summary>
        void RefreshOrderObjective()
        {
            float now = Time.unscaledTime;
            if (now >= nextOrderLookup)
            {
                nextOrderLookup = now + 1f;
                try { orderObjective = UnityEngine.Object.FindObjectOfType<OrderObjective>(); }
                catch (Exception e) { LogOnce.Error("finding the order objective", e); orderObjective = null; }
                if (orderObjective != null && !orderLogged)
                {
                    orderLogged = true;
                    var list = orderObjective.interactableObjects;
                    Mod.Log.LogInfo($"Order objective found with {(list != null ? list.Count : 0)} mushrooms.");
                }
            }
            if (orderObjective == null) return;
            try { _ = orderObjective.transform; }
            catch { orderObjective = null; }
        }

        OrderObjectiveInteractable PillarOf(Transform target)
        {
            IntPtr key = target.Pointer;
            if (pillarOfTarget.TryGetValue(key, out var p)) return p;
            try { p = target.GetComponentInParent<OrderObjectiveInteractable>(); }
            catch { p = null; }
            pillarOfTarget[key] = p;
            return p;
        }

        /// <summary>Geysers (the ice map's traps) and teleport portals, with which portals are open.</summary>
        void ScanFeatures()
        {
            Features.Clear();
            int portals = 0, open = 0;
            try
            {
                // Geysers stay put; keep each one's eruption timing across scans.
                var found = UnityEngine.Object.FindObjectsOfType<GeyserController>();
                var seen = new HashSet<IntPtr>();
                for (int i = 0; i < found.Length; i++)
                {
                    var g = found[i];
                    if (g == null || !g.isActiveAndEnabled) continue;
                    seen.Add(g.Pointer);
                    if (geysers.ContainsKey(g.Pointer)) continue;
                    Vector3 p = g.transform.position;
                    float erupt = Math.Max(0f, g.buildUpTimeInSeconds) + Math.Max(0f, g.activeTimeInSeconds);
                    geysers[g.Pointer] = new TrackedGeyser
                    {
                        Controller = g,
                        Pos = new NVec2(p.x, p.z),
                        EruptSeconds = erupt,
                        CycleSeconds = erupt + Math.Max(0f, g.cooldownTimeInSeconds),
                    };
                    if (geysers.Count == 1)
                        Mod.Log.LogInfo($"Geyser timing: build-up {g.buildUpTimeInSeconds:0.##} s, active {g.activeTimeInSeconds:0.##} s, cooldown {g.cooldownTimeInSeconds:0.##} s.");
                }
                foreach (var key in geysers.Keys.Where(k => !seen.Contains(k)).ToList()) geysers.Remove(key);
            }
            catch (Exception e) { LogOnce.Error("finding geysers", e); }
            int traps = geysers.Count;

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

        /// <summary>
        /// A geyser goes off when someone steps on it: the game sets isTriggered and runs build-up, eruption and
        /// cooldown for the times on the controller, on every player's game. The state is timed from the moment
        /// isTriggered turns on; it counts as recharging for as long as isTriggered stays on after the eruption.
        /// </summary>
        void TrackGeysers()
        {
            Geysers.Clear();
            float now = Time.time;
            foreach (var g in geysers.Values)
            {
                try
                {
                    if (g.Controller == null) continue;
                    bool triggered = g.Controller.isTriggered;
                    float since = g.TriggeredAt < 0f ? float.MaxValue : now - g.TriggeredAt;
                    if (triggered && !g.WasTriggered && since >= g.CycleSeconds)
                    {
                        g.TriggeredAt = now;
                        since = 0f;
                        if (geyserLogs++ < 4) Mod.Log.LogInfo($"Geyser at {g.Pos.X:0},{g.Pos.Y:0} went off.");
                    }
                    else if (!triggered && g.WasTriggered && geyserLogs++ < 8)
                        Mod.Log.LogInfo($"Geyser at {g.Pos.X:0},{g.Pos.Y:0} reset after {since:0.0} s.");
                    g.WasTriggered = triggered;

                    var state = since < g.EruptSeconds ? GeyserState.Erupting
                              : since < g.CycleSeconds || triggered ? GeyserState.Cooldown
                              : GeyserState.Ready;
                    Geysers.Add(new GeyserDot { Pos = g.Pos, State = state });
                }
                catch { } // destroyed between scans
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

        /// <summary>
        /// Whether the game is drawing this pointer right now: it's highlighted (the flash when an objective appears
        /// or is pinged, which shows even before an objective pointer's delay is up), its animator's "is shown" flag
        /// is on (what the player sees, set by the pointer's off-screen and on-screen rules), or <c>wasShown</c> is
        /// set. Before 1.2.2 only <c>wasShown</c> was read, and it never came on in the test runs. Plain reads, no
        /// patches.
        /// </summary>
        static bool IsShowing(ScreenPointer sp)
        {
            try
            {
                if (sp.isHighlighted || sp.wasShown) return true;
                return AnimatorShows(sp);
            }
            catch (Exception e) { LogOnce.Error("reading whether a screen pointer is shown", e); return false; }
        }

        static bool animatorBroken;

        /// <summary>The pointer animator's "is shown" flag (false if it has no active animator).</summary>
        internal static bool AnimatorShows(ScreenPointer sp)
        {
            if (animatorBroken) return false;
            try
            {
                var ap = sp.animationPlayer;
                var an = ap != null ? ap.animator : null;
                return an != null && an.isActiveAndEnabled && an.GetBool(ScreenPointerAnimationPlayer.IsShownParameter);
            }
            catch (Exception e)
            {
                animatorBroken = true;
                LogOnce.Error("reading a pointer's animator (falling back to its highlight and wasShown)", e);
                return false;
            }
        }

        /// <summary>
        /// True if the pointer's target is an enemy below boss rank (bosses keep their own icon). Its rank comes back
        /// in <paramref name="rank"/>, and while the pointer shows, the enemy is listed in <see cref="PointedEnemies"/>.
        /// </summary>
        bool TryEnemyPointer(Transform target, bool shown, out EnemyRank rank)
        {
            rank = EnemyRank.Small;
            Enemy enemy;
            try
            {
                IntPtr key = target.Pointer;
                if (!enemyOfTarget.TryGetValue(key, out enemy) || (enemy is object && enemy == null))
                {
                    if (enemyOfTarget.Count > 512) enemyOfTarget.Clear();
                    enemy = target.GetComponentInParent<Enemy>();
                    enemyOfTarget[key] = enemy;
                }
                if (enemy == null) return false;
                var info = InfoFor(enemy);
                rank = ranker.Rank(info.BaseHealth, info.Boss, info.MiniBoss);
                if (rank == EnemyRank.Boss) return false;
                if (shown) PointedEnemies.Add(new PointedEnemy { Target = enemy.transform, Rank = rank });
                return true;
            }
            catch (Exception e)
            {
                LogOnce.Error("checking whether a pointer is on an enemy", e);
                return false;
            }
        }

        /// <summary>True if the pointer's target is an objective piece: a mushroom, push object, herd animal or statue.</summary>
        bool IsPieceObject(Transform target)
        {
            IntPtr key = target.Pointer;
            if (pieceObjectTargets.TryGetValue(key, out bool isPiece)) return isPiece;
            try
            {
                isPiece = target.GetComponentInParent<Collectable>() != null || target.GetComponentInParent<HerdingTarget>() != null
                       || target.GetComponentInParent<PushObjectivePushable>() != null || target.GetComponentInParent<PetrifiedEnemy>() != null;
            }
            catch (Exception e)
            {
                LogOnce.Error("checking whether a pointer is on an objective piece", e);
                isPiece = false;
            }
            if (pieceObjectTargets.Count > 1024) pieceObjectTargets.Clear();
            pieceObjectTargets[key] = isPiece;
            return isPiece;
        }

        /// <summary>The game's icon on this pointer (its first image with a sprite), or null.</summary>
        static Sprite FirstIcon(ScreenPointer sp, out Color color)
        {
            color = Color.white;
            var images = sp.iconImages;
            if (images != null)
                for (int i = 0; i < images.Count; i++)
                {
                    var img = images[i];
                    if (img != null && img.sprite != null)
                    {
                        color = img.color;
                        return img.sprite;
                    }
                }
            return null;
        }

        internal static ulong KeyOf(Transform t)
        {
            try
            {
                var net = t.GetComponentInParent<NetworkObject>();
                if (net != null) return net.NetworkObjectId;
            }
            catch (Exception e) { LogOnce.Error("identifying an objective", e); }
            return KeyOfPosition(t.position);
        }

        /// <summary>A key for something without a NetworkObject that stays put: its rounded position.</summary>
        internal static ulong KeyOfPosition(Vector3 p)
        {
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
