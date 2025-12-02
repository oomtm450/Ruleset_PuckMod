using Codebase;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing the code for faceoffs.
    /// </summary>
    internal static class Faceoff {
        /// <summary>
        /// Function that returns the next faceoff position.
        /// </summary>
        /// <param name="team">PlayerTeam, team linked to the faceoff being called.</param>
        /// <param name="isIcing">Bool, was icing called or not.</param>
        /// <param name="puckLastState">(Vector3, Zone), puck's last position and zone.</param>
        /// <returns>FaceoffSpot, next faceoff position.</returns>
        internal static FaceoffSpot GetNextFaceoffPosition(PlayerTeam team, bool isIcing, (Vector3 Position, Zone Zone) puckLastState) {
            ushort teamOffset;
            if (team == PlayerTeam.Red)
                teamOffset = 2;
            else
                teamOffset = 0;

            if (puckLastState.Position.x < 0) {
                if (isIcing)
                    return FaceoffSpot.BlueteamDZoneLeft + teamOffset;
                else
                    return SetNextFaceoffPositionFromLastTouch(team, true, puckLastState);
            }
            else {
                if (isIcing)
                    return FaceoffSpot.BlueteamDZoneRight + teamOffset;
                else
                    return SetNextFaceoffPositionFromLastTouch(team, false, puckLastState);
            }
        }

        /// <summary>
        /// Function that returns the next faceoff position from the last touch.
        /// </summary>
        /// <param name="team">PlayerTeam, team linked to the faceoff being called.</param>
        /// <param name="left">Bool, true if the faceoff has to be on the left.</param>
        /// <param name="puckLastState">(Vector3, Zone), puck's last position and zone.</param>
        /// <returns>FaceoffSpot, next faceoff position.</returns>
        private static FaceoffSpot SetNextFaceoffPositionFromLastTouch(PlayerTeam team, bool left, (Vector3 Position, Zone Zone) puckLastState) {
            Zone puckZone = ZoneFunc.GetZone(puckLastState.Position, puckLastState.Zone, Ruleset.PuckRadius);
            if (puckZone == Zone.BlueTeam_BehindGoalLine || puckZone == Zone.BlueTeam_Zone) {
                if (team == PlayerTeam.Blue) {
                    if (left)
                        return FaceoffSpot.BlueteamDZoneLeft;
                    else
                        return FaceoffSpot.BlueteamDZoneRight;
                }
                else {
                    if (left)
                        return FaceoffSpot.BlueteamBLLeft;
                    else
                        return FaceoffSpot.BlueteamBLRight;
                }
            }
            else if (puckZone == Zone.RedTeam_BehindGoalLine || puckZone == Zone.RedTeam_Zone) {
                if (team == PlayerTeam.Red) {
                    if (left)
                        return FaceoffSpot.RedteamDZoneLeft;
                    else
                        return FaceoffSpot.RedteamDZoneRight;
                }
                else {
                    if (left)
                        return FaceoffSpot.RedteamBLLeft;
                    else
                        return FaceoffSpot.RedteamBLRight;
                }
            }
            else if (puckZone == Zone.BlueTeam_Center) {
                if (left)
                    return FaceoffSpot.BlueteamBLLeft;
                else
                    return FaceoffSpot.BlueteamBLRight;
            }
            else if (puckZone == Zone.RedTeam_Center) {
                if (left)
                    return FaceoffSpot.RedteamBLLeft;
                else
                    return FaceoffSpot.RedteamBLRight;
            }

            return FaceoffSpot.Center;
        }

        /// <summary>
        /// Function that returns the position of the faceoff dot linked to the faceoff spot.
        /// </summary>
        /// <param name="faceoffSpot">FaceoffSpot, faceoff spot.</param>
        /// <returns>Vector3, position of the faceoff dot.</returns>
        internal static Vector3 GetFaceoffDot(FaceoffSpot faceoffSpot) {
            switch (faceoffSpot) {
                case FaceoffSpot.BlueteamBLLeft:
                    return new Vector3(-9.97f, 0.01f, 11f);

                case FaceoffSpot.BlueteamBLRight:
                    return new Vector3(9.97f, 0.01f, 11f);

                case FaceoffSpot.RedteamBLLeft:
                    return new Vector3(-9.97f, 0.01f, -11f);

                case FaceoffSpot.RedteamBLRight:
                    return new Vector3(9.97f, 0.01f, -11f);

                case FaceoffSpot.BlueteamDZoneLeft:
                    return new Vector3(-9.95f, 0.01f, 29.75f);

                case FaceoffSpot.BlueteamDZoneRight:
                    return new Vector3(9.95f, 0.01f, 29.75f);

                case FaceoffSpot.RedteamDZoneLeft:
                    return new Vector3(-9.95f, 0.01f, -29.75f);

                case FaceoffSpot.RedteamDZoneRight:
                    return new Vector3(9.95f, 0.01f, -29.75f);

                default:
                    return new Vector3(0f, 0.01f, 0f);
            }
        }
    }
}
