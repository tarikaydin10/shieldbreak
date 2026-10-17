using Shieldbreak.Presentation.Admin;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Presentation.Cameras
{
    /// <summary>
    /// Combat camera (CLAUDE.md 3.9, game feel spec): shoulder offset, slight lag, yaw/pitch from the Combat look
    /// input (applied by PlayerCombatInput), kick on impacts. Without a player agent it orbits the fight (spectator).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        [Tooltip("Offset from the hero's feet in camera space: x = shoulder, y = height, z = distance (negative).")]
        public Vector3 shoulderOffset = new Vector3(0.55f, 1.75f, -3.1f);
        [Tooltip("Seconds the camera needs to catch up with the hero (lag).")]
        [Min(0.001f)] public float followLag = 0.08f;
        public Vector2 pitchLimits = new Vector2(-30f, 55f);
        [Tooltip("Kick strength in degrees per joule of effective energy, and the cap (spec: 2–4°).")]
        public float kickPerJoule = 0.006f;
        public float kickMaxDegrees = 4f;
        [Min(0.01f)] public float kickSeconds = 0.06f;
        [Tooltip("Spectator orbit speed (deg/s) and distance.")]
        public float spectatorOrbit = 12f;
        public float spectatorDistance = 7f;

        public float Yaw { get; set; }
        public float Pitch { get; set; } = 12f;

        Vector3 focus;
        Vector2 kick;
        float kickTime;
        Camera cam;
        float baseFov;
        float fovPulse;

        public string AdminStatusLabel => "Kamera";
        public string AdminStatus => runner != null && runner.PlayerAgent >= 0 ? $"Third Person · Blick {Mathf.Repeat(Yaw, 360f):0}°" : "Zuschauer";

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseFov = cam.fieldOfView;
        }

        /// <summary>Tilts the view in the direction of a blow (screen-space degrees) and pulses the FOV.</summary>
        public void Kick(Vector2 direction, float energy, bool ownHit)
        {
            float amount = Mathf.Min(kickMaxDegrees, energy * kickPerJoule);
            kick = direction.sqrMagnitude > 1e-6f ? direction.normalized * amount : new Vector2(0f, -amount);
            kickTime = kickSeconds;
            if (ownHit) fovPulse = 1.5f;
        }

        void LateUpdate()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || world.AgentCount == 0) return;
            float dt = Time.unscaledDeltaTime;

            Vector3 target;
            if (runner.PlayerAgent >= 0)
            {
                target = SimRunner.ToWorld(runner.InterpolatedPosition(runner.PlayerAgent));
            }
            else
            {
                // Spectator: centre of all fighters, slow orbit
                Vector3 sum = Vector3.zero;
                for (int i = 0; i < world.AgentCount; i++) sum += SimRunner.ToWorld(runner.InterpolatedPosition(i));
                target = sum / world.AgentCount;
                if (!GamePause.IsPaused) Yaw += spectatorOrbit * dt;
            }
            focus = Vector3.Lerp(focus == Vector3.zero ? target : focus, target, 1f - Mathf.Exp(-dt / followLag));

            Pitch = Mathf.Clamp(Pitch, pitchLimits.x, pitchLimits.y);
            kickTime = Mathf.Max(0f, kickTime - dt);
            float k = kickTime / Mathf.Max(kickSeconds, 1e-4f);
            var rotation = Quaternion.Euler(Pitch + kick.y * k, Yaw + kick.x * k, 0f);
            Vector3 offset = runner.PlayerAgent >= 0 ? shoulderOffset : new Vector3(0f, 2.2f, -spectatorDistance);
            transform.SetPositionAndRotation(focus + rotation * offset, rotation);

            fovPulse = Mathf.MoveTowards(fovPulse, 0f, dt * 15f);
            cam.fieldOfView = baseFov + fovPulse;
        }
    }
}
