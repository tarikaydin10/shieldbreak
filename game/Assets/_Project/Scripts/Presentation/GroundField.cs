using System.Collections.Generic;
using UnityEngine;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Keeps the ground under the whole map: one scene serves scenarios of different sizes, so the tiles placed in the
    /// scene are extended up to the map's size plus a margin. The new tiles come from the model, not from a scene tile:
    /// those are static-batched, a copy would draw the combined mesh. Presentation only: the sim has no ground. Lives on
    /// the parent of the tiles.
    /// </summary>
    public class GroundField : MonoBehaviour
    {
        public SimRunner runner;
        [Tooltip("The ground model; its pivot is the middle of the tile.")]
        public GameObject tileModel;
        [Min(1f)] public float tileSize = 20f;
        [Tooltip("Ground beyond the map edge (m), so the world does not end where the soldiers do.")]
        [Min(0f)] public float margin = 40f;

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

        Vector2Int Tile(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / tileSize), Mathf.FloorToInt(position.z / tileSize));
    }
}
