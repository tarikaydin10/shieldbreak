using UnityEngine;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// A model from the asset pipeline (assetforge, art/specs) as a mesh and the matrix from mesh to model space: the FBX
    /// importer puts the mesh under a node that turns Blender's Z-up into Unity's Y-up. Read from the imported model
    /// asset, so presentation can draw it with instancing or put it on its own GameObjects.
    /// </summary>
    public readonly struct GeneratedModel
    {
        public readonly Mesh Mesh;
        public readonly Matrix4x4 Matrix;
        /// <summary>Bounds of the mesh in model space (after Matrix): its height, its footprint.</summary>
        public readonly Bounds Bounds;

        GeneratedModel(Mesh mesh, Matrix4x4 matrix, Bounds bounds)
        {
            Mesh = mesh;
            Matrix = matrix;
            Bounds = bounds;
        }

        public bool IsValid => Mesh != null;

        public static GeneratedModel From(GameObject model)
        {
            if (model == null) return default;
            var filter = model.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null) return default;
            var root = model.transform;
            var matrix = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var local = filter.sharedMesh.bounds;
            var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                bounds.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return new GeneratedModel(filter.sharedMesh, matrix, bounds);
        }

        /// <summary>Puts the mesh on a new child GameObject of <paramref name="parent"/>, at <paramref name="local"/> in the parent.</summary>
        public Transform Attach(Transform parent, string name, Material material, Matrix4x4 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var m = local * Matrix;
            go.transform.localPosition = m.GetPosition();
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            return go.transform;
        }
    }
}
