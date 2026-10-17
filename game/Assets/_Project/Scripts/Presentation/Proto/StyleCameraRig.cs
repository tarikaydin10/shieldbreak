using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Proto
{
    /// <summary>
    /// Viewer for Scenes/Proto_Style: bookmarks at the distances the style has to work at (close combat,
    /// 15 m, 40 m readability, command view). Keys 1-9 switch, right mouse orbits, wheel zooms, O auto-orbits.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StyleCameraRig : MonoBehaviour
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

        public Bookmark[] bookmarks = Array.Empty<Bookmark>();
        public float blendTime = 0.5f;
        public float orbitSensitivity = 0.2f;
        public float autoOrbitDegPerSec = 6f;
        public bool showHelp = true;

        int current;
        Vector3 target;
        float yaw, pitch, distance;
        float yawOffset, pitchOffset, zoom = 1f;
        bool autoOrbit;

        public int Count => bookmarks.Length;

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
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null)
            {
                for (int i = 0; i < bookmarks.Length && i < 9; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Apply(i, false);
                if (keyboard.oKey.wasPressedThisFrame) autoOrbit = !autoOrbit;
                if (keyboard.hKey.wasPressedThisFrame) showHelp = !showHelp;
            }
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yawOffset += delta.x * orbitSensitivity;
                    pitchOffset = Mathf.Clamp(pitchOffset - delta.y * orbitSensitivity, -60f, 60f);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) zoom = Mathf.Clamp(zoom * (1f - scroll * 0.001f), 0.2f, 3f);
            }
            if (autoOrbit) yawOffset += autoOrbitDegPerSec * Time.deltaTime;

            var b = bookmarks[current];
            float k = 1f - Mathf.Exp(-Time.deltaTime * 4f / Mathf.Max(0.01f, blendTime));
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

        void OnGUI()
        {
            if (!showHelp || bookmarks.Length == 0) return;
            string text = $"[{current + 1}] {bookmarks[current].label}\n" +
                          $"1-{Mathf.Min(9, bookmarks.Length)}: Ansicht   RMB: drehen   Rad: Zoom   O: Orbit   H: Hilfe";
            GUI.Label(new Rect(12, 10, 700, 44), text);
        }
    }
}
