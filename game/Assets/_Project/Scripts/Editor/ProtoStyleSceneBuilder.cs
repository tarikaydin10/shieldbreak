using System.IO;
using Shieldbreak.Presentation.Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Builds Scenes/Proto_Style.unity from the pipeline assets: tiled ground, two small formations of dummy soldiers
    /// with helmet, shield and spear, the asset lineup, light, fog and the global post-FX profile. The scene and
    /// VP_Style.asset are the tuning surface afterwards; the builder never overwrites them without asking.
    /// </summary>
    public static class ProtoStyleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Proto_Style.unity";
        public const string ProfilePath = "Assets/_Project/Art/PostFX/VP_Style.asset";
        const string Gen = GeneratedArtImporter.GeneratedDir;
        const string ShotDir = "Logs/proto-style";

        static readonly Color FogColor = new Color(0.68f, 0.79f, 0.90f);

        [MenuItem("Shieldbreak/Art/Build Proto_Style Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Proto_Style", $"{ScenePath} existiert. Neu bauen und überschreiben?", "Überschreiben", "Abbrechen"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.57f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.31f, 0.26f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 260f;
            RenderSettings.sun = sun;

            BuildGround();
            BuildFormation("Team A", "CHR_dummy_team_a", "ARM_helmet_nasal_crest_a", "SHD_round_team_a", new Vector3(0, 0, -7f), 0f, 11);
            BuildFormation("Team B", "CHR_dummy_team_b", "ARM_helmet_nasal", "SHD_round_team_b", new Vector3(0, 0, 7f), 180f, 23);
            BuildLineup();

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = EnsureProfile();

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FogColor;
            camera.farClipPlane = 400f;
            camera.fieldOfView = 50f;
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = cameraGo.AddComponent<StyleCameraRig>();
            rig.bookmarks = new[]
            {
                new StyleCameraRig.Bookmark { label = "Nahkampf 4 m (Team A von vorn)", target = new Vector3(0, 1.4f, -7f), yaw = 160f, pitch = 6f, distance = 4f },
                new StyleCameraRig.Bookmark { label = "Kampf 15 m", target = new Vector3(0, 1f, 0), yaw = 125f, pitch = 14f, distance = 15f },
                new StyleCameraRig.Bookmark { label = "Lesbarkeit 40 m", target = new Vector3(0, 1f, 0), yaw = 95f, pitch = 8f, distance = 40f },
                new StyleCameraRig.Bookmark { label = "Kommandoansicht ~35 m hoch", target = new Vector3(0, 0, 2f), yaw = 0f, pitch = 52f, distance = 45f },
                new StyleCameraRig.Bookmark { label = "Asset-Reihe", target = new Vector3(0, 0.8f, -16f), yaw = 180f, pitch = 12f, distance = 6f },
            };
            rig.Apply(0, true);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[proto-style] built {ScenePath}");
        }

        static GameObject Place(string asset, string name, Transform parent, Vector3 localPos, float yaw)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Gen + asset + ".fbx");
            if (model == null) throw new FileNotFoundException($"{Gen}{asset}.fbx missing; run tools/assetgen/run.ps1");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static void BuildGround()
        {
            var root = new GameObject("Ground").transform;
            const int n = 9;
            const float tile = 20f;
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var pos = new Vector3((i - n / 2) * tile, 0f, (j - n / 2) * tile);
                var go = Place("ENV_ground_grass", $"Ground_{i}_{j}", root, pos, ((i * 3 + j * 5) % 4) * 90f);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static void BuildFormation(string label, string body, string helmet, string shield, Vector3 front, float facing, int seed)
        {
            var root = new GameObject(label).transform;
            root.SetPositionAndRotation(front, Quaternion.Euler(0f, facing, 0f));
            var random = new System.Random(seed);
            const int width = 10, depth = 3;
            for (int row = 0; row < depth; row++)
            for (int col = 0; col < width; col++)
            {
                float jx = (float)(random.NextDouble() - 0.5) * 0.12f;
                float jz = (float)(random.NextDouble() - 0.5) * 0.12f;
                var pos = new Vector3((col - (width - 1) * 0.5f) * 0.95f + jx, 0f, -row * 1.25f + jz);
                var soldier = new GameObject($"Soldier_{row}_{col}").transform;
                soldier.SetParent(root, false);
                soldier.localPosition = pos;
                soldier.localRotation = Quaternion.Euler(0f, (float)(random.NextDouble() - 0.5) * 8f, 0f);
                Place(body, "Body", soldier, Vector3.zero, 0f);
                Place(helmet, "Helmet", soldier, new Vector3(0f, 1.65f, 0f), 0f);
                Place(shield, "Shield", soldier, new Vector3(-0.24f, 1.0f, 0.2f), -8f);
                Place("WPN_spear_basic", "Spear", soldier, new Vector3(0.29f, 0.95f, 0.04f), 0f);
            }
        }

        static void BuildLineup()
        {
            var root = new GameObject("Asset Lineup").transform;
            root.position = new Vector3(0f, 0f, -16f);
            var manifest = GeneratedArtImporter.LoadManifest();
            float x = 0f;
            var names = new System.Collections.Generic.List<string>();
            foreach (var a in manifest.assets)
                if (a.category != "environment") names.Add(a.name);
            float spacing = 0.9f;
            float start = -(names.Count - 1) * spacing * 0.5f;
            foreach (var name in names)
            {
                float y = name.StartsWith("WPN_") ? 0.95f : name.StartsWith("SHD_") ? 0.48f : name.StartsWith("ARM_") ? 0.08f : 0f;
                Place(name, name, root, new Vector3(start + x, y, 0f), 0f);
                x += spacing;
            }
        }

        static VolumeProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;

            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.25f;
            color.contrast.value = 14f;
            color.saturation.value = 12f;
            color.colorFilter.value = new Color(1f, 0.98f, 0.95f);
            color.hueShift.overrideState = false;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.05f;
            bloom.intensity.value = 0.3f;
            bloom.scatter.value = 0.6f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.45f;
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.value = new Vector4(0.92f, 0.95f, 1.08f, 0f);
            smh.highlights.value = new Vector4(1.06f, 1.02f, 0.94f, 0f);

            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Renders every camera bookmark to game/Logs/proto-style/*.png (batchmode-capable).</summary>
        [MenuItem("Shieldbreak/Art/Capture Proto_Style Shots")]
        public static void CaptureShots()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var rig = Object.FindAnyObjectByType<StyleCameraRig>();
            var camera = rig.GetComponent<Camera>();
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Directory.CreateDirectory(ShotDir);
            for (int i = 0; i < rig.Count; i++)
            {
                rig.Apply(i, true);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(ShotDir, $"shot_{i + 1}.png"), tex.EncodeToPNG());
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log($"[proto-style] {rig.Count} shots written to {Path.GetFullPath(ShotDir)}");
        }

        public static void BuildAndCapture()
        {
            Build();
            CaptureShots();
        }
    }
}
