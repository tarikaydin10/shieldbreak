using System.IO;
using Shieldbreak.Data;
using Shieldbreak.Presentation.Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Builds Scenes/Proto_Animation.unity (M0 task 8): one soldier performing the overhead cut from an
    /// AttackDefinition against a target dummy, with camera bookmarks for the side view, the combat view and the
    /// 15 m readability check. CaptureShots renders key moments into one contact sheet.
    /// </summary>
    public static class ProtoAnimationSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Proto_Animation.unity";
        const string AttackPath = "Assets/_Project/Data/Attacks/Attack_OneHand_Overhead.asset";
        const string ShotDir = "Logs/proto-animation";
        static readonly float[] CapturePoses = { 0f, 0.3f, 0.55f, 0.63f, 0.70f, 0.85f };

        [MenuItem("Shieldbreak/Build Proto_Animation Scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Proto_Animation", $"{ScenePath} existiert. Neu bauen und überschreiben?", "Überschreiben", "Abbrechen"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var attack = SandboxSceneBuilder.LoadOrCreate<AttackDefinition>(AttackPath, _ => { });
            var visual = AssetDatabase.LoadAssetAtPath<SoldierVisualDefinition>(SandboxSceneBuilder.VisualPath);
            if (visual == null) throw new FileNotFoundException("Build the M0 sandbox first (creates SoldierVisual_Default).");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(45f, -60f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.57f, 0.53f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.31f, 0.26f);
            RenderSettings.skybox = null;

            SandboxSceneBuilder.BuildGround(40f, 40f);
            var groundRoot = GameObject.Find("Ground").transform;
            groundRoot.position = new Vector3(-20f, 0f, -20f);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProtoStyleSceneBuilder.ProfilePath);
            if (profile != null)
            {
                var volume = new GameObject("PostFX").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            // Attacker at the origin facing +Z, target dummy (team B) at chest-contact distance facing back
            var attackerGo = new GameObject("Attacker");
            var preview = attackerGo.AddComponent<AttackPreview>();
            preview.attack = attack;
            preview.visual = visual;
            SandboxSceneBuilder.SetMesh(preview.body, "CHR_dummy_team_a_left_arm");
            preview.upperArmMaterial = SandboxSceneBuilder.LitMaterial("M_ProtoArm_Tunic", "#B2352B");
            preview.forearmMaterial = SandboxSceneBuilder.LitMaterial("M_ProtoArm_Skin", "#B9835C");
            preview.bladeMaterial = SandboxSceneBuilder.LitMaterial("M_ProtoBlade", "#A9AFB8");
            EditorUtility.SetDirty(preview);

            var target = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedArtImporter.GeneratedDir + "CHR_dummy_team_b.fbx"));
            target.name = "Target";
            target.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.25f), Quaternion.Euler(0f, 180f, 0f));

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.68f, 0.79f, 0.9f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = camGo.AddComponent<StyleCameraRig>();
            rig.bookmarks = new[]
            {
                new StyleCameraRig.Bookmark { label = "Seite, 3,5 m (Bahn und Arm)", target = new Vector3(0f, 1.4f, 0.4f), yaw = -90f, pitch = 4f, distance = 3.5f },
                new StyleCameraRig.Bookmark { label = "Über der Schulter (Kampfkamera)", target = new Vector3(0.3f, 1.5f, 0.6f), yaw = 15f, pitch = 10f, distance = 3.2f },
                new StyleCameraRig.Bookmark { label = "Gegner-Sicht, 15 m (Lesbarkeit)", target = new Vector3(0f, 1.3f, 0f), yaw = 160f, pitch = 3f, distance = 15f },
                new StyleCameraRig.Bookmark { label = "Gegner-Sicht, 4 m", target = new Vector3(0f, 1.4f, 0f), yaw = 160f, pitch = 5f, distance = 4f },
            };
            rig.Apply(0, true);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[proto-animation] built {ScenePath}");
        }

        /// <summary>Renders CapturePoses from the side and from 15 m into Logs/proto-animation/sheet.png.</summary>
        [MenuItem("Shieldbreak/Capture Proto_Animation Shots")]
        public static void CaptureShots()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var preview = Object.FindAnyObjectByType<AttackPreview>();
            var rig = Object.FindAnyObjectByType<StyleCameraRig>();
            var cam = rig.GetComponent<Camera>();
            const int w = 640, h = 640;
            // Each view is rendered at the screen height it is judged at, then the centre is cropped: the 15 m row
            // at 1080p so the soldier has the size he would have on a real screen
            (int view, int renderSize)[] rows = { (0, 640), (2, 1080) };
            var frame = new Texture2D(w, h, TextureFormat.RGB24, false);
            var sheet = new Texture2D(w * CapturePoses.Length, h * rows.Length, TextureFormat.RGB24, false);
            Directory.CreateDirectory(ShotDir);
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;   // otherwise the first frame shows placeholder shaders

            for (int row = 0; row < rows.Length; row++)
            {
                int size = rows[row].renderSize;
                var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rig.Apply(rows[row].view, true);
                // Throw-away frame: the very first render still lacks uploaded textures
                preview.ShowPose(0f);
                cam.targetTexture = rt;
                cam.Render();
                for (int col = 0; col < CapturePoses.Length; col++)
                {
                    preview.ShowPose(CapturePoses[col]);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    frame.ReadPixels(new Rect((size - w) / 2, (size - h) / 2, w, h), 0, 0);
                    frame.Apply();
                    // Sheet rows top to bottom = rows in order (texture y grows upwards)
                    sheet.SetPixels(col * w, (rows.Length - 1 - row) * h, w, h, frame.GetPixels());
                }
                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);
            }
            ShaderUtil.allowAsyncCompilation = asyncShaders;
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, "sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(frame);
            Object.DestroyImmediate(sheet);
            Debug.Log($"[proto-animation] sheet written to {Path.GetFullPath(ShotDir)}");
        }

        public static void BuildAndCapture()
        {
            Build();
            CaptureShots();
        }
    }
}
