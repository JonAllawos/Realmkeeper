using System;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>Tunable constants for the food rule. Integer-valued to keep results exact.</summary>
    public sealed class FoodEconomyRules
    {
        /// <summary>One food unit feeds this many people for one day.</summary>
        public int PeoplePerFoodUnit { get; set; } = 10;

        /// <summary>A shortage begins when reserves cover fewer than this many days of consumption.</summary>
        public int ShortageThresholdDays { get; set; } = 15;

        /// <summary>A shortage ends when reserves recover to this many days (hysteresis avoids flapping).</summary>
        public int RecoveryThresholdDays { get; set; } = 30;

        /// <summary>While starving, population falls by population / divisor (at least 1) per day.</summary>
        public int StarvationLossDivisor { get; set; } = 200;

        public int DailyConsumption(int population)
        {
            if (population <= 0) return 0;
            return (population + PeoplePerFoodUnit - 1) / PeoplePerFoodUnit;
        }

        public void Validate()
        {
            if (PeoplePerFoodUnit < 1) throw new InvalidOperationException("PeoplePerFoodUnit must be >= 1.");
            if (ShortageThresholdDays < 0) throw new InvalidOperationException("ShortageThresholdDays must be >= 0.");
            if (RecoveryThresholdDays < ShortageThresholdDays) throw new InvalidOperationException("RecoveryThresholdDays must be >= ShortageThresholdDays.");
            if (StarvationLossDivisor < 1) throw new InvalidOperationException("StarvationLossDivisor must be >= 1.");
        }
    }

    /// <summary>
    /// Phase 1 settlement rule: each day a settlement produces food, then consumes food
    /// based on population. Deficits draw down reserves; low reserves raise a food-shortage
    /// event; empty reserves cause starvation (population loss). Fully deterministic, no randomness.
    /// </summary>
    public sealed class FoodEconomySystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.food";

        private readonly FoodEconomyRules _rules;

        public FoodEconomySystem(FoodEconomyRules rules = null)
        {
            _rules = rules ?? new FoodEconomyRules();
            _rules.Validate();
        }

        public string SystemId => Id;
        public int ExecutionOrder => 100;
        public FoodEconomyRules Rules => _rules;

        public void Execute(SimulationDayContext context)
        {
            foreach (var s in context.World.Settlements)
            {
                var consumption = _rules.DailyConsumption(s.Population);
                var previousReserves = s.FoodReserves;
                var available = checked((long)previousReserves + s.FoodProductionPerDay);

                int newReserves;
                if (available >= consumption)
                {
                    newReserves = (int)Math.Min(int.MaxValue, available - consumption);
                }
                else
                {
                    newReserves = 0;
                    var loss = Math.Max(1, s.Population / _rules.StarvationLossDivisor);
                    s.Population = Math.Max(0, s.Population - loss);
                    if (previousReserves > 0)
                    {
                        context.ReportEvent(HistoricalEventType.FoodReservesExhausted, EventSeverity.Critical, s.SettlementId,
                            $"{s.Name} has exhausted its food reserves. The people are starving.");
                    }
                }

                s.ResourceInventory.Set(ResourceTypes.Food, newReserves);
                UpdateShortageState(context, s, consumption, newReserves);
            }
        }

        private void UpdateShortageState(SimulationDayContext context, SettlementData s, int consumption, int reserves)
        {
            if (consumption == 0)
            {
                if (s.IsInFoodShortage) EndShortage(context, s);
                return;
            }

            var shortageLevel = (long)consumption * _rules.ShortageThresholdDays;
            var recoveryLevel = (long)consumption * _rules.RecoveryThresholdDays;

            if (!s.IsInFoodShortage && reserves < shortageLevel)
            {
                s.IsInFoodShortage = true;
                var daysLeft = reserves / consumption;
                context.ReportEvent(HistoricalEventType.FoodShortageBegan, EventSeverity.Warning, s.SettlementId,
                    $"Food is running short in {s.Name}: about {daysLeft} days of reserves remain.");
            }
            else if (s.IsInFoodShortage && reserves >= recoveryLevel)
            {
                EndShortage(context, s);
            }
        }

        private static void EndShortage(SimulationDayContext context, SettlementData s)
        {
            s.IsInFoodShortage = false;
            context.ReportEvent(HistoricalEventType.FoodShortageEnded, EventSeverity.Info, s.SettlementId,
                $"The granaries of {s.Name} have recovered.");
        }
    }
}
