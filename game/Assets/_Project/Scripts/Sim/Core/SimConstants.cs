namespace Shieldbreak.Sim.Core
{
    /// <summary>
    /// Fixed simulation timing. DT never changes at runtime; slow motion is done by running fewer ticks
    /// per real-time second in the presentation layer.
    /// </summary>
    public static class SimConstants
    {
        public const int TickRate = 60;
        public const float DT = 1f / TickRate;

        /// <summary>Upper bound for player ids; sizes per-player bookkeeping arrays.</summary>
        public const int MaxPlayers = 8;

        /// <summary>Edge length of a WorldGrid cell in meters (navigation, spatial hash, morale field).</summary>
        public const float CellSize = 1f;
    }
}
