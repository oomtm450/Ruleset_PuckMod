using Codebase;
using Codebase.Configs;
using Newtonsoft.Json;
using System;
using System.IO;

namespace oomtm450PuckMod_Ruleset.Configs {
    /// <summary>
    /// Class containing the configuration from oomtm450_template_clientconfig.json used for this mod.
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
        public string ModName { get; } = Constants.MOD_NAME;

        /// <summary>
        /// Bool, true if the music must be played ingame.
        /// </summary>
        public bool Music { get; set; } = true;

        /// <summary>
        /// Float, volume from 0.0 to 1.0 for the music.
        /// </summary>
        public float MusicVolume { get; set; } = 0.8f;

        /// <summary>
        /// Bool, true if the custom goal horns must be set.
        /// </summary>
        public bool CustomGoalHorns { get; set; } = true;

        /// <summary>
        /// Bool, true if the refs has to be team color coded.
        /// </summary>
        public bool TeamColor2DRefs { get; set; } = true;

        /// <summary>
        /// String, full path for the config file.
        /// </summary>
        [JsonIgnore]
        private readonly string _configPath = Path.Combine(Path.GetFullPath("."), Constants.MOD_NAME + "_clientconfig.json");

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
            ClientConfig config = new ClientConfig();

            try {
                if (File.Exists(config._configPath)) {
                    string configFileContent = File.ReadAllText(config._configPath);
                    config = SetConfig(configFileContent);
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
