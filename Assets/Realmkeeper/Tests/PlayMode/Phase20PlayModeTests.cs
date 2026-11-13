using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 20 in the shipped scene, through the real controls: a tiefling made in the redesigned creator (identity with
    /// pronouns and a concept, a species skin tone, height and build, a fiendish legacy), the live build panel and its
    /// "still to choose" list, cancelling without changing anything, the sheet's ability tiles and Origins, a person seen
    /// through "About them" (a stranger's sheet stays hidden), and everything surviving a save and reload. With
    /// RK_CAPTURE_DIR set it also writes the Phase 20 screenshots.
    /// </summary>
    public sealed class Phase20PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const string Slot = "p20-test";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(_dir)) Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (!string.IsNullOrEmpty(_dir)) File.WriteAllLines(Path.Combine(_dir, "p20_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var store = new Realmkeeper.Infrastructure.Persistence.JsonWorldStore(Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder));
            if (store.Exists(Slot)) store.Delete(Slot);
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
            Assert.That(b.enabledInHierarchy, Is.True, name + " is disabled");
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = b; b.SendEvent(click); }
        }

        private static ExplorationController Controller()
        {
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            return controller;
        }

        private static IEnumerator Settle(int frames = 4)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator ATiefling_MadeInTheCreator_IsShownAndKnownCorrectly_AndSurvivesSaveAndLoad()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Controller();
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(6);
            runtime.Playback.Pause();
            var cam = controller.CameraRig.Camera;

            // Open, write a little, cancel: nothing in the world changes.
            Assert.That(controller.OpenCreator(), Is.True);
            yield return null;
            var creator = controller.Creator;
            var root = creator.Root;
            root.Q<TextField>("xk-cc-name").value = "Ash Calloway";
            Click(root, "xk-creator-close");
            yield return null;
            Assert.That(creator.IsOpen, Is.False);
            Assert.That(session.GetCharacter().State, Is.EqualTo(CreationState.Pending), "Cancelling creates nothing.");

            Assert.That(controller.OpenCreator(), Is.True);
            yield return Settle();
            root = creator.Root;
            // 1 Identity, with pronouns and a concept.
            root.Q<TextField>("xk-cc-name").value = "Ash Calloway";
            root.Q<TextField>("xk-cc-pronouns").value = "they/them";
            root.Q<TextField>("xk-cc-concept").value = "a ferryman's child who keeps other people's secrets";
            Assert.That(creator.Build.Q<Label>(className: "xk-cc-build-name").text, Is.EqualTo("Ash Calloway"), "The build panel follows the choices.");
            Assert.That(creator.Build.Query<Label>(className: "xk-cc-build-problem").ToList().Count, Is.GreaterThan(0), "What is still to choose is listed.");
            yield return Shoot("p20_01_creator_overview", "Creator: identity with pronouns and a concept; the live build lists what is still to choose.", cam);
            Click(root, "xk-cc-next");

            // 3 Species first (so the appearance step offers the species' tones): tiefling, chthonic legacy.
            Click(root, "xk-cc-step-Species");
            Click(root, "xk-cc-species-tiefling");
            Assert.That(creator.Problems, Does.Contain("Fiendish Legacy"), "The legacy must be chosen.");
            yield return Shoot("p20_02_species_selection", "Species as cards; a tiefling selected, its legacy not yet chosen (the problem is named).", cam);
            Click(root, "xk-cc-species-fiendish_legacy-chthonic");
            Assert.That(creator.Problems, Does.StartWith("✓"));
            yield return Shoot("p20_03_species_detail", "Species detail: traits, lore, the chosen chthonic legacy.", cam);

            // 2 Appearance: a tiefling tone, taller, heavy build.
            Click(root, "xk-cc-step-Appearance");
            Assert.That(root.Q<Button>("xk-cc-skin-7"), Is.Not.Null, "A tiefling's own tones are offered.");
            Click(root, "xk-cc-skin-7");
            root.Q<SliderInt>("xk-cc-height").value = 106;
            Click(root, "xk-cc-build-2");
            yield return Settle();
            Assert.That(controller.Preview.Report, Is.Not.Null);
            yield return Shoot("p20_04_appearance", "Appearance: a tiefling skin tone (violet), height 106 %, heavy build, in the live preview.", cam);

            // Measured in Unity: a creator redraw with a changed look (the preview body is rebuilt), and one without.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 10; i++) { creator.Draft.Choices.Appearance.Build = i % 2 == 0 ? 1 : 2; creator.Render(); }
            sw.Stop();
            var changedMs = sw.Elapsed.TotalMilliseconds / 10;
            sw.Restart();
            for (var i = 0; i < 10; i++) creator.Render();
            sw.Stop();
            var sameMs = sw.Elapsed.TotalMilliseconds / 10;
            var measured = $"Creator redraw with a new look (preview rebuilt): {changedMs:0.0} ms; without a change: {sameMs:0.0} ms.";
            TestContext.WriteLine(measured);
            Log.Add("measure: " + measured);

            // 4 Background, 5 Class, 6 Abilities, 7 Skills, 8 Equipment.
            Click(root, "xk-cc-step-Background");
            Click(root, "xk-cc-background-criminal");
            yield return Shoot("p20_06_background_selection", "Background cards and the criminal background's detail.", cam);
            Click(root, "xk-cc-step-Class");
            Click(root, "xk-cc-class-rogue");
            yield return Shoot("p20_05_class_selection", "Class cards; the rogue's subclass and the road ahead (levels 2–6).", cam);
            Click(root, "xk-cc-step-Abilities");
            Click(root, "xk-cc-step-Skills");
            foreach (var s in new[] { "Acrobatics", "Insight", "Perception", "Persuasion" })
                if (root.Q<Button>("xk-cc-skillpick-" + s) != null && creator.Draft.Picked(ClassDefinition.SkillChoiceId).Count < 4) Click(root, "xk-cc-skillpick-" + s);
            foreach (var ch in RpgCatalog.FindClass("rogue").Choices.Where(c => c.Kind == ChoiceKind.Expertise))
                foreach (var s in SkillRules.All)
                    if (creator.Draft.Picked(ch.Id).Count < ch.Count && creator.Draft.Rules.SkillRank(s) != ProficiencyRank.None) creator.Draft.Toggle(ch.Id, s.ToString());
            creator.Render();
            yield return Shoot("p20_07_abilities_skills", "Skills with their totals and sources; the build panel shows abilities and saves.", cam);
            Click(root, "xk-cc-step-Review");
            yield return Settle();
            Assert.That(creator.Draft.Problems(CreationStep.Review), Is.Empty, string.Join("; ", creator.Draft.Problems(CreationStep.Review)));
            yield return Shoot("p20_08_review", "Review before creating.", cam);
            Click(root, "xk-cc-create");
            yield return Settle();

            var c = session.GetCharacter();
            Assert.That(c.State, Is.EqualTo(CreationState.Complete));
            Assert.That(c.Player.Rpg.Pronouns, Is.EqualTo("they/them"));
            Assert.That(c.Player.Rpg.Chosen("species:fiendish_legacy"), Is.EqualTo(new[] { "chthonic" }));
            Assert.That(c.Rules.Resistances, Has.Member("necrotic").And.No.Member("fire"));
            Assert.That(c.Player.Appearance.SkinTone, Is.EqualTo(7));
            Assert.That(c.Player.Appearance.HeightPercent, Is.EqualTo(106));
            Assert.That(c.Player.Appearance.Build, Is.EqualTo(2));

            // The sheet: ability tiles with their explanation; Origins with the legacy.
            controller.ToggleHub();
            yield return Settle();
            var hub = controller.HubWindow;
            hub.SelectCharacterPanel("Sheet");
            yield return Settle();
            var tile = hub.Root.Q<VisualElement>("xk-hub-ability-Constitution");
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.tooltip, Does.Contain("Modifier").And.Contain("Saving throw"), "Every number explains itself.");
            yield return Shoot("p20_09_sheet_overview", "Character sheet overview: name, concept, ability tiles with saves, combat numbers.", cam);
            var swSheet = System.Diagnostics.Stopwatch.StartNew();
            foreach (var panel in PlayerHubWindow.CharacterPanels) hub.SelectCharacterPanel(panel);
            swSheet.Stop();
            var sheetMeasured = $"Opening each of the {PlayerHubWindow.CharacterPanels.Count} sheet tabs: {swSheet.Elapsed.TotalMilliseconds / PlayerHubWindow.CharacterPanels.Count:0.0} ms each.";
            TestContext.WriteLine(sheetMeasured);
            Log.Add("measure: " + sheetMeasured);
            hub.SelectCharacterPanel("Skills");
            yield return Shoot("p20_10_sheet_skills", "Skills: every total with its sources.", cam);
            hub.SelectCharacterPanel("Origins");
            yield return Settle();
            Assert.That(hub.Root.Q<VisualElement>("xk-hub-content").Query<Label>().ToList().Any(l => l.text.Contains("Chthonic")), Is.True, "Origins name the legacy.");
            yield return Shoot("p20_11_sheet_origins", "Origins: species, legacy, traits (with what the game cannot play yet), background, history.", cam);
            hub.SelectCharacterPanel("Progression");
            yield return Shoot("p20_11b_sheet_progression", "Progression: experience and the history of levels taken.", cam);
            hub.Select("Equipment");
            yield return Shoot("p20_15_equipment_appearance", "Equipment beside the 3D preview wearing it.", cam);
            controller.CloseWindows();
            yield return Settle();

            // A person, as far as the player knows them.
            var stranger = controller.Residents.Keys.FirstOrDefault(id => session.GetKnownPerson(id)?.Acquaintance == Acquaintance.Seen && controller.StartDialogue(id).IsSuccess);
            if (!stranger.IsEmpty)
            {
                yield return Settle();
                Click(controller.Dialogue.Root, "xk-dialogue-about-toggle");
                yield return Settle();
                var about = controller.Dialogue.About;
                var labels = about.Query<Label>(className: "xk-w-key").ToList().Select(l => l.text).ToList();
                Assert.That(labels, Has.Member("Occupation"));
                Assert.That(labels, Has.No.Member("Abilities").And.No.Member("Trained as"), "A stranger's sheet stays hidden.");
                yield return Shoot("p20_13_npc_inspection", "About them: what the player knows of a person they have just met.", cam);
                controller.EndDialogue();
            }

            // A companion: asked in conversation, then their sheet in the party, and "About them" now shows their measure.
            var willing = new List<Realmkeeper.Domain.Identifiers.NpcId>();
            yield return PartyDriver.Willing(runtime, controller, session, 1, willing);
            cam = controller.CameraRig.Camera; // the reload rebuilt the scene
            yield return PartyDriver.Recruit(controller, session, willing[0]);
            Assert.That(session.GetKnownPerson(willing[0]).Acquaintance, Is.EqualTo(Acquaintance.Companion));
            Assert.That(session.GetKnownPerson(willing[0]).Lines.Select(l => l.Label), Has.Member("Abilities"));
            controller.ToggleHub();
            controller.HubWindow.ShowParty(willing[0]);
            yield return Settle();
            Assert.That(controller.HubWindow.Root.Q<VisualElement>("xk-party-abilities"), Is.Not.Null, "The companion's ability tiles.");
            yield return Shoot("p20_14_companion_sheet", "A companion's sheet in the party: ability tiles, saves, skills, features, loyalty.", cam);
            controller.CloseWindows();
            yield return Settle();

            // A level-up, opened and cancelled: nothing is applied until confirmed.
            var award = session.Dispatch(new AwardExperienceCommand(300, ExperienceSource.Milestone, reason: "Phase 20 test", awardKey: "p20-test-award"));
            Assert.That(award.IsSuccess, Is.True, award.Message);
            Assert.That(controller.OpenAdvancement(), Is.True);
            yield return Settle();
            yield return Shoot("p20_12_level_up", "The level-up window for level 2: what the new level gives, before confirming.", cam);
            controller.CloseWindows();
            yield return Settle();
            Assert.That(session.GetCharacter().Level, Is.EqualTo(1), "Closing the level-up applies nothing.");
            Assert.That(session.GetCharacter().CanAdvance, Is.True);

            // Save and reload: everything comes back.
            runtime.SelectSlot(Slot);
            Assert.That(runtime.Save().IsSuccess, Is.True);
            Assert.That(runtime.Load(Slot).IsSuccess, Is.True);
            yield return Settle();
            c = session.GetCharacter();
            Assert.That(c.Player.Rpg.Concept, Does.StartWith("a ferryman's child"));
            Assert.That(c.Player.Rpg.Chosen("species:fiendish_legacy"), Is.EqualTo(new[] { "chthonic" }));
            Assert.That(c.Player.Appearance.HeightPercent, Is.EqualTo(106));
        }

        private IEnumerator Shoot(string name, string note, Camera camera)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            foreach (var (w, h, suffix) in new[] { (1920, 1080, string.Empty), (1366, 768, "_768") })
            {
                yield return null;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var panels = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).Select(d => d.panelSettings).Distinct().ToList();
                var previous = panels.Select(x => (x, x.targetTexture, x.clearColor, x.scale)).ToList();
                foreach (var x in panels) { x.targetTexture = rt; x.clearColor = false; x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, h); }
                yield return null;
                yield return null;
                var target = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = target;
                yield return null;
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                File.WriteAllBytes(Path.Combine(_dir, name + suffix + ".jpg"), tex.EncodeToJPG(88));
                Object.Destroy(tex);
                foreach (var (x, t, cl, sc) in previous) { x.targetTexture = t; x.clearColor = cl; x.scale = sc; }
                rt.Release();
                Object.Destroy(rt);
                Log.Add($"{name}{suffix}: {w}x{h}. {note}");
                if (suffix.Length == 0 && !name.StartsWith("p20_0", StringComparison.Ordinal) && name != "p20_09_sheet_overview") break; // two sizes for the creator and the sheet overview
            }
        }
    }
}
