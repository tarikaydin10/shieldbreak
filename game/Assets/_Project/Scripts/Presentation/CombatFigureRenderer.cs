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
    /// Draws fighters as low-poly figures (CLAUDE.md: GameObjects only for the ~20 nearest agents): armless body,
    /// helmet, shield on an IK left arm, sword on an IK right arm following the sim's WeaponPath (interpolated between
    /// ticks), two-hand weapons with both hands on the grip and the shield slung on the back, drawing on a weapon switch,
    /// a jump arc for leaping attacks, lean in windup/release, flinch on hits, stagger when stunned, falling when dead.
    /// Reads sim state only.
    /// </summary>
    public class CombatFigureRenderer : MonoBehaviour
    {
        public SimRunner runner;
        public SoldierVisualDefinition visual;
        [Min(1)] public int maxFigures = 32;

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
            public Transform root, body, helmet, shield, upperR, foreR, upperL, foreL, weapon, weapon2;
            public float shieldT, flinch, deadTime = -1f;
            /// <summary>Local axis the body falls around (away from the killer) and how sideways that fall is.</summary>
            public Vector3 fallAxis = Vector3.back;
            public float fallSideways = 1f;
        }

        readonly List<Figure> figures = new List<Figure>();

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
            foreach (var f in figures) if (f.root != null) Destroy(f.root.gameObject);
            figures.Clear();
        }

        Transform Part(Transform parent, string partName, SoldierVisualDefinition.Part data)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = data.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = visual.material;
            // Static parts carry their attach offset; limb and weapon matrices are set every frame
            var m = data.LocalMatrix;
            go.transform.localPosition = m.GetPosition();
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            return go.transform;
        }

        Figure Build(int team)
        {
            var look = visual.teams[team];
            var f = new Figure { root = new GameObject($"Fighter_{figures.Count}").transform };
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
            return f;
        }

        void LateUpdate()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || visual == null) return;
            var agents = world.Agents;
            var combat = world.Combat;
            int count = Mathf.Min(agents.Length, maxFigures);
            while (figures.Count < count) figures.Add(Build(Mathf.Clamp(agents[figures.Count].Team, 0, 1)));

            foreach (var e in runner.FrameEvents)
            {
                if (e.Type == CombatEventType.Hit && e.Damage > 0f && e.Target < figures.Count) figures[e.Target].flinch = 1f;
                if (e.Type == CombatEventType.Death && e.Target < figures.Count && e.Attacker >= 0 && e.Attacker < agents.Length)
                {
                    // Fall away from the killer: direction in the dead soldier's own frame
                    var target = agents[e.Target];
                    float2 away = math.normalizesafe(target.Position - agents[e.Attacker].Position, -target.Facing);
                    var rot = Quaternion.LookRotation(new Vector3(target.Facing.x, 0f, target.Facing.y), Vector3.up);
                    Vector3 local = Quaternion.Inverse(rot) * new Vector3(away.x, 0f, away.y);
                    figures[e.Target].fallAxis = Vector3.Cross(Vector3.up, local).normalized;
                    figures[e.Target].fallSideways = Mathf.Abs(local.x);
                }
            }

            float dt = Time.unscaledDeltaTime;
            var setup = world.CombatSetup;
            bool hasCombat = world.HasCombat;
            for (int i = 0; i < count; i++)
            {
                var a = agents[i];
                var c = combat[i];
                var f = figures[i];
                Vector3 pos = SimRunner.ToWorld(runner.InterpolatedPosition(i));
                var facing = Quaternion.LookRotation(new Vector3(a.Facing.x, 0f, a.Facing.y), Vector3.up);

                // Which weapon is in the hand: while switching, the old one goes down, then the new one comes up
                bool switching = hasCombat && c.State == CombatState.Switching;
                float switchT = switching ? Mathf.Clamp01((c.StateTick - 1 + runner.Alpha) / setup.Tuning.WeaponSwitchTicks) : 1f;
                int shownWeapon = switching && switchT < 0.5f ? c.PreviousWeaponId : c.WeaponId;
                bool twoHand = hasCombat && setup.Weapons[shownWeapon].Archetype == WeaponArchetype.TwoHand;
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
                f.root.SetPositionAndRotation(pos + Vector3.up * lift, facing);

                if (switching)
                {
                    // Down to the hip and back up to the guard
                    float down = switchT < 0.5f ? switchT * 2f : 2f - switchT * 2f;
                    hand = math.lerp(hand, new float3(0.3f, 0.95f, 0.05f), down);
                    blade = math.normalize(math.lerp(blade, new float3(0.1f, -0.9f, 0.35f), down));
                }

                if (c.State == CombatState.Stunned) lean = stunLean;
                if (hasCombat && c.State != CombatState.Dead) lean += lowHealthSlouch * Mathf.Clamp01(1f - c.Health / setup.Tuning.HealthMax);
                f.flinch = Mathf.MoveTowards(f.flinch, 0f, dt / flinchSeconds);
                lean -= flinchDegrees * f.flinch;

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
                    if (f.deadTime < 0f) f.deadTime = 0f;
                    f.deadTime += dt;
                }
                else f.deadTime = -1f;
                float fall = f.deadTime < 0f ? 0f : Mathf.SmoothStep(0f, 1f, f.deadTime / fallSeconds);

                // Guard blend: shield raised (one-hand) or weapon across the body (two-hand)
                float guardTarget = (c.BlockHeld == 1 && c.State == CombatState.Idle) || bashing ? 1f : 0f;
                f.shieldT = Mathf.MoveTowards(f.shieldT, guardTarget, dt / shieldBlend);
                if (twoHand && !attacking)
                {
                    hand = math.lerp(hand, (float3)visual.twoHandBlockHand, f.shieldT);
                    blade = math.normalize(math.lerp(blade, math.normalize((float3)visual.twoHandBlockBlade), f.shieldT));
                }

                // Lean pivots at the hip so the feet stay planted
                var hip = new Vector3(0f, 0.95f, 0f);
                var bodyRot = Quaternion.AngleAxis(85f * fall, f.fallAxis) * Quaternion.AngleAxis(lean, Vector3.right);
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
                f.weapon.gameObject.SetActive(!twoHand);
                f.weapon2.gameObject.SetActive(twoHand);
                PlaceWeapon(twoHand ? f.weapon2 : f.weapon, twoHand ? visual.twoHandWeapon : visual.oneHandWeapon, handWorld, bladeWorld, right);
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
                    Vector3 grip = Vector3.Lerp(shieldRest, shieldGuard, f.shieldT);
                    grip += new Vector3(bashing ? 0.05f : 0f, 0f, bashPush);
                    Vector3 gripWorld = bodyToWorld.MultiplyPoint3x4(grip);
                    var shieldRot = f.root.rotation * bodyRot * Quaternion.Euler(0f, Mathf.Lerp(shieldData.yaw, 0f, f.shieldT), 0f) * shieldData.meshMatrix.rotation;
                    if (fall > 0f)
                    {
                        // The falling body rolls around its forward axis, which would leave the shield on its edge: tip it flat
                        shieldRot = Quaternion.AngleAxis(85f * fall * f.fallSideways, f.root.right) * shieldRot;
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
