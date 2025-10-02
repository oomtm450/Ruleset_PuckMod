using Codebase;
using HarmonyLib;
using oomtm450PuckMod_SoundsPack.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Unity.Netcode;

namespace oomtm450PuckMod_SoundsPack {
    public class SoundsPack : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private static readonly string MOD_VERSION = "1.0.0";

        /// <summary>
        /// ReadOnlyCollection of string, collection of datanames to not log.
        /// </summary>
        private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> {
            "eventName",
        });
        #endregion

        #region Fields and Properties
        // Server-side.
        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        internal static ServerConfig ServerConfig { get; set; } = new ServerConfig();

        // Client-side and server-side.
        internal static string ModName { get; set; } = "";

        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static Harmony _harmony;

        /// <summary>
        /// Bool, true if the mod has been patched in.
        /// </summary>
        private static bool _harmonyPatched = false;

        /// <summary>
        /// Bool, true if the mod has registered with the named message handler for server/client communication.
        /// </summary>
        private static bool _hasRegisteredWithNamedMessageHandler = false;

        // Client-side.
        /// <summary>
        /// ClientConfig, config set by the client.
        /// </summary>
        internal static ClientConfig ClientConfig = new ClientConfig();

        /// <summary>
        /// DateTime, last time client asked the server for startup data.
        /// </summary>
        private static DateTime _lastDateTimeAskStartupData = DateTime.MinValue;

        /// <summary>
        /// Bool, true if the server has responded and sent the startup data.
        /// </summary>
        private static bool _serverHasResponded = false;

        /// <summary>
        /// Int, number of time client asked the server for startup data.
        /// </summary>
        private static int _askServerForStartupDataCount = 0;
        #endregion

        #region Harmony Patches
        /// <summary>
        /// Class that patches the UpdatePlayer event from UIScoreboard.
        /// </summary>
        [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.UpdatePlayer))]
        public class UIScoreboard_UpdatePlayer_Patch {
            [HarmonyPostfix]
            public static void Postfix(Player player) {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer() || string.IsNullOrEmpty(ModName))
                        return;

                    if (!_hasRegisteredWithNamedMessageHandler || !_serverHasResponded) {
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(ModName + Codebase.Constants.SOUNDS_FROM_SERVER_TO_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;

                        DateTime now = DateTime.UtcNow;
                        if (_lastDateTimeAskStartupData + TimeSpan.FromSeconds(3) < now && _askServerForStartupDataCount++ < 5) {
                            _lastDateTimeAskStartupData = now;
                            (string, string) extraSoundsInfo = (ModName, Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sounds"));
                            NetworkCommunication.SendData(Codebase.Constants.SOUNDS_MOD_NAME + Constants.ASK_SERVER_FOR_STARTUP_DATA, extraSoundsInfo.ToString(), NetworkManager.ServerClientId, Codebase.Constants.SOUNDS_FROM_CLIENT_TO_SERVER, ClientConfig);
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(UIScoreboard_UpdatePlayer_Patch)} Postfix().\n{ex}", ClientConfig);
                }
            }
        }

        /// <summary>
        /// Method that manages received data from client-server communications.
        /// </summary>
        /// <param name="clientId">Ulong, Id of the client that sent the data. (0 if the server sent the data)</param>
        /// <param name="reader">FastBufferReader, stream containing the received data.</param>
        public static void ReceiveData(ulong clientId, FastBufferReader reader) {
            try {
                string dataName, dataStr;
                if (clientId == NetworkManager.ServerClientId) // If client Id is 0, we received data from the server, so we are client-sided.
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, ClientConfig);
                else
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, ServerConfig);

                if (dataName == ModName + "_" + nameof(MOD_VERSION)) // CLIENT-SIDE
                    _serverHasResponded = true;
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(ReceiveData)}.\n{ex}", ServerConfig);
            }
        }

        /// <summary>
        /// Method that launches when the mod is being enabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully enabled.</returns>
        public bool OnEnable() {
            try {
                if (_harmonyPatched)
                    return true;

                Logging.Log($"Reading sound pack name...", ServerConfig, true);

                ModName = Assembly.GetExecutingAssembly().GetName().Name;
                ServerConfig.ModName = ModName;
                ClientConfig.ModName = ModName;

                Logging.Log($"Sound pack name is \"{ModName}\".", ServerConfig, true);

                Logging.Log($"Enabling...", ServerConfig, true);

                _harmony = new Harmony(ModName);
                _harmony.PatchAll();

                Logging.Log($"Enabled.", ServerConfig, true);

                NetworkCommunication.AddToNotLogList(DATA_NAMES_TO_IGNORE);

                if (ServerFunc.IsDedicatedServer()) {
                    Server_RegisterNamedMessageHandler();

                    Logging.Log("Setting server sided config.", ServerConfig, true);
                    ServerConfig = ServerConfig.ReadConfig();
                }
                else {
                    Logging.Log("Setting client sided config.", ServerConfig, true);
                    ClientConfig = ClientConfig.ReadConfig();
                }

                Logging.Log("Subscribing to events.", ServerConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                }
                else {
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                }

                    _harmonyPatched = true;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to enable.\n{ex}", ServerConfig);
                return false;
            }
        }

        /// <summary>
        /// Method that launches when the mod is being disabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully disabled.</returns>
        public bool OnDisable() {
            try {
                if (!_harmonyPatched)
                    return true;

                Logging.Log($"Disabling...", ServerConfig, true);

                Logging.Log("Unsubscribing from events.", ServerConfig, true);
                NetworkCommunication.RemoveFromNotLogList(DATA_NAMES_TO_IGNORE);
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    //NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Codebase.Constants.SOUNDS_FROM_CLIENT_TO_SERVER);
                }
                else {
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(ModName + Codebase.Constants.SOUNDS_FROM_SERVER_TO_CLIENT);
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", ServerConfig, true);

                _harmonyPatched = false;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}", ServerConfig);
                return false;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Method called when a client has connected (joined a server) on the server-side.
        /// Used to set server-sided stuff after the game has loaded.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnClientConnected(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;

            try {
                Server_RegisterNamedMessageHandler();
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnClientConnected)}.\n{ex}", ServerConfig);
            }
        }

        /// <summary>
        /// Method called when the client has stopped on the client-side.
        /// Used to reset the config so that it doesn't carry over between servers.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStopped(Dictionary<string, object> message) {
            if (NetworkManager.Singleton == null || ServerFunc.IsDedicatedServer())
                return;

            //Logging.Log("Event_Client_OnClientStopped", ClientConfig);

            try {
                ServerConfig = new ServerConfig();

                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_Client_OnClientStopped)}.\n{ex}", ClientConfig);
            }
        }
        #endregion

        #region Methods/Functions
        private static void Server_RegisterNamedMessageHandler() {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null && !_hasRegisteredWithNamedMessageHandler) {
                //Logging.Log($"RegisterNamedMessageHandler {Codebase.Constants.SOUNDS_FROM_CLIENT_TO_SERVER}.", ServerConfig);
                //NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Codebase.Constants.SOUNDS_FROM_CLIENT_TO_SERVER, ReceiveData);

                _hasRegisteredWithNamedMessageHandler = true;
            }
        }
        #endregion
    }
}
