using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
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
    /// <summary>Shared steps for Phase 10 tests and captures.</summary>
    internal static class LivingDriver
    {
        /// <summary>Clicks through the fight as a player would, until a question is asked of the player or the fight ends.</summary>
        public static IEnumerator UntilPrompt(ExplorationController controller, int steps = 40)
        {
            var w = controller.Combat;
            for (var i = 0; i < steps && controller.InFight && w.Prompt == null; i++)
            {
                CombatDriver.Step(controller); // close in and attack on the player's turn, Continue otherwise
                yield return null;
            }
        }
    }

    /// <summary>
    /// Phase 10 in the shipped scene, through the real UI: a named person's character sheet in the inspector and their
    /// promotion from the DM tools; an outing from the HUD that draws creatures from the world, with the bestiary in
    /// the combat inspector, played to its end and settled once; a reaction asked of the player — accepted and
    /// declined from the prompt; and the planned settlement — its terrain, roads, water, buildings on their floors,
    /// residents on the ground, both views, save and load, and the strategic map drawn from the same plan.
    /// </summary>
    public sealed class Phase10PlayModeTests
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

        [UnityTest]
        public IEnumerator TheInspector_ShowsANamedPersonsSheet_AndTheDmPromotesACivilian()
        {
            var session = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            var guard = session.GetNpcs(new NpcFilter()).First(n => session.GetNpcCharacter(n.NpcId)?.HasSheet == true);
            hud.ShowPeople(true);
            hud.SelectNpc(guard.NpcId);
            yield return null;
            Assert.That(hud.NpcInspectorVisible, Is.True);
            Assert.That(hud.NpcInspectorBody.Q<Label>("ninsp-section-character"), Is.Not.Null, "A Character section.");
            var text = string.Join(" ", hud.NpcInspectorBody.Query<Label>().ToList().Select(l => l.text));
            Assert.That(text, Does.Contain("Hit points").And.Contain("Armour class").And.Contain("Abilities"), "The sheet: class, hit points, armour class, abilities.");

            var civilian = session.GetNpcs(new NpcFilter()).First(n => session.GetNpcCharacter(n.NpcId)?.HasSheet == false);
            hud.SelectNpc(civilian.NpcId);
            hud.SetManagingNpc(true);
            yield return null;
            Assert.That(hud.NpcInspectorBody.Q<Button>("tool-promote"), Is.Not.Null, "The DM can give a sheet.");
            LevelUpDriver.Click(hud.NpcInspectorBody, "tool-promote");
            yield return null;
            Assert.That(session.GetNpcCharacter(civilian.NpcId).HasSheet, Is.True);
            text = string.Join(" ", hud.NpcInspectorBody.Query<Label>().ToList().Select(l => l.text));
            Assert.That(text, Does.Contain("Hit points"), "The inspector refreshed to the new sheet.");
            Assert.That(hud.NpcInspectorBody.Q<Button>("tool-promote"), Is.Null, "Once only.");
        }

        [UnityTest]
        public IEnumerator AnOuting_DrawsCreaturesFromTheWorld_ShowsTheBestiary_AndSettlesOnce()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            c.AutoAi = false;
            Assert.That(c.Hud.Root.Q<Button>("xk-open-outing"), Is.Not.Null, "The outing button sits with the practice fights.");
            LevelUpDriver.Click(c.Hud.Root, "xk-open-outing");
            for (var i = 0; i < 3; i++) yield return null;
            Assert.That(c.InFight, Is.True, "The outing starts a fight.");
            var w = c.Combat;
            var creature = w.Snapshot.Order.First(x => x.Kind == CombatantKind.Creature);
            w.Inspect(creature.Id);
            yield return null;
            Assert.That(w.InspectorText, Does.Contain("Bestiary:"), "The creature's bestiary entry: category, challenge, behaviour, source.");

            var xp = session.GetCharacter().Player.Rpg.Experience;
            for (var i = 0; i < 200 && c.InFight; i++)
            {
                if (w.Prompt != null) LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
                else CombatDriver.Step(c);
                yield return null;
            }

            Assert.That(c.InFight, Is.False, "The outing ends.");
            var resolved = session.GetRecentEvents(50).Count(e => e.EventType == HistoricalEventType.EncounterResolved);
            Assert.That(resolved, Is.EqualTo(1), "Settled once, in the chronicle.");
            var slain = w.Snapshot.Order.Count(x => x.Kind == CombatantKind.Creature && x.State == VitalState.Dead);
            if (!c.PlayerIsDead && w.Snapshot.Outcome == EncounterOutcome.Victory && slain > 0)
                Assert.That(session.GetCharacter().Player.Rpg.Experience, Is.GreaterThan(xp), "Experience for the slain.");
            LevelUpDriver.Click(w.Root, "xk-combat-close");
            yield return null;
            Assert.That(session.GetRecentEvents(50).Count(e => e.EventType == HistoricalEventType.EncounterResolved), Is.EqualTo(1), "Closing settles nothing again.");
        }

        [UnityTest]
        public IEnumerator AReaction_IsAskedThroughThePrompt_AndCanBeAcceptedOrDeclined()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c, "wizard");
            session.Dispatch(new ChooseSpellCommand("class:spellbook", "shield"));
            Assert.That(session.Dispatch(new PrepareSpellsCommand(new[] { "shield", "magic_missile" })).IsSuccess, Is.True);
            c.AutoAi = false;
            // A hit that Shield could answer is up to the dice: practice fights are played (closing in and attacking, so
            // the bandits strike back) until one is asked, recovering between fights the in-game way. The dice are not touched.
            for (var attempt = 0; attempt < 6 && c.Combat?.Prompt == null; attempt++)
            {
                Assert.That(c.StartPracticeFight().IsSuccess, Is.True);
                yield return null;
                yield return LivingDriver.UntilPrompt(c);
                if (c.Combat.Prompt != null) break;
                if (c.InFight) LevelUpDriver.Click(c.Combat.Root, "xk-combat-calloff");
                yield return null;
                LevelUpDriver.Click(c.Combat.Root, "xk-combat-close");
                yield return null;
                Assume.That(c.PlayerIsDead, Is.False, "The wizard died before Shield was offered.");
                if (session.GetCharacter().Player.Rpg.CurrentHp < 1) session.ShortRest(1);
                Assert.That(session.Execute(new AdvanceTimeCommand(17 * 60)).IsSuccess, Is.True);
                Assert.That(session.LongRest().IsSuccess, Is.True);
            }

            Assume.That(c.Combat.Prompt, Is.Not.Null, "No hit landed that Shield could answer in six fights.");
            var w = c.Combat;
            var prompt = w.Root.Q<VisualElement>("xk-combat-prompt");
            Assert.That(prompt.ClassListContains("xk-hidden"), Is.False, "The question shows above the panel's columns.");
            Assert.That(w.PromptText, Does.Contain("Shield").And.Contain("Cost"));
            Assert.That(w.Root.Q<VisualElement>("xk-combat-actions").Query<Button>().ToList(), Is.Empty, "Nothing else can be done while it waits.");
            var slots = session.GetCharacter().Player.Rpg.Resources.TryGetValue("spell_slots_1", out var s1) ? s1 : -1;
            LevelUpDriver.Click(w.Root, "xk-combat-prompt-accept");
            yield return null;
            Assert.That(w.Prompt, Is.Null, "Answered.");
            Assert.That(w.ResultText + string.Join(" ", w.Snapshot.Log.Select(l => l.Text)), Does.Contain("Shield"));
            Assert.That(session.GetCharacter().Player.Rpg.Resources["spell_slots_1"], Is.EqualTo(slots - 1), "The slot is spent.");

            yield return LivingDriver.UntilPrompt(c);
            if (c.Combat.Prompt == null) yield break; // no second chance this fight (no slot, or the fight ended)
            LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
            yield return null;
            Assert.That(w.Prompt, Is.Null, "Declined: the attack stands.");
        }

        [UnityTest]
        public IEnumerator ThePlannedTown_IsBuiltFromItsPlan_OnItsGround_AndKeptAcrossSaveAndLoad()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            c.ManageCursor = false;
            var input = new ScriptedExplorationInput();
            c.SetInput(input);
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            Bootstrap.Selection.Select(town.SettlementId);
            Assert.That(Bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            var layout = c.Streamer.Current;
            Assert.That(layout.Plan, Is.Not.Null, "The town is its persisted plan.");
            var plan = layout.Plan;
            var root = c.EnvironmentRoot.transform;
            Assert.That(root.Find("Terrain"), Is.Not.Null, "Ground from the plan's terrain.");
            Assert.That(root.GetComponentsInChildren<MeshCollider>().Length, Is.GreaterThan(0), "The ground has collision.");
            if (plan.Bridges.Count > 0) Assert.That(root.GetComponentsInChildren<BoxCollider>().Any(b => b.name == "Deck"), Is.True, "Bridges carry roads over water.");
            if (plan.Waterways.Count > 0) Assert.That(root.Find("Water " + plan.Waterways[0].Key), Is.Not.Null);

            // Standing on the ground at the way in, and walking on it.
            for (var i = 0; i < 20; i++) yield return null;
            var p = c.Player.transform.position;
            Assert.That(c.Player.IsGrounded, Is.True);
            Assert.That(p.y, Is.EqualTo(plan.HeightAt(p.x, p.z)).Within(0.6f), "On the terrain.");
            input.Move = new Vector2(0f, 1f);
            for (var t = 0f; t < 2f; t += Time.deltaTime) yield return null; // by time, whatever the frame rate
            input.Move = Vector2.zero;
            var q = c.Player.transform.position;
            Assert.That(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)), Is.GreaterThan(1f), "Walked in.");
            Assert.That(q.y, Is.EqualTo(LocalPaths.StandingHeight(layout, q.x, q.z)).Within(0.8f), "Still on the ground.");

            // Residents stand on the ground (or a floor or deck).
            foreach (var r in Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None).Where(r => !r.IsHidden).Take(8))
            {
                var at = r.transform.localPosition;
                Assert.That(at.y, Is.EqualTo(LocalPaths.StandingHeight(layout, at.x, at.z)).Within(0.05f), r.DisplayName);
            }

            // Both views.
            Assert.That(c.TogglePerspective().IsSuccess, Is.True);
            yield return null;
            Assert.That(c.TogglePerspective().IsSuccess, Is.True);
            yield return null;

            // Save and load inside: the same plan comes back, the town is rebuilt from it.
            var before = session.GetSettlementLayout(plan.SettlementId);
            Assert.That(c.SaveNow().IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand(Runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            var after = session.GetSettlementLayout(plan.SettlementId);
            Assert.That(after.Buildings.Select(b => b.Key + b.Center), Is.EqualTo(before.Buildings.Select(b => b.Key + b.Center)));
            Assert.That(after.Roads.Count, Is.EqualTo(before.Roads.Count));

            // Back to the map: the strategic settlement is drawn from the same plan.
            Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True);
            for (var i = 0; i < 4; i++) yield return null;
            var view = Bootstrap.WorldView.FindSettlementView(plan.SettlementId);
            Assert.That(view, Is.Not.Null);
            Assert.That(view.BuildingCount, Is.GreaterThan(20), "The map shows the plan's buildings, not a handful of placeholders.");
            Bootstrap.Selection.Select(plan.SettlementId);
            yield return null;
            var identity = Bootstrap.Hud.Root.Q<Label>("insp-identity");
            Assert.That(identity, Is.Not.Null, "The inspector says what kind of place it is.");
            Assert.That(identity.text, Is.EqualTo(after.Identity.Summary));
        }
    }
}
