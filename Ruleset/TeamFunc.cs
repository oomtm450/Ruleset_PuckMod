namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing code for team functions.
    /// </summary>
    internal static class TeamFunc {
        /// <summary>
        /// Const PlayerTeam, default team for initializing variables.
        /// </summary>
        internal const PlayerTeam DEFAULT_TEAM = PlayerTeam.Blue;

        /// <summary>
        /// Function that returns the opposite team.
        /// </summary>
        /// <param name="team">PlayerTeam, team to get the opposite from.</param>
        /// <returns>PlayerTeam, opposite team.</returns>
        internal static PlayerTeam GetOtherTeam(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return PlayerTeam.Red;
            if (team == PlayerTeam.Red)
                return PlayerTeam.Blue;

            return PlayerTeam.None;
        }
    }
}
