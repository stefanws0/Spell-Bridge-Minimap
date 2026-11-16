using SpellBrigadeMinimap.Core;

namespace SpellBrigadeMinimap.Tests
{
    public static class DiscoverySetTests
    {
        public static void AddReportsNewKeysOnly()
        {
            var s = new DiscoverySet();
            Check.True(s.Add(5), "new key");
            Check.True(!s.Add(5), "repeat");
            Check.Equal(1, s.Count, "count");
        }

        public static void SerializeRoundTrips()
        {
            var a = new DiscoverySet();
            a.Add(1);
            a.Add(0xDEADBEEFUL);
            a.Add(ulong.MaxValue);
            var b = new DiscoverySet();
            Check.Equal(3, b.Merge(a.Serialize()), "added");
            Check.True(b.Contains(1) && b.Contains(0xDEADBEEFUL) && b.Contains(ulong.MaxValue), "all keys");
        }

        public static void MergeIgnoresJunkAndKnownKeys()
        {
            var s = new DiscoverySet();
            s.Add(10);
            Check.Equal(1, s.Merge("a, zz,,  b ,xyz"), "only b (11) is new and valid");
            Check.True(s.Contains(11), "b added");
        }

        public static void MergeOfNothingAddsNothing()
        {
            var s = new DiscoverySet();
            Check.Equal(0, s.Merge(""), "empty");
            Check.Equal(0, s.Merge(null), "null");
        }

        public static void ClearForgetsEverything()
        {
            var s = new DiscoverySet();
            s.Add(3);
            s.Clear();
            Check.True(!s.Contains(3), "forgotten");
            Check.Equal(0, s.Count, "count");
        }
    }
}
