using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Cameras
{
    /// <summary>
    /// Free top-down camera for testing (CLAUDE.md 3.9): pan with keys or middle-mouse drag, rotate, zoom.
    /// Pitch flattens slightly when zoomed in. Runs on unscaled time so it works while paused.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DebugTopDownCamera : MonoBehaviour, IAdminStatus
    {
        public Vector3 focus = new Vector3(48f, 0f, 48f);
        public float yaw;
        [Min(1f)] public float distance = 45f;
        public Vector2 distanceRange = new Vector2(4f, 160f);
        [Tooltip("Pitch at the closest and farthest zoom.")]
        public Vector2 pitchRange = new Vector2(35f, 70f);
        [Tooltip("Pan speed per metre of camera distance (1/s).")]
        public float panSpeed = 0.9f;
        public float fastMultiplier = 3f;
        public float rotateSpeed = 90f;
        public float dragSensitivity = 0.0018f;
        [Tooltip("Map bounds the focus point stays inside (min x/z, max x/z).")]
        public Rect bounds = new Rect(0f, 0f, 96f, 96f);

        InputActionMap map;
        InputAction move, rotate, zoom, panHold, look, fast;

        public string AdminStatusLabel => "Kamera";
        public string AdminStatus => $"Fokus ({focus.x:0}, {focus.z:0}) · Abstand {distance:0} m · Richtung {Mathf.Repeat(yaw, 360f):0}°";

        void OnEnable()
        {
            map = GameInput.Map("DebugCamera");
            move = map.FindAction("Move", true);
            rotate = map.FindAction("Rotate", true);
            zoom = map.FindAction("Zoom", true);
            panHold = map.FindAction("PanHold", true);
            look = map.FindAction("Look", true);
            fast = map.FindAction("Fast", true);
            map.Enable();
            Apply();
        }

        void OnDisable() => map?.Disable();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var flatRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = flatRotation * Vector3.forward;
            Vector3 right = flatRotation * Vector3.right;

            Vector2 m = move.ReadValue<Vector2>();
            float speed = distance * panSpeed * (fast.IsPressed() ? fastMultiplier : 1f);
            focus += (right * m.x + forward * m.y) * (speed * dt);

            if (panHold.IsPressed())
            {
                Vector2 d = look.ReadValue<Vector2>();
                focus -= (right * d.x + forward * d.y) * (distance * dragSensitivity);
            }

            yaw += rotate.ReadValue<float>() * rotateSpeed * dt;

            float scroll = zoom.ReadValue<float>();
            if (scroll != 0f) distance = Mathf.Clamp(distance * (1f - scroll * 0.001f), distanceRange.x, distanceRange.y);

            focus.x = Mathf.Clamp(focus.x, bounds.xMin, bounds.xMax);
            focus.z = Mathf.Clamp(focus.z, bounds.yMin, bounds.yMax);
            Apply();
        }

        void Apply()
        {
            float t = Mathf.InverseLerp(distanceRange.x, distanceRange.y, distance);
            float pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, t);
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
