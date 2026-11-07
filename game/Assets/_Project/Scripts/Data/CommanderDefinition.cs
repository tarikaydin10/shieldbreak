using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Core;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// Commander AI tuning (CLAUDE.md 3.7): how often it thinks, how its tactics score, what it may do; converted to
    /// the sim's CommanderSettings. Difficulty lives here as reaction time, noise and allowed tactics, never as stats.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Commander", fileName = "Commander")]
    public class CommanderDefinition : ScriptableObject
    {
        [Header("Thinking")]
        [Tooltip("The commander looks at the field this often (ms).")]
        [Min(17f)] public float decisionIntervalMs = 2500f;
        [Tooltip("A tactic stays at least this long, unless the commander retreats (ms).")]
        [Min(0f)] public float minTacticMs = 8000f;
        [Tooltip("Bonus for the current tactic: a close rival does not change the plan.")]
        [Min(0f)] public float hysteresis = 0.15f;
        [Tooltip("Random addition per score and decision: a commander who misjudges.")]
        [Min(0f)] public float scoreNoise = 0.1f;
        [Tooltip("Off: this commander never sends a formation round the enemy (easier).")]
        public bool allowFlank = true;

        [Header("Hold")]
        public float holdBase = 0.35f;
        [Tooltip("Taken off per unit of strength advantage (1 = twice the enemy): the weaker side prefers to stand.")]
        public float holdPerAdvantage = 0.6f;
        [Tooltip("Taken off holding and retreating per minute since the last fight: nobody stands opposite the enemy for ever.")]
        [Min(0f)] public float holdImpatiencePerMinute = 0.6f;
        [Tooltip("Attacking closer than this counts as a fight: it ends the impatience (m).")]
        [Min(0f)] public float contactDistance = 6f;

        [Header("Frontal attack")]
        public float frontalBase = 0.5f;
        public float frontalPerAdvantage = 0.5f;
        [Tooltip("Per unit of own morale above the enemy's.")]
        public float frontalPerMorale = 0.5f;

        [Header("Flank")]
        public float flankBase = 0.45f;
        public float flankPerAdvantage = 0.3f;
        [Tooltip("Per unit of own front wider than the enemy's.")]
        public float flankPerWidth = 0.2f;
        [Tooltip("Per unit of how empty that side of the enemy is.")]
        public float flankPerOpenSide = 0.4f;
        [Tooltip("Closer than this to the enemy there is no room left to go round him (m).")]
        [Min(0f)] public float flankMinDistance = 15f;
        [Tooltip("The flanking formation stops this far beyond the end of the enemy's line (m).")]
        [Min(0f)] public float flankMargin = 6f;
        [Tooltip("The flanking formation is sent on anew when its goal moved further than this (m).")]
        [Min(0f)] public float flankRetargetDistance = 4f;

        [Header("Retreat")]
        [Tooltip("Below this forecast balance (own fighting power per enemy's) the commander thinks of retreat.")]
        [Min(0f)] public float retreatRatio = 0.5f;
        public float retreatPerWeakness = 4f;
        [Tooltip("Below this average morale (0–1) the commander thinks of retreat.")]
        [Range(0f, 1f)] public float retreatMorale = 0.35f;
        public float retreatPerPanic = 3f;

        [Header("Forecast (Lanchester, melee)")]
        [Tooltip("A hero is worth this many line soldiers (3 to 5).")]
        [Min(0.1f)] public float heroStrength = 4f;
        [Tooltip("What one bot level of the own soldiers is worth, as a factor (level 3 = 1). Enemy soldiers count as average.")]
        [Min(0.1f)] public float strengthPerLevel = 1.3f;
        [Tooltip("How much morale counts: 1 = a soldier is worth his morale, 0 = morale does not count.")]
        [Range(0f, 1f)] public float moraleWeight = 0.5f;
        [Tooltip("How much of a front overlapping the enemy's counts as contact.")]
        [Range(0f, 1f)] public float flankShare = 0.5f;
        [Tooltip("Soldiers one soldier in contact kills per second: only for the forecast duration.")]
        [Min(0.001f)] public float killRate = 0.06f;

        static int Ticks(float ms) => Mathf.Max(1, Mathf.RoundToInt(ms * SimConstants.TickRate / 1000f));

        public CommanderSettings ToSettings() => new CommanderSettings
        {
            DecisionIntervalTicks = Ticks(decisionIntervalMs),
            MinTacticTicks = minTacticMs <= 0f ? 0 : Ticks(minTacticMs),
            Hysteresis = hysteresis,
            ScoreNoise = scoreNoise,
            AllowFlank = allowFlank ? 1 : 0,
            HoldBase = holdBase,
            HoldPerAdvantage = holdPerAdvantage,
            HoldImpatiencePerTick = holdImpatiencePerMinute / (60f * SimConstants.TickRate),
            ContactDistance = contactDistance,
            FrontalBase = frontalBase,
            FrontalPerAdvantage = frontalPerAdvantage,
            FrontalPerMorale = frontalPerMorale,
            FlankBase = flankBase,
            FlankPerAdvantage = flankPerAdvantage,
            FlankPerWidth = flankPerWidth,
            FlankPerOpenSide = flankPerOpenSide,
            FlankMinDistance = flankMinDistance,
            FlankMargin = flankMargin,
            FlankRetargetDistance = flankRetargetDistance,
            RetreatRatio = retreatRatio,
            RetreatPerWeakness = retreatPerWeakness,
            RetreatMorale = retreatMorale,
            RetreatPerPanic = retreatPerPanic,
            HeroStrength = heroStrength,
            StrengthPerLevel = strengthPerLevel,
            MoraleWeight = moraleWeight,
            FlankShare = flankShare,
            KillRate = killRate,
        };
    }
}
