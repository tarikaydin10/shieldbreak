namespace Shieldbreak.Presentation.Menu
{
    /// <summary>
    /// Whether a result screen (duel or company) is up. Gameplay input, the radial menu, the command view and the
    /// scene briefing step aside and the cursor is free while it is. Set by the scene's director, cleared on disable.
    /// </summary>
    public static class ResultScreen
    {
        public static bool IsOpen { get; internal set; }
    }
}
