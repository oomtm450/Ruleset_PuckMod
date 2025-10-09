using Codebase.Configs;

namespace oomtm450PuckMod_Stats.Configs {
    /// <summary>
    /// Class containing the old configuration from oomtm450_stats_serverconfig.json used for this mod.
    /// </summary>
    public class OldServerConfig : ISubConfig {
        #region Properties
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; } = true;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie save crease system.
        /// </summary>
        public float GoalieRadius { get; } = 0.7f; // TODO : Change next release.

        /// <summary>
        /// Float, delta of the puck Z direction to use with the goalie save crease system.
        /// </summary>
        public float GoalieSaveCreaseSystemZDelta { get; } = 0.0125f;

        /// <summary>
        /// Bool, true if the end of game stats JSON must be saved as file on the server.
        /// </summary>
        public bool SaveEOGJSON { get; } = true;
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
        #endregion
    }
}
