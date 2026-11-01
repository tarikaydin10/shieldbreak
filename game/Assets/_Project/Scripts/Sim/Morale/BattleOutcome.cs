using Shieldbreak.Sim.Agents;
using Unity.Collections;

namespace Shieldbreak.Sim.Morale
{
    public enum BattleResult : byte
    {
        Undecided = 0,
        Victory = 1,
        Defeat = 2,
    }

    /// <summary>
    /// When a battle is decided (CLAUDE.md 3.8: rout OR the commander dead), seen from one team. A side is beaten when
    /// none of its soldiers still fights (all dead, routing or fled); routed soldiers never come back in v1, so a
    /// decided battle stays decided. Read-only over the agent array: the same answer on every client.
    /// </summary>
    public static class BattleOutcome
    {
        /// <param name="team">The side asking (the player's team).</param>
        /// <param name="commander">Its commander (the hero), -1 = none; if he falls, the battle is lost.</param>
        public static BattleResult Evaluate(NativeArray<AgentData>.ReadOnly agents, int team, int commander)
        {
            if (commander >= 0 && commander < agents.Length && agents[commander].IsDead) return BattleResult.Defeat;
            bool own = false, enemy = false;
            for (int i = 0; i < agents.Length; i++)
            {
                var a = agents[i];
                if (a.IsDead || a.IsRouting) continue;
                if (a.Team == team) own = true;
                else enemy = true;
            }
            if (!own) return BattleResult.Defeat;
            return enemy ? BattleResult.Undecided : BattleResult.Victory;
        }
    }
}
