using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SpellBrigadeMinimap.Core
{
    /// <summary>
    /// The objectives someone in the party has seen, by key. Serializes to a short comma-separated hex list
    /// for sending between players.
    /// </summary>
    public sealed class DiscoverySet
    {
        readonly HashSet<ulong> keys = new HashSet<ulong>();

        public int Count => keys.Count;
        public bool Contains(ulong key) => keys.Contains(key);
        /// <summary>Returns true if the key wasn't known yet.</summary>
        public bool Add(ulong key) => keys.Add(key);
        public void Clear() => keys.Clear();

        public string Serialize() => string.Join(",", keys.OrderBy(k => k).Select(k => k.ToString("x", CultureInfo.InvariantCulture)));

        /// <summary>Adds every valid key in a serialized list; ignores anything malformed. Returns how many were new.</summary>
        public int Merge(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return 0;
            int added = 0;
            foreach (string part in payload.Split(','))
            {
                string token = part.Trim();
                if (token.Length == 0) continue;
                if (ulong.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong key) && keys.Add(key))
                    added++;
            }
            return added;
        }
    }
}
