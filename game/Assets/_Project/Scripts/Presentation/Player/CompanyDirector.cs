using Shieldbreak.Data;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Cameras;
using Shieldbreak.Presentation.Menu;
using Shieldbreak.Sim.Combat.Grammar;
using UnityEngine;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Runs the M2 company battle (preview of M2 task 8): hitstop and camera kicks for the hero's impacts (same feel
    /// as the duel), a start banner with the order controls, and the state of both sides in the F1 menu. The result
    /// screen follows with task 8; restart through the pause menu.
    /// </summary>
    public class CompanyDirector : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        public ThirdPersonCamera combatCamera;
        [Tooltip("How long the start banner stays (s).")]
        [Min(0f)] public float bannerSeconds = 6f;

        float bannerUntil;

        public string AdminStatusLabel => "Kompanie";
        public string AdminStatus
        {
            get
            {
                var world = runner != null ? runner.World : null;
                if (world == null) return "–";
                int fightA = 0, fightB = 0, broken = 0, brokenEnemy = 0;
                var agents = world.Agents;
                int myTeam = runner.PlayerAgent >= 0 ? agents[runner.PlayerAgent].Team : 0;
                for (int i = 0; i < agents.Length; i++)
                {
                    var a = agents[i];
                    bool own = a.Team == myTeam;
                    if (a.IsRouting || a.HasFled) { if (own) broken++; else brokenEnemy++; }
                    else if (!a.IsDead) { if (own) fightA++; else fightB++; }
                }
                var hero = runner.PlayerAgent >= 0 ? world.Combat[runner.PlayerAgent] : default;
                return $"Eigene {fightA} kämpfend, {broken} geflohen · Feind {fightB} kämpfend, {brokenEnemy} geflohen · Held {hero.Health:0} HP";
            }
        }

        void OnEnable()
        {
            if (runner == null) return;
            runner.HitstopHook += OnTick;
            runner.WorldRebuilt += ShowBanner;
            ShowBanner();
        }

        void OnDisable()
        {
            if (runner == null) return;
            runner.HitstopHook -= OnTick;
            runner.WorldRebuilt -= ShowBanner;
        }

        void ShowBanner() => bannerUntil = Time.unscaledTime + bannerSeconds;

        /// <summary>After every tick: hitstop and camera kick for impacts on or by the hero (as in the duel).</summary>
        void OnTick(SimRunner r)
        {
            var preset = r.scenario is CompanyScenarioDefinition company ? company.combatTuning : null;
            if (preset == null) return;
            int hero = r.PlayerAgent;
            foreach (var e in r.World.LastEvents)
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

        static Vector2 SwingDirection(int direction) =>
            direction == AttackDirection.Left ? new Vector2(1f, 0f) :
            direction == AttackDirection.Right ? new Vector2(-1f, 0f) :
            direction == AttackDirection.Thrust ? new Vector2(0f, 0.5f) : new Vector2(0f, 1f);

        void OnGUI()
        {
            if (Time.unscaledTime >= bannerUntil || AdminMenu.IsOpen || PauseMenu.IsOpen) return;
            Vector2 screen = MenuStyle.BeginScaled();
            const string text = "Kompanie (Vorschau) · Der Feind rückt vor. F halten (RB): Befehle, zielen, loslassen · Mausrad (LB): welche Formation · Esc: Neustart";
            GUI.Label(new Rect(screen.x * 0.5f - 430f, screen.y - 110f, 860f, 56f), text, MenuStyle.Hint);
        }
    }
}
