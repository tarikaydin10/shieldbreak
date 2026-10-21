using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Every game scene explains itself from its scene catalog entry: when it starts, a card with title and
    /// objective (where the entry asks for it; the duel and the tutorial have their own start texts); F2 shows the
    /// full help with objective, checklist and every control of the entry, F2 again hides it. Read-only: ticking
    /// off happens in the pause menu (the cursor is free there). Time keeps running underneath. Bootstrapped like
    /// the pause menu, so every scene has it; not in the main menu.
    /// </summary>
    public class SceneBriefing : MonoBehaviour, IAdminStatus
    {
        enum Mode { Hidden, Card, Full }

        const float CardWidth = 520f;

        Mode mode;
        float cardUntil;
        bool resolvePending = true;
        InputAction helpAction;
        Vector2 leftScroll, rightScroll;

        /// <summary>The catalog entry of the running scene (null in the main menu or without an entry).</summary>
        public static SceneCatalogDefinition.Entry Current { get; private set; }

        public string AdminStatusLabel => "Szene";
        public string AdminStatus => Current == null ? "kein Eintrag im Szenen-Katalog" :
            $"{Current.title} · Prüfliste {TestChecklist.Progress(Current)} · {GameInput.KeyboardBinding(helpAction)} Hilfe";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<SceneBriefing>() != null) return;
            var go = new GameObject("SceneBriefing");
            DontDestroyOnLoad(go);
            go.AddComponent<SceneBriefing>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void OnEnable()
        {
            helpAction = GameInput.Action("Global", "Help");
            helpAction.performed += OnHelp;
            GameInput.Map("Global").Enable();
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDisable()
        {
            helpAction.performed -= OnHelp;
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        void OnSceneChanged(Scene from, Scene to)
        {
            mode = Mode.Hidden;
            Current = null;
            resolvePending = true;
        }

        void OnHelp(InputAction.CallbackContext _)
        {
            if (MainMenu.IsActive || Current == null) return;
            mode = mode == Mode.Full ? Mode.Hidden : Mode.Full;
            leftScroll = rightScroll = Vector2.zero;
        }

        void Update()
        {
            // After the scene change; its SimRunner has taken its scenario by then (Awake)
            if (resolvePending)
            {
                resolvePending = false;
                Current = MainMenu.IsActive ? null : SceneLaunch.Running();
                if (Current != null && Current.briefingOnStart && SceneLaunch.Catalog.briefingSeconds > 0f)
                {
                    mode = Mode.Card;
                    cardUntil = Time.unscaledTime + SceneLaunch.Catalog.briefingSeconds;
                }
            }
            if (mode == Mode.Card && Time.unscaledTime >= cardUntil) mode = Mode.Hidden;
        }

        void OnGUI()
        {
            if (mode == Mode.Hidden || Current == null) return;
            if (AdminMenu.IsOpen || PauseMenu.IsOpen || DuelDirector.ResultOpen || RadialMenu.IsOpen) return;
            Vector2 screen = MenuStyle.BeginScaled();
            if (mode == Mode.Card) DrawCard(screen);
            else DrawFull(screen);
        }

        void DrawCard(Vector2 screen)
        {
            float width = Mathf.Min(CardWidth, screen.x - 32f);
            GUILayout.BeginArea(new Rect(screen.x - width - 16f, 48f, width, screen.y - 96f));
            GUILayout.BeginVertical(MenuStyle.Panel);
            GUILayout.Label(Current.title, MenuStyle.Large);
            GUILayout.Label(Current.objective, MenuStyle.Text);
            GUILayout.Space(6);
            GUILayout.Label($"{GameInput.KeyboardBinding(helpAction)}: Steuerung und Prüfliste · {TestChecklist.Binding("Pause")}: Pause, Prüfliste abhaken", MenuStyle.Muted);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        void DrawFull(Vector2 screen)
        {
            var rect = MenuStyle.CentredPanel(screen, 1180f, 940f);
            GUILayout.BeginArea(rect, MenuStyle.Panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Current.title, MenuStyle.Large);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{GameInput.KeyboardBinding(helpAction)} schließt · die Zeit läuft weiter", MenuStyle.Muted);
            GUILayout.EndHorizontal();
            float height = rect.height - 110f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(rect.width * 0.42f));
            leftScroll = GUILayout.BeginScrollView(leftScroll, GUILayout.Height(height));
            TestChecklist.DrawObjective(Current);
            TestChecklist.DrawChecks(Current, editable: false);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.Space(20);
            GUILayout.BeginVertical();
            rightScroll = GUILayout.BeginScrollView(rightScroll, GUILayout.Height(height));
            TestChecklist.DrawControls(Current);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
