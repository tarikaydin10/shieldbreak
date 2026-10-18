using System.Collections.Generic;
using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Turns the Combat action map into sim commands for the local hero: PlayerMove (camera-relative walk + look),
    /// CombatAttack, CombatBlock press/release, CombatFeint, CombatStep, CombatSpecial, CombatSwitchWeapon. Two control schemes choose the attack direction:
    /// Simple (default): tap = light attack chaining right/left (thrust while walking forward), hold = heavy
    /// overhead, done by morphing the young windup. Directional: direction from the recent mouse movement.
    /// The sim is the same for both; nothing here changes it directly.
    /// </summary>
    public class PlayerCombatInput : MonoBehaviour, IAdminStatus
    {
        public enum Scheme { Simple, Directional }

        const ushort LocalPlayer = 0;
        const string SchemePref = "combat.scheme";

        public SimRunner runner;
        public ThirdPersonCamera combatCamera;
        public Scheme scheme = Scheme.Simple;
        [Tooltip("Degrees per mouse count.")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full stick.")]
        public float stickSensitivity = 160f;

        [Header("Simple scheme")]
        [Tooltip("Holding the attack button this long turns the light attack into a heavy overhead (s). Must stay inside the morph window.")]
        [Min(0.02f)] public float heavyHoldSeconds = 0.1f;
        [Tooltip("A light attack after this pause starts the chain again with a right cut (s).")]
        [Min(0.1f)] public float chainResetSeconds = 1.0f;
        [Tooltip("Forward input above this makes a light attack a thrust.")]
        [Range(0f, 1f)] public float thrustForwardInput = 0.6f;

        [Header("Directional scheme")]
        [Tooltip("Mouse movement in this window before the click decides the attack direction (s).")]
        [Min(0.02f)] public float directionWindow = 0.12f;
        [Tooltip("Below this accumulated movement (mouse counts) the attack uses the auto direction.")]
        public float directionThreshold = 8f;
        [Tooltip("Direction used when the mouse did not move (Auto-Richtung).")]
        public int autoDirection = AttackDirection.Overhead;

        [Tooltip("Re-send the walk/look state at least this often (s), even if nothing changed.")]
        public float keepAlive = 0.25f;

        InputActionMap map;
        InputAction move, look, attack, block, feint, toggleScheme, sprint, step, special, nextWeapon;
        readonly InputAction[] weaponSlots = new InputAction[4];
        string weaponName = "";
        int lastSprint;
        readonly Queue<(float time, Vector2 delta)> recent = new Queue<(float, Vector2)>();
        float2 lastMove, lastLook;
        float lastSent;
        int lastDirection = -1;
        string lastKind = "";
        bool nextChainLeft;
        float lastLightTime = -10f;
        float pressTime;
        bool heavyPending;

        public string AdminStatusLabel => "Kampf";
        public string AdminStatus =>
            $"Steuerung {(scheme == Scheme.Simple ? "Einfach" : "Richtungen")} (Tab wechselt)" +
            (weaponName.Length > 0 ? $" · Waffe: {weaponName} (1–4)" : "") +
            (lastDirection < 0 ? "" : $" · letzter Angriff: {lastKind} {DirectionName(lastDirection)}");

        public static string DirectionName(int d) =>
            d == AttackDirection.Overhead ? "oben" : d == AttackDirection.Left ? "links" : d == AttackDirection.Right ? "rechts" : "Stich";

        void Awake()
        {
            scheme = (Scheme)PlayerPrefs.GetInt(SchemePref, (int)scheme);
        }

        void OnEnable()
        {
            map = GameInput.Map("Combat");
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            attack = map.FindAction("Attack", true);
            block = map.FindAction("Block", true);
            feint = map.FindAction("Feint", true);
            toggleScheme = map.FindAction("ToggleScheme", true);
            sprint = map.FindAction("Sprint", true);
            step = map.FindAction("Step", true);
            special = map.FindAction("Special", true);
            nextWeapon = map.FindAction("NextWeapon", true);
            for (int k = 0; k < weaponSlots.Length; k++) weaponSlots[k] = map.FindAction($"WeaponSlot{k + 1}", true);
            map.Enable();
        }

        void OnDisable()
        {
            map?.Disable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            var world = runner != null ? runner.World : null;
            bool active = world != null && runner.PlayerAgent >= 0 && !AdminMenu.IsOpen && !GamePause.IsPaused;
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
            if (toggleScheme.WasPressedThisFrame())
            {
                scheme = scheme == Scheme.Simple ? Scheme.Directional : Scheme.Simple;
                PlayerPrefs.SetInt(SchemePref, (int)scheme);
            }
            if (!active) return;

            int agent = runner.PlayerAgent;
            float now = Time.unscaledTime;

            // Look: mouse delta (counts) or stick (rate)
            Vector2 delta = look.ReadValue<Vector2>();
            bool stick = look.activeControl != null && look.activeControl.device is Gamepad;
            Vector2 degrees = stick ? delta * (stickSensitivity * Time.unscaledDeltaTime) : delta * mouseSensitivity;
            if (combatCamera != null)
            {
                combatCamera.Yaw += degrees.x;
                combatCamera.Pitch -= degrees.y;
            }
            recent.Enqueue((now, stick ? delta * 40f : delta));
            while (recent.Count > 0 && now - recent.Peek().time > directionWindow) recent.Dequeue();

            // Walk relative to the camera, look along the camera
            float yaw = combatCamera != null ? combatCamera.Yaw : 0f;
            var yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector2 m = move.ReadValue<Vector2>();
            Vector3 walk = yawRot * new Vector3(m.x, 0f, m.y);
            Vector3 forward = yawRot * Vector3.forward;
            float2 walk2 = new float2(walk.x, walk.z);
            float2 look2 = new float2(forward.x, forward.z);
            int sprinting = sprint.IsPressed() && m.sqrMagnitude > 0.04f ? 1 : 0;
            if (math.distance(walk2, lastMove) > 0.05f || math.dot(look2, lastLook) < 0.9994f || sprinting != lastSprint || now - lastSent > keepAlive)
            {
                world.Submit(LocalPlayer, InputCommandType.PlayerMove, new int4(agent, sprinting, 0, 0), new float4(walk2, look2));
                lastSprint = sprinting;
                lastMove = walk2;
                lastLook = look2;
                lastSent = now;
            }

            if (scheme == Scheme.Simple) SimpleAttack(world, agent, m, now);
            else if (attack.WasPressedThisFrame())
            {
                int direction = ReadMouseDirection(out bool auto);
                SubmitAttack(world, agent, direction, auto ? "Auto" : "");
            }

            if (block.WasPressedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatBlock, new int4(agent, 1, 0, 0));
            if (block.WasReleasedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatBlock, new int4(agent, 0, 0, 0));
            if (feint.WasPressedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatFeint, new int4(agent, 0, 0, 0));
            if (special.WasPressedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatSpecial, new int4(agent, 0, 0, 0));
            var own = world.Combat[agent];
            for (int k = 0; k < weaponSlots.Length; k++)
                if (weaponSlots[k].WasPressedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatSwitchWeapon, new int4(agent, k, 0, 0));
            if (nextWeapon.WasPressedThisFrame())
            {
                // Next filled slot after the drawn one
                for (int n = 1; n < 4; n++)
                {
                    int slot = (own.ActiveSlot + n) % 4;
                    if (own.Loadout[slot] >= 0 && own.Loadout[slot] != own.WeaponId)
                    {
                        world.Submit(LocalPlayer, InputCommandType.CombatSwitchWeapon, new int4(agent, slot, 0, 0));
                        break;
                    }
                }
            }
            weaponName = runner.scenario is DuelScenarioDefinition duel && own.WeaponId < duel.weapons.Length && duel.weapons[own.WeaponId] != null
                ? duel.weapons[own.WeaponId].displayName : "";
            if (step.WasPressedThisFrame())
            {
                // Step along the walk input relative to the view (the hero faces the camera direction); none = back
                int dir = m.sqrMagnitude < 0.04f ? StepDirection.Back :
                    Mathf.Abs(m.y) >= Mathf.Abs(m.x) ? (m.y > 0f ? StepDirection.Forward : StepDirection.Back) :
                    (m.x < 0f ? StepDirection.Left : StepDirection.Right);
                world.Submit(LocalPlayer, InputCommandType.CombatStep, new int4(agent, dir, 0, 0));
            }
        }

        /// <summary>
        /// Light attacks start immediately (no waiting for tap/hold), so the windup shows within one tick. Holding the
        /// button morphs the young windup into a heavy overhead; the sim allows that in the first 40 % of the windup.
        /// </summary>
        void SimpleAttack(SimWorld world, int agent, Vector2 moveInput, float now)
        {
            if (attack.WasPressedThisFrame())
            {
                int direction;
                if (moveInput.y > thrustForwardInput && Mathf.Abs(moveInput.x) < 0.5f) direction = AttackDirection.Thrust;
                else
                {
                    if (now - lastLightTime > chainResetSeconds) nextChainLeft = false;
                    direction = nextChainLeft ? AttackDirection.Left : AttackDirection.Right;
                    nextChainLeft = !nextChainLeft;
                }
                lastLightTime = now;
                pressTime = now;
                heavyPending = true;
                SubmitAttack(world, agent, direction, "leicht");
            }
            if (heavyPending && attack.IsPressed() && now - pressTime >= heavyHoldSeconds)
            {
                heavyPending = false;
                SubmitAttack(world, agent, AttackDirection.Overhead, "schwer");
            }
            if (attack.WasReleasedThisFrame()) heavyPending = false;
        }

        void SubmitAttack(SimWorld world, int agent, int direction, string kind)
        {
            world.Submit(LocalPlayer, InputCommandType.CombatAttack, new int4(agent, direction, 0, 0));
            lastDirection = direction;
            lastKind = kind;
        }

        int ReadMouseDirection(out bool auto)
        {
            Vector2 sum = Vector2.zero;
            foreach (var r in recent) sum += r.delta;
            auto = sum.magnitude < directionThreshold;
            if (auto) return autoDirection;
            if (Mathf.Abs(sum.x) > Mathf.Abs(sum.y)) return sum.x < 0f ? AttackDirection.Left : AttackDirection.Right;
            return sum.y > 0f ? AttackDirection.Overhead : AttackDirection.Thrust;
        }
    }
}
