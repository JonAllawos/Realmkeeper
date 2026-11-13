using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Climate
{
    /// <summary>
    /// Phase 21 (ADR-0105): works out each region's weather for the new day (order 40: before labour and production, so
    /// the day's work sees the day's weather). Cheap: eight periods per region, no per-frame work.
    /// </summary>
    public sealed class WeatherSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.weather";

        public string SystemId => Id;
        public int ExecutionOrder => 40;

        public void Execute(SimulationDayContext context) => WeatherModel.AdvanceTo(context.World, context.Date.ToDayNumber());
    }

    /// <summary>
    /// Phase 21: the season's effect on production, through the economy's existing seasonal hook. Over a year each source
    /// averages 100 %, so a settlement's annual balance is unchanged; within the year, harvest comes in autumn and winter
    /// is lean.
    /// </summary>
    public static class SeasonalProduction
    {
        public static int Percent(WorldDate date, ProductionSourceType source)
        {
            if (!date.IsValidDate) return 100;
            var s = SeasonRules.Of(date);
            switch (source)
            {
                case ProductionSourceType.Farming: return s == Season.Spring ? 85 : s == Season.Summer ? 110 : s == Season.Autumn ? 140 : 65;
                case ProductionSourceType.Fishing: return s == Season.Spring ? 100 : s == Season.Summer ? 112 : s == Season.Autumn ? 100 : 88;
                case ProductionSourceType.Hunting: return s == Season.Spring ? 90 : s == Season.Summer ? 100 : s == Season.Autumn ? 120 : 90;
                case ProductionSourceType.Logging: return s == Season.Winter ? 110 : s == Season.Spring ? 95 : s == Season.Summer ? 95 : 100;
                default: return 100;
            }
        }
    }
}
