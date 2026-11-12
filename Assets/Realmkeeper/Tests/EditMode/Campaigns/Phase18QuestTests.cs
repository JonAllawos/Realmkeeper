using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 18 (ADR-0091): the four quest chains, played through their conversations — each decision, each outcome, the
    /// lasting change to the world, and save and load in the middle. Scenarios 15–26 of the Phase 18 acceptance list.
    /// </summary>
    public sealed class Phase18QuestTests : Phase17Fixture
    {
        private QuestInstanceData First(string def) => W.Quests.Where(q => q.DefinitionId == def).OrderBy(q => q.InstanceId, StringComparer.Ordinal).First();

        private NpcData Person(QuestInstanceData q, string role) => W.FindNpc(new NpcId(Guid.Parse(q.Bound(role))));

        private SettlementData Town(QuestInstanceData q) => W.FindSettlement(new SettlementId(Guid.Parse(q.Bound("settlement"))));

        /// <summary>The chain's second quest, as completing the first would make it (the same people, the same place).</summary>
        private QuestInstanceData FollowUp(QuestInstanceData first, string def)
        {
            Edit(w => QuestLifecycle.Create(w, QuestCatalog.Find(def), new Dictionary<string, string>(first.Bindings), "after:" + first.InstanceId, null, null, out _));
            return W.Quests.Single(q => q.DefinitionId == def && q.SourceKey == "after:" + first.InstanceId);
        }

        private void Give(string item, int n = 1) =>
            Edit(w => w.Player.Inventory.Add(ItemStacks.Create(new ItemStackId(Guid.NewGuid()), ItemCatalog.Find(item), n, "test")));

        private void Open(QuestInstanceData q, string role)
        {
            Talk(Person(q, role));
            Ok(Say("hook:" + q.InstanceId + ":" + role));
        }

        private QuestInstanceData Now(QuestInstanceData q) => W.FindQuest(q.InstanceId);

        [Test]
        public void ASchema19Save_Loads_AndGainsItsPhase18PlacesAndChains_Once_KeepingEverythingElse()
        {
            var json = new Realmkeeper.Infrastructure.Persistence.JsonWorldSerializer().Serialize(W);
            var doc = Newtonsoft.Json.Linq.JObject.Parse(json);
            Realmkeeper.Tests.EditMode.Phase2.Phase2Worlds.StripToV19(doc);
            var old = new Realmkeeper.Infrastructure.Persistence.JsonWorldSerializer().Deserialize(doc.ToString());
            Assert.That(old.IsSuccess, Is.True, string.Join("; ", old.Errors ?? new string[0]));
            Assert.That(old.World.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(old.World.Places.Any(p => Realmkeeper.Domain.Places.Phase18Places.All.Contains(p.DefinitionId)), Is.False, "A v19 world had none.");
            var otherPlaces = old.World.Places.Count;
            Assert.That(new Realmkeeper.Infrastructure.Persistence.JsonWorldStore(Dir).Save(old.World, "v19").IsSuccess, Is.True);
            Load("v19");
            var places = W.Places.Where(p => Realmkeeper.Domain.Places.Phase18Places.All.Contains(p.DefinitionId)).ToList();
            Assert.That(places.Count, Is.GreaterThanOrEqualTo(W.Regions.Count * 3), "Each region gains its own Phase 18 places on load.");
            Assert.That(W.Places.Count - places.Count, Is.EqualTo(otherPlaces), "Every older place is kept.");
            Assert.That(W.Quests.Any(q => q.DefinitionId == QuestCatalog.DrownedDrift), Is.True, "And their chains.");
            Save("again");
            Load("again");
            Assert.That(W.Places.Count(p => Realmkeeper.Domain.Places.Phase18Places.All.Contains(p.DefinitionId)), Is.EqualTo(places.Count), "Once: loading again adds nothing.");
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        [Test]
        public void EveryChain_IsBoundSomewhere_InANewWorld_AndItsCatalogueIsValid()
        {
            foreach (var id in new[] { QuestCatalog.RecordHouse, QuestCatalog.DrownedDrift, QuestCatalog.CairnQuestion, QuestCatalog.MillersMeasure })
                Assert.That(W.Quests.Any(q => q.DefinitionId == id), Is.True, id + " is bound in a six-region world.");
            foreach (var id in new[] { QuestCatalog.RecordHouse, QuestCatalog.ReevesAccounts, QuestCatalog.CairnQuestion, QuestCatalog.CairnKeeping, QuestCatalog.DrownedDrift,
                         QuestCatalog.DriftFuture, QuestCatalog.MillersMeasure, QuestCatalog.MillForTwo })
            {
                var def = QuestCatalog.Find(id);
                Assert.That(def, Is.Not.Null, id);
                Assert.That(DialogueCatalog.Find(def.DialogueId), Is.Not.Null, id + " has its conversation.");
            }

            Assert.That(QuestCatalog.Find(QuestCatalog.RecordHouse).FollowUps, Does.Contain(QuestCatalog.ReevesAccounts));
            Assert.That(QuestCatalog.Find(QuestCatalog.CairnQuestion).FollowUps, Does.Contain(QuestCatalog.CairnKeeping));
            Assert.That(QuestCatalog.Find(QuestCatalog.DrownedDrift).FollowUps, Does.Contain(QuestCatalog.DriftFuture));
            Assert.That(QuestCatalog.Find(QuestCatalog.MillersMeasure).FollowUps, Does.Contain(QuestCatalog.MillForTwo));
            // Every decision quest has two or more ways to end.
            foreach (var id in new[] { QuestCatalog.ReevesAccounts, QuestCatalog.CairnKeeping, QuestCatalog.DriftFuture, QuestCatalog.MillersMeasure })
                Assert.That(QuestCatalog.Find(id).Objectives.Where(o => o.Outcome != null).Select(o => o.Outcome).Distinct().Count(), Is.GreaterThanOrEqualTo(2), id);
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        // Chain 3: the drift's future — sealing it is always open; working it needs the gallery drained.
        [Test]
        public void GreywatersFuture_SealedOrReopened_ChangesTheTown_AndReopeningNeedsTheGalleryDrained()
        {
            var q = FollowUp(First(QuestCatalog.DrownedDrift), QuestCatalog.DriftFuture);
            var town = Town(q);
            var stability = town.Stability;
            Open(q, "giver");
            Ok(Say("accept"));
            var view = S.GetConversation();
            var wet = view.Responses.Single(r => r.Id == "reopen_wet");
            Assert.That(wet.Enabled, Is.False, "Nobody goes down while it is flooded.");
            Assert.That(wet.Why, Does.Contain("pump"));
            Assert.That(view.Responses.Any(r => r.Id == "reopen"), Is.False);
            Edit(_ => { }); // saved and loaded mid-conversation
            Ok(Say("seal"));
            var done = Now(q);
            Assert.That(done.State, Is.EqualTo(QuestState.Completed));
            Assert.That(done.Outcome, Is.EqualTo("sealed"));
            Assert.That(W.FindSettlement(town.SettlementId).Stability, Is.EqualTo(Math.Min(100, stability + 4)), "A grave respected: the town steadier.");
        }

        [Test]
        public void GreywatersFuture_Reopened_WithTheGalleryDrained_PaysAndProspers()
        {
            var q = FollowUp(First(QuestCatalog.DrownedDrift), QuestCatalog.DriftFuture);
            var town = Town(q);
            var prosperity = town.Prosperity;
            Edit(w => w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "drift_drained", Text = "drained", Category = DiscoveryCategory.Geographic }));
            var purse = W.Player.Purse;
            Open(q, "giver");
            Ok(Say("accept"));
            Ok(Say("reopen"));
            var done = Now(q);
            Assert.That(done.Outcome, Is.EqualTo("reopened"));
            Assert.That(W.FindSettlement(town.SettlementId).Prosperity, Is.EqualTo(Math.Min(100, prosperity + 6)));
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 60), "A share of the first silver.");
            Assert.That(W.Player.Inventory.Any(s => s.ItemId == "pump_wrench"), Is.True, "The pump-wright's wrench: a tool for the mechanism.");
        }

        // Chain 2: who keeps the cairn — the old charter mediates; backing a side sets the factions against each other.
        [Test]
        public void WhoKeepsTheCairn_TheCharterMediates_AndTheFactionsDrawCloser()
        {
            var q = FollowUp(First(QuestCatalog.CairnQuestion), QuestCatalog.CairnKeeping);
            var a = new FactionId(Guid.Parse(q.Bound("faction")));
            var b = new FactionId(Guid.Parse(q.Bound("holder")));
            var opinion = W.FindRelation(a, b).Opinion;
            Give("old_charter");
            Open(q, "giver");
            Ok(Say("accept"));
            Ok(Say("share"));
            Assert.That(Now(q).Outcome, Is.EqualTo("shared"));
            Assert.That(W.FindRelation(a, b).Opinion, Is.EqualTo(Math.Min(100, opinion + 25)), "Held to the old charter, the two sides draw closer.");
            Assert.That(W.Player.Inventory.Any(s => s.ItemId == "old_charter"), Is.False, "The charter changed hands.");
            Assert.That(W.Player.Social.Decisions.Any(d => d.Key == "cairn_shared"), Is.True, "The decision is in the journal.");
        }

        [Test]
        public void WhoKeepsTheCairn_BackingTheClaim_TurnsTheFactionsAgainstEachOther()
        {
            var q = FollowUp(First(QuestCatalog.CairnQuestion), QuestCatalog.CairnKeeping);
            var a = new FactionId(Guid.Parse(q.Bound("faction")));
            var b = new FactionId(Guid.Parse(q.Bound("holder")));
            var opinion = W.FindRelation(a, b).Opinion;
            var standing = W.Player.Social.StandingWith(b);
            Open(q, "giver");
            Ok(Say("accept"));
            var view = S.GetConversation();
            Assert.That(view.Responses.Any(r => r.Id == "uphold"), Is.False, "Without the watch's log, there is nothing to uphold the holder with.");
            Ok(Say("claim"));
            Assert.That(Now(q).Outcome, Is.EqualTo("claimed"));
            Assert.That(W.FindRelation(a, b).Opinion, Is.EqualTo(Math.Max(-100, opinion - 20)));
            Assert.That(W.Player.Social.StandingWith(b), Is.LessThan(standing), "The holder will remember who took the other side.");
        }

        // Chain 1: the reeve's accounts — exposed before the town, or bought off.
        [Test]
        public void TheReevesAccounts_BoughtOff_PaysTheHero_AndStainsTheirName()
        {
            var q = FollowUp(First(QuestCatalog.RecordHouse), QuestCatalog.ReevesAccounts);
            Give("true_ledger");
            Open(q, "giver");
            Ok(Say("accept"));
            var purse = W.Player.Purse;
            var notoriety = W.Player.Social.Notoriety;
            Open(q, "suspect");
            Ok(Say("confront"));
            Ok(Say("sell"));
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 60));
            Assert.That(W.Player.Inventory.Any(s => s.ItemId == "true_ledger"), Is.False, "The ledger went back to the reeve.");
            Edit(_ => { }); // saved and loaded before the report
            Open(Now(q), "giver");
            Ok(Say("report_bought"));
            var done = Now(q);
            Assert.That(done.State, Is.EqualTo(QuestState.Completed));
            Assert.That(done.Outcome, Is.EqualTo("bought"));
            Assert.That(W.Player.Social.Notoriety, Is.GreaterThan(notoriety), "Silence that was paid for is known, in the end.");
        }

        [Test]
        public void TheReevesAccounts_Exposed_ShakesTheTown_ButMakesItRicher()
        {
            var q = FollowUp(First(QuestCatalog.RecordHouse), QuestCatalog.ReevesAccounts);
            var town = Town(q);
            var (stability, prosperity) = (town.Stability, town.Prosperity);
            Give("true_ledger");
            Open(q, "giver");
            Ok(Say("accept"));
            Open(q, "suspect");
            Ok(Say("confront"));
            Open(Now(q), "giver");
            Ok(Say("expose"));
            Ok(Say("report_exposed"));
            var done = Now(q);
            Assert.That(done.Outcome, Is.EqualTo("exposed"));
            var after = W.FindSettlement(town.SettlementId);
            Assert.That(after.Stability, Is.EqualTo(Math.Max(0, stability - 2)));
            Assert.That(after.Prosperity, Is.EqualTo(Math.Min(100, prosperity + 3)));
            Assert.That(W.FindNpc(Person(q, "suspect").NpcId).PlayerRegard.Hostility, Is.GreaterThanOrEqualTo(40), "The reeve will not forgive it.");
        }

        // Chain 4: the miller's measure → a mill for two.
        [Test]
        public void TheMillersMeasure_SidingWithTheMiller_ThenANewMill_FeedsTheTown()
        {
            var q = First(QuestCatalog.MillersMeasure);
            var town = Town(q);
            Open(q, "giver");
            Ok(Say("accept"));
            Open(Now(q), "miller");
            Ok(Say("heard"));
            Edit(w =>
            {
                var p = w.FindQuest(q.InstanceId).Progress("book");
                p.Done = true; // the market ledger was read (Short Measures tests the ledger itself)
            });
            Open(Now(q), "giver");
            Ok(Say("right"));
            var done = Now(q);
            Assert.That(done.State, Is.EqualTo(QuestState.Completed));
            Assert.That(done.Outcome, Is.EqualTo("miller_right"));
            var next = W.Quests.Single(x => x.DefinitionId == QuestCatalog.MillForTwo && x.SourceKey == "after:" + q.InstanceId);
            var prosperity = W.FindSettlement(town.SettlementId).Prosperity;
            Open(next, "giver");
            Ok(Say("accept"));
            Give("timber_plank", 4);
            Open(Now(next), "miller");
            Ok(Say("give"));
            Assert.That(Now(next).State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.FindSettlement(town.SettlementId).Prosperity, Is.EqualTo(Math.Min(100, prosperity + 4)), "New millstones: the town grinds more.");
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }
    }
}
