using Codebase;
using Codebase.Configs;
using System.Collections.Generic;

namespace oomtm450PuckMod_Ruleset.Configs {
    /// <summary>
    /// Class containing the old configuration from oomtm450_ruleset_serverconfig.json used for this mod.
    /// </summary>
    public class OldServerConfig : ISubConfig {
        #region Properties
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; } = true;

        /// <summary>
        /// Bool, true if the custom faceoff (any faceoff not in center) should be used.
        /// </summary>
        public bool UseCustomFaceoff { get; } = true;

        /// <summary>
        /// Bool, the game rounds down the time remaining on every faceoff.
        /// This readds 1 second on every faceoff so the game doesn't end too quickly.
        /// </summary>
        public bool ReAdd1SecondAfterFaceoff { get; set; } = true;

        /// <summary>
        /// Bool, true if the height of the puck drop on faceoffs shouldn't be modified.
        /// </summary>
        public bool UseDefaultPuckDropHeight { get; } = false;

        /// <summary>
        /// Float, height of the puck drop on faceoffs.
        /// </summary>
        public float PuckDropHeight { get; } = 1.1f;

        /// <summary>
        /// OldOffsideConfig, config related to offsides.
        /// </summary>
        public OldOffsideConfig Offside { get; } = new OldOffsideConfig();

        /// <summary>
        /// OldIcingConfig, config related to icings.
        /// </summary>
        public OldIcingConfig Icing { get; } = new OldIcingConfig();

        /// <summary>
        /// OldHighStickConfig, config related to high sticks.
        /// </summary>
        public OldHighStickConfig HighStick { get; } = new OldHighStickConfig();

        /// <summary>
        /// OldGIntConfig, config related to goalie interferences.
        /// </summary>
        public OldGIntConfig GInt { get; } = new OldGIntConfig();

        /// <summary>
        /// OldPenaltyConfig, config related to penalties.
        /// </summary>
        public OldPenaltyConfig Penalty { get; } = new OldPenaltyConfig();

        /// <summary>
        /// Int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        public int MaxTippedMilliseconds { get; } = 91;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; } = 300;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; } = 700;
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
        #endregion
    }

    /// <summary>
    /// Class containing the old config for penalties.
    /// </summary>
    public class OldPenaltyConfig : ISubConfig {
        /// <summary>
        /// Int, interference can be called after this number of milliseconds after touching the puck.
        /// </summary>
        public int InterferenceMillisecondsThreshold { get; } = 2000;

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Class containing the old config for offsides.
    /// </summary>
    public class OldOffsideConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team offsides are activated.
        /// </summary>
        public bool BlueTeam { get; } = true;

        /// <summary>
        /// Bool, true if red team offsides are activated.
        /// </summary>
        public bool RedTeam { get; } = true;

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Class containing the old config for icings.
    /// </summary>
    public class OldIcingConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team icings are activated.
        /// </summary>
        public bool BlueTeam { get; } = true;

        /// <summary>
        /// Bool, true if red team icings are activated.
        /// </summary>
        public bool RedTeam { get; } = true;

        /// <summary>
        /// Bool, true if deferred icing is activated. If false, icing will be called when the puck is touched.
        /// </summary>
        public bool Deferred { get; } = true;

        /// <summary>
        /// Dictionary of Zone and float, number of milliseconds after puck exiting the stick before arriving behind the goal line to not be considered for icing for each zone.
        /// </summary>
        public Dictionary<Zone, float> MaxPossibleTime { get; } = new Dictionary<Zone, float> {
            { Zone.BlueTeam_BehindGoalLine, 9250f },
            { Zone.RedTeam_BehindGoalLine, 9250f },
            { Zone.BlueTeam_Zone, 7500f },
            { Zone.RedTeam_Zone, 7500f },
            { Zone.BlueTeam_Center, 5500f },
            { Zone.RedTeam_Center, 5500f },
        };

        /// <summary>
        /// Int, number of milliseconds for icing to be called off if it has not being called.
        /// </summary>
        public int MaxActiveTime { get; } = 12000;

        /// <summary>
        /// Float, delta used to calculate the dynamic icing possible times.
        /// </summary>
        public float Delta { get; set; } = 21.5f;

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Class containing the old config for high sticks.
    /// </summary>
    public class OldHighStickConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team high stick are activated.
        /// </summary>
        public bool BlueTeam { get; } = true;

        /// <summary>
        /// Bool, true if red team high stick are activated.
        /// </summary>
        public bool RedTeam { get; } = true;

        /// <summary>
        /// Float, base height before hitting the puck with a stick is considered high stick.
        /// </summary>
        public float MaxHeight { get; } = 1.79f; // TODO : Change after release.

        /// <summary>
        /// Int, number of milliseconds after a high stick to not be considered.
        /// </summary>
        public int MaxMilliseconds { get; } = 5000;

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Class containing the old config for goalie interferences.
    /// </summary>
    public class OldGIntConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool BlueTeam { get; } = true;

        /// <summary>
        /// Bool, true if red team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool RedTeam { get; } = true;

        /// <summary>
        /// Int, number of milliseconds after a push on the goalie to be considered no goal.
        /// </summary>
        public int PushNoGoalMilliseconds { get; } = 3500;

        /// <summary>
        /// Int, number of milliseconds after a hit on the goalie to be considered no goal.
        /// </summary>
        public int HitNoGoalMilliseconds { get; } = 11000;

        /// <summary>
        /// Float, force threshold for a push on the goalie to be considered for goalie interference.
        /// </summary>
        public float CollisionForceThreshold { get; } = 0.971f;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie interference calls.
        /// </summary>
        public float GoalieRadius { get; } = 0.8f;

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        /// <exception cref="System.NotImplementedException">The old configs UpdateDefaultValues are not to be used.</exception>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            throw new System.NotImplementedException();
        }
    }
}
