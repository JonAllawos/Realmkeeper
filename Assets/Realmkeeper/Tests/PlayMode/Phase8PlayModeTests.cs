using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
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
    /// <summary>Shared steps for driving the encounter panel in the shipped scene (Phase 8 tests and captures).</summary>
    internal static class CombatDriver
    {
        public static IEnumerator EnterTownAsFighter(RealmkeeperBootstrap bootstrap, RealmkeeperRuntime runtime, ExplorationController controller, string cls = "fighter")
        {
            var session = bootstrap.Session;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlements().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            runtime.Playback.Pause();
            var created = session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices(cls)));
            Assert.That(created.IsSuccess, Is.True, created.Message);
            yield return null;
        }

        /// <summary>The index of the first enabled option button whose label starts with the text, or -1.</summary>
        public static int Option(ExplorationController controller, System.Func<CombatOption, bool> which)
        {
            var s = controller.Combat.Snapshot;
            for (var i = 0; i < s.PlayerOptions.Count; i++)
                if (s.PlayerOptions[i].Unavailable == null && which(s.PlayerOptions[i])) return i;
            return -1;
        }

        /// <summary>
        /// One step of play through the panel, as a player would click it: on the player's turn attack with the weapon
        /// if an enemy is in reach, else step towards the nearest enemy on the grid, else end the turn; on a
        /// computer-controlled turn, press Continue.
        /// </summary>
        public static void Step(ExplorationController controller)
        {
            var w = controller.Combat;
            var s = w.Snapshot;
            if (!s.IsPlayersTurn)
            {
                LevelUpDriver.Click(w.Root, "xk-combat-continue");
                return;
            }

            var attack = Option(controller, o => o.Action is AttackAction a && a.OptionId == "weapon");
            if (attack >= 0)
            {
                LevelUpDriver.Click(w.Root, "xk-combat-act-" + attack);
                return;
            }

            if (s.Player.MovementLeft >= 5)
            {
                var enemy = s.Order.Where(c => c.HostileToPlayer && c.State == VitalState.Conscious).OrderBy(c => c.Position.DistanceFeet(s.Player.Position)).FirstOrDefault();
                if (enemy != null && !enemy.Position.IsAdjacent(s.Player.Position))
                {
                    var step = s.Player.Position.Step(enemy.Position.X - s.Player.Position.X, enemy.Position.Y - s.Player.Position.Y);
                    var cell = w.Root.Q<Button>($"xk-combat-cell-{step.X}-{step.Y}");
                    if (cell != null && cell.ClassListContains("xk-combat-cell--reachable"))
                    {
                        LevelUpDriver.Click(w.Root, cell.name);
                        return;
                    }
                }
            }

            LevelUpDriver.Click(w.Root, "xk-combat-act-" + (s.PlayerOptions.Count - 1)); // End turn
        }
    }

    /// <summary>
    /// Phase 8 in the shipped scene: the practice fight started from the HUD and played to its end through the encounter
    /// panel's own buttons (attacks, moves on the grid, End turn, Continue for the AI), every unavailable action showing
    /// why; time stands still; the fight survives a save and load mid-way; the settlement inspector lists structures;
    /// the Hub shows the credits.
    /// </summary>
    public sealed class Phase8PlayModeTests
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

        [UnityTest]
        public IEnumerator PracticeFight_PlaysThroughThePanel_ToItsEnd()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(bootstrap, runtime, controller);
            controller.AutoAi = false;
            var hud = controller.Hud.Root;
            var practice = hud.Q<Button>("xk-open-practice");
            Assert.That(practice, Is.Not.Null);
            Assert.That(practice.ClassListContains("xk-hidden"), Is.False, "Offered once the character exists.");
            var events = session.GetRecentEvents(100000).Count;

            // Start from the HUD button.
            LevelUpDriver.Click(hud, "xk-open-practice");
            yield return null;
            var w = controller.Combat;
            Assert.That(w.IsOpen && controller.InFight, Is.True, "The encounter panel opens.");
            Assert.That(controller.Player.MovementEnabled, Is.False, "The character stands still while fighting.");
            Assert.That(practice.ClassListContains("xk-hidden"), Is.True);
            var s = w.Snapshot;
            Assert.That(w.Root.Q<VisualElement>("xk-combat-order").childCount, Is.EqualTo(s.Order.Count), "Every combatant in the initiative order.");
            Assert.That(w.Root.Q<Label>("xk-combat-title").text, Does.StartWith("Practice"));
            Assert.That(w.Root.Query<Button>(className: "xk-combat-cell").ToList().Count, Is.EqualTo(CombatWindow.GridColumns * CombatWindow.GridRows));
            Assert.That(controller.CombatMarkers, Is.Not.Null, "Ground markers stand on the 5-ft grid.");
            Assert.That(controller.CombatMarkers.transform.childCount, Is.EqualTo(s.Order.Count));
            Assert.That(controller.WaitHour().IsSuccess, Is.False, "Time stands still.");
            Assert.That(controller.ReturnToWorldMap().IsSuccess, Is.False, "No leaving mid-fight.");

            // The first player turn: a far enemy's attack is disabled and says why.
            for (var guard = 0; guard < 12 && !w.Snapshot.IsPlayersTurn && controller.InFight; guard++)
            {
                LevelUpDriver.Click(w.Root, "xk-combat-continue");
                yield return null;
            }

            if (controller.InFight)
            {
                Assert.That(w.Root.Q<Label>("xk-combat-turn").text, Is.EqualTo("Your turn"));
                var far = w.Snapshot.PlayerOptions.FirstOrDefault(o => o.Action is AttackAction a && a.OptionId == "weapon" && o.Unavailable != null);
                if (far != null)
                {
                    Assert.That(far.Unavailable, Does.Contain("ft away"));
                    Assert.That(w.Root.Query<Label>(className: "xk-combat-why").ToList().Select(l => l.text), Has.Member(far.Unavailable), "The reason is on screen.");
                }

                // Inspect an enemy by clicking its row.
                var enemyIndex = w.Snapshot.Order.ToList().FindIndex(c => c.HostileToPlayer);
                LevelUpDriver.Click(w.Root, "xk-combat-order-" + enemyIndex);
                Assert.That(w.InspectorText, Does.Contain("Armour class 12"), "The bandit's AC is explained.");
            }

            // Play to the end through the panel.
            for (var step = 0; step < 400 && controller.InFight; step++)
            {
                CombatDriver.Step(controller);
                yield return null;
            }

            Assert.That(controller.InFight, Is.False, "The fight ended.");
            Assert.That(w.IsOpen, Is.True, "The result stays on screen until closed.");
            var end = w.Snapshot;
            Assert.That(end.Status, Is.EqualTo(EncounterStatus.Ended));
            Assert.That(w.Root.Q<Label>("xk-combat-turn").text, Is.EqualTo("The fight is over"));
            Assert.That(w.Root.Q<ScrollView>("xk-combat-log").contentContainer.childCount, Is.GreaterThan(5));
            Assert.That(w.ResultText, Is.Not.Empty);
            var all = session.GetRecentEvents(100000);
            var combatEvents = all.Where(e => e.Data.TryGetValue("source", out var src) && src == "combat").ToList();
            Assert.That(combatEvents.Count(e => e.EventType == HistoricalEventType.EncounterStarted), Is.EqualTo(1));
            Assert.That(combatEvents.Count(e => e.EventType == HistoricalEventType.EncounterEnded), Is.EqualTo(1));
            Assert.That(all.Count - events, Is.LessThan(end.Log.Count), "A few chronicle entries, not one per swing.");

            LevelUpDriver.Click(w.Root, "xk-combat-close");
            yield return null;
            Assert.That(w.IsOpen, Is.False);
            Assert.That(controller.CombatMarkers, Is.Null);
            if (!controller.PlayerIsDead)
            {
                Assert.That(controller.Player.MovementEnabled, Is.True, "Free to walk again.");
                Assert.That(controller.WaitHour().IsSuccess, Is.True, "Time moves again.");
                Assert.That(practice.ClassListContains("xk-hidden"), Is.False);
            }
            else
            {
                Assert.That(controller.Player.MovementEnabled, Is.False, "The dead do not walk.");
                Assert.That(practice.ClassListContains("xk-hidden"), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator AFightInProgress_SurvivesSaveAndLoad_AndThePanelReturns()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(bootstrap, runtime, controller);
            controller.AutoAi = false;
            Assert.That(controller.StartPracticeFight().IsSuccess, Is.True);
            yield return null;
            for (var i = 0; i < 3 && controller.InFight; i++)
            {
                CombatDriver.Step(controller);
                yield return null;
            }

            Assume.That(controller.InFight, Is.True);
            var before = controller.Combat.Snapshot;
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(controller.InFight, Is.True, "The loaded fight reopens its panel.");
            var after = controller.Combat.Snapshot;
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.Round, Is.EqualTo(before.Round));
            Assert.That(after.Sequence, Is.EqualTo(before.Sequence));
            Assert.That(after.Order.Select(c => c.Name + c.Hp + c.Position), Is.EqualTo(before.Order.Select(c => c.Name + c.Hp + c.Position)));
            CombatDriver.Step(controller);
            yield return null;
            Assert.That(controller.Combat.Snapshot.Sequence, Is.GreaterThan(before.Sequence), "It plays on.");
        }

        [UnityTest]
        public IEnumerator Withdraw_IsRefusedNextToAnEnemy_AndEndsTheFightOtherwise()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(bootstrap, runtime, controller);
            controller.AutoAi = false;
            Assert.That(controller.StartPracticeFight().IsSuccess, Is.True);
            yield return null;
            var w = controller.Combat;
            var p = w.Snapshot.Player;
            var adjacent = w.Snapshot.Order.Any(c => c.HostileToPlayer && c.State == VitalState.Conscious && c.Position.IsAdjacent(p.Position));
            LevelUpDriver.Click(w.Root, "xk-combat-withdraw");
            yield return null;
            if (adjacent)
            {
                Assert.That(controller.InFight, Is.True);
                Assert.That(w.ResultText, Does.Contain("next to you"));
            }
            else
            {
                Assert.That(controller.InFight, Is.False);
                Assert.That(w.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Withdrawn));
            }
        }

        [UnityTest]
        public IEnumerator Structures_AreListedInTheInspector_SignsStandInTheStreet_AndTheHubShowsTheCredits()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            var town = session.GetSettlements().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            yield return null;
            var box = bootstrap.Hud.SettlementStructures;
            Assert.That(box, Is.Not.Null);
            var structures = session.GetStructures(town.SettlementId);
            Assert.That(box.childCount, Is.EqualTo(structures.Count + 1), "A summary line and one line per structure.");
            Assert.That(box.Children().Select(c => ((Label)c).text), Has.Some.Contains("Functional"));
            Assert.That(box.Children().Select(c => ((Label)c).text), Has.Some.Contains("Informational"));
            Assert.That(box.Children().Select(c => ((Label)c).text), Has.Some.Contains("Decorative"));

            yield return CombatDriver.EnterTownAsFighter(bootstrap, runtime, controller);
            Assert.That(controller.StructureSigns.Count, Is.GreaterThan(0), "Signs name the buildings.");
            Assert.That(controller.StructureSigns.Any(s => s.GetComponent<TextMesh>().text.Contains(structures.First(x => x.Kind == "Smithy").Name)), Is.True);

            controller.ToggleHub();
            yield return null;
            controller.HubWindow.Select("Info");
            yield return null;
            var credits = controller.HubWindow.Content.Q<VisualElement>("xk-hub-credits");
            Assert.That(credits, Is.Not.Null, "The Hub's Info section shows the credits.");
            var text = string.Join(" ", credits.Query<Label>().ToList().Select(l => l.text));
            Assert.That(text, Does.Contain("System Reference Document 5.2.1").And.Contain("Creative Commons Attribution 4.0").And.Contain("Quaternius").And.Contain("ambientCG"));
        }
    }
}
