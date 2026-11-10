using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Unity.Profiling;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Performance measurement for development builds. It does nothing unless the player is started
    /// with <c>-rkPerfProbe &lt;report path&gt;</c>. It then waits for the world, enters the first town
    /// through the normal command, and holds the camera at fixed poses (gate, street, building, plaza,
    /// tavern interior, overview), the same as the screenshot set. At each pose it records frame time
    /// (mean, 95th percentile) and the engine's render counters, writes a text report and quits.
    /// <c>-rkPrimitives</c> forces the primitive fallback, so one build measures before and after.
    /// </summary>
    public sealed class PerformanceProbe : MonoBehaviour
    {
        private const int Warmup = 60;
        private const int Samples = 240;

        private string _path;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-rkPerfProbe");
            if (i < 0 || i + 1 >= args.Length) return;
            if (args.Contains("-rkPrimitives")) ArtCatalog.Override(null);
            var go = new GameObject("Performance probe");
            DontDestroyOnLoad(go);
            go.AddComponent<PerformanceProbe>()._path = args[i + 1];
        }

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            var report = new StringBuilder();
            report.AppendLine("Realmkeeper performance probe (development build)");
            report.AppendLine($"date {DateTime.Now:yyyy-MM-dd HH:mm}, device {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), CPU {SystemInfo.processorType}");
            report.AppendLine($"resolution {Screen.width}x{Screen.height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, art {(ArtCatalog.Load() != null ? "kit" : "primitives")}");
            report.AppendLine();

            ExplorationInstaller installer = null;
            for (var i = 0; i < 600 && (installer == null || installer.Runtime == null || installer.Runtime.Host?.Session == null || !installer.Runtime.Session.HasWorld); i++)
            {
                installer = FindAnyObjectByType<ExplorationInstaller>();
                yield return null;
            }

            if (installer == null)
            {
                Finish(report.AppendLine("No world loaded."));
                yield break;
            }

            var session = installer.Runtime.Session;
            yield return Measure("map (strategic)", report);

            var town = session.GetSettlements().FirstOrDefault(s => s.SettlementType == SettlementType.Town);
            var result = session.Dispatch(new EnterSettlementCommand(town.SettlementId));
            report.AppendLine($"enter {town.Name}: {result.Status}");
            for (var i = 0; i < 30 && !installer.Controller.IsExploring; i++) yield return null;
            var controller = installer.Controller;
            controller.ManageCursor = false;
            var camera = controller.CameraRig.Camera;
            controller.CameraRig.enabled = false;
            var layout = controller.Streamer.Current;
            var half = layout.HalfExtent;
            var hall = layout.Buildings.First(b => b.Kind == BuildingKind.Hall);
            var house = layout.Buildings.First(b => b.Kind == BuildingKind.House);
            var door = new Vector3(Mathf.Sin(house.Yaw * Mathf.Deg2Rad), 0f, -Mathf.Cos(house.Yaw * Mathf.Deg2Rad));
            var houseCentre = new Vector3(house.Center.X, 0f, house.Center.Z);
            var hallCentre = new Vector3(hall.Center.X, 0f, hall.Center.Z);
            var poses = new List<(string, Vector3, Vector3)>
            {
                ("gate", new Vector3(0f, 2.6f, -half + 2f), new Vector3(0f, 2.2f, -half + 30f)),
                ("street", new Vector3(1.2f, 1.75f, -half + 12f), new Vector3(-0.5f, 2.6f, 0f)),
                ("building", houseCentre + door * (house.Depth / 2f + 7f) + Vector3.Cross(Vector3.up, door) * 3f + Vector3.up * 2.2f, houseCentre + Vector3.up * 3.2f),
                ("plaza", new Vector3(-9f, 4.5f, -layout.PlazaRadius - 9f), new Vector3(2f, 1f, 2f)),
                ("interior", hallCentre + new Vector3(-3.8f, 2.1f, -hall.Depth / 2f + 1.2f), hallCentre + new Vector3(2f, 1.2f, 2.5f)),
                ("overview", new Vector3(-half * 0.9f, half * 0.75f, -half * 1.25f), new Vector3(0f, 0f, 4f)),
            };
            foreach (var (name, from, at) in poses)
            {
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                yield return Measure(name, report);
            }

            // Phase 5: inside a shop; the street with the clock running fast (people re-planned and walking);
            // the street and plaza at night with every lamp lit.
            var shop = controller.Shops.Values.FirstOrDefault(v => v.Building.Fit == ShopFit.General);
            if (shop != null)
            {
                var t = shop.transform;
                var from = t.TransformPoint(new Vector3(-1.2f, 1.7f, -shop.Building.Depth / 2f + 1.0f));
                var at = t.TransformPoint(new Vector3(0.4f, 1.1f, shop.Building.Depth / 2f - 0.5f));
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
                yield return Measure("shop interior", report);
            }

            var playback = installer.Runtime.Playback;
            session.Dispatch(new Realmkeeper.Application.SetSimulationSpeedCommand(SimulationSpeed.VeryFast));
            playback.Play();
            var streetFrom = new Vector3(1.2f, 1.75f, -half + 12f);
            var streetAt = new Vector3(-0.5f, 2.6f, 0f);
            camera.transform.SetPositionAndRotation(streetFrom, Quaternion.LookRotation(streetAt - streetFrom, Vector3.up));
            var before = session.GetWorldTime();
            yield return Measure("street, clock x30", report);
            var after = session.GetWorldTime();
            report.AppendLine($"  clock {before.Clock} -> {after.Clock}, walking now {controller.Residents.Values.Count(v => v.IsWalking)}");
            playback.Pause();
            session.Dispatch(new Realmkeeper.Application.SetSimulationSpeedCommand(SimulationSpeed.Normal));

            var minute = session.GetWorldTime().MinuteOfDay;
            controller.WaitHour(minute < 22 * 60 + 30 ? 22 * 60 + 30 - minute : 1440 - minute + 22 * 60 + 30);
            camera.transform.SetPositionAndRotation(streetFrom, Quaternion.LookRotation(streetAt - streetFrom, Vector3.up));
            yield return Measure("street, night", report);
            var plazaFrom = new Vector3(-9f, 4.5f, -layout.PlazaRadius - 9f);
            camera.transform.SetPositionAndRotation(plazaFrom, Quaternion.LookRotation(new Vector3(2f, 1f, 2f) - plazaFrom, Vector3.up));
            yield return Measure("plaza, night", report);
            report.AppendLine($"  {session.GetWorldTime().Text}: lamps lit {controller.Atmosphere.Lamps.Count(l => l.enabled)}, residents out {controller.Residents.Values.Count(v => !v.IsHidden)}");

            report.AppendLine();
            report.AppendLine($"residents {controller.Residents.Count}, shops {controller.Shops.Count}, renderers {FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length}");
            controller.ReturnToWorldMap();
            Finish(report);
        }

        private static IEnumerator Measure(string name, StringBuilder report)
        {
            for (var i = 0; i < Warmup; i++) yield return null;
            var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            var memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            var gfx = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
            var times = new List<float>();
            long drawSum = 0, batchSum = 0, setPassSum = 0, triSum = 0;
            for (var i = 0; i < Samples; i++)
            {
                yield return null;
                times.Add(Time.unscaledDeltaTime * 1000f);
                drawSum += draws.LastValue;
                batchSum += batches.LastValue;
                setPassSum += setPass.LastValue;
                triSum += tris.LastValue;
            }

            times.Sort();
            var mean = times.Average();
            var p95 = times[(int)(times.Count * 0.95f)];
            report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-16} frame mean {1,6:F2} ms ({2,5:F0} fps)  p95 {3,6:F2} ms  draw calls {4,5}  batches {5,5}  set-pass {6,4}  triangles {7,9}  system mem {8,5} MB  gfx mem {9,5} MB",
                name, mean, 1000f / mean, p95, drawSum / Samples, batchSum / Samples, setPassSum / Samples, triSum / Samples,
                memory.LastValue / (1024 * 1024), gfx.LastValue / (1024 * 1024)));
            draws.Dispose();
            batches.Dispose();
            setPass.Dispose();
            tris.Dispose();
            mainThread.Dispose();
            memory.Dispose();
            gfx.Dispose();
        }

        private void Finish(StringBuilder report)
        {
            try
            {
                File.WriteAllText(_path, report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            UnityEngine.Application.Quit();
        }
    }
}
