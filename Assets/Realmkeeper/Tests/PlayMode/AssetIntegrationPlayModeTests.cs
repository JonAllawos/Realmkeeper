using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// The free assets integrated after the asset research (2026-11-12): the rest of the village kit in the catalog, the
    /// KayKit weapons and owned props drawing items, the Kenney sounds, the second animation library and the town kit.
    /// With RK_CAPTURE_DIR set it also writes a gallery of every item that now has an art model.
    /// </summary>
    public sealed class AssetIntegrationPlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;

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
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [UnityTest]
        public IEnumerator TheCatalog_HasTheNewPieces_AndItemsAreDrawnFromArtModels()
        {
            var catalog = ArtCatalog.Load();
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Modules.Count, Is.EqualTo(176), "The whole free village kit.");
            Assert.That(catalog.Module("Overhang_Plaster_Long"), Is.Not.Null);
            Assert.That(catalog.Module("Wall_Plaster_Straight"), Is.Not.Null, "The earlier pieces keep their IDs.");
            Assert.That(catalog.Scenery.Count(e => e.Id.StartsWith("weapon_", StringComparison.Ordinal)), Is.EqualTo(31));
            Assert.That(catalog.Scenery.Count(e => e.Id.StartsWith("town_", StringComparison.Ordinal)), Is.EqualTo(167));
            Assert.That(catalog.Scenery.Select(e => e.Id).Distinct().Count(), Is.EqualTo(catalog.Scenery.Count), "No scenery ID is taken twice.");
            Assert.That(catalog.SceneryModel("Anvil"), Is.Not.Null, "Earlier scenery keeps its IDs.");

            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            var holder = new GameObject("Item gallery");
            holder.transform.position = new Vector3(0f, 500f, 0f);
            var assets = holder.AddComponent<GeneratedAssets>();
            var x = 0f;
            var built = new System.Collections.Generic.List<(string id, GameObject model)>();
            foreach (var pair in ItemModels.ArtModels.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                Assert.That(ItemCatalog.Find(pair.Key), Is.Not.Null, pair.Key + " is an item.");
                Assert.That(catalog.SceneryModel(pair.Value.model), Is.Not.Null, pair.Key + " → " + pair.Value.model);
                var slot = new GameObject(pair.Key);
                slot.transform.SetParent(holder.transform, false);
                slot.transform.localPosition = new Vector3(x, 0f, 0f);
                var model = ItemModels.Build(slot.transform, pair.Key, catalog, assets);
                Assert.That(model, Is.Not.Null, pair.Key);
                Assert.That(model.transform.Cast<Transform>().Any(t => t.name.StartsWith("Art ", StringComparison.Ordinal)), Is.True, pair.Key + " uses its art model.");
                built.Add((pair.Key, model));
                x += 0.9f;
            }

            // A person-height post beside the row, for scale in the capture.
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.transform.SetParent(holder.transform, false);
            post.transform.localScale = new Vector3(0.05f, 1.75f, 0.05f);
            post.transform.localPosition = new Vector3(-0.9f, 0.875f, 0f);

            // An item with no art model keeps its built shape.
            var plain = ItemModels.Build(holder.transform, "bread_loaf", catalog, assets);
            Assert.That(plain.transform.Cast<Transform>().Any(t => t.name.StartsWith("Art ", StringComparison.Ordinal)), Is.False);
            plain.transform.localPosition = new Vector3(x, 0f, 0f);
            yield return null;
            foreach (var (id, model) in built) // renderer bounds are valid a frame after the model is made
            {
                var size = BoundsOf(model).size;
                Assert.That(Mathf.Max(size.x, size.y, size.z), Is.InRange(0.04f, 2.1f), id + " is a believable size: " + size.ToString("F2"));
            }


            if (!string.IsNullOrEmpty(_dir))
            {
                var n = ItemModels.ArtModels.Count;
                for (var page = 0; page < 3; page++)
                {
                    var go = new GameObject("gallery camera");
                    var cam = go.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 1.25f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.16f, 0.15f, 0.14f);
                    var centre = holder.transform.position + new Vector3((page * 9 + 4) * 0.9f - 0.45f, 0.75f, 0f);
                    go.transform.position = centre + new Vector3(0f, 0.6f, 4f);
                    go.transform.LookAt(centre);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.1f;
                    var rt = new RenderTexture(1920, 600, 24, RenderTextureFormat.ARGB32);
                    cam.targetTexture = rt;
                    cam.Render();
                    var active = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(1920, 600, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1920, 600), 0, 0);
                    tex.Apply();
                    RenderTexture.active = active;
                    File.WriteAllBytes(Path.Combine(_dir, $"assets_item_gallery_{page + 1}.jpg"), tex.EncodeToJPG(90));
                    Object.Destroy(tex);
                    cam.targetTexture = null;
                    rt.Release();
                    Object.Destroy(rt);
                    Object.Destroy(go);
                    yield return null;
                }

                File.WriteAllLines(Path.Combine(_dir, "assets_item_gallery.txt"),
                    ItemModels.ArtModels.OrderBy(p => p.Key, StringComparer.Ordinal).Select((p, i) => $"{i + 1}. {p.Key} -> {p.Value.model} x{p.Value.scale}"));
            }

            Object.Destroy(holder);
            Assert.That(controller, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TheSounds_Load_AndFollowTheVolumeSetting()
        {
            foreach (var pair in GameSounds.Takes)
                foreach (var take in pair.Value)
                    Assert.That(GameSounds.Clip(take), Is.Not.Null, pair.Key + ": " + take);

            var volume = RkSettings.SoundVolume;
            try
            {
                GameSounds.ResetCounters();
                RkSettings.SoundVolume = 0f;
                GameSounds.Play("coins");
                Assert.That(GameSounds.Requested, Is.EqualTo(1), "Counted even when silent.");
                Assert.That(GameSounds.Last, Is.EqualTo("coins"));
                RkSettings.SoundVolume = 0.5f;
                GameSounds.Play("door-open");
                GameSounds.Play("no-such-sound");
                Assert.That(GameSounds.Requested, Is.EqualTo(2), "An unknown sound is ignored.");
                RkSettings.SoundVolume = 3f;
                Assert.That(RkSettings.SoundVolume, Is.EqualTo(1f), "Clamped.");

                // The character sheet opens and closes with a page.
                var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
                var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
                var town = bootstrap.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
                bootstrap.Selection.Select(town.SettlementId);
                Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
                for (var i = 0; i < 6; i++) yield return null;
                GameSounds.ResetCounters();
                controller.ToggleHub();
                yield return null;
                Assert.That(GameSounds.Last, Is.EqualTo("book-open"));
                controller.ToggleHub();
                yield return null;
                Assert.That(GameSounds.Last, Is.EqualTo("book-close"));
            }
            finally
            {
                RkSettings.SoundVolume = volume;
            }
        }

#if UNITY_EDITOR
        [Test]
        public void TheSecondAnimationLibrary_IsImportedOnTheHumanoidRig()
        {
            const string path = "Assets/Realmkeeper/Art/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard.fbx";
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.animationType, Is.EqualTo(UnityEditor.ModelImporterAnimationType.Human));
            var clips = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToList();
            Assert.That(clips.Count, Is.GreaterThanOrEqualTo(40), string.Join(", ", clips.Select(c => c.name)));
            Assert.That(clips.All(c => c.isHumanMotion), Is.True, "Retargetable onto the people's rig.");
            TestContext.WriteLine("UAL2 clips: " + string.Join(", ", clips.Select(c => c.name).OrderBy(n => n)));
        }
#endif
    }
}
