using Newtonsoft.Json;
using System;
using System.IO;

namespace oomtm450PuckMod_Ruleset.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_template_serverconfig.json used for this mod.
    /// </summary>
    public class ServerConfig : IConfig {
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
        /// Int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        public int MaxTippedMilliseconds { get; set; } = 92;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered with challenge.
        /// </summary>
        public int MinPossessionMilliseconds { get; set; } = 235;

        /// <summary>
        /// Int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        public int MaxPossessionMilliseconds { get; set; } = 500;
        #endregion

        #region Methods/Functions
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

                try {
                    File.WriteAllText(configPath, config.ToString());
                }
                catch (Exception ex) {
                    Logging.LogError($"Can't write the server config file. (Permission error ?)\n{ex}");
                }

                Logging.Log($"Wrote server config : {config}", config, true);
            }
            catch (Exception ex) {
                Logging.LogError($"Can't read the server config file/folder. (Permission error ?)\n{ex}");
            }

            return config;
        }
        #endregion
    }

    /// <summary>
    /// Class containing the config for offsides.
    /// </summary>
    public class OffsideConfig {
        /// <summary>
        /// Bool, true if red team offsides are activated.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team offsides are activated.
        /// </summary>
        public bool BlueTeam { get; set; } = true;
    }

    /// <summary>
    /// Class containing the config for icings.
    /// </summary>
    public class IcingConfig {
        /// <summary>
        /// Bool, true if red team icings are activated.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team icings are activated.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if deferred icing is activated. If false, icing will be called when the puck is touched.
        /// </summary>
        public bool Deferred { get; set; } = true;

        /// <summary>
        /// Int, number of milliseconds after puck exiting the stick before arriving behind the goal line to not be considered for icing.
        /// </summary>
        public int MaxPossibleTime { get; set; } = 7000;

        /// <summary>
        /// Int, number of milliseconds for icing to be called off if it has not being called.
        /// </summary>
        public int MaxActiveTime { get; set; } = 12000;
    }

    /// <summary>
    /// Class containing the config for high sticks.
    /// </summary>
    public class HighStickConfig {
        /// <summary>
        /// Bool, true if red team high stick are activated.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team high stick are activated.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Float, base height before hitting the puck with a stick is considered high stick.
        /// </summary>
        public float MaxHeight { get; set; } = 1.78f;

        /// <summary>
        /// Int, number of milliseconds after a high stick to not be considered.
        /// </summary>
        public int MaxMilliseconds { get; set; } = 5000;
    }

    /// <summary>
    /// Class containing the config for goalie interferences.
    /// </summary>
    public class GIntConfig {
        /// <summary>
        /// Bool, true if red team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool RedTeam { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool BlueTeam { get; set; } = true;

        /// <summary>
        /// Int, number of milliseconds after a push on the goalie to be considered no goal.
        /// </summary>
        public int PushNoGoalMilliseconds { get; set; } = 3500;

        /// <summary>
        /// Int, number of milliseconds after a hit on the goalie to be considered no goal.
        /// </summary>
        public int HitNoGoalMilliseconds { get; set; } = 9000; // TODO : Remove when penalty is added.

        /// <summary>
        /// Float, force threshold for a push on the goalie to be considered for goalie interference.
        /// </summary>
        public float CollisionForceThreshold { get; set; } = 0.97f;

        /// <summary>
        /// Float, radius of a goalie. Make higher to augment the crease size for goalie interference calls.
        /// </summary>
        public float GoalieRadius { get; set; } = 0.784f;
    }
}
