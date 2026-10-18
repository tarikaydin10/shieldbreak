using System.IO;
using Shieldbreak.Data;
using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Presentation.Feedback;
using Shieldbreak.Presentation.Player;
using Shieldbreak.Sim.Combat.Grammar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Builds Scenes/M1_Duel.unity and its data: one-hand and two-hand weapon with their attacks, three feel presets
    /// (Schwer / Mittel / Tödlich), bot roster and the duel scenario. Existing assets keep their tuned values.
    /// </summary>
    public static class DuelSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/M1_Duel.unity";
        const string Root = "Assets/_Project/Data/";
        static readonly string[] DirectionNames = { "Overhead", "Left", "Right", "Thrust" };

        [MenuItem("Shieldbreak/Build M1 Duel Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("M1 Duel", $"{ScenePath} existiert. Neu bauen und überschreiben?", "Überschreiben", "Abbrechen"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var movement = AssetDatabase.LoadAssetAtPath<AgentMovementDefinition>(Root + "Agents/AgentMovement_Default.asset")
                           ?? SandboxSceneBuilder.LoadOrCreate<AgentMovementDefinition>(Root + "Agents/AgentMovement_Default.asset", _ => { });

            var weapon = SandboxSceneBuilder.LoadOrCreate<WeaponDefinition>(Root + "Weapons/Weapon_OneHandShield.asset", w =>
            {
                for (int d = 0; d < AttackDirection.Count; d++)
                    w.attacks[d] = SandboxSceneBuilder.LoadOrCreate<AttackDefinition>($"{Root}Attacks/OneHand_{DirectionNames[d]}.asset", a => a.ApplyDefaults(d));
            });
            var twoHand = SandboxSceneBuilder.LoadOrCreate<WeaponDefinition>(Root + "Weapons/Weapon_TwoHand.asset", w =>
            {
                w.ApplyTwoHandDefaults();
                for (int d = 0; d < AttackDirection.Count; d++)
                    w.attacks[d] = SandboxSceneBuilder.LoadOrCreate<AttackDefinition>($"{Root}Attacks/TwoHand_{DirectionNames[d]}.asset", a => a.ApplyTwoHandDefaults(d));
                w.specialAttack = SandboxSceneBuilder.LoadOrCreate<AttackDefinition>(Root + "Attacks/TwoHand_Leap.asset", a => a.ApplyLeapDefaults());
            });
            var presets = new[]
            {
                SandboxSceneBuilder.LoadOrCreate<CombatTuningDefinition>(Root + "Combat/CombatTuning_Schwer.asset", FillHeavy),
                SandboxSceneBuilder.LoadOrCreate<CombatTuningDefinition>(Root + "Combat/CombatTuning_Mittel.asset", _ => { }),
                SandboxSceneBuilder.LoadOrCreate<CombatTuningDefinition>(Root + "Combat/CombatTuning_Toedlich.asset", FillLethal),
            };
            var roster = SandboxSceneBuilder.LoadOrCreate<BotRosterDefinition>(Root + "Combat/BotRoster_Default.asset", _ => { });
            var duel = SandboxSceneBuilder.LoadOrCreate<DuelScenarioDefinition>(Root + "Scenarios/Duel_M1.asset", s =>
            {
                s.weapons = new[] { weapon, twoHand };
                s.presets = presets;
                s.activePreset = 1;
                s.roster = roster;
            });

            // Older duel assets know only the one-hand weapon: the weapon list is structure, not tuning
            if (duel.weapons.Length < 2 || duel.weapons[1] != twoHand)
            {
                duel.weapons = new[] { weapon, twoHand };
                EditorUtility.SetDirty(duel);
            }

            var visual = AssetDatabase.LoadAssetAtPath<SoldierVisualDefinition>(SandboxSceneBuilder.VisualPath);
            if (visual == null) throw new FileNotFoundException("Build the M0 sandbox first (creates SoldierVisual_Default).");
            SandboxSceneBuilder.SetMesh(visual.teams[0].armlessBody, "CHR_dummy_team_a_noarms");
            SandboxSceneBuilder.SetMesh(visual.teams[1].armlessBody, "CHR_dummy_team_b_noarms");
            SandboxSceneBuilder.SetMesh(visual.teams[0].upperArm, "PRP_limb_team_a");
            SandboxSceneBuilder.SetMesh(visual.teams[1].upperArm, "PRP_limb_team_b");
            SandboxSceneBuilder.SetMesh(visual.forearms[0], "PRP_limb_skin_mid");
            SandboxSceneBuilder.SetMesh(visual.forearms[1], "PRP_limb_skin_light");
            SandboxSceneBuilder.SetMesh(visual.oneHandWeapon, "WPN_sword_onehand");
            SandboxSceneBuilder.SetMesh(visual.twoHandWeapon, "WPN_sword_twohand");
            EditorUtility.SetDirty(visual);
            AssetDatabase.SaveAssets();

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
            SandboxSceneBuilder.BuildGround(duel.arenaSize, duel.arenaSize);

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

            var sim = new GameObject("Sim");
            var runner = sim.AddComponent<SimRunner>();
            runner.scenario = duel;
            runner.movement = movement;
            thirdPerson.runner = runner;
            var figures = sim.AddComponent<CombatFigureRenderer>();
            figures.runner = runner;
            figures.visual = visual;
            var input = sim.AddComponent<PlayerCombatInput>();
            input.runner = runner;
            input.combatCamera = thirdPerson;
            var director = sim.AddComponent<DuelDirector>();
            director.runner = runner;
            director.combatCamera = thirdPerson;
            var effects = sim.AddComponent<ImpactEffects>();
            effects.runner = runner;
            effects.visual = visual;
            sim.AddComponent<CombatAudio>().runner = runner;
            sim.AddComponent<StaminaVignette>().runner = runner;
            sim.AddComponent<TutorialDirector>().runner = runner;
            camGo.AddComponent<AudioListener>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[duel] built {ScenePath}");
        }

        static void FillHeavy(CombatTuningDefinition t)
        {
            t.damagePerJoule = 0.06f;
            t.hitStunMs = 450f;
            t.attackStaminaCost = 14f;
            t.blockStaminaPerJoule = 0.1f;
            t.staminaRegenPerSecond = 16f;
            t.hitstopMs = new Vector2(60f, 110f);
        }

        static void FillLethal(CombatTuningDefinition t)
        {
            t.damagePerJoule = 0.16f;
            t.zoneMultiplier = new Vector3(0.9f, 1.1f, 2f);
            t.perfectBlockMs = 200f;
            t.hitstopMs = new Vector2(30f, 60f);
        }
    }
}
