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
        /// OldFaceoffConfig, config related to faceoffs.
        /// </summary>
        public OldFaceoffConfig Faceoff { get; } = new OldFaceoffConfig();

        /// <summary>
        /// Int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        public int MaxTippedMilliseconds { get; } = 91;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; } = 300; // TODO : Change after release.

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; } = 700;  // TODO : Change after release.
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
        /// Double, deferred icing max possible time multiplicator.
        /// </summary>
        public double DeferredMaxPossibleTimeMultiplicator { get; } = 280d;

        /// <summary>
        /// Double, deferred icing max possible time addition (after multiplicator).
        /// </summary>
        public double DeferredMaxPossibleTimeAddition { get; } = 9500d;

        /// <summary>
        /// Float, deferred icing max possible time substraction depending of players distance to puck (after addition).
        /// </summary>
        public float DeferredMaxPossibleTimeDistanceDelta { get; } = 250f;

        /// <summary>
        /// Dictionary of Zone and float, number of milliseconds after puck exiting the stick before arriving behind the goal line to not be considered for icing for each zone.
        /// </summary>
        public Dictionary<Zone, float> MaxPossibleTime { get; } = new Dictionary<Zone, float> {
            { Zone.BlueTeam_BehindGoalLine, 9500f },
            { Zone.RedTeam_BehindGoalLine, 9500f },
            { Zone.BlueTeam_Zone, 7750f },
            { Zone.RedTeam_Zone, 7750f },
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
        public float Delta { get; } = 21.5f;

        /// <summary>
        /// Float, max height before deferred icing does not check for possibility that the other team touches the puck before icing.
        /// </summary>
        public float DeferredMaxHeight { get; } = 0.85f;

        /// <summary>
        /// Bool, true if icing team stamina has to be drained.
        /// </summary>
        public bool StaminaDrain { get; } = true;

        /// <summary>
        /// Bool, true if icing team goalie stamina has to be drained.
        /// </summary>
        public bool StaminaDrainGoalie { get; } = true;

        /// <summary>
        /// Float, amount to divide the stamina by for the team causing the icing if StaminaDrain is on.
        /// </summary>
        public float StaminaDrainDivisionAmount { get; } = 2f; // TODO : Change after release.

        /// <summary>
        /// Float, amount to remove from StaminaDrainDivisionAmount when applying additional stamina drain penalties.
        /// </summary>
        public float StaminaDrainDivisionAmountPenaltyDelta { get; } = 0.5f;

        /// <summary>
        /// Int, time between 2 icings to apply additional stamina drain penalties.
        /// </summary>
        public int StaminaDrainDivisionAmountPenaltyTime { get; } = 16; // TODO : Change after release.

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
        public float MaxHeight { get; } = Codebase.Constants.CROSSBAR_HEIGHT;

        /// <summary>
        /// Int, number of milliseconds after a high stick to not be considered.
        /// </summary>
        public int MaxMilliseconds { get; } = 8000;

        /// <summary>
        /// Float, delta used to calculate the high stick maximum frames before activation.
        /// </summary>
        public float Delta { get; } = 18f;

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
        public int PushNoGoalMilliseconds { get; } = 3500; // TODO : Change after release.

        /// <summary>
        /// Int, number of milliseconds after a hit on the goalie to be considered no goal.
        /// </summary>
        public int HitNoGoalMilliseconds { get; } = 11000;

        /// <summary>
        /// Float, force threshold for a push on the goalie to be considered for goalie interference.
        /// </summary>
        public float CollisionForceThreshold { get; } = 0.971f; // TODO : Change after release.

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie interference calls.
        /// </summary>
        public float GoalieRadius { get; } = 0.8f; // TODO : Change after release.

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
    public class OldFaceoffConfig : ISubConfig {
        /// <summary>
        /// Bool, true if the custom faceoff (any faceoff not in center) should be used.
        /// </summary>
        public bool UseCustomFaceoff { get; } = true;

        /// <summary>
        /// Bool, the game rounds down the time remaining on every faceoff.
        /// This readds 1 second on every faceoff so the game doesn't end too quickly.
        /// </summary>
        public bool ReAdd1SecondAfterFaceoff { get; } = true;

        /// <summary>
        /// Bool, true if the height of the puck drop on faceoffs shouldn't be modified.
        /// </summary>
        public bool UseDefaultPuckDropHeight { get; } = false;

        /// <summary>
        /// Float, height of the puck drop on faceoffs.
        /// </summary>
        public float PuckDropHeight { get; } = 1.1f;

        /// <summary>
        /// Bool, true if the faceoff violations module is enabled.
        /// </summary>
        public bool EnableViolations { get; } = true;

        /// <summary>
        /// Float, maximum height for the puck to be touched on faceoff.
        /// </summary>
        public float PuckIceContactHeight { get; } = 0.3f;

        /// <summary>
        /// Int, maximum of faceoff violations before getting penalized.
        /// </summary>
        public int MaxViolationsBeforePenalty { get; } = 2;

        /// <summary>
        /// Float, distance to teleport the penalized player.
        /// </summary>
        public float PenaltyFreezeDistance { get; } = 5f;

        /// <summary>
        /// Float, how long to freeze the penalized player.
        /// </summary>
        public float PenaltyFreezeDuration { get; } = 5f;

        /// <summary>
        /// Bool, true if players has to be freezed before puck drops.
        /// </summary>
        public bool FreezePlayersBeforeDrop { get; } = true;

        /// <summary>
        /// Float, number of seconds to freeze players before faceoff ends.
        /// </summary>
        public float FreezeBeforeDropTime { get; } = 2.999f;

        // Center position settings
        public float CenterMaxForward { get; } = 0;       // Centers can't move forward at all
        public float CenterMaxBackward { get;} = 2f;    // Backward wall
        public float CenterMaxLeft { get; } = 1f;        // Limited side movement
        public float CenterMaxRight { get; } = 1f;

        // Winger settings
        public float WingerMaxForward { get; } = 1f;     // Wingers can move forward a bit
        public float WingerMaxBackward { get; } = 2f;    // Backward wall
        public float WingerMaxToward { get; } = 0;      // Limited movement toward center (inward wall)
        public float WingerMaxAway { get; } = 5f;       // More movement away from center (outward wall toward boards)

        // Defense settings
        public float DefenseMaxForward { get; } = 0;      // Defense can't move forward at all
        public float DefenseMaxBackward { get; } = 0f;   // Backward wall
        public float DefenseMaxToward { get; } = 5f;     // Movement toward center
        public float DefenseMaxAway { get; } = 5f;      // Movement away from center (toward boards)

        // Goalie settings
        public float GoalieMaxForward { get; } = 2f;     // Minimal forward movement
        public float GoalieMaxBackward { get; } = 2f;    // Backward wall
        public float GoalieMaxLeft { get; } = 2f;
        public float GoalieMaxRight { get; } = 2f;

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
