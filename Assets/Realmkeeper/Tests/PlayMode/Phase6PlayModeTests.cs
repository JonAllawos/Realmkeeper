using System.Collections;
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
    /// <summary>
    /// Phase 6 in the shipped scene: a character created through the real creator (every step clicked),
    /// shown on the body and in the Player Hub's sheet, saved and reloaded with nothing duplicated; and a
    /// Phase 5 (schema v7) save that loads as a legacy character with nothing lost.
    /// </summary>
    public sealed class Phase6PlayModeTests
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

        private static void Click(VisualElement root, string name)
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

        private static ExplorationController Controller()
        {
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            return controller;
        }

        private static IEnumerator EnterTown(RealmkeeperBootstrap bootstrap)
        {
            var town = bootstrap.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CharacterCreation_ThroughEveryStep_ShowsOnTheBodyAndSheet_AndSurvivesSaveAndLoad()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Controller();
            yield return EnterTown(bootstrap);
            runtime.Playback.Pause();
            var hud = controller.Hud.Root;
            Assert.That(session.GetCharacter().State, Is.EqualTo(CreationState.Pending));
            var before = session.GetPlayerHub();
            var identity = session.GetCharacter().Player.CharacterId;
            Assert.That(hud.Q<Button>("xk-open-creator").ClassListContains("xk-hidden"), Is.False, "The HUD offers character creation.");

            Click(hud, "xk-open-creator");
            yield return null;
            var creator = controller.Creator;
            Assert.That(creator.IsOpen, Is.True);
            Assert.That(controller.Player.MovementEnabled, Is.False);
            var root = creator.Root;

            // 1 Identity
            root.Q<TextField>("xk-cc-name").value = "Ilsa Marrow";
            root.Q<TextField>("xk-cc-title").value = "of the Hollow Shrine";
            Click(root, "xk-cc-next");
            // 2 Appearance: female frame
            Click(root, "xk-cc-frame-1");
            Click(root, "xk-cc-next");
            // 3 Species: dwarf (a placeholder body at dwarf height)
            Click(root, "xk-cc-species-dwarf");
            Assert.That(root.Q<Label>(className: "xk-cc-warn").text, Does.Contain("Placeholder body"));
            Click(root, "xk-cc-next");
            // 4 Background: acolyte
            Click(root, "xk-cc-background-acolyte");
            Click(root, "xk-cc-next");
            // 5 Class: cleric, protector
            Click(root, "xk-cc-class-cleric");
            Assert.That(creator.Problems, Does.Contain("Divine Order"));
            Click(root, "xk-cc-class-divine_order-protector");
            Click(root, "xk-cc-next");
            // 6 Abilities: point buy, Wisdom 15, Constitution 14, Strength 13, Dexterity 12, Charisma 10, Intelligence 8 (27 points)
            Click(root, "xk-cc-method-1");
            for (var i = 0; i < 7; i++) Click(root, "xk-cc-plus-Wisdom");
            for (var i = 0; i < 6; i++) Click(root, "xk-cc-plus-Constitution");
            for (var i = 0; i < 5; i++) Click(root, "xk-cc-plus-Strength");
            for (var i = 0; i < 4; i++) Click(root, "xk-cc-plus-Dexterity");
            for (var i = 0; i < 2; i++) Click(root, "xk-cc-plus-Charisma");
            Assert.That(creator.Draft.PointsLeft, Is.Zero);
            Click(root, "xk-cc-plus-Intelligence");
            Assert.That(creator.Draft.Choices.BaseScores[(int)Ability.Intelligence], Is.EqualTo(8), "Nothing left to spend.");
            Click(root, "xk-cc-next");
            // 7 Skills: acolyte already grants Insight and Religion; choose Medicine and History
            Assert.That(root.Q<Button>("xk-cc-skillpick-Insight"), Is.Null, "A granted skill cannot be chosen again.");
            Click(root, "xk-cc-skillpick-Medicine");
            Click(root, "xk-cc-skillpick-History");
            Assert.That(creator.Problems, Does.StartWith("✓"));
            Click(root, "xk-cc-next");
            // 8 Equipment: a leather jerkin instead of the chain shirt
            Click(root, "xk-cc-kit-armour-leather");
            Click(root, "xk-cc-next");
            // 9 Review and confirm
            Assert.That(creator.Draft.Step, Is.EqualTo(CreationStep.Review));
            Assert.That(controller.Preview.Report.Props, Has.Some.StartsWith("mace_flanged@"), "The preview wears the chosen kit.");
            Click(root, "xk-cc-create");
            yield return null;

            Assert.That(creator.IsOpen, Is.False);
            Assert.That(controller.Player.MovementEnabled, Is.True);
            var c = session.GetCharacter();
            Assert.That(c.State, Is.EqualTo(CreationState.Complete));
            Assert.That(c.Player.CharacterId, Is.EqualTo(identity), "Same person.");
            Assert.That(c.FullName, Is.EqualTo("Ilsa Marrow of the Hollow Shrine"));
            Assert.That(c.Rules.Species.Id, Is.EqualTo("dwarf"));
            Assert.That(c.Rules.Class.Id, Is.EqualTo("cleric"));
            Assert.That(c.Rules.Score(Ability.Constitution), Is.EqualTo(16), "14 + 2 (dwarf).");
            Assert.That(c.Rules.MaxHp, Is.EqualTo(8 + 3 + 1), "d8 + Constitution +3 + dwarven toughness.");
            Assert.That(c.Rules.ArmorProficiencies, Has.Member("heavy"), "Protector order.");
            Assert.That(hud.Q<Button>("xk-open-creator").ClassListContains("xk-hidden"), Is.True, "No more creation prompt.");

            // The body: female dwarf height, leather jerkin worn, mace in hand, shield on the arm.
            Assert.That(controller.Player.LookSignature, Does.StartWith("Female").And.Contain("/x0.80"));
            Assert.That(controller.Player.BodyReport.Shown[EquipmentSlot.Chest], Is.EqualTo("Ranger:Body:Ranger"));
            Assert.That(controller.Player.BodyReport.Props, Has.Some.StartsWith("mace_flanged@hand_r").And.Some.StartsWith("shield_round@"));

            // The Player Hub sheet.
            controller.ToggleHub();
            yield return null;
            var hub = controller.HubWindow;
            Assert.That(hub.Section, Is.EqualTo("Character"));
            var vitals = hub.Content.Q<VisualElement>("xk-hub-vitals");
            Assert.That(vitals.Query<Label>().ToList().Select(l => l.text), Has.Member("12 / 12"));
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-ability-Wisdom").Query<Label>().ToList().Select(l => l.text), Has.Member("16").And.Member("+3").And.Member("● save +5"), "15 + 1 (dwarf), proficient save (Phase 20 ability tile).");
            hub.SelectCharacterPanel("Skills");
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-skill-Medicine").Q<Label>().text, Does.StartWith("●"));
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-skill-Arcana").Q<Label>().text, Does.StartWith("○"));
            hub.SelectCharacterPanel("Features");
            Assert.That(hub.Content.Query<Label>().ToList().Any(l => l.text == "Dwarven Toughness"), Is.True);
            hub.SelectCharacterPanel("Proficiencies");
            Assert.That(hub.Content.Query<Label>().ToList().Any(l => l.text.Contains("Darkvision 60 ft")), Is.True);
            foreach (var section in PlayerHubWindow.Sections)
            {
                hub.Select(section);
                Assert.That(hub.Content.childCount, Is.GreaterThan(0), section);
            }

            controller.CloseWindows();

            // Save and reload: everything kept, nothing granted twice, creation not offered again.
            var maces = session.GetPlayerHub().Equipment.Values.Concat(session.GetPlayerHub().Inventory).Count(i => i.ItemId == "mace_flanged");
            var symbols = session.GetPlayerHub().Inventory.Count(i => i.ItemId == "holy_symbol");
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return null;
            yield return null;
            var loaded = session.GetCharacter();
            Assert.That(loaded.Rules.Class.Id, Is.EqualTo("cleric"));
            Assert.That(loaded.Player.Rpg.BaseScores, Is.EqualTo(c.Player.Rpg.BaseScores));
            Assert.That(loaded.Player.Rpg.Chosen(ClassDefinition.SkillChoiceId), Is.EquivalentTo(new[] { "Medicine", "History" }));
            var hubAfter = session.GetPlayerHub();
            Assert.That(hubAfter.Equipment.Values.Concat(hubAfter.Inventory).Count(i => i.ItemId == "mace_flanged"), Is.EqualTo(maces));
            Assert.That(hubAfter.Inventory.Count(i => i.ItemId == "holy_symbol"), Is.EqualTo(symbols).And.EqualTo(2), "One from the Acolyte background, one as the cleric's chosen focus: each granted once.");
            Assert.That(hubAfter.Purse, Is.EqualTo(before.Purse + RpgCatalog.FindBackground("acolyte").Coin));
            Assert.That(controller.OpenCreator(), Is.False, "Creation happens once.");
            Assert.That(controller.Player.LookSignature, Does.Contain("/x0.80"), "The reloaded body is the dwarf's.");
        }

        [UnityTest]
        public IEnumerator Phase5Save_LoadsAsALegacyCharacter_WithNothingLost()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Controller();
            yield return EnterTown(bootstrap);
            runtime.Playback.Pause();
            var knife = session.GetPlayerHub().Inventory.First(i => i.ItemId == "dagger_iron");
            Assert.That(session.Dispatch(new EquipItemCommand(knife.StackId)).IsSuccess, Is.True);
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            var before = session.GetPlayerHub();
            var player = session.GetCharacter().Player;

            // Rewrite the save as a Phase 5 (schema v7) file: no character record.
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder, runtime.ActiveSlot.Name + JsonWorldStore.Extension);
            var doc = JObject.Parse(File.ReadAllText(path));
            doc["schemaVersion"] = 7;
            ((JObject)doc["world"]["player"]).Remove("rpg");
            File.WriteAllText(path, doc.ToString());

            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return null;
            yield return null;
            var c = session.GetCharacter();
            Assert.That(c.State, Is.EqualTo(CreationState.Legacy));
            var after = session.GetPlayerHub();
            Assert.That(c.Player.CharacterId, Is.EqualTo(player.CharacterId));
            Assert.That(after.Name, Is.EqualTo(before.Name));
            Assert.That(after.Purse, Is.EqualTo(before.Purse));
            Assert.That(after.Appearance.HairId, Is.EqualTo(before.Appearance.HairId));
            Assert.That(after.Inventory.Select(i => i.StackId), Is.EquivalentTo(before.Inventory.Select(i => i.StackId)));
            Assert.That(after.Equipment[EquipmentSlot.MainHand].ItemId, Is.EqualTo("dagger_iron"));
            Assert.That(c.Player.Pose, Is.EqualTo(player.Pose));
            Assert.That(controller.IsExploring, Is.True, "Still in the town.");
            Assert.That(controller.Player.BodyReport.Props, Has.Some.StartsWith("dagger_iron@hand_r"), "Still looks the same.");

            controller.ToggleHub();
            yield return null;
            Assert.That(controller.HubWindow.Content.Q<VisualElement>("xk-hub-create-banner"), Is.Not.Null, "Setup is offered, not forced.");
            controller.CloseWindows();
            Assert.That(controller.OpenCreator(), Is.True, "A legacy character can complete setup.");
            controller.CloseWindows();
        }
    }
}
