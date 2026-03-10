using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace oomtm450PuckMod_Ruleset.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_ruleset_serverconfig.json used for this mod.
    /// </summary>
    public class ServerConfig : IConfig, ISubConfig {
        #region Constants
        /// <summary>
        /// Const string, name used when sending the config data to the client.
        /// </summary>
        public const string CONFIG_DATA_NAME = Constants.MOD_NAME + "_config";
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
        public int MaxTippedMilliseconds { get; set; } = 44;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; set; } = 400;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; set; } = 850;

        /// <summary>
        /// Bool, authorize ref mode to be voted or activated by an admin.
        /// </summary>
        public bool RefMode { get; set; } = true;
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
            LogInfo = serverConfig.LogInfo;
            ModName = serverConfig.ModName;
            UseDefaultNumericValues = serverConfig.UseDefaultNumericValues;
            Faceoff = new FaceoffConfig(serverConfig.Faceoff);
            Offside = new OffsideConfig(serverConfig.Offside);
            Icing = new IcingConfig(serverConfig.Icing);
            HighStick = new HighStickConfig(serverConfig.HighStick);
            GInt = new GIntConfig(serverConfig.GInt);
            Penalty = new PenaltyConfig(serverConfig.Penalty);

            MaxTippedMilliseconds = serverConfig.MaxTippedMilliseconds;
            MinPossessionMilliseconds = serverConfig.MinPossessionMilliseconds;
            MaxPossessionMilliseconds = serverConfig.MaxPossessionMilliseconds;

            RefMode = serverConfig.RefMode;
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

            OldServerConfig _oldConfig = oldConfig as OldServerConfig;
            ServerConfig newConfig = new ServerConfig();

            //if (LogInfo == _oldConfig.LogInfo)
                //LogInfo = newConfig.LogInfo;

            if (MaxTippedMilliseconds == _oldConfig.MaxTippedMilliseconds)
                MaxTippedMilliseconds = newConfig.MaxTippedMilliseconds;

            if (MinPossessionMilliseconds == _oldConfig.MinPossessionMilliseconds)
                MinPossessionMilliseconds = newConfig.MinPossessionMilliseconds;

            if (MaxPossessionMilliseconds == _oldConfig.MaxPossessionMilliseconds)
                MaxPossessionMilliseconds = newConfig.MaxPossessionMilliseconds;

            if (RefMode == _oldConfig.RefMode)
                RefMode = newConfig.RefMode;

            Offside.UpdateDefaultValues(_oldConfig.Offside);
            Icing.UpdateDefaultValues(_oldConfig.Icing);
            HighStick.UpdateDefaultValues(_oldConfig.HighStick);
            GInt.UpdateDefaultValues(_oldConfig.GInt);
            Penalty.UpdateDefaultValues(_oldConfig.Penalty);
            Faceoff.UpdateDefaultValues(_oldConfig.Faceoff);
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
            ServerConfig config = new ServerConfig();

            try {
                string rootPath = Path.GetFullPath(".");
                string configPath = Path.Combine(rootPath, Constants.MOD_NAME + "_serverconfig.json");
                if (File.Exists(configPath)) {
                    string configFileContent = File.ReadAllText(configPath);
                    config = SetConfig(configFileContent);
                    Logging.Log($"Server config read.", config, true);
                }

                config.UpdateDefaultValues(new OldServerConfig());

                try {
                    File.WriteAllText(configPath, config.ToString());
                }
                catch (Exception ex) {
                    Logging.LogError($"Can't write the server config file. (Permission error ?)\n{ex}", config);
                }

                Logging.Log($"Wrote server config : {config}", config, true);

                if (config.UseDefaultNumericValues) {
                    ServerConfig defaultConfig = new ServerConfig {
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
                        },
                        Penalty = new PenaltyConfig {
                            Interference = config.Penalty.Interference,
                            GoalieInterference = config.Penalty.GoalieInterference,
                            DelayOfGame = config.Penalty.DelayOfGame,
                            FaceoffViolation = config.Penalty.FaceoffViolation,
                            Embellishment = config.Penalty.Embellishment,
                        },
                        Faceoff = new FaceoffConfig {
                            EnableViolations = config.Faceoff.EnableViolations,
                            FreezePlayersBeforeDrop = config.Faceoff.FreezePlayersBeforeDrop,
                            ReAdd1SecondAfterFaceoff = config.Faceoff.ReAdd1SecondAfterFaceoff,
                            UseCustomFaceoff = config.Faceoff.UseCustomFaceoff,
                            UseDefaultPuckDropHeight = config.Faceoff.UseDefaultPuckDropHeight,
                        },
                    };

                    config = defaultConfig;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Can't read the server config file/folder. (Permission error ?)\n{ex}", config);
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
        /// Bool, true if player interference is enabled.
        /// </summary>
        public bool Interference { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a player interference penalty in milliseconds.
        /// </summary>
        public int InterferenceTime { get; set; } = 45000;
        /// <summary>
        /// Int, interference can be called after this number of milliseconds after touching the puck.
        /// </summary>
        public int InterferenceMillisecondsThreshold { get; set; } = 2000;
        /// <summary>
        /// Int, interference can be called after this number of milliseconds after the player hit fell.
        /// </summary>
        public int InterferenceOnSamePlayerMillisecondsThreshold { get; set; } = 5500;
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
        public int GoalieInterferenceTime { get; set; } = 45000;

        /// <summary>
        /// Bool, true if delay of game is enabled and the invisible wall has to be lowered.
        /// </summary>
        public bool DelayOfGame { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a delay of game penalty in milliseconds.
        /// </summary>
        public int DelayOfGameTime { get; set; } = 45000;
        /// <summary>
        /// Float, delta of the puck Z direction to use with the delay of game.
        /// </summary>
        public float DelayOfGameZDelta { get; set; } = 0.0125f;
        /// <summary>
        /// Int, delay of game can be called if someone didn't touch the puck this number of milliseconds before leaving the stick.
        /// </summary>
        public int DelayOfGameMillisecondsThreshold { get; set; } = 120;

        /// <summary>
        /// Bool, true if faceoff violation penalty is enabled.
        /// </summary>
        public bool FaceoffViolation { get; set; } = true;
        /// <summary>
        /// Int, time in the box for a faceoff violation penalty in milliseconds.
        /// </summary>
        public int FaceoffViolationTime { get; set; } = 30000;

        /// <summary>
        /// Bool, true if embellishment penalty is enabled.
        /// </summary>
        public bool Embellishment { get; set; } = true;
        /// <summary>
        /// Int, time in the box for an embellishment penalty in milliseconds.
        /// </summary>
        public int EmbellishmentTime { get; set; } = 30000;
        /// <summary>
        /// Int, embellishment can be called after this number of milliseconds after the player gets up.
        /// </summary>
        public int EmbellishmentMillisecondsThreshold { get; set; } = 3500;
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
            MaxPenaltiesCountPerPlayer = penaltyConfig.MaxPenaltiesCountPerPlayer;
            MaxPenalizedPlayersPerTeam = penaltyConfig.MaxPenalizedPlayersPerTeam;

            Interference = penaltyConfig.Interference;
            InterferenceTime = penaltyConfig.InterferenceTime;
            InterferenceMillisecondsThreshold = penaltyConfig.InterferenceMillisecondsThreshold;
            InterferenceOnSamePlayerMillisecondsThreshold = penaltyConfig.InterferenceOnSamePlayerMillisecondsThreshold;
            JumpHeightMinimum = penaltyConfig.JumpHeightMinimum;

            GoalieInterference = penaltyConfig.GoalieInterference;
            GoalieInterferenceTime = penaltyConfig.GoalieInterferenceTime;

            DelayOfGame = penaltyConfig.DelayOfGame;
            DelayOfGameTime = penaltyConfig.DelayOfGameTime;
            DelayOfGameZDelta = penaltyConfig.DelayOfGameZDelta;
            DelayOfGameMillisecondsThreshold = penaltyConfig.DelayOfGameMillisecondsThreshold;

            FaceoffViolation = penaltyConfig.FaceoffViolation;
            FaceoffViolationTime = penaltyConfig.FaceoffViolationTime;

            Embellishment = penaltyConfig.Embellishment;
            EmbellishmentTime = penaltyConfig.EmbellishmentTime;
            EmbellishmentMillisecondsThreshold = penaltyConfig.EmbellishmentMillisecondsThreshold;
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldPenaltyConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldPenaltyConfig)}.", nameof(oldConfig));

            OldPenaltyConfig _oldConfig = oldConfig as OldPenaltyConfig;
            PenaltyConfig newConfig = new PenaltyConfig();

            if (MaxPenaltiesCountPerPlayer == _oldConfig.MaxPenaltiesCountPerPlayer)
                MaxPenaltiesCountPerPlayer = newConfig.MaxPenaltiesCountPerPlayer;

            if (MaxPenalizedPlayersPerTeam == _oldConfig.MaxPenalizedPlayersPerTeam)
                MaxPenalizedPlayersPerTeam = newConfig.MaxPenalizedPlayersPerTeam;


            if (Interference == _oldConfig.Interference)
                Interference = newConfig.Interference;

            if (InterferenceTime == _oldConfig.InterferenceTime)
                InterferenceTime = newConfig.InterferenceTime;

            if (InterferenceMillisecondsThreshold == _oldConfig.InterferenceMillisecondsThreshold)
                InterferenceMillisecondsThreshold = newConfig.InterferenceMillisecondsThreshold;

            if (InterferenceOnSamePlayerMillisecondsThreshold == _oldConfig.InterferenceOnSamePlayerMillisecondsThreshold)
                InterferenceOnSamePlayerMillisecondsThreshold = newConfig.InterferenceOnSamePlayerMillisecondsThreshold;

            if (JumpHeightMinimum == _oldConfig.JumpHeightMinimum)
                JumpHeightMinimum = newConfig.JumpHeightMinimum;


            if (GoalieInterference == _oldConfig.GoalieInterference)
                GoalieInterference = newConfig.GoalieInterference;

            if (GoalieInterferenceTime == _oldConfig.GoalieInterferenceTime)
                GoalieInterferenceTime = newConfig.GoalieInterferenceTime;


            if (DelayOfGame == _oldConfig.DelayOfGame)
                DelayOfGame = newConfig.DelayOfGame;

            if (DelayOfGameTime == _oldConfig.DelayOfGameTime)
                DelayOfGameTime = newConfig.DelayOfGameTime;

            if (DelayOfGameZDelta == _oldConfig.DelayOfGameZDelta)
                DelayOfGameZDelta = newConfig.DelayOfGameZDelta;

            if (DelayOfGameMillisecondsThreshold == _oldConfig.DelayOfGameMillisecondsThreshold)
                DelayOfGameMillisecondsThreshold = newConfig.DelayOfGameMillisecondsThreshold;


            if (FaceoffViolation == _oldConfig.FaceoffViolation)
                FaceoffViolation = newConfig.FaceoffViolation;

            if (FaceoffViolationTime == _oldConfig.FaceoffViolationTime)
                FaceoffViolationTime = newConfig.FaceoffViolationTime;


            if (Embellishment == _oldConfig.Embellishment)
                Embellishment = newConfig.Embellishment;

            if (EmbellishmentTime == _oldConfig.EmbellishmentTime)
                EmbellishmentTime = newConfig.EmbellishmentTime;

            if (EmbellishmentMillisecondsThreshold == _oldConfig.EmbellishmentMillisecondsThreshold)
                EmbellishmentMillisecondsThreshold = newConfig.EmbellishmentMillisecondsThreshold;
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
            BlueTeam = offsideConfig.BlueTeam;
            RedTeam = offsideConfig.RedTeam;
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldOffsideConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldOffsideConfig)}.", nameof(oldConfig));

            OldOffsideConfig _oldConfig = oldConfig as OldOffsideConfig;
            OffsideConfig newConfig = new OffsideConfig();

            if (BlueTeam == _oldConfig.BlueTeam)
                BlueTeam = newConfig.BlueTeam;

            if (RedTeam == _oldConfig.RedTeam)
                RedTeam = newConfig.RedTeam;
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
        public double DeferredMaxPossibleTimeMultiplicator { get; set; } = 280d;

        /// <summary>
        /// Double, deferred icing max possible time addition (after multiplicator).
        /// </summary>
        public double DeferredMaxPossibleTimeAddition { get; set; } = 9500d;

        /// <summary>
        /// Float, deferred icing max possible time substraction depending of players distance to puck (after addition).
        /// </summary>
        public float DeferredMaxPossibleTimeDistanceDelta { get; set; } = 250f;

        /// <summary>
        /// Dictionary of Zone and float, number of milliseconds after puck exiting the stick before arriving behind the goal line to not be considered for icing for each zone.
        /// </summary>
        public Dictionary<Zone, float> MaxPossibleTime { get; set; } = new Dictionary<Zone, float> {
            { Zone.BlueTeam_BehindGoalLine, 9750f },
            { Zone.RedTeam_BehindGoalLine, 9750f },
            { Zone.BlueTeam_Zone, 8000f },
            { Zone.RedTeam_Zone, 8000f },
            { Zone.BlueTeam_Center, 5750f },
            { Zone.RedTeam_Center, 5750f },
        };

        /// <summary>
        /// Int, number of milliseconds for icing to be called off if it has not being called.
        /// </summary>
        public int MaxActiveTime { get; set; } = 12000;

        /// <summary>
        /// Float, delta used to calculate the dynamic icing possible times.
        /// </summary>
        public float Delta { get; set; } = 21.75f;

        /// <summary>
        /// Float, max height before deferred icing does not check for possibility that the other team touches the puck before icing.
        /// </summary>
        public float DeferredMaxHeight { get; set; } = 0.85f;

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
            BlueTeam = icingConfig.BlueTeam;
            RedTeam = icingConfig.RedTeam;

            Deferred = icingConfig.Deferred;
            DeferredMaxPossibleTimeMultiplicator = icingConfig.DeferredMaxPossibleTimeMultiplicator;
            DeferredMaxPossibleTimeAddition = icingConfig.DeferredMaxPossibleTimeAddition;
            DeferredMaxPossibleTimeDistanceDelta = icingConfig.DeferredMaxPossibleTimeDistanceDelta;
            MaxPossibleTime = new Dictionary<Zone, float>(icingConfig.MaxPossibleTime);
            MaxActiveTime = icingConfig.MaxActiveTime;
            Delta = icingConfig.Delta;
            DeferredMaxHeight = icingConfig.DeferredMaxHeight;

            StaminaDrain = icingConfig.StaminaDrain;
            StaminaDrainGoalie = icingConfig.StaminaDrainGoalie;
            StaminaDrainDivisionAmount = icingConfig.StaminaDrainDivisionAmount;
            StaminaDrainDivisionAmountPenaltyDelta = icingConfig.StaminaDrainDivisionAmountPenaltyDelta;
            StaminaDrainDivisionAmountPenaltyTime = icingConfig.StaminaDrainDivisionAmountPenaltyTime;
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldIcingConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldIcingConfig)}.", nameof(oldConfig));

            OldIcingConfig _oldConfig = oldConfig as OldIcingConfig;
            IcingConfig newConfig = new IcingConfig();

            if (BlueTeam == _oldConfig.BlueTeam)
                BlueTeam = newConfig.BlueTeam;

            if (RedTeam == _oldConfig.RedTeam)
                RedTeam = newConfig.RedTeam;

            if (Deferred == _oldConfig.Deferred)
                Deferred = newConfig.Deferred;

            if (DeferredMaxPossibleTimeMultiplicator == _oldConfig.DeferredMaxPossibleTimeMultiplicator)
                DeferredMaxPossibleTimeMultiplicator = newConfig.DeferredMaxPossibleTimeMultiplicator;

            if (DeferredMaxPossibleTimeAddition == _oldConfig.DeferredMaxPossibleTimeAddition)
                DeferredMaxPossibleTimeAddition = newConfig.DeferredMaxPossibleTimeAddition;

            if (DeferredMaxPossibleTimeDistanceDelta == _oldConfig.DeferredMaxPossibleTimeDistanceDelta)
                DeferredMaxPossibleTimeDistanceDelta = newConfig.DeferredMaxPossibleTimeDistanceDelta;

            try {
                foreach (KeyValuePair<Zone, float> kvp in new Dictionary<Zone, float>(MaxPossibleTime)) {
                    if (_oldConfig.MaxPossibleTime.TryGetValue(kvp.Key, out float value) && value == kvp.Value)
                        MaxPossibleTime[kvp.Key] = newConfig.MaxPossibleTime[kvp.Key];
                }
            }
            catch { }

            if (MaxActiveTime == _oldConfig.MaxActiveTime)
                MaxActiveTime = newConfig.MaxActiveTime;

            if (Delta == _oldConfig.Delta)
                Delta = newConfig.Delta;

            if (DeferredMaxHeight == _oldConfig.DeferredMaxHeight)
                DeferredMaxHeight = newConfig.DeferredMaxHeight;

            if (StaminaDrain == _oldConfig.StaminaDrain)
                StaminaDrain = newConfig.StaminaDrain;

            if (StaminaDrainGoalie == _oldConfig.StaminaDrainGoalie)
                StaminaDrainGoalie = newConfig.StaminaDrainGoalie;

            if (StaminaDrainDivisionAmount == _oldConfig.StaminaDrainDivisionAmount)
                StaminaDrainDivisionAmount = newConfig.StaminaDrainDivisionAmount;

            if (StaminaDrainDivisionAmountPenaltyDelta == _oldConfig.StaminaDrainDivisionAmountPenaltyDelta)
                StaminaDrainDivisionAmountPenaltyDelta = newConfig.StaminaDrainDivisionAmountPenaltyDelta;

            if (StaminaDrainDivisionAmountPenaltyTime == _oldConfig.StaminaDrainDivisionAmountPenaltyTime)
                StaminaDrainDivisionAmountPenaltyTime = newConfig.StaminaDrainDivisionAmountPenaltyTime;
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
        public float MaxHeight { get; set; } = Codebase.Constants.CROSSBAR_HEIGHT + 0.05f;

        /// <summary>
        /// Int, number of milliseconds after a high stick to call high stick if no one touches the puck.
        /// </summary>
        public int MaxMilliseconds { get; set; } = 8000;

        /// <summary>
        /// Float, delta used to calculate the high stick maximum frames before activation.
        /// </summary>
        public float Delta { get; set; } = 20f;

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
            BlueTeam = highStickConfig.BlueTeam;
            RedTeam = highStickConfig.RedTeam;

            MaxHeight = highStickConfig.MaxHeight;
            MaxMilliseconds = highStickConfig.MaxMilliseconds;
            Delta = highStickConfig.Delta;
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldHighStickConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldHighStickConfig)}.", nameof(oldConfig));

            OldHighStickConfig _oldConfig = oldConfig as OldHighStickConfig;
            HighStickConfig newConfig = new HighStickConfig();

            if (BlueTeam == _oldConfig.BlueTeam)
                BlueTeam = newConfig.BlueTeam;

            if (RedTeam == _oldConfig.RedTeam)
                RedTeam = newConfig.RedTeam;

            if (MaxHeight == _oldConfig.MaxHeight)
                MaxHeight = newConfig.MaxHeight;

            if (MaxMilliseconds == _oldConfig.MaxMilliseconds)
                MaxMilliseconds = newConfig.MaxMilliseconds;

            if (Delta == _oldConfig.Delta)
                Delta = newConfig.Delta;
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
        public int PushNoGoalMilliseconds { get; set; } = 3750;

        /// <summary>
        /// Float, force threshold for a push on the goalie to be considered for goalie interference.
        /// </summary>
        public float CollisionForceThreshold { get; set; } = 0.97f;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie interference calls.
        /// </summary>
        public float GoalieRadius { get; set; } = 0.805f;

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
            BlueTeam = gIntConfig.BlueTeam;
            RedTeam = gIntConfig.RedTeam;

            PushNoGoalMilliseconds = gIntConfig.PushNoGoalMilliseconds;
            CollisionForceThreshold = gIntConfig.CollisionForceThreshold;
            GoalieRadius = gIntConfig.GoalieRadius;
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldGIntConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldGIntConfig)}.", nameof(oldConfig));

            OldGIntConfig _oldConfig = oldConfig as OldGIntConfig;
            GIntConfig newConfig = new GIntConfig();

            if (BlueTeam == _oldConfig.BlueTeam)
                BlueTeam = newConfig.BlueTeam;

            if (RedTeam == _oldConfig.RedTeam)
                RedTeam = newConfig.RedTeam;

            if (PushNoGoalMilliseconds == _oldConfig.PushNoGoalMilliseconds)
                PushNoGoalMilliseconds = newConfig.PushNoGoalMilliseconds;

            if (CollisionForceThreshold == _oldConfig.CollisionForceThreshold)
                CollisionForceThreshold = newConfig.CollisionForceThreshold;

            if (GoalieRadius == _oldConfig.GoalieRadius)
                GoalieRadius = newConfig.GoalieRadius;
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
        /// Bool, the game rounds down the time remaining on every faceoff.
        /// This readds 1 second on every faceoff so the game doesn't end too quickly.
        /// </summary>
        public bool ReAdd1SecondAfterFaceoff { get; set; } = true;

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
        /// Bool, true if players has to be freezed before puck drops.
        /// </summary>
        public bool FreezePlayersBeforeDrop { get; set; } = true;

        /// <summary>
        /// Float, number of seconds to freeze players before faceoff ends.
        /// </summary>
        public float FreezeBeforeDropTime { get; set; } = 3f;

        // Center position settings
        public float CenterMaxForward { get; set; } = 0;      // Centers can't move forward at all
        public float CenterMaxBackward { get; set; } = 2f;    // Backward wall
        public float CenterMaxLeft { get; set; } = 1f;        // Limited side movement
        public float CenterMaxRight { get; set; } = 1f;

        // Winger settings
        public float WingerMaxForward { get; set; } = 0.5f;     // Wingers can move forward a bit
        public float WingerMaxBackward { get; set; } = 2f;    // Backward wall
        public float WingerMaxToward { get; set; } = 0;       // Limited movement toward center (inward wall)
        public float WingerMaxAway { get; set; } = 2.5f;      // More movement away from center (outward wall toward boards)

        // Defense settings
        public float DefenseMaxForward { get; set; } = 0;     // Defense can't move forward at all
        public float DefenseMaxBackward { get; set; } = 0;    // Backward wall
        public float DefenseMaxToward { get; set; } = 2.5f;   // Movement toward center
        public float DefenseMaxAway { get; set; } = 2.5f;     // Movement away from center (toward boards)

        // Goalie settings
        public float GoalieMaxForward { get; set; } = 2f;     // Minimal forward movement
        public float GoalieMaxBackward { get; set; } = 2f;    // Backward wall
        public float GoalieMaxLeft { get; set; } = 2f;
        public float GoalieMaxRight { get; set; } = 2f;

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
            UseCustomFaceoff = faceoffConfig.UseCustomFaceoff;
            ReAdd1SecondAfterFaceoff = faceoffConfig.ReAdd1SecondAfterFaceoff;
            UseDefaultPuckDropHeight = faceoffConfig.UseDefaultPuckDropHeight;
            PuckDropHeight = faceoffConfig.PuckDropHeight;

            EnableViolations = faceoffConfig.EnableViolations;
            PuckIceContactHeight = faceoffConfig.PuckIceContactHeight;
            MaxViolationsBeforePenalty = faceoffConfig.MaxViolationsBeforePenalty;
            PenaltyFreezeDistance = faceoffConfig.PenaltyFreezeDistance;
            PenaltyFreezeDuration = faceoffConfig.PenaltyFreezeDuration;
            FreezePlayersBeforeDrop = faceoffConfig.FreezePlayersBeforeDrop;
            FreezeBeforeDropTime = faceoffConfig.FreezeBeforeDropTime;

            CenterMaxForward = faceoffConfig.CenterMaxForward;
            CenterMaxBackward = faceoffConfig.CenterMaxBackward;
            CenterMaxLeft = faceoffConfig.CenterMaxLeft;
            CenterMaxRight = faceoffConfig.CenterMaxRight;

            WingerMaxForward = faceoffConfig.WingerMaxForward;
            WingerMaxBackward = faceoffConfig.WingerMaxBackward;
            WingerMaxToward = faceoffConfig.WingerMaxToward;
            WingerMaxAway = faceoffConfig.WingerMaxAway;

            DefenseMaxForward = faceoffConfig.DefenseMaxForward;
            DefenseMaxBackward = faceoffConfig.DefenseMaxBackward;
            DefenseMaxToward = faceoffConfig.DefenseMaxToward;
            DefenseMaxAway = faceoffConfig.DefenseMaxAway;

            GoalieMaxForward = faceoffConfig.GoalieMaxForward;
            GoalieMaxBackward = faceoffConfig.GoalieMaxBackward;
            GoalieMaxLeft = faceoffConfig.GoalieMaxLeft;
            GoalieMaxRight = faceoffConfig.GoalieMaxRight;
        }
        #endregion

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldFaceoffConfig))
                throw new ArgumentException($"{nameof(oldConfig)} has to be typeof {nameof(OldFaceoffConfig)}.", nameof(oldConfig));

            OldFaceoffConfig _oldConfig = oldConfig as OldFaceoffConfig;
            FaceoffConfig newConfig = new FaceoffConfig();

            if (UseCustomFaceoff == _oldConfig.UseCustomFaceoff)
                UseCustomFaceoff = newConfig.UseCustomFaceoff;

            if (ReAdd1SecondAfterFaceoff == _oldConfig.ReAdd1SecondAfterFaceoff)
                ReAdd1SecondAfterFaceoff = newConfig.ReAdd1SecondAfterFaceoff;

            if (UseDefaultPuckDropHeight == _oldConfig.UseDefaultPuckDropHeight)
                UseDefaultPuckDropHeight = newConfig.UseDefaultPuckDropHeight;

            if (PuckDropHeight == _oldConfig.PuckDropHeight)
                PuckDropHeight = newConfig.PuckDropHeight;

            if (EnableViolations == _oldConfig.EnableViolations)
                EnableViolations = newConfig.EnableViolations;

            if (PuckIceContactHeight == _oldConfig.PuckIceContactHeight)
                PuckIceContactHeight = newConfig.PuckIceContactHeight;

            if (MaxViolationsBeforePenalty == _oldConfig.MaxViolationsBeforePenalty)
                MaxViolationsBeforePenalty = newConfig.MaxViolationsBeforePenalty;

            if (PenaltyFreezeDistance == _oldConfig.PenaltyFreezeDistance)
                PenaltyFreezeDistance = newConfig.PenaltyFreezeDistance;

            if (PenaltyFreezeDuration == _oldConfig.PenaltyFreezeDuration)
                PenaltyFreezeDuration = newConfig.PenaltyFreezeDuration;

            if (FreezePlayersBeforeDrop == _oldConfig.FreezePlayersBeforeDrop)
                FreezePlayersBeforeDrop = newConfig.FreezePlayersBeforeDrop;

            if (FreezeBeforeDropTime == _oldConfig.FreezeBeforeDropTime)
                FreezeBeforeDropTime = newConfig.FreezeBeforeDropTime;

            if (WingerMaxForward == _oldConfig.WingerMaxForward)
                WingerMaxForward = newConfig.WingerMaxForward;
            if (WingerMaxBackward == _oldConfig.WingerMaxBackward)
                WingerMaxBackward = newConfig.WingerMaxBackward;
            if (WingerMaxToward == _oldConfig.WingerMaxToward)
                WingerMaxToward = newConfig.WingerMaxToward;
            if (WingerMaxAway == _oldConfig.WingerMaxAway)
                WingerMaxAway = newConfig.WingerMaxAway;

            if (DefenseMaxForward == _oldConfig.DefenseMaxForward)
                DefenseMaxForward = newConfig.DefenseMaxForward;
            if (DefenseMaxBackward == _oldConfig.DefenseMaxBackward)
                DefenseMaxBackward = newConfig.DefenseMaxBackward;
            if (DefenseMaxToward == _oldConfig.DefenseMaxToward)
                DefenseMaxToward = newConfig.DefenseMaxToward;
            if (DefenseMaxAway == _oldConfig.DefenseMaxAway)
                DefenseMaxAway = newConfig.DefenseMaxAway;

            if (GoalieMaxForward == _oldConfig.GoalieMaxForward)
                GoalieMaxForward = newConfig.GoalieMaxForward;
            if (GoalieMaxBackward == _oldConfig.GoalieMaxBackward)
                GoalieMaxBackward = newConfig.GoalieMaxBackward;
            if (GoalieMaxLeft == _oldConfig.GoalieMaxLeft)
                GoalieMaxLeft = newConfig.GoalieMaxLeft;
            if (GoalieMaxRight == _oldConfig.GoalieMaxRight)
                GoalieMaxRight = newConfig.GoalieMaxRight;
        }
    }
}
