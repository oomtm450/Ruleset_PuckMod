﻿using System.Linq;

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
        #endregion
    }
}
