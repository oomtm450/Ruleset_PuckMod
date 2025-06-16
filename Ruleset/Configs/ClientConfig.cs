using Newtonsoft.Json;
using oomtm450PuckMod_Ruleset.SystemFunc;
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
        /// Function that serialize the ClientConfig object.
        /// </summary>
        /// <returns>String, serialized ClientConfig.</returns>
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
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

            string rootPath = Path.GetFullPath(".");
            string configPath = Path.Combine(rootPath, Constants.MOD_NAME + "_clientconfig.json");
            if (File.Exists(configPath)) {
                string configFileContent = File.ReadAllText(configPath);
                config = SetConfig(configFileContent);
            }

            File.WriteAllText(configPath, config.ToString());

            Logging.Log($"Writing client config : {config}", config);

            return config;
        }
    }
}
