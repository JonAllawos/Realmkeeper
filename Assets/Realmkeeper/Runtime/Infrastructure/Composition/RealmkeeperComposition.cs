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
        public static WorldSession CreateSession(string saveDirectory, RegionGeneratorSettings generatorSettings = null, FoodEconomyRules foodRules = null)
        {
            var randomFactory = new Xoshiro256RandomFactory();
            foodRules = foodRules ?? new FoodEconomyRules();
            var scheduler = StandardSimulationSystems.CreateScheduler(randomFactory, foodRules);
            var generator = new RegionGenerator(randomFactory, generatorSettings);
            var store = new JsonWorldStore(saveDirectory);
            return new WorldSession(scheduler, generator, store, foodRules);
        }
    }
}
