using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Tests.EditMode.Atlas;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>Phase 21: what the new daily work costs in the six-region world (measured, with generous bounds).</summary>
    public sealed class Phase21PerformanceTests : Phase16Fixture
    {
        [Test]
        public void A_year_of_weather_and_a_day_of_shops_are_cheap()
        {
            var w = W;
            var start = w.CurrentSimulationDate.ToDayNumber();
            var sw = Stopwatch.StartNew();
            for (var d = 1; d <= 360; d++) WeatherModel.AdvanceTo(w, start + d); // day by day (a catch-up is capped at 30 days)
            sw.Stop();
            var weatherPerDay = sw.Elapsed.TotalMilliseconds / 360.0;

            var rules = new CommerceRules();
            sw.Restart();
            foreach (var shop in w.Shops)
            {
                CommerceMath.Restock(w, shop, rules);
                ShopProduction.Produce(w, shop, rules);
            }

            sw.Stop();
            var shopsDay = sw.Elapsed.TotalMilliseconds;
            TestContext.WriteLine($"Weather: {weatherPerDay:0.000} ms a day for {w.Regions.Count} regions. Shops (restock and production): {shopsDay:0.00} ms a day for {w.Shops.Count} shops.");
            Assert.That(weatherPerDay, Is.LessThan(5), "weather is a small part of the daily tick");
            Assert.That(shopsDay, Is.LessThan(250));
        }
    }
}
