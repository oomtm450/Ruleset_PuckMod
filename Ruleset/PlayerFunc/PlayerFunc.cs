using Codebase;
using static Codebase.PlayerFunc;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal class PlayerFunc {
        #region Properties
        /// <summary>
        /// LockDictionary of ulong and string, dictionary of all players clientId and steamId.
        /// </summary>
        internal static LockDictionary<ulong, string> Players_ClientId_SteamId { get; } = new LockDictionary<ulong, string>();
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Method that teleports a player on a faceoff dot using predetermined offsets depending on the player's position.
        /// </summary>
        /// <param name="player">Player, player to teleport.</param>
        /// <param name="faceoffDot">Vector3, position of the faceoff dot.</param>
        /// <param name="faceoffSpot">FaceoffSpot, location of the faceoff.</param>
        /// <param name="playerPosition">String, player's position.</param>
        public static void TeleportOnFaceoff(Player player, Vector3 faceoffDot, FaceoffSpot faceoffSpot, string playerPosition = "", Quaternion rotation = default) {
            if (!IsPlayerPlaying(player))
                return;

            if (string.IsNullOrEmpty(playerPosition))
                playerPosition = player.PlayerPosition.Name;

            if (rotation.Equals(default))
                rotation = player.PlayerBody.Rigidbody.rotation;

            float xOffset = 0, zOffset = 0;
            switch (playerPosition) {
                case CENTER_POSITION:
                    zOffset = 1.5f;
                    break;
                case LEFT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team == PlayerTeam.Blue))
                        xOffset = 6.5f;
                    else
                        xOffset = 9f;
                    break;
                case RIGHT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team == PlayerTeam.Blue))
                        xOffset = -6.5f;
                    else
                        xOffset = -9f;
                    break;
                case LEFT_DEFENDER_POSITION:
                    zOffset = 13.75f;
                    if ((ushort)faceoffSpot >= 5)
                        zOffset -= 1f;

                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = -9f;
                        if (player.Team == PlayerTeam.Red)
                            rotation = Quaternion.Euler(0, -90, 0);
                        else
                            rotation = Quaternion.Euler(0, 90, 0);
                    }
                    else
                        xOffset = 4.5f;
                    break;
                case RIGHT_DEFENDER_POSITION:
                    zOffset = 13.75f;
                    if ((ushort)faceoffSpot >= 5)
                        zOffset -= 1f;

                    if ((faceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = 9f;
                        if (player.Team == PlayerTeam.Red)
                            rotation = Quaternion.Euler(0, 90, 0);
                        else
                            rotation = Quaternion.Euler(0, -90, 0);
                    }
                    else
                        xOffset = -4.5f;
                    break;

                case GOALIE_POSITION:
                    zOffset = 0.1f;
                    xOffset = 0.6f;
                    float quaternionY = 35;

                    if (player.Team == PlayerTeam.Red) {
                        zOffset *= -1f;
                        if (faceoffSpot == FaceoffSpot.RedteamDZoneLeft) {
                            xOffset *= -1f;
                            rotation = Quaternion.Euler(0, -1 * quaternionY, 0);
                        }
                        else if (faceoffSpot == FaceoffSpot.RedteamDZoneRight) {
                            rotation = Quaternion.Euler(0, quaternionY, 0);
                        }
                        else {
                            zOffset = 0;
                            xOffset = 0;
                        }
                    }
                    else {
                        if (faceoffSpot == FaceoffSpot.BlueteamDZoneLeft) {
                            xOffset *= -1f;
                            rotation = Quaternion.Euler(0, quaternionY - 180, 0);
                        }
                        else if (faceoffSpot == FaceoffSpot.BlueteamDZoneRight)
                            rotation = Quaternion.Euler(0, 180 - quaternionY, 0);
                        else {
                            zOffset = 0;
                            xOffset = 0;
                        }
                    }

                    player.PlayerBody.Server_Teleport(new Vector3(player.PlayerBody.transform.position.x + xOffset, player.PlayerBody.transform.position.y, player.PlayerBody.transform.position.z + zOffset), rotation);
                    break;
            }

            if (playerPosition != GOALIE_POSITION) {
                if (player.Team == PlayerTeam.Red) {
                    xOffset *= -1;
                    zOffset *= -1;
                }

                if (faceoffSpot == FaceoffSpot.Center && playerPosition != CENTER_POSITION) {
                    xOffset *= 2;

                    if (playerPosition != LEFT_DEFENDER_POSITION && playerPosition != RIGHT_DEFENDER_POSITION)
                        zOffset *= 1.9f;
                    else
                        zOffset *= 0.8f;
                }

                player.PlayerBody.Server_Teleport(new Vector3(faceoffDot.x + xOffset, faceoffDot.y, faceoffDot.z + zOffset), rotation);
            }
        }
        #endregion
    }
}
