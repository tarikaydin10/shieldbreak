using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Weekly playable build (CLAUDE.md 7): player starting in the main menu, then every scene of the scene catalog.
    /// Windows (SHIELDBREAK.exe) or macOS (SHIELDBREAK.app; build it on the Mac, Burst needs its Xcode command line tools).
    /// Batch entry: -executeMethod Shieldbreak.EditorTools.BuildTools.BuildWindowsBatch (or BuildMacBatch)
    /// -buildOutput &lt;folder&gt;.
    /// </summary>
    public static class BuildTools
    {
        /// <summary>Every scene of the build, main menu first; also the editor's build settings (scene loading in play mode).</summary>
        public static readonly string[] Scenes =
        {
            MainMenuSceneBuilder.ScenePath,
            DuelSceneBuilder.ScenePath,
            CompanySceneBuilder.ScenePath,
            SandboxSceneBuilder.ScenePath,
            ProtoAnimationSceneBuilder.ScenePath,
            ProtoStyleSceneBuilder.ScenePath,
        };

        [MenuItem("Shieldbreak/Build Windows Player")]
        public static void BuildWindowsMenu() => BuildMenu(BuildWindows);

        [MenuItem("Shieldbreak/Build Mac Player")]
        public static void BuildMacMenu() => BuildMenu(BuildMac);

        public static void BuildWindowsBatch() => BuildBatch(BuildWindows);

        public static void BuildMacBatch() => BuildBatch(BuildMac);

        public static BuildReport BuildWindows(string folder) => Build(folder, BuildTarget.StandaloneWindows64, "SHIELDBREAK.exe");

        public static BuildReport BuildMac(string folder) => Build(folder, BuildTarget.StandaloneOSX, "SHIELDBREAK.app");

        static void BuildMenu(Func<string, BuildReport> build)
        {
            string folder = EditorUtility.SaveFolderPanel("Build-Ordner", Path.GetFullPath(".."), "build");
            if (!string.IsNullOrEmpty(folder)) build(folder);
        }

        static void BuildBatch(Func<string, BuildReport> build)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildOutput");
            string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("../builds/latest");
            var report = build(folder);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build(string folder, BuildTarget target, string playerName)
        {
            var scenes = Scenes.Where(File.Exists).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No scenes to build; run the scene builders first.");
            EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();

            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(folder, playerName),
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[build] {s.result} ({target}): {s.outputPath}, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:0} s, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings, scenes: {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}");
            return report;
        }
    }
}
