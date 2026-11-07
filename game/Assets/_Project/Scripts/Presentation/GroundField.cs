using System.Collections.Generic;
using Shieldbreak.Sim.Grid;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Keeps the ground under the whole map: one scene serves scenarios of different sizes, so the tiles placed in the
    /// scene are extended up to the map's size plus a margin. The new tiles come from the model, not from a scene tile:
    /// those are static-batched, a copy would draw the combined mesh. Presentation only: the sim has no ground. Lives on
    /// the parent of the tiles. A map with relief or areas gets one mesh built from the grid instead of the tiles, its
    /// forests get trees (plain cones until M4 task 9 brings models from the pipeline; the sim knows no single tree).
    /// </summary>
    public class GroundField : MonoBehaviour
    {
        public SimRunner runner;
        [Tooltip("The ground model; its pivot is the middle of the tile.")]
        public GameObject tileModel;
        [Min(1f)] public float tileSize = 20f;
        [Tooltip("Ground beyond the map edge (m), so the world does not end where the soldiers do.")]
        [Min(0f)] public float margin = 40f;

        [Header("Relief")]
        [Tooltip("Size of one low-poly cell of the relief (m); the sim's ground is finer and stays the truth.")]
        [Min(1f)] public float reliefCell = 3f;
        [Tooltip("Colours of the relief, one per triangle (grass, grass_dark, grass_light of the palette): many of the first, few of the last.")]
        public Color[] reliefColors =
        {
            new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f),
            new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f),
            new Color(0.373f, 0.510f, 0.220f), new Color(0.373f, 0.510f, 0.220f), new Color(0.247f, 0.353f, 0.173f), new Color(0.553f, 0.667f, 0.306f),
        };

        [Tooltip("Colours of the forest floor (grass_dark, foliage of the palette).")]
        public Color[] forestColors = { new Color(0.247f, 0.353f, 0.173f), new Color(0.247f, 0.353f, 0.173f), new Color(0.184f, 0.290f, 0.165f) };
        [Tooltip("Colours of the swamp (dirt, neutral_dark, neutral of the palette).")]
        public Color[] swampColors = { new Color(0.243f, 0.290f, 0.227f), new Color(0.478f, 0.384f, 0.275f), new Color(0.435f, 0.498f, 0.353f) };
        [Tooltip("Crown and trunk of a tree (foliage, wood_dark of the palette).")]
        public Color crownColor = new Color(0.184f, 0.290f, 0.165f);
        public Color trunkColor = new Color(0.290f, 0.184f, 0.133f);

        [Header("Trees")]
        [Tooltip("Share of the forest cells with a tree: thin enough to see the soldiers.")]
        [Range(0f, 1f)] public float treesPerCell = 0.06f;
        [Tooltip("Smallest and largest height of a tree (m).")]
        public Vector2 treeHeight = new Vector2(6f, 9f);

        GameObject relief;
        Material reliefMaterial;
        Texture2D reliefTexture;
        Mesh treeMesh;
        Matrix4x4[] trees = System.Array.Empty<Matrix4x4>();
        int firstForest, firstSwamp, crownColumn, trunkColumn, columns;

        void OnEnable()
        {
            if (runner != null) runner.WorldRebuilt += Fill;
        }

        void OnDisable()
        {
            if (runner != null) runner.WorldRebuilt -= Fill;
        }

        void Start() => Fill();

        void Fill()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || tileModel == null || transform.childCount == 0) return;
            ClearRelief();
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(!world.Grid.HasRelief);
            if (world.Grid.HasRelief)
            {
                BuildRelief(world.Grid);
                return;
            }

            var taken = new HashSet<Vector2Int>();
            for (int i = 0; i < transform.childCount; i++) taken.Add(Tile(transform.GetChild(i).position));

            var layout = world.Grid.Layout;
            var from = new Vector2(layout.Origin.x - margin, layout.Origin.y - margin);
            var to = new Vector2(layout.Origin.x + layout.Width + margin, layout.Origin.y + layout.Height + margin);
            Vector2Int first = Tile(new Vector3(from.x, 0f, from.y)), last = Tile(new Vector3(to.x, 0f, to.y));

            var template = transform.GetChild(0);
            var material = template.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
            for (int x = first.x; x <= last.x; x++)
            {
                for (int y = first.y; y <= last.y; y++)
                {
                    if (!taken.Add(new Vector2Int(x, y))) continue;
                    var position = new Vector3((x + 0.5f) * tileSize, template.position.y, (y + 0.5f) * tileSize);
                    var tile = Instantiate(tileModel, position, template.rotation, transform);
                    tile.name = $"Ground_{x}_{y}";
                    if (material != null) tile.GetComponentInChildren<MeshRenderer>().sharedMaterial = material;
                }
            }
        }

        void ClearRelief()
        {
            if (relief != null) Destroy(relief);
            trees = System.Array.Empty<Matrix4x4>();
        }

        void OnDestroy()
        {
            ClearRelief();
            if (reliefMaterial != null) Destroy(reliefMaterial);
            if (reliefTexture != null) Destroy(reliefTexture);
            if (treeMesh != null) Destroy(treeMesh);
        }

        /// <summary>Flat-shaded triangles over the map and its margin, every triangle with its own vertices and one colour.</summary>
        void BuildRelief(WorldGrid grid)
        {
            var layout = grid.Layout;
            var material = ReliefMaterial();
            int cellsX = Mathf.CeilToInt((layout.Width + 2f * margin) / reliefCell);
            int cellsY = Mathf.CeilToInt((layout.Height + 2f * margin) / reliefCell);
            var vertices = new Vector3[cellsX * cellsY * 6];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[vertices.Length];
            float2 origin = layout.Origin - margin;
            Vector3 Corner(int x, int y)
            {
                float2 p = origin + new float2(x, y) * reliefCell;
                return new Vector3(p.x, grid.HeightAt(p), p.y);
            }

            int n = 0;
            for (int y = 0; y < cellsY; y++)
            for (int x = 0; x < cellsX; x++)
            {
                Vector3 a = Corner(x, y), b = Corner(x + 1, y), c = Corner(x + 1, y + 1), d = Corner(x, y + 1);
                // Clockwise from above: the side facing the sky
                var kind = grid.KindAt(origin + (new float2(x, y) + 0.5f) * reliefCell);
                n = Triangle(vertices, uvs, triangles, n, a, d, b, kind, x * 73 + y * 179);
                n = Triangle(vertices, uvs, triangles, n, b, d, c, kind, x * 151 + y * 37 + 11);
            }

            var mesh = new Mesh { name = "Relief", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            relief = new GameObject("Relief");
            relief.AddComponent<MeshFilter>().sharedMesh = mesh;
            relief.AddComponent<MeshRenderer>().sharedMaterial = material;
            PlantTrees(grid);
        }

        Vector2 Column(int column) => new Vector2((column + 0.5f) / columns, 0.5f);

        int Triangle(Vector3[] vertices, Vector2[] uvs, int[] triangles, int n, Vector3 a, Vector3 b, Vector3 c, GroundKind kind, int seed)
        {
            int pick = seed * 2654435 >> 8 & int.MaxValue;
            var uv = Column(kind == GroundKind.Forest ? firstForest + pick % forestColors.Length : kind == GroundKind.Swamp ? firstSwamp + pick % swampColors.Length : pick % reliefColors.Length);
            vertices[n] = a;
            vertices[n + 1] = b;
            vertices[n + 2] = c;
            for (int k = 0; k < 3; k++)
            {
                uvs[n + k] = uv;
                triangles[n + k] = n + k;
            }
            return n + 3;
        }

        Material ReliefMaterial()
        {
            if (reliefMaterial != null) return reliefMaterial;
            var colours = new List<Color>(reliefColors);
            firstForest = colours.Count;
            colours.AddRange(forestColors);
            firstSwamp = colours.Count;
            colours.AddRange(swampColors);
            crownColumn = colours.Count;
            colours.Add(crownColor);
            trunkColumn = colours.Count;
            colours.Add(trunkColor);
            columns = colours.Count;
            reliefTexture = new Texture2D(columns, 1) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            reliefTexture.SetPixels(colours.ToArray());
            reliefTexture.Apply();
            var scene = transform.GetChild(0).GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            reliefMaterial = new Material(scene) { hideFlags = HideFlags.HideAndDontSave, mainTexture = reliefTexture, enableInstancing = true };
            return reliefMaterial;
        }

        /// <summary>One tree per so many forest cells; the same trees on every start of the map.</summary>
        void PlantTrees(WorldGrid grid)
        {
            var planted = new List<Matrix4x4>();
            var layout = grid.Layout;
            for (int index = 0; index < layout.CellCount; index++)
            {
                if (grid.Ground[index] != (byte)GroundKind.Forest) continue;
                var dice = new Unity.Mathematics.Random((uint)index * 2654435761u + 1u);
                if (dice.NextFloat() >= treesPerCell) continue;
                float2 at = layout.CellCenter(layout.IndexToCell(index)) + dice.NextFloat2(-0.5f, 0.5f);
                float height = Mathf.Lerp(treeHeight.x, treeHeight.y, dice.NextFloat());
                planted.Add(Matrix4x4.TRS(SimRunner.ToWorld(at), Quaternion.Euler(0f, dice.NextFloat(360f), 0f), Vector3.one * height));
            }
            trees = planted.ToArray();
            if (treeMesh == null) treeMesh = BuildTree();
        }

        /// <summary>Six-sided trunk stub and two crowns, one unit high.</summary>
        Mesh BuildTree()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            void Cone(float bottom, float top, float radius, float tip, Vector2 uv)
            {
                const int sides = 6;
                for (int s = 0; s < sides; s++)
                {
                    float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                    var p0 = new Vector3(Mathf.Cos(a0) * radius, bottom, Mathf.Sin(a0) * radius);
                    var p1 = new Vector3(Mathf.Cos(a1) * radius, bottom, Mathf.Sin(a1) * radius);
                    var q0 = new Vector3(p0.x * tip, top, p0.z * tip);
                    var q1 = new Vector3(p1.x * tip, top, p1.z * tip);
                    vertices.AddRange(new[] { p0, q0, p1, p1, q0, q1 });
                    for (int k = 0; k < 6; k++) uvs.Add(uv);
                }
            }

            Cone(0f, 0.3f, 0.045f, 0.8f, Column(trunkColumn));
            Cone(0.2f, 0.7f, 0.26f, 0.35f, Column(crownColumn));
            Cone(0.55f, 1f, 0.19f, 0.02f, Column(crownColumn));
            var triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            var mesh = new Mesh { name = "Tree", vertices = vertices.ToArray(), uv = uvs.ToArray(), triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void LateUpdate()
        {
            if (trees.Length == 0 || treeMesh == null) return;
            var rp = new RenderParams(reliefMaterial) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            const int chunk = 1023;
            for (int start = 0; start < trees.Length; start += chunk)
                Graphics.RenderMeshInstanced(rp, treeMesh, 0, trees, Mathf.Min(chunk, trees.Length - start), start);
        }

        Vector2Int Tile(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
    }
}
