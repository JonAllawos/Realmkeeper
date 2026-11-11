using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Shared steps for Phase 12 tests and captures.</summary>
    internal static class QuestDriver
    {
        public static QuestSummaryView Find(WorldSession s, string definitionPrefix, Func<JournalView, System.Collections.Generic.List<QuestSummaryView>> list) =>
            list(s.GetJournal()).FirstOrDefault(q => q.InstanceId.StartsWith("quest:" + definitionPrefix, StringComparison.Ordinal));

        /// <summary>The instance ID of an authored quest (from its giver's hook in their conversation).</summary>
        public static string Instance(string definition, WorldSession s, NpcId giver)
        {
            var view = s.GetConversation();
            return view?.Responses.FirstOrDefault(r => r.Id.StartsWith("hook:quest:" + definition, StringComparison.Ordinal))?.QuestInstanceId;
        }

        /// <summary>Clicks a dialogue response by its ID in the dialogue window.</summary>
        public static void Say(ExplorationController c, string responseId) => LevelUpDriver.Click(c.Dialogue.Root, "xk-dialogue-response-" + responseId);

        /// <summary>Follows the giver's hook for a definition into its conversation.</summary>
        public static string Hook(ExplorationController c, string definition)
        {
            var r = c.Dialogue.View.Responses.FirstOrDefault(x => x.Id.StartsWith("hook:quest:" + definition, StringComparison.Ordinal));
            Assert.That(r, Is.Not.Null, "No way into " + definition + ": " + string.Join(", ", c.Dialogue.View.Responses.Select(x => x.Id)));
            Say(c, r.Id);
            return r.QuestInstanceId;
        }

        /// <summary>The bound person of a role, read from the quest's journal detail (or the hook's quest) via a fresh conversation-free query.</summary>
        public static NpcId Role(WorldSession s, string instanceId, string role)
        {
            var parts = s.GetQuestBindings(instanceId);
            return parts != null && parts.TryGetValue(role, out var id) && Guid.TryParse(id, out var g) ? new NpcId(g) : default;
        }
    }

    /// <summary>
    /// Phase 12 in the shipped scene, through the real UI (ADR-0055, ADR-0058): approaching a person and opening the
    /// conversation, choosing responses, accepting and declining, the journal (lists, detail, tracking), examining the
    /// ledger and the notice board, a delivery, rewards, reputation and an NPC's changed reaction, an encounter objective
    /// through an outing, and saving and reloading with everything kept. Seventeen flows.
    /// </summary>
    public sealed class Phase12PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Morning(ExplorationController c, WorldSession s) => ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 600);

        [UnityTest]
        public IEnumerator ShortMeasures_ThroughTheRealUi_FromApproachToReward_AndAfterAReload()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Morning(c, s);
            var granary = s.FindQuestInstance(QuestCatalog.GranaryShortfall);
            Assume.That(granary, Is.Not.Null, "This town has no granary quest.");
            var giver = QuestDriver.Role(s, granary, "giver");

            // 1. Approach the giver: they become the thing in front of the player; interacting greets and offers topics.
            if (c.Residents.TryGetValue(giver, out var resident))
            {
                var p = resident.transform.position;
                c.Player.Teleport(new LocalPose(p.x + 0.2f, p.y, p.z, 0f));
                for (var i = 0; i < 3; i++) yield return null;
                if (c.CurrentTarget == (object)resident)
                {
                    c.Interact();
                    yield return null;
                    Assert.That(c.Hud.TopicsVisible, Is.True);
                    // 2. Start dialogue: "Talk…" opens the conversation window.
                    LevelUpDriver.Click(c.Hud.Root, "xk-topic-0");
                    yield return null;
                }
            }

            if (c.Dialogue == null || !c.Dialogue.IsOpen) c.StartDialogue(giver); // the giver was not in the street this hour
            yield return null;
            Assert.That(c.Dialogue.IsOpen, Is.True, "The conversation window is open.");
            Assert.That(c.Dialogue.Root.Q<Label>("xk-dialogue-name").text, Does.Contain(c.Dialogue.View.Name));

            // 3. Select a response: ask about the place; the answer is theirs.
            QuestDriver.Say(c, "ask");
            yield return null;
            QuestDriver.Say(c, "ask_settlement");
            yield return null;
            Assert.That(c.Dialogue.View.NpcLine, Is.Not.Empty);
            QuestDriver.Say(c, "back");
            yield return null;

            // 4. Accept the quest from the giver's trouble.
            var id = QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return null;
            Assert.That(c.Dialogue.Root.Q<Label>("xk-dialogue-quest").text, Does.Contain("Short Measures"));
            QuestDriver.Say(c, "accept");
            yield return null;
            QuestDriver.Say(c, "ok");
            yield return null;
            Assert.That(c.Dialogue.IsOpen, Is.False, "The conversation ends.");
            Assert.That(c.LastQuestNotice, Does.Contain("New quest"));

            // 6–7. The journal: the quest is active; open it, track it.
            c.ToggleHub();
            c.HubWindow.SelectJournal("Active");
            yield return null;
            var row = c.HubWindow.Content.Q<VisualElement>("xk-journal-quest-" + id);
            Assert.That(row, Is.Not.Null, "Listed under Active.");
            ItemDriver.ClickText(row, "Open");
            yield return null;
            var detail = c.HubWindow.Content.Q<VisualElement>("xk-journal-detail");
            Assert.That(detail, Is.Not.Null);
            Assert.That(detail.Q<Label>("xk-journal-objective-suspect"), Is.Null, "Hidden objectives stay hidden.");
            LevelUpDriver.Click(detail, "xk-journal-track"); // tracked by default: untrack…
            yield return null;
            LevelUpDriver.Click(c.HubWindow.Content.Q<VisualElement>("xk-journal-detail"), "xk-journal-track"); // …and track again
            yield return null;
            Assert.That(s.GetTrackedQuests().Any(q => q.InstanceId == id), Is.True);
            Assert.That(c.Hud.Root.Q<Label>("xk-tracked").ClassListContains("xk-hidden"), Is.True, "The tracked line stays out of the way while a window is open.");
            c.CloseWindows();
            yield return null;
            Assert.That(c.Hud.Root.Q<Label>("xk-tracked").text, Does.Contain("Short Measures"), "The HUD shows the tracked quest once the window closes.");
            Assert.That(c.Hud.Root.Q<Label>("xk-tracked").ClassListContains("xk-hidden"), Is.False);

            // 8–9. Examine the ledger in the street: the objective completes; the board shows notices.
            var ledger = c.Interactables.First(i => i.InteractableKind == InteractableKind.GranaryLedger);
            var answer = ledger.Interact(s, c.Player.transform);
            Assert.That(answer.Text, Does.Contain("tally"), "The ledger shows the shortfall.");
            c.AfterQuestCommand();
            Assert.That(s.GetQuest(id).Objectives.Single(o => o.Id == "ledger").Done, Is.True);
            var board = c.Interactables.First(i => i.InteractableKind == InteractableKind.LabourNoticeBoard);
            Assert.That(board.Interact(s, c.Player.transform).Command.IsSuccess, Is.True);

            // The keeper, then the suspect.
            c.StartDialogue(QuestDriver.Role(s, id, "keeper"));
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return null;
            QuestDriver.Say(c, "thanks");
            yield return null;
            c.StartDialogue(QuestDriver.Role(s, id, "suspect"));
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return null;
            QuestDriver.Say(c, "press");
            yield return null;
            c.EndDialogue();

            // 10. A delivery: four sacks of grain to the keeper (given to the player through a saved file).
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 600, ("grain_sack", 4, null));
            c.StartDialogue(QuestDriver.Role(s, id, "keeper"));
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return null;
            QuestDriver.Say(c, "sacks");
            yield return null;
            Assert.That(s.GetQuest(id).Objectives.Single(o => o.Id == "replaced").Done, Is.True, c.Dialogue.LastMessage);

            // 12, 14, 15. Report: experience and coin; the giver's regard rises; they greet the player differently after.
            var xp = s.GetCharacter().Player.Rpg.Experience;
            var purse = s.GetCharacter().Player.Purse;
            c.StartDialogue(giver);
            yield return null;
            var before = c.Dialogue.View.NpcLine;
            QuestDriver.Hook(c, QuestCatalog.GranaryShortfall);
            yield return null;
            QuestDriver.Say(c, "report_replaced");
            yield return null;
            Assert.That(c.LastQuestNotice, Does.Contain("Quest completed"));
            Assert.That(s.GetCharacter().Player.Rpg.Experience, Is.EqualTo(xp + 120));
            Assert.That(s.GetCharacter().Player.Purse, Is.EqualTo(purse + 70));
            c.ToggleHub();
            c.HubWindow.SelectJournal("Reputation");
            yield return null;
            Assert.That(c.HubWindow.Content.Q<VisualElement>("xk-journal-reputation"), Is.Not.Null);
            Assert.That(ItemDriver.Texts(c.HubWindow.Content), Does.Contain("gratitude 2"), "The giver's gratitude shows.");
            c.CloseWindows();
            c.StartDialogue(giver);
            yield return null;
            Assert.That(c.Dialogue.View.NpcLine, Is.Not.EqualTo(before), "The giver reacts to what the player did.");
            Assert.That(c.Dialogue.View.Mood, Does.Contain("grateful"));
            c.EndDialogue();

            // 16–17. Save and reload: the quest, the rewards, the regard and the Chronicle are all still there.
            Assert.That(c.SaveNow().IsSuccess, Is.True);
            Assert.That(s.Execute(new LoadWorldCommand(Runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            Assert.That(QuestDriver.Find(s, QuestCatalog.GranaryShortfall, j => j.Completed), Is.Not.Null);
            Assert.That(s.GetCharacter().Player.Rpg.Experience, Is.EqualTo(xp + 120));
            Assert.That(s.GetJournal().Events.Any(e => e.Text.Contains("completed")), Is.True, "The Chronicle remembers.");
        }

        [UnityTest]
        public IEnumerator UnderSeal_IsDeclined_ThenTaken_AndTheLetterArrivesAsAnItem()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Morning(c, s);
            var letter = s.FindQuestInstance(QuestCatalog.SealedLetter);
            Assume.That(letter, Is.Not.Null);
            var giver = QuestDriver.Role(s, letter, "giver");

            // 5. Decline.
            c.StartDialogue(giver);
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.SealedLetter);
            yield return null;
            QuestDriver.Say(c, "decline");
            yield return null;
            Assert.That(QuestDriver.Find(s, QuestCatalog.SealedLetter, j => j.Available), Is.Not.Null, "Declined: known, not taken.");
            Assert.That(s.GetJournal().Decisions.Any(d => d.Text.Contains("Declined")), Is.True);

            // 13. Take it after all: the letter is now an item in the pack.
            c.StartDialogue(giver);
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.SealedLetter);
            yield return null;
            QuestDriver.Say(c, "accept");
            yield return null;
            Assert.That(s.GetInventory().Rows.Any(r => r.ItemId == "sealed_letter"), Is.True);
            Assert.That(QuestDriver.Find(s, QuestCatalog.SealedLetter, j => j.Active), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AHunt_IsProgressed_ByAFightTheQuestStarts()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Morning(c, s);
            var hunt = s.FindQuestInstance(QuestCatalog.WolvesAtTheFolds);
            Assume.That(hunt, Is.Not.Null, "No predators are in reach of this town's herds, so no hunt was asked for (the world decides).");
            var giver = QuestDriver.Role(s, hunt, "giver");
            Assume.That(s.HomeOf(giver) == s.GetCharacter().Player.SettlementId, "The hunt is asked for in another settlement.");
            c.AutoAi = false;
            c.StartDialogue(giver);
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.WolvesAtTheFolds);
            yield return null;
            QuestDriver.Say(c, "accept");
            yield return null;

            // 11. "Show me where": the outing seeks the quarry where it lives; the fight is played through the panel.
            c.StartDialogue(giver);
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.WolvesAtTheFolds);
            yield return null;
            QuestDriver.Say(c, "go");
            yield return null;
            Assert.That(c.InFight, Is.True, "The quest started the outing.");
            var w = c.Combat;
            for (var i = 0; i < 240 && c.InFight; i++)
            {
                if (w.Prompt != null) LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
                else CombatDriver.Step(c);
                yield return null;
            }

            var slain = w.Snapshot.Order.Count(x => x.Kind == CombatantKind.Creature && x.Name.StartsWith("Wolf", StringComparison.Ordinal) && x.State == VitalState.Dead);
            var progress = s.GetQuest(hunt).Objectives.Single(o => o.Id == "hunt");
            Assert.That(progress.Progress, Is.EqualTo(Math.Min(2, slain) + " / 2"), "Every wolf slain near the town counts; nothing else does.");
        }
    }
}
