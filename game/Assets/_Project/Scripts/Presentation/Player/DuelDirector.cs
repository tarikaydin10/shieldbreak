using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Sim.Combat.Grammar;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Runs the M1 duel: switches feel preset, bot level and spectator mode (rebuilding the world), turns impacts
    /// into hitstop and camera kicks, and shows debug bars and the result. Works on a runtime copy of the scenario
    /// so the asset on disk stays untouched.
    /// </summary>
    public class DuelDirector : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        public ThirdPersonCamera combatCamera;
        [Tooltip("Health and stamina bars over the heads (debug; the game feel spec has no combat HUD).")]
        public bool showBars;
        [Tooltip("Slow motion on decisive kills (the hero or the last enemy): sim seconds and time scale.")]
        public float killSlowMoSeconds = 0.1f;
        [Range(0.05f, 1f)] public float killSlowMoScale = 0.25f;

        DuelScenarioDefinition scenario;
        InputActionMap map;
        InputAction restart, nextPreset, nextLevel, nextOpponents, nextBotWeapon, spectator, bars;
        Texture2D pixel;
        GUIStyle banner;

        public string AdminStatusLabel => "Duell";
        public string AdminStatus
        {
            get
            {
                if (scenario == null || runner.World == null) return "kein Duell-Szenario";
                string preset = scenario.ActivePreset != null ? scenario.ActivePreset.name.Replace("CombatTuning_", "") : "?";
                string who = scenario.spectator ? $"Zuschauer: L{scenario.spectatorHeroLevel} gegen L{scenario.botLevel}" : $"Gegner L{scenario.botLevel} ×{scenario.opponents}";
                var combat = runner.World.Combat;
                string hero = combat.Length > 0 ? $"Held {combat[0].Health:0} HP / {combat[0].Stamina:0} Ausdauer" : "";
                string weapon = scenario.botWeapon < 0 ? "gemischt" : scenario.weapons.Length > scenario.botWeapon && scenario.weapons[scenario.botWeapon] != null ? scenario.weapons[scenario.botWeapon].displayName : "?";
                return $"Preset {preset} · {who} ({weapon}) · {hero}";
            }
        }

        void Awake()
        {
            if (runner.scenario is DuelScenarioDefinition source)
            {
                scenario = Instantiate(source);
                scenario.name = source.name;
                runner.scenario = scenario;
                // Main menu choice (preset, level, opponents, weapons, tutorial)
                bool fromMenu = Menu.DuelLaunch.Pending;
                Menu.DuelLaunch.Apply(scenario);
                // Build smoke test: SHIELDBREAK.exe -duelSpectator [-duelOpponents 3] [-duelBotWeapon 1] [-duelHeroWeapon 1] watches bots fight
                var args = System.Environment.GetCommandLineArgs();
                bool changed = System.Array.IndexOf(args, "-duelSpectator") >= 0;
                if (changed) scenario.spectator = true;
                if (IntArg(args, "-duelOpponents", out int opponents)) { scenario.opponents = Mathf.Clamp(opponents, 1, 6); changed = true; }
                if (IntArg(args, "-duelBotWeapon", out int botWeapon)) { scenario.botWeapon = Mathf.Clamp(botWeapon, -1, scenario.weapons.Length - 1); changed = true; }
                if (IntArg(args, "-duelHeroWeapon", out int heroWeapon))
                {
                    // Draw this weapon first, keep the others in the loadout
                    var slots = new System.Collections.Generic.List<int> { heroWeapon };
                    foreach (int w in scenario.heroLoadout) if (w != heroWeapon) slots.Add(w);
                    scenario.heroLoadout = slots.ToArray();
                    changed = true;
                }
                if (Array.IndexOf(args, "-duelTutorial") >= 0) { Menu.DuelLaunch.Tutorial = true; Menu.DuelLaunch.Pending = true; Menu.DuelLaunch.Apply(scenario); changed = true; }
                if (changed || fromMenu) runner.Restart();
            }
        }

        static bool IntArg(string[] args, string name, out int value)
        {
            value = 0;
            int at = System.Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out value);
        }

        void OnEnable()
        {
            map = GameInput.Map("Duel");
            restart = map.FindAction("Restart", true);
            nextPreset = map.FindAction("NextPreset", true);
            nextLevel = map.FindAction("NextBotLevel", true);
            nextOpponents = map.FindAction("NextOpponentCount", true);
            nextBotWeapon = map.FindAction("NextBotWeapon", true);
            spectator = map.FindAction("ToggleSpectator", true);
            bars = map.FindAction("ToggleBars", true);
            map.Enable();
            runner.HitstopHook += OnTick;
        }

        void OnDisable()
        {
            map?.Disable();
            if (runner != null) runner.HitstopHook -= OnTick;
        }

        void OnDestroy()
        {
            if (scenario != null) Destroy(scenario);
            if (pixel != null) Destroy(pixel);
        }

        void Update()
        {
            if (scenario == null) return;
            bool rebuild = false;
            if (restart.WasPressedThisFrame()) rebuild = true;
            if (nextPreset.WasPressedThisFrame() && scenario.presets.Length > 0)
            {
                scenario.activePreset = (scenario.activePreset + 1) % scenario.presets.Length;
                rebuild = true;
            }
            if (nextLevel.WasPressedThisFrame())
            {
                scenario.botLevel = scenario.botLevel % 5 + 1;
                rebuild = true;
            }
            if (nextOpponents.WasPressedThisFrame())
            {
                scenario.opponents = scenario.opponents % 3 + 1;
                rebuild = true;
            }
            if (nextBotWeapon.WasPressedThisFrame())
            {
                // Each weapon in turn, then mixed (-1)
                scenario.botWeapon = scenario.botWeapon + 1 >= scenario.weapons.Length ? -1 : scenario.botWeapon + 1;
                rebuild = true;
            }
            if (spectator.WasPressedThisFrame())
            {
                scenario.spectator = !scenario.spectator;
                rebuild = true;
            }
            if (bars.WasPressedThisFrame()) showBars = !showBars;
            if (rebuild) runner.Restart();
        }

        /// <summary>After every tick: hitstop and camera kick for impacts the local view cares about.</summary>
        void OnTick(SimRunner r)
        {
            var preset = scenario != null ? scenario.ActivePreset : null;
            if (preset == null) return;
            int hero = r.PlayerAgent;
            foreach (var e in r.World.LastEvents)
            {
                if (e.Type == CombatEventType.Death && IsDecisive(r, e.Target)) r.RequestSlowMotion(killSlowMoSeconds, killSlowMoScale);
                bool impact = e.Type == CombatEventType.Hit || e.Type == CombatEventType.Blocked || e.Type == CombatEventType.PerfectBlock || e.Type == CombatEventType.GuardBreak;
                if (!impact) continue;
                bool mine = hero < 0 || e.Attacker == hero || e.Target == hero;
                if (!mine) continue;
                float t = Mathf.Clamp01(e.Energy / preset.hitstopFullEnergy);
                r.RequestHitstop(Mathf.Lerp(preset.hitstopMs.x, preset.hitstopMs.y, t) * 0.001f);
                if (combatCamera == null) continue;
                bool ownHit = e.Attacker == hero;
                Vector2 dir = ownHit ? SwingDirection(e.Direction) : new Vector2(0f, -1f);
                combatCamera.Kick(dir, e.Energy, ownHit && e.Type == CombatEventType.Hit);
            }
        }

        /// <summary>The hero (agent 0, also in spectator mode) died, or the last of its enemies did.</summary>
        static bool IsDecisive(SimRunner r, int target)
        {
            var agents = r.World.Agents;
            var combat = r.World.Combat;
            if (target == 0) return true;
            for (int i = 0; i < agents.Length; i++)
                if (agents[i].Team != agents[0].Team && combat[i].State != CombatState.Dead) return false;
            return true;
        }

        static Vector2 SwingDirection(int direction) =>
            direction == AttackDirection.Left ? new Vector2(1f, 0f) :
            direction == AttackDirection.Right ? new Vector2(-1f, 0f) :
            direction == AttackDirection.Thrust ? new Vector2(0f, 0.5f) : new Vector2(0f, 1f);

        void OnGUI()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || scenario == null) return;
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
                banner = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            var cam = combatCamera != null ? combatCamera.GetComponent<Camera>() : Camera.main;
            var agents = world.Agents;
            var combat = world.Combat;
            var tuning = world.CombatSetup.Tuning;

            if (showBars && cam != null)
            {
                for (int i = 0; i < agents.Length; i++)
                {
                    if (combat[i].State == CombatState.Dead) continue;
                    Vector3 s = cam.WorldToScreenPoint(SimRunner.ToWorld(runner.InterpolatedPosition(i), 2.05f));
                    if (s.z <= 0f) continue;
                    float x = s.x - 30f, y = Screen.height - s.y;
                    Bar(x, y, 60f, 5f, combat[i].Health / tuning.HealthMax, new Color(0.85f, 0.2f, 0.2f));
                    Bar(x, y + 7f, 60f, 3f, combat[i].Stamina / tuning.StaminaMax, new Color(0.95f, 0.8f, 0.25f));
                }
            }

            int heroTeam = agents.Length > 0 ? agents[0].Team : 0;
            bool heroDead = combat.Length > 0 && combat[0].State == CombatState.Dead;
            bool enemiesDead = true;
            for (int i = 0; i < agents.Length; i++)
                if (agents[i].Team != heroTeam && combat[i].State != CombatState.Dead) enemiesDead = false;
            string text = heroDead ? (scenario.spectator ? "Rot fällt" : "Besiegt") : enemiesDead ? (scenario.spectator ? "Rot gewinnt" : "Sieg") : null;
            if (text != null)
            {
                GUI.color = heroDead ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.9f, 0.5f);
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), text, banner);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, Screen.height * 0.3f + 50, Screen.width, 30), "F5 neu · F6 Kampfgefühl · F7 Gegner-Stufe · F10 Gegnerzahl · F11 Gegner-Waffe · F8 Zuschauen", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
            }
        }

        void Bar(float x, float y, float w, float h, float fill, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 1, y - 1, w + 2, h + 2), pixel);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill), h), pixel);
            GUI.color = Color.white;
        }
    }
}
