using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Quests;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Quests
{
    /// <summary>
    /// Phase 12 (ADR-0056 … ADR-0061): quests bound to the real world, the objective engine, dialogue with checks and
    /// consequences, knowledge and deeds, reputation, emergent activities, rewards and consequences, the Chronicle, saving,
    /// and what happens when things fail. The vertical slice is played through the same commands the UI uses.
    /// </summary>
    public sealed class QuestSystemTests
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

        private void Edit(Action<WorldData> change)
        {
            var w = W;
            change(w);
            var saved = new JsonWorldStore(_dir).Save(w, "edit");
            Assert.That(saved.IsSuccess, Is.True, saved.Error);
            var load = _s.Execute(new LoadWorldCommand("edit"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
        }

        private void Ok(CommandResult r) => Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));

        /// <summary>A fighter in the first town, at mid-morning (people are up and about).</summary>
        private SettlementData Ready()
        {
            CombatTests.Create(_s, "fighter", CombatTests.Scores);
            var town = QuestGenesis.FirstTown(W);
            Ok(_s.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            Edit(w => w.MinuteOfDay = 10 * 60);
            return town;
        }

        private QuestInstanceData Quest(string def) => W.Quests.First(q => q.DefinitionId == def && q.SourceKey == "authored");

        private NpcId Role(QuestInstanceData q, string role) => new NpcId(Guid.Parse(q.Bindings[role]));

        /// <summary>Starts talking to someone and follows the hook into the quest's conversation for the given role.</summary>
        private void Talk(NpcId npc, string instanceId = null, string role = null)
        {
            var r = _s.Dispatch(new StartConversationCommand(npc));
            if (!r.IsSuccess && r.Message.Contains("is not here")) { Edit(w => w.MinuteOfDay = 14 * 60); r = _s.Dispatch(new StartConversationCommand(npc)); }
            Ok(r);
            if (instanceId != null) Choose("hook:" + instanceId + ":" + role);
        }

        private CommandResult Choose(string id)
        {
            var view = _s.GetConversation();
            Assert.That(view, Is.Not.Null, "No conversation.");
            Assert.That(view.Responses.Select(x => x.Id), Has.Member(id), "Offered: " + string.Join(", ", view.Responses.Select(x => x.Id)) + " | " + view.NpcLine);
            var r = _s.Dispatch(new ChooseResponseCommand(id));
            Ok(r);
            return r;
        }

        private void Valid()
        {
            var v = WorldValidator.Validate(W);
            Assert.That(v.IsValid, Is.True, string.Join("; ", v.Errors));
        }

        private int Food(SettlementId s) => W.FindSettlement(s).ResourceInventory.Get(Realmkeeper.Domain.Economy.ResourceTypes.Food);

        private ItemStackData NewStack(string itemId, int quantity, int n) =>
            ItemStacks.Create(new ItemStackId(new Guid(n, 7, 7, 1, 2, 3, 4, 5, 6, 7, 8)), ItemCatalog.Find(itemId), quantity, "test");

        // ----- Data -------------------------------------------------------------------------------------

        [Test]
        public void TheCatalogs_AreConsistent_AndStatesMoveOnlyAsAllowed()
        {
            Assert.That(QuestCatalog.Validate(), Is.Empty);
            Assert.That(DialogueCatalog.Validate(), Is.Empty);
            Assert.That(QuestCatalog.All.Count(), Is.GreaterThanOrEqualTo(5));
            Assert.That(QuestStates.CanMove(QuestState.Available, QuestState.Offered), Is.True);
            Assert.That(QuestStates.CanMove(QuestState.Offered, QuestState.Accepted), Is.True);
            Assert.That(QuestStates.CanMove(QuestState.Active, QuestState.Completed), Is.True);
            Assert.That(QuestStates.CanMove(QuestState.Completed, QuestState.Completed), Is.False, "Never completed twice.");
            Assert.That(QuestStates.CanMove(QuestState.Failed, QuestState.Active), Is.False, "A failed quest does not silently reactivate.");
            Assert.That(QuestStates.CanMove(QuestState.Abandoned, QuestState.Available), Is.False);
            Assert.That(QuestStates.CanMove(QuestState.Abandoned, QuestState.Available, repeatable: true), Is.True, "Only a repeatable contract reopens.");
            Assert.That(QuestStates.CanMove(QuestState.Available, QuestState.Active), Is.False, "Accepting goes through an offer.");
        }

        [Test]
        public void TheAuthoredQuests_AreBoundToRealPeople_AndTheGranaryProblemIsReal()
        {
            var w = W;
            var town = QuestGenesis.FirstTown(w);
            var granary = w.Quests.Single(q => q.DefinitionId == QuestCatalog.GranaryShortfall);
            Assert.That(granary.State, Is.EqualTo(QuestState.Available));
            foreach (var role in new[] { "giver", "keeper", "suspect" })
            {
                var npc = w.FindNpc(Role(granary, role));
                Assert.That(npc, Is.Not.Null, role);
                Assert.That(npc.HomeSettlementId, Is.EqualTo(town.SettlementId), role + " lives in the town.");
            }

            Assert.That(granary.Bindings.Values.Distinct().Count(), Is.EqualTo(granary.Bindings.Count), "Every role a different entity.");
            Assert.That(w.FindNpc(Role(granary, "suspect")).FactionId.ToString(), Is.EqualTo(granary.Bindings["suspect_faction"]));
            Assert.That(int.Parse(granary.Variables["missing"]), Is.InRange(20, 300));
            Assert.That(granary.AppliedConsequences.Count(k => k.Contains(":OnAvailable:")), Is.EqualTo(2), "The grain left the stores and the Chronicle says so.");
            Assert.That(w.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.QuestConsequence), Is.GreaterThanOrEqualTo(1));
            var letter = w.Quests.Single(q => q.DefinitionId == QuestCatalog.SealedLetter);
            Assert.That(w.FindNpc(Role(letter, "recipient")).HomeSettlementId, Is.Not.EqualTo(town.SettlementId), "The letter goes elsewhere.");
            var copy = W;
            Assert.That(QuestGenesis.Ensure(copy), Is.False.Or.True);
            Assert.That(copy.Quests.Count(q => q.SourceKey == "authored"), Is.EqualTo(w.Quests.Count(q => q.SourceKey == "authored")), "Authored quests are made once.");
            Valid();
        }

        // ----- The vertical slice: Short Measures --------------------------------------------------------

        private QuestInstanceData AcceptGranary()
        {
            var q = Quest(QuestCatalog.GranaryShortfall);
            Talk(Role(q, "giver"));
            Assert.That(_s.GetConversation().Responses.Any(r => r.Text.Contains("troubled")), Is.True, "The giver's trouble shows as a way into the quest.");
            Choose("hook:" + q.InstanceId + ":giver");
            Assert.That(Quest(QuestCatalog.GranaryShortfall).State, Is.EqualTo(QuestState.Offered));
            Choose("accept");
            Choose("ok");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.State, Is.EqualTo(QuestState.Active));
            Assert.That(_s.GetJournal().Active.Select(a => a.InstanceId), Has.Member(q.InstanceId));
            return q;
        }

        private void Investigate(QuestInstanceData q, SettlementData town)
        {
            Ok(_s.Dispatch(new InspectObjectCommand("ledger:" + town.SettlementId)));
            Talk(Role(q, "keeper"), q.InstanceId, "keeper");
            Choose("thanks");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.Progress("ledger").Done && q.Progress("keeper").Done, Is.True);
            Assert.That(q.Progress("suspect").Revealed, Is.True, "Stage 1 opens and shows its objective.");
            Assert.That(W.Player.Social.Knows("suspect_cart"), Is.True);
            Talk(Role(q, "suspect"), q.InstanceId, "suspect");
            Choose("press");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.Progress("returned").Revealed && q.Progress("covered").Revealed && q.Progress("replaced").Revealed, Is.True, "Three ways to end it.");
        }

        [Test]
        public void ShortMeasures_MadeGood_RestoresTheStores_PaysOnce_AndIsRemembered()
        {
            var town = Ready();
            var q = AcceptGranary();
            Investigate(q, town);
            _s.Dispatch(new EndConversationCommand());
            Edit(w => w.Player.Inventory.Add(NewStack("grain_sack", 4, 1)));
            var food = Food(town.SettlementId);
            var keeper = Role(q, "keeper");
            Talk(keeper, q.InstanceId, "keeper");
            Choose("sacks");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.Progress("replaced").Done, Is.True);
            Assert.That(q.Outcome, Is.EqualTo("replaced"));
            Assert.That(W.Player.Inventory.Any(s => s.ItemId == "grain_sack"), Is.False, "The sacks left the pack.");

            var purse = W.Player.Purse;
            var xp = W.Player.Rpg.Experience;
            Talk(Role(q, "giver"), q.InstanceId, "giver");
            Choose("report_replaced");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.State, Is.EqualTo(QuestState.Completed));
            Assert.That(W.Player.Rpg.Experience, Is.EqualTo(xp + 120));
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 70));
            Assert.That(Food(town.SettlementId), Is.EqualTo(food + int.Parse(q.Variables["missing"])), "The stores are made whole.");
            var giver = W.FindNpc(Role(q, "giver"));
            Assert.That(giver.PlayerRegard.Gratitude, Is.GreaterThanOrEqualTo(25));
            Assert.That(giver.Memories.Any(m => m.Subject.Contains("made good")), Is.True);
            Assert.That(giver.Knowledge.Any(k => k.Topic == KnowledgeTopic.PlayerDeed && k.Value == "fed_settlement"), Is.True, "The giver knows of the deed.");
            Assert.That(W.Player.Social.ReputationIn(town.SettlementId), Is.GreaterThanOrEqualTo(20));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.QuestCompleted), Is.EqualTo(1));
            Assert.That(W.Player.Rpg.ExperienceLog.Count(a => a.Key.StartsWith("quest:" + q.InstanceId)), Is.EqualTo(1));
            Valid();

            // Reload, and nothing is paid again; the report cannot be given twice.
            var ser = new JsonWorldSerializer();
            var back = ser.Deserialize(ser.Serialize(W)).World;
            var bq = back.FindQuest(q.InstanceId);
            Assert.That(bq.State, Is.EqualTo(QuestState.Completed));
            Assert.That(bq.RewardsClaimed, Is.True);
            Talk(Role(q, "giver"));
            Assert.That(_s.GetConversation().Responses.Any(r => r.Id.StartsWith("hook:" + q.InstanceId)), Is.False, "A finished quest offers nothing more.");
        }

        [Test]
        public void ShortMeasures_TheSilverPath_PaysTheBribe_LeavesTheStoresShort_AndSpreadsNoGoodWill()
        {
            var town = Ready();
            var q = AcceptGranary();
            Investigate(q, town);
            var food = Food(town.SettlementId);
            var purse = W.Player.Purse;
            Choose("silver");
            Choose("take");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 50));
            Assert.That(q.Progress("covered").Done, Is.True, "The decision completes the path.");
            Assert.That(q.Decisions, Has.Member("took_silver"));
            Assert.That(W.Player.Social.Decisions.Any(d => d.Key == "took_silver"), Is.True);

            Talk(Role(q, "giver"), q.InstanceId, "giver");
            var lie = Choose("report_lie");
            Assert.That(lie.Message, Does.Contain("Deception"), "The check is shown.");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.State, Is.EqualTo(QuestState.Completed));
            Assert.That(q.Outcome, Is.EqualTo("covered"));
            Assert.That(Food(town.SettlementId), Is.EqualTo(food), "The grain is still gone.");
            Assert.That(W.Player.Social.Notoriety, Is.GreaterThanOrEqualTo(5));
            var giver = W.FindNpc(Role(q, "giver"));
            var caught = lie.Message.Contains("failure");
            Assert.That(giver.PlayerRegard.Trust < 10, Is.EqualTo(caught || giver.PlayerRegard.Trust < 10), "A failed lie costs trust.");
            if (caught) Assert.That(giver.Memories.Any(m => m.Subject.Contains("lied")), Is.True);
            Valid();
        }

        [Test]
        public void ShortMeasures_Confronting_UsesTheRealCheck_AndEitherWayTheStoryHolds()
        {
            var town = Ready();
            var q = AcceptGranary();
            Investigate(q, town);
            var food = Food(town.SettlementId);
            var r = Choose("persuade");
            q = Quest(QuestCatalog.GranaryShortfall);
            if (r.Message.Contains("success"))
            {
                Assert.That(q.Outcome, Is.EqualTo("returned"));
                Assert.That(q.Progress("returned").Done, Is.True);
            }
            else
            {
                Assert.That(q.Progress("returned").Done, Is.False, "A failed persuasion changes nothing but feelings.");
                Assert.That(_s.GetConversation().Responses.Select(x => x.Id), Has.No.Member("persuade"), "Once tried, it is not offered again.");
                Choose("intimidate");
                q = Quest(QuestCatalog.GranaryShortfall);
            }

            if (q.Outcome == "returned")
            {
                Talk(Role(q, "giver"), q.InstanceId, "giver");
                Choose("report_true");
                q = Quest(QuestCatalog.GranaryShortfall);
                Assert.That(q.State, Is.EqualTo(QuestState.Completed));
                Assert.That(Food(town.SettlementId), Is.EqualTo(food + int.Parse(q.Variables["missing"])));
                var suspect = W.FindNpc(Role(q, "suspect"));
                Assert.That(suspect.PlayerRegard.Hostility, Is.GreaterThanOrEqualTo(30), "The one exposed resents it.");
                Assert.That(W.Player.Social.StandingWith(W.FindNpc(Role(q, "giver")).FactionId), Is.GreaterThanOrEqualTo(W.FindNpc(Role(q, "giver")).FactionId.IsEmpty ? 0 : 8));
            }

            Valid();
        }

        [Test]
        public void Declining_RecordsIt_AndTheQuestCanStillBeTakenLater_AbandoningIsFinal()
        {
            Ready();
            var q = Quest(QuestCatalog.GranaryShortfall);
            Talk(Role(q, "giver"), q.InstanceId, "giver");
            Choose("decline");
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.State, Is.EqualTo(QuestState.Discovered));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.QuestDeclined), Is.EqualTo(1));
            Assert.That(_s.GetJournal().Available.Select(a => a.InstanceId), Has.Member(q.InstanceId), "Known, not taken.");
            Talk(Role(q, "giver"), q.InstanceId, "giver");
            Choose("accept");
            Assert.That(Quest(QuestCatalog.GranaryShortfall).State, Is.EqualTo(QuestState.Active));
            Ok(_s.Dispatch(new EndConversationCommand()));
            var trust = W.FindNpc(Role(q, "giver")).PlayerRegard.Trust;
            Ok(_s.Dispatch(new AbandonQuestCommand(q.InstanceId)));
            Assert.That(Quest(QuestCatalog.GranaryShortfall).State, Is.EqualTo(QuestState.Abandoned));
            Assert.That(W.FindNpc(Role(q, "giver")).PlayerRegard.Trust, Is.EqualTo(Math.Max(0, trust - 10)));
            Assert.That(_s.Dispatch(new AbandonQuestCommand(q.InstanceId)).IsSuccess, Is.False);
            Talk(Role(q, "giver"));
            Assert.That(_s.GetConversation().Responses.Any(r => r.Id.StartsWith("hook:" + q.InstanceId)), Is.False, "Not silently offered again.");
        }

        // ----- Under Seal: one particular item ---------------------------------------------------------

        private QuestInstanceData AcceptLetter()
        {
            var q = Quest(QuestCatalog.SealedLetter);
            Talk(Role(q, "giver"), q.InstanceId, "giver");
            Choose("accept");
            q = Quest(QuestCatalog.SealedLetter);
            var stack = q.Variables["letter_stack"];
            Assert.That(W.Player.Inventory.Any(s => s.StackId.ToString() == stack && s.ItemId == "sealed_letter"), Is.True, "This letter, as an item in the pack.");
            Assert.That(W.Player.Social.KnownPlaces, Has.Member(W.FindNpc(Role(q, "recipient")).HomeSettlementId.ToString()), "Told where it goes.");
            return q;
        }

        private void GoTo(SettlementId s)
        {
            Ok(_s.Dispatch(new ExitSettlementCommand()));
            Ok(_s.Dispatch(new EnterSettlementCommand(s)));
            Edit(w => w.MinuteOfDay = 10 * 60);
        }

        [Test]
        public void UnderSeal_DeliveredSealed_PaysAndEarnsTrust_TheItemIsGone()
        {
            Ready();
            var q = AcceptLetter();
            var recipient = W.FindNpc(Role(q, "recipient"));
            GoTo(recipient.HomeSettlementId);
            Assert.That(Quest(QuestCatalog.SealedLetter).Progress("reach").Done, Is.True, "Reaching the place counts (optional).");
            var purse = W.Player.Purse;
            Talk(recipient.NpcId, q.InstanceId, "recipient");
            Choose("give_sealed");
            q = Quest(QuestCatalog.SealedLetter);
            Assert.That(q.State, Is.EqualTo(QuestState.Completed));
            Assert.That(q.Outcome, Is.EqualTo("sealed"));
            Assert.That(W.Player.Inventory.Any(s => s.ItemId == "sealed_letter"), Is.False);
            Assert.That(W.Player.Purse, Is.EqualTo(purse + 25));
            Assert.That(W.FindNpc(recipient.NpcId).PlayerRegard.Gratitude, Is.GreaterThanOrEqualTo(15));
            Assert.That(_s.GetConversation().NpcLine, Does.Contain("seal's whole"), "The person reacts to what happened.");
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.QuestItemDelivered), Is.EqualTo(1));
            Valid();
        }

        [Test]
        public void UnderSeal_ReadFirst_RevealsItsContents_AndTheRecipientKnows()
        {
            Ready();
            var q = AcceptLetter();
            var stack = new ItemStackId(Guid.Parse(q.Variables["letter_stack"]));
            var read = _s.Dispatch(new ReadItemCommand(stack));
            Ok(read);
            Assert.That(read.Message, Does.Contain("break the seal"));
            q = Quest(QuestCatalog.SealedLetter);
            Assert.That(q.Variables["opened"], Is.EqualTo("yes"));
            Assert.That(q.Progress("read").Done && q.Progress("read").Revealed, Is.True, "A hidden objective revealed by doing it.");
            Assert.That(W.Player.Social.Knows("letter_contents"), Is.True);
            var recipient = W.FindNpc(Role(q, "recipient"));
            var trust = recipient.PlayerRegard.Trust;
            GoTo(recipient.HomeSettlementId);
            Talk(recipient.NpcId, q.InstanceId, "recipient");
            Assert.That(_s.GetConversation().Responses.Select(r => r.Id), Has.No.Member("give_sealed"), "You cannot pretend it is sealed.");
            Choose("give_opened");
            q = Quest(QuestCatalog.SealedLetter);
            Assert.That(q.Outcome, Is.EqualTo("opened"));
            Assert.That(W.FindNpc(recipient.NpcId).PlayerRegard.Trust, Is.EqualTo(Math.Max(0, trust - 20)));
            Assert.That(W.FindNpc(recipient.NpcId).Memories.Any(m => m.Subject.Contains("seal broken")), Is.True);
        }

        // ----- The objective engine ------------------------------------------------------------------------

        [Test]
        public void TheHunt_CountsOnlyTheRightCreatures_NearTheRightPlace_AndSurvivingIsOptional()
        {
            var town = Ready();
            var copy = W;
            var def = QuestCatalog.Find(QuestCatalog.WolvesAtTheFolds);
            var giver = copy.Npcs.First(n => n.HomeSettlementId == town.SettlementId && !n.IsDeceased && n.Tier != NpcTier.Unassigned);
            var q = QuestLifecycle.Create(copy, def, new Dictionary<string, string> { ["giver"] = giver.NpcId.ToString(), ["creature"] = "wolf" }, "predators:test:wolf",
                new Dictionary<string, string> { ["count"] = "2", ["creature_plural"] = "wolves" }, null, out var problem);
            Assert.That(q, Is.Not.Null, problem);
            QuestLifecycle.Move(copy, q, QuestState.Offered, null, null);
            QuestLifecycle.Move(copy, q, QuestState.Accepted, null, null);
            QuestLifecycle.Move(copy, q, QuestState.Active, null, null);
            var other = copy.Settlements.First(s => s.SettlementId != town.SettlementId).SettlementId;
            QuestObjectives.Observe(copy, q, new QuestEvent { Kind = QuestEventKind.Defeated, Subject = "deer", Settlement = town.SettlementId });
            QuestObjectives.Observe(copy, q, new QuestEvent { Kind = QuestEventKind.Defeated, Subject = "wolf", Settlement = other });
            Assert.That(q.Progress("hunt").Count, Is.EqualTo(0), "Deer and wolves elsewhere do not count.");
            QuestObjectives.Observe(copy, q, new QuestEvent { Kind = QuestEventKind.Defeated, Subject = "wolf", Settlement = town.SettlementId });
            QuestObjectives.Observe(copy, q, new QuestEvent { Kind = QuestEventKind.LostEncounter, Subject = "wolf", Settlement = town.SettlementId });
            Assert.That(q.Progress("hunt").Count, Is.EqualTo(1));
            Assert.That(q.Progress("survive").Failed, Is.True, "The optional objective failed…");
            Assert.That(QuestLifecycle.FailureNow(copy, q), Is.Null, "…without failing the quest.");
            Assert.That(QuestObjectives.StageOpen(def, q, 1), Is.False, "Reporting waits for the hunt.");
            QuestObjectives.Observe(copy, q, new QuestEvent { Kind = QuestEventKind.Defeated, Subject = "wolf", Settlement = town.SettlementId });
            Assert.That(q.Progress("hunt").Done, Is.True);
            Assert.That(QuestObjectives.StageOpen(def, q, 1), Is.True);
            Assert.That(q.Progress("report").Revealed, Is.True);
            Assert.That(QuestObjectives.AllDone(def, q), Is.False);
        }

        [Test]
        public void AlternativePaths_SatisfyOneGroup_AndOptionalNeverBlocks()
        {
            var def = QuestCatalog.Find(QuestCatalog.GranaryShortfall);
            var q = new QuestInstanceData { InstanceId = "t", DefinitionId = def.Id, State = QuestState.Active };
            foreach (var o in def.Objectives) q.Objectives.Add(new ObjectiveProgressData { Id = o.Id });
            Assert.That(QuestObjectives.StageDone(def, q, 2), Is.False);
            q.Progress("covered").Done = true;
            Assert.That(QuestObjectives.StageDone(def, q, 2), Is.True, "Any one ending completes the resolution.");
            var hunt = QuestCatalog.Find(QuestCatalog.WolvesAtTheFolds);
            var h = new QuestInstanceData { InstanceId = "h", DefinitionId = hunt.Id, State = QuestState.Active };
            foreach (var o in hunt.Objectives) h.Objectives.Add(new ObjectiveProgressData { Id = o.Id });
            h.Progress("hunt").Done = true;
            Assert.That(QuestObjectives.StageDone(hunt, h, 0), Is.True, "The optional survival objective does not block.");
        }

        [Test]
        public void AnEventForALaterStage_DoesNotCount_UntilThatStageOpens()
        {
            Ready();
            AcceptGranary();
            var w = W;
            var q = w.Quests.First(x => x.DefinitionId == QuestCatalog.GranaryShortfall && x.SourceKey == "authored");
            var sacks = new QuestEvent { Kind = QuestEventKind.Delivered, Subject = "grain_sack", Target = q.Bindings["keeper"], Quantity = 4 };
            Assert.That(QuestObjectives.Observe(w, q, sacks), Is.False, "Stage 0 is open; the resolution stage is not.");
            Assert.That(q.Progress("replaced").Count, Is.EqualTo(0));
            Assert.That(q.Progress("replaced").Done, Is.False);
            foreach (var id in new[] { "ledger", "keeper", "suspect" }) q.Progress(id).Done = true;
            Assert.That(QuestObjectives.Observe(w, q, sacks), Is.True, "Once the stage is open the same delivery counts.");
            Assert.That(q.Progress("replaced").Done, Is.True);
        }

        [Test]
        public void AConsequence_IsAppliedOnce_EvenWhenItsTimingComesAgain()
        {
            var w = W;
            var q = w.Quests.Single(x => x.DefinitionId == QuestCatalog.GranaryShortfall);
            var town = w.FindSettlement(new SettlementId(Guid.Parse(q.Bindings["settlement"])));
            var food = new Realmkeeper.Domain.Economy.ResourceTypeId("food");
            var before = town.ResourceInventory.Get(food);
            var events = w.HistoricalEvents.Count;
            var applied = q.AppliedConsequences.Count;
            QuestEffects.ApplyAll(w, q, ConsequenceTiming.OnAvailable, QuestGenesis.Direct(w));
            Assert.That(town.ResourceInventory.Get(food), Is.EqualTo(before), "The grain is not taken twice.");
            Assert.That(w.HistoricalEvents.Count, Is.EqualTo(events), "The Chronicle is not told twice.");
            Assert.That(q.AppliedConsequences.Count, Is.EqualTo(applied));
        }

        [Test]
        public void AFailureCondition_FailsTheQuest_OnceAndWithItsReason()
        {
            Ready();
            var q = AcceptGranary();
            Edit(w => Realmkeeper.Simulation.Npcs.NpcSystem.RecordDeath(w, w.FindNpc(Role(q, "giver")), w.FindSettlement(w.FindNpc(Role(q, "giver")).HomeSettlementId), w.CurrentSimulationDate));
            _s.Execute(new AdvanceDaysCommand(1));
            q = Quest(QuestCatalog.GranaryShortfall);
            Assert.That(q.State, Is.EqualTo(QuestState.Failed));
            Assert.That(q.FailureReason, Does.Contain("dead"));
            _s.Execute(new AdvanceDaysCommand(1));
            Assert.That(W.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.QuestFailed), Is.EqualTo(1), "Failed once.");
        }

        // ----- Emergent activities ---------------------------------------------------------------------

        [Test]
        public void AFoodShortage_RaisesARequest_OnlyOnce_AndItLapsesWhenTheShortageEnds()
        {
            var town = Ready();
            var s = W.Settlements.First(x => x.SettlementId != town.SettlementId && W.Npcs.Any(n => n.HomeSettlementId == x.SettlementId && n.Tier != NpcTier.Unassigned));
            var copy = W;
            copy.FindSettlement(s.SettlementId).IsInFoodShortage = true; // derived each day from the stores; set on a copy
            Assert.That(QuestGenesis.Emerge(copy, null), Is.True);
            var key = "shortage:food:" + s.SettlementId;
            Assert.That(copy.Quests.Count(q => q.SourceKey == key), Is.EqualTo(1));
            Assert.That(QuestGenesis.Emerge(copy, null), Is.False, "No duplicate while one is open.");
            var relief = copy.Quests.Single(q => q.SourceKey == key);
            Assert.That(copy.FindNpc(new NpcId(Guid.Parse(relief.Bindings["giver"]))).HomeSettlementId, Is.EqualTo(s.SettlementId));
            copy.FindSettlement(s.SettlementId).IsInFoodShortage = false;
            Assert.That(QuestCauses.Holds(copy, relief), Is.False, "Its cause is read from the world.");
            relief.State = QuestState.Active;
            Assert.That(QuestLifecycle.FailureNow(copy, relief), Does.Contain("shortage"), "An accepted request lapses with its cause.");
            copy.FindSettlement(s.SettlementId).IsInFoodShortage = true;
            Assert.That(QuestLifecycle.FailureNow(copy, relief), Is.Null, "While the shortage lasts, it stands.");
        }

        [Test]
        public void AReputationChange_WithTheSameKey_IsAppliedOnce()
        {
            var town = Ready();
            var w = W;
            var npc = w.Npcs.First(n => n.HomeSettlementId == town.SettlementId && n.Tier != NpcTier.Unassigned && !n.IsDeceased);
            var target = "npc:" + npc.NpcId;
            var trust = npc.PlayerRegard?.Trust ?? 0;
            Assert.That(Reputation.Change(w, target, "Trust", 15, "test:once", "a test"), Is.True);
            var after = w.FindNpc(npc.NpcId).PlayerRegard.Trust;
            Assert.That(after, Is.EqualTo(trust + 15));
            Assert.That(Reputation.Change(w, target, "Trust", 15, "test:once", "a test"), Is.False, "A repeated key is refused.");
            Assert.That(w.FindNpc(npc.NpcId).PlayerRegard.Trust, Is.EqualTo(after));
            Assert.That(w.Player.Social.Ledger.Count(e => e.Key == "test:once"), Is.EqualTo(1));
            var standing = w.Player.Social.ReputationIn(town.SettlementId);
            Assert.That(Reputation.Change(w, "settlement:" + town.SettlementId, "Reputation", 5, "test:town", "a test"), Is.True);
            Assert.That(Reputation.Change(w, "settlement:" + town.SettlementId, "Reputation", 5, "test:town", "a test"), Is.False);
            Assert.That(w.Player.Social.ReputationIn(town.SettlementId), Is.EqualTo(standing + 5));
        }

        [Test]
        public void AMerchantOutOfStock_AsksForIt_AndTheDeliveryStocksTheShelf()
        {
            var town = Ready();
            var shop = W.Shops.FirstOrDefault(x => x.SettlementId == town.SettlementId && x.Categories.Contains(ShopCategory.Materials));
            Assume.That(shop, Is.Not.Null, "No materials shop in town.");
            Edit(w => w.FindShop(shop.ShopId).Stock.RemoveAll(l => l.ItemId == "timber_plank"));
            var copy = W;
            QuestGenesis.Emerge(copy, null);
            var q = copy.Quests.FirstOrDefault(x => x.SourceKey == "supplies:" + shop.ShopId + ":timber_plank");
            Assert.That(q, Is.Not.Null, "The empty shelf raised a request.");
            Assert.That(q.Bindings["giver"], Is.EqualTo(shop.ShopkeeperNpcId.ToString()));
            Assert.That(int.Parse(q.Variables["payment"]), Is.GreaterThan(0));
            Assert.That(QuestText.Fill(copy, q, QuestCatalog.Find(QuestCatalog.ShopSupplies).Description), Does.Contain("timber plank"));
        }

        // ----- Knowledge, deeds, reputation ------------------------------------------------------------

        [Test]
        public void ADeed_Spreads_ByThePhase4Channels_AndEachListenerReactsOnce()
        {
            Ready();
            var w = W;
            var witness = w.Npcs.First(n => !n.IsDeceased && n.Tier != NpcTier.Unassigned && w.Npcs.Count(x => x.HomeSettlementId == n.HomeSettlementId) > 3);
            Edit(x => Deeds.Witness(x, x.FindNpc(witness.NpcId), "recovered_grain", "test", "k1"));
            var heldBefore = W.Npcs.Count(n => n.Knowledge.Any(k => k.Topic == KnowledgeTopic.PlayerDeed));
            Assert.That(heldBefore, Is.EqualTo(1), "Only the witness knows at first.");
            _s.Execute(new AdvanceDaysCommand(14));
            var after = W;
            var holders = after.Npcs.Where(n => n.Knowledge.Any(k => k.Topic == KnowledgeTopic.PlayerDeed)).ToList();
            Assert.That(holders.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(holders.Count, Is.LessThan(after.Npcs.Count), "Not everyone knows everything.");
            foreach (var h in holders.Where(n => n.NpcId != witness.NpcId))
            {
                var deed = h.Knowledge.First(k => k.Topic == KnowledgeTopic.PlayerDeed);
                Assert.That(deed.Hops, Is.GreaterThan(0), "Heard, not seen.");
                Assert.That(deed.Status, Is.Not.EqualTo(KnowledgeStatus.Known));
            }

            var keys = after.Player.Social.Ledger.Select(e => e.Key).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "No reaction applied twice.");
            Valid();
        }

        [Test]
        public void ReputationTiers_ComeFromSeparateDimensions()
        {
            var t = ReputationThresholds.Default;
            Assert.That(t.ForPerson(0, 0, 0, 0, 0, 0, 0), Is.EqualTo(ReputationTier.Unknown));
            Assert.That(t.ForPerson(20, 10, 10, 10, 0, 0, 0), Is.EqualTo(ReputationTier.Recognized));
            Assert.That(t.ForPerson(30, 20, 60, 30, 0, 0, 0), Is.EqualTo(ReputationTier.Trusted));
            Assert.That(t.ForPerson(30, 0, 10, 10, 60, 0, 0), Is.EqualTo(ReputationTier.Feared));
            Assert.That(t.ForPerson(30, 50, 90, 90, 0, 0, 60), Is.EqualTo(ReputationTier.Hostile), "Hostility outranks warmth.");
            Assert.That(t.ForStanding(85), Is.EqualTo(ReputationTier.Honored));
            Assert.That(t.ForStanding(-60), Is.EqualTo(ReputationTier.Hostile));
        }

        [Test]
        public void FactionStanding_ChangesPrices_AndHostilityClosesTheShop()
        {
            var town = Ready();
            var shop = W.Shops.FirstOrDefault(x => x.SettlementId == town.SettlementId && !W.FindNpc(x.ShopkeeperNpcId).FactionId.IsEmpty && x.Stock.Count > 0);
            Assume.That(shop, Is.Not.Null);
            var faction = W.FindNpc(shop.ShopkeeperNpcId).FactionId;
            var item = ItemCatalog.Find(shop.Stock[0].ItemId);
            var before = Realmkeeper.Simulation.Commerce.CommerceMath.SellingPrice(W, W.FindShop(shop.ShopId), item);
            Edit(w => Reputation.Change(w, "faction:" + faction, "Standing", 40, "test", "test"));
            var after = Realmkeeper.Simulation.Commerce.CommerceMath.SellingPrice(W, W.FindShop(shop.ShopId), item);
            Assert.That(after.Factors.Any(f => f.Label.StartsWith("Standing with")), Is.True);
            Assert.That(after.UnitPrice, Is.LessThanOrEqualTo(before.UnitPrice));
            Edit(w => Reputation.Change(w, "faction:" + faction, "Standing", -100, "test2", "test"));
            var r = _s.Dispatch(new BuyItemCommand(shop.ShopId, item.Id, 1, after.UnitPrice));
            Assert.That(r.IsSuccess, Is.False);
            if (!r.Message.Contains("closed") && !r.Message.Contains("not")) Assert.Fail(r.Message);
        }

        [Test]
        public void Conditions_ReadThePerson_TheirKnowledge_AndTheContentRules()
        {
            Ready();
            var q = Quest(QuestCatalog.GranaryShortfall);
            Talk(Role(q, "giver"));
            Assert.That(_s.GetConversation().NpcLine, Is.Not.Empty);
            Choose("ask");
            Choose("ask_settlement");
            Assert.That(_s.GetConversation().NpcLine, Is.Not.EqualTo("\"Ask away.\""), "The answer is their own knowledge.");
            Choose("ask_rumours");
            Ok(_s.Dispatch(new EndConversationCommand()));

            // Threats hidden by the content rules: the intimidation is not offered.
            AcceptGranaryFrom(q);
            Investigate(Quest(QuestCatalog.GranaryShortfall), QuestGenesis.FirstTown(W));
            Assert.That(_s.GetConversation().Responses.Select(r => r.Id), Has.Member("intimidate"));
            Ok(_s.Dispatch(new SetContentPresentationCommand(ContentTags.Threats, ContentPresentation.Hide)));
            Assert.That(_s.GetConversation().Responses.Select(r => r.Id), Has.No.Member("intimidate"));
        }

        private void AcceptGranaryFrom(QuestInstanceData q)
        {
            Talk(Role(q, "giver"), q.InstanceId, "giver");
            Choose("accept");
            Choose("ok");
        }

        // ----- Discovery and the journal ----------------------------------------------------------------

        [Test]
        public void TheJournal_ShowsOnlyWhatThePlayerKnows()
        {
            var town = Ready();
            var j = _s.GetJournal();
            Assert.That(j.Active.Count + j.Available.Count, Is.EqualTo(0), "Nothing known yet: the world's quests are not the player's.");
            Assert.That(_s.GetQuest(Quest(QuestCatalog.GranaryShortfall).InstanceId), Is.Null);
            Ok(_s.Dispatch(new InspectObjectCommand("ledger:" + town.SettlementId)));
            Assert.That(_s.GetJournal().Available.Select(a => a.InstanceId), Has.Member(Quest(QuestCatalog.GranaryShortfall).InstanceId), "The ledger tells of it.");
            var detail = _s.GetQuest(Quest(QuestCatalog.GranaryShortfall).InstanceId);
            Assert.That(detail.Objectives.Select(o => o.Id), Has.No.Member("suspect"), "Hidden objectives stay hidden.");
            Assert.That(detail.Objectives.Select(o => o.Id), Has.No.Member("covered"));
            Assert.That(_s.GetJournal().Discoveries.Count, Is.GreaterThan(0));
            var events = _s.GetJournal().Events.Select(e => e.Text).ToList();
            Assert.That(events.Any(e => e.Contains("heard of")), Is.True, "The Chronicle shows the player's discovery.");
        }

        [Test]
        public void Tracking_IsLimited_AndOnlyForQuestsInProgress()
        {
            Ready();
            var q = AcceptGranary();
            Assert.That(Quest(QuestCatalog.GranaryShortfall).Tracked, Is.True, "The first quest taken is tracked.");
            Ok(_s.Dispatch(new TrackQuestCommand(q.InstanceId, false)));
            Assert.That(_s.GetTrackedQuests(), Is.Empty);
            Ok(_s.Dispatch(new TrackQuestCommand(q.InstanceId, true)));
            Assert.That(_s.GetTrackedQuests().Single().Next, Does.Contain("ledger"));
            Assert.That(_s.Dispatch(new TrackQuestCommand(Quest(QuestCatalog.SealedLetter).InstanceId, true)).IsSuccess, Is.False);
        }

        // ----- Saves --------------------------------------------------------------------------------------

        [Test]
        public void EverythingSurvivesSaveAndLoad_EvenMidConversation()
        {
            var town = Ready();
            var q = AcceptGranary();
            Ok(_s.Dispatch(new InspectObjectCommand("ledger:" + town.SettlementId)));
            Talk(Role(q, "keeper"), q.InstanceId, "keeper");
            var ser = new JsonWorldSerializer();
            var json = ser.Serialize(W);
            var back = ser.Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            Assert.That(JObject.Parse(ser.Serialize(back.World))["world"].ToString(), Is.EqualTo(JObject.Parse(json)["world"].ToString()), "Exact round trip.");
            Assert.That(back.World.Conversation.NodeId, Is.EqualTo("keeper"));
            Assert.That(back.World.FindQuest(q.InstanceId).Progress("ledger").Done, Is.True);

            // The same conversation, continued after a reload.
            Ok(_s.Execute(new SaveWorldCommand("mid")));
            var fresh = Realmkeeper.Infrastructure.Composition.RealmkeeperComposition.CreateSession(_dir);
            ExplorationModule.Register(fresh);
            Ok(fresh.Execute(new LoadWorldCommand("mid")));
            Assert.That(fresh.GetConversation().Responses.Select(r => r.Id), Has.Member("thanks"));
            Ok(fresh.Dispatch(new ChooseResponseCommand("thanks")));
        }

        [Test]
        public void AVersion13Save_GainsTheQuestWorldOnce()
        {
            Ready();
            var ser = new JsonWorldSerializer();
            var doc = JObject.Parse(ser.Serialize(W));
            Phase2Worlds.StripToV13(doc);
            Assert.That(doc["world"]["quests"], Is.Null);
            var loaded = ser.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.World.Quests, Is.Empty);
            Assert.That(loaded.World.WorldSettings.EnabledSimulationSystems, Has.Member(QuestSystem.Id));
            Assert.That(QuestGenesis.Ensure(loaded.World), Is.True);
            var count = loaded.World.Quests.Count;
            QuestGenesis.Ensure(loaded.World);
            Assert.That(loaded.World.Quests.Count, Is.EqualTo(count), "Once.");
            Assert.That(WorldValidator.Validate(loaded.World).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(loaded.World).Errors));
        }

        // ----- Faults and determinism ----------------------------------------------------------------------

        [Test]
        public void AFailedChoice_ChangesNothing_AndTooHeavyARewardFailsTheWholeCompletion()
        {
            var town = Ready();
            var q = AcceptGranary();
            Investigate(q, town);
            Ok(_s.Dispatch(new EndConversationCommand()));
            Edit(w => w.Player.Inventory.Add(NewStack("grain_sack", 4, 2)));
            Talk(Role(q, "keeper"), q.InstanceId, "keeper");
            Edit(w => w.Player.Inventory.RemoveAll(s => s.ItemId == "grain_sack"));
            var before = W;
            var r = _s.Dispatch(new ChooseResponseCommand("sacks"));
            Assert.That(r.IsSuccess, Is.False, "The response is not open without the sacks.");
            Assert.That(W.Conversation.Transcript.Count, Is.EqualTo(before.Conversation.Transcript.Count), "Nothing said, nothing changed.");

            // The hunt's reward is a potion: make it too heavy to carry, and the completion fails as a whole.
            var copy = W;
            var hunt = QuestLifecycle.Create(copy, QuestCatalog.Find(QuestCatalog.WolvesAtTheFolds),
                new Dictionary<string, string> { ["giver"] = q.Bindings["giver"], ["creature"] = "wolf" }, "predators:fault:wolf", new Dictionary<string, string> { ["count"] = "2", ["creature_plural"] = "wolves" }, null, out _);
            QuestLifecycle.Move(copy, hunt, QuestState.Offered, null, null);
            QuestLifecycle.Move(copy, hunt, QuestState.Accepted, null, null);
            QuestLifecycle.Move(copy, hunt, QuestState.Active, null, null);
            hunt.Progress("hunt").Count = 2;
            hunt.Progress("hunt").Done = true;
            hunt.Progress("report").Revealed = true;
            for (var i = 0; i < 40; i++) copy.Player.Inventory.Add(NewStack("iron_ore", 50, 100 + i));
            copy.Player.Inventory.RemoveAll(s => s.ItemId == "iron_ore" && ItemWeights.CarriedGrams(copy.Player) > ItemCommandHandlers.PackLimitGrams(copy.Player));
            while (ItemWeights.CarriedGrams(copy.Player) + 100 < ItemCommandHandlers.PackLimitGrams(copy.Player)) copy.Player.Inventory.Add(NewStack("iron_ore", 1, 900 + copy.Player.Inventory.Count));
            var context = new CommandContext(_s, copy, new Realmkeeper.Infrastructure.Randomness.Xoshiro256RandomFactory());
            var xp = copy.Player.Rpg.Experience;
            var failure = QuestRuntime.Apply(context, new[] { new QuestEvent { Kind = QuestEventKind.ObjectiveCompleted, Target = hunt.InstanceId, Detail = "report" } });
            Assert.That(failure, Is.Not.Null, "Too heavy: the completion is refused.");
            Assert.That(failure.Message, Does.Contain("Make room"));
        }

        [Test]
        public void ARewardAlreadyClaimed_IsNeverPaidAgain_EvenIfCompletionRunsTwice()
        {
            // Phase 13 (preflight): the guard in QuestRuntime.Complete cannot be reached through the public commands
            // (closed quests are skipped), so this calls it directly, twice, on the same quest.
            Ready();
            AcceptGranary();
            var copy = W;
            var q = copy.Quests.First(x => x.DefinitionId == QuestCatalog.GranaryShortfall && x.SourceKey == "authored");
            var def = QuestCatalog.Find(q.DefinitionId);
            q.Outcome = "returned";
            foreach (var p in q.Objectives) p.Done = p.Id != "replaced" && p.Id != "covered";
            var context = new CommandContext(_s, copy, new Realmkeeper.Infrastructure.Randomness.Xoshiro256RandomFactory());
            var coin = copy.Player.Purse;
            var xp = copy.Player.Rpg.Experience;
            Assert.That(QuestRuntime.Complete(context, copy, q, def, QuestRuntime.Reporter(context)), Is.Null, "The first completion pays.");
            Assert.That(q.RewardsClaimed, Is.True);
            var paidCoin = copy.Player.Purse;
            var paidXp = copy.Player.Rpg.Experience;
            Assert.That(paidCoin + paidXp, Is.GreaterThan(coin + xp), "Something was paid the first time.");
            Assert.That(QuestRuntime.Complete(context, copy, q, def, QuestRuntime.Reporter(context)), Is.Null);
            Assert.That(copy.Player.Purse, Is.EqualTo(paidCoin), "No second payment of coin.");
            Assert.That(copy.Player.Rpg.Experience, Is.EqualTo(paidXp), "No second payment of experience.");
            q.State = QuestState.Active; // even an open quest whose rewards were claimed is refused
            Assert.That(QuestRuntime.Complete(context, copy, q, def, QuestRuntime.Reporter(context)), Is.Null);
            Assert.That(copy.Player.Purse, Is.EqualTo(paidCoin));
        }

        [Test]
        public void TheValidator_CatchesImpossibleQuestStates()
        {
            Ready();
            AcceptGranary();
            string Errors(Action<WorldData> breakIt)
            {
                var w = W;
                breakIt(w);
                return string.Join("; ", WorldValidator.Validate(w).Errors);
            }

            var id = Quest(QuestCatalog.GranaryShortfall).InstanceId;
            Assert.That(Errors(w => w.FindQuest(id).State = QuestState.Completed), Does.Contain("never given"));
            Assert.That(Errors(w => w.FindQuest(id).RewardsClaimed = true), Does.Contain("without being completed"));
            Assert.That(Errors(w => w.FindQuest(id).Progress("ledger").Count = 9), Does.Contain("counts 9"));
            Assert.That(Errors(w => w.Quests.Add(w.FindQuest(id).Clone())), Does.Contain("duplicated"));
            Assert.That(Errors(w => w.FindQuest(id).DefinitionId = "nonesuch"), Does.Contain("unknown quest"));
            Assert.That(Errors(w => { w.FindQuest(id).State = QuestState.Failed; w.FindQuest(id).ClosedAt = 1; }), Does.Contain("tracked"));
            Assert.That(Errors(w => w.Player.Social.Notoriety = 400), Does.Contain("Notoriety"));
            Assert.That(Errors(w => { w.Player.Social.Ledger.Add(new ReputationChangeData { Key = "x" }); w.Player.Social.Ledger.Add(new ReputationChangeData { Key = "x" }); }), Does.Contain("twice"));
            Assert.That(Errors(w => w.Conversation = new ConversationData { NpcId = w.Npcs[0].NpcId, DialogueId = "npc_general", NodeId = "nowhere", Key = "c" }), Does.Contain("unknown place"));
        }

        [Test]
        public void CorruptQuestStateInASave_IsRejected()
        {
            Ready();
            AcceptGranary();
            var ser = new JsonWorldSerializer();
            var doc = JObject.Parse(ser.Serialize(W));
            doc["world"]["quests"][0]["state"] = "Half-done";
            Assert.That(ser.Deserialize(doc.ToString()).IsSuccess, Is.False);
        }

        [Test]
        public void TheSameActions_GiveTheSameWorld()
        {
            string Play(string dir)
            {
                var s = Phase2Worlds.Session(dir);
                ExplorationModule.Register(s);
                var keep = _s;
                var keepDir = _dir;
                _s = s;
                _dir = dir;
                try
                {
                    var town = Ready();
                    var q = AcceptGranary();
                    Investigate(q, town);
                    Choose("persuade");
                    return JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"]["quests"].ToString() + W.Player.Social.Ledger.Count;
                }
                finally
                {
                    _s = keep;
                    _dir = keepDir;
                }
            }

            var a = Play(Phase2Worlds.TempDirectory());
            var b = Play(Phase2Worlds.TempDirectory());
            Assert.That(b, Is.EqualTo(a), "Same seed, same choices, same rolls, same quests.");
        }
    }
}
