using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Places;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Campaigns;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 18: measured costs of what Phase 18 added, on the same six-region world as the Phase 16 and 17 measurements, so
    /// the figures compare:
    /// <list type="bullet">
    /// <item>generating each new place's layout, its finishing step included;</item>
    /// <item>the day's story pass, now with seven kinds;</item>
    /// <item>the overheard exchanges of a town;</item>
    /// <item>which findings a person would care about;</item>
    /// <item>a simulated day;</item>
    /// <item>save and load.</item>
    /// </list>
    /// It also checks determinism: the same world twice gives the same Phase 18 places, layouts, quest bindings, and the
    /// same world after ten days. Generous bounds catch pathological regressions; the numbers go to the test output.
    /// </summary>
    public sealed class Phase18PerformanceTests : Phase17Fixture
    {
        [Test]
        public void ThePhase18Content_IsMeasured()
        {
            Hero();
            Days(2); // warm up
            var sw = Stopwatch.StartNew();
            Days(10);
            sw.Stop();
            var dayMs = sw.Elapsed.TotalMilliseconds / 10;

            var layouts = 0;
            sw.Restart();
            foreach (var def in Phase18Places.Definitions())
            {
                var a = ArchetypeCatalog.Find(def.ArchetypeId);
                for (var seed = 1UL; seed <= 20; seed++, layouts++) PlaceGenerator.Generate(a, seed * 104729UL, def);
            }

            sw.Stop();
            var layoutMs = sw.Elapsed.TotalMilliseconds / layouts;

            // One snapshot for the timings below: W copies the whole world on every read, and timing that copy (which grew with
            // Phase 23's nine regions) measured the test, not the game. The save and load timings already did this.
            var world = W;
            sw.Restart();
            for (var i = 0; i < 200; i++) StorySynthesis.Run(world, null);
            sw.Stop();
            var storyMs = sw.Elapsed.TotalMilliseconds / 200;

            var town = world.Player.SettlementId;
            sw.Restart();
            for (var i = 0; i < 200; i++) NpcExchanges.Now(world, town);
            sw.Stop();
            var exchangeMs = sw.Elapsed.TotalMilliseconds / 200;

            var people = world.Npcs.Where(n => n.HomeSettlementId == town).Take(20).ToList();
            sw.Restart();
            for (var i = 0; i < 20; i++) foreach (var n in people) DiscoveryRules.Offerable(world, n);
            sw.Stop();
            var offerMs = sw.Elapsed.TotalMilliseconds / (20 * people.Count);

            var w = W;
            var json = new JsonWorldSerializer().Serialize(w);
            sw.Restart();
            for (var i = 0; i < 3; i++) new JsonWorldSerializer().Serialize(w);
            sw.Stop();
            var saveMs = sw.Elapsed.TotalMilliseconds / 3;
            sw.Restart();
            for (var i = 0; i < 3; i++) new JsonWorldSerializer().Deserialize(json);
            sw.Stop();
            var loadMs = sw.Elapsed.TotalMilliseconds / 3;

            var places = w.Places.Count(p => Phase18Places.All.Contains(p.DefinitionId));
            TestContext.WriteLine($"World: {w.Places.Count} places ({places} from Phase 18), {w.Quests.Count} quests, {Realmkeeper.Domain.Quests.QuestCatalog.All.Count()} quest definitions, {w.Npcs.Count} people.");
            TestContext.WriteLine($"Layout generation (finishing step included): {layoutMs:0.00} ms per place over {layouts}. Story pass: {storyMs:0.000} ms. " +
                                  $"A town's overheard exchanges: {exchangeMs:0.000} ms. Findings a person would care about: {offerMs:0.0000} ms.");
            TestContext.WriteLine($"Simulated day: {dayMs:0.0} ms. Save {saveMs:0} ms, load {loadMs:0} ms, {json.Length / 1024} KB.");
            Assert.That(layoutMs, Is.LessThan(200));
            Assert.That(storyMs, Is.LessThan(20));
            Assert.That(exchangeMs, Is.LessThan(5));
            // Raised from 1 ms to 3 ms at the owner's instruction (2026-11-12, Phase 19): the first run of this test inside
            // Unity measured 1.88 ms per person (Mono), against 0.42 ms under .NET. The bound still catches a pathological
            // regression; it is evaluated once per conversation, not per frame.
            Assert.That(offerMs, Is.LessThan(3));
            Assert.That(dayMs, Is.LessThan(200));
        }

        [Test]
        public void AnEventThatLastsYears_KeepsOneKeyPerPeriod_NotOnePerWeek()
        {
            var e = new Realmkeeper.Domain.World.WorldEventData();
            e.Applied.Add("blockade");
            for (var week = 0L; week < 200; week++)
            {
                Assert.That(e.OncePer("strain:", week), Is.True);
                Assert.That(e.OncePer("strain:", week), Is.False, "Once within the period.");
                Assert.That(e.OncePer("garrison:a:", week), Is.True);
            }

            Assert.That(e.Applied, Is.EquivalentTo(new[] { "blockade", "strain:199", "garrison:a:199" }), "Bounded: other keys kept, one key per prefix.");
        }

        [Test]
        public void TheSameWorld_GivesTheSamePhase18Places_Chains_AndTenDays()
        {
            string Run()
            {
                SetUp();
                Hero();
                var places = string.Join("|", W.Places.Where(p => Phase18Places.All.Contains(p.DefinitionId)).OrderBy(p => p.PlaceId, System.StringComparer.Ordinal)
                    .Select(p => p.PlaceId + ":" + p.Seed + ":" + p.Layout?.Rooms.Count + ":" + string.Join(",", p.Layout?.Features.Select(f => f.Id + "@" + f.Room) ?? Enumerable.Empty<string>())));
                var quests = string.Join("|", W.Quests.Where(q => q.DefinitionId != null).OrderBy(q => q.InstanceId, System.StringComparer.Ordinal)
                    .Select(q => q.DefinitionId + ":" + string.Join(",", q.Bindings.OrderBy(b => b.Key, System.StringComparer.Ordinal).Select(b => b.Key + "=" + b.Value))));
                Days(10);
                return places + "#" + quests + "#" + WorldFingerprint.Of(W);
            }

            var a = Run();
            TearDown();
            var b = Run();
            Assert.That(a.Length, Is.GreaterThan(100));
            Assert.That(b, Is.EqualTo(a));
        }
    }
}
