using System.Collections.Generic;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Shieldbreak.Presentation.Feedback
{
    /// <summary>
    /// Impact particles (game feel spec: flat, large, short): blood on hits, sparks on blocks, wood chips when a shield
    /// breaks, a bright burst on a perfect block. Pooled low-poly shards on a copy of the palette material, coloured per
    /// renderer. Presentation only; reads the runner's frame events.
    /// </summary>
    public class ImpactEffects : MonoBehaviour, Admin.IAdminStatus
    {
        public SimRunner runner;
        public SoldierVisualDefinition visual;
        [Min(8)] public int poolSize = 160;
        public float gravity = 9f;

        [Header("Colours")]
        public Color blood = new Color(0.42f, 0.03f, 0.03f);
        public Color spark = new Color(1f, 0.82f, 0.45f);
        public Color wood = new Color(0.45f, 0.3f, 0.16f);

        struct Shard
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Vector3 velocity, spin;
            public float life, age, size;
        }

        readonly List<Shard> shards = new List<Shard>();
        MaterialPropertyBlock block;
        Material material;
        Mesh mesh;
        int next, spawnedTotal;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public string AdminStatusLabel => "Treffer-Effekte";
        public string AdminStatus => $"{spawnedTotal} Splitter erzeugt, Pool {poolSize}";

        void Awake()
        {
            block = new MaterialPropertyBlock();
            mesh = BuildShardMesh();
            material = visual != null && visual.material != null ? new Material(visual.material) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "M_ImpactShards";
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.EnableKeyword("_EMISSION");
            material.enableInstancing = true;
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Shard");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                shards.Add(new Shard { transform = go.transform, renderer = r });
            }
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }

        /// <summary>Tetrahedron shard: visible from every side with the culled palette material.</summary>
        static Mesh BuildShardMesh()
        {
            var m = new Mesh { name = "ImpactShard" };
            var v = new[] { new Vector3(0f, 0.5f, 0f), new Vector3(-0.5f, -0.3f, 0.3f), new Vector3(0.5f, -0.3f, 0.3f), new Vector3(0f, -0.3f, -0.55f) };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Face(int a, int b, int c) { tris.Add(verts.Count); verts.Add(v[a]); tris.Add(verts.Count); verts.Add(v[b]); tris.Add(verts.Count); verts.Add(v[c]); }
            Face(0, 2, 1); Face(0, 3, 2); Face(0, 1, 3); Face(1, 2, 3);
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            var world = runner != null ? runner.World : null;
            if (world != null && world.HasCombat)
            {
                var agents = world.Agents;
                var combat = world.Combat;
                var setup = world.CombatSetup;
                foreach (var e in runner.FrameEvents)
                {
                    if (e.Target < 0 || e.Target >= agents.Length || e.Attacker < 0 || e.Attacker >= agents.Length) continue;
                    var point = new Vector3(e.Point.x, e.Point.y, e.Point.z);
                    float2 away = math.normalizesafe(agents[e.Target].Position - agents[e.Attacker].Position, agents[e.Attacker].Facing);
                    var dir = new Vector3(away.x, 0f, away.y);
                    float power = Mathf.Clamp01(e.Energy / 700f);
                    bool shield = setup.Weapons[combat[e.Target].WeaponId].Archetype == WeaponArchetype.OneHandShield;
                    switch (e.Type)
                    {
                        case CombatEventType.Hit:
                            if (e.Damage > 0f) Burst(point, dir, blood, false, 6 + (int)(10 * power), 2f + 3f * power, 0.1f, 0.18f, 0.45f);
                            else Burst(point, -dir, spark, true, 5, 3f, 0.03f, 0.05f, 0.2f);
                            break;
                        case CombatEventType.Blocked:
                            Burst(point, -dir, spark, true, 6 + (int)(6 * power), 3f + 2f * power, 0.03f, 0.06f, 0.22f);
                            if (shield) Burst(point, -dir, wood, false, 3 + (int)(4 * power), 2f, 0.05f, 0.09f, 0.35f);
                            break;
                        case CombatEventType.PerfectBlock:
                            Burst(point, -dir, spark, true, 16, 5f, 0.04f, 0.08f, 0.25f);
                            break;
                        case CombatEventType.GuardBreak:
                            Burst(point, dir, wood, false, 12, 3.5f, 0.06f, 0.12f, 0.5f);
                            Burst(point, -dir, spark, true, 8, 4f, 0.03f, 0.06f, 0.22f);
                            break;
                        case CombatEventType.Death:
                            Burst(point, dir, blood, false, 14, 3f, 0.12f, 0.22f, 0.6f);
                            break;
                    }
                }
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < shards.Count; i++)
            {
                var s = shards[i];
                if (s.life <= 0f) continue;
                s.age += dt;
                if (s.age >= s.life)
                {
                    s.life = 0f;
                    s.transform.gameObject.SetActive(false);
                    shards[i] = s;
                    continue;
                }
                s.velocity.y -= gravity * dt;
                var p = s.transform.position + s.velocity * dt;
                if (p.y < 0.02f)
                {
                    // Land flat and stop
                    p.y = 0.02f;
                    s.velocity = Vector3.zero;
                    s.spin = Vector3.zero;
                }
                float t = s.age / s.life;
                s.transform.SetPositionAndRotation(p, s.transform.rotation * Quaternion.Euler(s.spin * dt));
                s.transform.localScale = Vector3.one * (s.size * (1f - t * t));
                shards[i] = s;
            }
        }

        void Burst(Vector3 point, Vector3 dir, Color color, bool glow, int count, float speed, float minSize, float maxSize, float life)
        {
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, glow ? color * 2.2f : Color.black);
            for (int k = 0; k < count; k++)
            {
                var s = shards[next];
                next = (next + 1) % shards.Count;
                // Cone around the push direction, biased upwards
                var v = (dir + Random.insideUnitSphere * 0.8f + Vector3.up * Random.Range(0.3f, 1f)).normalized * speed * Random.Range(0.5f, 1.2f);
                s.velocity = v;
                s.spin = Random.insideUnitSphere * 720f;
                s.size = Random.Range(minSize, maxSize);
                s.life = life * Random.Range(0.7f, 1.3f);
                s.age = 0f;
                s.transform.SetPositionAndRotation(point + Random.insideUnitSphere * 0.05f, Random.rotation);
                s.transform.localScale = Vector3.one * s.size;
                s.renderer.SetPropertyBlock(block);
                s.transform.gameObject.SetActive(true);
                shards[(next - 1 + shards.Count) % shards.Count] = s;
                spawnedTotal++;
            }
        }
    }
}
