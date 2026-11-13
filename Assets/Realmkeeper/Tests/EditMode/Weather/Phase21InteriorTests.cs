using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Settlements;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>Phase 21 (ADR-0108): furnished interiors — planned from each room's kind, deterministic and validated.</summary>
    public sealed class Phase21InteriorTests
    {
        private static readonly BuildingFunction[] Functions =
        {
            BuildingFunction.Home, BuildingFunction.LargeHome, BuildingFunction.Cottage, BuildingFunction.Farmhouse, BuildingFunction.Workshop,
            BuildingFunction.Warehouse, BuildingFunction.GuardPost, BuildingFunction.Religious, BuildingFunction.Civic,
        };

        [Test]
        public void Every_kind_of_building_is_furnished_soundly_over_many_seeds()
        {
            var pieces = 0;
            foreach (var fn in Functions)
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var layout = InteriorGenerator.Generate(new InteriorRequest { Function = fn, WidthMetres = 9f + seed % 5, DepthMetres = 8f + seed % 4, Storeys = seed % 3 == 0 ? 2 : 1, Wealth = (int)(seed * 13 % 100) }, seed);
                var (rooms, blocked) = InteriorFurnishing.Inputs(layout);
                var plan = InteriorFurnishing.Plan(rooms, blocked, seed);
                Assert.That(InteriorFurnishing.Validate(rooms, blocked, plan), Is.Empty, $"{fn} seed {seed}");
                pieces += plan.Count;
            }

            Assert.That(pieces, Is.GreaterThan(Functions.Length * 60), "rooms are furnished, not bare");
        }

        [Test]
        public void The_same_seed_furnishes_the_same_way()
        {
            var layout = InteriorGenerator.Generate(new InteriorRequest { Function = BuildingFunction.LargeHome, WidthMetres = 12f, DepthMetres = 10f, Storeys = 2, Wealth = 70 }, 77);
            var (rooms, blocked) = InteriorFurnishing.Inputs(layout);
            string Key(System.Collections.Generic.List<FurnishingPiece> p) => string.Join("|", p.Select(x => $"{x.Room}:{x.Prop}:{x.X},{x.Z}:{x.Yaw}"));
            Assert.That(Key(InteriorFurnishing.Plan(rooms, blocked, 5)), Is.EqualTo(Key(InteriorFurnishing.Plan(rooms, blocked, 5))));
            Assert.That(Key(InteriorFurnishing.Plan(rooms, blocked, 6)), Is.Not.EqualTo(Key(InteriorFurnishing.Plan(rooms, blocked, 5))));
        }

        [Test]
        public void Bedrooms_have_beds_and_cellars_have_stores()
        {
            int bedrooms = 0, withBed = 0, stores = 0, withStores = 0;
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var layout = InteriorGenerator.Generate(new InteriorRequest { Function = seed % 2 == 0 ? BuildingFunction.LargeHome : BuildingFunction.Home, WidthMetres = 11f, DepthMetres = 10f, Storeys = 2, Wealth = 80 }, seed);
                var (rooms, blocked) = InteriorFurnishing.Inputs(layout);
                var plan = InteriorFurnishing.Plan(rooms, blocked, seed);
                foreach (var r in rooms)
                {
                    if (r.Kind == RoomKind.Bedroom) { bedrooms++; if (plan.Any(p => p.Room == r.Id && p.Kind == "bed")) withBed++; }
                    if (r.Kind == RoomKind.Cellar || r.Kind == RoomKind.Storage) { stores++; if (plan.Any(p => p.Room == r.Id)) withStores++; }
                }
            }

            TestContext.WriteLine($"{withBed}/{bedrooms} bedrooms with a bed; {withStores}/{stores} stores furnished.");
            Assert.That(bedrooms, Is.GreaterThan(0));
            Assert.That(withBed, Is.GreaterThanOrEqualTo(bedrooms * 9 / 10), "nearly every bedroom has room for a bed (a tiny one may not)");
            if (stores > 0) Assert.That(withStores, Is.GreaterThanOrEqualTo(stores * 9 / 10));
        }
    }
}
