using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 18 (ADR-0093): the world's stories from more causes, their lifecycle (made once, lapse if ignored, end the
    /// event they are about), and goods taken on the road coming back when the band is broken. Scenarios 30–40.
    /// </summary>
    public sealed class Phase18StoryTests : Phase17Fixture
    {
        private QuestInstanceData Synthesise()
        {
            Edit(w =>
            {
                // A clean slate of stories, so the cap does not hide the one under test.
                foreach (var q in w.Quests.Where(x => x.SourceKey != null && x.SourceKey.StartsWith(StorySynthesis.Prefix, StringComparison.Ordinal)).ToList()) w.Quests.Remove(q);
                StorySynthesis.Run(w, null);
            });
            return W.Quests.Where(x => x.SourceKey != null && x.SourceKey.StartsWith(StorySynthesis.Prefix, StringComparison.Ordinal)).OrderBy(x => x.CreatedAt).LastOrDefault();
        }

        private NpcData Bound(QuestInstanceData q, string role) => W.FindNpc(new NpcId(Guid.Parse(q.Bound(role))));

        private void Open(QuestInstanceData q, string role)
        {
            Talk(Bound(q, role));
            Ok(Say("hook:" + q.InstanceId + ":" + role));
        }

        private SettlementData Town(int i) => W.Settlements.OrderBy(s => s.SettlementId).ElementAt(i);

        [Test]
        public void ARecentlyLostCaravan_StillMakesAStory_ThoughItsEventIsAlreadyResolved()
        {
            Hero();
            var route = W.Routes.First(r => r.Passable);
            var origin = W.FindSettlement(route.A);
            Edit(w =>
            {
                var now = QuestClock.Now(w);
                var g = new TravelingGroupData { GroupId = "test-caravan", Kind = TravelGroupKind.Caravan, Name = "Test caravan", OriginId = route.A, DestinationId = route.B, State = TravelGroupState.Defeated };
                w.Groups.Add(g);
                var e = WorldEvents.Raise(w, WorldEventType.CaravanLost, "caravan-lost:test", now, 3, "outlaws took the test caravan", x => { x.RouteId = route.RouteId; x.GroupId = g.GroupId; x.SettlementId = route.B; }, out _);
                WorldEvents.Move(e, WorldEventState.Resolved, now, "the survivors reached home"); // as GroupSystem.Lose does, at once
                foreach (var n in w.Npcs.Where(n => n.HomeSettlementId == route.B && !n.IsDeceased).Take(1))
                    Realmkeeper.Simulation.Npcs.NpcMind.Learn(n, KnowledgeTopic.RecentEvent, e.EventId, "they heard", KnowledgeStatus.Known, 80, w.CurrentSimulationDate, default, 0, default,
                        () => new KnowledgeId(Guid.NewGuid()));
            });
            var story = Synthesise();
            Assert.That(story?.DefinitionId, Is.EqualTo(QuestCatalog.StoryLostCaravan), "Phase 17's lost caravan story, which a resolved event used to hide.");
            Assert.That(Synthesise()?.SourceKey, Is.Not.EqualTo(story.SourceKey).Or.Null, "Made once.");
        }

        [Test]
        public void GoodsTakenOnTheRoad_ComeBack_WhenTheBandIsBroken_Once()
        {
            var route = W.Routes.First(r => r.Passable);
            var dest = W.FindSettlement(route.B);
            var before = dest.ResourceInventory.Get(ResourceTypes.Food);
            Edit(w =>
            {
                var d = w.FindSettlement(route.B);
                d.ResourceInventory.Set(ResourceTypes.Food, Math.Max(0, d.ResourceInventory.Get(ResourceTypes.Food) - 100)); // room for it
                var now = QuestClock.Now(w);
                var e = WorldEvents.Raise(w, WorldEventType.CaravanLost, "caravan-lost:goods", now, 3, "outlaws took a grain caravan", x => { x.RouteId = route.RouteId; x.SettlementId = route.B; }, out _);
                e.Applied.Add("cargo:" + ResourceTypes.Food + ":60:" + route.B);
                e.Applied.Add("band:pop-test");
            });
            var stocked = W.FindSettlement(route.B).ResourceInventory.Get(ResourceTypes.Food);
            var band = new TravelingGroupData { GroupId = "band", Kind = TravelGroupKind.Bandits, PopulationId = "pop-test", State = TravelGroupState.Defeated };
            var w2 = W;
            var got = WorldEventSystem.Recover(w2, band, w2.CurrentSimulationDate.ToDayNumber(), QuestClock.Now(w2));
            Assert.That(got, Is.EqualTo(60 * WorldEventSystem.RecoveredPercent / 100), "Half the goods are found in the camp.");
            Assert.That(w2.FindSettlement(route.B).ResourceInventory.Get(ResourceTypes.Food), Is.EqualTo(stocked + got), "Real stock, carried on to the town it was bound for.");
            Assert.That(WorldEventSystem.Recover(w2, band, 0, 0), Is.EqualTo(0), "Once.");
            Assert.That(w2.WorldEvents.Single(e => e.Key == "caravan-lost:goods").History.Any(h => h.Contains("recovered")), Is.True, "The event's history says so.");
        }

        [Test]
        public void RefugeesArriving_AskTheTownWhatToDo_AndTheAnswer_EndsTheEvent()
        {
            Hero();
            var from = Town(0);
            var to = W.Settlements.First(s => s.RegionId == from.RegionId && s.SettlementId != from.SettlementId);
            Edit(w =>
            {
                var now = QuestClock.Now(w);
                var e = WorldEvents.Raise(w, WorldEventType.RefugeeMovement, "refugees:test", now, 2, "families left " + from.Name, x => { x.SettlementId = from.SettlementId; x.RegionId = from.RegionId; }, out _);
                var g = new TravelingGroupData { GroupId = "refugees:test", Kind = TravelGroupKind.Refugees, Name = "Families", OriginId = from.SettlementId, DestinationId = to.SettlementId, State = TravelGroupState.Travelling, EventId = e.EventId };
                w.Groups.Add(g);
                e.GroupId = g.GroupId;
            });
            var q = Synthesise();
            Assert.That(q.DefinitionId, Is.EqualTo(QuestCatalog.StoryRefugees));
            Assert.That(q.Bound("settlement"), Is.EqualTo(to.SettlementId.ToString()), "Asked where they arrive.");
            var prosperity = W.FindSettlement(to.SettlementId).Prosperity;
            Open(q, "giver");
            Ok(Say("accept"));
            Ok(Say("welcome"));
            Assert.That(W.FindQuest(q.InstanceId).Outcome, Is.EqualTo("welcomed"));
            Assert.That(W.FindSettlement(to.SettlementId).Prosperity, Is.EqualTo(Math.Min(100, prosperity + 3)), "Hands are hands.");
            Assert.That(W.WorldEvents.Single(e => e.Key == "refugees:test").State, Is.EqualTo(WorldEventState.Resolved), "The story ends the event it was about.");
        }

        [Test]
        public void UnrestInATown_FindsItsLoudestVoice_AndBreakingItUp_HasACost()
        {
            Hero();
            var s = Town(0);
            Edit(w =>
            {
                var t = w.FindSettlement(s.SettlementId);
                t.Stability = 20;
                WorldEvents.Raise(w, WorldEventType.LocalUnrest, "unrest:" + s.SettlementId, QuestClock.Now(w), 2, "stability fell", x => { x.SettlementId = s.SettlementId; x.RegionId = s.RegionId; }, out _);
            });
            var q = Synthesise();
            Assert.That(q.DefinitionId, Is.EqualTo(QuestCatalog.StoryUnrest));
            var agitator = Bound(q, "agitator");
            Assert.That(W.Npcs.Where(n => n.HomeSettlementId == s.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned && n.NpcId != Bound(q, "giver").NpcId &&
                (n.Role == NpcRole.Labourer || n.Role == NpcRole.Farmer || n.Role == NpcRole.Resident || n.Role == NpcRole.Smith)).Min(n => n.Condition.Morale),
                Is.EqualTo(agitator.Condition.Morale), "The unhappiest of them speaks for the lane.");
            Open(q, "giver");
            Ok(Say("accept"));
            Open(W.FindQuest(q.InstanceId), "agitator");
            Ok(Say("hear"));
            Ok(S.Dispatch(new EndConversationCommand()));
            var notoriety = W.Player.Social.Notoriety;
            Open(W.FindQuest(q.InstanceId), "giver");
            Ok(Say("crush"));
            Assert.That(W.FindQuest(q.InstanceId).Outcome, Is.EqualTo("crushed"));
            Assert.That(W.FindNpc(agitator.NpcId).PlayerRegard.Hostility, Is.GreaterThanOrEqualTo(35));
            Assert.That(W.Player.Social.Notoriety, Is.GreaterThan(notoriety));
            Assert.That(W.WorldEvents.Single(e => e.Key == "unrest:" + s.SettlementId).State, Is.EqualTo(WorldEventState.Resolved));
        }

        [Test]
        public void TradeCutOff_AMerchantPaysForTheLongWayRound_AndBothTownsGain()
        {
            Hero();
            var route = W.Routes.First(r => r.Passable && W.Npcs.Any(n => n.HomeSettlementId == r.A && n.Role == NpcRole.Merchant) && W.Npcs.Any(n => n.HomeSettlementId == r.B && n.Role == NpcRole.Merchant));
            Edit(w => WorldEvents.Raise(w, WorldEventType.TradeDisruption, "trade:test", QuestClock.Now(w), 2, "trade stopped", x => { x.RouteId = route.RouteId; }, out _));
            var q = Synthesise();
            Assert.That(q.DefinitionId, Is.EqualTo(QuestCatalog.StoryLongWay));
            var (a, b) = (W.FindSettlement(route.A).Prosperity, W.FindSettlement(route.B).Prosperity);
            var purse = W.Player.Purse;
            Open(q, "giver");
            Ok(Say("accept"));
            Assert.That(W.Player.Inventory.Where(s => s.ItemId == "merchants_cloth").Sum(s => s.Quantity), Is.EqualTo(2), "The goods to carry.");
            Open(W.FindQuest(q.InstanceId), "buyer");
            Ok(Say("give"));
            var done = W.FindQuest(q.InstanceId);
            Assert.That(done.State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 40));
            Assert.That(W.FindSettlement(route.A).Prosperity, Is.EqualTo(Math.Min(100, a + 2)));
            Assert.That(W.FindSettlement(route.B).Prosperity, Is.EqualTo(Math.Min(100, b + 2)));
        }

        [Test]
        public void ADeath_FarFromAFriend_IsCarriedAsWord_ByTheirNeighbours()
        {
            Hero();
            var home = Town(0);
            var away = W.Settlements.First(s => s.SettlementId != home.SettlementId);
            var dead = W.Npcs.First(n => n.HomeSettlementId == home.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned && !W.Party.Contains(n.NpcId) &&
                                         W.Quests.All(q => !q.Bindings.Values.Contains(n.NpcId.ToString())));
            var friend = W.Npcs.First(n => n.HomeSettlementId == away.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned && !W.Party.Contains(n.NpcId));
            Edit(w =>
            {
                var (x, y) = Realmkeeper.Simulation.Npcs.NpcMind.Order(dead.NpcId, friend.NpcId);
                w.NpcRelations.RemoveAll(r => r.NpcA == x && r.NpcB == y);
                w.NpcRelations.Add(new NpcRelationData { NpcA = x, NpcB = y, Kind = NpcRelationKind.Friend, Affinity = 60, Trust = 50, Origin = NpcRelationCause.Kindness });
            });
            Kill(dead);
            var q = Synthesise();
            Assert.That(q?.DefinitionId, Is.EqualTo(QuestCatalog.StoryWordHome));
            Assert.That(q.Bound("friend"), Is.EqualTo(friend.NpcId.ToString()));
            Assert.That(q.Variables["deceased_name"], Is.EqualTo(dead.Name));
            Open(q, "giver");
            Ok(Say("accept"));
            Open(W.FindQuest(q.InstanceId), "friend");
            Ok(Say("tell"));
            Assert.That(W.FindQuest(q.InstanceId).State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.FindNpc(friend.NpcId).PlayerRegard.Gratitude, Is.GreaterThanOrEqualTo(15));
        }

        // The lifecycle audit: every kind of world event has follow-up gameplay, or a stated reason why it does not.
        [Test]
        public void EveryWorldEventType_HasFollowUpGameplay_AndStoriesLapseIfIgnored()
        {
            var followUp = new Dictionary<WorldEventType, string>
            {
                [WorldEventType.FoodShortage] = "story: Grain Down the Road (Phase 17)",
                [WorldEventType.TradeDisruption] = "story: The Long Way Round (Phase 18)",
                [WorldEventType.CaravanLost] = "story: What Became of the Caravan; goods recovered when the band is broken (Phase 18)",
                [WorldEventType.BanditActivity] = "a paid patrol (Phase 16); road fights; breaking the band returns stolen goods (Phase 18)",
                [WorldEventType.PatrolResponse] = "the patrol itself is the follow-up to bandits",
                [WorldEventType.BridgeDamaged] = "quest: Timber for the Bridge (Phase 16)",
                [WorldEventType.RoadBlocked] = "quest: Timber for the Bridge (Phase 16); campaign lines (Phase 17)",
                [WorldEventType.FactionConflict] = "faction conflict stages and the player's part in them (Phase 18, ADR-0090)",
                [WorldEventType.RefugeeMovement] = "story: Room at the Hearth (Phase 18)",
                [WorldEventType.LocalUnrest] = "story: Hard Words in the town (Phase 18)",
            };
            foreach (WorldEventType t in Enum.GetValues(typeof(WorldEventType)))
                Assert.That(followUp.ContainsKey(t), Is.True, t + " has no follow-up gameplay.");
            foreach (var id in new[] { QuestCatalog.StoryRefugees, QuestCatalog.StoryUnrest, QuestCatalog.StoryLongWay, QuestCatalog.StoryWordHome })
                Assert.That(QuestCatalog.Find(id).ExpiresAfterDays, Is.GreaterThan(0), id + " lapses if never taken up.");
        }
    }
}
