using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
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
    /// Phase 20A in the shipped scene, through the real controls:
    /// - the creator shows each lineage benefit as active, conditional or not yet played;
    /// - an eye colour reaches the preview and the body in the world, with the chosen height and build;
    /// - the sheet's Origins say the same;
    /// - a tiefling fighter's legacy cantrip is offered as an innate cantrip in a fight;
    /// - a cloud goliath teleports with Cloud's Jaunt by clicking the grid.
    /// With RK_CAPTURE_DIR set it also writes the Phase 20A screenshots at 1920×1080, 1366×768 and 1280×720.
    /// </summary>
    public sealed class Phase20APlayModeTests
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
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, "p20a_capture_log.txt"), Log);
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

        private static IEnumerator EnterTown(RealmkeeperBootstrap bootstrap, RealmkeeperRuntime runtime)
        {
            var town = bootstrap.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(6);
            runtime.Playback.Pause();
        }

        private static int Badges(VisualElement root, string state) => root.Query<Label>("xk-benefit-" + state).ToList().Count;

        /// <summary>The eye textures the scene's bodies wear (from the eye slots' property blocks).</summary>
        private static List<string> EyeTextures()
        {
            var names = new List<string>();
            var id = Shader.PropertyToID("_BaseMap");
            foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith("M_Eyes", StringComparison.Ordinal)) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    var t = block.GetTexture(id);
                    if (t != null) names.Add(t.name);
                }
            }

            return names;
        }

        [UnityTest]
        public IEnumerator TheCreatorAndSheet_ShowHowEachBenefitIsPlayed_AndTheEyesAndBuildReachTheWorld()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Controller();
            yield return EnterTown(bootstrap, runtime);
            var cam = controller.CameraRig.Camera;

            Assert.That(controller.OpenCreator(), Is.True);
            yield return Settle();
            var creator = controller.Creator;
            var root = creator.Root;
            root.Q<TextField>("xk-cc-name").value = "Brannoc Esk";

            // Species: each benefit with its badge.
            Click(root, "xk-cc-step-Species");
            Click(root, "xk-cc-species-goliath");
            yield return Settle();
            Assert.That(Badges(root, "conditional"), Is.GreaterThanOrEqualTo(6), "The six ancestries are played, in their situations.");
            Assert.That(Badges(root, "not-yet-played"), Is.EqualTo(0), "Nothing a goliath has is unplayed now.");
            Click(root, "xk-cc-species-giant_ancestry-fire");
            yield return Shoot("p20a_01_creator_goliath_badges", "Creator, goliath: traits and the six ancestries, each marked active, conditional or not yet played, with when it works.", cam, true);

            Click(root, "xk-cc-species-gnome");
            yield return Settle();
            Assert.That(Badges(root, "not-yet-played"), Is.GreaterThanOrEqualTo(2), "Both gnome lineages wait for magic, and say so.");
            yield return Shoot("p20a_02_creator_gnome_not_yet_played", "Creator, gnome: both lineages marked not yet played, naming the missing magic.", cam, true);

            Click(root, "xk-cc-species-tiefling");
            Click(root, "xk-cc-species-fiendish_legacy-infernal");
            yield return Settle();
            Assert.That(root.Query<Label>().ToList().Any(l => l.text.Contains("Fire Bolt") && l.text.Contains("Charisma")), Is.True);
            yield return Shoot("p20a_03_creator_tiefling_legacy", "Creator, tiefling: the infernal legacy's cantrip cast with Charisma (conditional), its resistance always on.", cam, true);

            // Appearance: blue eyes, shorter, slight.
            Click(root, "xk-cc-step-Appearance");
            yield return Settle();
            Assert.That(root.Q<Button>("xk-cc-eyes-0"), Is.Not.Null, "The eye colours are offered.");
            Click(root, "xk-cc-eyes-5");
            root.Q<SliderInt>("xk-cc-height").value = 94;
            Click(root, "xk-cc-build-0");
            yield return Settle(6);
            Assert.That(EyeTextures(), Has.Member("Eye Blue"), "The preview's eyes are recoloured.");
            yield return Shoot("p20a_04_creator_eye_colour", "Appearance: eye colour swatches; blue chosen, height 94 %, slight build.", cam, true);

            // Finish as a fighter.
            creator.Draft.SetBackground("outlander");
            creator.Draft.SetClass("fighter");
            var cls = RpgCatalog.FindClass("fighter");
            foreach (var ch in cls.Choices.Where(x => x.Kind == ChoiceKind.Option)) creator.Draft.Toggle(ch.Id, ch.Options[0].Id);
            foreach (var s in cls.SkillOptions) if (creator.Draft.Picked(ClassDefinition.SkillChoiceId).Count < cls.SkillChoices) creator.Draft.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            creator.Render();
            Click(root, "xk-cc-step-Review");
            yield return Settle();
            Assert.That(creator.Draft.Problems(CreationStep.Review), Is.Empty, string.Join("; ", creator.Draft.Problems(CreationStep.Review)));
            Click(root, "xk-cc-create");
            yield return Settle(8);

            var c = session.GetCharacter();
            Assert.That(c.State, Is.EqualTo(CreationState.Complete));
            Assert.That(c.Player.Appearance.EyeColour, Is.EqualTo(5));
            var hub = session.GetPlayerHub();
            Assert.That(hub.Appearance.HeightPercent, Is.EqualTo(94), "The hub the world body is built from keeps the height (it dropped it before Phase 20A).");
            Assert.That(hub.Appearance.Build, Is.EqualTo(0));
            Assert.That(EyeTextures(), Has.Member("Eye Blue"), "The body in the world has the chosen eyes.");
            var bodies = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Body ", StringComparison.Ordinal)).ToList();
            var expectH = 0.94f; // a tiefling's typical height is a human's
            var body = bodies.FirstOrDefault(t => Mathf.Abs(t.localScale.y - expectH) < 0.005f && Mathf.Abs(t.localScale.x - expectH * PlayerAppearanceOptions.BuildWidth(0)) < 0.005f);
            Assert.That(body, Is.Not.Null, "The world body is 94 % tall and slight: " + string.Join(", ", bodies.Select(b => b.localScale.ToString("F3"))));
            yield return CloseUp(body, "p20a_05_eyes_closeup", "Close-up of the body in the world: blue irises, whites and skin unchanged.");

            // The sheet's Origins.
            controller.ToggleHub();
            yield return Settle();
            controller.HubWindow.SelectCharacterPanel("Origins");
            yield return Settle();
            var content = controller.HubWindow.Root.Q<VisualElement>("xk-hub-content");
            Assert.That(Badges(content, "conditional"), Is.GreaterThanOrEqualTo(1), "The legacy (resistance and a cantrip).");
            Assert.That(Badges(content, "active"), Is.GreaterThanOrEqualTo(1), "Darkvision.");
            yield return Shoot("p20a_06_sheet_origins_badges", "Sheet, Origins: each trait marked active, conditional or not yet played.", cam, true);
            controller.HubWindow.SelectCharacterPanel("Sheet");
            yield return Shoot("p20a_07_sheet_overview", "Sheet overview after Phase 20A.", cam, true);
            controller.CloseWindows();
            yield return Settle();

            // A fight: the legacy cantrip, offered as an innate cantrip.
            controller.AutoAi = false;
            Assert.That(controller.StartPracticeFight().IsSuccess, Is.True);
            yield return Settle(8);
            for (var i = 0; i < 10 && !controller.Combat.Snapshot.IsPlayersTurn; i++) { controller.RunAiTurn(); yield return Settle(2); }
            var bolt = controller.Combat.Snapshot.PlayerSpells.FirstOrDefault(s => s.Key == "fire_bolt");
            Assert.That(bolt, Is.Not.Null, "A tiefling fighter has the legacy cantrip.");
            Assert.That(bolt.CostText, Is.EqualTo("innate cantrip (Charisma)"));
            var innate = session.GetCharacter().Rules;
            Assert.That(controller.Combat.Snapshot.Player.SpellAttack, Is.EqualTo(innate.InnateSpellAttack(Ability.Charisma).Total), "The header shows the figures the cantrip uses, not 0.");
            Assert.That(controller.Combat.Snapshot.Player.SpellSaveDc, Is.EqualTo(innate.InnateSpellSaveDc(Ability.Charisma).Total));
            controller.Combat.OpenGroupPopup("spells");
            yield return Settle(2);
            yield return Shoot("p20a_08_combat_innate_cantrip", "Combat: a tiefling fighter's Fire Bolt, an innate cantrip cast with Charisma.", cam, true);
        }

        [UnityTest]
        public IEnumerator ACloudGoliath_JauntsToAClickedSquare()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Controller();
            yield return EnterTown(bootstrap, runtime);
            var choices = LevelUpDriver.Choices("fighter", "goliath");
            choices.Choices["species:giant_ancestry"] = new List<string> { "cloud" };
            var created = session.Dispatch(new CreateCharacterCommand(choices));
            Assert.That(created.IsSuccess, Is.True, created.Message);
            yield return Settle();

            controller.AutoAi = false;
            controller.MouseDrivesCombat = false;
            Assert.That(controller.StartPracticeFight().IsSuccess, Is.True);
            yield return Settle(8);
            for (var i = 0; i < 10 && !controller.Combat.Snapshot.IsPlayersTurn; i++) { controller.RunAiTurn(); yield return Settle(2); }
            Assert.That(controller.Combat.Snapshot.IsPlayersTurn, Is.True);
            var cam = controller.CameraRig.Camera;
            var feature = controller.Combat.Snapshot.PlayerFeatures.FirstOrDefault(f => f.Feature.Id == "clouds_jaunt");
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature.NeedsPoint, Is.True);
            Assert.That(feature.Unavailable, Is.Null, "Usable now: only the square is missing.");

            controller.Combat.OpenGroupPopup("features");
            yield return Settle(2);
            var root = controller.Combat.Root;
            Click(root, "xk-combat-feature-clouds_jaunt");
            yield return Settle(2);
            Assert.That(controller.Combat.AimingFeature, Is.True);
            Assert.That(root.Q<Button>("xk-combat-feature-clouds_jaunt-cancel"), Is.Not.Null);
            yield return Shoot("p20a_09_combat_clouds_jaunt_aim", "Combat: Cloud's Jaunt waiting for a square on the field.", cam, true);

            var start = controller.Combat.Snapshot.Player.Position;
            var moved = false;
            var candidates = new List<GridPos>();
            for (var dx = -6; dx <= 6; dx++)
                for (var dy = -6; dy <= 6; dy++)
                {
                    var p = new GridPos(start.X + dx, start.Y + dy, start.Z);
                    var d = start.DistanceFeet(p);
                    if (d >= 15 && d <= 30) candidates.Add(p);
                }

            foreach (var p in candidates.OrderByDescending(q => start.DistanceFeet(q)))
            {
                if (!controller.Combat.AimingFeature) { controller.Combat.BeginFeatureAim("clouds_jaunt"); yield return null; }
                controller.Combat.Aim(p);
                yield return Settle(2);
                if (!controller.Combat.Snapshot.Player.Position.Equals(start)) { moved = true; break; }
            }

            Assert.That(moved, Is.True, "A free square within 30 ft was reached.");
            Assert.That(start.DistanceFeet(controller.Combat.Snapshot.Player.Position), Is.LessThanOrEqualTo(30));
            Assert.That(controller.Combat.Snapshot.Log.Any(l => l.Text.Contains("Cloud's Jaunt")), Is.True);
            Assert.That(session.GetCharacter().Player.Rpg.Resources["stones_endurance"], Is.EqualTo(1), "One of two uses spent.");
            yield return Settle(6);
            yield return Shoot("p20a_10_combat_after_jaunt", "Combat: the goliath after Cloud's Jaunt (one ancestry use left).", cam, false);
        }

        // ----- Captures ----------------------------------------------------------------------------------

        /// <summary>A close camera on the head of <paramref name="body"/>.</summary>
        private IEnumerator CloseUp(Transform body, string name, string note)
        {
            if (string.IsNullOrEmpty(_dir) || body == null) yield break;
            var head = body.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<SkinnedMeshRenderer>() == null);
            if (head == null) yield break;
            var go = new GameObject("p20a close-up");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 22f;
            cam.nearClipPlane = 0.02f;
            var face = head.position + Vector3.up * 0.08f;
            go.transform.position = face + body.forward * 0.55f;
            go.transform.LookAt(face);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            light.intensity = 1.2f;
            yield return null;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            Save(rt, 1280, 720, name);
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(go);
            Log.Add($"{name}: 1280x720. {note}");
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
                Save(rt, w, h, name + suffix);
                foreach (var (x, t, cl, sc) in previous) { x.targetTexture = t; x.clearColor = cl; x.scale = sc; }
                rt.Release();
                Object.Destroy(rt);
                Log.Add($"{name}{suffix}: {w}x{h}. {note}");
                if (!allSizes) break;
            }
        }

        private void Save(RenderTexture rt, int w, int h, string file)
        {
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, file + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
        }
    }
}
