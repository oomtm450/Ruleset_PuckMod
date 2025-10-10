using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Codebase {
    /// <summary>
    /// Class containing the code for the zones and ice elements.
    /// </summary>
    internal static class ZoneFunc {
        #region Constants
        /// <summary>
        /// Const Zone, default zone for initializing variables.
        /// </summary>
        internal const Zone DEFAULT_ZONE = Zone.BlueTeam_Center;

        /// <summary>
        /// ReadOnlyDictionary of IceElement and (double Start, double End), dictionary containing all the start and end Z positions of all the lines on the ice for the zones.
        /// </summary>
        internal static ReadOnlyDictionary<IceElement, (double Start, double End)> ICE_Z_POSITIONS { get; } = new ReadOnlyDictionary<IceElement, (double, double)>(
            new Dictionary<IceElement, (double, double)> {
                { IceElement.BlueTeam_BlueLine, (13.0, 13.5) },
                { IceElement.RedTeam_BlueLine, (-13.5, -13.0) },
                { IceElement.CenterLine, (-0.25, 0.25) },
                { IceElement.BlueTeam_GoalLine, (39.75, 40) },
                { IceElement.RedTeam_GoalLine, (-40, -39.75) },
                { IceElement.BlueTeam_BluePaint, (37.25, 40) },
                { IceElement.RedTeam_BluePaint, (-40, -37.25) },
                { IceElement.BlueTeam_HashMarks, (29.1, 30.4) },
                { IceElement.RedTeam_HashMarks, (-30.4, -29.1) },
            }
        );

        /// <summary>
        /// ReadOnlyDictionary of IceElement and (double Start, double End), dictionary containing all the start and end Z positions of all the lines on the ice for the zones.
        /// </summary>
        internal static ReadOnlyDictionary<IceElement, (double Start, double End)> ICE_X_POSITIONS { get; } = new ReadOnlyDictionary<IceElement, (double, double)>(
            new Dictionary<IceElement, (double, double)> {
                { IceElement.BlueTeam_BluePaint, (-2.5, 2.5) },
                { IceElement.RedTeam_BluePaint, (-2.5, 2.5) },
            }
        );

        /// <summary>
        /// ReadOnlyCollection of Zone, defense zones attributed to the blue team.
        /// </summary>
        private static ReadOnlyCollection<Zone> BLUE_TEAM_DEFENSE_ZONES { get; } = new ReadOnlyCollection<Zone>(new List<Zone> {
            Zone.BlueTeam_Zone,
            Zone.BlueTeam_BehindGoalLine,
        });

        /// <summary>
        /// ReadOnlyCollection of Zone, zones attributed to the blue team.
        /// </summary>
        private static ReadOnlyCollection<Zone> BLUE_TEAM_ZONES { get; } = new ReadOnlyCollection<Zone>(new List<Zone> {
            Zone.BlueTeam_Zone,
            Zone.BlueTeam_BehindGoalLine,
            Zone.BlueTeam_Center,
        });

        /// <summary>
        /// ReadOnlyCollection of Zone, defense zones attributed to the red team.
        /// </summary>
        private static ReadOnlyCollection<Zone> RED_TEAM_DEFENSE_ZONES { get; } = new ReadOnlyCollection<Zone>(new List<Zone> {
            Zone.RedTeam_Zone,
            Zone.RedTeam_BehindGoalLine,
        });

        /// <summary>
        /// ReadOnlyCollection of Zone, zones attributed to the red team.
        /// </summary>
        private static ReadOnlyCollection<Zone> RED_TEAM_ZONES { get; } = new ReadOnlyCollection<Zone>(new List<Zone> {
            Zone.RedTeam_Zone,
            Zone.RedTeam_BehindGoalLine,
            Zone.RedTeam_Center,
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
            if (zMax < ICE_Z_POSITIONS[IceElement.RedTeam_GoalLine].Start) {
                return Zone.RedTeam_BehindGoalLine;
            }
            if (zMax < ICE_Z_POSITIONS[IceElement.RedTeam_GoalLine].End && oldZone == Zone.RedTeam_BehindGoalLine) {
                if (oldZone == Zone.RedTeam_BehindGoalLine)
                    return Zone.RedTeam_BehindGoalLine;
                else
                    return Zone.RedTeam_Zone;
            }

            if (zMax < ICE_Z_POSITIONS[IceElement.RedTeam_BlueLine].Start) {
                return Zone.RedTeam_Zone;
            }
            if (zMax < ICE_Z_POSITIONS[IceElement.RedTeam_BlueLine].End) {
                if (oldZone == Zone.RedTeam_Zone)
                    return Zone.RedTeam_Zone;
                else
                    return Zone.RedTeam_Center;
            }

            if (zMax < ICE_Z_POSITIONS[IceElement.CenterLine].Start) {
                return Zone.RedTeam_Center;
            }
            if (zMax < ICE_Z_POSITIONS[IceElement.CenterLine].End && oldZone == Zone.RedTeam_Center) {
                return Zone.RedTeam_Center;
            }

            // Both team.
            if (zMax < ICE_Z_POSITIONS[IceElement.RedTeam_BlueLine].End) {
                if (oldZone == Zone.RedTeam_Center)
                    return Zone.RedTeam_Center;
                else
                    return Zone.BlueTeam_Center;
            }

            // Blue team.
            if (zMax < ICE_Z_POSITIONS[IceElement.BlueTeam_BlueLine].Start) {
                return Zone.BlueTeam_Center;
            }
            if (zMax < ICE_Z_POSITIONS[IceElement.BlueTeam_BlueLine].End) {
                if (oldZone == Zone.BlueTeam_Center)
                    return Zone.BlueTeam_Center;
                else
                    return Zone.BlueTeam_Zone;
            }

            if (zMax < ICE_Z_POSITIONS[IceElement.BlueTeam_GoalLine].Start) {
                return Zone.BlueTeam_Zone;
            }
            if (zMax < ICE_Z_POSITIONS[IceElement.BlueTeam_GoalLine].End) {
                if (oldZone == Zone.BlueTeam_Zone)
                    return Zone.BlueTeam_Zone;
                else
                    return Zone.BlueTeam_BehindGoalLine;
            }

            return Zone.BlueTeam_BehindGoalLine;
        }

        /// <summary>
        /// Function that checks if a position is behind a team's hashmarks.
        /// </summary>
        /// <param name="team">PlayerTeam, team linked to the hashmarks.</param>
        /// <param name="position">Vector3, position to check.</param>
        /// <param name="radius">Float, radius of the object linked to the position that is being checked.</param>
        /// <returns>Bool, is behind hashmarks or not.</returns>
        internal static bool IsBehindHashmarks(PlayerTeam team, Vector3 position, float radius = 0) {
            float zMax = position.z + radius;

            if (team == PlayerTeam.Red) {
                double hashMarkZPosition = ICE_Z_POSITIONS[IceElement.RedTeam_HashMarks].Start + (ICE_Z_POSITIONS[IceElement.RedTeam_HashMarks].End - ICE_Z_POSITIONS[IceElement.RedTeam_HashMarks].Start);
                if (zMax < hashMarkZPosition)
                    return true;
            }
            else if (team == PlayerTeam.Blue) {
                double hashMarkZPosition = ICE_Z_POSITIONS[IceElement.BlueTeam_HashMarks].End - (ICE_Z_POSITIONS[IceElement.BlueTeam_HashMarks].End - ICE_Z_POSITIONS[IceElement.BlueTeam_HashMarks].Start);
                if (zMax > hashMarkZPosition)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Function that returns the zone of a faceoff spot.
        /// </summary>
        /// <param name="faceoffSpot">FaceoffSpot, faceoff spot to find the zone of.</param>
        /// <returns>Zone, zone containing the faceoff spot.</returns>
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
                    return DEFAULT_ZONE;
            }
        }

        /// <summary>
        /// Function that returns the zones linked to a team.
        /// </summary>
        /// <param name="team">PlayerTeam, team linked to the zones.</param>
        /// <param name="includeCenter">Bool, true if the center zone has to be included.</param>
        /// <returns>List of Zone, zones linked to the team.</returns>
        internal static List<Zone> GetTeamZones(PlayerTeam team, bool includeCenter = false) {
            switch (team) {
                case PlayerTeam.Blue:
                    if (includeCenter)
                        return BLUE_TEAM_ZONES.ToList();
                    else
                        return BLUE_TEAM_DEFENSE_ZONES.ToList();

                case PlayerTeam.Red:
                    if (includeCenter)
                        return RED_TEAM_ZONES.ToList();
                    else
                        return RED_TEAM_DEFENSE_ZONES.ToList();

                default:
                    return new List<Zone> {
                        Zone.None,
                    };
            }
        }
        #endregion
    }

    #region Enums
    /// <summary>
    /// Enum of all the zones of the ice.
    /// </summary>
    public enum Zone {
        None,
        BlueTeam_BehindGoalLine,
        RedTeam_BehindGoalLine,
        BlueTeam_Zone,
        RedTeam_Zone,
        BlueTeam_Center,
        RedTeam_Center,
    }

    /// <summary>
    /// Enum of the ice elements (lines, hashmarks, etc.).
    /// </summary>
    public enum IceElement {
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

    /// <summary>
    /// Enum of the faceoff locations.
    /// </summary>
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
    #endregion
}
