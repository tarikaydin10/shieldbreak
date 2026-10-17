using System.Collections.Generic;
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
    /// CombatAttack with a direction from the recent mouse movement (CLAUDE.md 3.5: up = overhead, sideways = side
    /// cut, down = thrust; no movement = auto), CombatBlock press/release, CombatFeint. Nothing here changes the sim
    /// directly.
    /// </summary>
    public class PlayerCombatInput : MonoBehaviour, IAdminStatus
    {
        const ushort LocalPlayer = 0;

        public SimRunner runner;
        public ThirdPersonCamera combatCamera;
        [Tooltip("Degrees per mouse count.")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full stick.")]
        public float stickSensitivity = 160f;
        [Tooltip("Mouse movement in this window before the click decides the attack direction (s).")]
        [Min(0.02f)] public float directionWindow = 0.12f;
        [Tooltip("Below this accumulated movement (mouse counts) the attack uses the auto direction.")]
        public float directionThreshold = 8f;
        [Tooltip("Direction used when the mouse did not move (Auto-Richtung).")]
        public int autoDirection = AttackDirection.Overhead;
        [Tooltip("Re-send the walk/look state at least this often (s), even if nothing changed.")]
        public float keepAlive = 0.25f;

        InputActionMap map;
        InputAction move, look, attack, block, feint;
        readonly Queue<(float time, Vector2 delta)> recent = new Queue<(float, Vector2)>();
        float2 lastMove, lastLook;
        float lastSent;
        int lastDirection = -1;
        bool lastAuto;

        public string AdminStatusLabel => "Kampf";
        public string AdminStatus => lastDirection < 0 ? "noch kein Angriff" :
            $"letzter Angriff: {DirectionName(lastDirection)}{(lastAuto ? " (Auto)" : "")}";

        public static string DirectionName(int d) =>
            d == AttackDirection.Overhead ? "oben" : d == AttackDirection.Left ? "links" : d == AttackDirection.Right ? "rechts" : "Stich";

        void OnEnable()
        {
            map = GameInput.Map("Combat");
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            attack = map.FindAction("Attack", true);
            block = map.FindAction("Block", true);
            feint = map.FindAction("Feint", true);
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
            if (math.distance(walk2, lastMove) > 0.05f || math.dot(look2, lastLook) < 0.9994f || now - lastSent > keepAlive)
            {
                world.Submit(LocalPlayer, InputCommandType.PlayerMove, new int4(agent, 0, 0, 0), new float4(walk2, look2));
                lastMove = walk2;
                lastLook = look2;
                lastSent = now;
            }

            if (attack.WasPressedThisFrame())
            {
                int direction = ReadDirection(out bool auto);
                world.Submit(LocalPlayer, InputCommandType.CombatAttack, new int4(agent, direction, 0, 0));
                lastDirection = direction;
                lastAuto = auto;
            }
            if (block.WasPressedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatBlock, new int4(agent, 1, 0, 0));
            if (block.WasReleasedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatBlock, new int4(agent, 0, 0, 0));
            if (feint.WasPressedThisFrame()) world.Submit(LocalPlayer, InputCommandType.CombatFeint, new int4(agent, 0, 0, 0));
        }

        int ReadDirection(out bool auto)
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
