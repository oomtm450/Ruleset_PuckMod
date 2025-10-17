using System.Diagnostics;

namespace Codebase {
    /// <summary>
    /// Class containing code for puck functions.
    /// </summary>
    public class PuckFunc {
        public static bool PuckIsTipped(string playerSteamId, int maxTippedMilliseconds, LockDictionary<string, Stopwatch> playersCurrentPuckTouch,
            LockDictionary<string, Stopwatch> lastTimeOnCollisionExitWasCalled) {
            if (!playersCurrentPuckTouch.TryGetValue(playerSteamId, out Stopwatch currentPuckTouchWatch))
                return false;

            if (!lastTimeOnCollisionExitWasCalled.TryGetValue(playerSteamId, out Stopwatch lastPuckExitWatch))
                return false;

            if (currentPuckTouchWatch.ElapsedMilliseconds - lastPuckExitWatch.ElapsedMilliseconds < maxTippedMilliseconds)
                return true;

            return false;
        }
    }
}
