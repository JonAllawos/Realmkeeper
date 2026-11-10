using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// The graphics pass in the shipped scene: the art catalog covers every module the planner can
    /// ask for, entering a town builds the kit environment within budget, every named resident has
    /// exactly one humanoid body keyed by its NPC ID, bodies animate from real movement, and leaving
    /// restores the strategic render settings.
    /// </summary>
    public sealed class GraphicsPlayModeTests
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

        [Test]
        public void Catalog_ProvidesEveryPlannedModule_AndValidCharacters()
        {
            var catalog = ArtCatalog.Load();
            Assert.That(catalog, Is.Not.Null, "The art catalog is set up (Realmkeeper ▸ Set Up Art).");
            foreach (var id in BuildingKitPlanner.KitModuleIds) Assert.That(catalog.Module(id), Is.Not.Null, id);
            Assert.That(catalog.Module("Prop_ExteriorBorder_Straight1"), Is.Not.Null);
            Assert.That(catalog.Module("Prop_WoodenFence_Single"), Is.Not.Null);
            Assert.That(catalog.Module("Prop_Wagon"), Is.Not.Null);
            foreach (var m in new[] { "Grass", "Cobble", "Dirt", "Gravel", "Plaster", "WoodTrim", "Wood", "Planks", "WallStone", "Sky", "Peasant", "Peasant2", "Ranger", "Ranger3" })
                Assert.That(catalog.Material(m), Is.Not.Null, m);
            Assert.That(catalog.Locomotion, Is.Not.Null);
            foreach (var body in catalog.Bodies)
            {
                var animator = body.Prefab.GetComponent<Animator>();
                var avatar = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman
                    ? animator.avatar
                    : HumanoidCharacterBuilder.FallbackAvatar(catalog, body.Id.StartsWith("Female", StringComparison.Ordinal) ? BodyFrame.Female : BodyFrame.Male);
                Assert.That(avatar != null && avatar.isValid && avatar.isHuman, Is.True, body.Id + " can be animated as a humanoid.");
            }

            foreach (var part in new[] { "Head_Male", "Head_Female", "Hair_Beard", "Hair_Long", "Hair_Buns", "Hair_SimpleParted" })
                Assert.That(catalog.Part(part), Is.Not.Null, part);
            Assert.That(Shader.Find("Universal Render Pipeline/Lit"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator EnteringATown_BuildsTheKitEnvironment_WithOneHumanoidPerResident()
        {
            var (bootstrap, controller) = Setup();
            for (var i = 0; i < 10; i++) yield return null; // the strategic environment is in place
            var fogBefore = RenderSettings.fog;
            var fogColourBefore = RenderSettings.fogColor;
            var ambientBefore = RenderSettings.ambientSkyColor;
            var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var shadowBefore = urp.shadowDistance;
            var cascadesBefore = urp.shadowCascadeCount;

            yield return Enter(bootstrap, controller, SettlementType.Town);
            Assert.That(controller.Catalog, Is.Not.Null, "The kit is used when the catalog exists.");
            var layout = controller.Streamer.Current;

            // Geometry: merged into few renderers, every building with a collider.
            var renderers = controller.EnvironmentRoot.GetComponentsInChildren<MeshRenderer>();
            Assert.That(renderers.Length, Is.InRange(50, 1500), "Merged static geometry stays within the draw budget.");
            foreach (var b in layout.Buildings)
            {
                var colliders = controller.EnvironmentRoot.transform.Find("Colliders/" + b.Key + " colliders");
                Assert.That(colliders, Is.Not.Null, b.Key);
                Assert.That(colliders.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThan(0), b.Key + " has collision.");
            }

            Assert.That(controller.EnvironmentRoot.GetComponentsInChildren<MeshCollider>(), Is.Empty, "No mesh colliders on detail.");

            // Residents: one body per persistent NPC ID, each a humanoid whose look comes from that ID.
            var expected = layout.Residents.Select(r => r.NpcId).Distinct().ToList();
            Assert.That(controller.Residents.Keys, Is.EquivalentTo(expected));
            Assert.That(Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None).Length, Is.EqualTo(expected.Count), "No duplicate bodies.");
            foreach (var pair in controller.Residents)
            {
                var view = pair.Value;
                Assert.That(view.NpcId, Is.EqualTo(pair.Key));
                Assert.That(view.Animation, Is.Not.Null, "A humanoid body, not the capsule fallback.");
                Assert.That(view.Animation.Animator.avatar != null && view.Animation.Animator.avatar.isHuman, Is.True);
                var npc = bootstrap.Session.FindNpc(pair.Key);
                Assert.That(view.Appearance.Signature, Is.EqualTo(NpcAppearanceResolver.Resolve(npc.NpcId, npc.Role, npc.Name,
                    npc.Sex == NpcSex.Female ? BodyFrame.Female : npc.Sex == NpcSex.Male ? BodyFrame.Male : (BodyFrame?)null).Signature),
                    "The body shows the appearance resolved from the resident's identity.");
                Assert.That(view.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.GreaterThanOrEqualTo(4), "Body, head and hair parts.");
            }

            Assert.That(controller.Player.Animation, Is.Not.Null, "The player has a humanoid body too.");

            // Leaving restores the strategic map's render settings.
            Assert.That(controller.ReturnToWorldMap().IsSuccess, Is.True);
            yield return null;
            yield return null;
            Assert.That(RenderSettings.fog, Is.EqualTo(fogBefore));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fogColourBefore));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(ambientBefore));
            Assert.That(urp.shadowDistance, Is.EqualTo(shadowBefore));
            Assert.That(urp.shadowCascadeCount, Is.EqualTo(cascadesBefore));
        }

        [UnityTest]
        public IEnumerator SameLayout_BuildsTheSameScene()
        {
            var (bootstrap, controller) = Setup();
            yield return Enter(bootstrap, controller, SettlementType.Village);
            var layout = controller.Streamer.Current;
            var catalog = controller.Catalog;
            var a = ModularEnvironmentBuilder.Build(layout, catalog);
            var b = ModularEnvironmentBuilder.Build(layout, catalog);
            yield return null; // merged-away renderers are destroyed at the end of the frame
            string Fingerprint(GameObject root) => string.Join("\n", root.GetComponentsInChildren<MeshFilter>()
                .Select(f => f.name + ":" + f.sharedMesh.vertexCount + ":" + f.GetComponent<MeshRenderer>().sharedMaterial.name + ":" + f.sharedMesh.bounds.center.ToString("F3")));
            Assert.That(Fingerprint(b), Is.EqualTo(Fingerprint(a)));
            string Colliders(GameObject root) => string.Join("\n", root.GetComponentsInChildren<BoxCollider>()
                .Select(c => c.name + c.transform.position.ToString("F2") + c.size.ToString("F2")));
            Assert.That(Colliders(b), Is.EqualTo(Colliders(a)));
            Object.Destroy(a);
            Object.Destroy(b);
            yield return null;
            Assert.That(controller.ReturnToWorldMap().IsSuccess, Is.True);
        }

        [UnityTest]
        public IEnumerator Bodies_AnimateFromRealMovement_AndTalking()
        {
            var (bootstrap, controller) = Setup();
            var input = new ScriptedExplorationInput();
            controller.SetInput(input);
            yield return Enter(bootstrap, controller, SettlementType.Town);
            var driver = controller.Player.Animation;
            var settle = 0f;
            while (settle < 0.6f)
            {
                settle += Time.deltaTime;
                yield return null;
            }

            Assert.That(driver.Speed, Is.LessThan(0.1f), "Standing still is idle.");

            input.Move = new Vector2(0f, 1f);
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.That(driver.Speed, Is.GreaterThan(1.5f), "Walking drives the locomotion blend.");
            Assert.That(driver.Animator.GetFloat("Speed"), Is.EqualTo(driver.Speed).Within(0.01f));
            input.Move = Vector2.zero;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.That(driver.Speed, Is.LessThan(0.1f), "Stopping returns to idle.");

            var resident = controller.Residents.Values.First();
            controller.Player.Teleport(new LocalPose(resident.transform.position.x + 1.2f, 0.1f, resident.transform.position.z, 270f));
            var response = resident.Interact(bootstrap.Session, controller.Player.transform);
            Assert.That(response.Succeeded, Is.True, response.Text);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(resident.Animation.Talking, Is.True, "A conversation plays the talking animation.");
            Assert.That(resident.Animation.Animator.GetBool("Talking"), Is.True);
            Assert.That(controller.ReturnToWorldMap().IsSuccess, Is.True);
        }

        private static (RealmkeeperBootstrap, ExplorationController) Setup()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var installer = Object.FindAnyObjectByType<ExplorationInstaller>();
            installer.Controller.ManageCursor = false;
            installer.Controller.SetInput(new ScriptedExplorationInput());
            return (bootstrap, installer.Controller);
        }

        private static IEnumerator Enter(RealmkeeperBootstrap bootstrap, ExplorationController controller, SettlementType type)
        {
            var settlement = bootstrap.WorldView.Settlements.Values.First(v => v.SettlementType == type);
            bootstrap.Selection.Select(settlement.SettlementId);
            var result = bootstrap.Hud.EnterSelectedSettlement();
            Assert.That(result.IsSuccess, Is.True, result.ToString());
            yield return null;
            yield return null;
            Assert.That(controller.IsExploring, Is.True);
        }
    }
}
