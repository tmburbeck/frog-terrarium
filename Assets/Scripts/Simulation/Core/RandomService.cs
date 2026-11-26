using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Hands out one independent, seeded random stream per system ("movement",
    /// "mapgen", "genetics" ...). Streams are derived from the world seed and the
    /// stream name, so adding a new system never changes the numbers an existing
    /// system receives. That keeps old seeds and bug repros stable.
    /// </summary>
    public sealed class RandomService
    {
        private readonly Dictionary<string, SeededRandom> _streams = new Dictionary<string, SeededRandom>();

        public RandomService(ulong worldSeed)
        {
            WorldSeed = worldSeed;
        }

        public ulong WorldSeed { get; }

        public SeededRandom Get(string streamName)
        {
            if (!_streams.TryGetValue(streamName, out var rng))
            {
                ulong h = Fnv1a64(streamName);
                rng = new SeededRandom(WorldSeed ^ h, h);
                _streams.Add(streamName, rng);
            }
            return rng;
        }

        /// <summary>All streams created so far, for saving. Order is sorted by name.</summary>
        public IEnumerable<KeyValuePair<string, SeededRandom>> Streams
        {
            get
            {
                var names = new List<string>(_streams.Keys);
                names.Sort(System.StringComparer.Ordinal);
                foreach (var n in names) yield return new KeyValuePair<string, SeededRandom>(n, _streams[n]);
            }
        }

        /// <summary>Replace a stream with a restored one (used by load).</summary>
        public void Restore(string streamName, SeededRandom rng) => _streams[streamName] = rng;

        /// <summary>Stable 64-bit string hash (FNV-1a), identical on every platform.</summary>
        public static ulong Fnv1a64(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }
    }
}
