using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;
using System;
using System.IO;

namespace oomtm450PuckMod_Stats.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_stats_serverconfig.json used for this mod.
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
        #endregion

        #region Properties
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; set; } = true;

        /// <summary>
        /// Bool, true if the numeric values has to be replaced be the default ones. Make this false to use custom values.
        /// </summary>
        public bool UseDefaultNumericValues { get; set; } = true;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie save crease system.
        /// </summary>
        public float GoalieRadius { get; set; } = 0.75f;

        /// <summary>
        /// Float, delta of the puck Z direction to use with the goalie save crease system.
        /// </summary>
        public float GoalieSaveCreaseSystemZDelta { get; set; } = 0.0125f;

        /// <summary>
        /// Bool, true if the end of game stats JSON must be saved as file on the server.
        /// </summary>
        public bool SaveEOGJSON { get; set; } = true;

        /// <summary>
        /// Int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        public int MaxTippedMilliseconds { get; set; } = 62;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; set; } = 333;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; set; } = 1250;

        /// <summary>
        /// Int, number of milliseconds for a change of possession to the other team be considered a turnover.
        /// </summary>
        public int TurnoverThresholdMilliseconds { get; set; } = 500;

        /// <summary>
        /// Bool, true if Stats mod has to log all stats event, like passes and shots.
        /// </summary>
        public bool LogStats { get; set; } = false;

        /// <summary>
        /// Float, maximum height for the puck to be touched on faceoff.
        /// </summary>
        public float PuckIceContactHeight { get; set; } = 0.205f;

        /// <summary>
        /// String, name of the mod.
        /// </summary>
        [JsonIgnore]
        public string ModName { get; } = Constants.MOD_NAME;
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

            if (GoalieRadius == _oldConfig.GoalieRadius)
                GoalieRadius = newConfig.GoalieRadius;

            if (GoalieSaveCreaseSystemZDelta == _oldConfig.GoalieSaveCreaseSystemZDelta)
                GoalieSaveCreaseSystemZDelta = newConfig.GoalieSaveCreaseSystemZDelta;

            if (SaveEOGJSON == _oldConfig.SaveEOGJSON)
                SaveEOGJSON = newConfig.SaveEOGJSON;

            if (MaxTippedMilliseconds == _oldConfig.MaxTippedMilliseconds)
                MaxTippedMilliseconds = newConfig.MaxTippedMilliseconds;

            if (MinPossessionMilliseconds == _oldConfig.MinPossessionMilliseconds)
                MinPossessionMilliseconds = newConfig.MinPossessionMilliseconds;

            if (MaxPossessionMilliseconds == _oldConfig.MaxPossessionMilliseconds)
                MaxPossessionMilliseconds = newConfig.MaxPossessionMilliseconds;

            if (TurnoverThresholdMilliseconds == _oldConfig.TurnoverThresholdMilliseconds)
                TurnoverThresholdMilliseconds = newConfig.TurnoverThresholdMilliseconds;

            if (LogStats == _oldConfig.LogStats)
                LogStats = newConfig.LogStats;

            if (PuckIceContactHeight == _oldConfig.PuckIceContactHeight)
                PuckIceContactHeight = newConfig.PuckIceContactHeight;
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
                    Logging.LogError($"Can't write the server config file. (Permission error ?)\n{ex}", config);
                }

                Logging.Log($"Wrote server config : {config}", config, true);

                if (config.UseDefaultNumericValues) {
                    ServerConfig defaultConfig = new ServerConfig {
                        LogInfo = config.LogInfo,
                        UseDefaultNumericValues = config.UseDefaultNumericValues,
                        SaveEOGJSON = config.SaveEOGJSON,
                        LogStats = config.LogStats,
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
}
