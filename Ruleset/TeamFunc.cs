namespace oomtm450PuckMod_Ruleset {
    internal static class TeamFunc {
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
