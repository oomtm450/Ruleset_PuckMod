﻿using Newtonsoft.Json;
using oomtm450PuckMod_Ruleset.SystemFunc;
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
        /// Bool, true if the config has been sent by the server.
        /// </summary>
        public bool SentByServer { get; set; } = false;

        /// <summary>
        /// String array, all admin steam Ids of the server.
        /// </summary>
        public string[] AdminSteamIds { get; set; }

        /// <summary>
        /// Bool, true if red team offsides are activated.
        /// </summary>
        public bool RedTeamOffsides { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team offsides are activated.
        /// </summary>
        public bool BlueTeamOffsides { get; set; } = true;

        /// <summary>
        /// Bool, true if red team icings are activated.
        /// </summary>
        public bool RedTeamIcings { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team icings are activated.
        /// </summary>
        public bool BlueTeamIcings { get; set; } = true;

        /// <summary>
        /// Bool, true if red team high stick are activated.
        /// </summary>
        public bool RedTeamHighStick { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team high stick are activated.
        /// </summary>
        public bool BlueTeamHighStick { get; set; } = true;

        /// <summary>
        /// Float, base height before hitting the puck with a stick is considered high stick.
        /// </summary>
        public float HighStickHeight { get; set; } = Ruleset.SHOULDERS_HEIGHT;

        /// <summary>
        /// Bool, true if red team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool RedTeamGInt { get; set; } = true;

        /// <summary>
        /// Bool, true if blue team is able to get their goal called off because of goalie interference.
        /// </summary>
        public bool BlueTeamGInt { get; set; } = true;

        /// <summary>
        /// Bool, true if deferred icing is activated. If false, icing will be called when the puck is touched.
        /// </summary>
        public bool DeferredIcing { get; set; } = true;
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

            try {
                File.WriteAllText(configPath, config.ToString());
            }
            catch (Exception ex) {
                Logging.LogError($"Can't write the server config file. (Permission error ?)\n{ex}");
            }

            Logging.Log($"Writing server config : {config}", config);

            config.SentByServer = true;
            config.AdminSteamIds = adminSteamIds;
            return config;
        }
        #endregion
    }
}
