namespace Codebase {
    internal static class Constants {
        /// <summary>
        /// Const string, name of the stats mod.
        /// </summary>
        internal const string STATS_MOD_NAME = "oomtm450_stats";

        /// <summary>
        /// Const string, used for the communication from the client.
        /// </summary>
        internal const string FROM_CLIENT_TO_STATS_SERVER = STATS_MOD_NAME + "_from_anothermod_client";

        /// <summary>
        /// Const float, radius of the puck.
        /// </summary>
        internal const float PUCK_RADIUS = 0.13f;

        /// <summary>
        /// Const float, radius of a player.
        /// </summary>
        internal const float PLAYER_RADIUS = 0.2625f;

        /// <summary>
        /// Const float, height of the net's crossbar.
        /// </summary>
        internal const float CROSSBAR_HEIGHT = 1.8f;

        /// <summary>
        /// Const string, data name for SOG.
        /// </summary>
        public const string SOG = STATS_MOD_NAME + "SOG";

        /// <summary>
        /// Const string, data name for the save percentage.
        /// </summary>
        public const string SAVEPERC = STATS_MOD_NAME + "SAVEPERC";
    }
}
