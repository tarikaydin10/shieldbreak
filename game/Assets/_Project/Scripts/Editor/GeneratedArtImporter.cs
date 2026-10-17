using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Shieldbreak.EditorTools
{
    /// <summary>
    /// Import rules for the output of tools/assetgen (see art/style/style-bible.md): fixed model import settings,
    /// one shared palette material, point-filtered palette texture, and a manifest check that flags models
    /// which were not produced (or were modified after being produced) by the pipeline.
    /// </summary>
    public class GeneratedArtImporter : AssetPostprocessor
    {
        public const string GeneratedDir = "Assets/_Project/Art/Generated/";
        public const string ManifestPath = GeneratedDir + "manifest.json";
        public const string PalettePath = "Assets/_Project/Art/Palette/palette.png";
        public const string MaterialPath = "Assets/_Project/Art/Palette/M_Palette.mat";

        [Serializable]
        public class ManifestAsset
        {
            public string name;
            public string category;
            public string fbx;
            public string fbx_sha256;
            public string geometry_sha256;
            public int tris;
        }

        [Serializable]
        public class Manifest
        {
            public int generator_version;
            public ManifestAsset[] assets;
        }

        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(GeneratedDir, StringComparison.Ordinal)) return;
            context.DependsOnSourceAsset(ManifestPath);
            context.DependsOnSourceAsset(MaterialPath);

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalSmoothingAngle = 0f;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;

            string error = VerifyAgainstManifest(assetPath);
            if (error != null)
                Debug.LogError($"[assetgen] {assetPath}: {error} Final assets come only from tools/assetgen/run.ps1.");
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(GeneratedDir, StringComparison.Ordinal)) return;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[assetgen] {MaterialPath} missing; run Shieldbreak/Art/Ensure Palette Material.");
                return;
            }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
        }

        void OnPreprocessTexture()
        {
            if (assetPath != PalettePath) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        public static Manifest LoadManifest()
        {
            return File.Exists(ManifestPath) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) : null;
        }

        /// <summary>Returns null when the file is listed in the manifest with a matching hash, else the reason.</summary>
        public static string VerifyAgainstManifest(string modelPath)
        {
            var manifest = LoadManifest();
            if (manifest?.assets == null) return "manifest.json missing.";
            string file = Path.GetFileName(modelPath);
            var entry = manifest.assets.FirstOrDefault(a => Path.GetFileName(a.fbx) == file);
            if (entry == null) return "not listed in manifest.json.";
            if (Sha256(modelPath) != entry.fbx_sha256) return "file hash differs from manifest.json (edited by hand?).";
            return null;
        }

        public static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [MenuItem("Shieldbreak/Art/Ensure Palette Material")]
        public static void EnsurePaletteMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
            var material = new Material(shader) { name = "M_Palette", enableInstancing = true };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[assetgen] created {MaterialPath}");
        }
    }
}
