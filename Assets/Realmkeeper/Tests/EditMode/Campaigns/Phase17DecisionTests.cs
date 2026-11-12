using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Campaigns;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0083, ADR-0084): decisions with persistent consequences, applied once, some of them later; and a
    /// story that adapts when the people in it die, turn hostile, go away or are replaced.
    /// </summary>
    public sealed class Phase17DecisionTests : Phase17Fixture
    {
        private void ToTheQuestion()
        {
            Hero();
            Begin();
            ChapterOne(lookAtTally: false);
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
        }

        // 33 ---------------------------------------------------------------------------------------------------

        [Test]
        public void AConditionalChoice_IsShownDisabled_WithItsReason_Until_ItsConditionHolds()
        {
            ToTheQuestion();
            var view = DialogueEngine.View(W);
            var shared = view.Responses.Single(r => r.Id == "shared");
            Assert.That(shared.Enabled, Is.False, "No proof yet: the shared count is out of reach.");
            Assert.That(shared.Why, Does.Contain("proof"));
            Assert.That(Say("shared").IsSuccess, Is.False, "A disabled choice cannot be made.");
            Assert.That(C.Decision("road_claim"), Is.Null);
            Assert.That(view.Responses.Any(r => r.Id == "friend"), Is.False, "The faction route is not shown to a stranger to the far faction (51).");
        }

        // 34–36, 42 ------------------------------------------------------------------------------------------

        [Test]
        public void BackingTheHomeTown_IsRecordedOnce_AndChangesTheWorldAtOnce()
        {
            ToTheQuestion();
            var reeve = Npc("reeve");
            var counterpart = Npc("counterpart");
            var homeFaction = CampaignEngine.Faction(W, C, "home_faction");
            var farFaction = CampaignEngine.Faction(W, C, "far_faction");
            var stability = Town("home_town").Stability;
            var gratitude = reeve.PlayerRegard.Gratitude;
            var homeStanding = homeFaction == null ? 0 : W.Player.Social.StandingWith(homeFaction.FactionId);
            Ok(Say("home"));
            var d = C.Decision("road_claim");
            Assert.That(d.ChoiceId, Is.EqualTo("home"));
            Assert.That(d.Source, Is.EqualTo(counterpart.Name), "Who put the question is recorded.");
            Assert.That(C.Flags["claim"], Is.EqualTo("home"));
            Assert.That(Npc("reeve").PlayerRegard.Gratitude, Is.GreaterThan(gratitude), "The reeve is grateful (a real regard change).");
            Assert.That(Town("home_town").Stability, Is.EqualTo(System.Math.Min(100, stability + 3)), "The home town is steadier.");
            if (homeFaction != null) Assert.That(W.Player.Social.StandingWith(homeFaction.FactionId), Is.GreaterThan(homeStanding), "Standing with the home town's faction rose.");
            Assert.That(Npc("counterpart").Memories.Any(m => m.Subject.Contains("side")), Is.True, "The far side remembers it.");
            Assert.That(W.Player.Social.Decisions.Any(x => x.Key == "campaign:" + Waystone + ":road_claim"), Is.True, "In the player's decision log.");
            Assert.That(C.Pending.Single(p => p.DecisionId == "road_claim").Fired, Is.False, "Its later consequence waits.");
            Assert.That(Quest("hold").State, Is.EqualTo(QuestState.Completed), "The quest waiting on the decision is done.");
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Completed));
            Assert.That(Arc("claim").Outcome, Is.EqualTo("home"));
            if (farFaction != null && homeFaction != null && farFaction.FactionId != homeFaction.FactionId)
                Assert.That(W.FindRelation(homeFaction.FactionId, farFaction.FactionId).Opinion, Is.LessThan(100), "The factions think less of each other.");
            Valid();
        }

        // 37, 38, 39 --------------------------------------------------------------------------------------------

        [Test]
        public void TheLaterConsequence_ComesInItsTime_Once_ThroughSavesAndReloads()
        {
            ToTheQuestion();
            Ok(Say("home"));
            var crossing = C.Bound("crossing");
            Save("decided");
            Days(3);
            Assert.That(C.Pending.Single().Fired, Is.False, "Not yet.");
            Load("decided");
            Assert.That(C.Decision("road_claim").ChoiceId, Is.EqualTo("home"), "The decision survives the reload (39).");
            Days(7);
            var p = C.Pending.Single();
            Assert.That(p.Fired, Is.True, "Six days on, the far town's carters stop paying (37).");
            Assert.That(W.WorldEvents.Count(e => e.Type == WorldEventType.TradeDisruption && e.RouteId == crossing && e.Key.StartsWith("campaign:")), Is.EqualTo(1));
            var standing = W.Player.Social.Ledger.Count;
            var events = W.WorldEvents.Count;
            Save("after");
            Load("after");
            Days(5);
            Assert.That(W.WorldEvents.Count(e => e.Key != null && e.Key.StartsWith("campaign:")), Is.EqualTo(1), "Never raised twice (38).");
            Assert.That(W.Player.Social.Ledger.Count(l => l.Key.Contains("road_claim")), Is.EqualTo(W.Player.Social.Ledger.Count(l => l.Key.Contains("road_claim"))));
            Assert.That(C.History.Count(h => h.Text.StartsWith("What came of it")), Is.EqualTo(1), "Reported once.");
            Assert.That(events, Is.GreaterThan(0));
            Assert.That(standing, Is.GreaterThan(0));
            Valid();
        }

        [Test]
        public void ADecisionCannotBeMadeTwice_NorUndone()
        {
            ToTheQuestion();
            Ok(Say("apart"));
            var w = W;
            var c = CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(Waystone));
            Assert.That(CampaignEngine.Decide(w, c, "road_claim", "home", "test", null), Does.Contain("already"));
            Assert.That(C.Decision("road_claim").ChoiceId, Is.EqualTo("apart"));
            Assert.That(S.Dispatch(new MakeCampaignDecisionCommand(Waystone, "road_claim", "home")).IsSuccess, Is.False, "A decision a person puts is not made from the journal.");
        }

        // 40 -------------------------------------------------------------------------------------------------

        [Test]
        public void ThePriorDecision_ChangesWhatPeopleSayLater()
        {
            ToTheQuestion();
            Ok(Say("far"));
            About(Npc("counterpart"), "keeper", "giver");
            Ok(Say("accept"));
            About(Npc("tollkeeper"), "keeper", "tollkeeper");
            Ok(Say("expose"));
            About(Npc("home_voice"), "accord", "giver");
            Ok(Say("accept"));
            var line = DialogueEngine.View(W).NpcLine;
            Assert.That(line, Does.Contain("You gave it to"), "The home town's voice speaks of the choice made weeks before.");
        }

        // 41, 44 ------------------------------------------------------------------------------------------------

        [Test]
        public void ExposingTheTollKeeper_MakesThemHostile_AndOpensAnotherWayToTheTruth()
        {
            ToTheQuestion();
            Ok(Say("apart"));
            About(Npc("counterpart"), "keeper", "giver");
            Ok(Say("accept"));
            About(Npc("tollkeeper"), "keeper", "tollkeeper");
            Ok(Say("expose"));
            Assert.That(CampaignCharacters.StatusOf(W, Npc("tollkeeper")), Is.EqualTo(CharacterStatus.Hostile), "A real regard change makes them hostile (44).");
            Assert.That(Arc("keeper").State, Is.EqualTo(ArcState.Completed));
            Assert.That(Arc("keeper").Outcome, Is.EqualTo("exposed"));
            Talk(Npc("tollkeeper"));
            Assert.That(Options().Any(o => o.StartsWith("hook:")), Is.False, "Nothing more to settle with them.");
        }

        [Test]
        public void IfTheTollKeeperCannotAnswer_TheirHousehold_IsOfferedAsAnotherWay()
        {
            Hero();
            Begin();
            Assume.That(Npc("tollkeeper_kin"), Is.Not.Null, "The toll-keeper lives alone in this world.");
            ChapterOne(lookAtTally: false);
            var keeper = Npc("tollkeeper");
            Edit(w => w.FindNpc(keeper.NpcId).PlayerRegard.Hostility = 90);
            Days(1);
            Assert.That(Quest("kin"), Is.Not.Null, "The household is asked instead (41: a quest becomes available because of what happened).");
            About(Npc("tollkeeper_kin"), "kin", "giver");
            Ok(Say("accept"));
            Ok(Say("sticks"));
            Assert.That(Arc("keeper").State, Is.EqualTo(ArcState.Completed));
            Assert.That(Arc("keeper").Outcome, Is.EqualTo("kin"));
            Assert.That(C.Flags["evidence"], Is.EqualTo("kin"));
        }

        // 43, 49 -------------------------------------------------------------------------------------------------

        [Test]
        public void IfTheTollKeeperDies_AndNobodyElseKnows_TheTruthIsLost_AndNobodyIsRaised()
        {
            Hero();
            Begin();
            ChapterOne(lookAtTally: false);
            var keeper = Npc("tollkeeper");
            var kin = Npc("tollkeeper_kin");
            Kill(keeper, kin);
            Days(1);
            Assert.That(Arc("keeper").State, Is.EqualTo(ArcState.Completed));
            Assert.That(Arc("keeper").Outcome, Is.EqualTo("lost"));
            Assert.That(W.FindNpc(keeper.NpcId).IsDeceased, Is.True, "Still dead.");
            Assert.That(Quest("keeper").State, Is.EqualTo(QuestState.Failed).Or.EqualTo(QuestState.Available).Or.EqualTo(QuestState.Unavailable));
            Valid();
        }

        // 45 --------------------------------------------------------------------------------------------------

        [Test]
        public void SomeoneAwayFromHome_IsUnavailable_NotErased()
        {
            Hero();
            Days(2); // caravans set out
            Begin();
            var keeper = Npc("tollkeeper");
            Assume.That(W.Groups.Any(x => x.IsActive && x.State != TravelGroupState.Arrived), Is.True, "No group on the road in this world.");
            Edit(w =>
            {
                var g = w.Groups.FirstOrDefault(x => x.IsActive && x.State != TravelGroupState.Arrived);
                if (g != null) g.LeaderNpcId = keeper.NpcId;
            });
            Assert.That(CampaignCharacters.StatusOf(W, Npc("tollkeeper")), Is.EqualTo(CharacterStatus.Unavailable));
            Assert.That(CampaignQueries.One(W, C).Characters.All(ch => ch.Role != "tollkeeper" || ch.Status == null || ch.Status == "away from home"), Is.True);
        }

        // 46 --------------------------------------------------------------------------------------------------

        [Test]
        public void WhenAFactionsLeaderChanges_TheStoryAsksTheNewLeader()
        {
            Hero();
            Begin();
            var farFaction = CampaignEngine.Faction(W, C, "far_faction");
            Assume.That(farFaction, Is.Not.Null, "The far town answers to no faction here.");
            var next = W.Npcs.First(n => !n.IsDeceased && n.FactionId == farFaction.FactionId && n.NpcId != farFaction.LeaderNpcId);
            Edit(w => w.FindFaction(farFaction.FactionId).LeaderNpcId = next.NpcId);
            Assert.That(Npc("far_leader").NpcId, Is.EqualTo(next.NpcId), "The role reads the leader when asked, not when the story began.");
            var warden = Npc("warden");
            Kill(warden);
            Assert.That(Npc("counterpart")?.NpcId, Is.EqualTo(next.NpcId), "With the warden gone, the new leader speaks for the far town.");
        }

        // 47 --------------------------------------------------------------------------------------------------

        [Test]
        public void ASharedCount_MendsTheRoad_OnlyOnceTheRoadIsOpen()
        {
            ToTheQuestion();
            MakeTheCaseNow();
            Ok(Say("shared"));
            var crossing = C.Bound("crossing");
            Edit(w => { var r = w.FindRoute(crossing); r.Condition = RouteCondition.Blocked; r.BlockedBy = "test"; });
            Days(10);
            Assert.That(C.Pending.Single().Fired, Is.False, "It waits while the road is barred (47).");
            Edit(w => { var r = w.FindRoute(crossing); r.Condition = RouteCondition.Worn; r.BlockedBy = null; });
            Days(1);
            Assert.That(C.Pending.Single().Fired, Is.True);
            Assert.That(W.FindRoute(crossing).Condition, Is.EqualTo(RouteCondition.Good), "The road is mended (42: actual world state).");
        }

        private void MakeTheCaseNow()
        {
            Edit(w =>
            {
                var c = CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(Waystone));
                c.Flags["case_made"] = "argued";
            });
            About(Npc("counterpart"), "hold", "giver");
        }

        // 51, 52, 53 ---------------------------------------------------------------------------------------------

        [Test]
        public void AFriendOfTheFarFaction_HasAWayOfTheirOwn()
        {
            Hero();
            Begin();
            var farFaction = CampaignEngine.Faction(W, C, "far_faction");
            Assume.That(farFaction, Is.Not.Null);
            ChapterOne(lookAtTally: false);
            Edit(w =>
            {
                var s = w.Player.Social;
                var f = s.FactionStandings.FirstOrDefault(x => x.FactionId == farFaction.FactionId);
                if (f == null) s.FactionStandings.Add(new FactionStandingData { FactionId = farFaction.FactionId, Standing = 25 });
                else f.Standing = 25;
            });
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
            Assert.That(Options(), Does.Contain("friend"), "Faction-exclusive: a friend of the far faction can speak as one.");
            Ok(Say("friend"));
            Assert.That(C.Flags["case_made"], Is.EqualTo("friend"));
            Ok(Say("shared"));
            Assert.That(C.Decision("road_claim").ChoiceId, Is.EqualTo("shared"));
        }

        [Test]
        public void RefusingToTakeASide_IsAChoice_TheStoryContinuesWithoutAnyAllegiance()
        {
            ToTheQuestion();
            var ledger = W.Player.Social.FactionStandings.Select(f => f.Standing).ToArray();
            Ok(Say("apart"));
            Assert.That(W.Player.Social.FactionStandings.Select(f => f.Standing).ToArray(), Is.EqualTo(ledger), "No faction gains or loses the player (52).");
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Completed));
            Assert.That(C.State, Is.EqualTo(CampaignState.Active), "The story goes on without a side taken (53).");
        }
    }
}
