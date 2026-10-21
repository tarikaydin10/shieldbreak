using Shieldbreak.Presentation.Admin;
using UnityEngine;

namespace Shieldbreak.Presentation.Cameras
{
    /// <summary>
    /// Command view camera (CLAUDE.md 3.9): while Active, the camera rises in about half a second from the combat
    /// camera to a high oblique view over the hero (looking the way the combat camera looked, a little ahead), and
    /// returns when Active ends. Runs after the ThirdPersonCamera and blends from wherever that one put the camera,
    /// so kicks and lag stay intact on the way back. Fog moves back with the height, so the field stays readable.
    /// Pure presentation: the sim knows nothing about it.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public class CommandViewCamera : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        public ThirdPersonCamera combatCamera;
        [Tooltip("Seconds to rise into the command view, and to come back (CLAUDE.md 3.9: about 0.5 s).")]
        [Min(0.05f)] public float blendSeconds = 0.5f;
        [Tooltip("Camera height above the ground (m); the mouse wheel changes it within the limits.")]
        public float height = 35f;
        public Vector2 heightLimits = new Vector2(18f, 60f);
        [Tooltip("Height change per mouse-wheel step (m).")]
        [Min(0.1f)] public float zoomStep = 3f;
        [Tooltip("Downward angle of the view (degrees).")]
        [Range(30f, 89f)] public float pitch = 58f;
        [Tooltip("The view centres this far ahead of the hero (m), in the direction the combat camera looked.")]
        public float lookAhead = 12f;
        [Tooltip("Fog start and end move back by this many metres per metre of height while the view is up.")]
        [Min(0f)] public float fogPushPerMetre = 1f;

        /// <summary>Set by the CommandView while the key is held.</summary>
        public bool Active { get; set; }
        /// <summary>0 = combat camera, 1 = command view; in between while rising or coming back.</summary>
        public float Blend { get; private set; }

        float fogStart, fogEnd;

        public string AdminStatusLabel => "Kommandokamera";
        public string AdminStatus => Blend <= 0f ? "unten" : $"{height:0} m hoch, Blick {pitch:0}° · Mausrad: Höhe";

        void Awake()
        {
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
        }

        void OnDisable()
        {
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        /// <summary>Mouse wheel: up = higher.</summary>
        public void Zoom(float steps) => height = Mathf.Clamp(height - steps * zoomStep, heightLimits.x, heightLimits.y);

        void LateUpdate()
        {
            Blend = Mathf.MoveTowards(Blend, Active ? 1f : 0f, Time.unscaledDeltaTime / blendSeconds);
            float t = Blend * Blend * (3f - 2f * Blend);
            RenderSettings.fogStartDistance = fogStart + t * height * fogPushPerMetre;
            RenderSettings.fogEndDistance = fogEnd + t * height * fogPushPerMetre;
            var world = runner != null ? runner.World : null;
            if (t <= 0f || world == null || runner.PlayerAgent < 0) return;

            float yaw = combatCamera != null ? combatCamera.Yaw : transform.eulerAngles.y;
            var flat = Quaternion.Euler(0f, yaw, 0f);
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = SimRunner.ToWorld(runner.InterpolatedPosition(runner.PlayerAgent)) + flat * Vector3.forward * lookAhead;
            Vector3 position = focus - rotation * Vector3.forward * (height / Mathf.Sin(pitch * Mathf.Deg2Rad));
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, t), Quaternion.Slerp(transform.rotation, rotation, t));
        }
    }
}
