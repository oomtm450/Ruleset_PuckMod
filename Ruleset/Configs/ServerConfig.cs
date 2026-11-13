using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace oomtm450PuckMod_Ruleset.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_ruleset_serverconfig.json used for this mod.
    /// </summary>
    public class ServerConfig : IConfig, ISubConfig {
        #region Constants
        /// <summary>
        /// Const string, name used when sending the config data to the client.
        /// </summary>
        [JsonIgnore]
        public const string CONFIG_DATA_NAME = Constants.MOD_NAME + "_config";

        /// <summary>
        /// String, full path for the config folder.
        /// </summary>
        [JsonIgnore]
        private static readonly string CONFIG_FOLDER_PATH = Path.Combine(Path.GetFullPath("."), "config");

        /// <summary>
        /// String, full path for the config file.
        /// </summary>
        [JsonIgnore]
        private static readonly string CONFIG_PATH = Path.Combine(CONFIG_FOLDER_PATH, Constants.MOD_NAME + "_serverconfig.json");

        [JsonIgnore]
        internal const float DEFAULT_PLAYER_HEIGHT = 0.0338f; // TODO : Check in new Puck build.
        #endregion

        #region Properties
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        
        public bool LogInfo { get; set; } = true;

        /// <summary>
        /// String, name of the mod.
        /// </summary>
        [JsonIgnore]
        public string ModName { get; } = Constants.MOD_NAME;

        /// <summary>
        /// Bool, true if the numeric values has to be replaced be the default ones. Make this false to use custom values.
        /// </summary>
        public bool UseDefaultNumericValues { get; set; } = true;

        /// <summary>
        /// FaceoffConfig, config related to faceoffs.
        /// </summary>
        public FaceoffConfig Faceoff { get; set; } = new FaceoffConfig();

        /// <summary>
        /// OffsideConfig, config related to offsides.
        /// </summary>
        public OffsideConfig Offside { get; set; } = new OffsideConfig();

        /// <summary>
        /// IcingConfig, config related to icings.
        /// </summary>
        public IcingConfig Icing { get; set; } = new IcingConfig();

        /// <summary>
        /// HighStickConfig, config related to high sticks.
        /// </summary>
        public HighStickConfig HighStick { get; set; } = new HighStickConfig();

        /// <summary>
        /// GIntConfig, config related to goalie interferences.
        /// </summary>
        public GIntConfig GInt { get; set; } = new GIntConfig();

        /// <summary>
        /// PenaltyConfig, config related to penalties.
        /// </summary>
        public PenaltyConfig Penalty { get; set; } = new PenaltyConfig();

        /// <summary>
        /// Int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        public int MaxTippedMilliseconds { get; set; } = 31;

        /// <summary>
        /// Float, puck speed tipping ratio.
        /// </summary>
        public float PuckSpeedTippingRatio { get; set; } = 0.025f;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; set; } = 333;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; set; } = 1250;

        /// <summary>
        /// Bool, authorize ref mode to be voted or activated by an admin.
        /// </summary>
        public bool RefMode { get; set; } = true;

        /// <summary>
        /// Int, number of games a newly added ref will be doing before being automaticaly removed.
        /// </summary>
        public int RefModeGameAmount { get; set; } = 2;

        /// <summary>
        /// Float, default standing player height.
        /// </summary>
        public float DefaultPlayerHeight { get; set; } = DEFAULT_PLAYER_HEIGHT;

        /// <summary>
        /// Bool, true the mod has to log all phase changes and stoppages.
        /// </summary>
        public bool LogPhaseChangeAndStoppage { get; set; } = true;

        /// <summary>
        /// Bool, true if the glass barriers has to be lowered.
        /// </summary>
        public bool LowerBarriers { get; set; } = true;

        /// <summary>
        /// Float, Y offset to use when teleporting/spawning players and pucks related to Ruleset.
        /// </summary>
        public float YOffsetForTeleport { get; set; } = 0.01f;

        /// <summary>
        /// Bool, true if the out of bounds looping bug has to be fixed.
        /// </summary>
        public bool FixOutOfBoundsLooping { get; set; } = true;
        #endregion

        #region Constructors
        /// <summary>
        /// Default constructor of ServerConfig.
        /// </summary>
        public ServerConfig() { }

        /// <summary>
        /// Copy constructor of ServerConfig.
        /// </summary>
        /// <param name="serverConfig">ServerConfig, config to copy.</param>
        public ServerConfig(ServerConfig serverConfig) {
            ConfigUpdater.CopyProperties(serverConfig, this);
        }
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldServerConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldServerConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new ServerConfig(), new List<string> {
                nameof(IConfig.LogInfo),
            });
        }

        /// <summary>
        /// Function that serialize the config object.
        /// </summary>
        /// <returns>String, serialized config.</returns>
        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Function that unserialize a ServerConfig.
        /// </summary>
        /// <param name="json">String, JSON that is the serialized ServerConfig.</param>
        /// <returns>ServerConfig, unserialized ServerConfig.</returns>
        internal static ServerConfig SetConfig(string json) {
            return JsonConvert.DeserializeObject<ServerConfig>(json);
        }

        /// <summary>
        /// Function that reads the config file for the mod and create a ServerConfig object with it.
        /// Also creates the file with the default values, if it doesn't exists.
        /// </summary>
        /// <returns>ServerConfig, parsed config.</returns>
        internal static ServerConfig ReadConfig() {
            ServerConfig config = new Configs.ServerConfig();

            try {
                if (!Directory.Exists(CONFIG_FOLDER_PATH))
                    Directory.CreateDirectory(CONFIG_FOLDER_PATH);

                if (File.Exists(CONFIG_PATH)) {
                    string configFileContent = File.ReadAllText(CONFIG_PATH);
                    config = SetConfig(configFileContent);
                    Logging.Log($"Server config read.", config, true);
                }

                config.UpdateDefaultValues(new OldServerConfig());

                try {
                    File.WriteAllText(CONFIG_PATH, config.ToString());
                }
                catch (Exception ex) {
                    Logging.LogError($"Can't write the server config file. (Permission error ?)\n{ex}", new Configs.ServerConfig());
                }

                Logging.Log($"Wrote server config : {config}", config, true);

                if (config.UseDefaultNumericValues) {
                    PenaltyConfig penaltyConfig;
                    if (config.Penalty.UseDefaultNumericValues) {
                        penaltyConfig = new PenaltyConfig {
                            Interference = config.Penalty.Interference,
                            GoalieInterference = config.Penalty.GoalieInterference,
                            DelayOfGame = config.Penalty.DelayOfGame,
                            FaceoffViolation = config.Penalty.FaceoffViolation,
                            Embellishment = config.Penalty.Embellishment,
                            Roughing = config.Penalty.Roughing,
                            Charging = config.Penalty.Charging,
                            Slashing = config.Penalty.Slashing,
                            HighStick = config.Penalty.HighStick,
                            Hooking = config.Penalty.Hooking,
                            UseDefaultNumericValues = config.Penalty.UseDefaultNumericValues,
                        };
                    }
                    else
                        penaltyConfig = config.Penalty;

                    ServerConfig defaultConfig = new Configs.ServerConfig {
                        LogInfo = config.LogInfo,
                        UseDefaultNumericValues = config.UseDefaultNumericValues,
                        RefMode = config.RefMode,
                        GInt = new GIntConfig {
                            BlueTeam = config.GInt.BlueTeam,
                            RedTeam = config.GInt.RedTeam,
                        },
                        Offside = new OffsideConfig {
                            BlueTeam = config.Offside.BlueTeam,
                            RedTeam = config.Offside.RedTeam,
                        },
                        Icing = new IcingConfig {
                            BlueTeam = config.Icing.BlueTeam,
                            RedTeam = config.Icing.RedTeam,
                            Deferred = config.Icing.Deferred,
                        },
                        HighStick = new HighStickConfig {
                            BlueTeam = config.HighStick.BlueTeam,
                            RedTeam = config.HighStick.RedTeam,
                            HandPassBlueTeam = config.HighStick.HandPassBlueTeam,
                            HandPassRedTeam = config.HighStick.HandPassRedTeam,
                        },
                        Penalty = penaltyConfig,
                        Faceoff = new FaceoffConfig {
                            EnableViolations = config.Faceoff.EnableViolations,
                            FreezeSkatersBeforeDrop = config.Faceoff.FreezeSkatersBeforeDrop,
                            FreezeGoaliesBeforeDrop = config.Faceoff.FreezeGoaliesBeforeDrop,
                            UseCustomFaceoff = config.Faceoff.UseCustomFaceoff,
                            UseDefaultPuckDropHeight = config.Faceoff.UseDefaultPuckDropHeight,
                            ResetPlayersOnFaceoff = config.Faceoff.ResetPlayersOnFaceoff,
                        },
                        LogPhaseChangeAndStoppage = config.LogPhaseChangeAndStoppage,
                        LowerBarriers = config.LowerBarriers,
                        FixOutOfBoundsLooping = config.FixOutOfBoundsLooping,
                    };

                    config = defaultConfig;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Can't read the server config file/folder. (Permission error ?)\n{ex}", new Configs.ServerConfig());
            }

            return config;
        }
        #endregion
    }

    /// <summary>
    /// Class containing the config for penalties.
    /// </summary>
    public class PenaltyConfig : ISubConfig {
        #region Properties
        /// <summary>
        /// Int, max number of penalties given to one player.
        /// </summary>
        public int MaxPenaltiesCountPerPlayer { get; set; } = 2;
        /// <summary>
        /// Int, max number of penalized players per team.
        /// </summary>
        public int MaxPenalizedPlayersPerTeam { get; set; } = 2;
        /// <summary>
        /// Int, maximum number of players per team where penalties don't count.
        /// </summary>
        public int MaximumPenaltyImmunedPlayersCountPerTeam { get; set; } = 2;

        /// <summary>
        /// Bool, true if player interference is enabled.
        /// </summary>
        public bool Interference { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a player interference penalty in milliseconds.
        /// </summary>
        public int InterferenceTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;
        /// <summary>
        /// Int, interference can be called after this number of milliseconds after touching the puck.
        /// </summary>
        public int InterferenceMillisecondsThreshold { get; set; } = 1600;
        /// <summary>
        /// Int, interference can be called after this number of milliseconds after the player hit fell.
        /// </summary>
        public int InterferenceOnSamePlayerMillisecondsThreshold { get; set; } = 5000;
        /// <summary>
        /// Float, minimum y for a hit to be considered.
        /// </summary>
        public float JumpHeightMinimum { get; set; } = 0.044f;

        /// <summary>
        /// Bool, true if goalie interference is enabled.
        /// </summary>
        public bool GoalieInterference { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a goalie interference penalty in milliseconds.
        /// </summary>
        public int GoalieInterferenceTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;

        /// <summary>
        /// Int, time for a late slip/fall to be considered as interference against the opposing player.
        /// </summary>
        public int LateInterferenceTimeThreshold { get; set; } = 500;

        /// <summary>
        /// Bool, true if delay of game is enabled and the invisible wall has to be lowered.
        /// </summary>
        public bool DelayOfGame { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a delay of game penalty in milliseconds.
        /// </summary>
        public int DelayOfGameTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;
        /// <summary>
        /// Float, delta of the puck Z direction to use with the delay of game.
        /// </summary>
        public float DelayOfGameZDelta { get; set; } = 0.0125f;
        /// <summary>
        /// Int, delay of game can be called if someone didn't touch the puck this number of milliseconds on the other team.
        /// </summary>
        public int DelayOfGameMillisecondsThreshold { get; set; } = 25;
        /// <summary>
        /// Int, delay of game can be called after this number of milliseconds after a faceoff since the last touch.
        /// </summary>
        public int DelayOfGameFaceoffProtectionMilliseconds { get; set; } = 2250;

        /// <summary>
        /// Bool, true if faceoff violation penalty is enabled.
        /// </summary>
        public bool FaceoffViolation { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a faceoff violation penalty in milliseconds.
        /// </summary>
        public int FaceoffViolationTime { get; set; } = PenaltyModule.SHORT_PENALTY_TIME_MS;

        /// <summary>
        /// Bool, true if embellishment penalty is enabled.
        /// </summary>
        public bool Embellishment { get; set; } = true;
        /// <summary>
        /// Int, time in the box for an embellishment penalty in milliseconds.
        /// </summary>
        public int EmbellishmentTime { get; set; } = PenaltyModule.SHORT_PENALTY_TIME_MS;
        /// <summary>
        /// Int, embellishment can be called after this number of milliseconds after the player gets up.
        /// </summary>
        public int EmbellishmentMillisecondsThreshold { get; set; } = 3250;
        /// <summary>
        /// Int, chance for an embellishment to be called. (2 is equal to 50%. 1 / 50 * 100 = 2)
        /// </summary>
        public int EmbellishmentChancePercInverse { get; set; } = 2;

        /// <summary>
        /// Bool, true if roughing penalty is enabled. (Embellishment revenge call, can't be called if Embellishment is off)
        /// </summary>
        public bool Roughing { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a roughing penalty in milliseconds.
        /// </summary>
        public int RoughingTime { get; set; } = PenaltyModule.SHORT_PENALTY_TIME_MS;
        /// <summary>
        /// Int, roughing can be called after this number of milliseconds after the player gets up.
        /// </summary>
        public int RoughingMillisecondsThreshold { get; set; } = 6500;
        /// <summary>
        /// Int, chance for an roughing to be called. (2 is equal to 50%. 1 / 50 * 100 = 2)
        /// </summary>
        public int RoughingChancePercInverse { get; set; } = 2;

        /// <summary>
        /// Bool, true if charging penalty is enabled.
        /// </summary>
        public bool Charging { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a charging penalty in milliseconds.
        /// </summary>
        public int ChargingTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;
        /// <summary>
        /// Float, skater's speed threshold to call a charging penalty.
        /// </summary>
        public float ChargingSpeedThreshold { get; set; } = 8.69f;
        /// <summary>
        /// Int, skater's last sprint timespan threshold to call a charging penalty in milliseconds.
        /// </summary>
        public int ChargingLastSprintTimeThreshold { get; set; } = 500;
        /// <summary>
        /// Int, skater's last sprint minimum total time to call a charging penalty in milliseconds.
        /// </summary>
        public int ChargingMinimumTotalSprintTime { get; set; } = 1400;

        /// <summary>
        /// Bool, true if slashing is enabled.
        /// </summary>
        public bool Slashing { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a slashing penalty in milliseconds.
        /// </summary>
        public int SlashingTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;
        /// <summary>
        /// Int, time between allowed slashes in milliseconds.
        /// </summary>
        public int SlashingAllowedHitMillisecondsThreshold { get; set; } = 400;
        /// <summary>
        /// Float, slash force threshold to consider a slashing penalty.
        /// </summary>
        public float SlashingForceThreshold { get; set; } = 2f;

        /// <summary>
        /// Bool, true if high stick penalty is enabled.
        /// </summary>
        public bool HighStick { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a high stick penalty in milliseconds.
        /// </summary>
        public int HighStickTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;
        /// <summary>
        /// Float, high stick force threshold to consider a slashing penalty.
        /// </summary>
        public float HighStickForceThreshold { get; set; } = 2f;

        /// <summary>
        /// Bool, true if hooking is enabled.
        /// </summary>
        public bool Hooking { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a hooking penalty in milliseconds.
        /// </summary>
        public int HookingTime { get; set; } = PenaltyModule.LONG_PENALTY_TIME_MS;
        /// <summary>
        /// Float, hooking force threshold to consider a hooking penalty.
        /// </summary>
        public float HookingForceThreshold { get; set; } = 2f;

        /// <summary>
        /// Bool, true if the numeric values has to be replaced be the default ones. Make this false to use custom values.
        /// </summary>
        public bool UseDefaultNumericValues { get; set; } = true;
        #endregion

        #region Constructors
        /// <summary>
        /// Default constructor of PenaltyConfig.
        /// </summary>
        public PenaltyConfig() { }

        /// <summary>
        /// Copy constructor of PenaltyConfig.
        /// </summary>
        /// <param name="penaltyConfig">PenaltyConfig, config to copy.</param>
        public PenaltyConfig(PenaltyConfig penaltyConfig) {
            ConfigUpdater.CopyProperties(penaltyConfig, this);
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldPenaltyConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldPenaltyConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new PenaltyConfig());
        }
    }

    /// <summary>
    /// Class containing the config for offsides.
    /// </summary>
    public class OffsideConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team offsides are activated.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if red team offsides are activated.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team intentional offsides are activated.
        /// </summary>
        public bool IntentionalOffsideBlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if red team intentional offsides are activated.
        /// </summary>
        public bool IntentionalOffsideRedTeam { get; set; } = true;

        /// <summary>
        /// Int, threshold for an intentional offside to be called in milliseconds.
        /// </summary>
        public int IntentionalOffsideMillisecondsThreshold { get; set; } = 3750;

        #region Constructors
        /// <summary>
        /// Default constructor of OffsideConfig.
        /// </summary>
        public OffsideConfig() { }

        /// <summary>
        /// Copy constructor of OffsideConfig.
        /// </summary>
        /// <param name="offsideConfig">OffsideConfig, config to copy.</param>
        public OffsideConfig(OffsideConfig offsideConfig) {
            ConfigUpdater.CopyProperties(offsideConfig, this);
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldOffsideConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldOffsideConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new OffsideConfig());
        }
    }

    /// <summary>
    /// Class containing the config for icings.
    /// </summary>
    public class IcingConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team icings are activated.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if red team icings are activated.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if deferred icing is activated. If false, icing will be called when the puck is touched.
        /// </summary>
        public bool Deferred { get; set; } = true;

        /// <summary>
        /// Double, deferred icing max possible time multiplicator.
        /// </summary>
        public double DeferredMaxPossibleTimeMultiplicator { get; set; } = 310d;

        /// <summary>
        /// Double, deferred icing max possible time addition (after multiplicator).
        /// </summary>
        public double DeferredMaxPossibleTimeAddition { get; set; } = 10350d;

        /// <summary>
        /// Float, deferred icing max possible time substraction depending of players distance to puck (after addition).
        /// </summary>
        public float DeferredMaxPossibleTimeDistanceDelta { get; set; } = 164f;

        /// <summary>
        /// Dictionary of Zone and float, number of milliseconds after puck exiting the stick before arriving behind the goal line to not be considered for icing for each zone.
        /// </summary>
        public Dictionary<Codebase.Zone, float> MaxPossibleTime { get; set; } = new Dictionary<Codebase.Zone, float> {
            { Codebase.Zone.BlueTeam_BehindGoalLine, 10350f },
            { Codebase.Zone.RedTeam_BehindGoalLine, 10350f },
            { Codebase.Zone.BlueTeam_Zone, 8375f },
            { Codebase.Zone.RedTeam_Zone, 8375f },
            { Codebase.Zone.BlueTeam_Center, 6075f },
            { Codebase.Zone.RedTeam_Center, 6075f },
        };

        /// <summary>
        /// Int, number of milliseconds for icing to be called off if it has not being called.
        /// </summary>
        public int MaxActiveTime { get; set; } = 12000;

        /// <summary>
        /// Float, delta used to calculate the dynamic icing possible times.
        /// </summary>
        public float Delta { get; set; } = 22f;

        /// <summary>
        /// Float, max height before deferred icing does not check for possibility that the other team touches the puck before icing.
        /// </summary>
        public float DeferredMaxHeight { get; set; } = 0.75f;

        /// <summary>
        /// Bool, true if icing team stamina has to be drained.
        /// </summary>
        public bool StaminaDrain { get; set; } = true;

        /// <summary>
        /// Bool, true if icing team goalie stamina has to be drained.
        /// </summary>
        public bool StaminaDrainGoalie { get; set; } = true;

        /// <summary>
        /// Float, amount to divide the stamina by for the team causing the icing if StaminaDrain is on.
        /// </summary>
        public float StaminaDrainDivisionAmount { get; set; } = 2.5f;

        /// <summary>
        /// Float, amount to remove from StaminaDrainDivisionAmount when applying additional stamina drain penalties.
        /// </summary>
        public float StaminaDrainDivisionAmountPenaltyDelta { get; set; } = 0.5f;

        /// <summary>
        /// Float, amount to divide the stamina by for the team causing the icing if StaminaDrain is on.
        /// </summary>
        public float StaminaDrainDivisionAmountGoalie { get; set; } = 2f;

        /// <summary>
        /// Float, amount to remove from StaminaDrainDivisionAmount when applying additional stamina drain penalties.
        /// </summary>
        public float StaminaDrainDivisionAmountPenaltyDeltaGoalie { get; set; } = 1f;

        /// <summary>
        /// Int, time in period seconds between 2 icings to apply additional stamina drain penalties.
        /// </summary>
        public int StaminaDrainDivisionAmountPenaltyTime { get; set; } = 21;

        #region Constructors
        /// <summary>
        /// Default constructor of IcingConfig.
        /// </summary>
        public IcingConfig() { }

        /// <summary>
        /// Copy constructor of IcingConfig.
        /// </summary>
        /// <param name="icingConfig">IcingConfig, config to copy.</param>
        public IcingConfig(IcingConfig icingConfig) {
            ConfigUpdater.CopyProperties(icingConfig, this);
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldIcingConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldIcingConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new IcingConfig());
        }
    }

    /// <summary>
    /// Class containing the config for high sticks.
    /// </summary>
    public class HighStickConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team high stick are activated.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if red team high stick are activated.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Float, base height before hitting the puck with a stick is considered high stick.
        /// </summary>
        public float MaxHeight { get; set; } = Codebase.Constants.SHOULDERS_HEIGHT;

        /// <summary>
        /// Float, base height before hitting the puck with a stick is considered high stick for a no goal situation.
        /// </summary>
        public float MaxHeightNoGoal { get; set; } = Codebase.Constants.CROSSBAR_HEIGHT;

        /// <summary>
        /// Int, number of milliseconds after a high stick to call high stick if no one touches the puck.
        /// </summary>
        public int MaxMilliseconds { get; set; } = 8000;

        /// <summary>
        /// Float, delta used to calculate the high stick maximum frames before activation.
        /// </summary>
        public float Delta { get; set; } = 20f;

        /// <summary>
        /// Bool, true to make a jumping high stick a hand pass situation for blue team.
        /// </summary>
        public bool HandPassBlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true to make a jumping high stick a hand pass situation for red team.
        /// </summary>
        public bool HandPassRedTeam { get; set; } = true;

        #region Constructors
        /// <summary>
        /// Default constructor of HighStickConfig.
        /// </summary>
        public HighStickConfig() { }

        /// <summary>
        /// Copy constructor of HighStickConfig.
        /// </summary>
        /// <param name="highStickConfig">HighStickConfig, config to copy.</param>
        public HighStickConfig(HighStickConfig highStickConfig) {
            ConfigUpdater.CopyProperties(highStickConfig, this);
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldHighStickConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldHighStickConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new HighStickConfig());
        }
    }

    /// <summary>
    /// Class containing the config for goalie interferences.
    /// </summary>
    public class GIntConfig : ISubConfig {
        /// <summary>
        /// Bool, true if blue team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if red team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Int, number of milliseconds after a push on the goalie to be considered no goal.
        /// </summary>
        public int PushNoGoalMilliseconds { get; set; } = 3350;

        /// <summary>
        /// Float, force threshold for a push on the goalie to be considered for goalie interference.
        /// </summary>
        public float CollisionForceThreshold { get; set; } = 0.9675f;

        /// <summary>
        /// Int, number of milliseconds between each player push to be considered when finding blame for a goalie push.
        /// </summary>
        public int CollisionTimeThreshold { get; set; } = 400;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie interference calls.
        /// </summary>
        public float GoalieRadius { get; set; } = 0.885f;

        #region Constructors
        /// <summary>
        /// Default constructor of GIntConfig.
        /// </summary>
        public GIntConfig() { }

        /// <summary>
        /// Copy constructor of GIntConfig.
        /// </summary>
        /// <param name="gIntConfig">GIntConfig, config to copy.</param>
        public GIntConfig(GIntConfig gIntConfig) {
            ConfigUpdater.CopyProperties(gIntConfig, this);
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldGIntConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldGIntConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new GIntConfig());
        }
    }

    /// <summary>
    /// Class containing the config for faceoffs.
    /// </summary>
    public class FaceoffConfig : ISubConfig {
        /// <summary>
        /// Bool, true if the custom faceoff (any faceoff not in center) should be used.
        /// </summary>
        public bool UseCustomFaceoff { get; set; } = true;

        /// <summary>
        /// Bool, true if the height of the puck drop on faceoffs shouldn't be modified.
        /// </summary>
        public bool UseDefaultPuckDropHeight { get; set; } = false;

        /// <summary>
        /// Float, height of the puck drop on faceoffs.
        /// </summary>
        public float PuckDropHeight { get; set; } = 1.1f;

        /// <summary>
        /// Bool, true if the faceoff violations module is enabled.
        /// </summary>
        public bool EnableViolations { get; set; } = true;

        /// <summary>
        /// Float, maximum height for the puck to be touched on faceoff.
        /// </summary>
        public float PuckIceContactHeight { get; set; } = 0.205f;

        /// <summary>
        /// Int, maximum of faceoff violations before getting penalized.
        /// </summary>
        public int MaxViolationsBeforePenalty { get; set; } = 2;

        /// <summary>
        /// Float, distance to teleport the penalized player.
        /// </summary>
        public float PenaltyFreezeDistance { get; set; } = 5f;

        /// <summary>
        /// Float, how long to freeze the penalized player.
        /// </summary>
        public float PenaltyFreezeDuration { get; set; } = 5f;

        /// <summary>
        /// Bool, true if skaters has to be freezed before puck drops.
        /// </summary>
        public bool FreezeSkatersBeforeDrop { get; set; } = true;

        /// <summary>
        /// Bool, true if goalies has to be freezed before puck drops.
        /// </summary>
        public bool FreezeGoaliesBeforeDrop { get; set; } = false;

        /// <summary>
        /// Float, number of seconds to freeze players before faceoff ends.
        /// </summary>
        public float FreezeBeforeDropTime { get; set; } = 2f;

        // Center position settings
        public float CenterMaxForward { get; set; } = 0;      // Centers can't move forward at all
        public float CenterMaxBackward { get; set; } = 1.8f;    // Backward wall
        public float CenterMaxLeft { get; set; } = 1f;        // Limited side movement
        public float CenterMaxRight { get; set; } = 1f;

        // Winger settings
        public float WingerMaxForward { get; set; } = 0.6f;     // Wingers can move forward a bit
        public float WingerMaxBackward { get; set; } = 1.8f;    // Backward wall
        public float WingerMaxToward { get; set; } = 0;       // Limited movement toward center (inward wall)
        public float WingerMaxAway { get; set; } = 2.3f;      // More movement away from center (outward wall toward boards)

        // Defense settings
        public float DefenseMaxForward { get; set; } = 0;     // Defense can't move forward at all
        public float DefenseMaxBackward { get; set; } = 0;    // Backward wall
        public float DefenseMaxToward { get; set; } = 2.4f;   // Movement toward center
        public float DefenseMaxAway { get; set; } = 2.4f;     // Movement away from center (toward boards)

        // Goalie settings
        public float GoalieMaxForward { get; set; } = 2f;
        public float GoalieMaxBackward { get; set; } = 2.1f;
        public float GoalieMaxLeft { get; set; } = 2.2f;
        public float GoalieMaxRight { get; set; } = 2.2f;

        /// <summary>
        /// Bool, reset some values on faceoff, like stamina.
        /// </summary>
        public bool ResetPlayersOnFaceoff { get; set; } = true;

        #region Constructors
        /// <summary>
        /// Default constructor of ServerConfig.
        /// </summary>
        public FaceoffConfig() { }

        /// <summary>
        /// Copy constructor of ServerConfig.
        /// </summary>
        /// <param name="faceoffConfig">FaceoffConfig, config to copy.</param>
        public FaceoffConfig(FaceoffConfig faceoffConfig) {
            ConfigUpdater.CopyProperties(faceoffConfig, this);
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldFaceoffConfig))
                throw new ArgumentException($"{nameof(oldConfig)} has to be typeof {nameof(OldFaceoffConfig)}.", nameof(oldConfig));

            ConfigUpdater.UpdateDefaults(this, oldConfig, new FaceoffConfig());
        }
    }

    internal static class ConfigUpdater {
        /// <summary>
        /// For every property of the current config that also exists (same name and type) in the old config:
        /// if the current value equals the old default, replace it with the new default.
        /// Sub-configs (ISubConfig) are recursed into.
        /// </summary>
        internal static void UpdateDefaults(object current, object oldDefaults, object newDefaults, List<string> propertiesToSkip = null) {
            foreach (PropertyInfo prop in current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (!prop.CanRead || !prop.CanWrite && !IsSubConfig(prop))
                    continue;

                // Skip [JsonIgnore]-style/non-config properties if needed.
                PropertyInfo oldProp = oldDefaults.GetType().GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
                if (oldProp == null || oldProp.PropertyType != prop.PropertyType)
                    continue; // Property didn't exist in the old config (new property): keep as is.

                if (propertiesToSkip != null) {
                    foreach (string property in propertiesToSkip) {
                        if (prop.Name == property)
                            continue;
                    }
                }

                object currentValue = prop.GetValue(current);
                object oldValue = oldProp.GetValue(oldDefaults);
                object newValue = prop.GetValue(newDefaults);

                // Nested config: recurse (works even with get-only properties).
                if (currentValue is Codebase.Configs.ISubConfig && oldValue != null && newValue != null) {
                    UpdateDefaults(currentValue, oldValue, newValue);
                    continue;
                }

                if (!prop.CanWrite)
                    continue;

                // Dictionaries: compare per key (replaces your MaxPossibleTime loop).
                if (currentValue is IDictionary currentDict && oldValue is IDictionary oldDict && newValue is IDictionary newDict) {
                    foreach (object key in currentDict.Keys.Cast<object>().ToList()) {
                        if (oldDict.Contains(key) && newDict.Contains(key) && Equals(currentDict[key], oldDict[key]))
                            currentDict[key] = newDict[key];
                    }
                    continue;
                }

                if (Equals(currentValue, oldValue))
                    prop.SetValue(current, newValue);
            }
        }

        private static bool IsSubConfig(PropertyInfo prop) => typeof(Codebase.Configs.ISubConfig).IsAssignableFrom(prop.PropertyType);

        internal static void CopyProperties(object source, object target) {
            foreach (PropertyInfo prop in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                object value = prop.GetValue(source);
                if (value is IDictionary dict) {
                    // Shallow clone dictionaries.
                    value = Activator.CreateInstance(value.GetType(), dict);
                }
                else if (value is Codebase.Configs.ISubConfig) {
                    object clone = Activator.CreateInstance(value.GetType());
                    CopyProperties(value, clone);
                    value = clone;
                }

                prop.SetValue(target, value);
            }
        }
    }
}
