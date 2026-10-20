using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Morale;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// Morale tuning (CLAUDE.md 3.4): panic field, morale per soldier, rout; converted to the sim's MoraleSettings.
    /// Panic is counted in "panic units" per 1-m cell (a death adds 1 by default); negative panic is confidence.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Morale", fileName = "Morale")]
    public class MoraleDefinition : ScriptableObject
    {
        [Tooltip("Off: soldiers never panic or rout.")]
        public bool enabled = true;

        [Header("Panic field: P += D·ΔP − k·P + sources − sinks")]
        [Tooltip("Field and morale update this often (ms). 50 ms = every third tick.")]
        [Min(17f)] public float fieldIntervalMs = 50f;
        [Tooltip("D: how fast panic spreads to neighbouring cells (m²/s). Too large for the interval is rejected (unstable).")]
        [Min(0f)] public float diffusion = 1.5f;
        [Tooltip("k: share of the panic that fades per second.")]
        [Min(0f)] public float decay = 0.3f;

        [Header("Sources and sinks")]
        [Tooltip("Panic where a soldier of the own side dies.")]
        [Min(0f)] public float deathPanic = 1f;
        [Tooltip("Panic where a soldier is hit from the side or behind.")]
        [Min(0f)] public float flankHitPanic = 0.3f;
        [Tooltip("Front arc (degrees, whole arc): hits from outside it count as from the side or behind.")]
        [Range(0f, 360f)] public float frontArcDeg = 120f;
        [Tooltip("Confidence (negative panic) for the other side where an enemy dies or breaks.")]
        [Min(0f)] public float killCourage = 0.5f;
        [Tooltip("Panic a fleeing soldier spreads around itself per second (rout is contagious).")]
        [Min(0f)] public float routPanicPerSecond = 0.3f;
        [Tooltip("The hero calms the own side within this radius (m)...")]
        [Min(0f)] public float leaderRadius = 8f;
        [Tooltip("...removing this share of the panic there per second.")]
        [Min(0f)] public float leaderCalm = 0.5f;

        [Header("Morale per soldier (1 = steady)")]
        [Tooltip("Morale lost per second per panic unit where the soldier stands (gained per unit of confidence).")]
        [Min(0f)] public float panicDrain = 5f;
        [Tooltip("Factor on that loss while the soldier holds its place in a formation (not when charging or alone).")]
        [Range(0f, 1f)] public float formationSteadiness = 0.7f;
        [Tooltip("Below this panic a soldier recovers...")]
        [Min(0f)] public float calmPanic = 0.02f;
        [Tooltip("...this much morale per second.")]
        [Min(0f)] public float recoveryPerSecond = 0.02f;
        [Tooltip("At or below this morale the soldier routs: leaves its formation, stops fighting, runs from the enemy.")]
        [Range(0f, 0.95f)] public float routThreshold = 0.25f;

        [Header("Flight")]
        [Tooltip("A fleeing soldier runs away from the enemies within this range (m).")]
        [Min(0.5f)] public float fleeSenseRange = 8f;
        [Tooltip("A fleeing soldier this close to the map edge leaves the field (m).")]
        [Min(0f)] public float edgeMargin = 1f;

        public MoraleSettings ToSettings() => new MoraleSettings
        {
            Enabled = enabled ? 1 : 0,
            FieldIntervalTicks = Mathf.Max(1, Mathf.RoundToInt(fieldIntervalMs * SimConstants.TickRate / 1000f)),
            Diffusion = diffusion,
            Decay = decay,
            DeathPanic = deathPanic,
            FlankHitPanic = flankHitPanic,
            // Converted once here (presentation side), so the sim compares cosines and needs no trigonometry
            FlankArcCos = frontArcDeg >= 360f ? -1f : Mathf.Cos(frontArcDeg * 0.5f * Mathf.Deg2Rad),
            KillCourage = killCourage,
            RoutPanicPerSecond = routPanicPerSecond,
            LeaderRadius = leaderRadius,
            LeaderCalm = leaderCalm,
            PanicDrain = panicDrain,
            FormationSteadiness = formationSteadiness,
            CalmPanic = calmPanic,
            Recovery = recoveryPerSecond,
            RoutThreshold = routThreshold,
            FleeSenseRange = fleeSenseRange,
            EdgeMargin = edgeMargin,
        };
    }
}
