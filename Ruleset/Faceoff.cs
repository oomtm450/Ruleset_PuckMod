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
                    return FaceoffSpot.BlueTeamDZoneLeft + teamOffset;
                else
                    return SetNextFaceoffPositionFromLastTouch(team, true, puckLastState, rule);
            }
            else {
                if (rule == Rule.Icing)
                    return FaceoffSpot.BlueTeamDZoneRight + teamOffset;
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
                if (team == PlayerTeam.Blue || rule == Rule.DelayOfGame || rule == Rule.None || rule == Rule.Offside || rule == Rule.HighStick) {
                    if (left)
                        return FaceoffSpot.BlueTeamDZoneLeft;
                    else
                        return FaceoffSpot.BlueTeamDZoneRight;
                }
                else {
                    if (left)
                        return FaceoffSpot.BlueTeamBLLeft;
                    else
                        return FaceoffSpot.BlueTeamBLRight;
                }
            }
            else if (puckZone == Codebase.Zone.RedTeam_BehindGoalLine || puckZone == Codebase.Zone.RedTeam_Zone) {
                if (team == PlayerTeam.Red || rule == Rule.DelayOfGame || rule == Rule.None || rule == Rule.Offside || rule == Rule.HighStick) {
                    if (left)
                        return FaceoffSpot.RedTeamDZoneLeft;
                    else
                        return FaceoffSpot.RedTeamDZoneRight;
                }
                else {
                    if (left)
                        return FaceoffSpot.RedTeamBLLeft;
                    else
                        return FaceoffSpot.RedTeamBLRight;
                }
            }
            else if (puckZone == Codebase.Zone.BlueTeam_Center) {
                if (left)
                    return FaceoffSpot.BlueTeamBLLeft;
                else
                    return FaceoffSpot.BlueTeamBLRight;
            }
            else if (puckZone == Codebase.Zone.RedTeam_Center) {
                if (left)
                    return FaceoffSpot.RedTeamBLLeft;
                else
                    return FaceoffSpot.RedTeamBLRight;
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
                case FaceoffSpot.BlueTeamBLLeft:
                    return new Vector3((-9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.BlueTeamBLRight:
                    return new Vector3((9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedTeamBLLeft:
                    return new Vector3((-9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedTeamBLRight:
                    return new Vector3((9.97f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-11f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.BlueTeamDZoneLeft:
                    return new Vector3((-9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (29.75f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.BlueTeamDZoneRight:
                    return new Vector3((9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (29.75f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedTeamDZoneLeft:
                    return new Vector3((-9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-29.75f * arenaScaleZ) + arenaOffsetZ);

                case FaceoffSpot.RedTeamDZoneRight:
                    return new Vector3((9.95f * arenaScaleX) + arenaOffsetX, arenaOffsetY, (-29.75f * arenaScaleZ) + arenaOffsetZ);

                default:
                    return new Vector3(arenaOffsetX, arenaOffsetY, arenaOffsetZ);
            }
        }
    }
}
