using UnityEngine;
using UnityEngine.Rendering;

namespace oomtm450PuckMod_Template.SystemFunc {
    internal class ServerFunc {
        /// <summary>
        /// Function that returns true if the instance is a dedicated server.
        /// </summary>
        /// <returns>Bool, true if this is a dedicated server.</returns>
        internal static bool IsDedicatedServer() {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }
    }
}
