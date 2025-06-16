using HarmonyLib;
using oomtm450PuckMod_Template.Configs;
using oomtm450PuckMod_Template.SystemFunc;
using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace oomtm450PuckMod_Template {
    /// <summary>
    /// Class containing the main code for the Template patch.
    /// </summary>
    public class Template : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private const string MOD_VERSION = "0.1.0DEV";
        #endregion

        #region Fields
        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static readonly Harmony _harmony = new Harmony(Constants.MOD_NAME);

        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        private static ServerConfig _serverConfig = new ServerConfig();

        /// <summary>
        /// ServerConfig, config set by the client.
        /// </summary>
        private static ClientConfig _clientConfig = new ClientConfig();
        #endregion

        /// <summary>
        /// Class that patches the Event_Client_OnPositionSelectClickPosition event from PlayerPositionManagerController.
        /// </summary>
        [HarmonyPatch(typeof(PlayerPositionManagerController), "Event_Client_OnPositionSelectClickPosition")]
        public class PlayerPositionManagerControllerPatch {
            /// <summary>
            /// Prefix patch function to check if the player is authorized to claim the selected position.
            /// </summary>
            /// <param name="message">Dictionary of string and object, content of the event.</param>
            /// <returns>Bool, true if the user is authorized.</returns>
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message) {
                // If this is the server or the config was not sent by server (mod not installed on the server ?), do not use the patch.
                if (ServerFunc.IsDedicatedServer() || !_serverConfig.SentByServer)
                    return true;

                Logging.Log("Event_Client_OnPositionSelectClickPosition", _clientConfig);

                /* From this point on to the end of the function, this is custom code that is left for example. */
                PlayerPosition currentPPosition = (PlayerPosition)message["playerPosition"];

                // Goalie bypass.
                if (PlayerFunc.IsGoalie(currentPPosition, false))
                    return true;

                // Admin bypass.
                if (_serverConfig.AdminBypass && PlayerFunc.IsAdmin(_serverConfig, _clientConfig))
                    return true;

                // Get blue team infos.
                bool hasBlueGoalie = false;
                int numberOfBlueSkaters = 0;
                foreach (PlayerPosition pPosition in PlayerPositionManager.Instance.BluePositions) {
                    if (PlayerFunc.IsAttacker(pPosition))
                        numberOfBlueSkaters++;
                    if (PlayerFunc.IsGoalie(pPosition))
                        hasBlueGoalie = true;
                }

                // Get red team infos.
                bool hasRedGoalie = false;
                int numberOfRedSkaters = 0;
                foreach (PlayerPosition pPosition in PlayerPositionManager.Instance.RedPositions) {
                    if (PlayerFunc.IsAttacker(pPosition))
                        numberOfRedSkaters++;
                    if (PlayerFunc.IsGoalie(pPosition))
                        hasRedGoalie = true;
                }

                int maxNumberOfSkaters = _serverConfig.MaxNumberOfSkaters;
                bool teamBalancing = TeamBalancing(hasBlueGoalie, hasRedGoalie);

                // Get certain informations depending the player's team.
                int numberOfSkaters;
                bool goalieAvailable = true;
                switch (currentPPosition.Team) {
                    case PlayerTeam.Blue:
                        numberOfSkaters = numberOfBlueSkaters;

                        if (teamBalancing) {
                            int newMaxNumberOfSkaters = numberOfRedSkaters + _serverConfig.TeamBalanceOffset + 1;
                            if (newMaxNumberOfSkaters < maxNumberOfSkaters)
                                maxNumberOfSkaters = newMaxNumberOfSkaters;
                        }

                        if (hasBlueGoalie)
                            goalieAvailable = false;

                        break;

                    case PlayerTeam.Red:
                        numberOfSkaters = numberOfRedSkaters;

                        if (teamBalancing) {
                            int newMaxNumberOfSkaters = numberOfBlueSkaters + _serverConfig.TeamBalanceOffset + 1;
                            if (newMaxNumberOfSkaters < maxNumberOfSkaters)
                                maxNumberOfSkaters = newMaxNumberOfSkaters;
                        }

                        if (hasRedGoalie)
                            goalieAvailable = false;

                        break;

                    default:
                        Logging.LogError("No team assigned to the current player position ?");
                        return true;
                }

                /* Logging for client debugging */
                if (teamBalancing)
                    Logging.Log("Team balancing is on.", _clientConfig);

                Logging.Log($"Current team : {nameof(currentPPosition.Team)} with {numberOfSkaters} skaters.", _clientConfig);
                Logging.Log($"Current number of skaters on red team : {numberOfRedSkaters}.", _clientConfig);
                Logging.Log($"Current number of skaters on blue team : {numberOfBlueSkaters}.", _clientConfig);
                /*                              */

                if (numberOfSkaters >= maxNumberOfSkaters) {
                    if (teamBalancing) {
                        if (goalieAvailable)
                            UIChat.Instance.AddChatMessage($"Teams are unbalanced ({maxNumberOfSkaters}). Go goalie or switch teams.");
                        else
                            UIChat.Instance.AddChatMessage($"Teams are unbalanced ({maxNumberOfSkaters}). Switch teams.");
                    }
                    else {
                        if (goalieAvailable)
                            UIChat.Instance.AddChatMessage($"Team is full ({maxNumberOfSkaters}). Only {PlayerFunc.GOALIE_POSITION} position is available.");
                        else
                            UIChat.Instance.AddChatMessage($"Team is full ({maxNumberOfSkaters}). Switch teams.");
                    }
                        
                    return false;
                }

                return true;

                /* End of example code. */
            }
        }

        /// <summary>
        /// Method called when the client has started on the client-side.
        /// Used to register to the server messaging (config sync and version check).
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStarted(Dictionary<string, object> message) {
            if (NetworkManager.Singleton == null || ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_Client_OnClientStarted", _clientConfig);

            try {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER, ReceiveData);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStarted.\n{ex}");
            }
        }

        /// <summary>
        /// Method called when the client has stopped on the client-side.
        /// Used to reset the config so that it doesn't carry over between servers.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStopped(Dictionary<string, object> message) {
            Logging.Log("Event_Client_OnClientStopped", _clientConfig);

            try {
                _serverConfig = new ServerConfig();
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStopped.\n{ex}");
            }
        }

        /// <summary>
        /// Method called when a client has "spawned" (joined a server) on the server-side.
        /// Used to send data to the new client that has connected (config and mod version).
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnPlayerSpawned(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;
            
            Logging.Log("Event_OnPlayerSpawned", _serverConfig);

            try {
                Player player = (Player)message["player"];

                NetworkCommunication.SendData(nameof(MOD_VERSION), MOD_VERSION, player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
                NetworkCommunication.SendData("config", _serverConfig.ToString(), player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnPlayerSpawned.\n{ex}");
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
                if (clientId == 0) { // If client Id is 0, we received data from the server, so we are client-sided.
                    Logging.Log("ReceiveData", _clientConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _clientConfig);
                }
                else {
                    Logging.Log("ReceiveData", _serverConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _serverConfig);
                }

                switch (dataName) {
                    case nameof(MOD_VERSION): // CLIENT-SIDE : Mod version check, kick if client and server versions are not the same.
                        if (MOD_VERSION == dataStr) // TODO : Move the kick later so that it doesn't break anything. Maybe even add a chat message and a 3-5 sec wait.
                            break;

                        NetworkCommunication.SendData("kick", "1", clientId, Constants.FROM_SERVER, _serverConfig);
                        break;

                    case "config": // CLIENT-SIDE : Set the server config on the client to use later for the Template logic, since the logic happens on the client.
                        _serverConfig = ServerConfig.SetConfig(dataStr);
                        break;

                    case "kick": // SERVER-SIDE : Kick the client that asked to be kicked.
                        if (dataStr != "1")
                            break;

                        Logging.Log($"Kicking client {clientId}.", _serverConfig);
                        NetworkManager.Singleton.DisconnectClient(clientId,
                            $"Mod is out of date. Please restart your game or unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop to update.");
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveData.\n{ex}");
            }
        }

        /// <summary>
        /// Method that launches when the mod is being enabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully enabled.</returns>
        public bool OnEnable() {
            try {
                Logging.Log($"Enabling...", _serverConfig, true);

                _harmony.PatchAll();

                Logging.Log($"Enabled.", _serverConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    Logging.Log("Setting server sided config.", _serverConfig, true);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT, ReceiveData);

                    _serverConfig = ServerConfig.ReadConfig(ServerManager.Instance.AdminSteamIds);
                }
                else {
                    Logging.Log("Setting client sided config.", _serverConfig, true);
                    _clientConfig = ClientConfig.ReadConfig();
                }

                Logging.Log("Subscribing to events.", _serverConfig, true);
                EventManager.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                EventManager.Instance.AddEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);

                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to enable.\n{ex}");
                return false;
            }
        }

        /// <summary>
        /// Method that launches when the mod is being disabled.
        /// </summary>
        /// <returns>Bool, true if the mod successfully disabled.</returns>
        public bool OnDisable() {
            try {
                Logging.Log("Unsubscribing from events.", _serverConfig, true);

                EventManager.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                EventManager.Instance.RemoveEventListener("Event_OnPlayerSpawned", Event_OnPlayerSpawned);

                Logging.Log($"Disabling...", _serverConfig, true);

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", _serverConfig, true);
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}");
                return false;
            }
        }

        /// <summary>
        /// Function that returns true if team balancing is activated.
        /// </summary>
        /// <param name="hasBlueGoalie">Bool, true if blue team has a goalie.</param>
        /// <param name="hasRedGoalie">Bool, true if red team has a goalie.</param>
        /// <returns>Bool, true if team balancing is activated.</returns>
        private static bool TeamBalancing(bool hasBlueGoalie, bool hasRedGoalie) {
            if (_serverConfig.TeamBalancing)
                return true;

            if (!_serverConfig.TeamBalancingGoalie)
                return false;

            if (hasBlueGoalie && hasRedGoalie)
                return false;

            if (hasBlueGoalie || hasRedGoalie)
                return true;

            return false;
        }
    }
}
