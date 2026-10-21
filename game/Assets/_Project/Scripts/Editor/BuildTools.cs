using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Weekly playable build (CLAUDE.md 7): Windows player starting in the main menu, then duel, company, sandbox and prototypes. Batch entry: -executeMethod Shieldbreak.EditorTools.BuildTools.BuildWindowsBatch
    /// -buildOutput &lt;folder&gt;.
    /// </summary>
    public static class BuildTools
    {
        const string ExeName = "SHIELDBREAK.exe";

        static readonly string[] Scenes =
        {
            MainMenuSceneBuilder.ScenePath,
            DuelSceneBuilder.ScenePath,
            CompanySceneBuilder.ScenePath,
            SandboxSceneBuilder.ScenePath,
            ProtoAnimationSceneBuilder.ScenePath,
            ProtoStyleSceneBuilder.ScenePath,
        };

        [MenuItem("Shieldbreak/Build Windows Player")]
        public static void BuildWindowsMenu()
        {
            string folder = EditorUtility.SaveFolderPanel("Build-Ordner", Path.GetFullPath(".."), "build");
            if (!string.IsNullOrEmpty(folder)) BuildWindows(folder);
        }

        public static void BuildWindowsBatch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildOutput");
            string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("../builds/latest");
            var report = BuildWindows(folder);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static BuildReport BuildWindows(string folder)
        {
            var scenes = Scenes.Where(File.Exists).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes to build; run the scene builders first.");
            EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();

            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[build] {s.result}: {s.outputPath}, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:0} s, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings, scenes: {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}");
            return report;
        }
    }
}
