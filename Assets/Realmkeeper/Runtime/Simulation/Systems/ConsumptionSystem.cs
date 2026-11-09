using System;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>
    /// The population uses resources: food by headcount, timber and stone for fuel and repairs,
    /// iron for workers' tools, coin for upkeep. Unmet demand is recorded as a shortfall, never
    /// as negative stock. Consequences are gradual:
    /// - food reserves below <see cref="FoodEconomyRules.ShortageThresholdDays"/> start a food shortage
    ///   (ended only at <see cref="FoodEconomyRules.RecoveryThresholdDays"/>, so it does not flap);
    /// - unmet food demand is tolerated for <see cref="FoodEconomyRules.StarvationGraceDays"/> days,
    ///   after which population falls by population / <see cref="FoodEconomyRules.StarvationLossDivisor"/> per day;
    /// - other resources enter shortage after <see cref="EconomyRules.ShortageConfirmDays"/> consecutive days below
    ///   <see cref="EconomyRules.ResourceShortageThresholdDays"/> days of cover.
    /// Prosperity and stability react through the wellbeing system, not here.
    /// </summary>
    public sealed class ConsumptionSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.consumption";

        private readonly EconomyRules _rules;

        public ConsumptionSystem(EconomyRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _rules.Validate();
        }

        public string SystemId => Id;
        public int ExecutionOrder => 200;

        public void Execute(SimulationDayContext context)
        {
            var day = context.Date.ToDayNumber();
            foreach (var s in context.World.Settlements)
            {
                var e = s.Economy;
                var workers = e.TotalWorkers;
                foreach (var resource in ResourceTypes.All)
                {
                    var demand = _rules.DailyDemand(resource, s.Population, workers);
                    var stock = s.ResourceInventory.Get(resource);
                    var consumed = Math.Min(stock, demand);
                    s.ResourceInventory.Set(resource, stock - consumed);

                    var flow = EconomyMath.Flow(e, resource, day);
                    flow.Demand += demand;
                    flow.Consumed += consumed;
                    flow.Shortfall += demand - consumed;

                    if (resource == ResourceTypes.Food) UpdateFood(context, s, demand, demand - consumed);
                    else UpdateResourceShortage(context, s, resource, demand, demand - consumed);
                }
            }
        }

        private void UpdateFood(SimulationDayContext context, SettlementData s, int demand, int shortfall)
        {
            var e = s.Economy;
            var ids = new[] { s.SettlementId };

            var day = context.Date.ToDayNumber();
            var markers = e.Markers;
            if (shortfall > 0)
            {
                e.DaysWithoutFood++;
                if (e.DaysWithoutFood == 1 && Due(markers.LastFoodExhaustedReportDay, day))
                {
                    markers.LastFoodExhaustedReportDay = day;
                    context.ReportEvent(HistoricalEventType.FoodReservesExhausted, EventSeverity.Critical, EventImportance.Major, ids,
                        new EventData().Set("shortfall", shortfall).Set("graceDays", _rules.Food.StarvationGraceDays));
                }

                if (e.DaysWithoutFood > _rules.Food.StarvationGraceDays)
                {
                    var loss = Math.Min(s.Population, Math.Max(1, s.Population / _rules.Food.StarvationLossDivisor));
                    s.Population -= loss;
                    if (e.DaysWithoutFood == _rules.Food.StarvationGraceDays + 1 && Due(markers.LastStarvationReportDay, day))
                    {
                        markers.LastStarvationReportDay = day;
                        context.ReportEvent(HistoricalEventType.StarvationBegan, EventSeverity.Critical, EventImportance.Major, ids,
                            new EventData().Set("dailyLoss", loss).Set("population", s.Population));
                    }
                }
            }
            else
            {
                e.DaysWithoutFood = 0;
            }

            if (demand == 0)
            {
                if (s.IsInFoodShortage) EndFoodShortage(context, s);
                return;
            }

            var reserves = s.FoodReserves;
            var shortageLevel = (long)demand * _rules.Food.ShortageThresholdDays;
            var recoveryLevel = (long)demand * _rules.Food.RecoveryThresholdDays;
            if (!s.IsInFoodShortage && reserves < shortageLevel)
            {
                s.IsInFoodShortage = true;
                context.ReportEvent(HistoricalEventType.FoodShortageBegan, EventSeverity.Warning, EventImportance.Notable, ids,
                    new EventData().Set("daysLeft", reserves / demand).Set("reserves", reserves).Set("demand", demand));
            }
            else if (s.IsInFoodShortage && reserves >= recoveryLevel && shortfall == 0)
            {
                EndFoodShortage(context, s);
            }
        }

        private bool Due(long lastReportDay, long day) => lastReportDay < 0 || day - lastReportDay >= _rules.FoodCrisisReportIntervalDays;

        private static void EndFoodShortage(SimulationDayContext context, SettlementData s)
        {
            s.IsInFoodShortage = false;
            context.ReportEvent(HistoricalEventType.FoodShortageEnded, EventSeverity.Positive, EventImportance.Notable,
                new[] { s.SettlementId }, new EventData().Set("reserves", s.FoodReserves));
        }

        private void UpdateResourceShortage(SimulationDayContext context, SettlementData s, ResourceTypeId resource, int demand, int shortfall)
        {
            var e = s.Economy;
            var status = e.FindStatus(resource);
            if (status == null)
            {
                status = new ResourceStatus { ResourceTypeId = resource };
                e.ResourceStatuses.Add(status);
                e.ResourceStatuses.Sort((a, b) => a.ResourceTypeId.CompareTo(b.ResourceTypeId));
            }

            var stock = s.ResourceInventory.Get(resource);
            var cover = EconomyMath.CoverDays(stock, demand);
            var below = shortfall > 0 || cover < _rules.ResourceShortageThresholdDays;

            if (!status.InShortage)
            {
                status.DaysBelowThreshold = below ? status.DaysBelowThreshold + 1 : 0;
                if (status.DaysBelowThreshold >= _rules.ShortageConfirmDays)
                {
                    status.InShortage = true;
                    context.ReportEvent(HistoricalEventType.ResourceShortageBegan, EventSeverity.Warning, EventImportance.Notable,
                        new[] { s.SettlementId },
                        new EventData().Set("resource", resource.Value).Set("stock", stock).Set("demand", demand)
                            .Set("daysLeft", cover == int.MaxValue ? -1 : cover));
                }
            }
            else
            {
                var recovered = shortfall == 0 && cover >= _rules.ResourceRecoveryDays;
                if (recovered)
                {
                    status.InShortage = false;
                    status.DaysBelowThreshold = 0;
                    context.ReportEvent(HistoricalEventType.ResourceShortageEnded, EventSeverity.Positive, EventImportance.Minor,
                        new[] { s.SettlementId }, new EventData().Set("resource", resource.Value).Set("stock", stock));
                }
                else
                {
                    status.DaysBelowThreshold++;
                }
            }
        }
    }
}
