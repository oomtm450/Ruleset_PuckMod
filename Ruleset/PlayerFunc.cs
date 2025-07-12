using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class PlayerFunc {
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
                case SystemFunc.PlayerFunc.CENTER_POSITION:
                    zOffset = 1.5f;
                    break;
                case SystemFunc.PlayerFunc.LEFT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team.Value == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team.Value == PlayerTeam.Blue))
                        xOffset = 6.5f;
                    else
                        xOffset = 9f;
                    break;
                case SystemFunc.PlayerFunc.RIGHT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team.Value == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team.Value == PlayerTeam.Blue))
                        xOffset = -6.5f;
                    else
                        xOffset = -9f;
                    break;
                case SystemFunc.PlayerFunc.LEFT_DEFENDER_POSITION:
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
                case SystemFunc.PlayerFunc.RIGHT_DEFENDER_POSITION:
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

                case SystemFunc.PlayerFunc.GOALIE_POSITION:
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

            if (player.PlayerPosition.Name != SystemFunc.PlayerFunc.GOALIE_POSITION) {
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
    }
}
