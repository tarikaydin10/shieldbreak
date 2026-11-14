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
    /// forests get trees and a few rocks, models from the pipeline (art/specs) drawn with instancing; the sim knows no single tree.
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
        [Header("Trees and rocks (models from the pipeline)")]
        [Tooltip("Tree models (ENV_tree_*); each tree picks one.")]
        public GameObject[] treeModels = System.Array.Empty<GameObject>();
        [Tooltip("Rock models (ENV_rock_*).")]
        public GameObject[] rockModels = System.Array.Empty<GameObject>();
        [Tooltip("Share of the forest and swamp cells with a rock.")]
        [Range(0f, 1f)] public float rocksPerCell = 0.008f;
        [Tooltip("Smallest and largest scale of a rock.")]
        public Vector2 rockScale = new Vector2(0.7f, 1.5f);
        [Tooltip("Share of the forest cells with a tree: thin enough to see the soldiers.")]
        [Range(0f, 1f)] public float treesPerCell = 0.06f;
        [Tooltip("Smallest and largest height of a tree (m).")]
        public Vector2 treeHeight = new Vector2(6f, 9f);

        GameObject relief;
        Material reliefMaterial;
        Texture2D reliefTexture;
        GeneratedModel[] treeKinds = System.Array.Empty<GeneratedModel>(), rockKinds = System.Array.Empty<GeneratedModel>();
        /// <summary>Planted instances per model: the trees first, then the rocks.</summary>
        Matrix4x4[][] planted = System.Array.Empty<Matrix4x4[]>();
        Material modelMaterial;
        int firstForest, firstSwamp, columns;

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
            planted = System.Array.Empty<Matrix4x4[]>();
        }

        void OnDestroy()
        {
            ClearRelief();
            if (reliefMaterial != null) Destroy(reliefMaterial);
            if (reliefTexture != null) Destroy(reliefTexture);
            if (modelMaterial != null) Destroy(modelMaterial);
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
            Plant(grid);
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
            columns = colours.Count;
            reliefTexture = new Texture2D(columns, 1) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            reliefTexture.SetPixels(colours.ToArray());
            reliefTexture.Apply();
            var scene = transform.GetChild(0).GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            reliefMaterial = new Material(scene) { hideFlags = HideFlags.HideAndDontSave, mainTexture = reliefTexture, enableInstancing = true };
            return reliefMaterial;
        }

        static GeneratedModel[] Read(GameObject[] models)
        {
            var read = new List<GeneratedModel>();
            foreach (var m in models)
            {
                var model = GeneratedModel.From(m);
                if (model.IsValid) read.Add(model);
            }
            return read.ToArray();
        }

        /// <summary>
        /// One tree per so many forest cells, a rock now and then in forest and swamp; the same on every start of the map.
        /// The dice come from the cell, never from the sim.
        /// </summary>
        void Plant(WorldGrid grid)
        {
            treeKinds = Read(treeModels);
            rockKinds = Read(rockModels);
            var lists = new List<Matrix4x4>[treeKinds.Length + rockKinds.Length];
            for (int k = 0; k < lists.Length; k++) lists[k] = new List<Matrix4x4>();
            var layout = grid.Layout;
            for (int index = 0; index < layout.CellCount; index++)
            {
                byte kind = grid.Ground[index];
                if (kind == (byte)GroundKind.Open) continue;
                var dice = new Unity.Mathematics.Random((uint)index * 2654435761u + 1u);
                float2 at = layout.CellCenter(layout.IndexToCell(index)) + dice.NextFloat2(-0.5f, 0.5f);
                var turn = Quaternion.Euler(0f, dice.NextFloat(360f), 0f);
                float roll = dice.NextFloat();
                if (kind == (byte)GroundKind.Forest && roll < treesPerCell && treeKinds.Length > 0)
                {
                    int pick = dice.NextInt(treeKinds.Length);
                    float height = Mathf.Lerp(treeHeight.x, treeHeight.y, dice.NextFloat());
                    float scale = height / Mathf.Max(0.1f, treeKinds[pick].Bounds.size.y);
                    lists[pick].Add(Matrix4x4.TRS(SimRunner.ToWorld(at), turn, Vector3.one * scale) * treeKinds[pick].Matrix);
                }
                else if (roll > 1f - rocksPerCell && rockKinds.Length > 0)
                {
                    int pick = dice.NextInt(rockKinds.Length);
                    float scale = Mathf.Lerp(rockScale.x, rockScale.y, dice.NextFloat());
                    lists[treeKinds.Length + pick].Add(Matrix4x4.TRS(SimRunner.ToWorld(at, -0.1f), turn, Vector3.one * scale) * rockKinds[pick].Matrix);
                }
            }
            planted = new Matrix4x4[lists.Length][];
            for (int k = 0; k < lists.Length; k++) planted[k] = lists[k].ToArray();
            if (modelMaterial == null && lists.Length > 0)
            {
                var palette = (treeModels.Length > 0 ? treeModels[0] : rockModels[0]).GetComponentInChildren<MeshRenderer>(true);
                var scene = palette != null && palette.sharedMaterial != null ? palette.sharedMaterial : transform.GetChild(0).GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                modelMaterial = new Material(scene) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
            }
        }

        void LateUpdate()
        {
            if (planted.Length == 0 || modelMaterial == null) return;
            var rp = new RenderParams(modelMaterial) { shadowCastingMode = ShadowCastingMode.On, receiveShadows = true };
            const int chunk = 1023;
            for (int k = 0; k < planted.Length; k++)
            {
                var mesh = k < treeKinds.Length ? treeKinds[k].Mesh : rockKinds[k - treeKinds.Length].Mesh;
                var list = planted[k];
                for (int start = 0; start < list.Length; start += chunk)
                    Graphics.RenderMeshInstanced(rp, mesh, 0, list, Mathf.Min(chunk, list.Length - start), start);
            }
        }

        Vector2Int Tile(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
    }
}
