using System;
using Realmkeeper.Application;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Composition
{
    /// <summary>
    /// Wires concrete adapters into the application layer. The single place that knows
    /// every implementation; the Unity bootstrap and tests both use it, so they run the
    /// same object graph.
    /// </summary>
    public static class RealmkeeperComposition
    {
        public static WorldSession CreateSession(string saveDirectory, RegionGeneratorSettings generatorSettings = null, EconomyRules rules = null)
        {
            var randomFactory = new Xoshiro256RandomFactory();
            rules = rules ?? EconomyRules.WithSeasons(); // Phase 21: the game runs with the seasons
            rules.Validate();
            var scheduler = StandardSimulationSystems.CreateScheduler(randomFactory, rules);
            var generator = new RegionGenerator(randomFactory, generatorSettings, rules);
            var store = new JsonWorldStore(saveDirectory);
            return new WorldSession(scheduler, generator, store, rules, randomFactory);
        }

        /// <summary>A session host whose sessions are built by <see cref="CreateSession"/>. Modules register handlers through <paramref name="configure"/>.</summary>
        public static WorldSessionHost CreateHost(string saveDirectory, Action<WorldSession> configure = null, RegionGeneratorSettings generatorSettings = null, EconomyRules rules = null) =>
            new WorldSessionHost(() => CreateSession(saveDirectory, generatorSettings, rules), configure);
    }
}
