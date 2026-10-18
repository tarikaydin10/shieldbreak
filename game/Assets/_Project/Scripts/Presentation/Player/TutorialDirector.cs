using System;
using System.Collections.Generic;
using Shieldbreak.Presentation.Admin;
using Shieldbreak.Presentation.Menu;
using Shieldbreak.Sim.Combat.Grammar;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shieldbreak.Presentation.Player
{
    /// <summary>
    /// Guided duel (main menu → Tutorial): one hint at a time, each step completes when the sim reports the matching
    /// event or state, so the player learns by doing. A patient L2 opponent; if the hero dies the duel restarts at the
    /// same step. F12 skips a step. Presentation only: it watches events, it never touches the sim.
    /// </summary>
    public class TutorialDirector : MonoBehaviour, IAdminStatus
    {
        public SimRunner runner;
        [Min(0.2f)] public float doneFlashSeconds = 1.2f;

        class Step
        {
            public string Title, Text;
            public Func<TutorialDirector, bool> Done;
        }

        readonly List<Step> steps = new List<Step>();
        int index;
        float doneUntil = -1f;
        float2 startPos;
        bool sprintSeen, switchedToTwoHand;
        InputActionMap map;
        InputAction skip;
        public bool Active { get; private set; }

        public string AdminStatusLabel => "Tutorial";
        public string AdminStatus => !Active ? "aus (Hauptmenü → Tutorial)" : $"Schritt {index + 1}/{steps.Count}: {steps[math.min(index, steps.Count - 1)].Title}";

        void Awake()
        {
            bool fromArgs = Array.IndexOf(Environment.GetCommandLineArgs(), "-duelTutorial") >= 0;
            Active = DuelLaunch.Tutorial || fromArgs;
            if (!Active) { enabled = false; return; }
            Build();
        }

        void OnEnable()
        {
            if (!Active) return;
            map = GameInput.Map("Tutorial");
            skip = map.FindAction("Skip", true);
            map.Enable();
            runner.WorldRebuilt += OnRebuilt;
        }

        void OnDisable()
        {
            map?.Disable();
            if (runner != null) runner.WorldRebuilt -= OnRebuilt;
        }

        void OnRebuilt()
        {
            sprintSeen = false;
            switchedToTwoHand = false;
            if (runner.World != null && runner.PlayerAgent >= 0) startPos = runner.World.Agents[runner.PlayerAgent].Position;
        }

        void Build()
        {
            steps.Add(new Step { Title = "Bewegen", Text = "Lauf mit WASD, schau dich mit der Maus um. Geh ein paar Meter.", Done = t => t.HeroMoved(2.5f) });
            steps.Add(new Step { Title = "Leichter Angriff", Text = "Tipp die linke Maustaste: ein schneller Seitenhieb. Mehrmals tippen wechselt rechts und links.", Done = t => t.HeroAttacked(AttackDirection.Left) || t.HeroAttacked(AttackDirection.Right) });
            steps.Add(new Step { Title = "Schwerer Hieb", Text = "Halte die linke Maustaste: der Hieb wird zum schweren Hieb von oben (trifft den Kopf).", Done = t => t.HeroWindup(AttackDirection.Overhead) });
            steps.Add(new Step { Title = "Stich", Text = "Lauf nach vorn (W) und tipp dabei die linke Maustaste: ein Stich, der am Schildrand vorbeigeht.", Done = t => t.HeroAttacked(AttackDirection.Thrust) });
            steps.Add(new Step { Title = "Block", Text = "Halte die rechte Maustaste, wenn der Gegner ausholt. Der Block hält alles von vorn, kostet aber Ausdauer.", Done = t => t.HeroEvent(CombatEventType.Blocked, asTarget: true) });
            steps.Add(new Step { Title = "Perfect Block", Text = "Drück den Block erst kurz vor dem Treffer: kostet nichts und lässt den Gegner taumeln. Dann sofort zuschlagen.", Done = t => t.HeroEvent(CombatEventType.PerfectBlock, asTarget: true) });
            steps.Add(new Step { Title = "Finte", Text = "Hol aus und drück sofort Q: der Hieb bricht ab. Damit lockst du Blocks heraus.", Done = t => t.HeroEvent(CombatEventType.Feint, asTarget: false) });
            steps.Add(new Step { Title = "Ausweichschritt", Text = "Leertaste: ein Schritt von 2 m in Laufrichtung, ohne Richtung nach hinten. Weich einem Hieb aus.", Done = t => t.HeroEvent(CombatEventType.Step, asTarget: false) });
            steps.Add(new Step { Title = "Sprint-Angriff", Text = "Geh auf Abstand, halte Shift und lauf auf den Gegner zu, dann schlag zu: dein Tempo geht in den Treffer.", Done = t => t.SprintHit() });
            steps.Add(new Step { Title = "Schildstoß", Text = "E: Schildstoß. Bricht jeden erhobenen Block, der Gegner taumelt, und dein Stich danach trifft.", Done = t => t.HeroEvent(CombatEventType.GuardBreak, asTarget: false) || t.HeroEvent(CombatEventType.SpecialStart, asTarget: false) });
            steps.Add(new Step { Title = "Zweihänder", Text = "Drück 2: du ziehst den Zweihänder (Schild auf den Rücken). Er trifft härter, blockt aber nur 90°.", Done = t => t.SwitchedToTwoHand() });
            steps.Add(new Step { Title = "Sprung-Hieb", Text = "Mit dem Zweihänder: E aus 2–3 m Abstand. Du springst heran, dein Körpertempo verstärkt den Hieb.", Done = t => t.HeroSpecialAttack() });
            steps.Add(new Step { Title = "Besiege den Gegner", Text = "Alles zusammen: Leben zeigt die Haltung, Ausdauer der Bildrand. Besieg ihn.", Done = t => t.EnemiesDead() });
        }

        void Update()
        {
            if (!Active || runner.World == null) return;
            if (index >= steps.Count) return;
            bool heroDead = runner.PlayerAgent >= 0 && runner.World.Combat[runner.PlayerAgent].State == CombatState.Dead;
            if (heroDead || (index < steps.Count - 1 && EnemiesDead()))
            {
                // Keep the step, start the duel again
                runner.Restart();
                return;
            }
            if (doneUntil >= 0f)
            {
                if (Time.unscaledTime >= doneUntil) { doneUntil = -1f; index++; }
                return;
            }
            if (skip.WasPressedThisFrame() || steps[index].Done(this)) doneUntil = Time.unscaledTime + doneFlashSeconds;
        }

        // ---- Step conditions ----

        bool HeroMoved(float metres) => runner.PlayerAgent >= 0 && math.distance(runner.World.Agents[runner.PlayerAgent].Position, startPos) > metres;

        bool HeroEvent(int type, bool asTarget)
        {
            int hero = runner.PlayerAgent;
            foreach (var e in runner.FrameEvents)
                if (e.Type == type && (asTarget ? e.Target == hero : e.Attacker == hero)) return true;
            return false;
        }

        bool HeroAttacked(int direction)
        {
            int hero = runner.PlayerAgent;
            foreach (var e in runner.FrameEvents)
                if (e.Type == CombatEventType.AttackStart && e.Attacker == hero && e.Direction == direction) return true;
            return false;
        }

        bool HeroWindup(int direction)
        {
            var c = runner.World.Combat[runner.PlayerAgent];
            return c.State == CombatState.Windup && c.Direction == direction;
        }

        bool SprintHit()
        {
            if (runner.World.Combat[runner.PlayerAgent].Sprinting == 1) sprintSeen = true;
            return sprintSeen && HeroEvent(CombatEventType.Hit, asTarget: false);
        }

        bool SwitchedToTwoHand()
        {
            var c = runner.World.Combat[runner.PlayerAgent];
            var setup = runner.World.CombatSetup;
            if (setup.Weapons[c.WeaponId].Archetype == Sim.Combat.Archetypes.WeaponArchetype.TwoHand) switchedToTwoHand = true;
            return switchedToTwoHand;
        }

        bool HeroSpecialAttack()
        {
            int hero = runner.PlayerAgent;
            foreach (var e in runner.FrameEvents)
                if (e.Type == CombatEventType.SpecialStart && e.Attacker == hero && e.Direction == AttackDirection.Special) return true;
            return false;
        }

        bool EnemiesDead()
        {
            var agents = runner.World.Agents;
            var combat = runner.World.Combat;
            int heroTeam = runner.PlayerAgent >= 0 ? agents[runner.PlayerAgent].Team : 0;
            for (int i = 0; i < agents.Length; i++)
                if (agents[i].Team != heroTeam && combat[i].State != CombatState.Dead) return false;
            return true;
        }

        void OnGUI()
        {
            if (!Active || PauseMenu.IsOpen || AdminMenu.IsOpen) return;
            var screen = MenuStyle.BeginScaled();
            float w = Mathf.Min(760f, screen.x - 40f);
            var rect = new Rect((screen.x - w) * 0.5f, 28f, w, 0f);
            string text;
            if (index >= steps.Count) text = "Tutorial abgeschlossen. Esc → Hauptmenü, oder F5 für ein neues Duell.";
            else
            {
                var s = steps[index];
                text = doneUntil >= 0f ? $"✓ {s.Title}" : $"Schritt {index + 1}/{steps.Count} · {s.Title}\n{s.Text}";
            }
            var content = new GUIContent(text);
            rect.height = MenuStyle.Hint.CalcHeight(content, w);
            var prev = GUI.color;
            if (doneUntil >= 0f) GUI.color = new Color(0.7f, 1f, 0.7f);
            GUI.Label(rect, content, MenuStyle.Hint);
            GUI.color = prev;
            if (index < steps.Count)
                GUI.Label(new Rect(rect.x, rect.yMax + 2f, w, 20f), "F12 überspringt den Schritt · Esc Pause", MenuStyle.Muted);
        }
    }
}
