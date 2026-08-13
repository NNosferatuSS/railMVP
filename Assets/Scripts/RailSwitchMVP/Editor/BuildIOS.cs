using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Entry point for headless iOS exports, called via -executeMethod from
    /// scripts/build-and-run-ios.sh. Exports into Builds/01 by default so Unity
    /// does an incremental ("Append") Xcode update instead of a full regenerate.
    /// </summary>
    public static class BuildIOS
    {
        private const string DefaultOutputPath = "Builds/01";

        public static void PerformBuild()
        {
            var outputPath = GetArg("-buildOutput") ?? DefaultOutputPath;
            var development = HasArg("-developmentBuild");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("BuildIOS: no enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"BuildIOS: build failed - {report.summary.result}");
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains(name);

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var idx = Array.IndexOf(args, name);
            return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
        }
    }
}
