using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Overlay present in every scene (bootstrapped automatically). F1 toggles it (Esc belongs to the PauseMenu).
    /// Lists every input of the currently enabled action maps with its binding and description, the status
    /// lines of all IAdminStatus components, and a few viewer settings. Debug tooling, IMGUI on purpose.
    /// </summary>
    public class AdminMenu : MonoBehaviour
    {
        const string PrefHint = "admin.showHint";
        const string PrefInactive = "admin.showInactive";
        const string PrefFps = "admin.showFps";
        const float StatusRefreshSeconds = 0.5f;

        bool open;
        /// <summary>True while the menu is shown; gameplay input (mouse look, attacks) pauses then.</summary>
        public static bool IsOpen { get; private set; }
        // Screen-space rect (GUI coordinates, y down) of the open panel; empty while closed
        static Rect panelScreenRect;
        bool showHint, showInactive, showFps;
        Vector2 scroll;
        float fps;
        float nextStatusRefresh;
        readonly List<IAdminStatus> statusProviders = new List<IAdminStatus>();

        InputAction toggleAction, helpAction;
        GUIStyle panel, title, heading, text, muted, key, paused, hint;
        Texture2D panelTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<AdminMenu>() != null) return;
            var go = new GameObject("AdminMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<AdminMenu>();
        }

        void Awake()
        {
            showHint = PlayerPrefs.GetInt(PrefHint, 1) == 1;
            showInactive = PlayerPrefs.GetInt(PrefInactive, 0) == 1;
            showFps = PlayerPrefs.GetInt(PrefFps, 0) == 1;
        }

        void OnEnable()
        {
            toggleAction = GameInput.Action("Global", "ToggleAdminMenu");
            helpAction = GameInput.Action("Global", "Help");
            toggleAction.performed += OnToggle;
            GameInput.Map("Global").Enable();
        }

        void OnDisable()
        {
            toggleAction.performed -= OnToggle;
        }

        void OnDestroy()
        {
            if (panelTexture != null) Destroy(panelTexture);
        }

        void OnToggle(InputAction.CallbackContext _) => open = !open;

        void Update()
        {
            IsOpen = open;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) fps = Mathf.Lerp(fps <= 0f ? 1f / dt : fps, 1f / dt, 0.1f);
            if (open && Time.unscaledTime >= nextStatusRefresh)
            {
                nextStatusRefresh = Time.unscaledTime + StatusRefreshSeconds;
                statusProviders.Clear();
                foreach (var b in FindObjectsByType<MonoBehaviour>())
                    if (b is IAdminStatus s && b.isActiveAndEnabled) statusProviders.Add(s);
                statusProviders.Sort((a, b) => string.CompareOrdinal(a.AdminStatusLabel, b.AdminStatusLabel));
            }
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            if (showFps) Badge($"{fps:0} fps", w - 10, alignRight: true);

            if (!open)
            {
                panelScreenRect = Rect.zero;
                if (showHint)
                {
                    string help = Menu.MainMenu.IsActive ? "" : $" · {GameInput.KeyboardBinding(helpAction)} Hilfe";
                    string label = GamePause.IsPaused ? "PAUSIERT" : $"{Binding(toggleAction)}  Status & Admin{help} · Esc Menü";
                    Badge(label, 12, alignRight: false);
                }
                return;
            }

            GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(460, w - 32), h - 32));
            GUILayout.BeginVertical(panel);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(h - 64));

            GUILayout.BeginHorizontal();
            GUILayout.Label("SHIELDBREAK · ADMIN", title);
            GUILayout.FlexibleSpace();
            GUILayout.Label(SceneManager.GetActiveScene().name, muted);
            GUILayout.EndHorizontal();

            if (GamePause.IsPaused) GUILayout.Label("PAUSIERT", paused);

            if (statusProviders.Count > 0)
            {
                GUILayout.Label("STATUS", heading);
                foreach (var s in statusProviders) Row(s.AdminStatusLabel, s.AdminStatus);
            }

            GUILayout.Label("STEUERUNG", heading);
            foreach (var map in GameInput.Asset.actionMaps)
            {
                bool active = map.enabled;
                if (!active && !showInactive) continue;
                GUILayout.Label(active ? GameInput.MapTitleOf(map) : $"{GameInput.MapTitleOf(map)} (inaktiv)", active ? text : muted);
                foreach (var action in map.actions)
                    Row(Binding(action), GameInput.Describe(action), !active);
                GUILayout.Space(4);
            }

            if (SceneManager.sceneCountInBuildSettings > 1)
            {
                GUILayout.Label("SZENEN", heading);
                int active = SceneManager.GetActiveScene().buildIndex;
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                {
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                    GUI.enabled = i != active;
                    if (GUILayout.Button(i == active ? $"{sceneName} (aktiv)" : sceneName))
                    {
                        GamePause.Set(false);
                        open = false;
                        SceneManager.LoadScene(i);
                    }
                    GUI.enabled = true;
                }
            }

            GUILayout.Label("EINSTELLUNGEN", heading);
            showHint = Setting(PrefHint, showHint, "Hinweis oben links zeigen, wenn das Menü zu ist");
            showInactive = Setting(PrefInactive, showInactive, "Inaktive Steuerung mit anzeigen");
            showFps = Setting(PrefFps, showFps, "FPS anzeigen");

            GUILayout.Space(6);
            GUILayout.Label("Neue Eingaben: Resources/Input/Shieldbreak.inputactions + controls.de.json", muted);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                panelScreenRect = new Rect((16 + r.x) * scale, (16 + r.y) * scale, r.width * scale, r.height * scale);
            }
            GUILayout.EndArea();
        }

        void Badge(string label, float x, bool alignRight)
        {
            Vector2 size = hint.CalcSize(new GUIContent(label)) + new Vector2(2f, 0f);
            GUI.Label(new Rect(alignRight ? x - size.x : x, 10, size.x, size.y), label, hint);
        }

        /// <summary>True if a pointer at <paramref name="screenPosition"/> (input space, y up) is over the open menu.</summary>
        public static bool BlocksPointer(Vector2 screenPosition)
        {
            if (Menu.PauseMenu.BlocksPointer(screenPosition)) return true;
            if (panelScreenRect.width <= 0f) return false;
            return panelScreenRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        static string Binding(InputAction action)
        {
            string s = action.GetBindingDisplayString();
            return string.IsNullOrEmpty(s) ? "–" : s;
        }

        void Row(string left, string right, bool dim = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(left, dim ? muted : key, GUILayout.Width(150));
            GUILayout.Label(right, dim ? muted : text);
            GUILayout.EndHorizontal();
        }

        bool Setting(string pref, bool value, string label)
        {
            bool next = GUILayout.Toggle(value, " " + label);
            if (next != value) PlayerPrefs.SetInt(pref, next ? 1 : 0);
            return next;
        }

        void EnsureStyles()
        {
            if (panel != null) return;
            panelTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panelTexture.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.09f, 0.92f));
            panelTexture.Apply();

            var fg = new Color(0.91f, 0.9f, 0.87f);
            var dim = new Color(0.6f, 0.62f, 0.66f);
            var accent = new Color(0.88f, 0.4f, 0.29f);

            panel = new GUIStyle { padding = new RectOffset(16, 16, 12, 14), normal = { background = panelTexture } };
            title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = fg } };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 12, 4), normal = { textColor = accent } };
            text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = fg } };
            muted = new GUIStyle(text) { normal = { textColor = dim } };
            key = new GUIStyle(text) { fontStyle = FontStyle.Bold };
            paused = new GUIStyle(title) { fontSize = 22, normal = { textColor = accent } };
            // No word wrap: with the width measured exactly, rounding would push the last word onto a clipped second line
            hint = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false, clipping = TextClipping.Overflow, padding = new RectOffset(8, 8, 4, 4), normal = { textColor = fg, background = panelTexture } };
        }
    }
}
