using Newtonsoft.Json;
using oomtm450PuckMod_Ruleset.SystemFunc;
using System.IO;

namespace oomtm450PuckMod_Ruleset.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_template_serverconfig.json used for this mod.
    /// </summary>
    public class ServerConfig : IConfig {
        #region Properties
        /// <summary>
        /// Int, number of skaters that are allowed on the ice at the same time per team.
        /// </summary>
        public int MaxNumberOfSkaters { get; set; } = 5;

        /// <summary>
        /// Bool, true if team balancing has to be respected.
        /// </summary>
        public bool TeamBalancing { get; set; } = false;

        /// <summary>
        /// Int, offset in the number of skaters between both teams if TeamBalancing or TeamBalancingGoalie is activated.
        /// </summary>
        public int TeamBalanceOffset { get; set; } = 0;

        /// <summary>
        /// Bool, if a goalie is playing in the red or blue team and if the other team has the same or more skaters, the next position has to be goalie.
        /// TLDR : Team balancing only if atleast one goalie is playing.
        /// </summary>
        public bool TeamBalancingGoalie { get; set; } = false;

        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; set; } = true;

        /// <summary>
        /// Bool, true if the config has been sent by the server.
        /// </summary>
        public bool SentByServer { get; set; } = false;

        /// <summary>
        /// Bool, true if admins can bypass the skaters limit.
        /// </summary>
        public bool AdminBypass { get; set; } = true;

        /// <summary>
        /// String array, all admin steam Ids of the server.
        /// </summary>
        public string[] AdminSteamIds { get; set; }
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Function that serialize the config object.
        /// </summary>
        /// <returns>String, serialized config.</returns>
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
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
        /// <param name="adminSteamIds">String array, all admin steam Ids of the server.</param>
        /// <returns>ServerConfig, parsed config.</returns>
        internal static ServerConfig ReadConfig(string[] adminSteamIds) {
            ServerConfig config = new ServerConfig();

            string rootPath = Path.GetFullPath(".");
            string configPath = Path.Combine(rootPath, Constants.MOD_NAME + "_serverconfig.json");
            if (File.Exists(configPath)) {
                string configFileContent = File.ReadAllText(configPath);
                config = SetConfig(configFileContent);
            }

            File.WriteAllText(configPath, config.ToString());

            Logging.Log($"Writing server config : {config}", config);

            config.SentByServer = true;
            config.AdminSteamIds = adminSteamIds;
            return config;
        }
        #endregion
    }
}
