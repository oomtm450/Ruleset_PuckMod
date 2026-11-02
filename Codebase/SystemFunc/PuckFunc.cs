using System.Diagnostics;

namespace Codebase {
    /// <summary>
    /// Class containing code for puck functions.
    /// </summary>
    public class PuckFunc {
        public static bool PuckIsTipped(string playerSteamId, int maxTippedMilliseconds, LockDictionary<string, Stopwatch> playersCurrentPuckTouch,
            LockDictionary<string, Stopwatch> lastTimeOnCollisionStayOrExitWasCalled, float puckSpeed, float puckSpeedRatio, float puckSpeedOnEnter,
            bool playerIsGoalie = false, float puckY = 0, float puckYThresholdGoalie = 0) {
            if (playerIsGoalie && puckY > puckYThresholdGoalie)
                return true;

            if (puckSpeedOnEnter >= puckSpeed)
                return true;

            if (!playersCurrentPuckTouch.TryGetValue(playerSteamId, out Stopwatch currentPuckTouchWatch))
                return true;

            if (!lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(playerSteamId, out Stopwatch lastPuckExitWatch))
                return true;

            if (currentPuckTouchWatch.ElapsedMilliseconds - lastPuckExitWatch.ElapsedMilliseconds < maxTippedMilliseconds / (puckSpeed * puckSpeedRatio))
                return true;

            return false;
        }
    }
}
