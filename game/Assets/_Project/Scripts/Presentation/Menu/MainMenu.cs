using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Main menu scene (first scene of the build): play a duel (with setup), tutorial, watch bots, sandbox and
    /// prototypes, controls (rebinding), options, quit. Admin-grade IMGUI; the real front end comes with M5.
    /// </summary>
    public class MainMenu : MonoBehaviour, IAdminStatus
    {
        public const string SceneName = "MainMenu";
        enum Page { Home, Duel, Controls, Options }

        [Tooltip("Duel scenario for the setup page (presets, weapons).")]
        public DuelScenarioDefinition duel;
        public string version = "M1 Duell";

        Page page;
        readonly ControlsPage controls = new ControlsPage();
        readonly OptionsPage options = new OptionsPage();

        public static bool IsActive => SceneManager.GetActiveScene().name == SceneName;

        public string AdminStatusLabel => "Hauptmenü";
        public string AdminStatus => $"Seite {page}";

        void Start()
        {
            GamePause.Set(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnGUI()
        {
            var screen = MenuStyle.BeginScaled();
            var rect = MenuStyle.CentredPanel(screen, page == Page.Controls ? 860 : 620, page == Page.Home ? 560 : 720);
            GUILayout.BeginArea(rect, MenuStyle.Panel);
            GUILayout.Label("SHIELDBREAK", MenuStyle.Title);
            GUILayout.Label($"{version} · Low-Poly-Schlachtsimulator · alles in Arbeit", MenuStyle.Subtitle);
            GUILayout.Space(10);
            switch (page)
            {
                case Page.Home: DrawHome(); break;
                case Page.Duel: DrawDuel(); break;
                case Page.Controls: if (controls.Draw(rect.height - 230f, showOnlyGameplay: true)) page = Page.Home; break;
                case Page.Options: if (options.Draw()) page = Page.Home; break;
            }
            GUILayout.EndArea();
        }

        void DrawHome()
        {
            if (MenuStyle.Big("Duell spielen")) page = Page.Duel;
            if (MenuStyle.Big("Tutorial · die Steuerung Schritt für Schritt")) Launch(tutorial: true);
            if (MenuStyle.Big("Zuschauen · Bot gegen Bot")) Launch(spectator: true);
            if (MenuStyle.Big("Kompanie · Held mit eigener Truppe (Vorschau)")) Load("M2_Company");
            GUILayout.Space(8);
            GUILayout.Label("WERKZEUGE", MenuStyle.Heading);
            if (MenuStyle.Big("Sandbox · 20 gegen 20 aus der Draufsicht")) Load("M0_Sandbox");
            if (MenuStyle.Big("Prototyp · Oben-Hieb (Animation)")) Load("Proto_Animation");
            if (MenuStyle.Big("Prototyp · Look und Kameras")) Load("Proto_Style");
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (MenuStyle.Small("Steuerung", 140)) page = Page.Controls;
            if (MenuStyle.Small("Optionen", 140)) page = Page.Options;
            GUILayout.FlexibleSpace();
            if (MenuStyle.Small("Beenden", 120)) Quit();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            GUILayout.Label("Im Spiel: Esc Pause-Menü · F1 Status und Admin", MenuStyle.Muted);
        }

        void DrawDuel()
        {
            GUILayout.Label("DUELL", MenuStyle.Heading);
            if (DuelSetupPage.Draw(duel)) Launch();
            GUILayout.Space(8);
            if (MenuStyle.Small("Zurück", 120)) page = Page.Home;
        }

        void Launch(bool tutorial = false, bool spectator = false)
        {
            DuelLaunch.Tutorial = tutorial;
            DuelLaunch.Spectator = spectator;
            DuelLaunch.Pending = true;
            Load("M1_Duel");
        }

        static void Load(string scene)
        {
            GamePause.Set(false);
            SceneManager.LoadScene(scene);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
