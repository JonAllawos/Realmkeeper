using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Evidence capture, not a pass/fail test: renders the shipped scene from fixed camera poses
    /// (strategic map, gate, street, a building, a resident, the plaza with residents, the hall
    /// interior, an overview) to PNG files, and writes scene statistics next to them. The same poses
    /// are used before and after a visual change, so the images compare like for like.
    /// Runs only when the environment variable <c>RK_CAPTURE_DIR</c> names an output folder
    /// (<c>RK_CAPTURE_TAG</c> prefixes the file names); otherwise it is ignored.
    /// </summary>
    public sealed class GraphicsCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;

        private string _dir;
        private string _tag;
        private readonly StringBuilder _stats = new StringBuilder();
        private InputSettings.BackgroundBehavior _background;
        private string _frame = "";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            // (Ignoring is decided in the test body: Assert.Ignore inside a UnitySetUp coroutine can stall the runner.)
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            _tag = Environment.GetEnvironmentVariable("RK_CAPTURE_TAG") ?? "capture";
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break; // nothing was loaded: never touch the runner's scene
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureTownSet()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var installer = Object.FindAnyObjectByType<ExplorationInstaller>();
            installer.Controller.ManageCursor = false;
            installer.Controller.SetInput(new ScriptedExplorationInput());

            var town = bootstrap.WorldView.Settlements.Values.Single(v => v.SettlementType == SettlementType.Town);
            bootstrap.CameraController.FocusOn(town.Center);
            for (var i = 0; i < 90; i++) yield return null;
            yield return Measure();
            yield return Shoot(bootstrap.CameraController.Camera, "map");

            Assert.That(bootstrap.Selection.SelectAtScreenPoint(
                bootstrap.CameraController.Camera.WorldToScreenPoint(town.Center + Vector3.up)), Is.True);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 20; i++) yield return null;

            var controller = installer.Controller;
            var layout = controller.Streamer.Current;
            var camera = controller.CameraRig.Camera;
            controller.CameraRig.enabled = false; // poses below are set by hand
            foreach (var view in controller.Residents.Values) view.enabled = false; // hold still for the shots
            yield return null;

            var half = layout.HalfExtent;
            var hall = layout.Buildings.First(b => b.Kind == BuildingKind.Hall);
            var house = layout.Buildings.First(b => b.Kind == BuildingKind.House);

            Pose(camera, new Vector3(0f, 2.6f, -half + 2f), new Vector3(0f, 2.2f, -half + 30f));
            yield return Measure();
            yield return Shoot(camera, "gate");

            Pose(camera, new Vector3(1.2f, 1.75f, -half + 12f), new Vector3(-0.5f, 2.6f, 0f));
            yield return Measure();
            yield return Shoot(camera, "street");

            var door = new Vector3(Mathf.Sin(house.Yaw * Mathf.Deg2Rad), 0f, -Mathf.Cos(house.Yaw * Mathf.Deg2Rad));
            var houseCentre = new Vector3(house.Center.X, 0f, house.Center.Z);
            var side = Vector3.Cross(Vector3.up, door).normalized;
            Pose(camera, houseCentre + door * (house.Depth / 2f + 7f) + side * 3f + Vector3.up * 2.2f, houseCentre + Vector3.up * 3.2f);
            yield return Measure();
            yield return Shoot(camera, "building");

            // The first resident (by stable ID) that can be seen clearly from some side.
            var shot = controller.Residents.Values.OrderBy(r => r.NpcId.ToString(), StringComparer.Ordinal)
                .Select(r => (view: r, from: ClearView(r.transform.position, SunSide()))).FirstOrDefault(c => c.from.HasValue);
            var resident = shot.view ?? controller.Residents.Values.First();
            var rp = resident.transform.position;
            Pose(camera, shot.from ?? rp + resident.transform.forward * 2.4f + Vector3.up * 1.6f, rp + Vector3.up * 1.25f);
            yield return Measure();
            yield return Shoot(camera, "character");

            Pose(camera, new Vector3(-9f, 4.5f, -layout.PlazaRadius - 9f), new Vector3(2f, 1f, 2f));
            yield return Measure();
            yield return Shoot(camera, "plaza_residents");

            var hallCentre = new Vector3(hall.Center.X, 0f, hall.Center.Z);
            Pose(camera, hallCentre + new Vector3(-3.8f, 2.1f, -hall.Depth / 2f + 1.2f), hallCentre + new Vector3(2f, 1.2f, 2.5f));
            yield return Measure();
            yield return Shoot(camera, "interior");

            Pose(camera, new Vector3(-half * 0.9f, half * 0.75f, -half * 1.25f), new Vector3(0f, 0f, 4f));
            yield return Measure();
            yield return Shoot(camera, "overview");

            WriteStats(controller, camera);
            File.WriteAllText(Path.Combine(_dir, _tag + "_stats.txt"), _stats.ToString());
            Assert.That(controller.ReturnToWorldMap().IsSuccess, Is.True);
        }

        /// <summary>A point 2.4 m from the resident at eye height with nothing in between (tries eight directions).</summary>
        /// <summary>The horizontal direction the sunlight comes from, so close-ups are lit from the front.</summary>
        private static Vector3 SunSide()
        {
            var sun = RenderSettings.sun;
            if (sun == null) return Vector3.back;
            var d = -sun.transform.forward;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.back;
        }

        private static Vector3? ClearView(Vector3 subject, Vector3 facing)
        {
            for (var i = 0; i < 8; i++)
            {
                var dir = Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 45f, 0f) * facing;
                var from = subject + dir * 2.4f + Vector3.up * 1.6f;
                var clear = true;
                foreach (var h in new[] { 0.4f, 0.9f, 1.4f })
                {
                    var eye = subject + dir * 2.4f + Vector3.up * h;
                    var target = subject + Vector3.up * h;
                    if (Physics.Linecast(eye, target - dir * 0.45f)) clear = false;
                }

                if (clear) return from;
            }

            return null;
        }

        /// <summary>Lets the pose render for real frames, then records frame time and the engine counters.</summary>
        private IEnumerator Measure()
        {
            for (var i = 0; i < 5; i++) yield return null;
            var total = 0f;
            const int frames = 30;
            for (var i = 0; i < frames; i++)
            {
                yield return null;
                total += Time.unscaledDeltaTime;
            }

            _frame = string.Format(CultureInfo.InvariantCulture, "frame {0,6:F2} ms ({1} cameras)", total * 1000f / frames, Camera.allCamerasCount);
#if UNITY_EDITOR
            _frame += string.Format(CultureInfo.InvariantCulture, "  batches {0,5}  set-pass {1,4}  triangles {2,9}  shadow casters {3,5}",
                UnityEditor.UnityStats.batches, UnityEditor.UnityStats.setPassCalls, UnityEditor.UnityStats.triangles, UnityEditor.UnityStats.shadowCasters);
#endif
        }

        private static void Pose(Camera camera, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
        }

        private IEnumerator Shoot(Camera camera, string name)
        {
            // Render counters for the renders below (they land in this frame; read them after it ends).
            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            yield return null;
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render(); // warm-up (shadow maps, probes)
            var watch = Stopwatch.StartNew();
            const int renders = 10;
            for (var i = 0; i < renders; i++) camera.Render();
            watch.Stop();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            camera.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(_dir, _tag + "_" + name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            var visible = CountVisible(camera);
            yield return null;
            const int counted = renders + 1;
            var engine = draws.Valid
                ? string.Format(CultureInfo.InvariantCulture, "  per render: draw calls {0,5}  batches {1,5}  set-pass {2,4}  triangles {3,9}  shadow casters {4,5}",
                    draws.LastValue / counted, batches.LastValue / counted, setPass.LastValue / counted, tris.LastValue / counted, shadowCasters.LastValue / counted)
                : "  (render counters unavailable)";
            draws.Dispose();
            batches.Dispose();
            setPass.Dispose();
            tris.Dispose();
            shadowCasters.Dispose();
            _stats.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-16} render CPU {1,6:F2} ms (mean of {2})  visible renderers {3,5}  materials {4,4}{5}",
                name, watch.Elapsed.TotalMilliseconds / renders, renders, visible.renderers, visible.materials, engine));
        }

        private static (int renderers, long triangles, int materials) CountVisible(Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var renderers = 0;
            long triangles = 0;
            var materials = new HashSet<Material>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                renderers++;
                foreach (var m in r.sharedMaterials) if (m != null) materials.Add(m);
                triangles += Triangles(r);
            }

            return (renderers, triangles, materials.Count);
        }

        private static long Triangles(Renderer r)
        {
            Mesh mesh = null;
            if (r is MeshRenderer) mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
            else if (r is SkinnedMeshRenderer s) mesh = s.sharedMesh;
            if (mesh == null || !mesh.isReadable && mesh.subMeshCount == 0) return 0;
            long count = 0;
            for (var i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
            return count;
        }

        private void WriteStats(ExplorationController controller, Camera camera)
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
            var materials = new HashSet<Material>(all.SelectMany(r => r.sharedMaterials).Where(m => m != null));
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
            var triangles = all.Sum(Triangles);
            _stats.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "scene total: renderers {0}, triangles {1}, materials {2}, colliders {3}, residents {4}, buildings {5}",
                all.Count, triangles, materials.Count, colliders, controller.Residents.Count, controller.Streamer.Current.Buildings.Count));
            _stats.AppendLine("graphics device: " + SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsDeviceType + ")");
        }
    }
}
