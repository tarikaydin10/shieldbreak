using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Presentation.Menu;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Morale;
using UnityEngine;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Runs the M2 company battle: hitstop and camera kicks for the hero's impacts (same feel as the duel), the state
    /// of both sides in the F1 menu, and the result screen once the battle is decided (BattleOutcome: a side broke or
    /// the hero fell). Objective and controls come from the scene catalog (SceneBriefing).
    /// </summary>
    public class CompanyDirector : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        public ThirdPersonCamera combatCamera;
        [Tooltip("Sim seconds between the decision and the result screen, so the rout can be watched.")]
        public float resultDelaySeconds = 2.5f;

        struct SideCount { public int Start, Standing, Fallen, Fled; }

        BattleResult outcome;
        int decidedTick = -1, heroKills, hitsLanded, hitsTaken;
        float damageDealt, damageTaken, commandSeconds;
        SideCount own, enemy;
        bool showing, counted;

        public string AdminStatusLabel => "Kompanie";
        public string AdminStatus
        {
            get
            {
                var world = runner != null ? runner.World : null;
                if (world == null) return "–";
                Count(out var mine, out var theirs);
                var hero = runner.PlayerAgent >= 0 ? world.Combat[runner.PlayerAgent] : default;
                string state = outcome == BattleResult.Undecided ? "läuft" : outcome == BattleResult.Victory ? "Sieg" : "Niederlage";
                return $"Eigene {mine.Standing} kämpfend, {mine.Fled} geflohen · Feind {theirs.Standing} kämpfend, {theirs.Fled} geflohen · Held {hero.Health:0} HP · {state}";
            }
        }

        void OnEnable()
        {
            if (runner == null) return;
            runner.HitstopHook += OnTick;
            runner.WorldRebuilt += ResetBattle;
            ResetBattle();
        }

        void OnDisable()
        {
            ResultScreen.IsOpen = false;
            if (runner == null) return;
            runner.HitstopHook -= OnTick;
            runner.WorldRebuilt -= ResetBattle;
        }

        void ResetBattle()
        {
            outcome = BattleResult.Undecided;
            decidedTick = -1;
            heroKills = hitsLanded = hitsTaken = 0;
            damageDealt = damageTaken = commandSeconds = 0f;
            showing = counted = false;
            ResultScreen.IsOpen = false;
        }

        void Update()
        {
            if (outcome == BattleResult.Undecided && CommandView.IsOpen) commandSeconds += Time.unscaledDeltaTime;
        }

        /// <summary>After every tick: hero statistics, hitstop and camera kicks (as in the duel), the battle's outcome.</summary>
        void OnTick(SimRunner r)
        {
            int hero = r.PlayerAgent;
            var world = r.World;
            if (!counted)
            {
                // Strength at the start (the first tick, whatever order runner and director woke up in)
                Count(out own, out enemy);
                own.Start = own.Standing;
                enemy.Start = enemy.Standing;
                counted = true;
            }
            if (outcome == BattleResult.Undecided)
            {
                foreach (var e in world.LastEvents)
                {
                    if (hero < 0) break;
                    if (e.Type == CombatEventType.Death && e.Attacker == hero && e.Target != hero) heroKills++;
                    if (e.Type == CombatEventType.Hit && e.Attacker == hero) { hitsLanded++; damageDealt += e.Damage; }
                    if (e.Type == CombatEventType.Hit && e.Target == hero) { hitsTaken++; damageTaken += e.Damage; }
                }
                int team = hero >= 0 ? world.Agents[hero].Team : 0;
                outcome = BattleOutcome.Evaluate(world.Agents, team, hero);
                if (outcome != BattleResult.Undecided) decidedTick = world.Tick;
            }
            else if (!showing && world.Tick - decidedTick >= resultDelaySeconds / Sim.Core.SimConstants.DT)
            {
                showing = true;
                Count(out var mine, out var theirs);
                own = Keep(own.Start, mine);
                enemy = Keep(enemy.Start, theirs);
            }

            var preset = r.scenario is CompanyScenarioDefinition company ? company.combatTuning : null;
            if (preset == null) return;
            foreach (var e in world.LastEvents)
            {
                bool impact = e.Type == CombatEventType.Hit || e.Type == CombatEventType.Blocked || e.Type == CombatEventType.PerfectBlock || e.Type == CombatEventType.GuardBreak;
                if (!impact || hero < 0 || (e.Attacker != hero && e.Target != hero)) continue;
                float t = Mathf.Clamp01(e.Energy / preset.hitstopFullEnergy);
                r.RequestHitstop(Mathf.Lerp(preset.hitstopMs.x, preset.hitstopMs.y, t) * 0.001f);
                if (combatCamera == null) continue;
                bool ownHit = e.Attacker == hero;
                Vector2 dir = ownHit ? SwingDirection(e.Direction) : new Vector2(0f, -1f);
                combatCamera.Kick(dir, e.Energy, ownHit && e.Type == CombatEventType.Hit);
            }
        }

        static SideCount Keep(int start, SideCount now) { now.Start = start; return now; }

        /// <summary>Soldiers per side (the hero not counted): still fighting, killed, routing or fled.</summary>
        void Count(out SideCount mine, out SideCount theirs)
        {
            mine = theirs = default;
            var world = runner.World;
            int hero = runner.PlayerAgent;
            var agents = world.Agents;
            int team = hero >= 0 ? agents[hero].Team : 0;
            for (int i = 0; i < agents.Length; i++)
            {
                if (i == hero) continue;
                var a = agents[i];
                ref var side = ref a.Team == team ? ref mine : ref theirs;
                if (a.HasFled || (a.IsRouting && !a.IsDead)) side.Fled++;
                else if (a.IsDead) side.Fallen++;
                else side.Standing++;
            }
        }

        static Vector2 SwingDirection(int direction) =>
            direction == AttackDirection.Left ? new Vector2(1f, 0f) :
            direction == AttackDirection.Right ? new Vector2(-1f, 0f) :
            direction == AttackDirection.Thrust ? new Vector2(0f, 0.5f) : new Vector2(0f, 1f);

        void OnGUI()
        {
            ResultScreen.IsOpen = showing;
            if (!showing || runner == null || runner.World == null || PauseMenu.IsOpen || AdminMenu.IsOpen) return;

            // Result screen: who broke, what it cost, then clear choices
            var screen = MenuStyle.BeginScaled();
            var rect = MenuStyle.CentredPanel(screen, 600, 500);
            GUILayout.BeginArea(rect, MenuStyle.Panel);
            var world = runner.World;
            int hero = runner.PlayerAgent;
            bool heroFell = hero >= 0 && world.Agents[hero].IsDead;
            bool won = outcome == BattleResult.Victory;
            var prev = GUI.color;
            GUI.color = won ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 0.55f, 0.5f);
            GUILayout.Label(won ? "Sieg" : heroFell ? "Gefallen" : "Niederlage", MenuStyle.Title);
            GUI.color = prev;
            GUILayout.Label(won ? "Der Feind ist gebrochen und flieht." : heroFell ? "Der Held ist gefallen, die Schlacht ist verloren." : "Deine Leute sind gebrochen.", MenuStyle.Subtitle);
            GUILayout.Space(8);
            GUILayout.Label("STATISTIK", MenuStyle.Heading);
            MenuStyle.Row("Dauer", $"{decidedTick * Sim.Core.SimConstants.DT:0} s");
            MenuStyle.Row("Eigene Truppe", Describe(own));
            MenuStyle.Row("Feind", Describe(enemy));
            MenuStyle.Row("Held", $"{heroKills} besiegt, {hitsLanded} Treffer ({damageDealt:0} Schaden)");
            MenuStyle.Row("Held getroffen", $"{hitsTaken} mal ({damageTaken:0} Schaden)");
            MenuStyle.Row("Kommandoansicht", $"{commandSeconds:0} s");
            GUILayout.Space(12);
            if (MenuStyle.Big("Nochmal · gleiche Schlacht")) runner.Restart();
            if (MenuStyle.Big("Hauptmenü")) UnityEngine.SceneManagement.SceneManager.LoadScene(MainMenu.SceneName);
            GUILayout.EndArea();
        }

        static string Describe(SideCount s) => $"{s.Standing} von {s.Start} stehen · {s.Fallen} gefallen · {s.Fled} geflohen";
    }
}
