using System.IO;
using Shieldbreak.Data;
using Shieldbreak.Presentation.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shieldbreak.EditorTools
{
    /// <summary>Builds Scenes/MainMenu.unity: a camera on a dark backdrop and the MainMenu component (first scene of the build).</summary>
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";

        [MenuItem("Shieldbreak/Build Main Menu Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Main Menu", $"{ScenePath} existiert. Neu bauen und überschreiben?", "Überschreiben", "Abbrechen"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.13f, 0.15f);
            camGo.AddComponent<AudioListener>();

            var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            menu.duel = AssetDatabase.LoadAssetAtPath<DuelScenarioDefinition>("Assets/_Project/Data/Scenarios/Duel_M1.asset");

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[menu] built {ScenePath}");
        }
    }
}
