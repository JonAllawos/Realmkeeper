using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>
    /// Runs after the food rule. Translates food security into slow, probabilistic drift of
    /// stability, prosperity and population. Uses the scheduler-provided random stream, so
    /// outcomes vary by seed but replay identically for the same seed.
    /// </summary>
    public sealed class SettlementWellbeingSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.wellbeing";
        public const int StabilityBaseline = 60;

        private readonly FoodEconomyRules _foodRules;

        public SettlementWellbeingSystem(FoodEconomyRules foodRules = null)
        {
            _foodRules = foodRules ?? new FoodEconomyRules();
        }

        public string SystemId => Id;
        public int ExecutionOrder => 200;

        public void Execute(SimulationDayContext context)
        {
            var random = context.Random;
            foreach (var s in context.World.Settlements)
            {
                var consumption = _foodRules.DailyConsumption(s.Population);
                if (s.IsInFoodShortage)
                {
                    if (random.Chance(0.5)) s.Stability--;
                    if (s.FoodReserves == 0 && consumption > 0) s.Stability--;
                    if (random.Chance(0.2)) s.Prosperity--;
                }
                else
                {
                    if (s.Stability < StabilityBaseline && random.Chance(0.1)) s.Stability++;
                    var surplus = s.FoodProductionPerDay > consumption;
                    if (surplus && random.Chance(0.05)) s.Prosperity++;
                    if (surplus && random.Chance(0.1)) s.Population += Math.Max(1, s.Population / 1000);
                }

                s.Stability = Clamp(s.Stability);
                s.Prosperity = Clamp(s.Prosperity);
            }
        }

        private static int Clamp(int v) => Math.Max(SettlementData.MinRating, Math.Min(SettlementData.MaxRating, v));
    }

    public static class StandardSimulationSystems
    {
        /// <summary>All Phase 1 systems, in no particular order (the scheduler orders them).</summary>
        public static IReadOnlyList<ISimulationSystem> Create(FoodEconomyRules rules = null)
        {
            rules = rules ?? new FoodEconomyRules();
            return new ISimulationSystem[] { new FoodEconomySystem(rules), new SettlementWellbeingSystem(rules) };
        }

        public static IReadOnlyList<string> AllIds => new[] { FoodEconomySystem.Id, SettlementWellbeingSystem.Id };

        public static SimulationScheduler CreateScheduler(IRandomSourceFactory randomFactory, FoodEconomyRules rules = null)
        {
            var scheduler = new SimulationScheduler(randomFactory);
            foreach (var system in Create(rules)) scheduler.Register(system);
            return scheduler;
        }
    }
}
