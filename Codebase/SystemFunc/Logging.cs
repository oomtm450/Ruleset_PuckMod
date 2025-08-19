using Codebase.Configs;
using UnityEngine;

namespace Codebase {
    /// <summary>
    /// Class containing code for logging.
    /// </summary>
    public static class Logging {
        /// <summary>
        /// Function that logs information to the debug console.
        /// </summary>
        /// <param name="msg">String, message to log.</param>
        /// <param name="config">IConfig, config to use to check if info must be logged.</param>
        /// <param name="bypassConfig">Bool, true to bypass the logs config. False by default.</param>
        public static void Log(string msg, IConfig config, bool bypassConfig = false) {
            if (bypassConfig || config == null || config.LogInfo)
                Debug.Log($"[{config.ModName}] {msg}");
        }

        /// <summary>
        /// Function that logs errors to the debug console.
        /// </summary>
        /// <param name="msg">String, message to log.</param>
        /// <param name="config">IConfig, config to use.</param>
        public static void LogError(string msg, IConfig config) {
            Debug.LogError($"[{config.ModName}] {msg}");
        }

        /// <summary>
        /// Function that logs warnings to the debug console.
        /// </summary>
        /// <param name="msg">String, message to log.</param>
        /// <param name="config">IConfig, config to use to check if info must be logged.</param>
        /// <param name="bypassConfig">Bool, true to bypass the logs config. False by default.</param>
        public static void LogWarning(string msg, IConfig config, bool bypassConfig = false) {
            if (bypassConfig || config == null || config.LogInfo)
                Debug.LogWarning($"[{config.ModName}] {msg}");
        }
    }
}
