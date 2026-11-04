namespace SSRP.Core
{
    /// <summary>
    /// The states of the match flow state machine (see GameManager in SSRP.Flow).
    /// Carried by <see cref="GameEvents.MatchStateChanged"/> so listeners know
    /// which state was entered without referencing the GameManager.
    /// </summary>
    public enum MatchPhase
    {
        Menu,
        Calibration,
        Countdown,
        Serving,
        Playing,
        PointScored,
        Paused,
        MatchOver
    }
}
