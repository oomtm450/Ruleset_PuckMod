using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing code for player functions.
    /// </summary>
    internal class PlayerFunc {
        #region Constants
        /// <summary>
        /// Const string, position name for the goalie.
        /// </summary>
        internal const string GOALIE_POSITION = "G";
        /// <summary>
        /// Const string, position name for the left winger.
        /// </summary>
        internal const string LEFT_WINGER_POSITION = "LW";
        /// <summary>
        /// Const string, position name for the center.
        /// </summary>
        internal const string CENTER_POSITION = "C";
        /// <summary>
        /// Const string, position name for the right winger.
        /// </summary>
        internal const string RIGHT_WINGER_POSITION = "RW";
        /// <summary>
        /// Const string, position name for the left defender.
        /// </summary>
        internal const string LEFT_DEFENDER_POSITION = "LD";
        /// <summary>
        /// Const string, position name for the right defender.
        /// </summary>
        internal const string RIGHT_DEFENDER_POSITION = "RD";
        #endregion

        #region Properties
        /// <summary>
        /// LockDictionary of ulong and string, dictionary of all players
        /// </summary>
        internal static LockDictionary<ulong, string> Players_ClientId_SteamId { get; } = new LockDictionary<ulong, string>();
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Function that checks if a player is on the ice playing.
        /// </summary>
        /// <param name="player">Player, player to check.</param>
        /// <returns>Bool, is player playing or not.</returns>
        internal static bool IsPlayerPlaying(Player player) {
            return !(!player || player.Role.Value == PlayerRole.None || !player.IsCharacterFullySpawned);
        }

        /// <summary>
        /// Method that teleports a player on a faceoff dot using predetermined offsets depending on the player's position.
        /// </summary>
        /// <param name="player">Player, player to teleport.</param>
        /// <param name="faceoffDot">Vector3, position of the faceoff dot.</param>
        /// <param name="faceoffSpot">FaceoffSpot, location of the faceoff.</param>
        internal static void TeleportOnFaceoff(Player player, Vector3 faceoffDot, FaceoffSpot faceoffSpot) {
            if (!IsPlayerPlaying(player) || faceoffSpot == FaceoffSpot.Center)
                return;

            float xOffset = 0, zOffset = 0;
            Quaternion quaternion = player.PlayerBody.Rigidbody.rotation;
            switch (player.PlayerPosition.Name) {
                case CENTER_POSITION:
                    zOffset = 1.5f;
                    break;
                case LEFT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team.Value == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team.Value == PlayerTeam.Blue))
                        xOffset = 6.5f;
                    else
                        xOffset = 9f;
                    break;
                case RIGHT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team.Value == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team.Value == PlayerTeam.Blue))
                        xOffset = -6.5f;
                    else
                        xOffset = -9f;
                    break;
                case LEFT_DEFENDER_POSITION:
                    zOffset = 13.75f;
                    if ((ushort)faceoffSpot >= 5)
                        zOffset -= 1f;

                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team.Value == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team.Value == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = -9f;
                        if (player.Team.Value == PlayerTeam.Red)
                            quaternion = Quaternion.Euler(0, -90, 0);
                        else
                            quaternion = Quaternion.Euler(0, 90, 0);
                    }
                    else
                        xOffset = 4.5f;
                    break;
                case RIGHT_DEFENDER_POSITION:
                    zOffset = 13.75f;
                    if ((ushort)faceoffSpot >= 5)
                        zOffset -= 1f;

                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team.Value == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team.Value == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = 9f;
                        if (player.Team.Value == PlayerTeam.Red)
                            quaternion = Quaternion.Euler(0, 90, 0);
                        else
                            quaternion = Quaternion.Euler(0, -90, 0);
                    }
                    else
                        xOffset = -4.5f;
                    break;

                case GOALIE_POSITION:
                    zOffset = 0.1f;
                    xOffset = 0.6f;
                    float quaternionY = 35;

                    if (player.Team.Value == PlayerTeam.Red) {
                        zOffset *= -1f;
                        if (faceoffSpot == FaceoffSpot.RedteamDZoneLeft) {
                            xOffset *= -1f;
                            quaternion = Quaternion.Euler(0, -1 * quaternionY, 0);
                        }
                        else if (faceoffSpot == FaceoffSpot.RedteamDZoneRight) {
                            quaternion = Quaternion.Euler(0, quaternionY, 0);
                        }
                        else {
                            zOffset = 0;
                            xOffset = 0;
                        }
                    }
                    else {
                        if (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft) {
                            xOffset *= -1f;
                            quaternion = Quaternion.Euler(0, quaternionY - 180, 0);
                        }
                        else if (faceoffSpot == FaceoffSpot.BlueteamDZoneRight)
                            quaternion = Quaternion.Euler(0, 180 - quaternionY, 0);
                        else {
                            zOffset = 0;
                            xOffset = 0;
                        }
                    }

                    player.PlayerBody.Server_Teleport(new Vector3(player.PlayerBody.transform.position.x + xOffset, player.PlayerBody.transform.position.y, player.PlayerBody.transform.position.z + zOffset), quaternion);
                    break;
            }

            if (player.PlayerPosition.Name != GOALIE_POSITION) {
                if (player.Team.Value == PlayerTeam.Red) {
                    xOffset *= -1;
                    zOffset *= -1;
                }
                player.PlayerBody.Server_Teleport(new Vector3(faceoffDot.x + xOffset, faceoffDot.y, faceoffDot.z + zOffset), quaternion);
            }
        }

        /// <summary>
        /// Function that finds a team's goalie.
        /// </summary>
        /// <param name="team">PlayerTeam, team of the goalie.</param>
        /// <returns>Player, goalie found or null.</returns>
        internal static Player GetTeamGoalie(PlayerTeam team) {
            return PlayerManager.Instance.GetPlayersByTeam(team).FirstOrDefault(x => x.Role.Value == PlayerRole.Goalie);
        }

        /// <summary>
        /// Function that finds the other team's goalie.
        /// </summary>
        /// <param name="team">PlayerTeam, opposing team of the goalie.</param>
        /// <returns>Player, goalie found or null.</returns>
        internal static Player GetOtherTeamGoalie(PlayerTeam team) {
            return PlayerManager.Instance.GetPlayersByTeam(TeamFunc.GetOtherTeam(team)).FirstOrDefault(x => x.Role.Value == PlayerRole.Goalie);
        }

        /// <summary>
        /// Function that returns true if the PlayerPosition has the given role, and if it is claimed depending on hasToBeClaimed.
        /// </summary>
        /// <param name="pPosition">PlayerPosition, object to check.</param>
        /// <param name="role">PlayerRole, role to check for in the PlayerPosition.</param>
        /// <param name="hasToBeClaimed">Bool, true if the PlayerPosition has to be claimed.</param>
        /// <returns>Bool, true if the PlayerPosition has the given PlayerRole, and is claimed or not depending of hasToBeClaimed.</returns>
        internal static bool IsRole(PlayerPosition pPosition, PlayerRole role, bool hasToBeClaimed = true) {
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
        internal static bool IsAttacker(PlayerPosition pPosition, bool hasToBeClaimed = true) {
            return IsRole(pPosition, PlayerRole.Attacker, hasToBeClaimed);
        }

        /// <summary>
        /// Function that returns true if the PlayerPosition is a goalie, and if it is claimed depending on hasToBeClaimed.
        /// </summary>
        /// <param name="pPosition">PlayerPosition, object to check.</param>
        /// <param name="hasToBeClaimed">Bool, true if the PlayerPosition has to be claimed.</param>
        /// <returns>Bool, true if the PlayerPosition is a goalie, and is claimed or not depending of hasToBeClaimed.</returns>
        internal static bool IsGoalie(PlayerPosition pPosition, bool hasToBeClaimed = true) {
            return IsRole(pPosition, PlayerRole.Goalie, hasToBeClaimed);
        }

        /// <summary>
        /// Function that returns true if the player is a goalie.
        /// </summary>
        /// <param name="player">Player, player to check.</param>
        /// <returns>Bool, true if the player is a goalie.</returns>
        internal static bool IsGoalie(Player player) {
            return player.Role.Value == PlayerRole.Goalie;
        }
        #endregion
    }
}
