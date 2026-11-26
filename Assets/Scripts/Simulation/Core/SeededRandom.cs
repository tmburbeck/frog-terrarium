using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Small, fast, deterministic random number generator (PCG32).
    /// The same seed always gives the same sequence on every machine, which makes
    /// map generation and bug reports repeatable. Its whole state is two numbers,
    /// so it can be written into save files and restored exactly.
    /// </summary>
    public sealed class SeededRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private ulong _increment;

        public SeededRandom(ulong seed, ulong stream = 54UL)
        {
            _state = 0UL;
            _increment = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        private SeededRandom() { }

        /// <summary>Recreate a generator from a saved state.</summary>
        public static SeededRandom FromState(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0UL)
                throw new ArgumentException("Increment must be odd.", nameof(increment));
            return new SeededRandom { _state = state, _increment = increment };
        }

        public ulong State => _state;
        public ulong Increment => _increment;

        /// <summary>A uniformly random 32-bit unsigned integer.</summary>
        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>A float in [0, 1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>A float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>An int in [min, max). Throws if max is not greater than min.</summary>
        public int Range(int min, int max)
        {
            if (max <= min) throw new ArgumentOutOfRangeException(nameof(max));
            uint span = (uint)(max - min);
            // Rejection sampling removes modulo bias.
            uint threshold = (uint)(-(int)span) % span;
            uint r;
            do { r = NextUInt(); } while (r < threshold);
            return min + (int)(r % span);
        }

        /// <summary>True with the given probability (0..1).</summary>
        public bool Chance(float probability) => NextFloat() < probability;
    }
}
