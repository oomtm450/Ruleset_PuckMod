using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class PlayerFunc {
        internal static bool IsPlayerPlaying(Player player) {
            return !(player.Role.Value == PlayerRole.None || !player.IsCharacterFullySpawned);
        }

        internal static void TeleportOnFaceoff(Player player, Vector3 faceoffDot, FaceoffSpot nextFaceoffSpot) {
            if (!IsPlayerPlaying(player) || nextFaceoffSpot == FaceoffSpot.Center)
                return;

            float xOffset = 0, zOffset = 0;
            Quaternion quaternion = player.PlayerBody.Rigidbody.rotation;
            switch (player.PlayerPosition.Name) {
                case SystemFunc.PlayerFunc.CENTER_POSITION:
                    zOffset = 1.5f;
                    break;
                case SystemFunc.PlayerFunc.LEFT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((nextFaceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team.Value == PlayerTeam.Red) || (nextFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team.Value == PlayerTeam.Blue))
                        xOffset = 7f;
                    else
                        xOffset = 9f;
                    break;
                case SystemFunc.PlayerFunc.RIGHT_WINGER_POSITION:
                    zOffset = 1.5f;
                    if ((nextFaceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team.Value == PlayerTeam.Red) || (nextFaceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team.Value == PlayerTeam.Blue))
                        xOffset = -7f;
                    else
                        xOffset = -9f;
                    break;
                case SystemFunc.PlayerFunc.LEFT_DEFENDER_POSITION:
                    zOffset = 13.5f;
                    if ((ushort)nextFaceoffSpot >= 5)
                        zOffset -= 1f;

                    if ((nextFaceoffSpot == FaceoffSpot.RedteamDZoneLeft && player.Team.Value == PlayerTeam.Red) || (nextFaceoffSpot == FaceoffSpot.BlueteamDZoneRight && player.Team.Value == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = -9f;
                        if (player.Team.Value == PlayerTeam.Red)
                            quaternion = Quaternion.Euler(0, -90, 0);
                        else
                            quaternion = Quaternion.Euler(0, 90, 0);
                    }
                    else
                        xOffset = 4f;
                    break;
                case SystemFunc.PlayerFunc.RIGHT_DEFENDER_POSITION:
                    zOffset = 13.5f;
                    if ((ushort)nextFaceoffSpot >= 5)
                        zOffset -= 1f;

                    if ((nextFaceoffSpot == FaceoffSpot.RedteamDZoneRight && player.Team.Value == PlayerTeam.Red) || (nextFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft && player.Team.Value == PlayerTeam.Blue)) {
                        zOffset = 1.5f;
                        xOffset = 9f;
                        if (player.Team.Value == PlayerTeam.Red)
                            quaternion = Quaternion.Euler(0, 90, 0);
                        else
                            quaternion = Quaternion.Euler(0, -90, 0);
                    }
                    else
                        xOffset = -4f;
                    break;

                case SystemFunc.PlayerFunc.GOALIE_POSITION:
                    zOffset = -0.1f;
                    if (player.Team.Value == PlayerTeam.Red) {
                        if (nextFaceoffSpot == FaceoffSpot.RedteamDZoneLeft) {
                            xOffset = -1.4f;
                            quaternion = Quaternion.Euler(0, -25, 0);
                        }
                        else if (nextFaceoffSpot == FaceoffSpot.RedteamDZoneRight) {
                            xOffset = 1.4f;
                            quaternion = Quaternion.Euler(0, 25, 0);
                        }
                    }
                    else {
                        zOffset = 0.1f;
                        if (nextFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft) {
                            xOffset = -1.4f;
                            quaternion = Quaternion.Euler(0, -155, 0);
                        }
                        else if (nextFaceoffSpot == FaceoffSpot.BlueteamDZoneRight) {
                            xOffset = 1.4f;
                            quaternion = Quaternion.Euler(0, 155, 0);
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
    }
}
