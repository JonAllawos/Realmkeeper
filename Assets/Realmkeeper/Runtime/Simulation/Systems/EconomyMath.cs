using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>One term of a prosperity or stability target, used to explain the target in the UI.</summary>
    public enum WellbeingFactor
    {
        Food = 0,
        Goods = 1,
        Trade = 2,
        Output = 3,
        EconomicSecurity = 4,
        Housing = 5,
        Prosperity = 6,
        Employment = 7,
        Shortages = 8,
        Starvation = 9,
    }

    /// <summary>A factor's score and its signed contribution to a target, in hundredths of a point.</summary>
    public readonly struct WellbeingContribution
    {
        public WellbeingContribution(WellbeingFactor factor, int score, int hundredths)
        {
            Factor = factor;
            Score = score;
            Hundredths = hundredths;
        }

        public WellbeingFactor Factor { get; }

        /// <summary>The factor's 0–100 score (or the penalty in points, for penalties).</summary>
        public int Score { get; }

        /// <summary>Signed contribution in hundredths of a rating point (penalties are negative).</summary>
        public int Hundredths { get; }

        /// <summary>Contribution rounded to whole points (half away from zero), for display.</summary>
        public int Points => Hundredths >= 0 ? (Hundredths + 50) / 100 : -((-Hundredths + 50) / 100);
    }

    /// <summary>Factor scores (0..100) behind a settlement's prosperity and stability targets.</summary>
    public readonly struct WellbeingFactors
    {
        public WellbeingFactors(int food, int resources, int trade, int output, int employment, int housing, int coin, int shortagePenalty, int starvationPenalty)
        {
            Food = food;
            Resources = resources;
            Trade = trade;
            Output = output;
            Employment = employment;
            Housing = housing;
            Coin = coin;
            ShortagePenalty = shortagePenalty;
            StarvationPenalty = starvationPenalty;
        }

        public int Food { get; }
        public int Resources { get; }
        public int Trade { get; }
        public int Output { get; }
        public int Employment { get; }
        public int Housing { get; }
        public int Coin { get; }
        public int ShortagePenalty { get; }
        public int StarvationPenalty { get; }
    }

    /// <summary>
    /// Pure economic calculations shared by the simulation systems, world generation (the
    /// founding-day projection) and tests. Integer arithmetic only; no state, no randomness.
    /// </summary>
    public static class EconomyMath
    {
        public static int Workforce(int population, EconomyRules rules) =>
            population <= 0 ? 0 : (int)((long)population * rules.LabourParticipationPercent / 100);

        /// <summary>
        /// Deterministic labour allocation in three passes:
        /// 1. Food first: farming, then fishing, then hunting get the workers needed to grow
        ///    <see cref="EconomyRules.FoodTargetPercent"/> of the settlement's food demand
        ///    (more for agricultural settlements, which grow surplus to trade).
        /// 2. The remaining workers go to the non-food sources by allocation weight, capped by capacity.
        /// 3. Anyone still idle works the food sources' remaining capacity, by weight.
        /// Sources are visited in list order, so ties always resolve the same way.
        /// </summary>
        public static void AllocateLabour(SettlementData settlement, EconomyRules rules, WorldDate date)
        {
            var economy = settlement.Economy;
            foreach (var p in economy.Production) p.Workers = 0;
            var remaining = Workforce(settlement.Population, rules);

            // Pass 1: staff food production up to its target, most productive source first in list order.
            var demand = rules.DailyDemand(ResourceTypes.Food, settlement.Population, 0);
            var granaryNearlyFull = settlement.ResourceInventory.Get(ResourceTypes.Food) * 100L >=
                                    (long)economy.CapacityOf(ResourceTypes.Food) * rules.FoodOverflowLabourPercent;
            long needed = (long)demand * (granaryNearlyFull ? 100 : rules.FoodTargetPercent(economy)) / 100;
            foreach (var p in economy.Production)
            {
                if (needed <= 0 || remaining <= 0) break;
                if (p.Output != ResourceTypes.Food || p.WorkerCapacity <= 0) continue;
                var perHundredWorkers = (long)rules.OutputPer100Workers(p.SourceType) * p.EfficiencyPercent * rules.SeasonalModifierPercent(date, p.SourceType) / 10000;
                if (perHundredWorkers <= 0) continue;
                var workers = (int)Math.Min(Math.Min(p.WorkerCapacity, remaining), (needed * 100 + perHundredWorkers - 1) / perHundredWorkers);
                p.Workers = workers;
                remaining -= workers;
                needed -= workers * perHundredWorkers / 100;
            }

            // Pass 2: other trades by weight. Pass 3: leftover labour back to food sources by weight,
            // unless the granary is nearly full (extra food would only be wasted).
            remaining = Distribute(economy, remaining, p => p.Output != ResourceTypes.Food);
            if (!granaryNearlyFull) Distribute(economy, remaining, p => p.Output == ResourceTypes.Food);
        }

        /// <summary>Weighted, capacity-capped distribution among the sources the filter accepts. Returns workers left over.</summary>
        private static int Distribute(SettlementEconomy economy, int remaining, Func<ProductionAssignment, bool> eligible)
        {
            bool Open(ProductionAssignment p) => eligible(p) && p.AllocationWeight > 0 && p.Workers < p.WorkerCapacity;

            // A posted labour notice doubles the focused source's weight (stored policy, not randomness).
            long Weight(ProductionAssignment p) => economy.LabourFocus == p.SourceType ? 2L * p.AllocationWeight : p.AllocationWeight;

            while (remaining > 0)
            {
                long totalWeight = 0;
                foreach (var p in economy.Production)
                {
                    if (Open(p)) totalWeight += Weight(p);
                }

                if (totalWeight == 0) break;

                var pool = remaining;
                var given = 0;
                foreach (var p in economy.Production)
                {
                    if (!Open(p)) continue;
                    var share = (int)(pool * Weight(p) / totalWeight);
                    var take = Math.Min(share, p.WorkerCapacity - p.Workers);
                    p.Workers += take;
                    given += take;
                }

                if (given == 0)
                {
                    // Shares rounded to zero: hand out single workers in list order.
                    foreach (var p in economy.Production)
                    {
                        if (remaining - given == 0) break;
                        if (!Open(p)) continue;
                        p.Workers++;
                        given++;
                    }

                    if (given == 0) break;
                }

                remaining -= given;
            }

            return remaining;
        }

        /// <summary>Daily output of one source: workers × rate × efficiency × season, floored.</summary>
        public static int SourceOutput(ProductionAssignment p, EconomyRules rules, WorldDate date)
        {
            if (p.Workers <= 0) return 0;
            var season = rules.SeasonalModifierPercent(date, p.SourceType);
            if (season <= 0) return 0;
            var value = (long)p.Workers * rules.OutputPer100Workers(p.SourceType) * p.EfficiencyPercent * season / 1000000L;
            return (int)Math.Min(int.MaxValue, value);
        }

        /// <summary>Whole days the stock lasts at the given daily demand (int.MaxValue when nothing is needed).</summary>
        public static int CoverDays(int stock, int demand) => demand <= 0 ? int.MaxValue : stock / demand;

        /// <summary>The ledger entry for a resource on a given day; clears the ledger when a new day starts.</summary>
        public static ResourceFlow Flow(SettlementEconomy economy, ResourceTypeId resource, long dayNumber)
        {
            if (economy.LedgerDay != dayNumber)
            {
                economy.LedgerDay = dayNumber;
                economy.LastDay.Clear();
                economy.LastDaySources.Clear();
                foreach (var r in ResourceTypes.All) economy.LastDay.Add(new ResourceFlow { ResourceTypeId = r });
            }

            var flow = economy.FindFlow(resource);
            if (flow == null)
            {
                flow = new ResourceFlow { ResourceTypeId = resource };
                economy.LastDay.Add(flow);
            }

            return flow;
        }

        /// <summary>The scores behind prosperity and stability. Explained in Documentation/Systems/Economy.md.</summary>
        public static WellbeingFactors Factors(SettlementData s, EconomyRules rules)
        {
            var e = s.Economy;
            var workers = e.TotalWorkers;
            var stock = s.ResourceInventory;

            // Food security: reserves measured in days of demand.
            var foodDemand = rules.DailyDemand(ResourceTypes.Food, s.Population, workers);
            var foodCover = CoverDays(stock.Get(ResourceTypes.Food), foodDemand);
            var food = foodCover == int.MaxValue ? 100 : Math.Min(100, (int)((long)foodCover * 100 / rules.FoodSecureDays));
            if (s.IsInFoodShortage) food = Math.Min(food, 40);
            if (e.DaysWithoutFood > 0) food = 0;

            // Availability of the other goods the population uses.
            var resourceTotal = 0;
            var counted = 0;
            var shortages = 0;
            foreach (var r in ResourceTypes.Goods)
            {
                if (r == ResourceTypes.Food) continue;
                var demand = rules.DailyDemand(r, s.Population, workers);
                var cover = CoverDays(stock.Get(r), demand);
                resourceTotal += cover == int.MaxValue ? 100 : Math.Min(100, (int)((long)cover * 100 / 30));
                counted++;
                if (e.FindStatus(r)?.InShortage == true) shortages++;
            }

            var resources = counted == 0 ? 100 : resourceTotal / counted;
            if (e.FindStatus(ResourceTypes.Coin)?.InShortage == true) shortages++;

            var trade = Math.Min(100, (int)((long)e.TradeActivity * 100 / rules.TradeActivityForFullScore));

            // Economic output: value of yesterday's production per 100 people.
            long outputValue = 0;
            foreach (var f in e.LastDay) outputValue += (long)f.Produced * rules.ValueOf(f.ResourceTypeId);
            var output = s.Population <= 0 ? 0 : (int)Math.Min(100, outputValue * 100 * 100 / ((long)s.Population * rules.OutputValuePer100PeopleForFullScore));

            var workforce = Workforce(s.Population, rules);
            var employment = workforce == 0 ? 100 : Math.Min(100, workers * 100 / workforce);

            // Housing pressure: comfortable up to 90% of capacity, untenable at 120%.
            var housingCapacity = Math.Max(1, rules.HousingCapacity(s.SettlementType));
            var occupancy = (int)((long)s.Population * 100 / housingCapacity);
            var housing = occupancy <= 90 ? 100 : occupancy >= 120 ? 0 : (120 - occupancy) * 100 / 30;

            var coinDemand = rules.DailyDemand(ResourceTypes.Coin, s.Population, workers);
            var coinCover = CoverDays(stock.Get(ResourceTypes.Coin), coinDemand);
            var coin = coinCover == int.MaxValue ? 100 : Math.Min(100, (int)((long)coinCover * 100 / 60));

            var shortagePenalty = (s.IsInFoodShortage ? 10 : 0) + 5 * shortages + Math.Min(20, e.DaysWithoutFood * 2);
            var starvationPenalty = e.DaysWithoutFood > rules.Food.StarvationGraceDays ? 15 : 0;
            return new WellbeingFactors(food, resources, trade, output, employment, housing, coin, shortagePenalty, starvationPenalty);
        }

        /// <summary>Prosperity target: 30% food, 15% goods, 20% trade, 35% output, minus shortage penalties.</summary>
        public static int ProsperityTarget(WellbeingFactors f) =>
            ClampRating((30 * f.Food + 15 * f.Resources + 20 * f.Trade + 35 * f.Output) / 100 - f.ShortagePenalty);

        /// <summary>Stability target: 35% food, 20% economic security, 15% housing, 20% prosperity, 10% employment, minus penalties.</summary>
        public static int StabilityTarget(WellbeingFactors f, int prosperity)
        {
            var economicSecurity = (f.Resources + f.Coin) / 2;
            return ClampRating((35 * f.Food + 20 * economicSecurity + 15 * f.Housing + 20 * prosperity + 10 * f.Employment) / 100
                               - f.ShortagePenalty - f.StarvationPenalty);
        }

        /// <summary>
        /// The terms of <see cref="ProsperityTarget"/>, each in hundredths of a point (weight × score,
        /// or −100 × penalty). <c>Clamp(Σ positive / 100 − penalties)</c> equals the target exactly.
        /// </summary>
        public static IReadOnlyList<WellbeingContribution> ProsperityContributions(WellbeingFactors f) => new[]
        {
            new WellbeingContribution(WellbeingFactor.Food, f.Food, 30 * f.Food),
            new WellbeingContribution(WellbeingFactor.Goods, f.Resources, 15 * f.Resources),
            new WellbeingContribution(WellbeingFactor.Trade, f.Trade, 20 * f.Trade),
            new WellbeingContribution(WellbeingFactor.Output, f.Output, 35 * f.Output),
            new WellbeingContribution(WellbeingFactor.Shortages, f.ShortagePenalty, -100 * f.ShortagePenalty),
        };

        /// <summary>The terms of <see cref="StabilityTarget"/>, in hundredths of a point, like <see cref="ProsperityContributions"/>.</summary>
        public static IReadOnlyList<WellbeingContribution> StabilityContributions(WellbeingFactors f, int prosperity)
        {
            var economicSecurity = (f.Resources + f.Coin) / 2;
            return new[]
            {
                new WellbeingContribution(WellbeingFactor.Food, f.Food, 35 * f.Food),
                new WellbeingContribution(WellbeingFactor.EconomicSecurity, economicSecurity, 20 * economicSecurity),
                new WellbeingContribution(WellbeingFactor.Housing, f.Housing, 15 * f.Housing),
                new WellbeingContribution(WellbeingFactor.Prosperity, prosperity, 20 * prosperity),
                new WellbeingContribution(WellbeingFactor.Employment, f.Employment, 10 * f.Employment),
                new WellbeingContribution(WellbeingFactor.Shortages, f.ShortagePenalty, -100 * f.ShortagePenalty),
                new WellbeingContribution(WellbeingFactor.Starvation, f.StarvationPenalty, -100 * f.StarvationPenalty),
            };
        }

        /// <summary>Recombines contributions the way the target formulas do (integer division of the weighted sum, then penalties, then clamp).</summary>
        public static int TargetFrom(IReadOnlyList<WellbeingContribution> parts)
        {
            long weighted = 0;
            long penalties = 0;
            foreach (var p in parts)
            {
                if (p.Hundredths >= 0) weighted += p.Hundredths;
                else penalties += -p.Hundredths / 100;
            }

            return ClampRating((int)(weighted / 100 - penalties));
        }

        /// <summary>Moves a rating toward its target by at most <paramref name="maxStep"/>.</summary>
        public static int StepToward(int current, int target, int maxStep)
        {
            if (target > current) return Math.Min(target, current + maxStep);
            if (target < current) return Math.Max(target, current - maxStep);
            return current;
        }

        public static int ClampRating(int value) => Math.Max(SettlementData.MinRating, Math.Min(SettlementData.MaxRating, value));
    }
}
