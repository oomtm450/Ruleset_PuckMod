using HarmonyLib;
using oomtm450PuckMod_Ruleset.Configs;
using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing the main code for the Ruleset patch.
    /// </summary>
    public class Ruleset : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private const string MOD_VERSION = "0.3.0DEV";

        /// <summary>
        /// Const float, radius of the puck.
        /// </summary>
        private const float PUCK_RADIUS = 0.13f;

        /// <summary>
        /// Const float, radius of a player.
        /// </summary>
        private const float PLAYER_RADIUS = 0.25f;
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

        private static readonly Dictionary<ArenaElement, (double Start, double End)> ICE_Z_POSITIONS = new Dictionary<ArenaElement, (double Start, double End)> {
            { ArenaElement.BlueTeam_BlueLine, (13.0, 13.5) },
            { ArenaElement.RedTeam_BlueLine, (-13.5, -13.0) },
            { ArenaElement.CenterLine, (-0.25, 0.25) },
            { ArenaElement.BlueTeam_GoalLine, (39.75, 40) },
            { ArenaElement.RedTeam_GoalLine, (-40, -39.75) },
        };

        private static readonly Dictionary<string, (PlayerTeam Team, bool IsOffside)> _isOffside = new Dictionary<string, (PlayerTeam, bool)>();

        private static readonly Dictionary<PlayerTeam, bool> _isIcingPossible = new Dictionary<PlayerTeam, bool>{
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static readonly Dictionary<PlayerTeam, bool> _isIcingActive = new Dictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static Zone _puckZone = Zone.BlueTeam_Center;

        private static Dictionary<string, (PlayerTeam Team, Zone Zone)> _playersZone = new Dictionary<string, (PlayerTeam, Zone)>();

        private static Dictionary<string, Stopwatch> _playersLastTimePuckPossession = new Dictionary<string, Stopwatch>();

        private static InputAction _getStickLocation;

        private static bool _changedPhase = false;

        private static int _periodTimeRemaining = 0;

        private static readonly object _locker = new object();

        // Barrier collider, position 0 -19 0 is realistic.
        #endregion

        /*/// <summary>
        /// Class that patches the OnCollisionEnter event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
        public class Puck_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
                    return;

                Stick stick = GetStick(collision.gameObject);
                if (!stick)
                    return;

                Logging.Log($"Puck was hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (enter)!", _serverConfig);
            }
        }*/

        /// <summary>
        /// Class that patches the OnCollisionStay event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionStay")]
        public class Puck_OnCollisionStay_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
                        return;

                    Stick stick = GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    //Logging.Log($"Puck is being hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (stay)!", _serverConfig);

                    if (!_playersLastTimePuckPossession.TryGetValue(stick.Player.SteamId.Value.ToString(), out Stopwatch watch)) {
                        watch = new Stopwatch();
                        watch.Start();
                        _playersLastTimePuckPossession.Add(stick.Player.SteamId.Value.ToString(), watch);
                    }

                    PlayerTeam otherTeam = GetOtherTeam(stick.Player.Team.Value);
                    // Offside logic.
                    List<Zone> zones = GetTeamZones(otherTeam);
                    if (IsOffside(stick.Player.Team.Value) && (_puckZone == zones[0] || _puckZone == zones[1])) {
                        UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED");
                        Faceoff();
                    }

                    // Icing logic.
                    if (IsIcing(otherTeam) && stick.Player.PlayerPosition.Role != PlayerRole.Goalie) {
                        UIChat.Instance.Server_SendSystemChatMessage($"ICING {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                        Faceoff();
                    }
                    else {
                        if (IsIcing(stick.Player.Team.Value))
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CANCELLED");
                        ResetIcings();
                    }
                    
                    watch.Restart();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionStay_Patch Postfix().\n{ex}");
                }
            }
        }

        /// <summary>
        /// Class that patches the Server_SetPhase event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_SetPhase))]
        public class GameManager_Server_SetPhase_Patch {
            [HarmonyPrefix]
            public static bool Prefix(GamePhase phase, ref int time) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return true;

                    if (phase == GamePhase.FaceOff) {
                        lock (_locker) {
                            // Reset possession times.
                            foreach (Stopwatch watch in _playersLastTimePuckPossession.Values)
                                watch.Restart();

                            // Reset offsides.
                            _isOffside.Clear();

                            // Reset players zone.
                            _playersZone.Clear();

                            // Reset puck possessions time.
                            _playersLastTimePuckPossession.Clear();
                        }
                        // Reset icings.
                        ResetIcings();

                        _puckZone = Zone.BlueTeam_Center;
                    }

                    if (!_changedPhase)
                        return true;

                    if (phase == GamePhase.Playing) {
                        Logging.Log($"Remaining time should of been {time} seconds.", _serverConfig); // TODO : Remove debug log.
                        _changedPhase = false;
                        time = _periodTimeRemaining;
                        Logging.Log($"Remaining time is now {time} seconds.", _serverConfig); // TODO : Remove debug log.
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Prefix().\n{ex}");
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the OnCollisionExit event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionExit")]
        public class Puck_OnCollisionExit_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
                        return;

                    Stick stick = GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    // Icing logic.
                    bool icingPossible = false;
                    if (GetTeamZones(stick.Player.Team.Value, true).Any(x => x == _puckZone))
                        icingPossible = true;

                    lock (_locker)
                        _isIcingPossible[stick.Player.Team.Value] = icingPossible;
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}");
                }
            }
        }

        /// <summary>
        /// Class that patches the Update event from PlayerInput.
        /// </summary>
        [HarmonyPatch(typeof(PlayerInput), "Update")]
        public class PlayerInput_Update_Patch {
            [HarmonyPrefix]
            public static bool Prefix() {
                try {
                    if (ServerFunc.IsDedicatedServer())
                        return true;

                    UIChat chat = UIChat.Instance;

                    if (chat.IsFocused)
                        return true;

                    if (_getStickLocation.WasPressedThisFrame()) {
                        Logging.Log($"Stick position : {PlayerManager.Instance.GetLocalPlayer().Stick.BladeHandlePosition}", _clientConfig);
                    }
                        
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PlayerInput_Update_Patch Prefix().\n{ex}");
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the Update event from ServerManager.
        /// </summary>
        [HarmonyPatch(typeof(ServerManager), "Update")]
        public class ServerManager_Update_Patch {
            [HarmonyPrefix]
            public static bool Prefix() {
                Puck puck = null;
                List<Player> players = null;

                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing)
                        return true;

                    players = PlayerManager.Instance.GetPlayers();
                    puck = PuckManager.Instance.GetPuck();

                    if (players.Count == 0 || puck == null)
                        return true;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 1.\n{ex}");
                }
                try {
                    _puckZone = GetZone(puck.Rigidbody.transform, _puckZone, PUCK_RADIUS);

                    // Icing logic.
                    lock (_locker) {
                        if (_isIcingPossible[PlayerTeam.Blue] && _puckZone == Zone.RedTeam_BehindGoalLine) {
                            if (!IsIcing(PlayerTeam.Blue))
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {PlayerTeam.Blue.ToString().ToUpperInvariant()} TEAM");
                            _isIcingActive[PlayerTeam.Blue] = true;
                        }
                        if (_isIcingPossible[PlayerTeam.Red] && _puckZone == Zone.BlueTeam_BehindGoalLine) {
                            if (!IsIcing(PlayerTeam.Red))
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {PlayerTeam.Red.ToString().ToUpperInvariant()} TEAM");
                            _isIcingActive[PlayerTeam.Red] = true;
                        }
                    }

                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 2.\n{ex}");
                }
                try {
                    // Offside logic.
                    foreach (Player player in players) {
                        string playerSteamId = player.SteamId.Value.ToString();

                        lock (_locker) {
                            if (!_isOffside.TryGetValue(playerSteamId, out _))
                                _isOffside.Add(playerSteamId, (player.Team.Value, false));
                        }

                        Zone oldPlayerZone;
                        if (!_playersZone.TryGetValue(playerSteamId, out var result)) {
                            if (player.Team.Value == PlayerTeam.Red)
                                oldPlayerZone = Zone.RedTeam_Center;
                            else
                                oldPlayerZone = Zone.BlueTeam_Center;

                            _playersZone.Add(playerSteamId, (player.Team.Value, oldPlayerZone));
                        }
                        else
                            oldPlayerZone = result.Zone;

                        Zone playerZone = GetZone(player.PlayerBody.transform, oldPlayerZone, PLAYER_RADIUS);
                        _playersZone[playerSteamId] = (player.Team.Value, playerZone);

                        // Is offside.
                        bool offside = false;
                        if (player.Team.Value == PlayerTeam.Blue) {
                            if (_puckZone != Zone.RedTeam_Zone && _puckZone != Zone.RedTeam_BehindGoalLine && (playerZone == Zone.RedTeam_Zone || playerZone == Zone.RedTeam_BehindGoalLine))
                                offside = true;
                        }
                        else if (player.Team.Value == PlayerTeam.Red) {
                            if (_puckZone != Zone.BlueTeam_Zone && _puckZone != Zone.BlueTeam_BehindGoalLine && (playerZone == Zone.BlueTeam_Zone || playerZone == Zone.BlueTeam_BehindGoalLine))
                                offside = true;
                        }

                        if (offside) {
                            //if (!IsOffside(player.Team.Value))
                                //UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {player.Team.Value.ToString().ToUpperInvariant()} TEAM");
                            lock (_locker)
                                _isOffside[playerSteamId] = (player.Team.Value, true);
                        }

                        // Is not offside.
                        bool notOffside = false;
                        if (player.Team.Value == PlayerTeam.Blue) {
                            lock (_locker) {
                                if (_playersZone[playerSteamId].Zone != Zone.RedTeam_Zone && _playersZone[playerSteamId].Zone != Zone.RedTeam_BehindGoalLine && _isOffside[playerSteamId].IsOffside)
                                    notOffside = true;
                            }
                                
                        }
                        else if (player.Team.Value == PlayerTeam.Red) {
                            lock (_locker) {
                                if (_playersZone[playerSteamId].Zone != Zone.BlueTeam_Zone && _playersZone[playerSteamId].Zone != Zone.BlueTeam_BehindGoalLine && _isOffside[playerSteamId].IsOffside)
                                    notOffside = true;
                            }
                        }

                        if (notOffside) {
                            lock (_locker)
                                _isOffside[playerSteamId] = (player.Team.Value, false);
                            //if (!IsOffside(player.Team.Value))
                                //UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {player.Team.Value.ToString().ToUpperInvariant()} TEAM CANCELLED");
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 3.\n{ex}"); // TODO : No goal if offside.
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the Event_Server_OnPuckEnterTeamGoal event from GameManagerController.
        /// </summary>
        [HarmonyPatch(typeof(GameManagerController), "Event_Server_OnPuckEnterTeamGoal")]
        public class GameManagerController_GameManagerController_Patch {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message) {
                try {
                    PlayerTeam playerTeam = (PlayerTeam)message["team"];
                    playerTeam = GetOtherTeam(playerTeam);

                    // No goal if offside.
                    if (IsOffside(playerTeam)) {
                        UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {playerTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                        Faceoff();
                        return false;
                    }
                        
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManagerController_GameManagerController_Patch Prefix().\n{ex}");
                }

                return true;
            }
        }

        private static void ResetIcings() {
            lock (_locker) {
                foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingPossible.Keys))
                    _isIcingPossible[key] = false;

                foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingActive.Keys))
                    _isIcingActive[key] = false;
            }
        }

        private static void Faceoff() {
            _periodTimeRemaining = GameManager.Instance.GameState.Value.Time;
            _changedPhase = true;
            GameManager.Instance.Server_SetPhase(GamePhase.FaceOff,
                ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.phaseDurationMap[GamePhase.FaceOff]);
        }

        private static Zone GetZone(Transform transform, Zone oldZone, float radius) {
            float zMax = transform.position.z + radius;
            
            // Red team.
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_GoalLine].Start) {
                return Zone.RedTeam_BehindGoalLine;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_GoalLine].End && oldZone == Zone.RedTeam_BehindGoalLine) {
                if (oldZone == Zone.RedTeam_BehindGoalLine)
                    return Zone.RedTeam_BehindGoalLine;
                else
                    return Zone.RedTeam_Zone;
            }

            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_BlueLine].Start) {
                return Zone.RedTeam_Zone;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_BlueLine].End) {
                if (oldZone == Zone.RedTeam_Zone)
                    return Zone.RedTeam_Zone;
                else
                    return Zone.RedTeam_Center;
            }

            if (zMax < ICE_Z_POSITIONS[ArenaElement.CenterLine].Start) {
                return Zone.RedTeam_Center;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.CenterLine].End && oldZone == Zone.RedTeam_Center) {
                return Zone.RedTeam_Center;
            }

            // Both team.
            if (zMax < ICE_Z_POSITIONS[ArenaElement.RedTeam_BlueLine].End) {
                if (oldZone == Zone.RedTeam_Center)
                    return Zone.RedTeam_Center;
                else
                    return Zone.BlueTeam_Center;
            }

            // Blue team.
            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_BlueLine].Start) {
                return Zone.BlueTeam_Center;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_BlueLine].End) {
                if (oldZone == Zone.BlueTeam_Center)
                    return Zone.BlueTeam_Center;
                else
                    return Zone.BlueTeam_Zone;
            }

            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_GoalLine].Start) {
                return Zone.BlueTeam_Zone;
            }
            if (zMax < ICE_Z_POSITIONS[ArenaElement.BlueTeam_GoalLine].End) {
                if (oldZone == Zone.BlueTeam_Zone)
                    return Zone.BlueTeam_Zone;
                else
                    return Zone.BlueTeam_BehindGoalLine;
            }

            return Zone.BlueTeam_BehindGoalLine;
        }

        private static List<Zone> GetTeamZones(PlayerTeam team, bool includeCenter = false) {
            switch (team) { // TODO : Optimize with pre made lists.
                case PlayerTeam.Blue:
                    List<Zone> blueZones = new List<Zone> { Zone.BlueTeam_Zone, Zone.BlueTeam_BehindGoalLine };
                    if (includeCenter)
                        blueZones.Add(Zone.BlueTeam_Center);
                    return blueZones;

                case PlayerTeam.Red:
                    List<Zone> redZones = new List<Zone> { Zone.RedTeam_Zone, Zone.RedTeam_BehindGoalLine };
                    if (includeCenter)
                        redZones.Add(Zone.RedTeam_Center);
                    return redZones;
            }

            return new List<Zone> { Zone.None };
        }

        private static PlayerTeam GetOtherTeam(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return PlayerTeam.Red;
            if (team == PlayerTeam.Red)
                return PlayerTeam.Blue;

            return PlayerTeam.None;
        }

        private static bool IsOffside(PlayerTeam team) {
            lock (_locker)
                return _isOffside.Where(x => x.Value.Team == team).Any(x => x.Value.IsOffside);
        }

        private static bool IsIcing(PlayerTeam team) {
            lock (_locker)
                return _isIcingActive[team];
        }

        /// <summary>
        /// Function that returns a Stick instance from a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject, GameObject to use.</param>
        /// <returns>Stick, found Stick object or null.</returns>
        private static Stick GetStick(GameObject gameObject) {
            return gameObject.GetComponent<Stick>();
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

                    case "config": // CLIENT-SIDE : Set the server config on the client to use later for the Ruleset logic, since the logic happens on the client.
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

                    _getStickLocation = new InputAction(binding: "<keyboard>/#(o)");
                    _getStickLocation.Enable();
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

                _getStickLocation.Disable();
                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", _serverConfig, true);
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}");
                return false;
            }
        }
    }

    public enum ArenaElement {
        BlueTeam_BlueLine,
        RedTeam_BlueLine,
        CenterLine,
        BlueTeam_GoalLine,
        RedTeam_GoalLine,
    }

    public enum Zone {
        None,
        RedTeam_BehindGoalLine,
        BlueTeam_BehindGoalLine,
        RedTeam_Zone,
        BlueTeam_Zone,
        RedTeam_Center,
        BlueTeam_Center,
    }
}
