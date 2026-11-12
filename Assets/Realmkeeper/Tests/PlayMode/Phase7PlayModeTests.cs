using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Shared steps for driving the level-up window in the shipped scene (Phase 7 tests and captures).</summary>
    internal static class LevelUpDriver
    {
        public static void Click(VisualElement root, string name)
        {
            var b = root.Q<Button>(name);
            Assert.That(b, Is.Not.Null, "No button " + name);
            Assert.That(b.enabledSelf, Is.True, name + " is disabled");
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = b;
                b.SendEvent(click);
            }
        }

        /// <summary>A valid level-1 character of the class (first options everywhere).</summary>
        public static CharacterCreationChoices Choices(string classId, string species = "human")
        {
            var cls = RpgCatalog.FindClass(classId);
            var sp = RpgCatalog.FindSpecies(species);
            var d = new CharacterDraft();
            d.Choices.Name = "Tamsin Vale";
            d.SetSpecies(species);
            d.SetBackground("outlander");
            d.SetClass(classId);
            foreach (var c in sp.Choices.Concat(cls.Choices).Where(c => c.Kind == ChoiceKind.Option)) d.Toggle(c.Id, c.Options[0].Id);
            foreach (var s in cls.SkillOptions) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            foreach (var c in sp.Choices.Where(c => c.Kind == ChoiceKind.Skill)) foreach (var s in SkillRules.All) d.Toggle(c.Id, s.ToString());
            foreach (var c in cls.Choices.Where(c => c.Kind == ChoiceKind.Expertise)) foreach (var s in SkillRules.All) d.Toggle(c.Id, s.ToString());
            Assert.That(d.Problems(CreationStep.Review), Is.Empty, classId);
            return d.Clean();
        }

        /// <summary>Rewrites a current save of a created character to the Phase 6 (schema v8) layout, as Phase 6 would have written it.</summary>
        public static void RewriteAsPhaseSix(string path)
        {
            var doc = JObject.Parse(File.ReadAllText(path));
            doc["schemaVersion"] = 8;
            var rpg = (JObject)doc["world"]["player"]["rpg"];
            rpg.Remove("rulesVersion");
            rpg.Remove("experienceLog");
            rpg.Remove("advancements");
            var resources = (JObject)rpg["resources"];
            resources.Remove("hit_dice");
            if (resources["pact_slots"] != null)
            {
                resources["spell_slots_1"] = resources["pact_slots"];
                resources.Remove("pact_slots");
            }

            var choices = (JObject)rpg["choices"];
            choices.Remove("class:primal_order"); // level-1 choices Phase 6 did not have
            choices.Remove("class:invocations");
            File.WriteAllText(path, doc.ToString());
        }

        private static string Id(ChoiceDefinition c) => c.Id.Replace(':', '-');

        /// <summary>
        /// Takes the open level-up window through one level by clicking: Next through every page, the suggested pick
        /// on each choice page (the subclass, feat and its points, skills, invocations …), then Confirm.
        /// <paramref name="onPage"/> runs after each page's picks (captures).
        /// </summary>
        public static IEnumerator ThroughOneLevel(ExplorationController controller, WorldSession session, Func<AdvancementPage, IEnumerator> onPage = null)
        {
            var w = controller.Advancement;
            Assert.That(w.IsOpen, Is.True, "The level-up window is open.");
            var root = w.Root;
            var level = w.Draft.Level;
            var plan = CharacterAdvancement.SuggestChoices(session.GetCharacter().Player.Rpg, level);
            Assert.That(w.Draft.Page.IsOverview, Is.True);
            Assert.That(root.Q<VisualElement>("xk-lv-cards"), Is.Not.Null, "The overview shows what the level brings.");
            if (onPage != null) yield return onPage(w.Draft.Page);
            for (var guard = 0; guard < 40 && !w.Draft.Page.IsReview; guard++)
            {
                Click(root, "xk-lv-next");
                var page = w.Draft.Page;
                if (page.Choice == null) continue;
                var c = page.Choice.Value.Choice;
                if (c.Kind == ChoiceKind.Subclass) Click(root, $"xk-lv-pick-{Id(c)}-{plan.SubclassId}");
                else if (c.Kind == ChoiceKind.AbilityIncrease) foreach (var a in plan.Choices[c.Id]) Click(root, "xk-lv-plus-" + a);
                else foreach (var v in plan.Choices[c.Id]) Click(root, $"xk-lv-pick-{Id(c)}-{v}");
                Assert.That(w.Draft.PageProblems(w.Draft.Page), Is.Empty, $"Level {level} {c.Name}: {w.Problems}");
                if (onPage != null) yield return onPage(w.Draft.Page);
            }

            Assert.That(w.Draft.Page.IsReview, Is.True);
            Assert.That(w.Draft.Problems(), Is.Empty, string.Join("; ", w.Draft.Problems()));
            if (onPage != null) yield return onPage(w.Draft.Page);
            Click(root, "xk-lv-confirm");
            yield return null;
            Assert.That(session.GetCharacter().Level, Is.EqualTo(level), "Confirming applied the level.");
        }
    }

    /// <summary>
    /// Phase 7 in the shipped scene: a Phase 6 character (a save rewritten to schema v8) earns experience through the
    /// application command, sees the HUD and Hub offer the level-up, goes through the real level-up window (every page
    /// clicked, choices made with its buttons), confirms, and the sheet, save and reload hold — for six classes with
    /// different progressions.
    /// </summary>
    public sealed class Phase7PlayModeTests
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

        [UnityTest] public IEnumerator Fighter_ToLevel5() => Run("fighter", 5, c =>
        {
            Assert.That(c.Rules.HasFeature("extra_attack") && c.Rules.HasFeature("action_surge"), Is.True);
            Assert.That(c.Rules.Subclass.Id, Is.EqualTo("champion"));
            Assert.That(c.Rules.InitiativeRoll.HasAdvantage, Is.True, "Remarkable Athlete.");
            Assert.That(c.Rules.ResourceMax(c.Rules.FindResource("second_wind")), Is.EqualTo(3));
            Assert.That(c.Player.Rpg.Chosen("lv4:feat"), Has.Count.EqualTo(1));
        });

        [UnityTest] public IEnumerator Rogue_ToLevel6() => Run("rogue", 6, c =>
        {
            Assert.That(c.Rules.Scaling("sneak_attack"), Is.EqualTo(3));
            Assert.That(c.Player.Rpg.Chosen("lv6:expertise"), Has.Count.EqualTo(2));
            Assert.That(c.Rules.Subclass.Id, Is.EqualTo("thief"));
            Assert.That(c.Rules.MovementModes, Has.Some.StartsWith("climb"));
        });

        [UnityTest] public IEnumerator Cleric_ToLevel3() => Run("cleric", 3, c =>
        {
            Assert.That(c.Rules.Subclass.Id, Is.EqualTo("life"));
            Assert.That(c.Player.Rpg.Resources["channel_divinity"], Is.EqualTo(2));
            Assert.That(c.Player.Rpg.Resources["spell_slots_2"], Is.EqualTo(2));
        });

        [UnityTest] public IEnumerator Wizard_ToLevel4() => Run("wizard", 4, c =>
        {
            Assert.That(c.Rules.Subclass.Id, Is.EqualTo("evoker"));
            Assert.That(c.Player.Rpg.Chosen("lv2:scholar"), Has.Count.EqualTo(1));
            Assert.That(c.Rules.ScoreBreakdown(Ability.Intelligence).Text, Does.Contain("Ability Score Improvement"));
        });

        [UnityTest] public IEnumerator Barbarian_ToLevel5() => Run("barbarian", 5, c =>
        {
            Assert.That(c.Rules.Speed, Is.EqualTo(40), "Fast Movement.");
            Assert.That(c.Player.Rpg.Resources["rage"], Is.EqualTo(3));
            Assert.That(c.Rules.Subclass.Id, Is.EqualTo("berserker"));
            Assert.That(c.Player.Rpg.Chosen("lv3:primal_knowledge"), Has.Count.EqualTo(1));
        });

        [UnityTest] public IEnumerator Warlock_ToLevel5_MakingItsOpenLevelOneInvocation() => Run("warlock", 5, c =>
        {
            var invocations = c.Rules.ChoiceSlots.Where(s => s.Choice.Pool == "invocation").SelectMany(s => c.Player.Rpg.Chosen(s.Choice.Id)).ToList();
            Assert.That(invocations, Has.Count.EqualTo(5), "One (made at the first level-up, open since Phase 6) + 2 + 2.");
            Assert.That(c.Player.Rpg.Chosen("class:invocations"), Has.Count.EqualTo(1));
            Assert.That(c.Rules.SpellSlotText, Is.EqualTo("2 pact slots of level 3"));
            Assert.That(c.Rules.Subclass.Id, Is.EqualTo("fiend"));
        });

        private static IEnumerator Run(string cls, int target, Action<CharacterSnapshot> verify)
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return null;
            yield return null;
            runtime.Playback.Pause();

            // 1. A Phase 6 character: created, saved, the save rewritten to schema v8, and loaded.
            var created = session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices(cls)));
            Assert.That(created.IsSuccess, Is.True, created.Message);
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder, runtime.ActiveSlot.Name + JsonWorldStore.Extension);
            LevelUpDriver.RewriteAsPhaseSix(path);
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return null;
            yield return null;
            var start = session.GetCharacter();
            Assert.That(start.Player.Rpg.RulesVersion, Is.EqualTo(6), "Loaded as a Phase 6 character.");
            Assert.That(start.Level, Is.EqualTo(1));
            Assert.That(start.Player.Rpg.Resources["hit_dice"], Is.EqualTo(1), "Migration gave the hit die.");
            var inventory = session.GetPlayerHub().Inventory.Select(i => i.StackId + ":" + i.Quantity).ToList();
            var purse = session.GetPlayerHub().Purse;
            var hud = controller.Hud.Root;
            Assert.That(hud.Q<Button>("xk-open-levelup").ClassListContains("xk-hidden"), Is.True, "No level-up without experience.");

            // 2. Experience through the application command; eligibility shows on the HUD and in the Hub.
            var award = session.Dispatch(new AwardExperienceCommand(ProgressionRules.ExperienceFor(target), ExperienceSource.Milestone, "test-" + cls, "Proving their worth"));
            Assert.That(award.IsSuccess, Is.True, award.Message);
            yield return null;
            Assert.That(session.GetCharacter().Level, Is.EqualTo(1), "Experience alone does not level up.");
            Assert.That(hud.Q<Button>("xk-open-levelup").ClassListContains("xk-hidden"), Is.False, "The HUD shows the level-up.");
            controller.ToggleHub();
            yield return null;
            var hub = controller.HubWindow;
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-levelup-banner"), Is.Not.Null, "The Hub shows the level-up.");
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-xp"), Is.Not.Null);

            // 3. The level-up window, opened from the Hub, through every level up to the target.
            LevelUpDriver.Click(hub.Content, "xk-hub-levelup");
            yield return null;
            Assert.That(controller.Advancement.IsOpen, Is.True);
            Assert.That(controller.Player.MovementEnabled, Is.False);
            for (var level = 2; level <= target; level++)
            {
                if (level == 2 && cls == "warlock")
                    Assert.That(controller.Advancement.Draft.Pages.Select(p => p.Title), Has.Member("Eldritch Invocations"), "The open level-1 invocation is asked for.");
                yield return LevelUpDriver.ThroughOneLevel(controller, session);
            }

            // 4. The sheet: the window closed onto the Hub's progression panel.
            Assert.That(controller.Advancement.IsOpen, Is.False);
            Assert.That(hub.IsOpen, Is.True);
            Assert.That(hub.CharacterPanel, Is.EqualTo("Progression"));
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-advancement-" + target), Is.Not.Null);
            var c = session.GetCharacter();
            Assert.That(c.Level, Is.EqualTo(target));
            Assert.That(c.Outstanding, Is.Empty);
            Assert.That(c.Player.Rpg.RulesVersion, Is.EqualTo(ProgressionRules.CurrentRulesVersion));
            Assert.That(c.Player.Rpg.CurrentHp, Is.EqualTo(c.Rules.MaxHp), "Full health rises with each level.");
            verify(c);
            hub.SelectCharacterPanel("Features");
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-resource-hit_dice"), Is.Not.Null);
            controller.CloseWindows();

            // 5. Save and reload: everything kept, nothing granted twice, no further level.
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            var before = session.GetCharacter().Player.Rpg;
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return null;
            yield return null;
            var after = session.GetCharacter();
            Assert.That(after.Level, Is.EqualTo(target));
            Assert.That(after.Player.Rpg.SubclassId, Is.EqualTo(before.SubclassId));
            Assert.That(after.Player.Rpg.Choices.Keys, Is.EquivalentTo(before.Choices.Keys));
            foreach (var pair in before.Choices) Assert.That(after.Player.Rpg.Chosen(pair.Key), Is.EqualTo(pair.Value), pair.Key);
            Assert.That(after.Player.Rpg.Resources, Is.EquivalentTo(before.Resources));
            Assert.That(after.Player.Rpg.Advancements.Count, Is.EqualTo(target - 1));
            Assert.That(session.GetPlayerHub().Inventory.Select(i => i.StackId + ":" + i.Quantity), Is.EqualTo(inventory), "Levelling grants no items.");
            Assert.That(session.GetPlayerHub().Purse, Is.EqualTo(purse));
            Assert.That(controller.OpenAdvancement(), Is.False, "No level without more experience.");
            Assert.That(session.Dispatch(new AwardExperienceCommand(10, ExperienceSource.Milestone, "test-" + cls)).IsSuccess, Is.False, "The same award is never given twice.");
            Assert.That(after.Rules.Features.Select(f => f.feature.Id).Distinct().Count(), Is.EqualTo(after.Rules.Features.Count), "Features are granted once.");
        }
    }
}
