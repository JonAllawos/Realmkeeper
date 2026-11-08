namespace Realmkeeper.Domain.Randomness
{
    /// <summary>
    /// Deterministic pseudo-random source. Implementations must produce an identical
    /// sequence for an identical seed on every platform (integer-only state).
    /// The implementation lives in Realmkeeper.Infrastructure; Domain and Simulation
    /// depend only on this abstraction.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Next uniformly distributed 64-bit value.</summary>
        ulong NextULong();

        /// <summary>Uniform integer in [minInclusive, maxExclusive). Requires maxExclusive &gt; minInclusive.</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Uniform double in [0, 1), built from 53 random bits.</summary>
        double NextDouble();
    }

    /// <summary>Creates seeded random sources. Lets Simulation derive per-day/per-system streams.</summary>
    public interface IRandomSourceFactory
    {
        IRandomSource Create(ulong seed);
    }
}
