namespace Codebase.Configs {
    /// <summary>
    /// Class containing the interface for configurations.
    /// </summary>
    public interface IConfig {
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        bool LogInfo { get; set; }

        /// <summary>
        /// String, name of the mod.
        /// </summary>
        string ModName { get; }
    }
}
