using System;
using System.Collections.Generic;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    internal enum PieceKind { Mushroom, Crate, Herd, Statue, Hunt }

    /// <summary>One thing an objective wants found, pushed, herded, broken or hunted.</summary>
    internal struct Piece
    {
        public Transform Target;
        public PieceKind Kind;
        /// <summary>Already where it should be (in the zone / pen).</summary>
        public bool Done;
        /// <summary>Where it has to go, if anywhere.</summary>
        public bool HasGoal;
        public NVec2 Goal;
        /// <summary>Same on every player's game (its NetworkObject id), for sharing who has found it.</summary>
        public ulong Key;
        /// <summary>The key of the zone it has to go to (its objective's), so its guide line waits until that's found.</summary>
        public ulong GoalKey;
    }

    internal struct ObjectiveCount
    {
        public PieceKind Kind;
        public int Done, Total;
    }

    /// <summary>A push objective's zone or a herding objective's pen.</summary>
    internal struct ObjectiveZone
    {
        public NVec2 Center;
        public float Radius;
        /// <summary>Where it is in the world (for the found-on-screen check).</summary>
        public Vector3 World;
        /// <summary>Its objective's key: found once the objective or the zone itself has been seen.</summary>
        public ulong Key;
        /// <summary>The game's icon for it (<see cref="GameIcons.PushZone"/> or <see cref="GameIcons.HerdingZone"/>).</summary>
        public string Icon;
    }

    /// <summary>
    /// The pieces of the running objectives, read from the game's objective managers: the collect objective's
    /// mushrooms, the push objective's objects and zone, the herding objective's animals and pen, the petrified
    /// enemies to break, plus every hunt target, and the clean objective's goo colour. Managers are looked up once
    /// a second; pieces every scan.
    /// </summary>
    internal sealed class ObjectivePieces
    {
        const float LookupSeconds = 1f;

        public readonly List<Piece> Pieces = new List<Piece>();
        public readonly List<ObjectiveCount> Counts = new List<ObjectiveCount>();
        public readonly List<ObjectiveZone> Zones = new List<ObjectiveZone>();
        public readonly List<Piece> HuntTargets = new List<Piece>();
        /// <summary>The running clean objective's goo colour (see <see cref="ReadGooColour"/>), if there is one.</summary>
        public Color? GooColour { get; private set; }

        readonly Dictionary<IntPtr, PieceKind> pieceTransforms = new Dictionary<IntPtr, PieceKind>();
        readonly Dictionary<IntPtr, ulong> keys = new Dictionary<IntPtr, ulong>();
        /// <summary>White copies of the game's icons on each piece's own pointer (see <see cref="IconRecolor"/>).</summary>
        readonly Dictionary<IntPtr, Sprite> pieceIcons = new Dictionary<IntPtr, Sprite>();
        /// <summary>Pieces the game is pointing at from the screen edge (its hint pointers, or regular ones it shows).</summary>
        readonly HashSet<IntPtr> pointedPieces = new HashSet<IntPtr>();
        readonly HashSet<string> logged = new HashSet<string>();
        CollectObjectiveManager collect;
        PushObjectiveManager push;
        HerdingObjectiveManager herd;
        PetrifiedEnemiesObjectiveManager petrified;
        CleanObjectiveManager clean;
        float nextLookup;

        public void Reset()
        {
            Pieces.Clear();
            Counts.Clear();
            Zones.Clear();
            HuntTargets.Clear();
            GooColour = null;
            pieceTransforms.Clear();
            pointedPieces.Clear();
            pieceIcons.Clear();
            keys.Clear();
            collect = null;
            push = null;
            herd = null;
            petrified = null;
            clean = null;
            nextLookup = 0f;
        }

        public void Scan(float now)
        {
            if (now >= nextLookup)
            {
                nextLookup = now + LookupSeconds;
                Lookup();
            }
            Pieces.Clear();
            Counts.Clear();
            Zones.Clear();
            pieceTransforms.Clear();
            pointedPieces.Clear();
            try { ScanCollect(); } catch (Exception e) { LogOnce.Error("reading the collect objective", e); }
            try { ScanPush(); } catch (Exception e) { LogOnce.Error("reading the push objective", e); }
            try { ScanHerd(); } catch (Exception e) { LogOnce.Error("reading the herding objective", e); }
            try { ScanPetrified(); } catch (Exception e) { LogOnce.Error("reading the petrified enemies objective", e); }
        }

        /// <summary>
        /// True if <paramref name="t"/> belongs to a piece, so the game's own pointer at it isn't drawn twice. If the
        /// game is showing that pointer (<paramref name="shown"/>), the piece is remembered as pointed at for this scan.
        /// A white copy of the pointer's icon (<paramref name="icon"/>) is kept to draw the piece with.
        /// </summary>
        public bool ClaimPointer(Transform t, bool shown, Sprite icon)
        {
            for (int i = 0; i < 4 && t != null; i++, t = t.parent)
                if (pieceTransforms.TryGetValue(t.Pointer, out PieceKind _))
                {
                    if (shown) pointedPieces.Add(t.Pointer);
                    if (icon != null)
                    {
                        var white = IconRecolor.White(icon, t);
                        if (white != null)
                        {
                            if (pieceIcons.Count > 256) pieceIcons.Clear();
                            pieceIcons[t.Pointer] = white;
                        }
                    }
                    return true;
                }
            return false;
        }

        /// <summary>
        /// The white game icon to draw a piece of this kind with: its own pointer's, else the game's icon for the kind
        /// (pass a null <paramref name="piece"/> for the counters). Null if the game's icon isn't known (yet).
        /// </summary>
        public Sprite PieceIcon(Transform piece, PieceKind kind)
        {
            if (piece != null && pieceIcons.TryGetValue(piece.Pointer, out var s) && s != null) return s;
            return GameIcons.White(GameIcons.PieceName(kind));
        }

        /// <summary>True if the game is pointing at this piece from the screen edge (as of the last scan).</summary>
        public bool IsPointedAt(Transform pieceTarget) => pieceTarget != null && pointedPieces.Contains(pieceTarget.Pointer);

        /// <summary>
        /// True if <paramref name="t"/> belongs to a hunt target (drawn by the minimap, not as the game's pointer); if
        /// the game is showing that pointer, the target counts as pointed at.
        /// </summary>
        public bool IsHuntTarget(Transform t, bool shown)
        {
            for (int i = 0; i < 4 && t != null; i++, t = t.parent)
                for (int j = 0; j < HuntTargets.Count; j++)
                {
                    var target = HuntTargets[j].Target;
                    if (target == null || target.Pointer != t.Pointer) continue;
                    if (shown) pointedPieces.Add(target.Pointer);
                    return true;
                }
            return false;
        }

        void Lookup()
        {
            try
            {
                collect = Active(UObject.FindObjectOfType<CollectObjectiveManager>());
                push = Active(UObject.FindObjectOfType<PushObjectiveManager>());
                herd = Active(UObject.FindObjectOfType<HerdingObjectiveManager>());
                petrified = Active(UObject.FindObjectOfType<PetrifiedEnemiesObjectiveManager>());
                clean = Active(UObject.FindObjectOfType<CleanObjectiveManager>());
            }
            catch (Exception e) { LogOnce.Error("finding the objectives", e); }
            try { GooColour = ReadGooColour(); }
            catch (Exception e) { LogOnce.Error("reading the clean objective's goo colour", e); GooColour = null; }

            HuntTargets.Clear();
            try
            {
                var hunted = UObject.FindObjectsOfType<HuntTarget>();
                for (int i = 0; i < hunted.Length; i++)
                    if (hunted[i] != null && hunted[i].isActiveAndEnabled)
                    {
                        var t = hunted[i].transform;
                        HuntTargets.Add(new Piece { Target = t, Kind = PieceKind.Hunt, Key = KeyFor(t) });
                    }
                if (HuntTargets.Count > 0) Once("hunt", $"Hunt objective: {HuntTargets.Count} target(s).");
            }
            catch (Exception e) { LogOnce.Error("finding hunt targets", e); }
        }

        /// <summary>
        /// The clean objective's goo: the zone's starting colour, fully opaque. Dark sludge is lifted so it still
        /// shows on dark ground (its hue kept).
        /// </summary>
        Color? ReadGooColour()
        {
            if (clean == null || clean.isDespawning) return null;
            var zone = clean.cleanZone;
            if (zone == null) return null;
            Color c = zone.initialColor;
            Once("clean", $"Clean objective: {zone.cleanZoneVariant} goo, colours {Show(zone.initialColor)}, {Show(zone.initialColor1)}, {Show(zone.initialColor2)}.");
            c.a = 1f;
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            const float MinBrightness = 0.55f;
            if (max < 0.01f) return new Color(0.5f, 0.42f, 0.62f, 1f);       // black: a dusky purple
            if (max < MinBrightness) c = new Color(c.r * MinBrightness / max, c.g * MinBrightness / max, c.b * MinBrightness / max, 1f);
            return c;
        }

        static string Show(Color c) => $"({c.r:0.##}, {c.g:0.##}, {c.b:0.##}, {c.a:0.##})";

        static T Active<T>(T behaviour) where T : Behaviour => behaviour != null && behaviour.isActiveAndEnabled ? behaviour : null;

        ulong KeyFor(Transform t)
        {
            if (!keys.TryGetValue(t.Pointer, out ulong key))
            {
                if (keys.Count > 1024) keys.Clear();
                keys[t.Pointer] = key = WorldScanner.KeyOf(t);
            }
            return key;
        }

        void Add(Transform t, PieceKind kind, bool done = false, bool hasGoal = false, NVec2 goal = default, ulong goalKey = 0)
        {
            Pieces.Add(new Piece { Target = t, Kind = kind, Done = done, HasGoal = hasGoal, Goal = goal, Key = KeyFor(t), GoalKey = goalKey });
            pieceTransforms[t.Pointer] = kind;
        }

        void ScanCollect()
        {
            if (collect == null || collect.isDespawning) return;
            var list = collect.currentCollectables;
            int left = 0;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    if (c == null || !c.isActiveAndEnabled || c.hasBeenPickedUp) continue;
                    Add(c.transform, PieceKind.Mushroom);
                    left++;
                }
            int done = collect.currentCollectedAmount.Value, total = collect.totalCollectablesTarget;
            if (left == 0 && total <= 0) return;
            Counts.Add(new ObjectiveCount { Kind = PieceKind.Mushroom, Done = done, Total = Math.Max(total, done) });
            Once("collect", $"Collect objective: {left} mushrooms out, {done}/{total} collected.");
        }

        void ScanPush()
        {
            if (push == null || push.isDespawning) return;
            var list = push.spawnedPushables;
            if (list == null || list.Count == 0) return;
            Vector3 c = push.transform.position;
            var center = new NVec2(c.x, c.z);
            float r = push.zoneRadius;
            ulong zoneKey = KeyFor(push.transform);
            Zones.Add(new ObjectiveZone { Center = center, Radius = r, World = c, Key = zoneKey, Icon = GameIcons.PushZone });
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                Vector3 pos = p.transform.position;
                bool inZone = NVec2.Distance(new NVec2(pos.x, pos.z), center) <= r;
                Add(p.transform, PieceKind.Crate, inZone, true, center, zoneKey);
            }
            int total = push.totalPushables > 0 ? push.totalPushables : list.Count;
            Counts.Add(new ObjectiveCount { Kind = PieceKind.Crate, Done = push.numberOfPushablesInZone.Value, Total = total });
            Once("push", $"Push objective: zone at {c.x:0},{c.z:0} radius {r:0.#}, {list.Count} objects.");
        }

        void ScanHerd()
        {
            if (herd == null || herd.isDespawning) return;
            var list = herd.currentHerdingTargets;
            if (list == null || list.Count == 0) return;
            ulong penKey = KeyFor(herd.transform);
            Vector3 penWorld = herd.transform.position;
            bool havePen = false;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.isActiveAndEnabled) continue;
                penWorld = t.zonePosition;
                havePen = true;
                Add(t.transform, PieceKind.Herd, t.IsInZone, true, new NVec2(penWorld.x, penWorld.z), penKey);
            }
            if (!havePen) penWorld = herd.transform.position;
            var pen = new NVec2(penWorld.x, penWorld.z);
            Zones.Add(new ObjectiveZone { Center = pen, Radius = HerdingObjectiveManager.ZoneRadius, World = penWorld, Key = penKey, Icon = GameIcons.HerdingZone });
            int total = herd.numberOfHuntingTargets > 0 ? herd.numberOfHuntingTargets : list.Count;
            Counts.Add(new ObjectiveCount { Kind = PieceKind.Herd, Done = herd.herdingTargetsInTrigger.Value, Total = total });
            Once("herd", $"Herding objective: pen at {pen.X:0},{pen.Y:0} radius {HerdingObjectiveManager.ZoneRadius:0.#}, {list.Count} animals.");
        }

        void ScanPetrified()
        {
            if (petrified == null || petrified.isDespawning) return;
            var list = petrified.petrifiedEnemies;
            int left = 0;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e == null || !e.isActiveAndEnabled) continue;
                    Add(e.transform, PieceKind.Statue);
                    left++;
                }
            int done = petrified.currentKilledAmount.Value, total = petrified.totalPetrifiedEnemiesTarget;
            if (left == 0 && total <= 0) return;
            Counts.Add(new ObjectiveCount { Kind = PieceKind.Statue, Done = done, Total = Math.Max(total, done) });
            Once("petrified", $"Petrified enemies objective: {left} standing, {done}/{total} broken.");
        }

        void Once(string key, string message)
        {
            if (logged.Add(key)) Mod.Log.LogInfo(message);
        }
    }
}
