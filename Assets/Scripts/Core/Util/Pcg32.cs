using System;

namespace Casino.Core
{
    public interface IRandom
    {
        /// <summary>Uniform integer in [0, maxExclusive).</summary>
        int Next(int maxExclusive);

        /// <summary>Uniform double in [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>PCG-XSH-RR 32-bit generator: small, fast and statistically solid, and seedable for tests.</summary>
    public sealed class Pcg32 : IRandom
    {
        const ulong Multiplier = 6364136223846793005UL;
        ulong state;
        readonly ulong increment;

        public Pcg32(ulong seed, ulong sequence = 0xda3e39cb94b95bdbUL)
        {
            increment = (sequence << 1) | 1UL;
            state = 0;
            NextUInt();
            state += seed;
            NextUInt();
        }

        public static Pcg32 FromTime()
        {
            var seed = (ulong)DateTime.UtcNow.Ticks ^ ((ulong)Environment.TickCount << 32);
            return new Pcg32(seed, (ulong)Guid.NewGuid().GetHashCode());
        }

        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            // Lemire-style rejection to avoid modulo bias.
            uint bound = (uint)maxExclusive;
            uint threshold = (uint)(-bound % bound);
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);
    }
}
