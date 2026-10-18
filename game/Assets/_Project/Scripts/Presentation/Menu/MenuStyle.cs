using UnityEngine;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Shared look of the main menu, pause menu and their pages (IMGUI, admin-grade on purpose: readable, no art
    /// before M4). Everything is drawn in a 1080p-based scale so it reads the same on every screen.
    /// </summary>
    public static class MenuStyle
    {
        public static GUIStyle Panel, Title, Subtitle, Heading, Text, Muted, Key, Button, BigButton, Hint;
        static Texture2D panelTexture, buttonTexture, buttonHover;
        public static readonly Color Accent = new Color(0.88f, 0.4f, 0.29f);
        public static readonly Color Fg = new Color(0.91f, 0.9f, 0.87f);
        public static readonly Color Dim = new Color(0.6f, 0.62f, 0.66f);

        /// <summary>Sets the GUI scale and returns the scaled screen size.</summary>
        public static Vector2 BeginScaled()
        {
            Ensure();
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            return new Vector2(Screen.width / scale, Screen.height / scale);
        }

        public static float Scale => Mathf.Max(1f, Screen.height / 1080f);

        /// <summary>Centred panel of the given width; returns its rect in scaled GUI space.</summary>
        public static Rect CentredPanel(Vector2 screen, float width, float height)
        {
            width = Mathf.Min(width, screen.x - 32f);
            height = Mathf.Min(height, screen.y - 32f);
            return new Rect((screen.x - width) * 0.5f, (screen.y - height) * 0.5f, width, height);
        }

        public static bool Big(string label, bool enabled = true)
        {
            GUI.enabled = enabled;
            bool b = GUILayout.Button(label, BigButton, GUILayout.Height(44));
            GUI.enabled = true;
            return b;
        }

        public static bool Small(string label, float width = 0f, bool enabled = true)
        {
            GUI.enabled = enabled;
            bool b = width > 0f ? GUILayout.Button(label, Button, GUILayout.Width(width)) : GUILayout.Button(label, Button);
            GUI.enabled = true;
            return b;
        }

        /// <summary>Left/right chooser: "‹ value ›" with a label. Returns the new index.</summary>
        public static int Chooser(string label, int index, string[] values)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Text, GUILayout.Width(180));
            if (Small("‹", 36)) index = (index + values.Length - 1) % values.Length;
            GUILayout.Label(values[Mathf.Clamp(index, 0, values.Length - 1)], Key, GUILayout.MinWidth(200));
            if (Small("›", 36)) index = (index + 1) % values.Length;
            GUILayout.EndHorizontal();
            return index;
        }

        public static float Slider(string label, float value, float min, float max, string format = "0.00")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Text, GUILayout.Width(180));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.MinWidth(200));
            GUILayout.Label(value.ToString(format), Key, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            return value;
        }

        public static void Row(string left, string right, float leftWidth = 170f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(left, Key, GUILayout.Width(leftWidth));
            GUILayout.Label(right, Text);
            GUILayout.EndHorizontal();
        }

        static void Ensure()
        {
            if (Panel != null && panelTexture != null) return;
            panelTexture = Solid(new Color(0.07f, 0.08f, 0.09f, 0.94f));
            buttonTexture = Solid(new Color(0.16f, 0.18f, 0.21f, 1f));
            buttonHover = Solid(new Color(0.26f, 0.2f, 0.18f, 1f));

            Panel = new GUIStyle { padding = new RectOffset(28, 28, 22, 24), normal = { background = panelTexture } };
            Title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, normal = { textColor = Fg } };
            Subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Dim } };
            Heading = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 14, 4), normal = { textColor = Accent } };
            Text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = Fg } };
            Muted = new GUIStyle(Text) { normal = { textColor = Dim } };
            Key = new GUIStyle(Text) { fontStyle = FontStyle.Bold };
            Button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14, padding = new RectOffset(12, 12, 6, 6),
                normal = { background = buttonTexture, textColor = Fg }, hover = { background = buttonHover, textColor = Fg },
                active = { background = buttonHover, textColor = Fg }, focused = { background = buttonTexture, textColor = Fg },
            };
            BigButton = new GUIStyle(Button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 18, 8, 8) };
            Hint = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(16, 16, 10, 10), normal = { textColor = Fg, background = panelTexture } };
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
