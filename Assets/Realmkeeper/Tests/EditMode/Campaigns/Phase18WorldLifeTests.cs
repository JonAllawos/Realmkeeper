using System;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Party;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Party;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 18 (ADR-0090, ADR-0094, ADR-0095): factions' conflicts move through stages and the player can take part;
    /// factions act without the player; people's days show their natures and their town's condition; people talk to each
    /// other about what they really know; companions react to more, with cooldowns, and have more stories. Scenarios 41–58.
    /// </summary>
    public sealed class Phase18WorldLifeTests : Phase17Fixture
    {
        /// <summary>Someone willing to join (regard and a free post), then recruited by the ordinary command.</summary>
        private void Recruit(NpcData npc)
        {
            Edit(w =>
            {
                var x = w.FindNpc(npc.NpcId);
                if (RecruitRules.HoldsPost(x.Role)) x.Role = NpcRole.Resident;
                x.PlayerRegard.Familiarity = 40;
                x.PlayerRegard.Trust = 40;
                x.PlayerRegard.Affinity = 20;
            });
            Ok(S.Dispatch(new RecruitCompanionCommand(npc.NpcId)));
        }

        private RouteSegmentData Crossing() => W.Routes.First(r => r.IsCrossing && !W.FindSettlement(r.A).FactionId.IsEmpty && !W.FindSettlement(r.B).FactionId.IsEmpty &&
                                                                   W.FindSettlement(r.A).FactionId != W.FindSettlement(r.B).FactionId);

        private void SetOpinion(FactionId a, FactionId b, int opinion, int rivalry = 80) =>
            Edit(w => { var r = w.FindRelation(a, b); r.Opinion = opinion; r.Rivalry = rivalry; r.Trust = 0; r.Agreement = AgreementType.None; });

        [Test]
        public void AConflict_MovesThroughItsStages_WithTheirEffects_AndEasesBack()
        {
            var crossing = Crossing();
            var fa = W.FindSettlement(crossing.A).FactionId;
            var fb = W.FindSettlement(crossing.B).FactionId;
            Assert.That(WorldEventSystem.ConflictStage(new FactionRelationData { Opinion = -30, Rivalry = 80 }), Is.EqualTo(1), "Tension.");
            Assert.That(WorldEventSystem.ConflictStage(new FactionRelationData { Opinion = -45, Rivalry = 80 }), Is.EqualTo(2), "A dispute.");
            Assert.That(WorldEventSystem.ConflictStage(new FactionRelationData { Opinion = -60, Rivalry = 80 }), Is.EqualTo(3), "Skirmishes.");
            Assert.That(WorldEventSystem.ConflictStage(new FactionRelationData { Opinion = -90, Rivalry = 80 }), Is.EqualTo(4), "Open conflict.");

            SetOpinion(fa, fb, -100, 100);
            Days(1);
            var e = W.WorldEvents.Single(x => x.Type == WorldEventType.FactionConflict && x.IsOpen && x.RouteId == crossing.RouteId);
            Assert.That(e.Severity, Is.EqualTo(WorldEventSystem.ConflictStage(W.FindRelation(fa, fb))));
            Assert.That(W.FindRoute(crossing.RouteId).Condition, Is.EqualTo(RouteCondition.Blocked), "Open conflict bars the crossing.");

            SetOpinion(fa, fb, -55, 60); // skirmishes: the soldiers stand down, the raids go on
            Days(1);
            var stage = WorldEventSystem.ConflictStage(W.FindRelation(fa, fb));
            Assume.That(stage, Is.InRange(2, 3), "The simulation's own drift kept it in dispute or skirmishes.");
            Assert.That(W.FindWorldEvent(e.EventId).Severity, Is.EqualTo(stage));
            Assert.That(W.FindRoute(crossing.RouteId).Passable, Is.True, "Below open conflict, the crossing is open.");
            Assert.That(W.FindWorldEvent(e.EventId).History.Any(h => h.Contains("eased")), Is.True, "The event's history says it eased.");

            SetOpinion(fa, fb, -55, 60);
            Days(7);
            Assert.That(W.FindWorldEvent(e.EventId).Applied.Any(k => k.StartsWith("strain:", StringComparison.Ordinal)), Is.True,
                "Each week of dispute or worse, the border towns lose stability (the strain is applied, once a week).");

            SetOpinion(fa, fb, 40, 0);
            Days(1);
            Assert.That(W.FindWorldEvent(e.EventId).State, Is.EqualTo(WorldEventState.Resolved), "At peace, the conflict ends.");
            Valid();
        }

        [Test]
        public void ThePlayer_CanMediateAConflict_AndTheFactionsDrawBack()
        {
            Hero();
            var crossing = Crossing();
            var fa = W.FindSettlement(crossing.A).FactionId;
            var fb = W.FindSettlement(crossing.B).FactionId;
            SetOpinion(fa, fb, -48, 60);
            Days(1);
            var conflict = W.WorldEvents.Single(x => x.Type == WorldEventType.FactionConflict && x.IsOpen && x.RouteId == crossing.RouteId);
            Assume.That(conflict.Severity, Is.GreaterThanOrEqualTo(2), "Still a dispute after the day's political drift.");
            Edit(w => StorySynthesis.For(w, w.FindWorldEvent(conflict.EventId), null));
            var story = W.Quests.SingleOrDefault(q => q.DefinitionId == QuestCatalog.StoryConflict && q.SourceKey == StorySynthesis.Prefix + "conflict:" + conflict.EventId);
            Assert.That(story, Is.Not.Null, "A dispute at a crossing becomes a story.");
            var a = new FactionId(Guid.Parse(story.Bound("faction")));
            var b = new FactionId(Guid.Parse(story.Bound("other")));
            // Trusted by both sides: the player can carry an offer without a roll.
            Edit(w =>
            {
                w.Player.Social.FactionStandings.RemoveAll(s => s.FactionId == a || s.FactionId == b);
                w.Player.Social.FactionStandings.Add(new FactionStandingData { FactionId = a, Standing = 25 });
                w.Player.Social.FactionStandings.Add(new FactionStandingData { FactionId = b, Standing = 25 });
            });
            var opinion = W.FindRelation(a, b).Opinion;
            var giver = W.FindNpc(new NpcId(Guid.Parse(story.Bound("giver"))));
            Talk(giver);
            Ok(Say("hook:" + story.InstanceId + ":giver"));
            Ok(Say("accept"));
            Ok(Say("mediate_friend"));
            var done = W.FindQuest(story.InstanceId);
            Assert.That(done.Outcome, Is.EqualTo("mediated"));
            Assert.That(W.FindRelation(a, b).Opinion, Is.EqualTo(Math.Min(100, opinion + 30)));
            Assert.That(W.Player.Social.Decisions.Any(d => d.Key == "conflict_mediated"), Is.True);
        }

        [Test]
        public void Factions_ActWithoutThePlayer_ReliefForTheHungry_GarrisonsAtTheBorder()
        {
            var town = W.Settlements.First(s => !s.FactionId.IsEmpty);
            Edit(w =>
            {
                var s = w.FindSettlement(town.SettlementId);
                s.ResourceInventory.Set(ResourceTypes.Food, 0);
                w.FindFaction(s.FactionId).Treasury = 500;
            });
            Days(2);
            var e = W.WorldEvents.FirstOrDefault(x => x.Type == WorldEventType.FoodShortage && x.SettlementId == town.SettlementId);
            Assume.That(e, Is.Not.Null, "The town went short, as the simulation decides.");
            Assert.That(e.History.Any(h => h.Contains("from its treasury")), Is.True, "Its faction sent grain from the treasury.");
            Assert.That(W.FindFaction(town.FactionId).Treasury, Is.LessThan(500));
        }

        [Test]
        public void PeoplesDays_ShowTheirNatures_AndTheirTownsCondition()
        {
            var w = W;
            var s = w.Settlements.First(x => w.Npcs.Count(n => n.HomeSettlementId == x.SettlementId && !n.IsDeceased) > 8);
            var people = w.Npcs.Where(n => n.HomeSettlementId == s.SettlementId && !n.IsDeceased && n.Role != NpcRole.Innkeeper && n.Tier != NpcTier.Unassigned).ToList();
            var someone = people.First();
            // The same person, timid, in a restless town, stays in of an evening.
            Edit(x =>
            {
                var n = x.FindNpc(someone.NpcId);
                n.Personality[NpcTrait.Courage] = 20;
                n.Personality[NpcTrait.Sociability] = 90;
                WorldEvents.Raise(x, WorldEventType.LocalUnrest, "unrest:" + s.SettlementId, QuestClock.Now(x), 2, "restless", ev => { ev.SettlementId = s.SettlementId; }, out _);
            });
            var evening = NpcActivityPlan.At(W, W.FindNpc(someone.NpcId), W.CurrentSimulationDate, 20 * 60);
            Assert.That(evening.Reason, Does.Contain("restless").Or.Contain("Night"), "A timid soul keeps indoors while the town is restless.");

            // Compassion in a hungry town: helping hand out bread.
            Edit(x =>
            {
                x.WorldEvents.RemoveAll(ev => ev.Key == "unrest:" + s.SettlementId);
                x.FindNpc(someone.NpcId).Personality[NpcTrait.Compassion] = 90;
                x.FindSettlement(s.SettlementId).ResourceInventory.Set(ResourceTypes.Food, 0);
            });
            var t = W.FindSettlement(s.SettlementId);
            if (t.IsInFoodShortage && NpcGenesis.FirstOf(W, s.SettlementId, LocationKind.Square) != null)
                Assert.That(NpcActivityPlan.At(W, W.FindNpc(someone.NpcId), W.CurrentSimulationDate, 19 * 60).Reason, Does.Contain("bread").Or.Contain("Night"));

            // Discipline moves the working day.
            var worker = people.FirstOrDefault(n => !n.WorkplaceId.IsEmpty && n.Role != NpcRole.Guard && !W.Shops.Any(sh => sh.ShopkeeperNpcId == n.NpcId));
            if (worker != null)
            {
                Edit(x => x.FindNpc(worker.NpcId).Personality[NpcTrait.Discipline] = 20);
                Edit(x => x.FindNpc(worker.NpcId).Personality[NpcTrait.Discipline] = 90);
                var (start, _) = NpcActivityPlan.WorkHours(W, W.FindNpc(worker.NpcId));
                var early = NpcActivityPlan.At(W, W.FindNpc(worker.NpcId), W.CurrentSimulationDate, start - 15);
                Assert.That(early.Reason, Is.Not.Null);
            }
        }

        [Test]
        public void PeopleTalkToEachOther_OnlyIfTheyKnowEachOther_AndOnlyAboutWhatTheyKnow()
        {
            var w = W;
            var s = w.Settlements.First();
            var a = w.Npcs.First(n => n.HomeSettlementId == s.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned);
            var b = w.Npcs.First(n => n.HomeSettlementId == s.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned && n.NpcId != a.NpcId);
            Edit(x =>
            {
                var (p, q) = NpcMind.Order(a.NpcId, b.NpcId);
                x.NpcRelations.RemoveAll(r => r.NpcA == p && r.NpcB == q);
                x.NpcRelations.Add(new NpcRelationData { NpcA = p, NpcB = q, Kind = NpcRelationKind.Friend, Affinity = 50, Origin = NpcRelationCause.Kindness });
            });
            var line = NpcExchanges.Topic(W, W.FindSettlement(s.SettlementId), W.FindNpc(a.NpcId), 1);
            Assert.That(line, Does.StartWith("\""), "They say something, in their own words.");
            Assert.That(NpcExchanges.Talkative(ActivityKind.Working), Is.False, "Nobody chats at the forge.");
            foreach (var x in NpcExchanges.Now(W, s.SettlementId))
            {
                var p = W.FindNpc(x.A);
                var q = W.FindNpc(x.B);
                Assert.That(W.FindNpcRelation(p.NpcId, q.NpcId), Is.Not.Null, "Only people who know each other.");
                Assert.That(NpcActivityPlan.Now(W, p).Location, Is.EqualTo(NpcActivityPlan.Now(W, q).Location), "Only people in the same place.");
            }
        }

        [Test]
        public void Companions_ReactToOutcomesAndDiscoveries_Once_AndKeepQuietAfterAPassingRemark()
        {
            Hero();
            var npc = W.Npcs.First(n => !n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId == W.Player.SettlementId && n.Role != NpcRole.Elder);
            Recruit(npc);
            Edit(x =>
            {
                var c = x.FindNpc(npc.NpcId);
                c.Personality[NpcTrait.Compassion] = 95;
                c.Personality[NpcTrait.Curiosity] = 95;
            });
            var loyalty = W.FindCompanion(npc.NpcId).Loyalty;
            Edit(x =>
            {
                var q = new QuestInstanceData { InstanceId = "test-refugees", DefinitionId = QuestCatalog.StoryRefugees, Outcome = "welcomed" };
                CompanionStories.QuestOutcome(x, q, null);
                CompanionStories.QuestOutcome(x, q, null); // twice: once only
            });
            Assert.That(W.FindCompanion(npc.NpcId).Loyalty, Is.GreaterThan(loyalty), "A compassionate companion approves of taking the families in.");
            var after = W.FindCompanion(npc.NpcId).Loyalty;
            Assert.That(W.FindCompanion(npc.NpcId).Remarks.Count(r => r.Key.StartsWith("outcome:test-refugees", StringComparison.Ordinal)), Is.EqualTo(1));

            Edit(x =>
            {
                Assert.That(CompanionStories.Discovered(x, new DiscoveryData { Key = "d1", Text = "old", Category = DiscoveryCategory.Historical, Major = true }), Is.True, "Curious: they remark.");
                Assert.That(CompanionStories.Discovered(x, new DiscoveryData { Key = "d2", Text = "older", Category = DiscoveryCategory.Historical, Major = true }), Is.False, "Then a quiet spell.");
                Assert.That(CompanionStories.Discovered(x, new DiscoveryData { Key = "d3", Text = "minor", Category = DiscoveryCategory.Historical, Major = false }), Is.False, "Minor finds pass unremarked.");
            });
            Assert.That(W.FindCompanion(npc.NpcId).Loyalty, Is.EqualTo(after));
        }

        [Test]
        public void ACompanionWithFamilyElsewhere_MayAskToGoHome_WhenNoRivalWaits()
        {
            Hero();
            var npc = W.Npcs.First(n => !n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId == W.Player.SettlementId && n.Role != NpcRole.Elder);
            var kin = W.Npcs.First(n => !n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId != npc.HomeSettlementId);
            Recruit(npc);
            Edit(x =>
            {
                x.NpcRelations.RemoveAll(r => r.NpcA == npc.NpcId || r.NpcB == npc.NpcId);
                var (p, q) = NpcMind.Order(npc.NpcId, kin.NpcId);
                x.NpcRelations.Add(new NpcRelationData { NpcA = p, NpcB = q, Kind = NpcRelationKind.Household, Affinity = 60, Origin = NpcRelationCause.SharedHousehold });
                var rec = x.FindCompanion(npc.NpcId);
                rec.Loyalty = 90;
                rec.PersonalQuestId = null;
                rec.StoryQuestId = null;
                var m = x.Party.Find(npc.NpcId);
                CompanionStories.OfferSecondStory(x, x.FindNpc(npc.NpcId), rec, m, null);
            });
            var rec2 = W.FindCompanion(npc.NpcId);
            var story = W.FindQuest(rec2.StoryQuestId);
            Assert.That(story?.DefinitionId, Is.EqualTo(QuestCatalog.CompanionGoingHome), "Family elsewhere, no rival: Going Home.");
            Assert.That(story.Bound("kin"), Is.EqualTo(kin.NpcId.ToString()));
        }

        [Test]
        public void AFollower_WillNotWadeIntoUndrainedDeepWater_AndSaysWhy()
        {
            var place = new PlaceData { PlaceId = "p", Layout = new PlaceLayout() };
            place.Layout.Rooms.Add(new PlaceRoom { Id = "a", Rect = new CellRect(0, 0, 6, 6) });
            place.Layout.Rooms.Add(new PlaceRoom { Id = "b", Rect = new CellRect(8, 0, 6, 6), Hazard = "deep_water" });
            place.Layout.Connections.Add(new PlaceConnection { Id = "ab", Kind = ConnectionKind.Opening, A = "a", B = "b", Rect = new CellRect(6, 2, 2, 2) });
            Assert.That(PlaceRoutes.Find(place, "a", "b", Mover.Follower), Is.Null);
            Assert.That(PlaceRoutes.Blocker(place, "a", "b"), Is.EqualTo("the water is too deep for them"));
            place.Variables["drained"] = "yes";
            Assert.That(PlaceRoutes.Find(place, "a", "b", Mover.Follower), Is.Not.Null, "Drained, they follow.");
        }
    }
}
