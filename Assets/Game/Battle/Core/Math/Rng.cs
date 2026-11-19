namespace RingsideHearts.Battle.Core
{
    /// <summary>Small seeded xorshift32 generator. Same seed, same sequence on every runtime.</summary>
    public sealed class Rng
    {
        uint _state;

        public Rng(int seed)
        {
            _state = seed == 0 ? 1u : (uint)seed;
        }

        uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[0, 1)</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>[0, maxExclusive)</summary>
        public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

        /// <summary>[min, max)</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();
    }
}
