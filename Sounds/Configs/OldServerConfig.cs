using Codebase.Configs;

namespace oomtm450PuckMod_Sounds.Configs {
    /// <summary>
    /// Class containing the old configuration from oomtm450_sounds_serverconfig.json used for this mod.
    /// </summary>
    public class OldServerConfig : ISubConfig {
        #region Properties
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; } = true;

        /// <summary>
        /// Bool, true if music is enabled.
        /// </summary>
        public bool EnableMusic { get; } = true;
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
