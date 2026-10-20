using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shieldbreak.Data;
using Shieldbreak.DebugTools;
using Shieldbreak.Presentation;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Sim.Formations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Builds Scenes/M0_Sandbox.unity (debug top-down view, soldiers, selection, formation and move orders, 20 against
    /// 20 with the duel's weapons) plus its data assets. Existing assets are kept so tuning survives a rebuild; the
    /// scene is only overwritten after asking.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/M0_Sandbox.unity";
        const string MovementPath = "Assets/_Project/Data/Agents/AgentMovement_Default.asset";
        const string SandboxPath = "Assets/_Project/Data/Scenarios/Sandbox_20v20.asset";
        const string StressPath = "Assets/_Project/Data/Scenarios/Stress_250v250.asset";
        const string FormationPath = "Assets/_Project/Data/Formations/Formation_Default.asset";
        const string OneHandPath = "Assets/_Project/Data/Weapons/Weapon_OneHandShield.asset";
        const string TwoHandPath = "Assets/_Project/Data/Weapons/Weapon_TwoHand.asset";
        const string TuningPath = "Assets/_Project/Data/Combat/CombatTuning_Mittel.asset";
        const string RosterPath = "Assets/_Project/Data/Combat/BotRoster_Default.asset";
        internal const string VisualPath = "Assets/_Project/Data/Visuals/SoldierVisual_Default.asset";
        const string Gen = GeneratedArtImporter.GeneratedDir;
        const float GroundTile = 20f;
        // Ground patches have up to 0.3 m of bumps; sink them a little so feet do not float over the dips
        const float GroundSink = 0.1f;
        const string MaterialDir = "Assets/_Project/Art/Debug";

        [MenuItem("Shieldbreak/Build M0 Sandbox Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("M0 Sandbox", $"{ScenePath} existiert. Neu bauen und überschreiben?", "Überschreiben", "Abbrechen"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var movement = LoadOrCreate<AgentMovementDefinition>(MovementPath, _ => { });
            var formation = LoadOrCreate<FormationDefinition>(FormationPath, _ => { });
            var sandbox = LoadOrCreate<SandboxScenarioDefinition>(SandboxPath, s =>
            {
                FillSandbox(s);
                s.formation = formation;
            });
            LoadOrCreate<SandboxScenarioDefinition>(StressPath, FillStress);
            // The 20v20 fights with the duel's weapons once they exist (the duel builder creates them)
            if (!sandbox.HasCombat && FillCombat(sandbox)) EditorUtility.SetDirty(sandbox);

            var teamASel = LitMaterial("M_Agent_TeamA_Selected", "#E0664B", emission: 0.35f);
            var teamBSel = LitMaterial("M_Agent_TeamB_Selected", "#5E92D6", emission: 0.35f);

            GeneratedArtImporter.EnsurePaletteMaterial();
            var palette = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtImporter.MaterialPath);
            if (!palette.enableInstancing)
            {
                palette.enableInstancing = true;
                EditorUtility.SetDirty(palette);
            }
            var visual = LoadOrCreate<SoldierVisualDefinition>(VisualPath, FillVisualOffsets);
            RefreshVisual(visual, palette, new[] { teamASel, teamBSel });

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float w = sandbox.mapWidth, h = sandbox.mapHeight;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.57f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.31f, 0.26f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            BuildGround(w, h);

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
            cam.backgroundColor = new Color(0.68f, 0.79f, 0.9f);
            cam.farClipPlane = 600f;
            cam.fieldOfView = 50f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var topDown = camGo.AddComponent<DebugTopDownCamera>();
            topDown.bounds = new Rect(0f, 0f, w, h);
            topDown.focus = new Vector3(w * 0.5f, 0f, h * 0.5f);
            topDown.distance = 40f;

            var sim = new GameObject("Sim");
            var runner = sim.AddComponent<SimRunner>();
            runner.scenario = sandbox;
            runner.movement = movement;
            var renderer = sim.AddComponent<AgentRenderer>();
            renderer.runner = runner;
            renderer.visual = visual;
            // Fighting figures for the 20v20 (swings, blocks, stuns, deaths); the instanced renderer draws the rest
            var figures = sim.AddComponent<CombatFigureRenderer>();
            figures.runner = runner;
            figures.visual = visual;
            figures.maxFigures = 64;
            renderer.figures = figures;
            var orders = sim.AddComponent<SandboxOrders>();
            orders.runner = runner;
            orders.viewCamera = cam;
            var overlay = sim.AddComponent<SimDebugOverlay>();
            overlay.runner = runner;
            overlay.viewCamera = cam;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[sandbox] built {ScenePath}");
        }

        internal static void BuildGround(float w, float h)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Gen + "ENV_ground_grass.fbx");
            if (model == null) throw new FileNotFoundException("ENV_ground_grass.fbx missing; run tools/assetgen/run.ps1");
            var root = new GameObject("Ground").transform;
            int nx = Mathf.CeilToInt(w / GroundTile), nz = Mathf.CeilToInt(h / GroundTile);
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(model);
                tile.name = $"Ground_{i}_{j}";
                tile.transform.SetParent(root, false);
                tile.transform.localPosition = new Vector3((i + 0.5f) * GroundTile, -GroundSink, (j + 0.5f) * GroundTile);
                tile.transform.localRotation = Quaternion.Euler(0f, ((i * 3 + j * 5) % 4) * 90f, 0f);
                GameObjectUtility.SetStaticEditorFlags(tile, StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>Attach points, same as Proto_Style (hands at ±0.29 m, 0.9 m high; head centre 1.63 m).</summary>
        static void FillVisualOffsets(SoldierVisualDefinition v)
        {
            foreach (var team in v.teams)
            {
                team.helmet.offset = new Vector3(0f, 1.65f, 0f);
                team.shield.offset = new Vector3(-0.24f, 1.0f, 0.2f);
                team.shield.yaw = -8f;
                team.weapon.offset = new Vector3(0.29f, 0.95f, 0.04f);
            }
        }

        /// <summary>Meshes and materials are refreshed on every build; tuned offsets are kept.</summary>
        static void RefreshVisual(SoldierVisualDefinition v, Material palette, Material[] selection)
        {
            v.material = palette;
            v.selectionMaterials = selection;
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            v.selectionMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(cylinder);

            string[,] names =
            {
                { "CHR_dummy_team_a", "ARM_helmet_nasal_crest_a", "SHD_round_team_a", "WPN_spear_basic" },
                { "CHR_dummy_team_b", "ARM_helmet_nasal", "SHD_round_team_b", "WPN_spear_basic" },
            };
            for (int t = 0; t < 2; t++)
            {
                var parts = v.teams[t].Parts;
                for (int k = 0; k < parts.Length; k++) SetMesh(parts[k], names[t, k]);
            }
            EditorUtility.SetDirty(v);
            AssetDatabase.SaveAssets();
        }

        internal static void SetMesh(SoldierVisualDefinition.Part part, string asset)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Gen + asset + ".fbx");
            if (model == null) throw new FileNotFoundException($"{asset}.fbx missing; run tools/assetgen/run.ps1");
            var filter = model.GetComponentInChildren<MeshFilter>();
            part.mesh = filter.sharedMesh;
            // Same convention as Proto_Style: model root placed with identity rotation, imported scale kept
            var root = model.transform;
            part.meshMatrix = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        }

        static void FillSandbox(SandboxScenarioDefinition s)
        {
            s.mapWidth = 96;
            s.mapHeight = 96;
            s.groups = new[]
            {
                Group("A Linie", 0, 10, 5, new Vector2(38f, 42f), new Vector2(58f, 42f)),
                Group("A Schildwall", 0, 10, 5, new Vector2(38f, 54f), new Vector2(58f, 54f), FormationType.ShieldWall),
                Group("B Keil", 1, 10, 9, new Vector2(58f, 54f), new Vector2(38f, 54f), FormationType.Wedge),
                Group("B Linie", 1, 10, 5, new Vector2(58f, 42f), new Vector2(38f, 42f)),
            };
            // A wall with a 6 m gate to the north, a rock to the south, brush behind team B
            s.obstacles = new[]
            {
                Obstacle("Mauer links", new Vector2(26f, 64f), new Vector2(45f, 66f), 255),
                Obstacle("Mauer rechts", new Vector2(51f, 64f), new Vector2(70f, 66f), 255),
                Obstacle("Fels", new Vector2(42f, 24f), new Vector2(54f, 32f), 255),
                Obstacle("Gestrüpp", new Vector2(70f, 36f), new Vector2(78f, 60f), 4),
            };
        }

        /// <summary>Weapons (sword and shield, two-hander), the "Mittel" preset and the bot roster; false if the duel assets do not exist yet.</summary>
        static bool FillCombat(SandboxScenarioDefinition s)
        {
            var oneHand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(OneHandPath);
            var twoHand = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(TwoHandPath);
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuningDefinition>(TuningPath);
            if (oneHand == null || twoHand == null || tuning == null) return false;
            s.weapons = new[] { oneHand, twoHand };
            s.combatTuning = tuning;
            s.roster = AssetDatabase.LoadAssetAtPath<BotRosterDefinition>(RosterPath);
            return true;
        }

        static void FillStress(SandboxScenarioDefinition s)
        {
            s.mapWidth = 128;
            s.mapHeight = 128;
            s.groups = new[]
            {
                // Loose blocks: the stress test measures steering and combat, not 250-soldier reforms
                Group("A Block", 0, 250, 25, new Vector2(52f, 64f), new Vector2(76f, 64f), loose: true),
                Group("B Block", 1, 250, 25, new Vector2(76f, 64f), new Vector2(52f, 64f), loose: true),
            };
        }

        static SandboxScenarioDefinition.SpawnGroup Group(string name, int team, int count, int files, Vector2 front, Vector2 face,
            FormationType type = FormationType.Line, bool loose = false) =>
            new SandboxScenarioDefinition.SpawnGroup
            {
                name = name, team = team, count = count, files = files, front = front, faceTowards = face,
                asFormation = !loose, formationType = type,
            };

        static SandboxScenarioDefinition.Obstacle Obstacle(string name, Vector2 min, Vector2 max, int cost) =>
            new SandboxScenarioDefinition.Obstacle { name = name, min = min, max = max, cost = cost };

        internal static T LoadOrCreate<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        internal static Material LitMaterial(string name, string hex, float emission = 0f, bool instancing = true)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Directory.CreateDirectory(MaterialDir);
            ColorUtility.TryParseHtmlString(hex, out var color);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = instancing };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
