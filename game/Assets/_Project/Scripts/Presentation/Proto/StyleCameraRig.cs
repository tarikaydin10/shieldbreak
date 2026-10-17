using System;
using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Proto
{
    /// <summary>
    /// Viewer for Scenes/Proto_Style: bookmarks at the distances the style has to work at (close combat,
    /// 15 m, 40 m readability, command view). Controls come from the "ProtoCamera" action map (see F1 menu).
    /// Runs on unscaled time so the view stays usable while the game is paused.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StyleCameraRig : MonoBehaviour, IAdminStatus
    {
        [Serializable]
        public struct Bookmark
        {
            public string label;
            public Vector3 target;
            public float yaw;
            public float pitch;
            public float distance;
        }

        const string MapName = "ProtoCamera";

        public Bookmark[] bookmarks = Array.Empty<Bookmark>();
        public float blendTime = 0.5f;
        public float orbitSensitivity = 0.2f;
        public float autoOrbitDegPerSec = 6f;

        int current;
        Vector3 target;
        float yaw, pitch, distance;
        float yawOffset, pitchOffset, zoom = 1f;
        bool autoOrbit;

        InputActionMap map;
        InputAction[] views;
        InputAction orbitHold, look, zoomAxis, autoOrbitToggle;

        public int Count => bookmarks.Length;

        public string AdminStatusLabel => "Kamera";
        public string AdminStatus => bookmarks.Length == 0
            ? "keine Ansichten"
            : $"[{current + 1}] {bookmarks[current].label}{(autoOrbit ? " · Orbit" : "")}";

        void OnEnable()
        {
            map = GameInput.Map(MapName);
            views = new InputAction[5];
            for (int i = 0; i < views.Length; i++) views[i] = map.FindAction("View" + (i + 1), throwIfNotFound: true);
            orbitHold = map.FindAction("OrbitHold", throwIfNotFound: true);
            look = map.FindAction("Look", throwIfNotFound: true);
            zoomAxis = map.FindAction("Zoom", throwIfNotFound: true);
            autoOrbitToggle = map.FindAction("AutoOrbit", throwIfNotFound: true);
            map.Enable();
        }

        void OnDisable()
        {
            map?.Disable();
        }

        void Start()
        {
            if (bookmarks.Length > 0) Apply(0, true);
        }

        /// <summary>Switches bookmark; instant skips the blend (used by the screenshot capture).</summary>
        public void Apply(int index, bool instant)
        {
            current = Mathf.Clamp(index, 0, bookmarks.Length - 1);
            yawOffset = pitchOffset = 0f;
            zoom = 1f;
            if (!instant) return;
            var b = bookmarks[current];
            target = b.target;
            yaw = b.yaw;
            pitch = b.pitch;
            distance = b.distance;
            UpdateTransform();
        }

        void Update()
        {
            if (bookmarks.Length == 0) return;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < views.Length && i < bookmarks.Length; i++)
                if (views[i].WasPressedThisFrame()) Apply(i, false);
            if (autoOrbitToggle.WasPressedThisFrame()) autoOrbit = !autoOrbit;

            if (orbitHold.IsPressed())
            {
                Vector2 delta = look.ReadValue<Vector2>();
                yawOffset += delta.x * orbitSensitivity;
                pitchOffset = Mathf.Clamp(pitchOffset - delta.y * orbitSensitivity, -60f, 60f);
            }
            float scroll = zoomAxis.ReadValue<float>();
            if (scroll != 0f) zoom = Mathf.Clamp(zoom * (1f - scroll * 0.001f), 0.2f, 3f);
            if (autoOrbit) yawOffset += autoOrbitDegPerSec * dt;

            var b = bookmarks[current];
            float k = 1f - Mathf.Exp(-dt * 4f / Mathf.Max(0.01f, blendTime));
            target = Vector3.Lerp(target, b.target, k);
            yaw = Mathf.LerpAngle(yaw, b.yaw + yawOffset, k);
            pitch = Mathf.Lerp(pitch, Mathf.Clamp(b.pitch + pitchOffset, -10f, 89f), k);
            distance = Mathf.Lerp(distance, b.distance * zoom, k);
            UpdateTransform();
        }

        void UpdateTransform()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }
    }
}
