using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 29 (ADR-0122 with ADR-0115): the overland ground and its roads show the ground's weather through their own
    /// materials, on the old mesh ground and the Unity Terrain alike. In the running game, out in the country: wet and deep-snow
    /// conditions from the real rule (GroundConditions.Of over a region's weather) are applied the way the controller applies
    /// them, then the atmosphere's real weather pass runs (it adopts the scene's ground, roof and leaf materials). The ground
    /// and the roads must still be drawn with their own materials, carrying the conditions' _Wet and _Snow. Before this, the
    /// atmosphere replaced the mesh ground's and the roads' materials with tinted copies, which hid _Wet and _Snow.
    /// </summary>
    public sealed class Phase29GroundWeatherPlayModeTests
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
            OverlandWorld.UseUnityTerrain = true;
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        /// <summary>The ground and road surfaces as drawn, with the material each should be drawn with.</summary>
        private static List<(string what, Material drawn, Material own)> Surfaces(OverlandWorld w)
        {
            var list = new List<(string, Material, Material)>();
            if (w.UnityTerrain != null) list.Add(("terrain", w.UnityTerrain.materialTemplate, w.GroundMaterial));
            foreach (var r in w.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "Roads") list.Add(("roads", r.sharedMaterial, w.RoadMaterial));
                else if (r.name.StartsWith("Chunk ")) list.Add((r.name, r.sharedMaterial, w.GroundMaterial));
            return list;
        }

        private static (float wet, float snow) Mapping(GroundConditions g) =>
            (g.Wetness >= 40 ? Mathf.InverseLerp(40f, 90f, g.Wetness) : 0f, g.Snow >= 10 ? Mathf.InverseLerp(10f, 60f, g.Snow) : 0f);

        private static void AssertShows(OverlandWorld w, (float wet, float snow) want, string context)
        {
            var surfaces = Surfaces(w);
            Assert.That(surfaces.Any(s => s.what == "roads"), Is.True, context + ": the roads (and their bridge decks) were found.");
            foreach (var (what, drawn, own) in surfaces)
            {
                Assert.That(drawn, Is.SameAs(own), $"{context}: {what} is drawn with its own material.");
                Assert.That(drawn.GetFloat("_Wet"), Is.EqualTo(want.wet).Within(1e-3f), $"{context}: {what} wetness.");
                Assert.That(drawn.GetFloat("_Snow"), Is.EqualTo(want.snow).Within(1e-3f), $"{context}: {what} snow.");
            }
        }

        private static IEnumerator Settle(int n)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private static IEnumerator Leave(ExplorationController c)
        {
            c.ReturnToWorldMap();
            for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
            yield return Settle(6);
            Assert.That(c.IsExploring, Is.False, "On the map.");
        }

        private static IEnumerator WalkIn(ExplorationController c, RealmkeeperBootstrap b, WorldSession s, SettlementId town)
        {
            b.Selection.Select(town);
            yield return Settle(2);
            var onRoad = s.GetAtlas().Journey != null && s.GetPlayer().LocationKind == CharacterLocationKind.WorldMap;
            var r = onRoad ? s.Dispatch(new EnterOverlandCommand()) : b.Hud.WalkCountryBySelected();
            Assert.That(r.IsSuccess, Is.True, r.Message);
            for (var i = 0; i < 600 && !(c.InOverland && !c.Travelling); i++) yield return null;
            Assert.That(c.InOverland, Is.True, "Into the country.");
            yield return Settle(10);
        }

        /// <summary>Every fixture on the current ground: shown, then three atmosphere passes, and it must still be shown.</summary>
        private static IEnumerator Sweep(ExplorationController c, WorldSession s, Dictionary<string, (float wet, float snow, bool flooded, int barriers)> results)
        {
            var w = c.Overland;
            var live = s.GetWeather();
            var baseColours = Surfaces(w).Select(x => x.own).Distinct().ToDictionary(m => m, m => m.GetColor("_BaseColor"));
            var fixtures = GroundWeatherFixtures.All;
            for (var round = 0; round < 2; round++) // twice over: no compounding after repeated changes
                foreach (var f in fixtures)
                {
                    Assert.That(f.Conditions.State, Is.EqualTo(f.State), $"{f.Name}: the fixture is the state it says.");
                    Assert.That(f.Conditions.FordsFlooded, Is.EqualTo(f.Flooded), $"{f.Name}: flooding.");
                    GroundWeatherFixtures.Show(c, f, live);
                    // Atmosphere transitions (other skies) must not erase or tint the simulated ground.
                    foreach (var other in new[] { GroundWeatherFixtures.DeepSnow, GroundWeatherFixtures.Wet, GroundWeatherFixtures.Dry })
                        c.Atmosphere.SetWeather(GroundWeatherFixtures.Summary(other, live));
                    yield return null;
                    var mode = w.UnityTerrain != null ? "Unity Terrain" : "mesh ground";
                    AssertShows(w, f.Expected, $"{mode}, {f.Name} (round {round + 1})");
                    Assert.That(w.FordsFlooded, Is.EqualTo(f.Flooded), $"{mode}, {f.Name}: fords.");
                    foreach (var kv in baseColours)
                        Assert.That(kv.Key.GetColor("_BaseColor"), Is.EqualTo(kv.Value), $"{mode}, {f.Name}: {kv.Key.name} is never tinted.");
                    results[f.Name] = (f.Expected.wet, f.Expected.snow, w.FordsFlooded, w.BarrierColliders);
                }
        }

        [UnityTest]
        public IEnumerator EveryGroundState_ShowsTheSameOnBothGrounds_SurvivesTheAtmosphere_AndTheAuthorityReturnsOnReEntryAndLoad()
        {
            OverlandWorld.UseUnityTerrain = true;
            var b = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var s = b.Session;
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            var town = s.GetSettlementsInView().First(x => x.SettlementType == SettlementType.Town).SettlementId;
            b.Selection.Select(town);
            Assert.That(b.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(6);
            runtime.Playback.Pause(); // the live weather cannot move under the fixtures
            Assert.That(s.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter"))).IsSuccess, Is.True);
            yield return null;
            c.GoOutIntoCountry();
            for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
            Assert.That(c.InOverland && c.Overland.UnityTerrain != null, Is.True, "Out on the Unity Terrain.");
            yield return Settle(10);
            var authority = s.GetGroundConditions();
            AssertShows(c.Overland, Mapping(authority), "Unity Terrain on entry (the simulated ground)");

            var terrain = new Dictionary<string, (float wet, float snow, bool flooded, int barriers)>();
            yield return Sweep(c, s, terrain);

            // The mesh ground: the same states, the same presentation.
            yield return Leave(c);
            OverlandWorld.UseUnityTerrain = false;
            yield return WalkIn(c, b, s, town);
            Assert.That(c.Overland.UnityTerrain, Is.Null, "On the mesh ground.");
            AssertShows(c.Overland, Mapping(s.GetGroundConditions()), "mesh ground on entry (the simulated ground)");
            var mesh = new Dictionary<string, (float wet, float snow, bool flooded, int barriers)>();
            yield return Sweep(c, s, mesh);
            foreach (var f in GroundWeatherFixtures.All)
            {
                Assert.That(mesh[f.Name].wet, Is.EqualTo(terrain[f.Name].wet).Within(1e-4f), f.Name);
                Assert.That(mesh[f.Name].snow, Is.EqualTo(terrain[f.Name].snow).Within(1e-4f), f.Name);
                Assert.That(mesh[f.Name].flooded, Is.EqualTo(terrain[f.Name].flooded), f.Name);
                TestContext.WriteLine($"{f.Name} ({f.State}): _Wet {terrain[f.Name].wet:0.00}, _Snow {terrain[f.Name].snow:0.00}, fords flooded {terrain[f.Name].flooded}; barriers terrain {terrain[f.Name].barriers}, mesh {mesh[f.Name].barriers}.");
            }

            Assert.That(terrain["flooded"].barriers, Is.GreaterThanOrEqualTo(terrain["dry"].barriers), "A flood adds ford barriers, never removes any (terrain).");
            Assert.That(mesh["flooded"].barriers, Is.GreaterThanOrEqualTo(mesh["dry"].barriers), "And on the mesh ground.");

            // Re-entry (the cached terrain): the simulated ground is shown again, not the last fixture.
            yield return Leave(c);
            OverlandWorld.UseUnityTerrain = true;
            yield return WalkIn(c, b, s, town);
            Assert.That(c.Overland.TerrainFromCache, Is.True, "Re-entered from the cache.");
            AssertShows(c.Overland, Mapping(s.GetGroundConditions()), "Unity Terrain on re-entry");

            // Save and load in the country: the same.
            GroundWeatherFixtures.Show(c, GroundWeatherFixtures.DeepSnow, s.GetWeather()); // a stale look that must not survive the load
            Assert.That(s.Execute(new SaveWorldCommand("p29weather")).IsSuccess, Is.True);
            Assert.That(s.Execute(new LoadWorldCommand("p29weather")).IsSuccess, Is.True);
            yield return Settle(12);
            Assert.That(c.InOverland, Is.True, "Loaded into the country.");
            AssertShows(c.Overland, Mapping(s.GetGroundConditions()), "after a save and load");
            TestContext.WriteLine($"Simulated ground at the test's hour: {s.GetGroundConditions().State} (wetness {s.GetGroundConditions().Wetness}, snow {s.GetGroundConditions().Snow}).");
        }

        [UnityTest]
        public IEnumerator OnTheUnityTerrain_TheGroundAndRoadsShowWetAndSnow() => Check(true);

        [UnityTest]
        public IEnumerator OnTheMeshGround_TheGroundAndRoadsShowWetAndSnow() => Check(false);

        private static IEnumerator Check(bool unityTerrain)
        {
            OverlandWorld.UseUnityTerrain = unityTerrain;
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            runtime.Playback.Pause();
            Assert.That(session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter"))).IsSuccess, Is.True);
            yield return null;
            c.GoOutIntoCountry();
            for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
            Assert.That(c.InOverland, Is.True);
            var w = c.Overland;
            Assert.That(w.UnityTerrain != null, Is.EqualTo(unityTerrain));
            for (var i = 0; i < 10; i++) yield return null;

            foreach (var (name, weather, wet, snow) in new[]
                     {
                         ("wet", new RegionWeatherData { GroundWetness = 80 }, 0.8f, 0f),
                         ("deep snow", new RegionWeatherData { SnowCover = 70 }, 0f, 1f),
                         ("dry", new RegionWeatherData(), 0f, 0f),
                     })
            {
                var g = GroundConditions.Of(weather, 600);
                w.ApplyGroundConditions(g); // as the controller does when the conditions change
                c.Atmosphere.SetWeather(session.GetWeather()); // the atmosphere's real pass: it adopts the scene again
                yield return null;

                // What the ground and the roads are actually drawn with.
                var drawn = new List<(string what, Material m)>();
                if (unityTerrain) drawn.Add(("terrain", w.UnityTerrain.materialTemplate));
                foreach (var r in w.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name == "Roads" || r.name.StartsWith("Chunk ")) drawn.Add((r.name, r.sharedMaterial));
                Assert.That(drawn.Count, Is.GreaterThan(1), "The ground and the roads were found.");
                Assert.That(drawn.Any(d => d.what == "Roads"), Is.True);
                foreach (var (what, m) in drawn)
                {
                    Assert.That(m, Is.SameAs(what == "Roads" ? w.RoadMaterial : w.GroundMaterial), $"{name}: {what} is drawn with its own material, not an atmosphere copy.");
                    Assert.That(m.GetFloat("_Wet"), Is.EqualTo(wet).Within(1e-3f), $"{name}: {what} wetness.");
                    Assert.That(m.GetFloat("_Snow"), Is.EqualTo(snow).Within(1e-3f), $"{name}: {what} snow.");
                }

                TestContext.WriteLine($"{(unityTerrain ? "Unity Terrain" : "mesh ground")}, {name} ({g.State}): {drawn.Count} ground and road surfaces show _Wet {wet:0.00}, _Snow {snow:0.00}.");
            }
        }
    }
}
