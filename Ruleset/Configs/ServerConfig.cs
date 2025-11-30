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
        public int MaxTippedMilliseconds { get; set; } = 91;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; set; } = 300;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; set; } = 700;

        /// <summary>
        /// Float, puck speed multiplicator relative to vanilla. (If puck is 1.5x faster in your server, set this to 1.5)
        /// </summary>
        public float PuckSpeedRelativeToVanilla { get; set; } = 1f;
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

            if (UseCustomFaceoff == _oldConfig.UseCustomFaceoff)
                UseCustomFaceoff = newConfig.UseCustomFaceoff;

            if (ReAdd1SecondAfterFaceoff == _oldConfig.ReAdd1SecondAfterFaceoff)
                ReAdd1SecondAfterFaceoff = newConfig.ReAdd1SecondAfterFaceoff;

            if (UseDefaultPuckDropHeight == _oldConfig.UseDefaultPuckDropHeight)
                UseDefaultPuckDropHeight = newConfig.UseDefaultPuckDropHeight;

            if (PuckDropHeight == _oldConfig.PuckDropHeight)
                PuckDropHeight = newConfig.PuckDropHeight;

            if (MaxTippedMilliseconds == _oldConfig.MaxTippedMilliseconds)
                MaxTippedMilliseconds = newConfig.MaxTippedMilliseconds;

            if (MinPossessionMilliseconds == _oldConfig.MinPossessionMilliseconds)
                MinPossessionMilliseconds = newConfig.MinPossessionMilliseconds;

            if (MaxPossessionMilliseconds == _oldConfig.MaxPossessionMilliseconds)
                MaxPossessionMilliseconds = newConfig.MaxPossessionMilliseconds;

            Offside.UpdateDefaultValues(_oldConfig.Offside);
            Icing.UpdateDefaultValues(_oldConfig.Icing);
            HighStick.UpdateDefaultValues(_oldConfig.HighStick);
            GInt.UpdateDefaultValues(_oldConfig.GInt);
            Penalty.UpdateDefaultValues(_oldConfig.Penalty);
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
        /// <summary>
        /// Int, interference can be called after this number of milliseconds after touching the puck.
        /// </summary>
        public int InterferenceMillisecondsThreshold { get; set; } = 2000;

        /// <summary>
        /// Method that updates this config with the new default values, if the old default values were used.
        /// </summary>
        /// <param name="oldConfig">ISubConfig, config with old values.</param>
        public void UpdateDefaultValues(ISubConfig oldConfig) {
            if (!(oldConfig is OldPenaltyConfig))
                throw new ArgumentException($"oldConfig has to be typeof {nameof(OldPenaltyConfig)}.", nameof(oldConfig));

            OldPenaltyConfig _oldConfig = oldConfig as OldPenaltyConfig;
            PenaltyConfig newConfig = new PenaltyConfig();

            if (InterferenceMillisecondsThreshold == _oldConfig.InterferenceMillisecondsThreshold)
                InterferenceMillisecondsThreshold = newConfig.InterferenceMillisecondsThreshold;
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
        public int MaxActiveTime { get; set; } = 12000;

        /// <summary>
        /// Float, delta used to calculate the dynamic icing possible times.
        /// </summary>
        public float Delta { get; set; } = 21.5f;

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
        public float MaxHeight { get; set; } = 1.8f;

        /// <summary>
        /// Int, number of milliseconds after a high stick to not be considered.
        /// </summary>
        public int MaxMilliseconds { get; set; } = 5000;

        /// <summary>
        /// Float, delta used to calculate the high stick maximum frames before activation.
        /// </summary>
        public float Delta { get; set; } = 18f;

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
        public int PushNoGoalMilliseconds { get; set; } = 3500;

        /// <summary>
        /// Int, number of milliseconds after a hit on the goalie to be considered no goal.
        /// </summary>
        public int HitNoGoalMilliseconds { get; set; } = 11000; // TODO : Remove when penalty is added.

        /// <summary>
        /// Float, force threshold for a push on the goalie to be considered for goalie interference.
        /// </summary>
        public float CollisionForceThreshold { get; set; } = 0.971f;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie interference calls.
        /// </summary>
        public float GoalieRadius { get; set; } = 0.8f;

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

            if (HitNoGoalMilliseconds == _oldConfig.HitNoGoalMilliseconds)
                HitNoGoalMilliseconds = newConfig.HitNoGoalMilliseconds;

            if (CollisionForceThreshold == _oldConfig.CollisionForceThreshold)
                CollisionForceThreshold = newConfig.CollisionForceThreshold;

            if (GoalieRadius == _oldConfig.GoalieRadius)
                GoalieRadius = newConfig.GoalieRadius;
        }
    }
}
