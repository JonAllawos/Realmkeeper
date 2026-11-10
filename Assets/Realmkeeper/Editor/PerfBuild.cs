using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Realmkeeper.Editor
{
    /// <summary>
    /// Batch entry point for a Windows development build used to measure performance
    /// (<c>-executeMethod Realmkeeper.Editor.PerfBuild.Build</c>, output folder from <c>RK_BUILD_OUT</c>).
    /// Run the player with <c>-rkPerfProbe &lt;report&gt;</c>; see <c>PerformanceProbe</c>.
    /// </summary>
    public static class PerfBuild
    {
        public static void Build()
        {
            var output = Environment.GetEnvironmentVariable("RK_BUILD_OUT");
            if (string.IsNullOrEmpty(output)) output = "Builds/Development";
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = System.IO.Path.Combine(output, "Realmkeeper.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"Realmkeeper development build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
