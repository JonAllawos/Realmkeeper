using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 29 visual parity (ADR-0122). The Unity Terrain's colour is the mesh ground's colour: the biome colour texture is
    /// linear (not sRGB, which converted the already-linear colours a second time and darkened the ground) and half-float
    /// (colours above 1, the shade and snowy peaks, are not clipped); read back from the GPU it holds exactly what the mesh
    /// ground puts in its vertex colours. Roads and the bridge decks they carry keep their own vertex colours in their own
    /// Realmkeeper/VertexColorLit material, sit on the terrain, and take the ground weather. The mesh fallback is unchanged.
    /// </summary>
    public sealed class Phase29TerrainParityPlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private Transform _root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            for (var i = 0; i < 8; i++) yield return null;
            _root = new GameObject("Terrain parity test").transform;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            OverlandWorld.UseUnityTerrain = true;
            OverlandTerrainCache.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static OverlandTerrain Terrain()
        {
            var s = Object.FindAnyObjectByType<RealmkeeperBootstrap>().Session;
            return s.GetOverlandTerrain(RealmIds.Surface, s.GetRegions().First().RegionId);
        }

        private OverlandWorld Build(OverlandTerrain t, bool unityTerrain, string name)
        {
            OverlandWorld.UseUnityTerrain = unityTerrain;
            return OverlandWorld.Build(_root, new OverlandView { Terrain = t, RegionName = name, RegionKind = t.Kind }, ArtCatalog.Load());
        }

        [UnityTest]
        public IEnumerator TheTerrainsColour_IsExactlyTheMeshGroundsColour_LinearAndUnclipped()
        {
            var t = Terrain();
            var w = Build(t, true, "colour");
            var tex = w.UnityTerrain.materialTemplate.GetTexture("_BiomeColour") as Texture2D;
            Assert.That(tex, Is.Not.Null);
            Assert.That(GraphicsFormatUtility.IsSRGBFormat(tex.graphicsFormat), Is.False, "Linear: the colours are already linear, as vertex colours were.");
            Assert.That(tex.format, Is.EqualTo(TextureFormat.RGBAHalf), "Half-float: colours above 1 are kept.");

            // Read the texture back from the GPU, texel for texel, and compare with the function the mesh ground uses.
            var rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var active = RenderTexture.active; // before the blit, which makes rt the active target
            Graphics.Blit(tex, rt);
            var read = new Texture2D(tex.width, tex.height, TextureFormat.RGBAHalf, false, true);
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            read.Apply();
            RenderTexture.active = active;
            var step = OverlandScale.MetresPerCell / OverlandWorld.TexelsPerCell;
            float worst = 0f, brightest = 0f, darkest = 9f;
            var aboveOne = 0;
            for (var z = 0; z < tex.height; z += 7)
                for (var x = 0; x < tex.width; x += 7)
                {
                    var expected = w.GroundColourAt((x + 0.5f) * step, (z + 0.5f) * step);
                    var got = read.GetPixel(x, z);
                    var diff = Mathf.Max(Mathf.Abs(expected.r - got.r), Mathf.Abs(expected.g - got.g), Mathf.Abs(expected.b - got.b));
                    worst = Mathf.Max(worst, diff / Mathf.Max(0.05f, expected.maxColorComponent));
                    brightest = Mathf.Max(brightest, expected.maxColorComponent);
                    darkest = Mathf.Min(darkest, expected.maxColorComponent);
                    if (expected.maxColorComponent > 1f) { aboveOne++; Assert.That(got.maxColorComponent, Is.EqualTo(expected.maxColorComponent).Within(0.01f), "Not clipped at 1."); }
                }

            Assert.That(worst, Is.LessThan(0.01f), "Within half-float precision (relative).");
            TestContext.WriteLine($"Colour texels compared: worst relative difference {worst:0.0000}; linear values {darkest:0.000}–{brightest:0.000}; {aboveOne} above 1.");
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(read);

            // The mesh ground's vertex colours come from the same function, at its own vertices.
            Object.Destroy(w.gameObject);
            yield return null;
            var mesh = Build(t, false, "mesh colour");
            var at = new Vector3(t.SizeX * 0.4f, 0f, t.SizeZ * 0.6f);
            mesh.Prewarm(mesh.transform.TransformPoint(at));
            yield return null;
            var chunk = mesh.GetComponentsInChildren<MeshFilter>().First(f => f.name.EndsWith("(detail)"));
            var m = chunk.sharedMesh;
            var vs = m.vertices;
            var cs = m.colors;
            for (var i = 0; i < vs.Length; i += 97)
            {
                var expected = mesh.GroundColourAt(vs[i].x, vs[i].z);
                Assert.That(cs[i].r, Is.EqualTo(expected.r).Within(1e-4f));
                Assert.That(cs[i].g, Is.EqualTo(expected.g).Within(1e-4f));
            }
        }

        [UnityTest]
        public IEnumerator RoadsAndBridges_KeepTheirColours_SitOnTheTerrain_AndTakeTheWeather_InBothModes()
        {
            var t = Terrain();
            var dirt = new Color(0.68f, 0.56f, 0.38f).linear;
            var planks = new Color(0.42f, 0.31f, 0.20f).linear;

            // Unity Terrain: roads in their own vertex-colour material.
            var w = Build(t, true, "roads on terrain");
            var roads = w.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Roads");
            Assert.That(roads.sharedMaterial, Is.SameAs(w.RoadMaterial));
            Assert.That(roads.sharedMaterial.shader.name, Is.EqualTo("Realmkeeper/VertexColorLit"), "Not the terrain shader.");
            Assert.That(roads.sharedMaterial, Is.Not.SameAs(w.UnityTerrain.materialTemplate));
            Assert.That(roads.sharedMaterial.GetFloat("_TexStrength"), Is.EqualTo(0.85f).Within(1e-4f), "The same grain as the ground.");
            Assert.That(roads.sharedMaterial.GetTexture("_DirtTex"), Is.SameAs(w.UnityTerrain.materialTemplate.GetTexture("_DirtTex")));
            var mesh = roads.GetComponent<MeshFilter>().sharedMesh;
            var colours = mesh.colors;
            Assert.That(colours.Any(c => Close(c, dirt)), Is.True, "Dirt roads.");
            var bridges = colours.Count(c => Close(c, planks));
            Assert.That(bridges, Is.GreaterThan(0), "Bridge decks in planks.");

            // On the terrain: no road vertex below the drawn ground.
            var verts = mesh.vertices;
            var sink = 0f;
            var below = 0;
            foreach (var v in verts)
            {
                var world = roads.transform.TransformPoint(v);
                var ground = w.UnityTerrain.SampleHeight(world) + w.UnityTerrain.transform.position.y;
                var gap = world.y - ground;
                if (gap < 0f) { below++; sink = Mathf.Max(sink, -gap); }
            }

            // The old near mesh ground, measured the same way: its 10 m triangles through the game's heights (GroundMesh).
            var meshBelow = 0;
            var meshSink = 0f;
            foreach (var v in verts)
            {
                var gap = v.y - MeshGround(t, v.x, v.z);
                if (gap < 0f) { meshBelow++; meshSink = Mathf.Max(meshSink, -gap); }
            }

            TestContext.WriteLine($"Road vertices {verts.Length} ({bridges} on bridges). Under the Unity Terrain: {below}, at most {sink:0.000} m. Under the old 10 m mesh ground: {meshBelow}, at most {meshSink:0.000} m.");
            Assert.That(below, Is.LessThanOrEqualTo(meshBelow), "No more road under the ground than with the old mesh ground.");
            Assert.That(sink, Is.LessThanOrEqualTo(Mathf.Max(meshSink, 0.05f)), $"Roads sit on the terrain no worse than on the mesh ground ({below} of {verts.Length} vertices under it, at most {sink:0.000} m).");

            // The ground weather reaches the roads as well as the ground.
            w.SetGroundWeather(0.6f, 0.8f);
            foreach (var m in new[] { w.RoadMaterial, w.UnityTerrain.materialTemplate })
            {
                Assert.That(m.GetFloat("_Wet"), Is.EqualTo(0.6f).Within(1e-4f), m.name);
                Assert.That(m.GetFloat("_Snow"), Is.EqualTo(0.8f).Within(1e-4f), m.name);
            }

            Object.Destroy(w.gameObject);
            yield return null;

            // Mesh fallback: as in Phase 23, roads share the ground's vertex-colour material.
            var fallback = Build(t, false, "roads on mesh");
            var fallbackRoads = fallback.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "Roads");
            Assert.That(fallback.UnityTerrain, Is.Null);
            Assert.That(fallbackRoads.sharedMaterial.shader.name, Is.EqualTo("Realmkeeper/VertexColorLit"));
            Assert.That(fallbackRoads.sharedMaterial, Is.SameAs(fallback.RoadMaterial));
            Assert.That(fallbackRoads.GetComponent<MeshFilter>().sharedMesh.colors.Count(c => Close(c, planks)), Is.EqualTo(bridges), "The same roads and bridges.");
            fallback.SetGroundWeather(0.3f, 0f);
            Assert.That(fallback.RoadMaterial.GetFloat("_Wet"), Is.EqualTo(0.3f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator TheTerrainAndTheMeshGround_RenderAlike_AtTheSameSpot_LitAndInAmbientLight()
        {
            var t = Terrain();
            // A level, plain spot away from water: the same cell for both grounds.
            var spot = Vector3.zero;
            for (var z = 20; z < t.Map.Depth - 20 && spot == Vector3.zero; z++)
                for (var x = 20; x < t.Map.Width - 20; x++)
                {
                    var c = t.Cell(x, z);
                    if (c.Road || (c.Biome != Biome.Grassland && c.Biome != Biome.Meadow)) continue;
                    var (cx, cz) = OverlandScale.CellCentre(new Realmkeeper.Domain.World.MapCoord(x, z));
                    if (Mathf.Abs(t.HeightAt(cx + 8f, cz) - t.HeightAt(cx - 8f, cz)) + Mathf.Abs(t.HeightAt(cx, cz + 8f) - t.HeightAt(cx, cz - 8f)) > 0.4f) continue;
                    spot = new Vector3(cx, 0f, cz);
                    break;
                }

            Assert.That(spot, Is.Not.EqualTo(Vector3.zero), "A level plain.");
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.enabled).ToList();
            var results = new Dictionary<string, Color>();
            foreach (var unityTerrain in new[] { true, false })
            {
                var w = Build(t, unityTerrain, unityTerrain ? "terrain render" : "mesh render");
                w.Prewarm(w.transform.TransformPoint(spot));
                yield return null;
                var ground = w.transform.TransformPoint(new Vector3(spot.x, t.HeightAt(spot.x, spot.z), spot.z));
                foreach (var lit in new[] { true, false })
                {
                    foreach (var l in sun) l.enabled = lit;
                    yield return null;
                    results[(unityTerrain ? "terrain" : "mesh") + (lit ? " lit" : " ambient")] = Average(ground);
                }

                foreach (var l in sun) l.enabled = true;
                Object.Destroy(w.gameObject);
                yield return null;
            }

            // Before the shader stopped applying URP's per-renderer culling flag to the sun, "terrain lit" equalled
            // "terrain ambient": the Unity Terrain received no direct sunlight (ratio 0.31 against the mesh ground).
            foreach (var r in results) TestContext.WriteLine($"{r.Key}: linear ({r.Value.r:0.000}, {r.Value.g:0.000}, {r.Value.b:0.000})");
            float Ratio(string a, string b) => results[a].grayscale / Mathf.Max(1e-4f, results[b].grayscale);
            TestContext.WriteLine($"Terrain / mesh: lit {Ratio("terrain lit", "mesh lit"):0.000}, ambient only {Ratio("terrain ambient", "mesh ambient"):0.000}.");
            Assert.That(Ratio("terrain ambient", "mesh ambient"), Is.EqualTo(1f).Within(0.08f), "The same ambient light on both grounds.");
            Assert.That(Ratio("terrain lit", "mesh lit"), Is.EqualTo(1f).Within(0.08f), "The same brightness in sunlight.");
        }

        /// <summary>The mean linear colour of the ground seen straight down from 60 m (a 12 m square, no fog, no interface).</summary>
        private static Color Average(Vector3 ground)
        {
            var go = new GameObject("Parity camera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 200f;
            go.transform.position = ground + Vector3.up * 60f;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var rt = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            cam.targetTexture = rt;
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(32, 32, TextureFormat.RGBAHalf, false, true);
            tex.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderSettings.fog = fog;
            var sum = Color.clear;
            foreach (var p in tex.GetPixels()) sum += p;
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(tex);
            Object.Destroy(go);
            return sum / (32f * 32f);
        }

        /// <summary>The height of the old near mesh ground at (x, z): its 10 m grid of the game's heights, split as GroundMesh splits it.</summary>
        private static float MeshGround(OverlandTerrain t, float x, float z)
        {
            const float step = 10f;
            var ix = Mathf.FloorToInt(x / step);
            var iz = Mathf.FloorToInt(z / step);
            var fx = x / step - ix;
            var fz = z / step - iz;
            float H(int i, int k) => t.HeightAt(Mathf.Min(i * step, t.SizeX), Mathf.Min(k * step, t.SizeZ));
            var h00 = H(ix, iz);
            var h10 = H(ix + 1, iz);
            var h01 = H(ix, iz + 1);
            var h11 = H(ix + 1, iz + 1);
            // Triangles (i, i+n, i+1) and (i+1, i+n, i+n+1): the diagonal runs from (1,0) to (0,1).
            return fx + fz <= 1f ? h00 + (h10 - h00) * fx + (h01 - h00) * fz : h11 + (h01 - h11) * (1f - fx) + (h10 - h11) * (1f - fz);
        }

        private static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f && Mathf.Abs(a.b - b.b) < 1e-3f;
    }
}
