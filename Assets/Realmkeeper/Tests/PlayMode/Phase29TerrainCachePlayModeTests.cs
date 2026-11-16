using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
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
    /// Phase 29 (ADR-0122): the per-region Unity Terrain cache. A first build misses and a repeat entry hits with the very same
    /// heights and textures; the ground and its collider stand where the game's own heights say, built or cached; regions
    /// switch, evict least recently used first, and a new terrain object (a new seed, a loaded save) is never served stale
    /// ground; ground weather reaches a cached terrain; the old mesh ground still works from the same heights and leaves the
    /// cache alone. Overland worlds are built here directly from the session's terrains (no trees: no catalog).
    /// </summary>
    public sealed class Phase29TerrainCachePlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private Transform _root;
        public static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            for (var i = 0; i < 8; i++) yield return null;
            _root = new GameObject("Terrain cache test").transform;
            OverlandTerrainCache.Clear();
            OverlandTerrainCache.ResetCounters();
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            OverlandWorld.UseUnityTerrain = true;
            OverlandTerrainCache.Capacity = 3;
            OverlandTerrainCache.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static WorldSession Session => Object.FindAnyObjectByType<RealmkeeperBootstrap>().Session;

        private static List<RegionId> Regions() => Session.GetRegions().Select(r => r.RegionId).Distinct().ToList();

        private static OverlandTerrain TerrainOf(RegionId region) => Session.GetOverlandTerrain(RealmIds.Surface, region);

        private OverlandWorld Build(OverlandTerrain terrain, string name) =>
            OverlandWorld.Build(_root, new OverlandView { Terrain = terrain, RegionName = name, RegionKind = terrain.Kind, Profile = null }, null);

        /// <summary>Heightmap grid points across the region (where the terrain's samples are the game's heights exactly).</summary>
        private static IEnumerable<Vector2> GridPoints(OverlandTerrain t)
        {
            var res = OverlandWorld.HeightmapResolution;
            foreach (var (i, j) in new[] { (3, 5), (100, 900), (512, 512), (800, 40), (1000, 1000), (257, 640), (700, 333), (64, 64) })
                yield return new Vector2(i * t.SizeX / (res - 1), j * t.SizeZ / (res - 1));
        }

        /// <summary>The largest gap between the drawn ground, its collider and the game's height, over the grid points.</summary>
        private static (float drawn, float collider) Gaps(OverlandWorld w, OverlandTerrain t)
        {
            Physics.SyncTransforms();
            float drawn = 0f, collider = 0f;
            foreach (var p in GridPoints(t))
            {
                var world = w.transform.TransformPoint(new Vector3(p.x, 0f, p.y));
                var truth = w.transform.TransformPoint(new Vector3(p.x, t.HeightAt(p.x, p.y), p.y)).y;
                drawn = Mathf.Max(drawn, Mathf.Abs(w.UnityTerrain.SampleHeight(world) + w.UnityTerrain.transform.position.y - truth));
                var col = w.UnityTerrain.GetComponent<TerrainCollider>();
                Assert.That(col.Raycast(new Ray(new Vector3(world.x, truth + 500f, world.z), Vector3.down), out var hit, 2000f), Is.True, $"The collider is under ({p.x:0},{p.y:0}).");
                collider = Mathf.Max(collider, Mathf.Abs(hit.point.y - truth));
            }

            return (drawn, collider);
        }

        [UnityTest]
        public IEnumerator AFirstEntryBuilds_ARepeatEntryReusesTheSameGround_AlignedWithTheGamesHeights()
        {
            var region = Regions()[0];
            var t = TerrainOf(region);
            var first = Build(t, "first");
            Assert.That(first.UnityTerrain, Is.Not.Null, "Unity Terrain is the default.");
            Assert.That(first.TerrainFromCache, Is.False);
            Assert.That((OverlandTerrainCache.Hits, OverlandTerrainCache.Misses), Is.EqualTo((0, 1)));
            var data = first.UnityTerrain.terrainData;
            var lo = first.UnityTerrain.transform.localPosition.y;
            var (d1, c1) = Gaps(first, t);
            var builtMs = first.TerrainBuildMs;
            Object.Destroy(first.gameObject);
            yield return null;
            yield return null;

            Assert.That(data != null, Is.True, "The cache keeps the heights after the overland world that built them is gone.");
            var again = Build(t, "again");
            Assert.That(again.TerrainFromCache, Is.True);
            Assert.That((OverlandTerrainCache.Hits, OverlandTerrainCache.Misses), Is.EqualTo((1, 1)));
            Assert.That(again.UnityTerrain.terrainData, Is.SameAs(data), "The same heights, not a rebuild.");
            Assert.That(again.UnityTerrain.transform.localPosition.y, Is.EqualTo(lo));
            var ground = again.UnityTerrain.materialTemplate;
            Assert.That(ground.shader.name, Is.EqualTo("Realmkeeper/TerrainGround"));
            Assert.That(ground.GetTexture("_BiomeColour"), Is.Not.Null.And.SameAs(OverlandTerrainCache.Find(t).Colour));
            Assert.That(ground.GetTexture("_GroundWeights"), Is.Not.Null);
            var (d2, c2) = Gaps(again, t);
            Assert.That(d1, Is.LessThan(0.05f), "Built: drawn where the game's heights are.");
            Assert.That(c1, Is.LessThan(0.05f), "Built: the collider there too.");
            Assert.That(d2, Is.EqualTo(d1).Within(1e-4f), "Cached: exactly as built.");
            Assert.That(c2, Is.EqualTo(c1).Within(1e-4f));
            Assert.That(again.TerrainBuildMs, Is.LessThan(builtMs), "Cheaper than the build.");

            // Ground weather reaches a cached terrain's material.
            again.SetGroundWeather(0.6f, 0.8f);
            Assert.That(ground.GetFloat("_Wet"), Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(ground.GetFloat("_Snow"), Is.EqualTo(0.8f).Within(1e-4f));
            Log.Add($"Built {builtMs:0} ms, cached {again.TerrainBuildMs:0} ms; gaps drawn {d1:0.000} m, collider {c1:0.000} m; retained {OverlandTerrainCache.RetainedBytes / 1048576f:0.0} MB.");
            foreach (var l in Log) TestContext.WriteLine(l);
        }

        [UnityTest]
        public IEnumerator RegionsSwitch_TheLeastRecentlyUsedIsEvicted_AndANewTerrainIsNeverServedOldGround()
        {
            var regions = Regions();
            Assume.That(regions.Count, Is.GreaterThanOrEqualTo(2), "Two regions to switch between.");
            var a = TerrainOf(regions[0]);
            var b = TerrainOf(regions[1]);

            Object.Destroy(Build(a, "a").gameObject);
            Object.Destroy(Build(b, "b").gameObject);
            yield return null;
            var aAgain = Build(a, "a again");
            Assert.That(aAgain.TerrainFromCache, Is.True, "Back after visiting another region: a hit.");
            Assert.That(OverlandTerrainCache.Count, Is.EqualTo(2));
            Object.Destroy(aAgain.gameObject);
            yield return null;

            // Room for one: each new region evicts the last.
            OverlandTerrainCache.Capacity = 1;
            var evictions = OverlandTerrainCache.Evictions;
            var bThird = Build(b, "b, room for one");
            Assert.That(bThird.TerrainFromCache, Is.True, "b was the most recent: still there.");
            Assert.That(OverlandTerrainCache.Evictions, Is.EqualTo(evictions + 1), "The lowered capacity applies at once: a is evicted.");
            Assert.That(OverlandTerrainCache.Count, Is.EqualTo(1));
            Object.Destroy(bThird.gameObject);
            yield return null;
            evictions = OverlandTerrainCache.Evictions;
            var aThird = Build(a, "a, room for one");
            Assert.That(aThird.TerrainFromCache, Is.False, "Evicted, so rebuilt.");
            Assert.That(OverlandTerrainCache.Evictions, Is.GreaterThan(evictions));
            Assert.That(OverlandTerrainCache.Count, Is.EqualTo(1));
            Object.Destroy(aThird.gameObject);
            yield return null;
            OverlandTerrainCache.Capacity = 3;

            // A new terrain object for the same map (as a new or loaded world brings) is never matched.
            var fresh = new OverlandTerrain(a.Map, a.Kind, 424242);
            Assert.That(OverlandTerrainCache.Find(fresh), Is.Null, "A different terrain object: a miss.");
            Assert.That(OverlandTerrainCache.Fingerprint(fresh), Is.Not.EqualTo(OverlandTerrainCache.Fingerprint(new OverlandTerrain(a.Map, a.Kind, 777))), "The fingerprint follows the ground.");
            Assert.That(OverlandTerrainCache.Fingerprint(a), Is.EqualTo(OverlandTerrainCache.Fingerprint(a)), "Deterministic.");

            // Saving and loading replaces the session's terrains: the next entry builds, never reuses the old world's ground.
            Assert.That(Session.Execute(new SaveWorldCommand("p29cache")).IsSuccess, Is.True);
            Assert.That(Session.Execute(new LoadWorldCommand("p29cache")).IsSuccess, Is.True);
            yield return null;
            var loaded = TerrainOf(regions[0]);
            Assert.That(loaded, Is.Not.SameAs(a), "A loaded world brings its own terrain.");
            var afterLoad = Build(loaded, "after load");
            Assert.That(afterLoad.TerrainFromCache, Is.False, "No stale ground after a load.");
            var again = Build(loaded, "after load, again");
            Assert.That(again.TerrainFromCache, Is.True, "Then cached as usual.");
            Assert.That(OverlandTerrainCache.Count, Is.LessThanOrEqualTo(3), "Never more than the capacity.");
            TestContext.WriteLine($"Hits {OverlandTerrainCache.Hits}, misses {OverlandTerrainCache.Misses}, evictions {OverlandTerrainCache.Evictions}, retained {OverlandTerrainCache.RetainedBytes / 1048576f:0.0} MB in {OverlandTerrainCache.Count} region(s).");
        }

        [UnityTest]
        public IEnumerator TheOldMeshGround_StillWorks_FromTheSameHeights_AndLeavesTheCacheAlone()
        {
            var t = TerrainOf(Regions()[0]);
            OverlandWorld.UseUnityTerrain = false;
            var (hits, misses) = (OverlandTerrainCache.Hits, OverlandTerrainCache.Misses);
            var w = Build(t, "mesh ground");
            Assert.That(w.UnityTerrain, Is.Null, "The fallback draws the chunk meshes.");
            Assert.That(w.CoarseChunks, Is.GreaterThan(0));
            Assert.That((OverlandTerrainCache.Hits, OverlandTerrainCache.Misses), Is.EqualTo((hits, misses)), "The fallback does not touch the cache.");
            var at = new Vector3(t.SizeX * 0.5f, 0f, t.SizeZ * 0.5f);
            w.Prewarm(w.transform.TransformPoint(at));
            yield return null;
            Physics.SyncTransforms();
            var worst = 0f;
            for (var i = 0; i < 6; i++)
            {
                var x = Mathf.Round((at.x + (i - 3) * 37f) / 10f) * 10f; // the detail mesh's vertices: every 10 m
                var z = Mathf.Round((at.z + (i - 2) * 23f) / 10f) * 10f;
                var truth = w.transform.TransformPoint(new Vector3(x, t.HeightAt(x, z), z)).y;
                var hitAny = Physics.RaycastAll(w.transform.TransformPoint(new Vector3(x, t.HeightAt(x, z) + 300f, z)), Vector3.down, 1000f)
                    .Where(h => h.collider is MeshCollider && h.collider.transform.IsChildOf(w.transform)).ToList();
                Assert.That(hitAny, Is.Not.Empty, $"The mesh ground's collider is under ({x},{z}).");
                worst = Mathf.Max(worst, Mathf.Abs(hitAny.OrderBy(h => h.distance).First().point.y - truth));
            }

            Assert.That(worst, Is.LessThan(0.05f), "The fallback stands on the same heights.");
            Object.Destroy(w.gameObject);
            OverlandWorld.UseUnityTerrain = true;
        }
    }
}
