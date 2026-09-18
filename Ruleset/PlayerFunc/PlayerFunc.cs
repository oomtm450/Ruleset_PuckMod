using Codebase;
using UnityEngine;
using static Codebase.PlayerFunc;

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
        /// <param name="arenaScaleX">Float, scale of the arena X coordinate.</param>
        /// <param name="arenaScaleZ">Float, scale of the arena X coordinate.</param>
        /// <param name="playerPosition">String, player's position.</param>
        /// <param name="rotation">Quaternion, player's rotation.</param>
        public static void TeleportOnFaceoff(Player player, Vector3 faceoffDot, FaceoffSpot faceoffSpot, float arenaScaleX = 1f, float arenaScaleZ = 1f,
            string playerPosition = "", Quaternion rotation = default) {
            if (!IsPlayerPlaying(player))
                return;

            arenaScaleX = arenaScaleX > 1f ? 1f : arenaScaleX;
            arenaScaleZ = arenaScaleZ > 1f ? 1f : arenaScaleZ;

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
                    if ((faceoffSpot == FaceoffSpot.RedTeamDZoneRight && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && player.Team == PlayerTeam.Blue))
                        xOffset = 9.75f;
                    else
                        xOffset = 9f;
                    break;

                case RIGHT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((faceoffSpot == FaceoffSpot.RedTeamDZoneLeft && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueTeamDZoneRight && player.Team == PlayerTeam.Blue))
                        xOffset = -9.75f;
                    else
                        xOffset = -9f;
                    break;

                case LEFT_DEFENDER_POSITION:
                    if ((faceoffSpot == FaceoffSpot.RedTeamDZoneLeft && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueTeamDZoneRight && player.Team == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = -7f;
                        if (player.Team == PlayerTeam.Red)
                            rotation = Quaternion.Euler(0, -90, 0);
                        else
                            rotation = Quaternion.Euler(0, 90, 0);
                    }
                    else {
                        zOffset = 13.75f;
                        if ((ushort)faceoffSpot >= 5)
                            zOffset -= 1f;

                        xOffset = 4.5f;

                        if ((faceoffSpot == FaceoffSpot.RedTeamDZoneRight && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && player.Team == PlayerTeam.Blue)) {
                            zOffset -= 1f;
                            xOffset += 0.5f;
                        }
                    }
                    break;

                case RIGHT_DEFENDER_POSITION:
                    if ((faceoffSpot == FaceoffSpot.RedTeamDZoneRight && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && player.Team == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = 7f;
                        if (player.Team == PlayerTeam.Red)
                            rotation = Quaternion.Euler(0, 90, 0);
                        else
                            rotation = Quaternion.Euler(0, -90, 0);
                    }
                    else {
                        zOffset = 13.75f;
                        if ((ushort)faceoffSpot >= 5)
                            zOffset -= 1f;

                        xOffset = -4.5f;

                        if ((faceoffSpot == FaceoffSpot.RedTeamDZoneLeft && player.Team == PlayerTeam.Red) || (faceoffSpot == FaceoffSpot.BlueTeamDZoneRight && player.Team == PlayerTeam.Blue)) {
                            zOffset -= 1f;
                            xOffset -= 0.5f;
                        }
                    }
                    break;

                case GOALIE_POSITION:
                    zOffset = 0.1f;
                    xOffset = 0.6f;
                    float quaternionY = 35;

                    if (player.Team == PlayerTeam.Red) {
                        zOffset *= -1f;
                        if (faceoffSpot == FaceoffSpot.RedTeamDZoneLeft) {
                            xOffset *= -1f;
                            rotation = Quaternion.Euler(0, -1 * quaternionY, 0);
                        }
                        else if (faceoffSpot == FaceoffSpot.RedTeamDZoneRight) {
                            rotation = Quaternion.Euler(0, quaternionY, 0);
                        }
                        else {
                            zOffset = 0;
                            xOffset = 0;
                        }
                    }
                    else {
                        if (faceoffSpot == FaceoffSpot.BlueTeamDZoneLeft) {
                            xOffset *= -1f;
                            rotation = Quaternion.Euler(0, quaternionY - 180, 0);
                        }
                        else if (faceoffSpot == FaceoffSpot.BlueTeamDZoneRight)
                            rotation = Quaternion.Euler(0, 180 - quaternionY, 0);
                        else {
                            zOffset = 0;
                            xOffset = 0;
                        }
                    }

                    Vector3 teleportPosition = new Vector3(player.PlayerBody.transform.position.x + (xOffset * arenaScaleX) + Ruleset.ArenaOffsetX, faceoffDot.y + Ruleset.ServerConfig.YOffsetForTeleport, player.PlayerBody.transform.position.z + (zOffset * arenaScaleZ) + Ruleset.ArenaOffsetZ);
                    player.PlayerBody.Server_Teleport(teleportPosition, rotation);
                    Ruleset.PlayersToTeleport.Add(new PlayerWithCoordinate { Player = player, Position = teleportPosition, Rotation = rotation, });
                    break;
            }

            if (playerPosition != GOALIE_POSITION) {
                if (player.Team == PlayerTeam.Red) {
                    xOffset *= -1;
                    zOffset *= -1;
                }

                if (faceoffSpot == FaceoffSpot.Center && playerPosition != CENTER_POSITION) {
                    xOffset *= 1.9f;

                    if (playerPosition != LEFT_DEFENDER_POSITION && playerPosition != RIGHT_DEFENDER_POSITION)
                        zOffset *= 1.9f;
                    else
                        zOffset *= 0.8f;
                }

                Vector3 teleportPosition = new Vector3(faceoffDot.x + (xOffset * arenaScaleX), faceoffDot.y + Ruleset.ServerConfig.YOffsetForTeleport, faceoffDot.z + (zOffset * arenaScaleZ));
                player.PlayerBody.Server_Teleport(teleportPosition, rotation);
                Ruleset.PlayersToTeleport.Add(new PlayerWithCoordinate { Player = player, Position = teleportPosition, Rotation = rotation, });
            }

            player.PlayerBody.Server_Unfreeze();
            Ruleset.UnfreezeStick(player.Stick);
        }
        #endregion
    }
}
