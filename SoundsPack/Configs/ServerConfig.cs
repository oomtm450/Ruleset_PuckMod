using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;
using System;
using System.IO;

namespace oomtm450PuckMod_SoundsPack.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_sounds_serverconfig.json used for this mod.
    /// </summary>
    public class ServerConfig : IConfig, ISubConfig {
        #region Properties
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; set; } = true;

        /// <summary>
        /// String, name of the mod.
        /// </summary>
        [JsonIgnore]
        public string ModName { get; set; } = Constants.MOD_NAME;
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
            ServerConfig config = new ServerConfig {
                ModName = SoundsPack.ModName,
            };

            try {
                string rootPath = Path.GetFullPath(".");
                string configPath = Path.Combine(rootPath, SoundsPack.ModName + "_serverconfig.json");
                if (File.Exists(configPath)) {
                    string configFileContent = File.ReadAllText(configPath);
                    config = SetConfig(configFileContent);
                    config.ModName = SoundsPack.ModName;
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
}
