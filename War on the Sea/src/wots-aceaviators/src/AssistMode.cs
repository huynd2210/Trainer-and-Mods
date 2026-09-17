namespace WoTSAceAviators
{
    /// <summary>
    /// How much help player strike aircraft get. Both modes remove the game's random aim
    /// error; they differ in whether the ordnance is steered after it leaves the rack.
    /// </summary>
    public enum AssistMode
    {
        /// <summary>
        /// Remove the random aim error and stop there. The run is aimed true and the ordnance
        /// then flies on the game's own physics, so a target that manoeuvres after the release
        /// can still get out from under it. Duds still happen.
        /// </summary>
        AimOnly = 0,

        /// <summary>
        /// Also steer released ordnance onto the target for the rest of its flight, and
        /// suppress the dud roll on a round that lands. Nothing gets away.
        /// </summary>
        AimAndGuidance = 1
    }
}
