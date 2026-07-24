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
        /// <param name="rule">Rule, rule called for the faceoff.</param>
        /// <param name="puckLastState">(Vector3, Zone), puck's last position and zone.</param>
        /// <returns>FaceoffSpot, next faceoff position.</returns>
        internal static FaceoffSpot GetNextFaceoffPosition(PlayerTeam team, Rule rule, (Vector3 Position, Codebase.Zone Zone) puckLastState) {
            ushort teamOffset;
            if (team == PlayerTeam.Red)
                teamOffset = 2;
            else
                teamOffset = 0;

            if (puckLastState.Position.x < 0) {
                if (rule == Rule.Icing)
                    return FaceoffSpot.BlueteamDZoneLeft + teamOffset;
                else
                    return SetNextFaceoffPositionFromLastTouch(team, true, puckLastState, rule);
            }
            else {
                if (rule == Rule.Icing)
                    return FaceoffSpot.BlueteamDZoneRight + teamOffset;
                else
                    return SetNextFaceoffPositionFromLastTouch(team, false, puckLastState, rule);
            }
        }

        /// <summary>
        /// Function that returns the next faceoff position from the last touch.
        /// </summary>
        /// <param name="team">PlayerTeam, team linked to the faceoff being called.</param>
        /// <param name="left">Bool, true if the faceoff has to be on the left.</param>
        /// <param name="puckLastState">(Vector3, Zone), puck's last position and zone.</param>
        /// <param name="rule">Rule, rule called for the faceoff.</param>
        /// <returns>FaceoffSpot, next faceoff position.</returns>
        private static FaceoffSpot SetNextFaceoffPositionFromLastTouch(PlayerTeam team, bool left, (Vector3 Position, Codebase.Zone Zone) puckLastState, Rule rule) {
            Codebase.Zone puckZone = ZoneFunc.GetZone(puckLastState.Position, puckLastState.Zone, Ruleset.PuckRadius);

            if (puckZone == Codebase.Zone.BlueTeam_BehindGoalLine || puckZone == Codebase.Zone.BlueTeam_Zone) {
                if (team == PlayerTeam.Blue || rule == Rule.DelayOfGame) {
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
            else if (puckZone == Codebase.Zone.RedTeam_BehindGoalLine || puckZone == Codebase.Zone.RedTeam_Zone) {
                if (team == PlayerTeam.Red || rule == Rule.DelayOfGame) {
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
            else if (puckZone == Codebase.Zone.BlueTeam_Center) {
                if (left)
                    return FaceoffSpot.BlueteamBLLeft;
                else
                    return FaceoffSpot.BlueteamBLRight;
            }
            else if (puckZone == Codebase.Zone.RedTeam_Center) {
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
        /// <param name="arenaScaleX">Float, scale of the arena in X.</param>
        /// <param name="arenaScaleZ">Float, scale of the arena in Z.</param>
        /// <param name="arenaOffsetX">Float, offset of the arena in X.</param>
        /// <param name="arenaOffsetY">Float, offset of the arena in Y.</param>
        /// <param name="arenaOffsetZ">Float, offset of the arena in Z.</param>
        /// <returns>Vector3, position of the faceoff dot.</returns>
        internal static Vector3 GetFaceoffDot(FaceoffSpot faceoffSpot, float arenaScaleX = 1f, float arenaScaleZ = 1f, float arenaOffsetX = 0, float arenaOffsetY = 0, float arenaOffsetZ = 0) {
            switch (faceoffSpot) {
                case FaceoffSpot.BlueteamBLLeft:
                    return new Vector3((-9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.BlueteamBLRight:
                    return new Vector3((9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedteamBLLeft:
                    return new Vector3((-9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedteamBLRight:
                    return new Vector3((9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.BlueteamDZoneLeft:
                    return new Vector3((-9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (29.75f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.BlueteamDZoneRight:
                    return new Vector3((9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (29.75f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedteamDZoneLeft:
                    return new Vector3((-9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-29.75f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedteamDZoneRight:
                    return new Vector3((9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-29.75f * arenaScaleZ) + arenaOffsetZ);

                default:
                    return new Vector3(arenaOffsetX, arenaOffsetY, arenaOffsetZ);
            }
        }
    }
}
