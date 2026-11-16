using System;
using System.Collections.Generic;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>Small (red), big (orange), mini-boss (purple) or boss (own icon).</summary>
    public enum EnemyRank { Small, Big, MiniBoss, Boss }

    /// <summary>
    /// Sorts enemies by how tough their type is. "The swarm" is the weakest type seen at least
    /// <c>minCount</c> times in one scan; its base health only ever goes down during a run, so ranks don't
    /// flicker as waves change. An enemy is big when its type's base health is at least <c>bigFactor</c> times
    /// the swarm's. Bosses and mini-bosses are decided by the caller (the game flags bosses; mini-bosses are the
    /// elite types and a few named specials).
    /// </summary>
    public sealed class EnemyRanker
    {
        readonly float bigFactor;
        readonly int minCount;
        readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        readonly Dictionary<int, float> health = new Dictionary<int, float>();

        public EnemyRanker(float bigFactor, int minCount)
        {
            this.bigFactor = bigFactor;
            this.minCount = minCount;
        }

        /// <summary>Base health of the weakest common enemy type so far, or infinity before one is known.</summary>
        public float SwarmHealth { get; private set; } = float.PositiveInfinity;

        public void Reset()
        {
            counts.Clear();
            health.Clear();
            SwarmHealth = float.PositiveInfinity;
        }

        /// <summary>One enemy of <paramref name="type"/> seen in this scan.</summary>
        public void Observe(int type, float baseHealth)
        {
            counts.TryGetValue(type, out int n);
            counts[type] = n + 1;
            health[type] = baseHealth;
        }

        /// <summary>Updates the swarm's health from this scan's counts and starts a new scan.</summary>
        public void EndScan()
        {
            foreach (var kv in counts)
                if (kv.Value >= minCount && health[kv.Key] > 0f)
                    SwarmHealth = Math.Min(SwarmHealth, health[kv.Key]);
            counts.Clear();
        }

        public EnemyRank Rank(float baseHealth, bool isBoss, bool isMiniBoss)
        {
            if (isBoss) return EnemyRank.Boss;
            if (isMiniBoss) return EnemyRank.MiniBoss;
            if (!float.IsPositiveInfinity(SwarmHealth) && baseHealth >= SwarmHealth * bigFactor) return EnemyRank.Big;
            return EnemyRank.Small;
        }
    }
}
