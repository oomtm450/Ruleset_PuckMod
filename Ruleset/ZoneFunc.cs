using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class ZoneFunc {
        #region Constants
        /// <summary>
        /// Dictionary of ArenaElement and (double Start, double End), dictionary containing all the start and end Z positions of all the lines on the ice for the zones.
        /// </summary>
        internal static Dictionary<ArenaElement, (double Start, double End)> ICE_Z_POSITIONS { get; } = new Dictionary<ArenaElement, (double Start, double End)> {
            { ArenaElement.BlueTeam_BlueLine, (13.0, 13.5) },
            { ArenaElement.RedTeam_BlueLine, (-13.5, -13.0) },
            { ArenaElement.CenterLine, (-0.25, 0.25) },
            { ArenaElement.BlueTeam_GoalLine, (39.75, 40) },
            { ArenaElement.RedTeam_GoalLine, (-40, -39.75) },
            { ArenaElement.BlueTeam_BluePaint, (37.25, 40) },
            { ArenaElement.RedTeam_BluePaint, (-40, -37.25) },
            { ArenaElement.BlueTeam_HashMarks, (29.1, 30.4) },
            { ArenaElement.RedTeam_HashMarks, (-30.4, -29.1) },
        };

        /// <summary>
        /// Dictionary of ArenaElement and (double Start, double End), dictionary containing all the start and end Z positions of all the lines on the ice for the zones.
        /// </summary>
        internal static Dictionary<ArenaElement, (double Start, double End)> ICE_X_POSITIONS { get; } = new Dictionary<ArenaElement, (double Start, double End)> {
            { ArenaElement.BlueTeam_BluePaint, (-2.5, 2.5) },
            { ArenaElement.RedTeam_BluePaint, (-2.5, 2.5) },
        };

        /// <summary>
        /// ReadOnlyCollection of Zone, defense zones attributed to the blue team.
        /// </summary>
        private static ReadOnlyCollection<Zone> BLUE_TEAM_DEFENSE_ZONES { get; } = new ReadOnlyCollection<Zone>(new List<Zone> {
            Zone.BlueTeam_Zone,
            Zone.BlueTeam_BehindGoalLine,
        });

        /// <summary>
        /// ReadOnlyCollection of Zone, defense zones attributed to the red team.
        /// </summary>
        private static ReadOnlyCollection<Zone> RED_TEAM_DEFENSE_ZONES { get; } = new ReadOnlyCollection<Zone>(new List<Zone> {
            Zone.RedTeam_Zone,
            Zone.RedTeam_BehindGoalLine,
        });
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Function that returns the zone of the sent position using the oldZone and radius as context for including the line itself.
        /// </summary>
        /// <param name="position">Vector3, position with the unknown zone.</param>
        /// <param name="oldZone">Zone, last zone of the position.</param>
        /// <param name="radius">Float, radius (half diameter) of the position's physic object for additional precision.</param>
        /// <returns>Zone, zone of the position.</returns>
        internal static Zone GetZone(Vector3 position, Zone oldZone, float radius) {
            float zMax = position.z + radius;
            
            // Red team.
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_GoalLine].Start) {
                return Zone.RedTeam_BehindGoalLine;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_GoalLine].End && oldZone == Zone.RedTeam_BehindGoalLine) {
                if (oldZone == Zone.RedTeam_BehindGoalLine)
                    return Zone.RedTeam_BehindGoalLine;
                else
                    return Zone.RedTeam_Zone;
            }

            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_BlueLine].Start) {
                return Zone.RedTeam_Zone;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_BlueLine].End) {
                if (oldZone == Zone.RedTeam_Zone)
                    return Zone.RedTeam_Zone;
                else
                    return Zone.RedTeam_Center;
            }

            if (zMax < ICE_Z_POSITIONS[ArenaElement.CenterLine].Start) {
                return Zone.RedTeam_Center;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.CenterLine].End && oldZone == Zone.RedTeam_Center) {
                return Zone.RedTeam_Center;
            }

            // Both team.
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_BlueLine].End) {
                if (oldZone == Zone.RedTeam_Center)
                    return Zone.RedTeam_Center;
                else
                    return Zone.BlueTeam_Center;
            }

            // Blue team.
            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_BlueLine].Start) {
                return Zone.BlueTeam_Center;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_BlueLine].End) {
                if (oldZone == Zone.BlueTeam_Center)
                    return Zone.BlueTeam_Center;
                else
                    return Zone.BlueTeam_Zone;
            }

            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_GoalLine].Start) {
                return Zone.BlueTeam_Zone;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_GoalLine].End) {
                if (oldZone == Zone.BlueTeam_Zone)
                    return Zone.BlueTeam_Zone;
                else
                    return Zone.BlueTeam_BehindGoalLine;
            }

            return Zone.BlueTeam_BehindGoalLine;
        }

        internal static bool IsBehindHashmarks(PlayerTeam team, Vector3 position, Zone oldZone, float radius) {
            float zMax = position.z + radius;

            // Red team.
            if (team == PlayerTeam.Red) {
                if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_HashMarks].Start)
                    return true;
            }
            else if (team == PlayerTeam.Blue) {
                if (zMax > ICE_Z_POSITIONS[ArenaElement.BlueTeam_HashMarks].End)
                    return true;
            }

            return false;
        }

        internal static Zone GetZone(FaceoffSpot faceoffSpot) {
            switch (faceoffSpot) {
                case FaceoffSpot.BlueteamDZoneLeft:
                case FaceoffSpot.BlueteamDZoneRight:
                    return Zone.BlueTeam_Zone;
                case FaceoffSpot.BlueteamBLLeft:
                case FaceoffSpot.BlueteamBLRight:
                    return Zone.BlueTeam_Center;

                case FaceoffSpot.RedteamDZoneLeft:
                case FaceoffSpot.RedteamDZoneRight:
                    return Zone.RedTeam_Zone;
                case FaceoffSpot.RedteamBLLeft:
                case FaceoffSpot.RedteamBLRight:
                    return Zone.RedTeam_Center;

                default:
                    return Zone.BlueTeam_Center;
            }
        }

        internal static List<Zone> GetTeamZones(PlayerTeam team, bool includeCenter = false) {
            switch (team) { // TODO : Optimize with pre made lists.
                case PlayerTeam.Blue:
                    if (includeCenter) {
                        return new List<Zone>(BLUE_TEAM_DEFENSE_ZONES) {
                            Zone.BlueTeam_Center
                        };
                    }
                    else
                        return BLUE_TEAM_DEFENSE_ZONES.ToList();

                case PlayerTeam.Red:
                    if (includeCenter) {
                        return new List<Zone>(RED_TEAM_DEFENSE_ZONES) {
                            Zone.RedTeam_Center
                        };
                    }
                    else
                        return RED_TEAM_DEFENSE_ZONES.ToList();
            }

            return new List<Zone> {
                Zone.None,
            };
        }
        #endregion
    }

    #region Enums
    public enum Zone {
        None,
        BlueTeam_BehindGoalLine,
        RedTeam_BehindGoalLine,
        BlueTeam_Zone,
        RedTeam_Zone,
        BlueTeam_Center,
        RedTeam_Center,
    }

    public enum ArenaElement {
        BlueTeam_BlueLine,
        RedTeam_BlueLine,
        CenterLine,
        BlueTeam_GoalLine,
        RedTeam_GoalLine,
        BlueTeam_HashMarks,
        RedTeam_HashMarks,
        BlueTeam_BluePaint,
        RedTeam_BluePaint,
    }
    #endregion
}
