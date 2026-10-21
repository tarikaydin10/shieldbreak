using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Keeps the test mode complete (CLAUDE.md 8): every build scene and every scenario asset is reachable through
    /// the scene catalog, every entry explains itself (objective, controls, checklist), and the editor's build
    /// settings list the build's scenes, so the menus can load them in play mode too.
    /// </summary>
    public class SceneCatalogTests
    {
        const string CatalogPath = "Assets/_Project/Resources/SceneCatalog.asset";
        const string InputPath = "Assets/_Project/Resources/Input/Shieldbreak.inputactions";
        const string ScenarioFolder = "Assets/_Project/Data/Scenarios";

        static string SceneName(string path) => Path.GetFileNameWithoutExtension(path);

        static SceneCatalogDefinition LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalogDefinition>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(SceneCatalogDefinition.Load(), Is.SameAs(catalog), "the game loads the catalog from Resources");
            return catalog;
        }

        [Test]
        public void EveryBuildSceneHasAnEntry_AndEveryEntryABuildScene()
        {
            var catalog = LoadCatalog();
            var buildScenes = BuildTools.Scenes.Select(SceneName).ToList();
            string menu = SceneName(MainMenuSceneBuilder.ScenePath);

            var unlisted = buildScenes.Where(s => s != menu && catalog.entries.All(e => e.scene != s)).ToList();
            Assert.That(unlisted, Is.Empty, $"Build scenes without an entry in {CatalogPath}: {string.Join(", ", unlisted)}");

            var unknown = catalog.entries.Where(e => !buildScenes.Contains(e.scene)).Select(e => $"{e.id} ({e.scene})").ToList();
            Assert.That(unknown, Is.Empty, $"Entries whose scene is not in BuildTools.Scenes: {string.Join(", ", unknown)}");
        }

        [Test]
        public void EveryScenarioIsReachable()
        {
            // Reachable = a build scene uses it (its SimRunner) or an entry starts a scene with it
            var catalog = LoadCatalog();
            var sceneFiles = BuildTools.Scenes.Select(File.ReadAllText).ToList();
            var unreachable = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { ScenarioFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!sceneFiles.Any(text => text.Contains(guid)) && catalog.entries.All(e => e.scenario != asset))
                    unreachable.Add(path);
            }
            Assert.That(unreachable, Is.Empty, $"Scenarios no scene and no catalog entry starts: {string.Join(", ", unreachable)}");
        }

        [Test]
        public void EveryEntryExplainsItself()
        {
            var catalog = LoadCatalog();
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            string duelScene = SceneName(DuelSceneBuilder.ScenePath);
            var ids = new HashSet<string>();
            foreach (var e in catalog.entries)
            {
                Assert.That(string.IsNullOrWhiteSpace(e.id), Is.False, "entry without id");
                Assert.That(ids.Add(e.id), Is.True, $"id {e.id} twice");
                Assert.That(string.IsNullOrWhiteSpace(e.title), Is.False, $"{e.id}: no title");
                Assert.That(string.IsNullOrWhiteSpace(e.section), Is.False, $"{e.id}: no section");
                Assert.That(string.IsNullOrWhiteSpace(e.objective), Is.False, $"{e.id}: no objective");
                Assert.That(e.checks.Length, Is.GreaterThan(0), $"{e.id}: no checklist");
                Assert.That(e.checks.Any(string.IsNullOrWhiteSpace), Is.False, $"{e.id}: empty check");
                Assert.That(e.controlMaps.Length, Is.GreaterThan(0), $"{e.id}: no controls");
                foreach (string map in e.controlMaps)
                    Assert.That(input.FindActionMap(map), Is.Not.Null, $"{e.id}: action map {map} does not exist");
                if (e.scenario != null)
                    Assert.That(e.scenario, Is.InstanceOf<ISimScenario>(), $"{e.id}: scenario is no ISimScenario");
                if (e.launch != SceneCatalogDefinition.LaunchKind.Scene)
                    Assert.That(e.scene, Is.EqualTo(duelScene), $"{e.id}: duel launch outside the duel scene");
            }
        }

        [Test]
        public void EditorBuildSettings_ListTheBuildScenes()
        {
            var listed = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(listed, Is.EqualTo(BuildTools.Scenes),
                "ProjectSettings/EditorBuildSettings.asset must list BuildTools.Scenes in order (main menu first), else menus cannot load scenes in play mode");
        }

        [Test]
        public void Resolve_PrefersTheRunningScenario_ThenTheStartedEntry()
        {
            var sandbox = ScriptableObject.CreateInstance<SandboxScenarioDefinition>();
            var stress = ScriptableObject.CreateInstance<SandboxScenarioDefinition>();
            var catalog = ScriptableObject.CreateInstance<SceneCatalogDefinition>();
            try
            {
                catalog.entries = new[]
                {
                    new SceneCatalogDefinition.Entry { id = "duel", scene = "Duel" },
                    new SceneCatalogDefinition.Entry { id = "tutorial", scene = "Duel", launch = SceneCatalogDefinition.LaunchKind.DuelTutorial },
                    new SceneCatalogDefinition.Entry { id = "sandbox", scene = "Sandbox" },
                    new SceneCatalogDefinition.Entry { id = "stress", scene = "Sandbox", scenario = stress },
                };
                Assert.That(catalog.Resolve("Sandbox", stress, null).id, Is.EqualTo("stress"));
                Assert.That(catalog.Resolve("Sandbox", sandbox, null).id, Is.EqualTo("sandbox"), "the scene's own scenario");
                Assert.That(catalog.Resolve("Sandbox", sandbox, "stress").id, Is.EqualTo("sandbox"), "started as stress test, but the scene runs its own");
                Assert.That(catalog.Resolve("Duel", null, "tutorial").id, Is.EqualTo("tutorial"));
                Assert.That(catalog.Resolve("Duel", null, "sandbox").id, Is.EqualTo("duel"), "an entry of another scene falls back to the first");
                Assert.That(catalog.Resolve("Elsewhere", null, "duel"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(stress);
                Object.DestroyImmediate(sandbox);
            }
        }
    }
}
