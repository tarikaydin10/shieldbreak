using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Data
{
    /// <summary>
    /// A ScriptableObject that sets up a SimWorld (map, spawns, combat tables). The SimRunner holds one and can
    /// rebuild the world from it at any time (restart, preset change).
    /// </summary>
    public interface ISimScenario
    {
        /// <param name="playerAgent">Agent controlled by local player 0, or -1 if none.</param>
        SimWorld CreateWorld(AgentMoveSettings movement, out int playerAgent);
    }
}
