using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class EnemyRankerTests
    {
        static EnemyRanker WithSwarm()
        {
            var r = new EnemyRanker(bigFactor: 3f, minCount: 5);
            for (int i = 0; i < 10; i++) r.Observe(1, 10f);   // the swarm
            for (int i = 0; i < 2; i++) r.Observe(2, 50f);    // a few big ones
            r.EndScan();
            return r;
        }

        public static void EnemiesWithManyTimesTheSwarmsHealthAreBig()
        {
            var r = WithSwarm();
            Check.Equal(EnemyRank.Big, r.Rank(50f, false, false), "50 vs swarm 10");
            Check.Equal(EnemyRank.Small, r.Rank(10f, false, false), "the swarm itself");
            Check.Equal(EnemyRank.Small, r.Rank(25f, false, false), "under 3x");
        }

        public static void RareWeakTypesDoNotLowerTheSwarmHealth()
        {
            var r = WithSwarm();
            r.Observe(3, 1f);
            r.Observe(3, 1f);
            r.EndScan();
            Check.Equal(10f, r.SwarmHealth, "two eggs don't count as the swarm");
        }

        public static void SwarmHealthOnlyGoesDown()
        {
            var r = WithSwarm();
            for (int i = 0; i < 8; i++) r.Observe(4, 40f);
            r.EndScan();
            Check.Equal(10f, r.SwarmHealth, "a later wave of tougher enemies doesn't raise it");
        }

        public static void BossesAndMiniBossesComeFirst()
        {
            var r = WithSwarm();
            Check.Equal(EnemyRank.Boss, r.Rank(10f, true, true), "boss");
            Check.Equal(EnemyRank.MiniBoss, r.Rank(10f, false, true), "mini-boss");
        }

        public static void EverythingIsSmallUntilTheSwarmIsKnown()
        {
            var r = new EnemyRanker(3f, 5);
            r.Observe(1, 10f);
            r.EndScan();
            Check.Equal(EnemyRank.Small, r.Rank(500f, false, false), "no swarm seen yet");
        }
    }
}
