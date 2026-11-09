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
            rules = rules ?? new EconomyRules();
            rules.Validate();
            var scheduler = StandardSimulationSystems.CreateScheduler(randomFactory, rules);
            var generator = new RegionGenerator(randomFactory, generatorSettings, rules);
            var store = new JsonWorldStore(saveDirectory);
            return new WorldSession(scheduler, generator, store, rules);
        }
    }
}
