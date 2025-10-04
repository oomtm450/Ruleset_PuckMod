using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;
using System;
using System.IO;

namespace oomtm450PuckMod_SoundsPack.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_sounds_clientconfig.json used for this mod.
    /// </summary>
    public class ClientConfig : IConfig {
        /// <summary>
        /// Bool, true if the info logs must be printed.
        /// </summary>
        public bool LogInfo { get; set; } = true;

        /// <summary>
        /// String, name of the mod.
        /// </summary>
        [JsonIgnore]
        public string ModName { get; set; } = Constants.MOD_NAME;

        /// <summary>
        /// String, full path for the config file.
        /// </summary>
        [JsonIgnore]
        private string _configPath = "";

        /// <summary>
        /// Function that serialize the ClientConfig object.
        /// </summary>
        /// <returns>String, serialized ClientConfig.</returns>
        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Function that unserialize a ClientConfig.
        /// </summary>
        /// <param name="json">String, JSON that is the serialized ClientConfig.</param>
        /// <returns>ClientConfig, unserialized ClientConfig.</returns>
        internal static ClientConfig SetConfig(string json) {
            return JsonConvert.DeserializeObject<ClientConfig>(json);
        }

        /// <summary>
        /// Function that reads the config file for the mod and create a ClientConfig object with it.
        /// Also creates the file with the default values, if it doesn't exists.
        /// </summary>
        /// <returns>ClientConfig, parsed config.</returns>
        internal static ClientConfig ReadConfig() {
            ClientConfig config = new ClientConfig {
                ModName = SoundsPack.ModName,
            };

            try {
                config._configPath = Path.Combine(Path.GetFullPath("."), SoundsPack.ModName + "_clientconfig.json");
                if (File.Exists(config._configPath)) {
                    string configFileContent = File.ReadAllText(config._configPath);
                    string configPath = config._configPath;
                    config = SetConfig(configFileContent);
                    config._configPath = configPath;
                    config.ModName = SoundsPack.ModName;
                    Logging.Log($"Client config read.", config, true);
                }

                config.Save();
            }
            catch (Exception ex) {
                Logging.LogError($"Can't read the server config file/folder. (Permission error ?)\n{ex}", config);
            }

            return config;
        }

        internal void Save() {
            if (string.IsNullOrEmpty(_configPath)) {
                Logging.LogError($"Can't write the client config file. ({nameof(_configPath)} null or empty)", this);
                return;
            }

            try {
                File.WriteAllText(_configPath, ToString());
            }
            catch (Exception ex) {
                Logging.LogError($"Can't write the client config file. (Permission error ?)\n{ex}", this);
            }

            Logging.Log($"Wrote client config : {ToString()}", this, true);
        }
    }
}
