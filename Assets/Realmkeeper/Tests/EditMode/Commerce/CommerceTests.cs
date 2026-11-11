using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Commerce
{
    /// <summary>Phase 5 through the session: shops, buying and selling, belongings, the clock, persistence.</summary>
    public sealed class CommerceTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_s);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        private static string World(string json) => JObject.Parse(json)["world"].ToString();

        /// <summary>The world section without its identity (two sessions of one seed differ only in world ID).</summary>
        private static string Content(string json)
        {
            var w = (JObject)JObject.Parse(json)["world"];
            w.Remove("worldId");
            return w.ToString();
        }

        private string Json() => World(new JsonWorldSerializer().Serialize(W));

        private SettlementData Town => W.Settlements.First(s => s.SettlementType == SettlementType.Town);

        private void Enter(SettlementId id)
        {
            var r = _s.Dispatch(new EnterSettlementCommand(id));
            Assert.That(r.IsSuccess, Is.True, r.ToString());
        }

        /// <summary>Advances the clock hour by hour until a shop in the town is open (its keeper there, in hours).</summary>
        private ShopSummary OpenShopInTown(Func<ShopSummary, bool> which = null)
        {
            Enter(Town.SettlementId);
            for (var hour = 0; hour < 24 * 4; hour++)
            {
                var open = _s.GetShops(Town.SettlementId).FirstOrDefault(s => s.IsOpen && (which == null || which(s)));
                if (open != null) return open;
                Assert.That(_s.Execute(new AdvanceTimeCommand(60)).IsSuccess, Is.True);
            }

            Assert.Fail("No shop in the town opened within four days.");
            return null;
        }

        private void AssertRejectedAndUnchanged<T>(T command, CommandStatus status) where T : ICommand
        {
            var before = Json();
            var r = _s.Dispatch(command);
            Assert.That(r.Status, Is.EqualTo(status), r.ToString());
            Assert.That(Json(), Is.EqualTo(before), "A rejected command changes nothing.");
        }

        // ----- Genesis -------------------------------------------------------------------------------

        [Test]
        public void NewWorld_HasShopsKeptByExistingResidents_StockedAndPlausible()
        {
            var w = W;
            Assert.That(w.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(w.Shops.Count, Is.GreaterThanOrEqualTo(2));
            var town = w.Shops.Where(s => s.SettlementId == Town.SettlementId).ToList();
            Assert.That(town.Select(s => s.Categories[0]), Has.Member(ShopCategory.General).And.Member(ShopCategory.WeaponsAndArmour),
                "A town has a general merchant and a smithy.");
            foreach (var shop in w.Shops)
            {
                var keeper = w.FindNpc(shop.ShopkeeperNpcId);
                Assert.That(keeper, Is.Not.Null, shop.Name);
                Assert.That(keeper.HomeSettlementId, Is.EqualTo(shop.SettlementId), "The keeper lives there.");
                Assert.That(keeper.WorkplaceId, Is.EqualTo(shop.LocationId), "The keeper works at the shop.");
                Assert.That(w.FindLocation(shop.LocationId).SettlementId, Is.EqualTo(shop.SettlementId));
                Assert.That(shop.Stock.Sum(l => l.Quantity), Is.GreaterThan(0), shop.Name + " opened with stock.");
                Assert.That(shop.Stock.All(l => CommerceMath.Sells(shop, ItemCatalog.Find(l.ItemId))), Is.True, "Only what the shop deals in.");
            }

            Assert.That(w.Shops.Select(s => s.ShopkeeperNpcId).Distinct().Count(), Is.EqualTo(w.Shops.Count), "One shop per keeper.");
        }

        [Test]
        public void NewPlayer_HasStartingKit_AndAValidAppearance()
        {
            var p = W.Player;
            Assert.That(p.IsOutfitted, Is.True);
            Assert.That(p.Purse, Is.EqualTo(CommerceGenesis.StartingPurse));
            Assert.That(p.Equipped(EquipmentSlot.Chest).ItemId, Is.EqualTo("tunic_linen"));
            Assert.That(p.Equipped(EquipmentSlot.Feet).ItemId, Is.EqualTo("shoes_simple"));
            Assert.That(p.Inventory.Select(i => i.ItemId), Has.Member("dagger_iron").And.Member("bread_loaf"));
            Assert.That(PlayerAppearanceOptions.Check(p.Appearance), Is.Null);
            var hub = _s.GetPlayerHub();
            Assert.That(hub.Equipment.Keys, Has.Member(EquipmentSlot.Chest));
            Assert.That(hub.Weapon, Is.EqualTo("Bare hands"), "The knife is in the pack, not drawn.");
        }

        // ----- Buying --------------------------------------------------------------------------------

        [Test]
        public void Buy_FullSequence_IsAtomicAndRecorded()
        {
            var shop = OpenShopInTown();
            var detail = _s.GetShopDetail(shop.ShopId);
            var line = detail.ForSale.Where(l => l.UnitPrice <= detail.PlayerPurse).OrderBy(l => l.UnitPrice).First();
            var before = W;
            var keeperBefore = before.FindNpc(shop.KeeperId).Condition.Wealth;
            var stockBefore = before.FindShop(shop.ShopId).QuantityOf(line.ItemId);
            var heldBefore = before.Player.Inventory.Where(i => i.ItemId == line.ItemId).Sum(i => i.Quantity);
            var eventsBefore = before.HistoricalEvents.Count;

            var r = _s.Dispatch(new BuyItemCommand(shop.ShopId, line.ItemId, 1, line.UnitPrice));
            Assert.That(r.IsSuccess, Is.True, r.ToString());

            var after = W;
            Assert.That(after.Player.Purse, Is.EqualTo(before.Player.Purse - line.UnitPrice), "Coin deducted.");
            Assert.That(after.Player.Inventory.Where(i => i.ItemId == line.ItemId).Sum(i => i.Quantity), Is.EqualTo(heldBefore + 1), "Goods transferred.");
            Assert.That(after.FindShop(shop.ShopId).QuantityOf(line.ItemId), Is.EqualTo(stockBefore - 1), "Stock reduced.");
            Assert.That(after.FindNpc(shop.KeeperId).Condition.Wealth, Is.EqualTo(keeperBefore + line.UnitPrice), "The merchant is credited.");
            Assert.That(after.Player.Purse + after.FindNpc(shop.KeeperId).Condition.Wealth, Is.EqualTo(before.Player.Purse + keeperBefore), "No coin created or lost.");
            var t = after.Transactions.Last();
            Assert.That(t.Kind, Is.EqualTo(TransactionKind.Purchase));
            Assert.That(t.Total, Is.EqualTo(line.UnitPrice));
            Assert.That(t.MinuteOfDay, Is.EqualTo(after.MinuteOfDay));
            Assert.That(after.HistoricalEvents.Count, Is.EqualTo(eventsBefore + 1));
            var e = after.HistoricalEvents.Last();
            Assert.That(e.EventType, Is.EqualTo(HistoricalEventType.PlayerPurchase));
            Assert.That(e.NpcIds, Has.Member(shop.KeeperId));
            Assert.That(EventFormatter.Format(e, id => after.FindSettlement(id)?.Name), Does.Contain("bought").And.Contain(shop.KeeperName));

            // Saved and loaded: purse, pack, stock and ledger survive.
            var json = new JsonWorldSerializer().Serialize(after);
            var back = new JsonWorldSerializer().Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            Assert.That(World(new JsonWorldSerializer().Serialize(back.World)), Is.EqualTo(World(json)), "Exact round trip.");
            Assert.That(back.World.FindShop(shop.ShopId).QuantityOf(line.ItemId), Is.EqualTo(stockBefore - 1));
            Assert.That(back.World.Player.Purse, Is.EqualTo(after.Player.Purse));
            Assert.That(back.World.Player.Inventory.Where(i => i.ItemId == line.ItemId).Sum(i => i.Quantity), Is.EqualTo(heldBefore + 1), "The goods are still in the pack after loading.");
            Assert.That(back.World.Transactions.Count, Is.EqualTo(after.Transactions.Count), "The ledger is kept.");
        }

        [Test]
        public void Buy_Refusals_ChangeNothing()
        {
            var shop = OpenShopInTown();
            var detail = _s.GetShopDetail(shop.ShopId);
            var line = detail.ForSale.First();

            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, line.ItemId, 1, line.UnitPrice + 1), CommandStatus.InvalidSessionState); // price moved
            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, line.ItemId, line.Quantity + 1, line.UnitPrice), CommandStatus.InvalidSessionState); // stock
            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, line.ItemId, 0, line.UnitPrice), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, "no_such_item", 1, 1), CommandStatus.NotFound);
            AssertRejectedAndUnchanged(new BuyItemCommand(new ShopId(Guid.NewGuid()), line.ItemId, 1, line.UnitPrice), CommandStatus.NotFound);

            // Not enough coin: spend the purse down by discarding nothing — set up via a dear item.
            var dear = detail.ForSale.OrderByDescending(l => l.UnitPrice).First();
            var many = Math.Min(dear.Quantity, W.Player.Purse / dear.UnitPrice + 1);
            if (many * dear.UnitPrice > W.Player.Purse)
                AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, dear.ItemId, many, dear.UnitPrice), CommandStatus.InvalidSessionState);

            // Closed at night.
            var minute = W.MinuteOfDay;
            _s.Execute(new AdvanceTimeCommand((WorldTime.MinutesPerDay - minute) + 2 * 60)); // 02:00
            Assert.That(_s.GetShops(Town.SettlementId).All(s => !s.IsOpen), Is.True, "Every shop is shut at 02:00.");
            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, line.ItemId, 1, line.UnitPrice), CommandStatus.InvalidSessionState);
        }

        [Test]
        public void Buy_FromAnotherSettlement_IsRefused()
        {
            var shop = OpenShopInTown();
            var line = _s.GetShopDetail(shop.ShopId).ForSale.First();
            Assert.That(_s.Dispatch(new ExitSettlementCommand()).IsSuccess, Is.True);
            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, line.ItemId, 1, line.UnitPrice), CommandStatus.InvalidSessionState);
        }

        [Test]
        public void AbsentShopkeeper_ShutsTheShop_InOpeningHours_AndNoOneIsSpawnedInTheirPlace()
        {
            var shop = OpenShopInTown(s => s.Categories.Contains(ShopCategory.General)); // not the tavern kitchen, which is at the tavern
            var people = W.Npcs.Count;
            // The DM sends the keeper to the tavern for the day: in shop hours, nobody is at the counter.
            var tavern = W.Locations.First(l => l.SettlementId == shop.SettlementId && l.Kind == LocationKind.Tavern);
            var r = _s.Dispatch(new MoveNpcCommand(shop.KeeperId, tavern.LocationId));
            Assert.That(r.IsSuccess, Is.True, r.ToString());
            var after = _s.GetShops(Town.SettlementId).First(s => s.ShopId == shop.ShopId);
            Assert.That(after.IsOpen, Is.False, "The keeper is away.");
            Assert.That(after.Status, Does.Contain("not here").And.Contain(tavern.Name));
            Assert.That(after.KeeperId, Is.EqualTo(shop.KeeperId), "Still their shop.");
            Assert.That(W.Npcs.Count, Is.EqualTo(people), "No replacement is spawned.");
            var line = _s.GetShopDetail(shop.ShopId).ForSale.First();
            AssertRejectedAndUnchanged(new BuyItemCommand(shop.ShopId, line.ItemId, 1, line.UnitPrice), CommandStatus.InvalidSessionState);
        }

        [Test]
        public void ClosedShop_ReportsWhereTheKeeperIs()
        {
            Enter(Town.SettlementId);
            var minute = W.MinuteOfDay;
            _s.Execute(new AdvanceTimeCommand(WorldTime.MinutesPerDay - minute + 3 * 60)); // 03:00
            var shop = _s.GetShops(Town.SettlementId).First();
            Assert.That(shop.IsOpen, Is.False);
            Assert.That(shop.Status, Does.Contain("Closed"));
        }

        // ----- Selling, regard, no arbitrage --------------------------------------------------------

        [Test]
        public void Sell_PaysFromTheKeepersTill_AndBuyingBackCostsMoreThanSellingEarns()
        {
            var shop = OpenShopInTown(s => s.Categories.Contains(ShopCategory.WeaponsAndArmour));
            var knife = W.Player.Inventory.First(i => i.ItemId == "dagger_iron");
            var offer = _s.GetShopDetail(shop.ShopId).Offers.First(o => o.StackId == knife.StackId);
            Assert.That(offer.Accepted, Is.True, offer.Explanation);
            var before = W;
            var r = _s.Dispatch(new SellItemCommand(shop.ShopId, knife.StackId, 1, offer.UnitPrice));
            Assert.That(r.IsSuccess, Is.True, r.ToString());
            var after = W;
            Assert.That(after.Player.Purse, Is.EqualTo(before.Player.Purse + offer.UnitPrice));
            Assert.That(after.FindNpc(shop.KeeperId).Condition.Wealth, Is.EqualTo(before.FindNpc(shop.KeeperId).Condition.Wealth - offer.UnitPrice));
            Assert.That(after.Player.Inventory.Any(i => i.StackId == knife.StackId), Is.False);
            Assert.That(after.FindShop(shop.ShopId).QuantityOf("dagger_iron"), Is.EqualTo(before.FindShop(shop.ShopId).QuantityOf("dagger_iron") + 1));
            Assert.That(after.HistoricalEvents.Last().EventType, Is.EqualTo(HistoricalEventType.PlayerSale));

            var buyBack = _s.GetShopDetail(shop.ShopId).ForSale.First(l => l.ItemId == "dagger_iron");
            Assert.That(buyBack.UnitPrice, Is.GreaterThan(offer.UnitPrice), "No profit in selling and buying back.");
        }

        [Test]
        public void Regard_GrowsOncePerDay_NotPerTrade()
        {
            var shop = OpenShopInTown();
            var line = _s.GetShopDetail(shop.ShopId).ForSale.Where(l => l.Quantity >= 2).OrderBy(l => l.UnitPrice).First();
            for (var i = 0; i < 2; i++)
            {
                var price = _s.GetShopDetail(shop.ShopId).ForSale.First(l => l.ItemId == line.ItemId).UnitPrice;
                Assert.That(_s.Dispatch(new BuyItemCommand(shop.ShopId, line.ItemId, 1, price)).IsSuccess, Is.True);
            }

            var memories = W.FindNpc(shop.KeeperId).Memories.Where(m => m.Subject != null && m.Subject.StartsWith("Sold ")).ToList();
            Assert.That(memories.Count, Is.EqualTo(line.UnitPrice * 2 >= PlayerCommandHandlers.MemorableTrade ? 2 : 1),
                "The keeper remembers the first trade with the player (and only large ones after it).");
            Assert.That(memories[0].Subject, Does.Contain(W.Player.Name));
            var regard = W.FindNpc(shop.KeeperId).PlayerRegard;
            Assert.That(regard.Familiarity, Is.EqualTo(PlayerCommandHandlers.TradeFamiliarity), "Repeated trades on one day count once.");
            Assert.That(regard.Trades, Is.GreaterThanOrEqualTo(2));
            Assert.That(_s.GetPlayerHub().KnownBy.Select(k => k.NpcId), Has.Member(shop.KeeperId));
        }

        [Test]
        public void Prices_AreExplained()
        {
            var shop = OpenShopInTown();
            var line = _s.GetShopDetail(shop.ShopId).ForSale.First();
            Assert.That(line.Factors, Has.Some.Contains("Shop markup"));
            Assert.That(line.Explanation, Does.StartWith("Base " + line.BaseValue));
            var d = ItemCatalog.Find(line.ItemId);
            Assert.That(line.UnitPrice, Is.InRange(d.BaseValue * 85 / 100, d.BaseValue * 170 / 100 + 1), "Never wildly volatile.");
        }

        // ----- Belongings ----------------------------------------------------------------------------

        [Test]
        public void Equip_Unequip_TwoHanded_AndInvalidEquipment()
        {
            var knife = W.Player.Inventory.First(i => i.ItemId == "dagger_iron");
            Assert.That(_s.Dispatch(new EquipItemCommand(knife.StackId)).IsSuccess, Is.True);
            Assert.That(W.Player.Equipped(EquipmentSlot.MainHand).StackId, Is.EqualTo(knife.StackId), "The same item moves; it is not copied.");
            Assert.That(W.Player.Inventory.Any(i => i.StackId == knife.StackId), Is.False, "One owner: equipped, not also in the pack.");
            Assert.That(_s.GetPlayerHub().Weapon, Is.EqualTo("Iron dagger"));

            var bread = W.Player.Inventory.First(i => i.ItemId == "bread_loaf");
            AssertRejectedAndUnchanged(new EquipItemCommand(bread.StackId), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new EquipItemCommand(knife.StackId), CommandStatus.InvalidSessionState); // already equipped
            AssertRejectedAndUnchanged(new UnequipItemCommand(EquipmentSlot.Back), CommandStatus.InvalidSessionState);

            // A two-handed spear clears the off hand; a shield then displaces the spear.
            var w = W;
            Give(w, "shield_round");
            Give(w, "spear_ash");
            _s = Reload(w);
            Assert.That(_s.Dispatch(new EquipItemCommand(W.Player.Inventory.First(i => i.ItemId == "shield_round").StackId)).IsSuccess, Is.True);
            Assert.That(_s.Dispatch(new EquipItemCommand(W.Player.Inventory.First(i => i.ItemId == "spear_ash").StackId)).IsSuccess, Is.True);
            Assert.That(W.Player.Equipped(EquipmentSlot.OffHand), Is.Null, "The spear needs both hands.");
            Assert.That(W.Player.Inventory.Select(i => i.ItemId), Has.Member("shield_round").And.Member("dagger_iron"));
            Assert.That(_s.Dispatch(new EquipItemCommand(W.Player.Inventory.First(i => i.ItemId == "shield_round").StackId)).IsSuccess, Is.True);
            Assert.That(W.Player.Equipped(EquipmentSlot.MainHand), Is.Null, "Taking up a shield puts the spear away.");

            Assert.That(_s.Dispatch(new UnequipItemCommand(EquipmentSlot.Chest)).IsSuccess, Is.True);
            Assert.That(W.Player.Equipped(EquipmentSlot.Chest), Is.Null);
            Assert.That(W.Player.Inventory.Select(i => i.ItemId), Has.Member("tunic_linen"));
        }

        [Test]
        public void SplitCombineDiscardAndEat()
        {
            var bread = W.Player.Inventory.First(i => i.ItemId == "bread_loaf");
            Assert.That(bread.Quantity, Is.EqualTo(3));
            Assert.That(_s.Dispatch(new SplitStackCommand(bread.StackId, 1)).IsSuccess, Is.True);
            var stacks = W.Player.Inventory.Where(i => i.ItemId == "bread_loaf").ToList();
            Assert.That(stacks.Select(s => s.Quantity).OrderBy(q => q), Is.EqualTo(new[] { 1, 2 }));
            AssertRejectedAndUnchanged(new SplitStackCommand(bread.StackId, 2), CommandStatus.ValidationFailed); // would empty it
            Assert.That(_s.Dispatch(new CombineStacksCommand(stacks[1].StackId, stacks[0].StackId)).IsSuccess, Is.True);
            Assert.That(W.Player.Inventory.Where(i => i.ItemId == "bread_loaf").Sum(i => i.Quantity), Is.EqualTo(3), "Nothing lost or made.");
            Assert.That(W.Player.Inventory.Count(i => i.ItemId == "bread_loaf"), Is.EqualTo(1));

            var fed = W.Player.Fed;
            var loaf = W.Player.Inventory.First(i => i.ItemId == "bread_loaf");
            Assert.That(_s.Dispatch(new UseItemCommand(loaf.StackId)).IsSuccess, Is.True);
            Assert.That(W.Player.Fed, Is.EqualTo(Math.Min(100, fed + ItemCatalog.Find("bread_loaf").Nourishment)));
            Assert.That(W.Player.Inventory.First(i => i.ItemId == "bread_loaf").Quantity, Is.EqualTo(2));
            var knife = W.Player.Inventory.First(i => i.ItemId == "dagger_iron");
            AssertRejectedAndUnchanged(new UseItemCommand(knife.StackId), CommandStatus.ValidationFailed);
            Assert.That(_s.Dispatch(new DiscardItemCommand(knife.StackId, 1)).IsSuccess, Is.True);
            Assert.That(W.Player.Inventory.Any(i => i.ItemId == "dagger_iron"), Is.False);
        }

        [Test]
        public void Appearance_OnlyWhatTheArtCanRender()
        {
            Assert.That(_s.Dispatch(new SetPlayerAppearanceCommand(new PlayerAppearanceData
                { Frame = NpcSex.Female, HairId = "Hair_Buns", BeardId = null, SkinTone = 3, HairColour = 4, ClothTint = 2 })).IsSuccess, Is.True);
            Assert.That(W.Player.Appearance.Frame, Is.EqualTo(NpcSex.Female));
            AssertRejectedAndUnchanged(new SetPlayerAppearanceCommand(new PlayerAppearanceData { Frame = NpcSex.Female, HairId = "Hair_Buns", BeardId = "Hair_Beard" }),
                CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new SetPlayerAppearanceCommand(new PlayerAppearanceData { Frame = NpcSex.Male, HairId = "Hair_Mohawk" }), CommandStatus.ValidationFailed);
            AssertRejectedAndUnchanged(new SetPlayerAppearanceCommand(new PlayerAppearanceData { Frame = NpcSex.Male, HairId = "Hair_Long", SkinTone = 9 }), CommandStatus.ValidationFailed);
            Assert.That(_s.Dispatch(new SetPlayerProfileCommand(age: 40, background: "Sellsword")).IsSuccess, Is.True);
            Assert.That(W.Player.Background, Is.EqualTo("Sellsword"));
            AssertRejectedAndUnchanged(new SetPlayerProfileCommand(age: 5), CommandStatus.ValidationFailed);
        }

        // ----- The clock -----------------------------------------------------------------------------

        [Test]
        public void Clock_AdvancesMinutes_RunsADayAtMidnight_AndSmallStepsEqualOneLargeStep()
        {
            var start = W;
            Assert.That(start.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute));
            Assert.That(_s.Execute(new AdvanceTimeCommand(90)).DaysAdvanced, Is.Zero);
            Assert.That(W.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute + 90));
            Assert.That(_s.GetWorldTime().Clock, Is.EqualTo("09:30"));
            Assert.That(_s.GetWorldTime().DayPart, Is.EqualTo(DayPart.Morning));

            var big = Phase2Worlds.Session(_dir + "-b");
            var small = Phase2Worlds.Session(_dir + "-c");
            try
            {
                Assert.That(big.Execute(new AdvanceTimeCommand(2000)).DaysAdvanced, Is.EqualTo(1));
                for (var i = 0; i < 200; i++) small.Execute(new AdvanceTimeCommand(10));
                Assert.That(Content(new JsonWorldSerializer().Serialize(small.CurrentWorldSnapshotForTests())),
                    Is.EqualTo(Content(new JsonWorldSerializer().Serialize(big.CurrentWorldSnapshotForTests()))),
                    "200 steps of 10 minutes give exactly the world of one 2000-minute step (clock, day, hunger).");
                Assert.That(big.CurrentWorldSnapshotForTests().CurrentSimulationDate, Is.EqualTo(start.CurrentSimulationDate.AddDays(1)));
                Assert.That(big.CurrentWorldSnapshotForTests().Player.Fed, Is.LessThan(start.Player.Fed), "The player grows hungry.");
            }
            finally
            {
                foreach (var d in new[] { _dir + "-b", _dir + "-c" }) if (Directory.Exists(d)) Directory.Delete(d, true);
            }

            Assert.That(_s.Execute(new AdvanceTimeCommand(0)).IsSuccess, Is.False);
            Assert.That(_s.Execute(new AdvanceTimeCommand(AdvanceTimeCommand.MaxMinutes + 1)).IsSuccess, Is.False);
        }

        [Test]
        public void Playback_LivingWorld_RunsMinutes_RealmMapRunsDays()
        {
            var playback = new SimulationPlayback(_s);
            playback.LivingWorld = true;
            playback.Play();
            playback.Tick(30.0); // Normal: two game minutes a second
            Assert.That(W.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute + 60));
            playback.Tick(0.25);
            Assert.That(W.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute + 60), "Half a minute is carried, not rounded up.");
            playback.Tick(0.25);
            Assert.That(W.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute + 61));
            playback.Tick(1000); // a hitch is clamped
            Assert.That(W.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute + 61 + SimulationPlayback.MaxLivingMinutesPerTick));
            playback.LivingWorld = false;
            var date = W.CurrentSimulationDate;
            var minute = W.MinuteOfDay;
            Assert.That(playback.Tick(1.0), Is.EqualTo(1));
            Assert.That(W.CurrentSimulationDate, Is.EqualTo(date.AddDays(1)));
            Assert.That(W.MinuteOfDay, Is.EqualTo(minute), "Whole days leave the time of day alone.");
        }

        [Test]
        public void Routines_SleepAtNight_WorkInHours_AndPresenceFollowsTheHour()
        {
            var w = W;
            var keeper = w.FindNpc(w.Shops.First().ShopkeeperNpcId);
            var date = w.CurrentSimulationDate;
            var night = NpcActivityPlan.At(w, keeper, date, 2 * 60);
            Assert.That(night.Activity, Is.EqualTo(ActivityKind.Sleeping));
            Assert.That(night.Location, Is.EqualTo(keeper.ResidenceId));

            // On some day of the week the keeper is at the counter in shop hours.
            var atWork = Enumerable.Range(0, 7).Select(d => NpcActivityPlan.At(w, keeper, date.AddDays(d), 10 * 60 + 30))
                .FirstOrDefault(x => x.Location == keeper.WorkplaceId);
            Assert.That(atWork.IsKnown, Is.True, "A shopkeeper works at the shop on work days.");
            Assert.That(atWork.Activity, Is.EqualTo(ActivityKind.Trading));
            Assert.That(NpcActivityPlan.At(w, keeper, date, 600), Is.EqualTo(NpcActivityPlan.At(w.DeepClone(), keeper, date, 600)), "Deterministic.");

            var asleep = _s.GetPresenceIn(Town.SettlementId);
            Assert.That(asleep, Is.Not.Empty);
        }

        // ----- Conversation ---------------------------------------------------------------------------

        [Test]
        public void Topics_AreBoundToWhatThePersonKnows()
        {
            var shop = OpenShopInTown();
            var w = W;
            var keeper = w.FindNpc(shop.KeeperId);
            var goods = NpcDialogue.Answer(w, keeper, ConversationTopic.Goods, default, "Wren");
            Assert.That(goods, Does.StartWith("I have").Or.Contain("bare"));
            var prices = NpcDialogue.Answer(w, keeper, ConversationTopic.Prices, default, "Wren");
            Assert.That(prices, Does.Contain("coin"));

            var other = w.Npcs.First(n => !n.IsDeceased && n.HomeSettlementId == Town.SettlementId && w.Shops.All(s => s.ShopkeeperNpcId != n.NpcId));
            Assert.That(NpcDialogue.Answer(w, other, ConversationTopic.Shops, default, "Wren"), Does.Contain(shop.Name));
            Assert.That(NpcDialogue.Answer(w, other, ConversationTopic.Prices, default, "Wren"), Does.Not.Match("[0-9]"),
                "Only a shopkeeper names exact prices.");
            var stranger = w.Npcs.First(n => !n.IsDeceased && n.HomeSettlementId != Town.SettlementId);
            Assert.That(TradeTalkFor(w, stranger, ConversationTopic.Shops), Does.Contain("not from"));
        }

        private string TradeTalkFor(WorldData w, NpcData npc, ConversationTopic topic)
        {
            // Put the stranger in the town for the question (as if visiting).
            var copy = w.DeepClone();
            var n = copy.FindNpc(npc.NpcId);
            n.CurrentLocationId = copy.Locations.First(l => l.SettlementId == Town.SettlementId).LocationId;
            return NpcDialogue.Answer(copy, n, topic, default, "Wren");
        }

        // ----- Persistence, migration and invariants -----------------------------------------------

        [Test]
        public void Version6Save_GainsCommerceOnce_AndMatchesAFreshWorld()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(Phase2Worlds.GeneratedBeforeCommerce(4242)));
            Phase2Worlds.StripToV6(doc);
            var eventsBefore = ((JArray)doc["world"]["historicalEvents"]).Count;
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var w = loaded.World;
            Assert.That(w.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(w.MinuteOfDay, Is.EqualTo(WorldTime.DefaultStartMinute), "A justified default: 08:00.");
            Assert.That(w.Shops, Is.Not.Empty);
            Assert.That(w.Transactions, Is.Empty, "No invented trades.");
            Assert.That(w.HistoricalEvents.Count, Is.EqualTo(eventsBefore), "No invented history.");
            Assert.That(w.Player.IsOutfitted, Is.True);
            Phase2Worlds.ExpectLegacyThenTreatAsFresh(w);
            Assert.That(World(serializer.Serialize(w)), Is.EqualTo(World(serializer.Serialize(Phase2Worlds.Generated(4242)))),
                "Exactly the shops, stock, stores and kit of a fresh world of that seed.");

            // Once only: a v7 save is never re-initialised.
            w.Player.Purse = 3;
            w.Shops[0].Stock.Clear();
            var again = serializer.Deserialize(serializer.Serialize(w)).World;
            Assert.That(again.Player.Purse, Is.EqualTo(3));
            Assert.That(again.Shops[0].Stock, Is.Empty);
        }

        [Test]
        public void Validator_CatchesBrokenBelongingsAndShops()
        {
            void Broken(Action<WorldData> breakIt, string expect)
            {
                var w = W;
                breakIt(w);
                var v = WorldValidator.Validate(w);
                Assert.That(v.IsValid, Is.False, "Should have failed: " + expect);
                Assert.That(string.Join("; ", v.Errors), Does.Contain(expect));
            }

            Assert.That(WorldValidator.Validate(W).IsValid, Is.True);
            Broken(w => w.Player.Purse = -1, "purse is negative");
            Broken(w => w.Player.Inventory.Add(w.Player.Equipped(EquipmentSlot.Chest).Clone()), "more than one owner");
            Broken(w => w.Player.Equipment[EquipmentSlot.Head] = w.Player.Inventory.First(i => i.ItemId == "dagger_iron").Clone(), "cannot be worn");
            Broken(w => w.Player.Inventory.First().Quantity = 0, "holds 0");
            Broken(w => w.Player.Inventory.First(i => i.ItemId == "dagger_iron").Quantity = 2, "in one stack");
            Broken(w => w.Shops[0].ShopkeeperNpcId = new NpcId(Guid.NewGuid()), "no existing shopkeeper");
            Broken(w => w.Shops[1].ShopkeeperNpcId = w.Shops[0].ShopkeeperNpcId, "already keeps another shop");
            Broken(w => w.Shops[0].Stock.Add(new ShopStockData { ItemId = "unicorn", Quantity = 1 }), "unknown item");
            Broken(w => w.Shops[0].OpensAt = w.Shops[0].ClosesAt, "invalid hours");
            Broken(w => w.MinuteOfDay = WorldTime.MinutesPerDay, "Minute of day");
            Broken(w => w.Transactions.Add(new TransactionData
            {
                TransactionId = new TransactionId(Guid.NewGuid()), Date = w.CurrentSimulationDate, ItemId = "bread_loaf", Quantity = 2, UnitPrice = 3, Total = 5,
                ShopId = w.Shops[0].ShopId, ShopkeeperNpcId = w.Shops[0].ShopkeeperNpcId,
            }), "is not 2 × 3");
        }

        [Test]
        public void Restock_TakesMaterialsFromTheSettlement_AndStopsInUnrest()
        {
            var w = W;
            var shop = w.Shops.First(s => s.Categories.Contains(ShopCategory.WeaponsAndArmour));
            var town = w.FindSettlement(shop.SettlementId);
            shop.Stock.Clear();
            var iron = town.ResourceInventory.Get(ResourceTypes.Iron);
            var treasury = town.ResourceInventory.Get(ResourceTypes.Coin);
            var keeper = w.FindNpc(shop.ShopkeeperNpcId);
            keeper.Condition.Wealth = 10000;
            var added = CommerceMath.Restock(w, shop, new CommerceRules());
            Assert.That(added, Is.GreaterThan(0));
            Assert.That(town.ResourceInventory.Get(ResourceTypes.Iron), Is.LessThan(iron), "Iron is used, not conjured.");
            Assert.That(town.ResourceInventory.Get(ResourceTypes.Coin) - treasury, Is.EqualTo(10000 - keeper.Condition.Wealth),
                "What the keeper pays, the treasury receives.");

            town.Stability = 5;
            shop.Stock.Clear();
            Assert.That(CommerceMath.Restock(w, shop, new CommerceRules()), Is.Zero, "No restocking in unrest.");
            Assert.That(CommerceMath.Availability(w, shop).IsOpen, Is.False);
        }

        // ----- helpers -------------------------------------------------------------------------------

        private static void Give(WorldData w, string item)
        {
            w.Player.Inventory.Add(new ItemStackData { StackId = new ItemStackId(Guid.NewGuid()), ItemId = item, Quantity = 1 });
        }

        /// <summary>Loads a world edited in the test into the session through a real save slot (so it is validated).</summary>
        private WorldSession Reload(WorldData w)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "edited" + JsonWorldStore.Extension), new JsonWorldSerializer().Serialize(w));
            var r = _s.Execute(new LoadWorldCommand("edited"));
            Assert.That(r.IsSuccess, Is.True, r.ToString());
            return _s;
        }
    }
}
