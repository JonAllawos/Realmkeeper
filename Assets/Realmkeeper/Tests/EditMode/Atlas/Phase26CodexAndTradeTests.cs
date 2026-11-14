using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Combat;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 26 below the screens. It covers:
    /// <list type="bullet">
    /// <item>the codex (ADR-0117): only what the player knows, from existing records; facts widen as knowledge widens;
    /// search, category and sort; no duplicates; the same after a save and load;</item>
    /// <item>trade: the comparison for an item the player does not own, read-only and the same as for one they own;
    /// buying and selling several at once through the existing commands, exactly.</item>
    /// </list>
    /// </summary>
    public sealed class Phase26CodexAndTradeTests : Phase16Fixture
    {
        private SettlementData Start => In(Home).OrderBy(s => s.SettlementId).First(s => s.SettlementType == SettlementType.Town);

        private void Hero()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            Edit(w => { w.MinuteOfDay = 10 * 60; w.Player.Purse = 500; });
            Ok(S.Dispatch(new EnterSettlementCommand(Start.SettlementId)));
        }

        // ----- Codex -------------------------------------------------------------------------------------------

        [Test]
        public void Codex_ListsOnlyWhatThePlayerKnows_AndWidensAsTheyLearnMore()
        {
            Hero();
            var codex = S.GetCodex();
            var w = W;
            var strangers = w.Npcs.Where(n => KnownPersonQueries.AcquaintanceOf(w, n) == Acquaintance.Seen && w.Companions.All(c => c.NpcId != n.NpcId)).ToList();
            Assert.That(strangers, Is.Not.Empty);
            foreach (var n in strangers.Take(50)) Assert.That(codex.Entries.Any(e => e.Id == "person:" + n.NpcId), Is.False, "A stranger is not in the codex: " + n.Name);
            Assert.That(codex.Entries.Single(e => e.Id == "settlement:" + Start.SettlementId).Status, Is.EqualTo("Visited"));
            foreach (var st in w.Settlements.Where(s => !w.Player.Social.VisitedSettlements.Contains(s.SettlementId) && !w.Player.Social.KnownPlaces.Contains(s.SettlementId.ToString())))
                Assert.That(codex.Entries.Any(e => e.Id == "settlement:" + st.SettlementId), Is.False, "Never heard of: " + st.Name);
            var gov = w.FindFaction(Start.FactionId);
            if (gov != null) Assert.That(codex.Entries.Any(e => e.Id == "faction:" + gov.FactionId && e.Links.Contains("Governs " + Start.Name)), Is.True, "The faction governing where you have been.");
            foreach (var pl in w.Places.Where(p => p.Discovery == PlaceDiscovery.Unknown)) Assert.That(codex.Entries.Any(e => e.Id == "place:" + pl.PlaceId), Is.False);

            // Meeting someone adds them, with what meeting tells; nothing about the others changes.
            var person = strangers.First(n => n.HomeSettlementId == Start.SettlementId && !n.IsDeceased);
            Edit(x => x.FindNpc(person.NpcId).MetPlayerOn = x.CurrentSimulationDate);
            var after = S.GetCodex();
            var entry = after.Entries.Single(e => e.Id == "person:" + person.NpcId);
            Assert.That(entry.Name, Is.EqualTo(person.Name));
            Assert.That(entry.Lines, Is.Not.Empty);
            Assert.That(entry.Withheld, Is.Not.Null, "What knowing them better would add is said, not shown.");
            Assert.That(after.Entries.Count(e => e.Category == CodexCategory.People), Is.EqualTo(codex.Entries.Count(e => e.Category == CodexCategory.People) + 1));

            // A rumoured place: named, without its description; found: with it.
            var place = w.Places.FirstOrDefault(p => p.Discovery == PlaceDiscovery.Unknown && !string.IsNullOrEmpty(p.Description));
            Assume.That(place, Is.Not.Null);
            Edit(x => x.FindPlace(place.PlaceId).Discovery = PlaceDiscovery.Rumored);
            var rumoured = S.GetCodex().Entries.Single(e => e.Id == "place:" + place.PlaceId);
            Assert.That(rumoured.Status, Is.EqualTo("Rumoured"));
            Assert.That(rumoured.Text, Is.Null, "A rumour does not describe the place.");
            Edit(x => x.FindPlace(place.PlaceId).Discovery = PlaceDiscovery.Discovered);
            Assert.That(S.GetCodex().Entries.Single(e => e.Id == "place:" + place.PlaceId).Text, Is.EqualTo(place.Description));
        }

        [Test]
        public void Codex_SearchesFiltersAndSorts_WithoutDuplicates_AndIsTheSameAfterSaveAndLoad()
        {
            Hero();
            var people = W.Npcs.Where(n => n.HomeSettlementId == Start.SettlementId && !n.IsDeceased).Take(3).Select(n => n.NpcId).ToList();
            Edit(x => { foreach (var id in people) x.FindNpc(id).MetPlayerOn = x.CurrentSimulationDate; });
            var all = S.GetCodex();
            Assert.That(all.Entries.Select(e => e.Id).Distinct().Count(), Is.EqualTo(all.Entries.Count), "No duplicates.");
            Assert.That(all.Total, Is.EqualTo(all.Entries.Count));

            var onlyPeople = S.GetCodex(category: CodexCategory.People);
            Assert.That(onlyPeople.Entries.All(e => e.Category == CodexCategory.People), Is.True);
            Assert.That(onlyPeople.Entries.Count, Is.EqualTo(all.Counts[CodexCategory.People]));

            var name = W.FindNpc(people[0]).Name;
            var found = S.GetCodex(name.Split(' ')[0].ToUpperInvariant());
            Assert.That(found.Entries.Any(e => e.Name == name), Is.True, "Search ignores case and finds names.");
            Assert.That(S.GetCodex("zzzz-nothing-like-this").Entries, Is.Empty);

            var byName = S.GetCodex(category: CodexCategory.People).Entries.Select(e => e.Name).ToList();
            Assert.That(byName, Is.Ordered.Using((IComparer)StringComparer.Ordinal));

            Assert.That(S.Execute(new SaveWorldCommand("p26codex")).IsSuccess, Is.True);
            Assert.That(S.Execute(new LoadWorldCommand("p26codex")).IsSuccess, Is.True);
            Assert.That(S.GetCodex().Entries.Select(e => e.Id), Is.EqualTo(all.Entries.Select(e => e.Id)), "The codex is rebuilt from the saved records, the same.");
        }

        // ----- Trade -------------------------------------------------------------------------------------------

        private ShopId OpenShopWithStock(Func<PriceLine, bool> which, out PriceLine line)
        {
            foreach (var shop in W.Shops.Where(s => s.SettlementId == Start.SettlementId).OrderBy(s => s.ShopId))
            {
                var d = S.GetShopDetail(shop.ShopId);
                if (d == null || !d.Summary.IsOpen) continue;
                line = d.ForSale.FirstOrDefault(which);
                if (line != null) return shop.ShopId;
            }

            line = null;
            Assume.That(false, "No open shop with such stock here.");
            return default;
        }

        [Test]
        public void ComparingAShopsItem_ChangesNothing_AndMatchesTheComparisonOnceOwned()
        {
            Hero();
            var shop = OpenShopWithStock(l => ItemCatalog.Find(l.ItemId)?.IsEquippable == true && l.UnitPrice <= 400, out var line);
            var before = WorldJson();
            var cmp = S.CompareCatalogItem(line.ItemId);
            Assert.That(cmp, Is.Not.Null);
            Assert.That(WorldJson(), Is.EqualTo(before), "A comparison is read-only.");
            Ok(S.Dispatch(new BuyItemCommand(shop, line.ItemId, 1, line.UnitPrice)));
            var stack = W.Player.Inventory.First(i => i.ItemId == line.ItemId);
            var owned = S.CompareItem(stack.StackId);
            Assert.That(owned.Slot, Is.EqualTo(cmp.Slot));
            Assert.That(owned.Lines.Select(l => (l.Label, l.Before, l.After, l.Better)), Is.EqualTo(cmp.Lines.Select(l => (l.Label, l.Before, l.After, l.Better))),
                "The same rules path: what the shop showed is what wearing it gives.");
            Assert.That(S.CompareCatalogItem("no_such_item"), Is.Null);
        }

        [Test]
        public void BuyingAndSellingSeveral_MovesExactlyThatMany_AndTheCoin()
        {
            Hero();
            var shop = OpenShopWithStock(l => l.Quantity >= 2 && l.Item?.Stackable == true && l.UnitPrice > 0 && l.UnitPrice * 2 <= 500, out var line); // stackable, so two bought are one stack to sell
            var purse = W.Player.Purse;
            var held = W.Player.Inventory.Where(i => i.ItemId == line.ItemId).Sum(i => i.Quantity);
            Ok(S.Dispatch(new BuyItemCommand(shop, line.ItemId, 2, line.UnitPrice)));
            Assert.That(W.Player.Purse, Is.EqualTo(purse - 2 * line.UnitPrice));
            Assert.That(W.Player.Inventory.Where(i => i.ItemId == line.ItemId).Sum(i => i.Quantity), Is.EqualTo(held + 2));
            Assert.That(S.GetShopDetail(shop).ForSale.FirstOrDefault(l => l.ItemId == line.ItemId)?.Quantity ?? 0, Is.EqualTo(line.Quantity - 2));

            var tooMany = S.Dispatch(new BuyItemCommand(shop, line.ItemId, 999, line.UnitPrice));
            Assert.That(tooMany.IsSuccess, Is.False, "More than the stock (or the purse) is refused whole.");
            Assert.That(W.Player.Inventory.Where(i => i.ItemId == line.ItemId).Sum(i => i.Quantity), Is.EqualTo(held + 2), "Nothing moved.");

            // Selling: what was just bought, back to the same shop (or, if it will not buy that back, anything it takes two of).
            var offers = S.GetShopDetail(shop).Offers;
            var offer = offers.FirstOrDefault(o => o.Accepted && o.ItemId == line.ItemId && o.Quantity >= 2) ?? offers.FirstOrDefault(o => o.Accepted && o.Quantity >= 2);
            Assert.That(offer, Is.Not.Null, "The shop buys two of something the player carries: " + string.Join(", ", offers.Select(o => o.Name + " ×" + o.Quantity + (o.Accepted ? string.Empty : " (" + o.Refusal + ")"))));
            var purse2 = W.Player.Purse;
            var have = W.Player.Inventory.Where(i => i.ItemId == offer.ItemId).Sum(i => i.Quantity);
            Ok(S.Dispatch(new SellItemCommand(shop, offer.StackId, 2, offer.UnitPrice)));
            Assert.That(W.Player.Purse, Is.EqualTo(purse2 + 2 * offer.UnitPrice));
            Assert.That(W.Player.Inventory.Where(i => i.ItemId == offer.ItemId).Sum(i => i.Quantity), Is.EqualTo(have - 2));
            Valid();
        }
    }
}
