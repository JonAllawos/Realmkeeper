using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Systems;
using Realmkeeper.Tests.EditMode.Npcs;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Commerce
{
    /// <summary>
    /// Cost of Phase 5 at three sizes, with every settlement's shops: whole days (restocking included),
    /// everyone's whereabouts for one hour (what exploration and shop availability compute), and every
    /// shop's explained prices. Bounds are generous ceilings that catch a change in complexity class;
    /// the measured numbers are printed for the report.
    /// </summary>
    public sealed class CommercePerformanceTests
    {
        private const int Days = 30;

        [Test]
        public void DefaultWorld() => Measure(Phase2Worlds.Generated(2026), "default world", 15.0, 5.0);

        [Test]
        public void About100People() => Measure(NpcPerformanceTests.WithPeople(Phase2Worlds.Generated(2026), 33), "~100 people", 40.0, 10.0);

        [Test]
        public void About300People_TenSettlements() =>
            Measure(NpcPerformanceTests.WithPeople(NpcPerformanceTests.WithSettlements(Phase2Worlds.Generated(2026), 10), 30), "~300 people / 10 settlements", 120.0, 30.0);

        private static void Measure(WorldData world, string label, double maxMsPerDay, double maxMsPerHour)
        {
            CommerceGenesis.EnsureShops(world); // settlements added for the test get their shops too
            var validation = WorldValidator.Validate(world);
            Assert.That(validation.IsValid, Is.True, string.Join("; ", validation.Errors.Take(5)));
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            scheduler.AdvanceDays(world, 2); // warm-up (JIT)

            var day = Stopwatch.StartNew();
            var run = scheduler.AdvanceDays(world, Days);
            day.Stop();
            Assert.That(run.Completed, Is.True, string.Join("; ", run.Errors));
            var w = run.World;
            var perDay = day.Elapsed.TotalMilliseconds / Days;

            // Every hour of a day: everyone's whereabouts, and every shop's availability.
            var hours = Stopwatch.StartNew();
            var open = 0;
            for (var hour = 0; hour < 24; hour++)
            {
                w.MinuteOfDay = hour * 60;
                foreach (var n in w.Npcs) if (!n.IsDeceased) NpcActivityPlan.Now(w, n);
                foreach (var s in w.Shops) if (CommerceMath.Availability(w, s).IsOpen) open++;
            }

            hours.Stop();
            var perHour = hours.Elapsed.TotalMilliseconds / 24;

            var prices = Stopwatch.StartNew();
            var quoted = 0;
            foreach (var s in w.Shops)
            foreach (var line in s.Stock)
            {
                CommerceMath.SellingPrice(w, s, ItemCatalog.Find(line.ItemId));
                quoted++;
            }

            prices.Stop();
            TestContext.Out.WriteLine(
                $"PERF5 {label}: {w.Npcs.Count} people, {w.Shops.Count} shops, {w.Shops.Sum(s => s.Stock.Sum(l => l.Quantity))} units in stock | " +
                $"day {perDay:F2} ms | one hour of whereabouts + availability {perHour:F3} ms ({open} shop-hours open) | " +
                $"{quoted} prices {prices.Elapsed.TotalMilliseconds:F2} ms");
            Assert.That(perDay, Is.LessThan(maxMsPerDay), $"{label}: {perDay:F2} ms per simulated day.");
            Assert.That(perHour, Is.LessThan(maxMsPerHour), $"{label}: {perHour:F3} ms per hour of whereabouts.");
            Assert.That(open, Is.GreaterThan(0), "Shops open during the day.");
        }
    }
}
