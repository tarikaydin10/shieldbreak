using System.IO;
using System.Linq;
using NUnit.Framework;
using Shieldbreak.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// Every model in Art/Generated must come from tools/assetgen unmodified and use the shared palette material
    /// (art/style/style-bible.md). Geometry reproducibility itself is checked by tools/assetgen/run.ps1 -Check.
    /// </summary>
    public class ArtPipelineTests
    {
        [Test]
        public void ManifestListsEveryGeneratedModelWithMatchingHash()
        {
            var manifest = GeneratedArtImporter.LoadManifest();
            Assert.That(manifest?.assets, Is.Not.Null.And.Not.Empty, "manifest.json missing or empty");

            var files = Directory.GetFiles(GeneratedArtImporter.GeneratedDir, "*.fbx").Select(f => f.Replace('\\', '/')).ToArray();
            foreach (var file in files)
                Assert.That(GeneratedArtImporter.VerifyAgainstManifest(file), Is.Null, file);
            foreach (var asset in manifest.assets)
                Assert.That(files.Any(f => Path.GetFileName(f) == Path.GetFileName(asset.fbx)), $"{asset.fbx} listed but missing");
        }

        [Test]
        public void GeneratedModelsUseOnlyThePaletteMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtImporter.MaterialPath);
            Assert.That(material, Is.Not.Null, "M_Palette.mat missing");

            foreach (var asset in GeneratedArtImporter.LoadManifest().assets)
            {
                string path = GeneratedArtImporter.GeneratedDir + Path.GetFileName(asset.fbx);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(model, Is.Not.Null, path);
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, path);
                foreach (var renderer in renderers)
                    Assert.That(renderer.sharedMaterials, Is.All.EqualTo(material), path);
            }
        }

        [Test]
        public void PaletteTextureIsPointFilteredWithoutMips()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(GeneratedArtImporter.PalettePath);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }
    }
}
