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
    /// the parent of the tiles. A map with relief gets one mesh built from the sim's heights instead of the tiles.
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

        GameObject relief;
        Material reliefMaterial;
        Texture2D reliefTexture;

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
        }

        void OnDestroy()
        {
            ClearRelief();
            if (reliefMaterial != null) Destroy(reliefMaterial);
            if (reliefTexture != null) Destroy(reliefTexture);
        }

        /// <summary>Flat-shaded triangles over the map and its margin, every triangle with its own vertices and one colour.</summary>
        void BuildRelief(WorldGrid grid)
        {
            var layout = grid.Layout;
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
                n = Triangle(vertices, uvs, triangles, n, a, d, b, x * 73 + y * 179);
                n = Triangle(vertices, uvs, triangles, n, b, d, c, x * 151 + y * 37 + 11);
            }

            var mesh = new Mesh { name = "Relief", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            relief = new GameObject("Relief");
            relief.AddComponent<MeshFilter>().sharedMesh = mesh;
            relief.AddComponent<MeshRenderer>().sharedMaterial = ReliefMaterial();
        }

        int Triangle(Vector3[] vertices, Vector2[] uvs, int[] triangles, int n, Vector3 a, Vector3 b, Vector3 c, int seed)
        {
            int colour = (seed * 2654435 >> 8 & int.MaxValue) % reliefColors.Length;
            var uv = new Vector2((colour + 0.5f) / reliefColors.Length, 0.5f);
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
            reliefTexture = new Texture2D(reliefColors.Length, 1) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            reliefTexture.SetPixels(reliefColors);
            reliefTexture.Apply();
            var scene = transform.GetChild(0).GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            reliefMaterial = new Material(scene) { hideFlags = HideFlags.HideAndDontSave, mainTexture = reliefTexture };
            return reliefMaterial;
        }

        Vector2Int Tile(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
    }
}
