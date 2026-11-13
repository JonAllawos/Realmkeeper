using System;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Systems
{
    /// <summary>Tunable constants for food. Integer-valued to keep results exact.</summary>
    public sealed class FoodEconomyRules
    {
        /// <summary>One food unit feeds this many people for one day.</summary>
        public int PeoplePerFoodUnit { get; set; } = 10;

        /// <summary>A shortage begins when reserves cover fewer than this many days of consumption.</summary>
        public int ShortageThresholdDays { get; set; } = 15;

        /// <summary>A shortage ends when reserves recover to this many days (hysteresis avoids flapping).</summary>
        public int RecoveryThresholdDays { get; set; } = 30;

        /// <summary>Days of unmet food demand tolerated before people start to die.</summary>
        public int StarvationGraceDays { get; set; } = 3;

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
            if (StarvationGraceDays < 0) throw new InvalidOperationException("StarvationGraceDays must be >= 0.");
            if (StarvationLossDivisor < 1) throw new InvalidOperationException("StarvationLossDivisor must be >= 1.");
        }
    }

    /// <summary>
    /// Every economic constant in one place. Values are integers (percent, per-mille, per-100)
    /// so all arithmetic is exact and identical on every runtime. Change values here, not in
    /// systems; tests construct their own instances.
    /// </summary>
    public sealed class EconomyRules
    {
        public FoodEconomyRules Food { get; set; } = new FoodEconomyRules();

        // ----- Labour and production ----------------------------------------------------

        /// <summary>Share of the population that works, in percent.</summary>
        public int LabourParticipationPercent { get; set; } = 50;

        /// <summary>
        /// Food the labour system aims to grow, as a percent of the settlement's own demand:
        /// agricultural settlements farm for export, others for themselves with a safety margin.
        /// </summary>
        public int FoodTargetPercent(SettlementEconomy economy)
        {
            if (economy.Specialization == SettlementSpecialization.Agricultural) return AgriculturalFoodTargetPercent;
            if (economy.SecondarySpecialization == SettlementSpecialization.Agricultural) return 135;
            return DefaultFoodTargetPercent;
        }

        public int AgriculturalFoodTargetPercent { get; set; } = 170;
        public int DefaultFoodTargetPercent { get; set; } = 115;

        /// <summary>Daily output of 100 workers at 100% efficiency.</summary>
        public int OutputPer100Workers(ProductionSourceType source)
        {
            switch (source)
            {
                case ProductionSourceType.Farming: return FarmingRate;
                case ProductionSourceType.Fishing: return FishingRate;
                case ProductionSourceType.Hunting: return HuntingRate;
                case ProductionSourceType.Logging: return LoggingRate;
                case ProductionSourceType.Quarrying: return QuarryingRate;
                case ProductionSourceType.Mining: return MiningRate;
                case ProductionSourceType.Trade: return TradeRate;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        public int FarmingRate { get; set; } = 50;
        public int FishingRate { get; set; } = 40;
        public int HuntingRate { get; set; } = 25;
        public int LoggingRate { get; set; } = 40;
        public int QuarryingRate { get; set; } = 40;
        public int MiningRate { get; set; } = 30;
        public int TradeRate { get; set; } = 40;

        /// <summary>Spare labour returns to food production only while the granary is below this percent of capacity.</summary>
        public int FoodOverflowLabourPercent { get; set; } = 75;

        /// <summary>
        /// Seasonal hook: percent of normal output for a source on a date. Phase 2 keeps every
        /// season at 100; later phases can plug in a calendar-aware rule without touching systems.
        /// </summary>
        public Func<Domain.Calendar.WorldDate, ProductionSourceType, int> SeasonalModifierPercent { get; set; } = (date, source) => 100;

        /// <summary>
        /// Phase 21: the game's rules — these defaults with the seasons plugged into the hook above (farms yield at harvest,
        /// little in winter; each source averages 100 % over a year). Plain <c>new EconomyRules()</c> keeps every season at
        /// 100, so the economy's own arithmetic stays testable on its own.
        /// </summary>
        public static EconomyRules WithSeasons() => new EconomyRules { SeasonalModifierPercent = Climate.SeasonalProduction.Percent };

        // ----- Consumption ----------------------------------------------------------------

        public int PeoplePerTimberUnit { get; set; } = 80;
        public int PeoplePerStoneUnit { get; set; } = 500;
        public int WorkersPerIronUnit { get; set; } = 300;
        public int PeoplePerCoinUnit { get; set; } = 100;

        /// <summary>Daily demand for a resource. Food and timber round up (everyone eats and needs fuel); the rest round down.</summary>
        public int DailyDemand(ResourceTypeId resource, int population, int workers)
        {
            if (population <= 0) return 0;
            if (resource == ResourceTypes.Food) return Food.DailyConsumption(population);
            if (resource == ResourceTypes.Timber) return (population + PeoplePerTimberUnit - 1) / PeoplePerTimberUnit;
            if (resource == ResourceTypes.Stone) return population / PeoplePerStoneUnit;
            if (resource == ResourceTypes.Iron) return Math.Max(0, workers) / WorkersPerIronUnit;
            if (resource == ResourceTypes.Coin) return population / PeoplePerCoinUnit;
            return 0;
        }

        /// <summary>Non-food shortage begins after stock covers fewer than this many days for <see cref="ShortageConfirmDays"/> days running.</summary>
        public int ResourceShortageThresholdDays { get; set; } = 7;
        public int ResourceRecoveryDays { get; set; } = 14;
        public int ShortageConfirmDays { get; set; } = 3;

        // ----- Trade ----------------------------------------------------------------------

        /// <summary>Days of its own demand an origin keeps back before exporting.</summary>
        public int ReserveDays(ResourceTypeId resource) => resource == ResourceTypes.Food ? 45 : 30;

        /// <summary>Days of demand a destination tries to hold; it imports up to this level.</summary>
        public int TargetDays(ResourceTypeId resource) => resource == ResourceTypes.Food ? 60 : 45;

        /// <summary>Coin paid per unit on delivery.</summary>
        public int Price(ResourceTypeId resource)
        {
            if (resource == ResourceTypes.Food) return 2;
            if (resource == ResourceTypes.Timber || resource == ResourceTypes.Stone) return 3;
            if (resource == ResourceTypes.Iron) return 6;
            return 1;
        }

        /// <summary>Food spoils on the road; other goods arrive whole.</summary>
        public bool IsPerishable(ResourceTypeId resource) => resource == ResourceTypes.Food;

        public int MinimumShipment { get; set; } = 5;

        /// <summary>Days a shipment waits at a full destination before turning back.</summary>
        public int MaxWaitDays { get; set; } = 5;

        /// <summary>Minimum days between chronicle entries for deliveries on one route.</summary>
        public int TradeReportIntervalDays { get; set; } = 60;

        /// <summary>Minimum days between repeated food-crisis entries (reserves exhausted, starvation) for one settlement.</summary>
        public int FoodCrisisReportIntervalDays { get; set; } = 30;

        /// <summary>Trade activity loses 1/N of its value per day (about a month of memory).</summary>
        public int TradeActivityDecayDays { get; set; } = 30;

        // ----- Prosperity, stability, population -----------------------------------------

        /// <summary>Largest change to prosperity or stability in one day.</summary>
        public int MaxDailyRatingChange { get; set; } = 1;

        /// <summary>Food reserves (in days) that count as fully secure.</summary>
        public int FoodSecureDays { get; set; } = 60;

        /// <summary>Trade activity that scores 100 for prosperity.</summary>
        public int TradeActivityForFullScore { get; set; } = 600;

        /// <summary>Daily output value per 100 people that scores 100 for prosperity.</summary>
        public int OutputValuePer100PeopleForFullScore { get; set; } = 40;

        public int ValueOf(ResourceTypeId resource)
        {
            if (resource == ResourceTypes.Timber || resource == ResourceTypes.Stone) return 2;
            if (resource == ResourceTypes.Iron) return 4;
            return 1;
        }

        public int AnnualGrowthPerMille { get; set; } = 24;
        public int AnnualEmigrationPerMille { get; set; } = 30;

        /// <summary>Below this stability people start to leave.</summary>
        public int EmigrationStabilityThreshold { get; set; } = 25;

        /// <summary>People the settlement's buildings and land comfortably support.</summary>
        public int HousingCapacity(SettlementType type)
        {
            switch (type)
            {
                case SettlementType.City: return 6000;
                case SettlementType.Town: return 2600;
                case SettlementType.Fortress: return 900;
                case SettlementType.Village: return 950;
                default: return 320;
            }
        }

        public void Validate()
        {
            Food.Validate();
            if (LabourParticipationPercent < 0 || LabourParticipationPercent > 100) throw new InvalidOperationException("LabourParticipationPercent must be 0..100.");
            foreach (var s in ProductionSources.All)
            {
                if (OutputPer100Workers(s) < 0) throw new InvalidOperationException($"Output rate for {s} must be >= 0.");
            }

            if (PeoplePerTimberUnit < 1 || PeoplePerStoneUnit < 1 || WorkersPerIronUnit < 1 || PeoplePerCoinUnit < 1)
                throw new InvalidOperationException("Consumption divisors must be >= 1.");
            if (ShortageConfirmDays < 1 || ResourceRecoveryDays < ResourceShortageThresholdDays)
                throw new InvalidOperationException("Resource shortage thresholds are inconsistent.");
            if (MinimumShipment < 1 || MaxWaitDays < 0 || TradeReportIntervalDays < 0 || TradeActivityDecayDays < 1)
                throw new InvalidOperationException("Trade settings are out of range.");
            if (MaxDailyRatingChange < 1 || FoodSecureDays < 1 || TradeActivityForFullScore < 1 || OutputValuePer100PeopleForFullScore < 1)
                throw new InvalidOperationException("Wellbeing settings are out of range.");
            if (SeasonalModifierPercent == null) throw new InvalidOperationException("SeasonalModifierPercent is required.");
        }
    }
}
