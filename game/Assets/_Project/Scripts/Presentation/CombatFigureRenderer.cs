using System.Collections.Generic;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Presentation
{
    /// <summary>
    /// Draws fighters as low-poly figures (CLAUDE.md 3.2: GameObjects only for the nearest agents, M4 task 2): armless body,
    /// helmet, shield on an IK left arm, sword on an IK right arm following the sim's WeaponPath (interpolated between
    /// ticks), two-hand weapons with both hands on the grip and the shield slung on the back, drawing on a weapon switch,
    /// a jump arc for leaping attacks, lean in windup/release, flinch on hits, stagger when stunned, falling when dead.
    /// Riders sit on the horse from the pipeline (body and four legs that swing with its speed) and couch the lance.
    /// Every fighter is posed the same way; the nearest (and the hero) keep figures of their own, everybody else is posed
    /// on a hidden template figure and drawn with instancing, so a battle costs a few draw calls per part, not per soldier.
    /// What a fighter looks like over time (flinch, fall, shield, gait) belongs to the agent, not to the figure.
    /// Reads sim state only.
    /// </summary>
    public class CombatFigureRenderer : MonoBehaviour
    {
        public SimRunner runner;
        public SoldierVisualDefinition visual;
        [Tooltip("Fighters nearest the camera that get figures of their own (GameObjects); the hero always does.")]
        [Min(1)] public int fullFigures = 24;
        [Tooltip("Camera the distance is measured from; empty = the main camera.")]
        public Camera viewer;
        [Tooltip("Length and thickness of an arrow in flight (m); larger than life, so it reads from afar.")]
        public Vector2 arrowSize = new Vector2(1f, 0.05f);
        [Tooltip("Colour of an arrow (wood_dark of the palette): a cube's UVs would span the whole palette texture.")]
        public Color arrowColor = new Color(0.29f, 0.184f, 0.133f);

        [Header("Horse")]
        [Tooltip("The rider sits this much higher than a man standing (m).")]
        [Min(0f)] public float saddleHeight = 0.6f;
        [Tooltip("Ground a horse covers per stride (m): the legs swing faster the faster it runs.")]
        [Min(0.1f)] public float stride = 2.4f;
        [Tooltip("Swing of the legs at a gallop (degrees).")]
        public float legSwing = 32f;

        [Header("Body (soldier frame: x right, y up, z forward)")]
        public Vector3 rightShoulder = new Vector3(0.22f, 1.42f, 0f);
        public Vector3 leftShoulder = new Vector3(-0.22f, 1.42f, 0f);
        [Min(0.1f)] public float upperArmLength = 0.32f;
        [Min(0.1f)] public float forearmLength = 0.32f;
        [Min(0.02f)] public float armThickness = 0.1f;
        public Vector3 rightElbowHint = new Vector3(1f, -0.6f, -0.4f);
        public Vector3 leftElbowHint = new Vector3(-1f, -0.6f, -0.2f);

        [Header("Shield")]
        [Tooltip("Grip position at rest and when blocking.")]
        public Vector3 shieldRest = new Vector3(-0.26f, 1.0f, 0.2f);
        public Vector3 shieldGuard = new Vector3(-0.08f, 1.28f, 0.42f);
        [Tooltip("Seconds to raise or lower the shield.")]
        [Min(0.01f)] public float shieldBlend = 0.08f;

        [Header("Motion feel")]
        [Tooltip("Upper body lean in degrees at windup end (back) and at contact (forward).")]
        public Vector2 attackLean = new Vector2(-8f, 12f);
        public float stunLean = -14f;
        public float flinchDegrees = 10f;
        [Min(0.01f)] public float flinchSeconds = 0.25f;
        [Min(0.05f)] public float fallSeconds = 0.6f;
        [Tooltip("How far the hip drops when a fighter falls (m).")]
        public float deadDrop = 0.75f;
        [Tooltip("Health shows in the posture (no HUD): forward slouch in degrees at almost no health.")]
        public float lowHealthSlouch = 12f;

        class Figure
        {
            public Transform root, body, helmet, shield, upperR, foreR, upperL, foreL, weapon, weapon2, weapon3;
            /// <summary>Riders only: the horse and its four leg pivots (front left, front right, hind left, hind right), and the lance.</summary>
            public Transform horse, lance;
            public Transform[] legs;
            /// <summary>Every drawn part and its mesh (a template figure hands these to the instanced batches).</summary>
            public readonly List<(Transform part, Mesh mesh)> parts = new List<(Transform, Mesh)>();
            public int kind;
        }

        /// <summary>How an agent looks over time: carried by the agent, whichever figure draws it this frame.</summary>
        class Look
        {
            public float gait, shieldT, flinch, deadTime = -1f;
            /// <summary>Local axis the body falls around (away from the killer) and how sideways that fall is.</summary>
            public Vector3 fallAxis = Vector3.back;
            public float fallSideways = 1f;
        }

        Look[] looks = System.Array.Empty<Look>();
        readonly Dictionary<int, Figure> near = new Dictionary<int, Figure>();
        readonly Dictionary<int, Stack<Figure>> spare = new Dictionary<int, Stack<Figure>>();
        readonly Figure[] templates = new Figure[4];
        readonly List<Figure> all = new List<Figure>();
        readonly Dictionary<Mesh, List<Matrix4x4>> batches = new Dictionary<Mesh, List<Matrix4x4>>();
        float[] sortKeys = System.Array.Empty<float>();
        int[] sortIds = System.Array.Empty<int>();
        readonly HashSet<int> wanted = new HashSet<int>();
        readonly List<int> leaving = new List<int>();
        Material instanced;
        int figureCount;

        public int FullFigureCount => near.Count;

        void OnEnable()
        {
            if (runner != null) runner.WorldRebuilt += Clear;
        }

        void OnDisable()
        {
            if (runner != null) runner.WorldRebuilt -= Clear;
        }

        void Clear()
        {
            foreach (var f in all)
            {
                if (f.root != null) Destroy(f.root.gameObject);
                if (f.horse != null) Destroy(f.horse.gameObject);
            }
            all.Clear();
            near.Clear();
            spare.Clear();
            for (int k = 0; k < templates.Length; k++) templates[k] = null;
            looks = System.Array.Empty<Look>();
        }

        Transform Part(Transform parent, string partName, SoldierVisualDefinition.Part data)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = data.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = visual.material;
            building?.parts.Add((go.transform, data.mesh));
            // Static parts carry their attach offset; limb and weapon matrices are set every frame
            var m = data.LocalMatrix;
            go.transform.localPosition = m.GetPosition();
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            return go.transform;
        }

        Figure building;

        Figure Build(int team, bool mounted)
        {
            var look = visual.teams[team];
            var f = new Figure { root = new GameObject($"Fighter_{figureCount++}").transform, kind = team * 2 + (mounted ? 1 : 0) };
            building = f;
            f.root.SetParent(transform, false);
            f.body = new GameObject("Body").transform;
            f.body.SetParent(f.root, false);
            Part(f.body, "Torso", look.armlessBody);
            f.helmet = Part(f.body, "Helmet", look.helmet);
            f.shield = Part(f.root, "Shield", look.shield);
            f.upperR = Part(f.root, "UpperArmR", look.upperArm);
            f.foreR = Part(f.root, "ForearmR", visual.forearms[team]);
            f.upperL = Part(f.root, "UpperArmL", look.upperArm);
            f.foreL = Part(f.root, "ForearmL", visual.forearms[team]);
            f.weapon = Part(f.root, "Weapon", visual.oneHandWeapon);
            f.weapon2 = Part(f.root, "TwoHandWeapon", visual.twoHandWeapon);
            f.weapon3 = Part(f.root, "Polearm", visual.polearmWeapon);
            if (mounted) BuildHorse(f, team);
            building = null;
            all.Add(f);
            return f;
        }

        GeneratedModel[] horseBodies;
        GeneratedModel horseLeg, lanceModel;
        bool modelsRead;

        void ReadModels()
        {
            if (modelsRead) return;
            modelsRead = true;
            horseBodies = new GeneratedModel[visual.horseBodies != null ? visual.horseBodies.Length : 0];
            for (int t = 0; t < horseBodies.Length; t++) horseBodies[t] = GeneratedModel.From(visual.horseBodies[t]);
            horseLeg = GeneratedModel.From(visual.horseLeg);
            lanceModel = GeneratedModel.From(visual.lance);
            if (horseBodies.Length == 0 || !horseBodies[0].IsValid || !horseLeg.IsValid) Debug.LogWarning("[figures] The horse models are missing in the soldier visual: riders are drawn without a horse.");
        }

        void BuildHorse(Figure f, int team)
        {
            ReadModels();
            if (horseBodies.Length > 0 && horseLeg.IsValid)
            {
                f.horse = new GameObject("Horse").transform;
                f.horse.SetParent(f.root.parent, false);
                var horseBody = horseBodies[Mathf.Clamp(team, 0, horseBodies.Length - 1)];
                f.parts.Add((horseBody.Attach(f.horse, "Body", visual.material, Matrix4x4.Translate(Vector3.up * visual.seatHeight)), horseBody.Mesh));
                f.legs = new Transform[4];
                for (int l = 0; l < 4; l++)
                {
                    var hip = new GameObject($"Leg{l}").transform;
                    hip.SetParent(f.horse, false);
                    hip.localPosition = new Vector3(l % 2 == 0 ? visual.frontHip.x : -visual.frontHip.x, visual.frontHip.y, l < 2 ? visual.frontHip.z : visual.hindZ);
                    f.parts.Add((horseLeg.Attach(hip, "Leg", visual.material, Matrix4x4.Translate(Vector3.down * visual.frontHip.y)), horseLeg.Mesh));
                    f.legs[l] = hip;
                }
            }
            if (lanceModel.IsValid)
            {
                f.lance = lanceModel.Attach(f.root, "Lance", visual.material, Matrix4x4.identity);
                f.parts.Add((f.lance, lanceModel.Mesh));
            }
        }

        /// <summary>Horse under the rider: legs swing with the speed (diagonal pairs, like a trot), it falls over with him.</summary>
        void PlaceHorse(Figure f, Look look, Vector3 pos, Quaternion facing, float speed, float fall, float dt)
        {
            f.horse.SetPositionAndRotation(pos + Vector3.down * (0.35f * fall), facing * Quaternion.AngleAxis(80f * fall, Vector3.forward));
            look.gait = Mathf.Repeat(look.gait + speed * dt / stride, 1f);
            float swing = legSwing * Mathf.Clamp01(speed / 3f) * (1f - fall) * Mathf.Sin(look.gait * 2f * Mathf.PI);
            for (int l = 0; l < 4; l++)
                f.legs[l].localRotation = Quaternion.Euler(l == 0 || l == 3 ? swing : -swing, 0f, 0f);
        }

        /// <summary>The lance model lies along +Y with its grip at the origin: grip in the hand, point along the blade.</summary>
        void PlaceLance(Transform lance, Vector3 hand, Vector3 blade)
        {
            var m = Matrix4x4.TRS(hand, Quaternion.FromToRotation(Vector3.up, blade), Vector3.one) * lanceModel.Matrix;
            lance.SetPositionAndRotation(m.GetPosition(), m.rotation);
            lance.localScale = m.lossyScale;
        }

        readonly List<Transform> arrows = new List<Transform>();
        Material arrowMaterial;
        Texture2D arrowTexture;

        /// <summary>Arrows are plain stretched cubes until M4 (CLAUDE.md: no assets before), one per flying arrow, along their flight.</summary>
        void DrawArrows(Sim.Core.SimWorld world)
        {
            var flying = world.Projectiles;
            int shown = 0;
            for (int p = 0; p < flying.Length; p++)
            {
                var arrow = flying[p];
                if (arrow.Active == 0) continue;
                if (shown == arrows.Count)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform, false);
                    go.transform.localScale = new Vector3(arrowSize.y, arrowSize.y, arrowSize.x);
                    go.GetComponent<MeshRenderer>().sharedMaterial = ArrowMaterial();
                    arrows.Add(go.transform);
                }
                var t = arrows[shown++];
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                // One tick behind the sim's position by the interpolation alpha
                Vector3 velocity = new Vector3(arrow.Velocity.x, arrow.Velocity.y, arrow.Velocity.z);
                Vector3 at = new Vector3(arrow.Position.x, arrow.Position.y, arrow.Position.z) - velocity * ((1f - runner.Alpha) * Sim.Core.SimConstants.DT);
                t.SetPositionAndRotation(at, Quaternion.LookRotation(velocity));
            }
            for (int a = shown; a < arrows.Count; a++)
                if (arrows[a].gameObject.activeSelf) arrows[a].gameObject.SetActive(false);
        }

        Material ArrowMaterial()
        {
            if (arrowMaterial != null) return arrowMaterial;
            arrowTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            arrowTexture.SetPixel(0, 0, arrowColor);
            arrowTexture.Apply();
            arrowMaterial = new Material(visual.material) { hideFlags = HideFlags.HideAndDontSave, mainTexture = arrowTexture };
            return arrowMaterial;
        }

        void OnDestroy()
        {
            if (arrowMaterial != null) Destroy(arrowMaterial);
            if (arrowTexture != null) Destroy(arrowTexture);
            if (instanced != null) Destroy(instanced);
        }

        void LateUpdate()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || visual == null) return;
            var agents = world.Agents;
            var combat = world.Combat;
            int count = agents.Length;
            if (looks.Length != count)
            {
                var grown = new Look[count];
                for (int i = 0; i < count; i++) grown[i] = i < looks.Length ? looks[i] : new Look();
                looks = grown;
            }

            foreach (var e in runner.FrameEvents)
            {
                if (e.Type == CombatEventType.Hit && e.Damage > 0f && e.Target >= 0 && e.Target < count) looks[e.Target].flinch = 1f;
                if (e.Type == CombatEventType.Death && e.Target >= 0 && e.Target < count && e.Attacker >= 0 && e.Attacker < count)
                {
                    // Fall away from the killer: direction in the dead soldier's own frame
                    var target = agents[e.Target];
                    float2 away = math.normalizesafe(target.Position - agents[e.Attacker].Position, -target.Facing);
                    var rot = Quaternion.LookRotation(new Vector3(target.Facing.x, 0f, target.Facing.y), Vector3.up);
                    Vector3 local = Quaternion.Inverse(rot) * new Vector3(away.x, 0f, away.y);
                    looks[e.Target].fallAxis = Vector3.Cross(Vector3.up, local).normalized;
                    looks[e.Target].fallSideways = Mathf.Abs(local.x);
                }
            }

            DrawArrows(world);
            ChooseNear(world);
            foreach (var list in batches.Values) list.Clear();

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < count; i++)
            {
                var a = agents[i];
                // Fled soldiers (Sim/Morale) left the field
                if (a.HasFled) continue;
                int kind = Mathf.Clamp(a.Team, 0, 1) * 2 + (a.IsMounted ? 1 : 0);
                bool own = near.TryGetValue(i, out var f);
                if (!own) f = Template(kind);
                Pose(world, i, f, looks[i], dt);
                if (!own) Collect(f);
            }
            DrawBatches();
        }

        /// <summary>The nearest fighters to the camera, and the hero, get figures of their own; the others give theirs back.</summary>
        void ChooseNear(Sim.Core.SimWorld world)
        {
            var agents = world.Agents;
            var cam = viewer != null ? viewer : Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            if (sortKeys.Length < agents.Length)
            {
                sortKeys = new float[agents.Length];
                sortIds = new int[agents.Length];
            }
            int n = 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i].HasFled) continue;
                sortKeys[n] = DistanceSq(i, eye);
                sortIds[n++] = i;
            }
            System.Array.Sort(sortKeys, sortIds, 0, n);
            wanted.Clear();
            int hero = runner.PlayerAgent;
            if (hero >= 0 && hero < agents.Length && !agents[hero].HasFled) wanted.Add(hero);
            for (int k = 0; k < n && wanted.Count < fullFigures; k++) wanted.Add(sortIds[k]);

            leaving.Clear();
            foreach (var pair in near)
                if (!wanted.Contains(pair.Key) || KindOf(agents[pair.Key]) != pair.Value.kind) leaving.Add(pair.Key);
            foreach (int i in leaving)
            {
                var f = near[i];
                near.Remove(i);
                Show(f, false);
                if (!spare.TryGetValue(f.kind, out var stack)) spare[f.kind] = stack = new Stack<Figure>();
                stack.Push(f);
            }
            foreach (int i in wanted)
            {
                if (near.ContainsKey(i)) continue;
                int kind = KindOf(agents[i]);
                var f = spare.TryGetValue(kind, out var stack) && stack.Count > 0 ? stack.Pop() : Build(kind / 2, (kind & 1) == 1);
                Show(f, true);
                near[i] = f;
            }
        }

        float DistanceSq(int i, Vector3 eye)
        {
            float2 p = runner.InterpolatedPosition(i);
            float dx = p.x - eye.x, dz = p.y - eye.z;
            return dx * dx + dz * dz;
        }

        static int KindOf(in Sim.Agents.AgentData a) => Mathf.Clamp(a.Team, 0, 1) * 2 + (a.IsMounted ? 1 : 0);

        static void Show(Figure f, bool shown)
        {
            f.root.gameObject.SetActive(shown);
            if (f.horse != null) f.horse.gameObject.SetActive(shown);
        }

        /// <summary>A hidden figure per team and mount, posed for each far fighter in turn and read off into the batches.</summary>
        Figure Template(int kind)
        {
            if (templates[kind] != null) return templates[kind];
            var f = Build(kind / 2, (kind & 1) == 1);
            f.root.name = $"Template_{kind}";
            Show(f, false);
            return templates[kind] = f;
        }

        void Collect(Figure f)
        {
            foreach (var (part, mesh) in f.parts)
            {
                if (mesh == null || !part.gameObject.activeSelf) continue;
                if (!batches.TryGetValue(mesh, out var list)) batches[mesh] = list = new List<Matrix4x4>();
                list.Add(part.localToWorldMatrix);
            }
        }

        void DrawBatches()
        {
            if (instanced == null) instanced = new Material(visual.material) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
            var rp = new RenderParams(instanced) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows = true };
            const int chunk = 1023;
            foreach (var pair in batches)
            {
                var list = pair.Value;
                for (int start = 0; start < list.Count; start += chunk)
                    Graphics.RenderMeshInstanced(rp, pair.Key, 0, list, Mathf.Min(chunk, list.Count - start), start);
            }
        }

        /// <summary>Poses one fighter on a figure: its own, or the template that stands in for it.</summary>
        void Pose(Sim.Core.SimWorld world, int i, Figure f, Look look, float dt)
        {
            var a = world.Agents[i];
            var c = world.Combat[i];
            var setup = world.CombatSetup;
            bool hasCombat = world.HasCombat;
            {
                Vector3 pos = SimRunner.ToWorld(runner.InterpolatedPosition(i));
                var facing = Quaternion.LookRotation(new Vector3(a.Facing.x, 0f, a.Facing.y), Vector3.up);

                // Which weapon is in the hand: while switching, the old one goes down, then the new one comes up
                bool switching = hasCombat && c.State == CombatState.Switching;
                float switchT = switching ? Mathf.Clamp01((c.StateTick - 1 + runner.Alpha) / setup.Tuning.WeaponSwitchTicks) : 1f;
                int shownWeapon = switching && switchT < 0.5f ? c.PreviousWeaponId : c.WeaponId;
                bool polearm = hasCombat && setup.Weapons[shownWeapon].Archetype == WeaponArchetype.Polearm;
                bool lance = hasCombat && setup.Weapons[shownWeapon].Archetype == WeaponArchetype.Lance;
                bool twoHand = polearm || (hasCombat && setup.Weapons[shownWeapon].Archetype == WeaponArchetype.TwoHand);
                bool carriesShield = !hasCombat || LoadoutHasShield(c, setup);

                // Weapon pose from the sim's attack table (guard pose when not attacking)
                float3 hand, blade;
                float lean = 0f, lift = 0f;
                bool attacking = c.State == CombatState.Windup || c.State == CombatState.Release || c.State == CombatState.Recovery;
                if (hasCombat)
                {
                    var weaponData = setup.Weapons[shownWeapon];
                    var attack = setup.Attacks[weaponData.AttackIndex(attacking ? c.Direction : 0)];
                    float elapsed = attacking ? math.max(0f, c.StateTick - 1 + runner.Alpha) : 0f;
                    float pose = attack.Timing.PoseTime(elapsed);
                    WeaponPath.Evaluate(setup.Keys, attack.FirstKey, attack.KeyCount, pose, out hand, out blade);
                    if (attacking)
                    {
                        lean = LeanAt(pose, attack.Timing);
                        // Leaping attack: the body follows a jump arc over the lunge
                        if (attack.LungeDistance > 0f)
                            lift = Mathf.Sin(Mathf.PI * Mathf.Clamp01((pose - attack.LungeStartPose) / (attack.LungeEndPose - attack.LungeStartPose))) * visual.leapHeight;
                    }
                }
                else
                {
                    hand = new float3(0.25f, 1.1f, 0.35f);
                    blade = math.normalize(new float3(-0.2f, 0.7f, 0.7f));
                }
                if (hasCombat && c.State == CombatState.Jumping && setup.Tuning.JumpTicks > 0)
                    lift += Mathf.Sin(Mathf.PI * Mathf.Clamp01((c.StateTick - 1 + runner.Alpha) / setup.Tuning.JumpTicks)) * setup.Tuning.JumpHeight;
                f.root.SetPositionAndRotation(pos + Vector3.up * lift, facing);
                bool riding = f.horse != null;

                if (switching)
                {
                    // Down to the hip and back up to the guard
                    float down = switchT < 0.5f ? switchT * 2f : 2f - switchT * 2f;
                    hand = math.lerp(hand, new float3(0.3f, 0.95f, 0.05f), down);
                    blade = math.normalize(math.lerp(blade, new float3(0.1f, -0.9f, 0.35f), down));
                }

                if (c.State == CombatState.Stunned) lean = stunLean;
                if (hasCombat && c.State != CombatState.Dead) lean += lowHealthSlouch * Mathf.Clamp01(1f - c.Health / setup.Tuning.HealthMax);
                look.flinch = Mathf.MoveTowards(look.flinch, 0f, dt / flinchSeconds);
                lean -= flinchDegrees * look.flinch;

                // Shield bash: pull back in the windup, punch forward in the active phase, lean into it
                bool bashing = c.State == CombatState.Special;
                float bashPush = 0f;
                if (bashing && hasCombat)
                {
                    var rules = setup.Tuning;
                    float tick = c.StateTick - 1 + runner.Alpha;
                    float windup = rules.SpecialWindupTicks, active = rules.SpecialActiveTicks;
                    bashPush = tick <= windup ? -0.15f * (tick / windup)
                        : tick <= windup + active ? 0.35f : Mathf.Lerp(0.35f, 0f, (tick - windup - active) / Mathf.Max(1f, rules.SpecialRecoveryTicks));
                    lean += bashPush * 30f;
                }

                // Death: fall to the side over fallSeconds
                if (c.State == CombatState.Dead)
                {
                    if (look.deadTime < 0f) look.deadTime = 0f;
                    look.deadTime += dt;
                }
                else look.deadTime = -1f;
                float fall = look.deadTime < 0f ? 0f : Mathf.SmoothStep(0f, 1f, look.deadTime / fallSeconds);
                if (riding)
                {
                    // In the saddle until he falls; the horse goes down with him
                    PlaceHorse(f, look, pos, facing, math.length(a.Velocity), fall, dt);
                    f.root.position += Vector3.up * (saddleHeight * (1f - fall));
                }

                // Guard blend: shield raised (one-hand) or weapon across the body (two-hand)
                float guardTarget = (c.BlockHeld == 1 && c.State == CombatState.Idle) || bashing ? 1f : 0f;
                look.shieldT = Mathf.MoveTowards(look.shieldT, guardTarget, dt / shieldBlend);
                if (twoHand && !attacking)
                {
                    hand = math.lerp(hand, (float3)visual.twoHandBlockHand, look.shieldT);
                    blade = math.normalize(math.lerp(blade, math.normalize((float3)visual.twoHandBlockBlade), look.shieldT));
                }

                // Lean pivots at the hip so the feet stay planted
                var hip = new Vector3(0f, 0.95f, 0f);
                var bodyRot = Quaternion.AngleAxis(85f * fall, look.fallAxis) * Quaternion.AngleAxis(lean, Vector3.right);
                f.body.localRotation = bodyRot;
                // A fallen body rolls about the hip, so it also drops to lie on the ground
                f.body.localPosition = hip - bodyRot * hip + Vector3.down * (deadDrop * fall);
                Matrix4x4 bodyToRoot = Matrix4x4.TRS(f.body.localPosition, bodyRot, Vector3.one);
                Matrix4x4 rootToWorld = f.root.localToWorldMatrix;
                Matrix4x4 bodyToWorld = rootToWorld * bodyToRoot;

                // Right arm and weapon
                Vector3 handWorld = bodyToWorld.MultiplyPoint3x4((Vector3)hand);
                Vector3 bladeWorld = bodyToWorld.MultiplyVector((Vector3)blade).normalized;
                Vector3 right = f.root.right;
                f.weapon.gameObject.SetActive(!twoHand && !lance);
                if (f.lance != null)
                {
                    f.lance.gameObject.SetActive(lance);
                    if (lance) PlaceLance(f.lance, handWorld, bladeWorld);
                }
                f.weapon2.gameObject.SetActive(twoHand && !polearm);
                f.weapon3.gameObject.SetActive(polearm);
                if (!lance) PlaceWeapon(polearm ? f.weapon3 : twoHand ? f.weapon2 : f.weapon, polearm ? visual.polearmWeapon : twoHand ? visual.twoHandWeapon : visual.oneHandWeapon, handWorld, bladeWorld, right);
                Vector3 shoulderR = bodyToWorld.MultiplyPoint3x4(rightShoulder);
                Vector3 elbowR = SolveElbow(shoulderR, handWorld, upperArmLength, forearmLength, bodyToWorld.MultiplyVector(rightElbowHint));
                PlaceLimb(f.upperR, shoulderR, elbowR);
                PlaceLimb(f.foreR, elbowR, handWorld);

                // Shield: on the arm with a one-hand weapon, slung on the back otherwise, absent without one in the loadout
                var shieldData = visual.teams[Mathf.Clamp(a.Team, 0, 1)].shield;
                f.shield.gameObject.SetActive(carriesShield);
                Vector3 leftHand;
                if (!twoHand)
                {
                    Vector3 grip = Vector3.Lerp(shieldRest, shieldGuard, look.shieldT);
                    grip += new Vector3(bashing ? 0.05f : 0f, 0f, bashPush);
                    Vector3 gripWorld = bodyToWorld.MultiplyPoint3x4(grip);
                    var shieldRot = f.root.rotation * bodyRot * Quaternion.Euler(0f, Mathf.Lerp(shieldData.yaw, 0f, look.shieldT), 0f) * shieldData.meshMatrix.rotation;
                    if (fall > 0f)
                    {
                        // The falling body rolls around its forward axis, which would leave the shield on its edge: tip it flat
                        shieldRot = Quaternion.AngleAxis(85f * fall * look.fallSideways, f.root.right) * shieldRot;
                        gripWorld.y = Mathf.Lerp(gripWorld.y, pos.y + 0.1f, fall);
                    }
                    f.shield.SetPositionAndRotation(gripWorld, shieldRot);
                    leftHand = gripWorld;
                }
                else
                {
                    f.shield.SetPositionAndRotation(bodyToWorld.MultiplyPoint3x4(visual.shieldBackOffset),
                        f.root.rotation * bodyRot * Quaternion.Euler(visual.shieldBackEuler) * shieldData.meshMatrix.rotation);
                    // Off hand lower on the same grip
                    leftHand = handWorld - bladeWorld * visual.twoHandGripSpacing;
                }
                Vector3 shoulderL = bodyToWorld.MultiplyPoint3x4(leftShoulder);
                Vector3 elbowL = SolveElbow(shoulderL, leftHand, upperArmLength, forearmLength, bodyToWorld.MultiplyVector(leftElbowHint));
                PlaceLimb(f.upperL, shoulderL, elbowL);
                PlaceLimb(f.foreL, elbowL, leftHand);
            }
        }

        static bool LoadoutHasShield(in CombatData c, CombatSetup setup)
        {
            for (int s = 0; s < 4; s++)
            {
                int w = c.Loadout[s];
                if (w >= 0 && w < setup.Weapons.Length && setup.Weapons[w].Archetype == WeaponArchetype.OneHandShield) return true;
            }
            return false;
        }

        float LeanAt(float pose, AttackTiming timing)
        {
            // Back towards the end of the windup, forward through the release, recover after
            if (pose <= timing.ReleaseStartPose) return attackLean.x * Mathf.SmoothStep(0f, 1f, pose / timing.ReleaseStartPose);
            if (pose <= timing.RecoveryStartPose)
                return Mathf.Lerp(attackLean.x, attackLean.y, Mathf.SmoothStep(0f, 1f, (pose - timing.ReleaseStartPose) / (timing.RecoveryStartPose - timing.ReleaseStartPose)));
            return Mathf.Lerp(attackLean.y, 0f, Mathf.SmoothStep(0f, 1f, (pose - timing.RecoveryStartPose) / (1f - timing.RecoveryStartPose)));
        }

        void PlaceWeapon(Transform weapon, SoldierVisualDefinition.Part part, Vector3 hand, Vector3 blade, Vector3 right)
        {
            // Blade along +Y; its wide side across the swing plane so the edge leads
            Vector3 wide = Vector3.Cross(blade, right);
            if (wide.sqrMagnitude < 1e-6f) wide = Vector3.Cross(blade, Vector3.forward);
            Vector3 forward = Vector3.Cross(wide.normalized, blade);
            var rotation = Quaternion.LookRotation(forward, blade);
            var m = Matrix4x4.TRS(hand, rotation, Vector3.one) * part.meshMatrix;
            weapon.SetPositionAndRotation(m.GetPosition(), m.rotation);
            weapon.localScale = m.lossyScale;
        }

        void PlaceLimb(Transform limb, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 1e-4f) return;
            // Limb segments are unit prisms along +Y (Blender +Z) from 0 to 1
            limb.SetPositionAndRotation(from, Quaternion.FromToRotation(Vector3.up, d / length));
            limb.localScale = new Vector3(armThickness, length, armThickness);
        }

        static Vector3 SolveElbow(Vector3 shoulder, Vector3 target, float a, float b, Vector3 hint)
        {
            Vector3 toTarget = target - shoulder;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, (a + b) * 0.999f);
            Vector3 dir = toTarget.sqrMagnitude > 1e-8f ? toTarget.normalized : Vector3.forward;
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 pole = Vector3.ProjectOnPlane(hint, dir);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(Vector3.down, dir);
            return shoulder + dir * (a * cosA) + pole.normalized * (a * Mathf.Sqrt(1f - cosA * cosA));
        }
    }
}
