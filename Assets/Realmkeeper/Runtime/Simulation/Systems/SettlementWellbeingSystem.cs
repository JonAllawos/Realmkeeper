using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>
    /// Prosperity and stability follow explainable targets computed from the settlement's
    /// economy (see <see cref="EconomyMath.Factors"/>). Each day a rating moves toward its
    /// target by at most <see cref="EconomyRules.MaxDailyRatingChange"/>, so no ordinary day
    /// can swing a rating sharply. No randomness.
    /// </summary>
    public sealed class SettlementWellbeingSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.wellbeing";

        private readonly EconomyRules _rules;

        public SettlementWellbeingSystem(EconomyRules rules) { _rules = rules ?? throw new ArgumentNullException(nameof(rules)); }

        public string SystemId => Id;
        public int ExecutionOrder => 300;

        public void Execute(SimulationDayContext context)
        {
            foreach (var s in context.World.Settlements)
            {
                var factors = EconomyMath.Factors(s, _rules);
                var prosperityTarget = EconomyMath.ProsperityTarget(factors);
                s.Prosperity = EconomyMath.StepToward(s.Prosperity, prosperityTarget, _rules.MaxDailyRatingChange);
                var stabilityTarget = EconomyMath.StabilityTarget(factors, s.Prosperity);
                s.Stability = EconomyMath.StepToward(s.Stability, stabilityTarget, _rules.MaxDailyRatingChange);

                s.Economy.ProsperityTarget = prosperityTarget;
                s.Economy.StabilityTarget = stabilityTarget;
            }
        }
    }

    /// <summary>
    /// Slow, deterministic population change. Fed, stable settlements below their housing
    /// capacity grow (about 2–3% a year); unstable or overcrowded ones lose people. Fractions
    /// of a person carry over in <see cref="SettlementEconomy.PopulationProgressMilli"/>.
    /// Starvation deaths are applied by the consumption system.
    /// </summary>
    public sealed class PopulationSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.population";

        private readonly EconomyRules _rules;

        public PopulationSystem(EconomyRules rules) { _rules = rules ?? throw new ArgumentNullException(nameof(rules)); }

        public string SystemId => Id;
        public int ExecutionOrder => 400;

        public void Execute(SimulationDayContext context)
        {
            foreach (var s in context.World.Settlements)
            {
                var e = s.Economy;
                if (s.Population <= 0) continue;

                var housing = _rules.HousingCapacity(s.SettlementType);
                var fed = !s.IsInFoodShortage && e.DaysWithoutFood == 0;
                var perMille = 0;
                if (fed && s.Stability >= 40 && s.Population < housing) perMille += _rules.AnnualGrowthPerMille + s.Prosperity / 10;
                if (s.Stability < _rules.EmigrationStabilityThreshold || (long)s.Population * 100 > (long)housing * 110) perMille -= _rules.AnnualEmigrationPerMille;
                if (perMille == 0) continue;

                // Milli-people per day = population × per-mille per year / 360 days.
                e.PopulationProgressMilli += (int)((long)s.Population * perMille / WorldDateDaysPerYear);
                while (e.PopulationProgressMilli >= 1000)
                {
                    s.Population++;
                    e.PopulationProgressMilli -= 1000;
                }

                while (e.PopulationProgressMilli <= -1000 && s.Population > 0)
                {
                    s.Population--;
                    e.PopulationProgressMilli += 1000;
                }
            }
        }

        private const int WorldDateDaysPerYear = Domain.Calendar.WorldDate.DaysPerYear;
    }

    /// <summary>
    /// Records milestones in the chronicle only when something meaningful changed since the
    /// last report: prosperity or stability moving 10 points, population changing 5%, and the
    /// treasury or lifetime production passing the next rung of the milestone ladder
    /// (1,000 / 2,500 / 5,000 / 10,000 …). Never one event per settlement per day.
    /// </summary>
    public sealed class ChronicleSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.chronicle";
        public const int RatingReportStep = 10;
        public const int PopulationReportPercent = 5;

        /// <summary>Lifetime production and treasury thresholds that earn a chronicle entry.</summary>
        public static readonly IReadOnlyList<int> ProductionLadder = new[] { 1000, 2500, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };

        /// <summary>Highest ladder value at or below <paramref name="amount"/> (0 when below the first rung).</summary>
        public static int HighestRung(int amount)
        {
            var rung = 0;
            foreach (var step in ProductionLadder)
            {
                if (amount >= step) rung = step;
            }

            return rung;
        }

        public string SystemId => Id;
        public int ExecutionOrder => 500;

        public void Execute(SimulationDayContext context)
        {
            foreach (var s in context.World.Settlements)
            {
                var e = s.Economy;
                if (!e.IsInitialized) continue;
                var m = e.Markers;
                var ids = new[] { s.SettlementId };

                if (Math.Abs(s.Prosperity - m.ReportedProsperity) >= RatingReportStep)
                {
                    var rose = s.Prosperity > m.ReportedProsperity;
                    context.ReportEvent(HistoricalEventType.ProsperityChanged, rose ? EventSeverity.Positive : EventSeverity.Warning,
                        EventImportance.Notable, ids, new EventData().Set("from", m.ReportedProsperity).Set("to", s.Prosperity).Set("target", e.ProsperityTarget));
                    m.ReportedProsperity = s.Prosperity;
                }

                if (Math.Abs(s.Stability - m.ReportedStability) >= RatingReportStep)
                {
                    var rose = s.Stability > m.ReportedStability;
                    context.ReportEvent(HistoricalEventType.StabilityChanged, rose ? EventSeverity.Positive : EventSeverity.Warning,
                        EventImportance.Notable, ids, new EventData().Set("from", m.ReportedStability).Set("to", s.Stability).Set("target", e.StabilityTarget));
                    m.ReportedStability = s.Stability;
                }

                var populationStep = Math.Max(10, m.ReportedPopulation * PopulationReportPercent / 100);
                if (Math.Abs(s.Population - m.ReportedPopulation) >= populationStep)
                {
                    var grew = s.Population > m.ReportedPopulation;
                    context.ReportEvent(grew ? HistoricalEventType.SettlementGrew : HistoricalEventType.SettlementDeclined,
                        grew ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Notable, ids,
                        new EventData().Set("from", m.ReportedPopulation).Set("to", s.Population));
                    m.ReportedPopulation = s.Population;
                }

                var milestone = HighestRung(s.ResourceInventory.Get(ResourceTypes.Coin));
                if (milestone > m.CoinMilestone)
                {
                    context.ReportEvent(HistoricalEventType.EconomicMilestone, EventSeverity.Positive, EventImportance.Minor, ids,
                        new EventData().Set("treasury", milestone));
                    m.CoinMilestone = milestone;
                }

                foreach (var resource in ResourceTypes.Goods)
                {
                    m.ProductionMilestoneIndex.TryGetValue(resource, out var index);
                    var lifetime = e.LifetimeProduced.Get(resource);
                    var reached = -1;
                    while (index < ProductionLadder.Count && lifetime >= ProductionLadder[index])
                    {
                        reached = ProductionLadder[index];
                        index++;
                    }

                    if (reached < 0) continue;
                    m.ProductionMilestoneIndex[resource] = index;
                    context.ReportEvent(HistoricalEventType.ProductionMilestone, EventSeverity.Positive, EventImportance.Minor, ids,
                        new EventData().Set("resource", resource.Value).Set("amount", reached));
                }
            }
        }

        /// <summary>Sets the markers to the current state without reporting (used at founding and after upgrades).</summary>
        public static void Baseline(SettlementData s)
        {
            var m = s.Economy.Markers;
            m.ReportedProsperity = s.Prosperity;
            m.ReportedStability = s.Stability;
            m.ReportedPopulation = s.Population;
            m.CoinMilestone = HighestRung(s.ResourceInventory.Get(ResourceTypes.Coin));
            foreach (var resource in ResourceTypes.Goods)
            {
                var lifetime = s.Economy.LifetimeProduced.Get(resource);
                var index = 0;
                while (index < ProductionLadder.Count && lifetime >= ProductionLadder[index]) index++;
                m.ProductionMilestoneIndex[resource] = index;
            }
        }
    }

    public static class StandardSimulationSystems
    {
        /// <summary>All Phase 2 systems, in no particular order (the scheduler orders them).</summary>
        public static IReadOnlyList<ISimulationSystem> Create(EconomyRules rules = null, PoliticalRules politics = null)
        {
            rules = rules ?? new EconomyRules();
            rules.Validate();
            politics = politics ?? new PoliticalRules();
            politics.Validate();
            return new ISimulationSystem[]
            {
                new LabourSystem(rules),
                new ProductionSystem(rules),
                new TradeSystem(rules),
                new ConsumptionSystem(rules),
                new SettlementWellbeingSystem(rules),
                new PopulationSystem(rules),
                new Climate.WeatherSystem(), // Phase 21
                new Npcs.NpcSystem(),
                new Commerce.ShopSystem(),
                new PoliticalSystem(politics),
                new Fauna.FaunaSystem(), // Phase 10
                new Items.GatheringSystem(), // Phase 11
                new Quests.QuestSystem(), // Phase 12
                new Campaigns.CampaignSystem(), // Phase 17
                new Party.PartySystem(), // Phase 15
                new Atlas.GroupSystem(), // Phase 16
                new Atlas.FactionActivitySystem(), // Phase 16
                new Atlas.WorldEventSystem(), // Phase 16
                new ChronicleSystem(),
            };
        }

        public static IReadOnlyList<string> AllIds => new[]
        {
            Climate.WeatherSystem.Id, LabourSystem.Id, ProductionSystem.Id, TradeSystem.Id, Atlas.GroupSystem.Id, ConsumptionSystem.Id,
            SettlementWellbeingSystem.Id, PopulationSystem.Id, Npcs.NpcSystem.Id, Commerce.ShopSystem.Id, PoliticalSystem.Id, Atlas.FactionActivitySystem.Id, ChronicleSystem.Id, Fauna.FaunaSystem.Id, Items.GatheringSystem.Id, Atlas.WorldEventSystem.Id, Quests.QuestSystem.Id, Campaigns.CampaignSystem.Id, Party.PartySystem.Id,
        };

        /// <summary>System IDs used by Phase 1 saves; a world listing these is upgraded to <see cref="AllIds"/>.</summary>
        public static IReadOnlyList<string> LegacyIds => new[] { "realmkeeper.food", "realmkeeper.wellbeing" };

        public static SimulationScheduler CreateScheduler(IRandomSourceFactory randomFactory, EconomyRules rules = null, PoliticalRules politics = null)
        {
            var scheduler = new SimulationScheduler(randomFactory);
            foreach (var system in Create(rules, politics)) scheduler.Register(system);
            return scheduler;
        }
    }
}
