using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>Phase 21 (ADR-0107): physical work stations in shops, the new recipes, and the keepers' own daily making.</summary>
    public sealed class Phase21StationTests
    {
        private string _dir;
        private WorldSession _s;
        private int _ids;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_s);
            _ids = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private void Edit(Action<WorldData> change)
        {
            var w = W;
            change(w);
            var saved = new JsonWorldStore(_dir).Save(w, "edit");
            Assert.That(saved.IsSuccess, Is.True, saved.Error);
            var load = _s.Execute(new LoadWorldCommand("edit"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
        }

        private void Give(string itemId, int quantity = 1)
        {
            var def = ItemCatalog.Find(itemId);
            var stacks = new List<ItemStackData>();
            for (var i = 0; i < (def.Stackable ? 1 : quantity); i++)
            {
                _ids++;
                var b = new byte[16];
                BitConverter.GetBytes(_ids).CopyTo(b, 0);
                b[15] = 0x21;
                stacks.Add(ItemStacks.Create(new ItemStackId(new Guid(b)), def, def.Stackable ? quantity : 1, "test"));
            }

            Edit(w => w.Player.Inventory.AddRange(stacks));
        }

        private void At(ShopData shop, bool open)
        {
            var w = W;
            for (var m = 0; m < 24 * 60; m += 15)
            {
                w.MinuteOfDay = m;
                if (CommerceMath.Availability(w, w.FindShop(shop.ShopId)).IsOpen != open) continue;
                var minute = m;
                Edit(x => x.MinuteOfDay = minute);
                return;
            }

            Assume.That(false, "No such hour.");
        }

        private static void Ok(CommandResult r) => Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));

        [Test]
        public void The_recipes_are_valid_and_every_station_is_kept_by_some_kind_of_shop()
        {
            Assert.That(RecipeCatalog.Validate(), Is.Empty);
            Assert.That(RecipeCatalog.Recipes.Count, Is.GreaterThanOrEqualTo(11 + 15));
            foreach (var r in RecipeCatalog.Recipes.Where(x => x.StationKind != StationKind.None))
                Assert.That(ShopKinds.All.Any(k => k.Stations.Contains(r.StationKind)), Is.True, $"{r.Name}: some shop keeps a {StationWords.Name(r.StationKind)}");
            foreach (var k in ShopKinds.All.Where(x => x.Stations.Length > 0 && x.Workshop)) // (the tavern hearth is for the player's cooking)
            foreach (var st in k.Stations)
                Assert.That(RecipeCatalog.Recipes.Any(r => r.StationKind == st && k.Deals(ItemCatalog.Find(r.OutputItemId))), Is.True, $"{k.Name}'s {StationWords.Name(st)} makes something it sells");
        }

        [Test]
        public void The_player_works_at_a_station_only_while_its_shop_is_open()
        {
            CombatTests.Create(_s, "fighter", CombatTests.Scores);
            CombatTests.EnterTown(_s);
            var town = W.Player.SettlementId;
            var forge = CraftingStations.In(W, town, StationKind.Forge).FirstOrDefault();
            Assume.That(forge, Is.Not.Null, "The town has a smithy.");
            Give("iron_bar", 2);
            Give("timber_plank", 1);
            Give("smiths_tools", 1);

            At(forge, false);
            var closed = _s.Dispatch(StartWorkCommand.Craft("forge_shortsword", forge.ShopId));
            Assert.That(closed.IsSuccess, Is.False);
            Assert.That(closed.Message, Does.Contain(forge.Name), "the refusal names where the forge is and why it cannot be used");

            At(forge, true);
            Ok(_s.Dispatch(StartWorkCommand.Craft("forge_shortsword", forge.ShopId)));
            Assert.That(W.WorkJobs.Any(j => j.RecipeId == "forge_shortsword"), Is.True);
            Assert.That(W.Player.Inventory.Any(s => s.ItemId == "iron_bar"), Is.False, "the materials are held by the work");
        }

        [Test]
        public void A_station_recipe_is_refused_where_no_shop_keeps_the_station()
        {
            CombatTests.Create(_s, "fighter", CombatTests.Scores);
            CombatTests.EnterTown(_s);
            var town = W.Player.SettlementId;
            Assume.That(CraftingStations.In(W, town, StationKind.SewingTable).Count, Is.EqualTo(0).Or.GreaterThan(0));
            Edit(w => { foreach (var s in w.Shops.Where(x => x.SettlementId == town && CraftingStations.Provides(x, StationKind.SewingTable))) { s.Kind = ShopKind.GeneralStore; s.Categories = new List<ShopCategory> { ShopCategory.General }; } });
            Give("linen_cloth", 2);
            Give("weavers_tools", 1);
            var r = _s.Dispatch(StartWorkCommand.Craft("sew_tunic"));
            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.Message, Does.Contain("sewing table"));
        }

        [Test]
        public void Keepers_make_goods_at_their_own_station_from_their_own_stock()
        {
            var w = W;
            var shop = w.Shops.FirstOrDefault(s => s.Kind == ShopKind.Leatherworker);
            Assume.That(shop, Is.Not.Null, "A leatherworks exists.");
            shop.Stock.Clear();
            CommerceMath.AddStock(shop, "cured_leather", 6);
            var before = 6;
            shop.Supply.Clear(); // only what this day makes
            var made = ShopProduction.Produce(w, shop);
            Assert.That(made, Is.GreaterThan(0));
            var leatherUsed = before - shop.QuantityOf("cured_leather");
            Assert.That(leatherUsed, Is.GreaterThan(0), "the inputs leave the stock");
            var produced = shop.Supply.Where(r => r.Origin == SupplyOrigin.Produced).ToList();
            Assert.That(produced.Sum(r => r.Quantity), Is.EqualTo(made));
            foreach (var r in produced)
            {
                Assert.That(ShopKinds.Find(shop.Kind).Items, Does.Contain(r.ItemId), "only its trade's goods");
                Assert.That(r.Detail, Does.Contain(w.FindNpc(shop.ShopkeeperNpcId).Name), "made by the keeper");
            }

            var def = ShopKinds.Find(shop.Kind);
            var minutes = produced.Sum(r => { var rec = RecipeCatalog.Recipes.First(x => x.OutputItemId == r.ItemId && ShopProduction.Works(shop, def, x)); return rec.Minutes * (r.Quantity / rec.OutputQuantity); });
            Assert.That(minutes, Is.LessThanOrEqualTo(ShopProduction.MinutesPerDay), "no more than a working day");

            // Nothing to work with: nothing made.
            shop.Stock.Clear();
            var supply = shop.Supply.Count;
            Assert.That(ShopProduction.Produce(w, shop), Is.EqualTo(0));
            Assert.That(shop.Supply.Count, Is.EqualTo(supply));
        }

        [Test]
        public void The_daily_tick_runs_production_and_it_is_deterministic()
        {
            Ok(_s.Execute(new AdvanceDaysCommand(10)));
            var a = W.Shops.Select(s => s.Name + ":" + string.Join(",", s.Stock.Select(l => l.ItemId + l.Quantity))).ToList();
            var other = Phase2Worlds.Session(Phase2Worlds.TempDirectory());
            ExplorationModule.Register(other);
            Ok(other.Execute(new AdvanceDaysCommand(10)));
            var b = other.CurrentWorldSnapshotForTests().Shops.Select(s => s.Name + ":" + string.Join(",", s.Stock.Select(l => l.ItemId + l.Quantity))).ToList();
            Assert.That(b, Is.EqualTo(a));
            TestContext.WriteLine(string.Join("\n", W.Shops.Select(s => $"{s.Name}: {s.Supply.Count(r => r.Origin == SupplyOrigin.Produced)} produced lines, {s.Stock.Sum(l => l.Quantity)} units, keeper wealth {W.FindNpc(s.ShopkeeperNpcId).Condition.Wealth}; {string.Join(" ", s.Supply.Select(r => r.Origin + ":" + r.ItemId))}")));
        }
    }
}
