using System.Collections.Generic;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using UnityEngine;

namespace Shieldbreak.Presentation.Feedback
{
    /// <summary>
    /// Combat sounds (game feel spec): a windup cue per direction so attacks can be heard coming, and three layers per
    /// hit (whoosh at the release, material impact, grunt). Also block, perfect block, guard break, death, step and
    /// weapon draw. Placeholder sounds from ProceduralSfx, 3D at the fighters. Presentation only.
    /// </summary>
    public class CombatAudio : MonoBehaviour, Admin.IAdminStatus
    {
        public SimRunner runner;
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0f, 1f)] public float cueVolume = 0.35f;
        [Min(4)] public int voices = 24;
        [Tooltip("Random pitch spread per sound (±).")]
        [Range(0f, 0.3f)] public float pitchSpread = 0.08f;

        AudioClip whoosh, whooshHeavy, flesh, wood, steel, grunt, gruntDeath, step, draw;
        readonly AudioClip[] cues = new AudioClip[AttackDirection.Count + 1];
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly List<int> previousState = new List<int>();
        int nextVoice, played;

        public string AdminStatusLabel => "Sound";
        public string AdminStatus => $"Platzhalter-Sounds (synthetisch), Lautstärke {volume:0.00}, {played} gespielt";

        void Awake()
        {
            whoosh = ProceduralSfx.Whoosh(0.24f, 1000f);
            whooshHeavy = ProceduralSfx.Whoosh(0.32f, 650f);
            flesh = ProceduralSfx.Flesh();
            wood = ProceduralSfx.Wood();
            steel = ProceduralSfx.Steel();
            grunt = ProceduralSfx.Grunt();
            gruntDeath = ProceduralSfx.Grunt(95f, 0.55f, 59);
            step = ProceduralSfx.Step();
            draw = ProceduralSfx.Draw();
            // Distinct windup cues: overhead rises low, side cuts glide in opposite directions, thrust is a short high tick
            cues[AttackDirection.Overhead] = ProceduralSfx.Cue("cue_overhead", 180f, 260f, 0.3f);
            cues[AttackDirection.Left] = ProceduralSfx.Cue("cue_left", 340f, 250f, 0.25f);
            cues[AttackDirection.Right] = ProceduralSfx.Cue("cue_right", 250f, 340f, 0.25f);
            cues[AttackDirection.Thrust] = ProceduralSfx.Cue("cue_thrust", 460f, 480f, 0.12f);
            cues[AttackDirection.Special] = ProceduralSfx.Cue("cue_special", 140f, 300f, 0.4f);

            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Voice");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2f;
                src.maxDistance = 45f;
                src.dopplerLevel = 0f;
                pool.Add(src);
            }
        }

        void Start()
        {
            // The scene builder makes a bare camera; sounds need exactly one listener
            if (FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                (cam != null ? cam.gameObject : gameObject).AddComponent<AudioListener>();
            }
        }

        void LateUpdate()
        {
            var world = runner != null ? runner.World : null;
            if (world == null || !world.HasCombat) return;
            var agents = world.Agents;
            var combat = world.Combat;
            var setup = world.CombatSetup;
            while (previousState.Count < agents.Length) previousState.Add(CombatState.Idle);

            // Whoosh when a swing enters its release (heavier and lower for the two-hander)
            for (int i = 0; i < agents.Length; i++)
            {
                int state = combat[i].State;
                if (state == CombatState.Release && previousState[i] != CombatState.Release)
                {
                    bool heavy = setup.Weapons[combat[i].WeaponId].Archetype == WeaponArchetype.TwoHand;
                    Play(heavy ? whooshHeavy : whoosh, At(i), 0.7f, combat[i].Direction == AttackDirection.Thrust ? 1.3f : 1f);
                }
                previousState[i] = state;
            }

            foreach (var e in runner.FrameEvents)
            {
                int who = e.Target >= 0 ? e.Target : e.Attacker;
                if (who < 0 || who >= agents.Length) continue;
                Vector3 at = e.Target >= 0 ? new Vector3(e.Point.x, e.Point.y, e.Point.z) : At(who);
                if (e.Target >= 0 && e.Point.y <= 0f) at = At(who) + Vector3.up * 1.2f;
                bool shield = setup.Weapons[combat[who].WeaponId].Archetype == WeaponArchetype.OneHandShield;
                float loud = Mathf.Clamp(e.Energy / 500f, 0.5f, 1.3f);
                switch (e.Type)
                {
                    case CombatEventType.AttackStart:
                        Play(cues[Mathf.Clamp(e.Direction, 0, AttackDirection.Count - 1)], At(e.Attacker), cueVolume, 1f);
                        break;
                    case CombatEventType.SpecialStart:
                        Play(cues[AttackDirection.Special], At(e.Attacker), cueVolume, 1f);
                        break;
                    case CombatEventType.Hit:
                        if (e.Damage > 0f)
                        {
                            Play(flesh, at, loud, 1f);
                            Play(grunt, At(e.Target) + Vector3.up * 1.6f, 0.6f, 1f);
                        }
                        else Play(steel, at, 0.4f, 1.4f);
                        break;
                    case CombatEventType.Blocked:
                        Play(shield ? wood : steel, at, loud * 0.8f, 1f);
                        break;
                    case CombatEventType.PerfectBlock:
                        Play(steel, at, 1f, 1.15f);
                        if (shield) Play(wood, at, 0.7f, 1.1f);
                        break;
                    case CombatEventType.GuardBreak:
                        Play(wood, at, 1.2f, 0.7f);
                        Play(grunt, At(who) + Vector3.up * 1.6f, 0.7f, 0.9f);
                        break;
                    case CombatEventType.Death:
                        Play(gruntDeath, At(e.Target) + Vector3.up * 1.4f, 0.9f, 1f);
                        break;
                    case CombatEventType.Step:
                        Play(step, At(e.Attacker), 0.6f, 1f);
                        break;
                    case CombatEventType.WeaponSwitch:
                        Play(draw, At(e.Attacker) + Vector3.up * 1.1f, 0.6f, 1f);
                        break;
                }
            }
        }

        Vector3 At(int agent) => SimRunner.ToWorld(runner.InterpolatedPosition(agent), 1.3f);

        void Play(AudioClip clip, Vector3 position, float gain, float pitch)
        {
            if (clip == null || volume <= 0f) return;
            var src = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Count;
            src.transform.position = position;
            src.clip = clip;
            src.volume = Mathf.Clamp01(gain * volume);
            src.pitch = pitch * (1f + Random.Range(-pitchSpread, pitchSpread));
            src.Play();
            played++;
        }
    }
}
