using System.IO;
using Shieldbreak.Data;
using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Presentation.Feedback;
using Shieldbreak.Presentation.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Builds Scenes/M2_Company.unity (preview of M2 task 8): the hero with his own company against a bot company on
    /// the 96 m sandbox ground, combat camera, fight figures, radial menu. Needs the duel and sandbox assets (run those
    /// builders first). Existing assets are kept so tuning survives a rebuild; the scene is only overwritten after asking.
    /// </summary>
    public static class CompanySceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/M2_Company.unity";
        const string Root = "Assets/_Project/Data/";

        [MenuItem("Shieldbreak/Build M2 Company Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("M2 Kompanie", $"{ScenePath} existiert. Neu bauen und überschreiben?", "Überschreiben", "Abbrechen"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var movement = AssetDatabase.LoadAssetAtPath<AgentMovementDefinition>(Root + "Agents/AgentMovement_Default.asset");
            var visual = AssetDatabase.LoadAssetAtPath<SoldierVisualDefinition>(SandboxSceneBuilder.VisualPath);
            var oneHand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "Weapons/Weapon_OneHandShield.asset");
            var twoHand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Root + "Weapons/Weapon_TwoHand.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuningDefinition>(Root + "Combat/CombatTuning_Mittel.asset");
            if (movement == null || visual == null || oneHand == null || twoHand == null || tuning == null)
                throw new FileNotFoundException("Build the M0 sandbox and M1 duel scenes first (they create movement, visuals, weapons and presets).");
            var company = SandboxSceneBuilder.LoadOrCreate<CompanyScenarioDefinition>(Root + "Scenarios/Company_M2.asset", s =>
            {
                s.ApplyDefaultLayout();
                s.weapons = new[] { oneHand, twoHand };
                s.combatTuning = tuning;
                s.roster = AssetDatabase.LoadAssetAtPath<BotRosterDefinition>(Root + "Combat/BotRoster_Default.asset");
                s.formation = AssetDatabase.LoadAssetAtPath<FormationDefinition>(Root + "Formations/Formation_Default.asset");
                s.morale = AssetDatabase.LoadAssetAtPath<MoraleDefinition>(Root + "Morale/Morale_Default.asset");
            });

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -40f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.57f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.31f, 0.26f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.68f, 0.79f, 0.9f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 160f;
            SandboxSceneBuilder.BuildGround(company.mapSize, company.mapSize);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProtoStyleSceneBuilder.ProfilePath);
            if (profile != null)
            {
                var volume = new GameObject("PostFX").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var thirdPerson = camGo.AddComponent<ThirdPersonCamera>();
            camGo.AddComponent<AudioListener>();

            var sim = new GameObject("Sim");
            var runner = sim.AddComponent<SimRunner>();
            runner.scenario = company;
            runner.movement = movement;
            thirdPerson.runner = runner;
            var figures = sim.AddComponent<CombatFigureRenderer>();
            figures.runner = runner;
            figures.visual = visual;
            figures.maxFigures = 64;
            var input = sim.AddComponent<PlayerCombatInput>();
            input.runner = runner;
            input.combatCamera = thirdPerson;
            var effects = sim.AddComponent<ImpactEffects>();
            effects.runner = runner;
            effects.visual = visual;
            sim.AddComponent<CombatAudio>().runner = runner;
            sim.AddComponent<StaminaVignette>().runner = runner;
            // Soldiers beyond the figure budget, if a scenario has more than 64
            var instanced = sim.AddComponent<AgentRenderer>();
            instanced.runner = runner;
            instanced.visual = visual;
            instanced.figures = figures;
            sim.AddComponent<RadialMenu>().runner = runner;
            var director = sim.AddComponent<CompanyDirector>();
            director.runner = runner;
            director.combatCamera = thirdPerson;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[company] built {ScenePath}");
        }
    }
}
