using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class ZoneFunc {
        #region Constants
        /// <summary>
        /// Dictionary of ArenaElement and (double Start, double End), dictionary containing all the start and end Z positions of all the lines on the ice for the zones.
        /// </summary>
        private static readonly Dictionary<ArenaElement, (double Start, double End)> ICE_Z_POSITIONS = new Dictionary<ArenaElement, (double Start, double End)> {
            { ArenaElement.BlueTeam_BlueLine, (13.0, 13.5) },
            { ArenaElement.RedTeam_BlueLine, (-13.5, -13.0) },
            { ArenaElement.CenterLine, (-0.25, 0.25) },
            { ArenaElement.BlueTeam_GoalLine, (39.75, 40) },
            { ArenaElement.RedTeam_GoalLine, (-40, -39.75) },
        };
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

        internal static List<Zone> GetTeamZones(PlayerTeam team, bool includeCenter = false) {
            switch (team) { // TODO : Optimize with pre made lists.
                case PlayerTeam.Blue:
                    List<Zone> blueZones = new List<Zone> { Zone.BlueTeam_Zone, Zone.BlueTeam_BehindGoalLine };
                    if (includeCenter)
                        blueZones.Add(Zone.BlueTeam_Center);
                    return blueZones;

                case PlayerTeam.Red:
                    List<Zone> redZones = new List<Zone> { Zone.RedTeam_Zone, Zone.RedTeam_BehindGoalLine };
                    if (includeCenter)
                        redZones.Add(Zone.RedTeam_Center);
                    return redZones;
            }

            return new List<Zone> { Zone.None };
        }
        #endregion
    }

    #region Enums
    public enum Zone {
        None,
        RedTeam_BehindGoalLine,
        BlueTeam_BehindGoalLine,
        RedTeam_Zone,
        BlueTeam_Zone,
        RedTeam_Center,
        BlueTeam_Center,
    }

    public enum ArenaElement {
        BlueTeam_BlueLine,
        RedTeam_BlueLine,
        CenterLine,
        BlueTeam_GoalLine,
        RedTeam_GoalLine,
    }
    #endregion
}
