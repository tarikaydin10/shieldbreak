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
        [Tooltip("Size of one tile of the relief (m): the camera and the shadows skip the tiles they do not see.")]
        [Min(3f)] public float reliefTile = 60f;
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
        [Tooltip("Share of the forest cells with a tree. The crowns sit high, so the soldiers stay visible under them.")]
        [Range(0f, 1f)] public float treesPerCell = 0.07f;
        [Tooltip("Smallest and largest height of a tree (m).")]
        public Vector2 treeHeight = new Vector2(14f, 22f);
        [Tooltip("Bush models (ENV_bush_*), the undergrowth of the forest.")]
        public GameObject[] bushModels = System.Array.Empty<GameObject>();
        [Tooltip("Share of the forest cells with a bush.")]
        [Range(0f, 1f)] public float bushesPerCell = 0.05f;
        [Tooltip("Share of the open cells with a bush.")]
        [Range(0f, 1f)] public float openBushesPerCell = 0.002f;
        [Tooltip("Smallest and largest scale of a bush.")]
        public Vector2 bushScale = new Vector2(0.8f, 1.6f);

        [Header("Backdrop beyond the map (a map with relief)")]
        [Tooltip("How far the land goes on beyond the map edge (m); nobody walks there, it is the horizon.")]
        [Min(0f)] public float backdrop = 140f;
        [Tooltip("Flat strip along the map edge before the land rises (m).")]
        [Min(0f)] public float backdropFlat = 12f;
        [Tooltip("Height the hills beyond the map reach at the far end (m), on average.")]
        [Min(0f)] public float backdropHeight = 26f;
        [Tooltip("Length of the hills beyond the map (m).")]
        [Min(5f)] public float backdropWavelength = 70f;
        [Tooltip("Share of the land beyond the map under forest.")]
        [Range(0f, 1f)] public float backdropForest = 0.45f;
        [Tooltip("Distance between the trees of the forest beyond the map (m).")]
        [Min(1f)] public float backdropTreeSpacing = 6f;
        [Tooltip("Size of one chunk of planted trees, bushes and rocks (m): the camera and the shadows skip the chunks they do not see.")]
        [Min(5f)] public float plantChunk = 80f;

        GameObject relief;
        readonly List<Mesh> reliefMeshes = new List<Mesh>();
        Material reliefMaterial;
        Texture2D reliefTexture;
        /// <summary>Every model that is planted: trees, then bushes, then rocks.</summary>
        GeneratedModel[] kinds = System.Array.Empty<GeneratedModel>();
        int treeCount, bushCount;
        /// <summary>Planted instances of one model in one chunk of the field, with the bounds of them all.</summary>
        struct Batch
        {
            public Mesh Mesh;
            public Matrix4x4[] Matrices;
            public Bounds Bounds;
            public bool Shadows;
        }

        /// <summary>Every batch to draw: one draw call each, skipped by the camera and the shadows when out of sight.</summary>
        Batch[] batches = System.Array.Empty<Batch>();
        WorldGrid ground;
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
            foreach (var mesh in reliefMeshes) Destroy(mesh);
            reliefMeshes.Clear();
            batches = System.Array.Empty<Batch>();
        }

        void OnDestroy()
        {
            ClearRelief();
            if (reliefMaterial != null) Destroy(reliefMaterial);
            if (reliefTexture != null) Destroy(reliefTexture);
            if (modelMaterial != null) Destroy(modelMaterial);
        }

        /// <summary>Metres a point lies outside the map (0 on it).</summary>
        static float Outside(in Shieldbreak.Sim.Grid.GridLayout layout, float2 p)
        {
            float2 min = layout.Origin, max = layout.Origin + new float2(layout.Width, layout.Height);
            return math.distance(p, math.clamp(p, min, max));
        }

        /// <summary>
        /// Height of the ground the camera sees: the sim's heights on the map, beyond it the border height plus hills that
        /// rise towards the horizon. Presentation only; nobody stands beyond the edge.
        /// </summary>
        float GroundHeight(WorldGrid grid, float2 p)
        {
            float h = grid.HeightAt(p);
            float outside = Outside(grid.Layout, p);
            if (outside <= backdropFlat) return h;
            float t = Mathf.SmoothStep(0f, 1f, (outside - backdropFlat) / Mathf.Max(1f, backdrop - backdropFlat));
            float hills = Mathf.PerlinNoise(p.x / backdropWavelength + 31.7f, p.y / backdropWavelength + 57.3f);
            return h + backdropHeight * t * (0.3f + 1.4f * hills);
        }

        /// <summary>Forest beyond the map: patches of noise, never on the flat strip along the edge.</summary>
        bool BackdropForest(WorldGrid grid, float2 p) =>
            Outside(grid.Layout, p) > backdropFlat * 0.5f
            && Mathf.PerlinNoise(p.x / 38f + 5.1f, p.y / 38f + 9.7f) > 0.72f - 0.45f * backdropForest;

        GroundKind KindOf(WorldGrid grid, float2 p) =>
            Outside(grid.Layout, p) <= 0f ? grid.KindAt(p) : BackdropForest(grid, p) ? GroundKind.Forest : GroundKind.Open;

        /// <summary>
        /// Flat-shaded triangles over the map and the land beyond it, every triangle with its own vertices and one colour.
        /// Cut into tiles, each its own renderer: the camera and the shadows only draw the tiles they see, and tiles
        /// beyond the map cast no shadow.
        /// </summary>
        void BuildRelief(WorldGrid grid)
        {
            var layout = grid.Layout;
            var material = ReliefMaterial();
            float edge = Mathf.Max(margin, backdrop);
            int cellsX = Mathf.CeilToInt((layout.Width + 2f * edge) / reliefCell);
            int cellsY = Mathf.CeilToInt((layout.Height + 2f * edge) / reliefCell);
            float2 origin = layout.Origin - edge;
            var corners = new Vector3[(cellsX + 1) * (cellsY + 1)];
            for (int y = 0; y <= cellsY; y++)
            for (int x = 0; x <= cellsX; x++)
            {
                float2 p = origin + new float2(x, y) * reliefCell;
                corners[y * (cellsX + 1) + x] = new Vector3(p.x, GroundHeight(grid, p), p.y);
            }
            Vector3 Corner(int x, int y) => corners[y * (cellsX + 1) + x];

            relief = new GameObject("Relief");
            int tile = Mathf.Max(1, Mathf.RoundToInt(reliefTile / reliefCell));
            for (int ty = 0; ty < cellsY; ty += tile)
            for (int tx = 0; tx < cellsX; tx += tile)
            {
                int w = Mathf.Min(tile, cellsX - tx), h = Mathf.Min(tile, cellsY - ty);
                var vertices = new Vector3[w * h * 6];
                var uvs = new Vector2[vertices.Length];
                var triangles = new int[vertices.Length];
                int n = 0;
                for (int y = ty; y < ty + h; y++)
                for (int x = tx; x < tx + w; x++)
                {
                    Vector3 a = Corner(x, y), b = Corner(x + 1, y), c = Corner(x + 1, y + 1), d = Corner(x, y + 1);
                    // Clockwise from above: the side facing the sky
                    var kind = KindOf(grid, origin + (new float2(x, y) + 0.5f) * reliefCell);
                    n = Triangle(vertices, uvs, triangles, n, a, d, b, kind, x * 73 + y * 179);
                    n = Triangle(vertices, uvs, triangles, n, b, d, c, kind, x * 151 + y * 37 + 11);
                }
                var mesh = new Mesh { name = $"Relief_{tx}_{ty}", vertices = vertices, uv = uvs, triangles = triangles };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                reliefMeshes.Add(mesh);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(relief.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // A tile wholly beyond the map edge only receives shadows
                float2 min = origin + new float2(tx, ty) * reliefCell, max = origin + new float2(tx + w, ty + h) * reliefCell;
                bool onMap = math.all(max > layout.Origin) && math.all(min < layout.Origin + new float2(layout.Width, layout.Height));
                renderer.shadowCastingMode = onMap ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
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
        /// Trees and bushes in the forest, a bush now and then on open ground, a rock now and then in forest and swamp; beyond
        /// the map a forest on the hills of the horizon. The same on every start of the map: the dice come from the cell,
        /// never from the sim.
        /// </summary>
        void Plant(WorldGrid grid)
        {
            ground = grid;
            var trees = Read(treeModels);
            var bushes = Read(bushModels);
            var rocks = Read(rockModels);
            treeCount = trees.Length;
            bushCount = bushes.Length;
            kinds = new GeneratedModel[trees.Length + bushes.Length + rocks.Length];
            trees.CopyTo(kinds, 0);
            bushes.CopyTo(kinds, treeCount);
            rocks.CopyTo(kinds, treeCount + bushCount);
            var near = new List<Matrix4x4>[kinds.Length];
            var far = new List<Matrix4x4>[kinds.Length];
            for (int k = 0; k < kinds.Length; k++)
            {
                near[k] = new List<Matrix4x4>();
                far[k] = new List<Matrix4x4>();
            }

            var layout = grid.Layout;
            for (int index = 0; index < layout.CellCount; index++)
            {
                var kind = (GroundKind)grid.Ground[index];
                var dice = new Unity.Mathematics.Random((uint)index * 2654435761u + 1u);
                float2 at = layout.CellCenter(layout.IndexToCell(index)) + dice.NextFloat2(-0.5f, 0.5f);
                float roll = dice.NextFloat();
                if (kind == GroundKind.Forest && roll < treesPerCell) AddTree(near, ref dice, at, grid.HeightAt(at));
                else if (kind == GroundKind.Forest && roll < treesPerCell + bushesPerCell) AddBush(near, ref dice, at, grid.HeightAt(at));
                else if (kind == GroundKind.Open && roll < openBushesPerCell) AddBush(near, ref dice, at, grid.HeightAt(at));
                else if (kind != GroundKind.Open && roll > 1f - rocksPerCell) AddRock(near, ref dice, at, grid.HeightAt(at));
            }

            // Beyond the map: a lattice of spots, each a tree where the forest is, sometimes a bush or rock elsewhere
            float edge = Mathf.Max(margin, backdrop);
            int spots = Mathf.CeilToInt((Mathf.Max(layout.Width, layout.Height) + 2f * edge) / backdropTreeSpacing);
            float2 origin = layout.Origin - edge;
            for (int y = 0; y < spots; y++)
            for (int x = 0; x < spots; x++)
            {
                var dice = new Unity.Mathematics.Random((uint)(y * spots + x) * 2246822519u + 7u);
                float2 at = origin + (new float2(x, y) + dice.NextFloat2(0.1f, 0.9f)) * backdropTreeSpacing;
                if (Outside(layout, at) <= 1f) continue;
                float h = GroundHeight(grid, at);
                if (BackdropForest(grid, at)) AddTree(far, ref dice, at, h);
                else
                {
                    float roll = dice.NextFloat();
                    if (roll < 0.04f) AddBush(far, ref dice, at, h);
                    else if (roll > 0.985f) AddRock(far, ref dice, at, h);
                }
            }

            var all = new List<Batch>();
            for (int k = 0; k < kinds.Length; k++)
            {
                Chunk(all, kinds[k].Mesh, near[k], true);
                Chunk(all, kinds[k].Mesh, far[k], false);
            }
            batches = all.ToArray();
            if (modelMaterial == null && kinds.Length > 0)
            {
                var any = treeModels.Length > 0 ? treeModels[0] : bushModels.Length > 0 ? bushModels[0] : rockModels[0];
                var palette = any.GetComponentInChildren<MeshRenderer>(true);
                var scene = palette != null && palette.sharedMaterial != null ? palette.sharedMaterial : transform.GetChild(0).GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                modelMaterial = new Material(scene) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
            }
        }

        void AddTree(List<Matrix4x4>[] lists, ref Unity.Mathematics.Random dice, float2 at, float height)
        {
            if (treeCount == 0) return;
            int pick = dice.NextInt(treeCount);
            float tall = Mathf.Lerp(treeHeight.x, treeHeight.y, dice.NextFloat());
            float scale = tall / Mathf.Max(0.1f, kinds[pick].Bounds.size.y);
            var turn = Quaternion.Euler(0f, dice.NextFloat(360f), 0f);
            lists[pick].Add(Matrix4x4.TRS(new Vector3(at.x, height, at.y), turn, Vector3.one * scale) * kinds[pick].Matrix);
        }

        void AddBush(List<Matrix4x4>[] lists, ref Unity.Mathematics.Random dice, float2 at, float height)
        {
            if (bushCount == 0) return;
            int pick = treeCount + dice.NextInt(bushCount);
            float scale = Mathf.Lerp(bushScale.x, bushScale.y, dice.NextFloat());
            var turn = Quaternion.Euler(0f, dice.NextFloat(360f), 0f);
            lists[pick].Add(Matrix4x4.TRS(new Vector3(at.x, height - 0.05f, at.y), turn, Vector3.one * scale) * kinds[pick].Matrix);
        }

        void AddRock(List<Matrix4x4>[] lists, ref Unity.Mathematics.Random dice, float2 at, float height)
        {
            int rocks = kinds.Length - treeCount - bushCount;
            if (rocks == 0) return;
            int pick = treeCount + bushCount + dice.NextInt(rocks);
            float scale = Mathf.Lerp(rockScale.x, rockScale.y, dice.NextFloat());
            var turn = Quaternion.Euler(0f, dice.NextFloat(360f), 0f);
            lists[pick].Add(Matrix4x4.TRS(new Vector3(at.x, height - 0.1f, at.y), turn, Vector3.one * scale) * kinds[pick].Matrix);
        }

        /// <summary>Splits the instances of one model into square chunks of the field, each with the bounds of its instances.</summary>
        void Chunk(List<Batch> into, Mesh mesh, List<Matrix4x4> instances, bool shadows)
        {
            var groups = new Dictionary<Vector2Int, List<Matrix4x4>>();
            foreach (var m in instances)
            {
                var at = m.GetPosition();
                var key = new Vector2Int(Mathf.FloorToInt(at.x / plantChunk), Mathf.FloorToInt(at.z / plantChunk));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Matrix4x4>();
                list.Add(m);
            }
            const int most = 1023;
            foreach (var list in groups.Values)
            for (int start = 0; start < list.Count; start += most)
            {
                int count = Mathf.Min(most, list.Count - start);
                var matrices = list.GetRange(start, count).ToArray();
                var bounds = WorldBounds(mesh.bounds, matrices[0]);
                for (int i = 1; i < count; i++) bounds.Encapsulate(WorldBounds(mesh.bounds, matrices[i]));
                into.Add(new Batch { Mesh = mesh, Matrices = matrices, Bounds = bounds, Shadows = shadows });
            }
        }

        static Bounds WorldBounds(Bounds local, Matrix4x4 m)
        {
            var bounds = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(m.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return bounds;
        }

        void LateUpdate()
        {
            if (batches.Length == 0 || modelMaterial == null) return;
            foreach (var b in batches)
            {
                var rp = new RenderParams(modelMaterial)
                {
                    shadowCastingMode = b.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    worldBounds = b.Bounds,
                };
                Graphics.RenderMeshInstanced(rp, b.Mesh, 0, b.Matrices);
            }
        }

        Vector2Int Tile(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
    }
}
