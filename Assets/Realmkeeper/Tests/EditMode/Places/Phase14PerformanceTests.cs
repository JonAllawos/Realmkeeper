using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Places;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 14 costs (.NET 8, Release; printed and bounded loosely — Editor and harness figures, not production):
    /// sight and cover, AI routes with sight, the indoor schedule for a crowded building, follower routes, witnesses,
    /// generating the new places, many lasting conditions, and saving and loading the world with every place.
    /// </summary>
    public sealed class Phase14PerformanceTests : Phase14Fixture
    {
        private sealed class Open : ISightGrid
        {
            private readonly bool[,] _wall;
            public Open(int size, int seed)
            {
                _wall = new bool[size, size];
                var r = new Random(seed);
                for (var x = 0; x < size; x++) for (var y = 0; y < size; y++) _wall[x, y] = r.Next(100) < 15;
            }

            public bool IsWall(int x, int y) => x < 0 || y < 0 || x >= _wall.GetLength(0) || y >= _wall.GetLength(1) || _wall[x, y];
            public string WallName(int x, int y) => "a wall";
            public bool IsLowCover(int x, int y) => false;
            public string LowCoverName(int x, int y) => string.Empty;
            public string CreatureAt(int x, int y) => null;
        }

        [Test]
        public void Phase14_IsCheap()
        {
            // Sight: 10,000 cover checks on a 40x40 grid with 15 % walls (distances up to 40 squares).
            var grid = new Open(40, 7);
            var rnd = new Random(1);
            var sw = Stopwatch.StartNew();
            var total = 0;
            for (var i = 0; i < 10000; i++)
                if (GridSight.Cover(grid, new GridPos(rnd.Next(40), rnd.Next(40)), new GridPos(rnd.Next(40), rnd.Next(40))).HasLineOfEffect) total++;
            var cover = sw.Elapsed.TotalMilliseconds / 10000;
            sw.Restart();
            for (var i = 0; i < 10000; i++) GridSight.CentreLineClear(grid, new GridPos(rnd.Next(40), rnd.Next(40)), new GridPos(rnd.Next(40), rnd.Next(40)));
            var centre = sw.Elapsed.TotalMilliseconds / 10000;

            // Generation of the five new places.
            sw.Restart();
            var n = 0;
            foreach (var def in Phase14Places.Definitions())
                for (var seed = 1UL; seed <= 40; seed++) { PlaceGenerator.Generate(ArchetypeCatalog.Find(def.ArchetypeId), seed * 13UL, def); n++; }
            var generate = sw.Elapsed.TotalMilliseconds / n;

            // The world: a crowded building's indoor schedule over a day, follower routes, witnesses.
            H.Hero();
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(town.SettlementId));
            PlaceData busiest = null;
            var most = -1;
            foreach (var b in layout.Buildings.Where(b => b.Function != null && b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None &&
                                                          InteriorGenerator.HasInterior(b.Function.Value)).Take(12))
            {
                if (W.Player.IsInPlace) Ok(S.Dispatch(new ExitPlaceCommand()));
                if (!S.Dispatch(new EnterBuildingCommand(b.Key)).IsSuccess) continue;
                var p = W.FindPlace(W.Player.PlaceId);
                var people = Enumerable.Range(0, 24).Max(h => IndoorSchedule.Everyone(W, p, W.CurrentSimulationDate.ToDayNumber() * 1440L + h * 60).Count);
                if (people > most) { most = people; busiest = p; }
            }

            var w = W;
            var day = w.CurrentSimulationDate.ToDayNumber() * 1440L;
            sw.Restart();
            for (var m = 0; m < 1440; m += 5) IndoorSchedule.Everyone(w, busiest, day + m);
            var indoor = sw.Elapsed.TotalMilliseconds / (1440 / 5);
            var room = busiest.Layout.Rooms.Last();
            sw.Restart();
            for (var i = 0; i < 1000; i++) PlaceRoutes.Find(busiest, busiest.Layout.Entrance.A, room.Id, Mover.Follower);
            var route = sw.Elapsed.TotalMilliseconds / 1000;
            sw.Restart();
            for (var i = 0; i < 200; i++) CrimeRules.Witnesses(w, busiest, room, room.Rect.CenterX, room.Rect.CenterZ, false);
            var witnesses = sw.Elapsed.TotalMilliseconds / 200;

            // Many lasting conditions: apply, query and expire.
            var sheet = w.Player.Rpg.Clone();
            sw.Restart();
            for (var i = 0; i < 1000; i++)
            {
                LastingConditions.Apply(sheet, ConditionType.Poisoned, "s" + i, "x", i, 30, ConditionRemedy.Restoration);
                LastingConditions.Active(sheet, i);
                LastingConditions.Expire(sheet, i + 31);
            }

            var conditions = sw.Elapsed.TotalMilliseconds / 1000;

            // Save and load with every place.
            var serializer = new JsonWorldSerializer();
            sw.Restart();
            var json = serializer.Serialize(w);
            var save = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            Assert.That(serializer.Deserialize(json).IsSuccess, Is.True);
            var load = sw.Elapsed.TotalMilliseconds;

            TestContext.WriteLine($"Cover (corner lines, 40x40 grid, 15% walls): {cover * 1000:0.0} µs per check ({total} of 10000 in line); area line of effect {centre * 1000:0.0} µs");
            TestContext.WriteLine($"Generating the five new places: {generate:0.00} ms each ({n} layouts)");
            TestContext.WriteLine($"Indoor schedule for the busiest building ({most} people at once): {indoor:0.000} ms per query; follower route {route * 1000:0.0} µs; witnesses {witnesses:0.000} ms");
            TestContext.WriteLine($"The indoor schedule consults every person's plan: {w.Npcs.Count} people in the world, so {indoor * 1000 / Math.Max(1, w.Npcs.Count):0.0} µs per person per query");
            TestContext.WriteLine($"Lasting conditions: apply + query + expire {conditions * 1000:0.0} µs");
            TestContext.WriteLine($"Save {json.Length / 1024} KB in {save:0} ms, load {load:0} ms; places in the world: {w.Places.Count}");
            Assert.That(cover, Is.LessThan(2), "A cover check");
            Assert.That(indoor, Is.LessThan(50), "The indoor schedule");
            Assert.That(generate, Is.LessThan(50));
        }
    }
}
