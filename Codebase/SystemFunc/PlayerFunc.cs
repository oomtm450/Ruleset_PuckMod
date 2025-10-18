using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Codebase {
    /// <summary>
    /// Class containing code for player functions.
    /// </summary>
    public class PlayerFunc {
        #region Constants
        /// <summary>
        /// Const string, position name for the goalie.
        /// </summary>
        public const string GOALIE_POSITION = "G";
        /// <summary>
        /// Const string, position name for the left winger.
        /// </summary>
        public const string LEFT_WINGER_POSITION = "LW";
        /// <summary>
        /// Const string, position name for the center.
        /// </summary>
        public const string CENTER_POSITION = "C";
        /// <summary>
        /// Const string, position name for the right winger.
        /// </summary>
        public const string RIGHT_WINGER_POSITION = "RW";
        /// <summary>
        /// Const string, position name for the left defender.
        /// </summary>
        public const string LEFT_DEFENDER_POSITION = "LD";
        /// <summary>
        /// Const string, position name for the right defender.
        /// </summary>
        public const string RIGHT_DEFENDER_POSITION = "RD";
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Function that checks if a player is on the ice playing.
        /// </summary>
        /// <param name="player">Player, player to check.</param>
        /// <returns>Bool, is player playing or not.</returns>
        public static bool IsPlayerPlaying(Player player) {
            return !(!player || player.Role.Value == PlayerRole.None || !player.IsCharacterFullySpawned);
        }

        /// <summary>
        /// Function that finds a team's goalie.
        /// </summary>
        /// <param name="team">PlayerTeam, team of the goalie.</param>
        /// <returns>Player, goalie found or null.</returns>
        public static Player GetTeamGoalie(PlayerTeam team) {
            return PlayerManager.Instance.GetPlayersByTeam(team).FirstOrDefault(x => x.Role.Value == PlayerRole.Goalie);
        }

        /// <summary>
        /// Function that finds the other team's goalie.
        /// </summary>
        /// <param name="team">PlayerTeam, opposing team of the goalie.</param>
        /// <returns>Player, goalie found or null.</returns>
        public static Player GetOtherTeamGoalie(PlayerTeam team) {
            return PlayerManager.Instance.GetPlayersByTeam(TeamFunc.GetOtherTeam(team)).FirstOrDefault(x => x.Role.Value == PlayerRole.Goalie);
        }

        /// <summary>
        /// Function that returns true if the PlayerPosition has the given role, and if it is claimed depending on hasToBeClaimed.
        /// </summary>
        /// <param name="pPosition">PlayerPosition, object to check.</param>
        /// <param name="role">PlayerRole, role to check for in the PlayerPosition.</param>
        /// <param name="hasToBeClaimed">Bool, true if the PlayerPosition has to be claimed.</param>
        /// <returns>Bool, true if the PlayerPosition has the given PlayerRole, and is claimed or not depending of hasToBeClaimed.</returns>
        public static bool IsRole(PlayerPosition pPosition, PlayerRole role, bool hasToBeClaimed = true) {
            bool output = pPosition.Role == role;
            if (hasToBeClaimed)
                return output && pPosition.IsClaimed;

            return output;
        }

        /// <summary>
        /// Function that returns true if the PlayerPosition is an attacker (skater), and if it is claimed depending on hasToBeClaimed.
        /// </summary>
        /// <param name="pPosition">PlayerPosition, object to check.</param>
        /// <param name="hasToBeClaimed">Bool, true if the PlayerPosition has to be claimed.</param>
        /// <returns>Bool, true if the PlayerPosition is an attacker (skater), and is claimed or not depending of hasToBeClaimed.</returns>
        public static bool IsAttacker(PlayerPosition pPosition, bool hasToBeClaimed = true) {
            return IsRole(pPosition, PlayerRole.Attacker, hasToBeClaimed);
        }

        /// <summary>
        /// Function that returns true if the PlayerPosition is a goalie, and if it is claimed depending on hasToBeClaimed.
        /// </summary>
        /// <param name="pPosition">PlayerPosition, object to check.</param>
        /// <param name="hasToBeClaimed">Bool, true if the PlayerPosition has to be claimed.</param>
        /// <returns>Bool, true if the PlayerPosition is a goalie, and is claimed or not depending of hasToBeClaimed.</returns>
        public static bool IsGoalie(PlayerPosition pPosition, bool hasToBeClaimed = true) {
            return IsRole(pPosition, PlayerRole.Goalie, hasToBeClaimed);
        }

        /// <summary>
        /// Function that returns true if the player is a goalie.
        /// </summary>
        /// <param name="player">Player, player to check.</param>
        /// <returns>Bool, true if the player is a goalie.</returns>
        public static bool IsGoalie(Player player) {
            return player.Role.Value == PlayerRole.Goalie;
        }

        /// <summary>
        /// Function that returns the player steam Id that has possession.
        /// </summary>
        /// <param name="checkForChallenge">Bool, false if we only check logic without challenging puck possession from other players to determine possession.</param>
        /// <returns>String, player steam Id with the possession or an empty string if no one has the puck (or it is challenged).</returns>
        public static string GetPlayerSteamIdInPossession(int minPossessionMilliseconds, int maxPossessionMilliseconds, int maxTippedMilliseconds,
            LockDictionary<string, Stopwatch> playersLastTimePuckPossession, LockDictionary<string, Stopwatch> playersCurrentPuckTouch, bool checkForChallenge = true) {
            Dictionary<string, Stopwatch> dict;
            dict = playersLastTimePuckPossession
                .Where(x => x.Value.ElapsedMilliseconds < minPossessionMilliseconds &&
                    playersCurrentPuckTouch.Keys.Any(y => y == x.Key) &&
                    playersCurrentPuckTouch[x.Key].ElapsedMilliseconds > maxTippedMilliseconds)
                .ToDictionary(x => x.Key, x => x.Value);

            /*if (!checkForChallenge && playersCurrentPuckTouch.Count != 0) {
                Logging.Log($"playersCurrentPuckTouch milliseconds : {playersCurrentPuckTouch.First().Value.ElapsedMilliseconds}", _serverConfig, true);
            }*/

            /*if (!checkForChallenge) {
                Logging.Log($"Number of possession found : {dict.Count}", _serverConfig, true);
                if (playersLastTimePuckPossession.Count != 0)
                    Logging.Log($"Possession milliseconds : {playersLastTimePuckPossession.First().Value.ElapsedMilliseconds}", _serverConfig, true);
            }*/

            if (dict.Count > 1) { // Puck possession is challenged.
                if (checkForChallenge)
                    return "";
                else
                    return dict.OrderBy(x => x.Value.ElapsedMilliseconds).First().Key;
            }

            if (dict.Count == 1)
                return dict.First().Key;

            List<string> steamIds = playersLastTimePuckPossession
                .Where(x => x.Value.ElapsedMilliseconds < maxPossessionMilliseconds ||
                    (playersCurrentPuckTouch.Keys.Any(y => y == x.Key) &&
                    playersCurrentPuckTouch[x.Key].ElapsedMilliseconds > maxTippedMilliseconds))
                .OrderBy(x => x.Value.ElapsedMilliseconds)
                .Select(x => x.Key).ToList();

            /*if (!checkForChallenge && steamIds.Count != 0) {
                Logging.Log($"Possession {steamIds.First()}.", _serverConfig, true);
            }
            else if (!checkForChallenge) {
                Logging.Log($"No extra possession.", _serverConfig, true);
            }*/

            if (steamIds.Count != 0)
                return steamIds.First();

            return "";
        }
        #endregion
    }
}
