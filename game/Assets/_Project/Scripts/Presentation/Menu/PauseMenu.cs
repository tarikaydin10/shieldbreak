using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Esc in any game scene: pauses (GamePause, the sim runs zero ticks) and shows resume, restart, objective and
    /// checklist of the scene (ticked off here, the cursor is free), controls, options and back to the main menu.
    /// Bootstrapped like the admin menu, so every scene has it. Not in the main menu scene itself. Multiplayer (M6)
    /// will show the menu without pausing.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        enum Page { Home, Checklist, Controls, Options }

        public static bool IsOpen { get; private set; }
        static Rect panelScreenRect;

        Page page;
        bool open;
        Vector2 checklistScroll;
        InputAction pauseAction;
        readonly ControlsPage controls = new ControlsPage();
        readonly OptionsPage options = new OptionsPage();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<PauseMenu>() != null) return;
            var go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<PauseMenu>();
        }

        void OnEnable()
        {
            pauseAction = GameInput.Action("Global", "Pause");
            pauseAction.performed += OnPause;
            GameInput.Map("Global").Enable();
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDisable()
        {
            pauseAction.performed -= OnPause;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        void OnSceneChanged(Scene from, Scene to)
        {
            controls.Cancel();
            open = false;
            page = Page.Home;
            GamePause.Set(false);
        }

        void OnPause(InputAction.CallbackContext _)
        {
            if (MainMenu.IsActive) return;
            if (controls.IsRebinding) return; // Esc cancels the rebind instead
            if (open && page != Page.Home) { page = Page.Home; return; }
            Set(!open);
        }

        void Set(bool value)
        {
            open = value;
            page = Page.Home;
            GamePause.Set(open);
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update() => IsOpen = open;

        /// <summary>True if a pointer at <paramref name="screenPosition"/> (input space, y up) is over the open menu.</summary>
        public static bool BlocksPointer(Vector2 screenPosition) =>
            panelScreenRect.width > 0f && panelScreenRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        void OnGUI()
        {
            if (!open)
            {
                panelScreenRect = Rect.zero;
                return;
            }
            var screen = MenuStyle.BeginScaled();
            float scale = MenuStyle.Scale;
            var entry = SceneBriefing.Current;
            var rect = MenuStyle.CentredPanel(screen, page == Page.Controls || page == Page.Checklist ? 860 : 520, page == Page.Home ? 480 : 760);
            panelScreenRect = new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);
            GUILayout.BeginArea(rect, MenuStyle.Panel);
            GUILayout.Label("PAUSE", MenuStyle.Title);
            GUILayout.Label(SceneManager.GetActiveScene().name, MenuStyle.Subtitle);
            GUILayout.Space(10);
            switch (page)
            {
                case Page.Home:
                    if (MenuStyle.Big("Fortsetzen (Esc)")) Set(false);
                    var runner = FindAnyObjectByType<SimRunner>();
                    if (runner != null && MenuStyle.Big("Neu starten")) { runner.Restart(); Set(false); }
                    if (entry != null && MenuStyle.Big($"Ziel und Prüfliste ({TestChecklist.Progress(entry)})"))
                    {
                        page = Page.Checklist;
                        checklistScroll = Vector2.zero;
                    }
                    if (MenuStyle.Big("Steuerung")) page = Page.Controls;
                    if (MenuStyle.Big("Optionen")) page = Page.Options;
                    GUILayout.Space(8);
                    if (MenuStyle.Big("Zum Hauptmenü")) { Set(false); SceneManager.LoadScene(MainMenu.SceneName); }
                    GUILayout.Space(6);
                    GUILayout.Label("F1 zeigt Status und Admin-Werkzeuge.", MenuStyle.Muted);
                    break;
                case Page.Checklist:
                    if (entry == null) { page = Page.Home; break; }
                    GUILayout.Label(entry.title, MenuStyle.Large);
                    checklistScroll = GUILayout.BeginScrollView(checklistScroll, GUILayout.Height(rect.height - 270f));
                    TestChecklist.DrawObjective(entry);
                    TestChecklist.DrawChecks(entry, editable: true);
                    GUILayout.EndScrollView();
                    GUILayout.Label("Alle Szenen und ihre Prüflisten: Hauptmenü → Testmodus.", MenuStyle.Muted);
                    if (MenuStyle.Small("Zurück", 120)) page = Page.Home;
                    break;
                case Page.Controls:
                    if (controls.Draw(rect.height - 220f, showOnlyGameplay: false)) page = Page.Home;
                    break;
                case Page.Options:
                    if (options.Draw()) page = Page.Home;
                    break;
            }
            GUILayout.EndArea();
        }
    }
}
