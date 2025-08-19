using UnityEngine;
using UnityEngine.Rendering;

namespace Codebase {
    /// <summary>
    /// Class containing code for server functions.
    /// </summary>
    public static class ServerFunc {
        /// <summary>
        /// Function that returns true if the instance is a dedicated server.
        /// </summary>
        /// <returns>Bool, true if this is a dedicated server.</returns>
        public static bool IsDedicatedServer() {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }
    }
}
