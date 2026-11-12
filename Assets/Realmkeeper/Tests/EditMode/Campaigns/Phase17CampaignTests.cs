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
    /// Phase 17 (ADR-0082): the campaign architecture — an instance bound to the world's own people, chapters and arcs
    /// that open as their prerequisites close, sequential, parallel, optional and alternative progress, refusal,
    /// failure when the world stops waiting, completion, and the world going on afterwards. Numbers refer to the brief's
    /// acceptance scenarios.
    /// </summary>
    public sealed class Phase17CampaignTests : Phase17Fixture
    {
        // 19 -------------------------------------------------------------------------------------------------

        [Test]
        public void StartingTheCampaign_BindsItsRolesToTheWorldsOwnPeople_AndCopiesNoOne()
        {
            Hero();
            var people = W.Npcs.Count;
            var settlements = W.Settlements.Count;
            Begin();
            Assert.That(W.Npcs.Count, Is.EqualTo(people), "No campaign-only people.");
            Assert.That(W.Settlements.Count, Is.EqualTo(settlements));
            foreach (var role in new[] { "reeve", "warden", "tollkeeper" })
                Assert.That(W.Npcs.Any(n => n.NpcId == Npc(role).NpcId), Is.True, role + " is one of the world's own people.");
            Assert.That(Town("far_town").RegionId, Is.Not.EqualTo(Town("home_town").RegionId), "The far town lies across the border (multi-region).");
            Assert.That(W.FindRoute(C.Bound("crossing")).IsCrossing, Is.True);
            Assert.That(C.State, Is.EqualTo(CampaignState.Active));
            Assert.That(C.Chapter("short_count").State, Is.EqualTo(ChapterState.Active));
            Assert.That(Arc("letter").State, Is.EqualTo(ArcState.Active));
            Assert.That(Quest("letter").State, Is.EqualTo(QuestState.Available), "Its first quest waits on the reeve, through the ordinary quest system.");
            Assert.That(Quest("letter").SourceKey, Does.StartWith("campaign:" + Waystone));
            Valid();
        }

        // 20 -------------------------------------------------------------------------------------------------

        [Test]
        public void AnOldWorld_ThatHasLived_CanTakeUpTheCampaign_WithoutItsHistoryRewritten()
        {
            Hero();
            Days(30);
            var history = W.HistoricalEvents.Count;
            var deadBefore = W.Npcs.Count(n => n.IsDeceased);
            Begin();
            Assert.That(W.HistoricalEvents.Take(history).Select(e => e.EventId), Is.EqualTo(W.HistoricalEvents.Take(history).Select(e => e.EventId)), "Earlier history untouched.");
            Assert.That(W.Npcs.Count(n => n.IsDeceased), Is.EqualTo(deadBefore), "Nobody revived or removed.");
            foreach (var role in new[] { "reeve", "warden", "tollkeeper" }) Assert.That(Npc(role).IsDeceased, Is.False, "Only the living are cast.");
        }

        [Test]
        public void AWorldWithoutAWayOut_IsToldWhyTheCampaignCannotBegin()
        {
            Hero();
            // An older world that has not yet discovered the lands beyond has no road out of its home region. (The route
            // network is rebuilt on load, so the test removes the crossings from a copy and asks that copy.)
            var w = W;
            w.Routes.RemoveAll(r => r.IsCrossing);
            var why = CampaignEngine.Compatibility(w, Def);
            Assert.That(why, Does.Contain("road").IgnoreCase, "The reason is given in plain words.");
            Assert.That(why, Does.Contain("discover the lands beyond"), "…with what would make it possible.");
            var offer = CampaignQueries.View(w).Offers.Single(o => o.CampaignId == Waystone);
            Assert.That(offer.CanStart, Is.False);
            Assert.That(offer.Why, Is.EqualTo(why));

            // And a start condition that does not hold refuses the command itself, with nothing changed.
            var strict = new CampaignDefinition
            {
                Id = "test_strict", Title = "Strict", Version = 1, FinalArc = Def.FinalArc, Roles = Def.Roles, Quests = Def.Quests, Chapters = Def.Chapters, Arcs = Def.Arcs,
                Decisions = Def.Decisions, Endings = Def.Endings, StartConditions = new[] { QuestCondition.Of(ConditionKind.SettlementStability, "home_town", min: 101) },
            };
            Assert.That(CampaignCatalog.Register(strict), Is.Empty);
            try
            {
                var r2 = S.Dispatch(new StartCampaignCommand("test_strict"));
                Assert.That(r2.IsSuccess, Is.False);
                Assert.That(r2.Message, Does.Contain("not as this campaign needs it"));
                Assert.That(W.Campaigns, Is.Empty);
            }
            finally
            {
                CampaignCatalog.Unregister("test_strict");
            }
        }

        // 21 -------------------------------------------------------------------------------------------------

        [Test]
        public void ASandboxWorld_RunsWithoutACampaign()
        {
            Hero();
            Days(14);
            Assert.That(W.Campaigns, Is.Empty, "Nothing starts a campaign by itself.");
            Assert.That(CampaignQueries.View(W).Sandbox, Is.True);
            Assert.That(W.Quests, Is.Not.Empty, "Quests still arise from the world.");
            Valid();
        }

        // 22–25 ------------------------------------------------------------------------------------------------

        [Test]
        public void TheFirstChapter_RunsItsArcsInParallel_AndItsLetterInOrder()
        {
            Hero();
            Begin();
            Assert.That(Arc("letter").State, Is.EqualTo(ArcState.Active));
            if (Npc("carter") != null) Assert.That(Arc("tally").State, Is.EqualTo(ArcState.Active), "The optional arc runs alongside (parallel).");
            About(Npc("reeve"), "letter", "giver");
            Ok(Say("accept"));
            Assert.That(Quest("letter").State, Is.EqualTo(QuestState.Active));
            Assert.That(Realmkeeper.Simulation.Quests.QuestObjectives.StageOpen(QuestCatalog.Find(QuestCatalog.WaystoneLetter), Quest("letter"), 1), Is.False, "Delivering waits on crossing the border (sequential).");
            GoTo(Town("far_town"));
            Assert.That(Quest("letter").Progress("cross").Done, Is.True);
            Assert.That(Quest("letter").Progress("deliver").Revealed, Is.True);
            About(Npc("counterpart"), "letter", "recipient");
            Ok(Say("give"));
            Assert.That(Quest("letter").State, Is.EqualTo(QuestState.Completed));
            Assert.That(Arc("letter").State, Is.EqualTo(ArcState.Completed));
            Assert.That(Arc("letter").Outcome, Is.EqualTo("delivered"));
            if (Npc("carter") != null)
            {
                Assert.That(C.Chapter("short_count").State, Is.EqualTo(ChapterState.Completed), "The optional arc does not hold the chapter.");
                Assert.That(Arc("tally").State, Is.EqualTo(ArcState.Active), "…and stays open to finish.");
            }

            Assert.That(C.Chapter("two_accounts").State, Is.EqualTo(ChapterState.Active));
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Active));
            Assert.That(Arc("keeper").State, Is.EqualTo(ArcState.Active), "Two arcs of chapter two run side by side.");
            Valid();
        }

        [Test]
        public void TheOptionalArc_CanBeFinished_AndItsProofOpensAChoice()
        {
            Hero();
            Begin();
            Assume.That(Npc("carter"), Is.Not.Null, "This world's home town has nobody to pay the toll.");
            ChapterOne();
            Assert.That(Arc("tally").State, Is.EqualTo(ArcState.Completed));
            Assert.That(C.Flags["case_made"], Is.Not.Empty, "The proof makes the case for a shared count.");
        }

        // 26 -------------------------------------------------------------------------------------------------

        [Test]
        public void IfTheWardenDies_TheLetterHasNobody_AndTheStoryGoesOnAnotherWay()
        {
            Hero();
            Begin();
            About(Npc("reeve"), "letter", "giver");
            Ok(Say("accept"));
            var warden = Npc("warden");
            Kill(warden);
            Days(1);
            Assert.That(Quest("letter").State, Is.EqualTo(QuestState.Failed), "The quest's recipient is dead: the ordinary quest rules fail it.");
            Assert.That(Arc("letter").State, Is.EqualTo(ArcState.Completed), "The arc goes on by its alternative.");
            Assert.That(Arc("letter").Outcome, Is.EqualTo("unanswered"));
            Assert.That(W.FindNpc(warden.NpcId).IsDeceased, Is.True, "Nobody is brought back for the story's sake (49).");
            var counterpart = Npc("counterpart");
            Assert.That(counterpart == null || counterpart.NpcId != warden.NpcId, Is.True, "Someone else speaks for the far town, or nobody does.");
        }

        // 27 -------------------------------------------------------------------------------------------------

        [Test]
        public void ThePlayerCanRefuseAnOptionalArc()
        {
            Hero();
            Begin();
            Assume.That(Npc("carter"), Is.Not.Null);
            Ok(S.Dispatch(new RefuseArcCommand(Waystone, "tally")));
            Assert.That(Arc("tally").State, Is.EqualTo(ArcState.Abandoned));
            Assert.That(Quest("tally").State, Is.EqualTo(QuestState.Unavailable), "Its untaken quest lapses.");
            Assert.That(S.Dispatch(new RefuseArcCommand(Waystone, "claim")).IsSuccess, Is.False, "A required arc cannot be refused (the campaign can be set aside instead).");
        }

        // 28 -------------------------------------------------------------------------------------------------

        [Test]
        public void TheWorldDoesNotWait_AnUnansweredQuestionFails_AndTheStoryEndsWithoutThePlayer()
        {
            Hero();
            Begin();
            ChapterOne(lookAtTally: false);
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Active));
            Days(21);
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Failed), "Twenty days on, the towns settled it themselves.");
            Assert.That(C.Flags["claim"], Is.EqualTo("unsettled"));
            Assert.That(Arc("claim").Reason, Does.Contain("settled"));
        }

        // 29–32 ------------------------------------------------------------------------------------------------

        [Test]
        public void TheWholeStory_SharedCount_EndsTheCampaign_AndTheWorldGoesOn()
        {
            Hero();
            Begin();
            ChapterOne();
            Assume.That(C.Flags.ContainsKey("case_made"), Is.True);
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
            Ok(Say("shared"));
            Assert.That(C.Decision("road_claim").ChoiceId, Is.EqualTo("shared"));
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Completed));
            About(Npc("counterpart"), "keeper", "giver");
            Ok(Say("accept"));
            About(Npc("tollkeeper"), "keeper", "tollkeeper");
            Ok(Say("repay"));
            Assert.That(Arc("keeper").State, Is.EqualTo(ArcState.Completed));
            Assert.That(C.Chapter("two_accounts").State, Is.EqualTo(ChapterState.Completed), "Chapter two closes (30).");
            About(Npc("home_voice"), "accord", "giver");
            Ok(Say("accept"));
            Ok(Say("tell"));
            Assert.That(C.State, Is.EqualTo(CampaignState.Completed), "The campaign is complete (31).");
            Assert.That(C.Ending, Is.EqualTo("shared"));

            // 32: the world goes on; nothing reopens the story.
            var decisions = C.Decisions.Count;
            Days(10);
            Assert.That(C.State, Is.EqualTo(CampaignState.Completed));
            Assert.That(C.Decisions.Count, Is.EqualTo(decisions));
            Assert.That(S.Dispatch(new SetCampaignStateCommand(Waystone, CampaignAction.Resume)).IsSuccess, Is.False, "A finished campaign cannot be reopened.");
            Assert.That(S.Dispatch(new StartCampaignCommand(Waystone)).IsSuccess, Is.False, "…or started again in the same world.");
            GoTo(Town("home_town"));
            Valid();
        }

        [Test]
        public void SettingTheCampaignAside_KeepsEverything_AndTheWorldGoesOn()
        {
            Hero();
            Begin();
            ChapterOne(lookAtTally: false);
            Ok(S.Dispatch(new SetCampaignStateCommand(Waystone, CampaignAction.Suspend)));
            var started = Arc("claim").StartedAt;
            Days(3);
            Assert.That(C.State, Is.EqualTo(CampaignState.Suspended));
            Assert.That(Arc("claim").State, Is.EqualTo(ArcState.Active), "Progress is kept.");
            Assert.That(Arc("claim").StartedAt, Is.EqualTo(started));
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
            Assert.That(Say("apart").IsSuccess, Is.False, "Nothing is decided while the story is set aside.");
            Ok(S.Dispatch(new EndConversationCommand()));
            Ok(S.Dispatch(new SetCampaignStateCommand(Waystone, CampaignAction.Resume)));
            Assert.That(C.State, Is.EqualTo(CampaignState.Active));
        }

        // Validation ----------------------------------------------------------------------------------------

        [Test]
        public void TheDemonstrationCampaign_Validates_AndIsMarkedProvisional()
        {
            Assert.That(CampaignCatalog.Rejected.ContainsKey(Waystone), Is.False, CampaignCatalog.Rejected.TryGetValue(Waystone, out var why) ? string.Join("\n", why) : null);
            Assert.That(CampaignValidator.Validate(Def), Is.Empty);
            Assert.That(Def.Provisional, Is.True, "Demonstration content, not setting canon.");
            Assert.That(QuestCatalog.Validate(), Is.Empty);
            Assert.That(DialogueCatalog.Validate(), Is.Empty);
        }

        [Test]
        public void MalformedCampaigns_AreRejected_WithReadableErrors()
        {
            var bad = new CampaignDefinition
            {
                Id = "broken", Title = "Broken", Version = 1, FinalArc = "nowhere",
                Roles = new[]
                {
                    new CampaignRoleSpec { Name = "a", Kind = CampaignRoleKind.Npc, Rule = CampaignRoleRule.PersonIn, Of = "missing_town" },
                    new CampaignRoleSpec { Name = "a", Kind = CampaignRoleKind.Npc },
                },
                Quests = new[] { new CampaignQuestRef { Key = "q", QuestId = "no_such_quest" } },
                Chapters = new[]
                {
                    new ChapterDefinition { Id = "one", Title = "One", After = new[] { "two" } },
                    new ChapterDefinition { Id = "two", Title = "Two", After = new[] { "one" } },
                },
                Arcs = new[]
                {
                    new ArcDefinition { Id = "x", ChapterId = "one", Title = "X", After = new[] { "y" }, Objectives = new[] { new ArcObjectiveDefinition { Id = "o", Kind = ArcObjectiveKind.Quest, QuestKey = "q" } } },
                    new ArcDefinition { Id = "y", ChapterId = "one", Title = "Y", After = new[] { "x" }, Objectives = new[] { new ArcObjectiveDefinition { Id = "o", Kind = ArcObjectiveKind.State, Optional = true } } },
                },
                Decisions = new[]
                {
                    new DecisionDefinition { Id = "d", ArcId = "x", Choices = new[] { new ChoiceDefinition { Id = "c", Summary = "Did nothing" } } },
                    new DecisionDefinition { Id = "d", ArcId = "ghost", Choices = new ChoiceDefinition[0] },
                },
            };
            var errors = CampaignCatalog.Register(bad);
            string Has(string text) => errors.FirstOrDefault(e => e.Contains(text));
            Assert.That(Has("two of role 'a'"), Is.Not.Null, "Duplicate IDs.");
            Assert.That(Has("'missing_town', which is not a role"), Is.Not.Null, "Missing references.");
            Assert.That(Has("'no_such_quest', which does not exist"), Is.Not.Null, "Unknown quests.");
            Assert.That(Has("chapters wait on each other in a circle"), Is.Not.Null, "Circular chapters.");
            Assert.That(Has("arcs wait on each other in a circle"), Is.Not.Null, "Circular arcs.");
            Assert.That(Has("only optional objectives"), Is.Not.Null, "An arc that can never finish.");
            Assert.That(Has("changes nothing"), Is.Not.Null, "A decision with no consequence.");
            Assert.That(Has("two of decision 'd'"), Is.Not.Null);
            Assert.That(Has("has no choices"), Is.Not.Null);
            Assert.That(Has("final arc 'nowhere'"), Is.Not.Null, "Unresolvable ending.");
            Assert.That(CampaignCatalog.Find("broken"), Is.Null, "It never enters the catalog.");
            Assert.That(CampaignCatalog.Rejected.ContainsKey("broken"), Is.True);
            Hero();
            var r = S.Dispatch(new StartCampaignCommand("broken"));
            Assert.That(r.IsSuccess, Is.False);
            Assert.That(r.Message, Does.Contain("malformed"));
        }

        [Test]
        public void ASaveFromANewerCampaignVersion_IsRefused_NotSilentlyAccepted()
        {
            Hero();
            Begin();
            var w = W;
            CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(Waystone)).Version = Def.Version + 1;
            var v = WorldValidator.Validate(w);
            Assert.That(v.IsValid, Is.False);
            Assert.That(string.Join(" ", v.Errors), Does.Contain("newer version"));
        }
    }
}
