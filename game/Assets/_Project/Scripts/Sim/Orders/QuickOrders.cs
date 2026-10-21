using Unity.Collections;
using Shieldbreak.Sim.Formations;

namespace Shieldbreak.Sim.Orders
{
    /// <summary>The quick commands of the radial menu (CLAUDE.md 3.9): behaviors and the shapes used most in melee.</summary>
    public enum QuickCommand
    {
        Hold = 0,
        Advance = 1,
        Charge = 2,
        Follow = 3,
        Retreat = 4,
        Line = 5,
        ShieldWall = 6,
        Wedge = 7,
    }

    /// <summary>
    /// Quick orders without a map position (radial menu, later hotkeys): which formations a player commands and the
    /// FormationOrder each quick command becomes. Pure and deterministic; the orders go through the InputQueue like
    /// every other player order.
    /// </summary>
    public static class QuickOrders
    {
        public const int CommandCount = 8;

        /// <summary>
        /// Writes the ids of the formations <paramref name="owner"/> commands (own, not empty) into
        /// <paramref name="output"/> in ascending order; returns how many.
        /// </summary>
        public static int Commanded(NativeArray<FormationData>.ReadOnly formations, int owner, NativeArray<int> output)
        {
            int n = 0;
            for (int id = 0; id < formations.Length && n < output.Length; id++)
            {
                var f = formations[id];
                if (f.Owner == owner && f.MemberCount > 0) output[n++] = id;
            }
            return n;
        }

        /// <summary>Follow needs someone to follow; everything else always works.</summary>
        public static bool IsAvailable(QuickCommand command, int heroAgent) => command != QuickCommand.Follow || heroAgent >= 0;

        /// <summary>The order a quick command gives one formation. Follow follows <paramref name="heroAgent"/>.</summary>
        public static bool TryBuild(QuickCommand command, int formation, int heroAgent, out FormationOrder order)
        {
            switch (command)
            {
                case QuickCommand.Hold: order = FormationOrder.Hold(formation); return true;
                case QuickCommand.Advance: order = FormationOrder.Advance(formation); return true;
                case QuickCommand.Charge: order = FormationOrder.Charge(formation); return true;
                case QuickCommand.Retreat: order = FormationOrder.Retreat(formation); return true;
                case QuickCommand.Line: order = FormationOrder.Reshape(formation, FormationType.Line); return true;
                case QuickCommand.ShieldWall: order = FormationOrder.Reshape(formation, FormationType.ShieldWall); return true;
                case QuickCommand.Wedge: order = FormationOrder.Reshape(formation, FormationType.Wedge); return true;
                case QuickCommand.Follow when heroAgent >= 0: order = FormationOrder.Follow(formation, heroAgent); return true;
                default: order = default; return false;
            }
        }
    }
}
