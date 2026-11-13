using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Tests.EditMode.Atlas;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>Phase 21 (ADR-0106): shop kinds, specialists in the town's unused shop buildings, supply records and shortfalls.</summary>
    public sealed class Phase21ShopTests : Phase16Fixture
    {
        private static readonly ShopKind[] Phase5Kinds = { ShopKind.GeneralStore, ShopKind.Outfitter, ShopKind.Smithy, ShopKind.TavernKitchen, ShopKind.GranaryYard, ShopKind.FarmStall };

        [Test]
        public void Every_shop_has_a_kind_and_the_catalog_is_sound()
        {
            var w = W;
            Assert.That(w.Shops.All(s => s.Kind != ShopKind.Unspecified), Is.True);
            foreach (var d in ShopKinds.All)
            {
                Assert.That(d.Items.Length > 0 || d.Categories.Length > 0, Is.True, d.Name + " deals in something");
                foreach (var id in d.Items) Assert.That(ItemCatalog.Find(id), Is.Not.Null, $"{d.Name}: {id} is a real item");
                Assert.That(d.Keepers, Is.Not.Empty);
                Assert.That(d.Opens, Is.LessThan(d.Closes));
            }
        }

        [Test]
        public void Towns_gain_specialists_in_their_unused_shop_buildings_kept_by_their_own_people()
        {
            var w = W;
            var specialists = w.Shops.Where(s => !Phase5Kinds.Contains(s.Kind)).ToList();
            Assert.That(specialists, Is.Not.Empty, "the six-region world has specialist shops");
            TestContext.WriteLine(string.Join("\n", specialists.Select(s => $"{w.FindSettlement(s.SettlementId).Name}: {s.Name} ({s.Kind}), {s.Stock.Sum(x => x.Quantity)} units")));
            foreach (var town in w.Settlements.Where(x => x.SettlementType >= SettlementType.Town))
                TestContext.WriteLine($"ROLES {town.Name}: " + string.Join(", ", w.Npcs.Where(n => n.HomeSettlementId == town.SettlementId && !n.IsDeceased)
                    .Select(n => n.Role + (w.Shops.Any(x => x.ShopkeeperNpcId == n.NpcId) ? "*" : "") + (n.Tier == NpcTier.Unassigned ? "?" : "") + (n.ResidenceId.IsEmpty ? "-" : ""))));
            foreach (var shop in specialists)
            {
                var s = w.FindSettlement(shop.SettlementId);
                var def = ShopKinds.Find(shop.Kind);
                Assert.That(s.SettlementType, Is.GreaterThanOrEqualTo(def.MinimumSize), shop.Name);
                var place = w.FindLocation(shop.LocationId);
                var building = w.FindLayout(s.SettlementId).FindBuilding(place.LayoutKey);
                Assert.That(building, Is.Not.Null, shop.Name + " stands in a planned building");
                Assert.That(building.Function, Is.EqualTo(BuildingFunction.Shop));
                Assert.That(building.IsSite, Is.True);
                Assert.That(w.Locations.Count(l => l.SettlementId == s.SettlementId && l.LayoutKey == place.LayoutKey), Is.EqualTo(1), "one place per building");
                var keeper = w.FindNpc(shop.ShopkeeperNpcId);
                Assert.That(keeper.HomeSettlementId, Is.EqualTo(s.SettlementId), "a resident keeps it");
                Assert.That(keeper.WorkplaceId, Is.EqualTo(shop.LocationId));
                Assert.That(def.Keepers, Does.Contain(keeper.Role));
                Assert.That(w.Shops.Count(x => x.ShopkeeperNpcId == keeper.NpcId), Is.EqualTo(1));
                foreach (var line in shop.Stock) Assert.That(def.Items, Does.Contain(line.ItemId), $"{shop.Name} holds only its trade's goods");
            }

            Valid();
        }

        [Test]
        public void Placing_specialists_again_changes_nothing()
        {
            var w = W;
            string Json() => JObject.Parse(new JsonWorldSerializer().Serialize(w))["world"].ToString();
            var before = Json();
            Assert.That(CommerceGenesis.EnsureKinds(w), Is.EqualTo(0));
            Assert.That(Json(), Is.EqualTo(before));
        }

        [Test]
        public void Stock_is_recorded_with_where_it_came_from()
        {
            var w = W;
            var withSupply = w.Shops.Where(s => s.Supply.Count > 0).ToList();
            Assert.That(withSupply, Is.Not.Empty);
            foreach (var shop in withSupply)
            foreach (var r in shop.Supply)
            {
                var item = ItemCatalog.Find(r.ItemId);
                Assert.That(r.Quantity, Is.GreaterThan(0));
                if (r.Origin == SupplyOrigin.MadeLocally) Assert.That(item.Materials, Is.Not.Empty, "made here from the stores");
                if (r.Origin == SupplyOrigin.Imported) Assert.That(item.Materials, Is.Empty, "only what is not made here comes by road");
                Assert.That(r.Detail, Is.Not.Empty);
            }
        }

        [Test]
        public void Imports_need_an_open_trade_road_and_the_shop_says_why()
        {
            var w = W;
            var shop = w.Shops.First(s => w.FindSettlement(s.SettlementId).Stability >= 30 &&
                                          ItemCatalog.Items.Any(i => i.Materials.Count == 0 && CommerceMath.Sells(s, i)));
            var item = ItemCatalog.Items.First(i => i.Materials.Count == 0 && CommerceMath.Sells(shop, i));
            foreach (var r in w.TradeRoutes) if (r.OriginId == shop.SettlementId || r.DestinationId == shop.SettlementId) r.Status = TradeRouteStatus.Suspended;
            shop.Stock.RemoveAll(l => l.ItemId == item.Id);
            w.FindNpc(shop.ShopkeeperNpcId).Condition.Wealth = 10000;
            Assert.That(CommerceMath.ImportRoute(w, shop.SettlementId), Is.Null);
            CommerceMath.Restock(w, shop, new CommerceRules());
            Assert.That(shop.QuantityOf(item.Id), Is.EqualTo(0), "nothing comes in with every road shut");
            Assert.That(CommerceMath.Shortfall(w, shop, item), Does.Contain("trade road"));

            foreach (var r in w.TradeRoutes) if (r.OriginId == shop.SettlementId || r.DestinationId == shop.SettlementId) r.Status = TradeRouteStatus.Active;
            CommerceMath.Restock(w, shop, new CommerceRules());
            Assert.That(shop.QuantityOf(item.Id), Is.GreaterThan(0), "with a road open it is bought in");
            Assert.That(shop.Supply.Last(r => r.ItemId == item.Id).Origin, Is.EqualTo(SupplyOrigin.Imported));
            Assert.That(shop.Supply.Last(r => r.ItemId == item.Id).Detail, Does.Contain("road from"));
        }

        [Test]
        public void The_shop_detail_names_its_trade_its_shortfalls_and_its_supplies()
        {
            var shop = W.Shops.First(s => s.Kind == ShopKind.Leatherworker || s.Kind == ShopKind.Smithy);
            var detail = S.GetShopDetail(shop.ShopId);
            Assert.That(detail.Trade, Is.Not.Empty);
            Assert.That(detail.Summary.Kind, Is.EqualTo(shop.Kind));
            Assert.That(detail.Supply.Count, Is.EqualTo(shop.Supply.Count));
            foreach (var (_, _, why) in detail.Shortfalls) Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void Kinds_and_supply_survive_saving_and_an_older_save_gets_the_same_kinds()
        {
            var before = W.Shops.ToDictionary(s => s.ShopId, s => (s.Kind, s.Supply.Count));
            Edit(_ => { });
            foreach (var s in W.Shops) Assert.That((s.Kind, s.Supply.Count), Is.EqualTo(before[s.ShopId]));

            var doc = JObject.Parse(new JsonWorldSerializer().Serialize(W));
            doc["schemaVersion"] = 22;
            // A v22 save had only the Phase 5 kinds (with categories) and no records; specialists did not exist yet.
            var shops = (JArray)doc["world"]["shops"];
            foreach (var s in shops.Children<JObject>().ToList())
            {
                var kind = (string)s["kind"];
                if (!Phase5Kinds.Select(k => k.ToString()).Contains(kind)) { s.Remove(); continue; }
                s.Remove("kind");
                s.Remove("supply");
            }

            // (Their places and keepers stay: a world may have spare locations; only the shops are taken out here.)
            foreach (var st in ((JArray)doc["world"]["structures"]).Children<JObject>().ToList())
                if (st["shopId"] != null && !shops.Children<JObject>().Any(s => (string)s["shopId"] == (string)st["shopId"]) && (string)st["shopId"] != System.Guid.Empty.ToString()) st["shopId"] = System.Guid.Empty.ToString();
            var loaded = new JsonWorldSerializer().Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors ?? new string[0]));
            foreach (var s in loaded.World.Shops) s.Kind = ShopKind.Unspecified; // as a v22 save reads
            CommerceGenesis.EnsureKinds(loaded.World);
            foreach (var s in loaded.World.Shops.Where(x => before.ContainsKey(x.ShopId) && Phase5Kinds.Contains(before[x.ShopId].Kind)))
                Assert.That(s.Kind, Is.EqualTo(before[s.ShopId].Kind), s.Name + " is given the kind it was made as");
        }
    }
}
