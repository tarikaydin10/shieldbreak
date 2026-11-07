using Unity.Mathematics;

namespace Shieldbreak.Sim.Morale
{
    /// <summary>
    /// The psychology of a battle in two pure rules, so the morale job and the tests use the same ones. A side that
    /// has bled far more than its enemy loses heart; a soldier at the end of his courage breaks by chance, so they go
    /// one by one and not all in the same moment (Bannerlord: a panic chance, and a chain reaction among those near).
    /// </summary>
    public static class MoraleRules
    {
        /// <summary>
        /// Morale a soldier loses per second because his side lost a larger share of its men (<paramref name="ownLost"/>,
        /// 0 to 1, fallen or fled) than the enemy did. Losses alike on both sides cost nothing.
        /// </summary>
        public static float LossGapDrain(float ownLost, float enemyLost, in MoraleSettings s) =>
            s.LossGapDrain * math.max(0f, ownLost - enemyLost - s.LossGapTolerance);

        /// <summary>Chance that a soldier with this morale breaks within <paramref name="dt"/> seconds: none above the threshold, more the deeper below it.</summary>
        public static float BreakChance(float morale, in MoraleSettings s, float dt)
        {
            if (morale > s.RoutThreshold) return 0f;
            float depth = s.RoutThreshold > 0f ? 1f - morale / s.RoutThreshold : 1f;
            return math.saturate(s.BreakChancePerSecond * (0.25f + 0.75f * depth) * dt);
        }
    }
}
