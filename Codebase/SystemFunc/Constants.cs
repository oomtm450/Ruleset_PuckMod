namespace Codebase {
    internal static class Constants {
        /// <summary>
        /// Const string, prefix of all the mod's names.
        /// </summary>
        private const string MODS_PREFIX = "oomtm450_";

        /// <summary>
        /// Const string, name of the Stats mod.
        /// </summary>
        internal const string STATS_MOD_NAME = MODS_PREFIX + "stats";

        /// <summary>
        /// Const string, name of the Ruleset mod.
        /// </summary>
        internal const string RULESET_MOD_NAME = MODS_PREFIX + "ruleset";

        /// <summary>
        /// Const string, name of the Sounds mod.
        /// </summary>
        internal const string SOUNDS_MOD_NAME = MODS_PREFIX + "sounds";

        /// <summary>
        /// Const string, used for the communication from the server for the Sounds mod.
        /// </summary>
        internal const string SOUNDS_FROM_SERVER_TO_CLIENT = SOUNDS_MOD_NAME + "_server";

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

        /// <summary>
        /// Const string, data name for blocked shots.
        /// </summary>
        public const string BLOCK = STATS_MOD_NAME + "BLOCK";

        /// <summary>
        /// Const string, data name for passes.
        /// </summary>
        public const string PASS = STATS_MOD_NAME + "PASS";

        /// <summary>
        /// Const string, data name for pausing mods.
        /// </summary>
        public const string PAUSE = "pause";

        /// <summary>
        /// Const string, data name for enabling or disabling the logic in mods.
        /// </summary>
        public const string LOGIC = "logic";

        /// <summary>
        /// Const string, data name for telling mods that Ruleset changed phase manually.
        /// </summary>
        public const string CHANGED_PHASE = "chphase";
    }
}
