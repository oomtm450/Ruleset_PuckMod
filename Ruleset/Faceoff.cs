using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class Faceoff {
        internal static void SetNextFaceoffPosition(PlayerTeam team, bool isIcing, (Vector3 Position, Zone Zone) puckLastState) {
            ushort teamOffset;
            if (team == PlayerTeam.Red)
                teamOffset = 2;
            else
                teamOffset = 0;

            if (puckLastState.Position.x < 0) {
                if (isIcing)
                    Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamDZoneLeft + teamOffset;
                else
                    SetNextFaceoffPositionFromLastTouch(team, true, puckLastState);
            }
            else {
                if (isIcing)
                    Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamDZoneRight + teamOffset;
                else
                    SetNextFaceoffPositionFromLastTouch(team, false, puckLastState);
            }
        }

        private static void SetNextFaceoffPositionFromLastTouch(PlayerTeam team, bool left, (Vector3 Position, Zone Zone) puckLastState) {
            Zone puckZone = ZoneFunc.GetZone(puckLastState.Position, puckLastState.Zone, Ruleset.PUCK_RADIUS);
            if (puckZone == Zone.BlueTeam_BehindGoalLine || puckZone == Zone.BlueTeam_Zone) {
                if (team == PlayerTeam.Blue) {
                    if (left)
                        Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamDZoneLeft;
                    else
                        Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamDZoneRight;
                }
                else {
                    if (left)
                        Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamBLLeft;
                    else
                        Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamBLRight;
                }
            }
            else if (puckZone == Zone.RedTeam_BehindGoalLine || puckZone == Zone.RedTeam_Zone) {
                if (team == PlayerTeam.Red) {
                    if (left)
                        Ruleset.NextFaceoffSpot = FaceoffSpot.RedteamDZoneLeft;
                    else
                        Ruleset.NextFaceoffSpot = FaceoffSpot.RedteamDZoneRight;
                }
                else {
                    if (left)
                        Ruleset.NextFaceoffSpot = FaceoffSpot.RedteamBLLeft;
                    else
                        Ruleset.NextFaceoffSpot = FaceoffSpot.RedteamBLRight;
                }
            }
            else if (puckZone == Zone.BlueTeam_Center) {
                if (left)
                    Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamBLLeft;
                else
                    Ruleset.NextFaceoffSpot = FaceoffSpot.BlueteamBLRight;
            }
            else if (puckZone == Zone.RedTeam_Center) {
                if (left)
                    Ruleset.NextFaceoffSpot = FaceoffSpot.RedteamBLLeft;
                else
                    Ruleset.NextFaceoffSpot = FaceoffSpot.RedteamBLRight;
            }
        }

        internal static Vector3 GetFaceoffDot(FaceoffSpot nextFaceoffSpot) {
            Vector3 dot;

            switch (nextFaceoffSpot) {
                case FaceoffSpot.BlueteamBLLeft:
                    dot = new Vector3(-9.975f, 0.01f, 11f);
                    break;

                case FaceoffSpot.BlueteamBLRight:
                    dot = new Vector3(9.975f, 0.01f, 11f);
                    break;

                case FaceoffSpot.RedteamBLLeft:
                    dot = new Vector3(-9.975f, 0.01f, -11f);
                    break;

                case FaceoffSpot.RedteamBLRight:
                    dot = new Vector3(9.975f, 0.01f, -11f);
                    break;

                case FaceoffSpot.BlueteamDZoneLeft:
                    dot = new Vector3(-9.95f, 0.01f, 29.75f);
                    break;

                case FaceoffSpot.BlueteamDZoneRight:
                    dot = new Vector3(9.95f, 0.01f, 29.75f);
                    break;

                case FaceoffSpot.RedteamDZoneLeft:
                    dot = new Vector3(-9.95f, 0.01f, -29.75f);
                    break;

                case FaceoffSpot.RedteamDZoneRight:
                    dot = new Vector3(9.95f, 0.01f, -29.75f);
                    break;

                default:
                    dot = new Vector3(0f, 0.01f, 0f);
                    break;
            }

            return dot;
        }
    }

    public enum FaceoffSpot : ushort {
        Center,
        BlueteamBLLeft,
        BlueteamBLRight,
        RedteamBLLeft,
        RedteamBLRight,
        BlueteamDZoneLeft,
        BlueteamDZoneRight,
        RedteamDZoneLeft,
        RedteamDZoneRight,
    }
}
