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
    /// Phase 22 (ADR-0109) in the shipped scene, through the real controls:
    /// - the creator shows a tiefling's level 3 and 5 legacy spells as arriving later, asks for the legacy's ability, and its
    ///   review shows level 1 with everything the character starts with (AC, features, magic, uses);
    /// - the created character is exactly level 1 (the baseline holds);
    /// - earning enough experience shows the level-up notice once, and the HUD button stays bronze with the choices open;
    /// - the level-up window separates what is gained automatically from the player's choices; closing it keeps the picks;
    ///   reopening resumes; confirming applies each level once; nothing is chosen for the player;
    /// - the sheet's Progression shows the class, the latest level, the waiting level and what is still locked.
    /// With RK_CAPTURE_DIR set it writes the Phase 22 screenshots (1920×1080, 1366×768 and 1280×720 where it matters).
    /// </summary>
    public sealed class Phase22PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private static readonly List<string> Log = new List<string>();
        private static readonly (int w, int h, string suffix)[] Sizes = { (1920, 1080, string.Empty), (1366, 768, "_768"), (1280, 720, "_720") };

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
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, "p22_capture_log.txt"), Log);
            Log.Clear();
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
            Assert.That(b.enabledInHierarchy, Is.True, name + " is disabled");
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = b; b.SendEvent(click); }
        }

        private static IEnumerator Settle(int frames = 4)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static string Texts(VisualElement root) => string.Join(" | ", root.Query<Label>().ToList().Select(l => l.text));

        /// <summary>Makes every choice the open level-up asks for, the first allowed pick each, as a player clicking down the list would.</summary>
        private static void ChooseAll(AdvancementDraft d)
        {
            for (var pass = 0; pass < 4; pass++)
                foreach (var slot in d.Required)
                {
                    var c = slot.Choice;
                    if (c.Kind == ChoiceKind.Subclass) { if (d.Choices.SubclassId == null) d.Toggle(c, c.Options[0].Id); continue; }
                    var values = c.Kind == ChoiceKind.Option ? c.Options.Select(o => o.Id)
                        : c.Kind == ChoiceKind.Feat ? new[] { "ability_score_improvement" }.Concat(RpgCatalog.Feats.Select(f => f.Id))
                        : c.Kind == ChoiceKind.AbilityIncrease ? AbilityRules.All.Select(a => a.ToString())
                        : c.Kind == ChoiceKind.Language ? RpgCatalog.Languages.Select(l => l.Id)
                        : (c.Skills.Count > 0 ? c.Skills : SkillRules.All).Select(s => s.ToString());
                    foreach (var v in values)
                    {
                        if (d.Picked(c.Id).Count >= c.Count) break;
                        if (d.Unavailable(c, v) == null && !d.IsPicked(c, v)) d.Toggle(c, v);
                    }
                }
        }

        [UnityTest]
        public IEnumerator ANewCharacter_IsLevelOne_AndLevelsUpByThePlayersOwnChoices()
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
            yield return Settle(6);
            runtime.Playback.Pause();
            var cam = controller.CameraRig.Camera;

            // ----- The creator: a tiefling wizard -----
            Assert.That(session.Dispatch(new AwardExperienceCommand(300, ExperienceSource.Story, "before")).IsSuccess, Is.False, "D4: nothing is earned before creation.");
            Assert.That(controller.OpenCreator(), Is.True);
            yield return Settle();
            var creator = controller.Creator;
            var root = creator.Root;
            root.Q<TextField>("xk-cc-name").value = "Ysmay Corrow";
            Click(root, "xk-cc-step-Species");
            Click(root, "xk-cc-species-tiefling");
            Click(root, "xk-cc-species-fiendish_legacy-infernal");
            yield return Settle();
            var speciesText = Texts(root);
            Assert.That(speciesText, Does.Contain("Legacy Spell (level 3) (from level 3)").And.Contain("Legacy Spell (level 5) (from level 5)"), "Later traits are shown as later.");
            Assert.That(creator.Draft.Problems(CreationStep.Species), Has.Some.Contains("Legacy Spellcasting Ability"), "The legacy's ability must be chosen.");
            Click(root, "xk-cc-species-legacy_ability-intelligence");
            yield return Settle();
            Assert.That(creator.Draft.Problems(CreationStep.Species), Is.Empty);
            yield return Shoot("p22_01_creator_tiefling_ability_and_later_traits", "Creator, tiefling: the legacy's ability chosen (Intelligence); the level 3 and 5 legacy spells marked as arriving later, not at creation.", cam, true);

            creator.Draft.SetBackground("scholar");
            creator.Draft.SetClass("wizard");
            var cls = RpgCatalog.FindClass("wizard");
            foreach (var ch in cls.Choices.Where(x => x.Kind == ChoiceKind.Option))
                foreach (var o in ch.Options) if (creator.Draft.Picked(ch.Id).Count < ch.Count) creator.Draft.Toggle(ch.Id, o.Id);
            foreach (var s in cls.SkillOptions) if (creator.Draft.Picked(ClassDefinition.SkillChoiceId).Count < cls.SkillChoices) creator.Draft.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            creator.Render();
            Click(root, "xk-cc-step-Review");
            yield return Settle();
            Assert.That(creator.Draft.Problems(CreationStep.Review), Is.Empty, string.Join("; ", creator.Draft.Problems(CreationStep.Review)));
            var level = root.Q<VisualElement>("xk-cc-review-level");
            Assert.That(level, Is.Not.Null, "The review states the level.");
            Assert.That(Texts(level), Does.Contain("1 · 0 experience").And.Contain("proficiency +2"));
            Assert.That(root.Q<VisualElement>("xk-cc-review-ac"), Is.Not.Null, "Armour class.");
            var magic = Texts(root.Q<VisualElement>("xk-cc-review-magic"));
            Assert.That(magic, Does.Contain("slots: 2 × 1st"));
            Assert.That(magic.Split(new[] { "Fire Bolt" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1), "Fire Bolt (the legacy's cantrip) is listed once: " + magic);
            Assert.That(Texts(root.Q<VisualElement>("xk-cc-review-resources")), Does.Contain("Hit Dice 1").Or.Contain("Hit dice 1"));
            Assert.That(Texts(root.Q<VisualElement>("xk-cc-review-features")), Does.Not.Contain("Legacy Spell"), "Nothing from a later level.");
            yield return Shoot("p22_02_creator_review_level_one", "Creator review: level 1, AC, initiative and speed, level 1 features, magic (cantrips, spellbook, two 1st-level slots), uses, and the species' later spells.", cam, true);
            Click(root, "xk-cc-create");
            yield return Settle(8);

            var c = session.GetCharacter();
            Assert.That(c.State, Is.EqualTo(CreationState.Complete));
            Assert.That(c.Level, Is.EqualTo(1));
            Assert.That(c.Experience, Is.EqualTo(0));
            Assert.That(LevelOneBaseline.Check(c.Player.Rpg), Is.Empty, string.Join("; ", LevelOneBaseline.Check(c.Player.Rpg)));
            Assert.That(controller.Hud.Root.Q<Button>("xk-open-levelup").ClassListContains("xk-hidden"), Is.True, "No level-up at level 1.");

            controller.ToggleHub();
            yield return Settle();
            controller.HubWindow.SelectCharacterPanel("Progression");
            yield return Settle();
            var hub = controller.HubWindow.Root;
            Assert.That(Texts(hub), Does.Contain("Legacy Spell (level 3)"), "Locked, with its level.");
            yield return Shoot("p22_03_sheet_progression_level_one", "Sheet, Progression at level 1: class, experience, and what is still locked by level (the legacy spells at 3 and 5).", cam, false);
            controller.CloseWindows();
            yield return Settle();

            // ----- Experience: the notice, once -----
            Assert.That(session.Dispatch(new AwardExperienceCommand(ProgressionRules.ExperienceFor(3), ExperienceSource.Quest, "p22-quest", "A long errand")).IsSuccess, Is.True);
            controller.CheckLevelUp();
            yield return Settle();
            Assert.That(controller.LevelNoticeShown, Is.True, "An unmistakable notice.");
            var notice = controller.Hud.Root.Q<VisualElement>("xk-levelnotice");
            Assert.That(Texts(notice), Does.Contain("Levels 2–3 reached").And.Contain("Nothing is taken until you confirm"));
            Assert.That(session.GetCharacter().Level, Is.EqualTo(1), "Nothing applied.");
            var hudButton = controller.Hud.Root.Q<Button>("xk-open-levelup");
            Assert.That(hudButton.ClassListContains("xk-hidden"), Is.False);
            Assert.That(hudButton.text, Does.StartWith("Level 2 ready"));
            Assert.That(hudButton.ClassListContains("xk-levelup-ready"), Is.True);
            yield return Shoot("p22_04_level_up_notice", "The level-up notice: levels 2–3 reached, what level 2 brings, nothing taken until confirmed; the HUD button bronze.", cam, true);
            Click(notice, "xk-levelnotice-later");
            yield return Settle();
            Assert.That(controller.LevelNoticeShown, Is.False);
            controller.CheckLevelUp();
            Assert.That(controller.LevelNoticeShown, Is.False, "Shown once per level.");
            Assert.That(session.GetCharacter().Player.Rpg.LevelUpNoticed, Is.EqualTo(3));

            // ----- Level 2: the overview, picks kept on closing, resumed on reopening -----
            Click(controller.Hud.Root, "xk-open-levelup");
            yield return Settle();
            var adv = controller.Advancement;
            Assert.That(adv.IsOpen, Is.True);
            var content = adv.Content;
            var overview = Texts(content);
            Assert.That(overview, Does.Contain("Gained automatically when you confirm").And.Contain("Your choices").And.Contain("Still to come"));
            yield return Shoot("p22_05_level_up_overview", "Level-up overview: gained automatically (hit points, proficiency, features, resources) apart from the player's choices; what is still to come.", cam, true);

            var first = adv.Draft.Required.FirstOrDefault(s => s.Choice.Kind == ChoiceKind.Option && s.Choice.Options.Any(o => adv.Draft.Unavailable(s.Choice, o.Id) == null));
            Assert.That(first.Choice, Is.Not.Null, "A wizard's level 2 adds spellbook spells.");
            var pick = first.Choice.Options.First(o => adv.Draft.Unavailable(first.Choice, o.Id) == null);
            adv.Draft.Toggle(first.Choice, pick.Id);
            adv.Render();
            controller.CloseWindows(); // "Later": the picks are kept
            yield return Settle();
            var kept = session.GetCharacter().Player.Rpg.PendingAdvancement;
            Assert.That(kept, Is.Not.Null, "Closing keeps the picks.");
            Assert.That(kept.Choices[first.Choice.Id], Has.Member(pick.Id));
            Assert.That(session.GetCharacter().Level, Is.EqualTo(1), "Nothing applied.");
            controller.CheckLevelUp();
            Assert.That(hudButton.tooltip, Does.Contain("You started this level-up"));
            yield return Shoot("p22_06_hud_level_up_in_progress", "After closing: the HUD button keeps the level-up waiting, with its choices.", cam, false);

            Assert.That(controller.OpenAdvancement(), Is.True);
            yield return Settle();
            Assert.That(adv.Draft.Resumed, Is.True);
            Assert.That(adv.Draft.IsPicked(first.Choice, pick.Id), Is.True, "Resumed where the player left off.");
            Assert.That(Texts(content), Does.Contain("Picking up where you left off"));
            ChooseAll(adv.Draft);
            adv.GoTo(AdvancementDraft.ReviewTitle);
            yield return Settle();
            Assert.That(adv.Draft.Problems(), Is.Empty, string.Join("; ", adv.Draft.Problems()));
            Click(adv.Root, "xk-lv-confirm");
            yield return Settle(6);
            Assert.That(session.GetCharacter().Level, Is.EqualTo(2));
            Assert.That(session.GetCharacter().Player.Rpg.PendingAdvancement, Is.Null);

            // ----- Level 3: the subclass is the player's to choose -----
            Assert.That(adv.IsOpen, Is.True, "The window reopens for level 3.");
            Assert.That(adv.Draft.Level, Is.EqualTo(3));
            Assert.That(adv.Draft.Choices.SubclassId, Is.Null, "No subclass is chosen for the player.");
            var subPage = adv.Draft.Pages.First(p => p.Choice != null && p.Choice.Value.Choice.Kind == ChoiceKind.Subclass);
            adv.GoTo(subPage.Title);
            yield return Settle();
            yield return Shoot("p22_07_level_up_subclass_choice", "Level 3: the Arcane Tradition, the player's own choice (none picked for them).", cam, true);
            ChooseAll(adv.Draft);
            adv.GoTo(AdvancementDraft.ReviewTitle);
            yield return Settle();
            yield return Shoot("p22_08_level_up_review", "Level 3 review before confirming.", cam, false);
            Click(adv.Root, "xk-lv-confirm");
            yield return Settle(8);
            var after = session.GetCharacter();
            Assert.That(after.Level, Is.EqualTo(3));
            Assert.That(after.Player.Rpg.SubclassId, Is.Not.Null);
            Assert.That(after.Player.Rpg.Advancements.Count, Is.EqualTo(2), "Each level once.");
            Assert.That(after.Rules.ActiveFeatures.Any(f => f.Feature.Id == "legacy_spell_3"), Is.True, "The legacy spell arrives at level 3 (shown as not yet played).");
            Assert.That(after.CanAdvance, Is.False);

            Assert.That(controller.HubWindow.IsOpen, Is.True, "The sheet opens on Progression after the last level.");
            yield return Settle();
            Assert.That(Texts(controller.HubWindow.Root), Does.Contain("Latest: level 3"));
            yield return Shoot("p22_09_sheet_after_levelling", "Sheet, Progression at level 3: the tradition, the latest level's summary, levels gained, and what is still locked.", cam, true);
        }

        private IEnumerator Shoot(string name, string note, Camera camera, bool allSizes)
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            foreach (var (w, h, suffix) in Sizes)
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
                if (!allSizes) break;
            }
        }
    }
}
