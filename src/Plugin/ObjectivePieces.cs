using System;
using System.Collections.Generic;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;
using UObject = UnityEngine.Object;

namespace SpellBrigadeMinimap
{
    internal enum PieceKind { Mushroom, Crate, Herd, Stone }

    /// <summary>One thing an objective wants found, pushed, herded or broken.</summary>
    internal struct Piece
    {
        public Transform Target;
        public PieceKind Kind;
        /// <summary>Already where it should be (in the zone / pen).</summary>
        public bool Done;
        /// <summary>Where it has to go, if anywhere.</summary>
        public bool HasGoal;
        public NVec2 Goal;
    }

    internal struct ObjectiveCount
    {
        public PieceKind Kind;
        public int Done, Total;
    }

    internal struct ObjectiveZone
    {
        public NVec2 Center;
        public float Radius;
    }

    /// <summary>
    /// The pieces of the running objectives, read from the game's objective managers: the collect objective's
    /// mushrooms, the push objective's objects and zone, the herding objective's animals and pen, the petrified
    /// enemies to break, plus every hunt target. Managers are looked up once a second; pieces every scan.
    /// </summary>
    internal sealed class ObjectivePieces
    {
        const float LookupSeconds = 1f;

        public readonly List<Piece> Pieces = new List<Piece>();
        public readonly List<ObjectiveCount> Counts = new List<ObjectiveCount>();
        public readonly List<ObjectiveZone> Zones = new List<ObjectiveZone>();
        public readonly List<Transform> HuntTargets = new List<Transform>();

        readonly HashSet<IntPtr> pieceTransforms = new HashSet<IntPtr>();
        readonly HashSet<string> logged = new HashSet<string>();
        CollectObjectiveManager collect;
        PushObjectiveManager push;
        HerdingObjectiveManager herd;
        PetrifiedEnemiesObjectiveManager petrified;
        float nextLookup;

        public void Reset()
        {
            Pieces.Clear();
            Counts.Clear();
            Zones.Clear();
            HuntTargets.Clear();
            pieceTransforms.Clear();
            collect = null;
            push = null;
            herd = null;
            petrified = null;
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
            try { ScanCollect(); } catch (Exception e) { LogOnce.Error("reading the collect objective", e); }
            try { ScanPush(); } catch (Exception e) { LogOnce.Error("reading the push objective", e); }
            try { ScanHerd(); } catch (Exception e) { LogOnce.Error("reading the herding objective", e); }
            try { ScanPetrified(); } catch (Exception e) { LogOnce.Error("reading the petrified enemies objective", e); }
        }

        /// <summary>True if <paramref name="t"/> belongs to a piece (so the game's own pointer at it isn't drawn twice).</summary>
        public bool IsPiece(Transform t)
        {
            for (int i = 0; i < 4 && t != null; i++, t = t.parent)
                if (pieceTransforms.Contains(t.Pointer)) return true;
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
            }
            catch (Exception e) { LogOnce.Error("finding the objectives", e); }

            HuntTargets.Clear();
            try
            {
                var hunted = UObject.FindObjectsOfType<HuntTarget>();
                for (int i = 0; i < hunted.Length; i++)
                    if (hunted[i] != null && hunted[i].isActiveAndEnabled) HuntTargets.Add(hunted[i].transform);
                if (HuntTargets.Count > 0) Once("hunt", $"Hunt objective: {HuntTargets.Count} target(s).");
            }
            catch (Exception e) { LogOnce.Error("finding hunt targets", e); }
        }

        static T Active<T>(T behaviour) where T : Behaviour => behaviour != null && behaviour.isActiveAndEnabled ? behaviour : null;

        void Add(Transform t, PieceKind kind, bool done = false, bool hasGoal = false, NVec2 goal = default)
        {
            Pieces.Add(new Piece { Target = t, Kind = kind, Done = done, HasGoal = hasGoal, Goal = goal });
            pieceTransforms.Add(t.Pointer);
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
            Zones.Add(new ObjectiveZone { Center = center, Radius = r });
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                Vector3 pos = p.transform.position;
                bool inZone = NVec2.Distance(new NVec2(pos.x, pos.z), center) <= r;
                Add(p.transform, PieceKind.Crate, inZone, true, center);
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
            NVec2 pen = default;
            bool havePen = false;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.isActiveAndEnabled) continue;
                Vector3 z = t.zonePosition;
                pen = new NVec2(z.x, z.z);
                havePen = true;
                Add(t.transform, PieceKind.Herd, t.IsInZone, true, pen);
            }
            if (!havePen)
            {
                Vector3 c = herd.transform.position;
                pen = new NVec2(c.x, c.z);
            }
            Zones.Add(new ObjectiveZone { Center = pen, Radius = HerdingObjectiveManager.ZoneRadius });
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
                    Add(e.transform, PieceKind.Stone);
                    left++;
                }
            int done = petrified.currentKilledAmount.Value, total = petrified.totalPetrifiedEnemiesTarget;
            if (left == 0 && total <= 0) return;
            Counts.Add(new ObjectiveCount { Kind = PieceKind.Stone, Done = done, Total = Math.Max(total, done) });
            Once("petrified", $"Petrified enemies objective: {left} standing, {done}/{total} broken.");
        }

        void Once(string key, string message)
        {
            if (logged.Add(key)) Mod.Log.LogInfo(message);
        }
    }
}
