using Codebase;
using HarmonyLib;
using oomtm450PuckMod_Stats.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace oomtm450PuckMod_Stats {
    public class Stats : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private static readonly string MOD_VERSION = "0.1.0DEV1";

        /// <summary>
        /// List of string, last released versions of the mod.
        /// </summary>
        private static readonly ReadOnlyCollection<string> OLD_MOD_VERSIONS = new ReadOnlyCollection<string>(new List<string> {
            //"0.1.0",
        });

        /// <summary>
        /// Const string, data name for batching the SOG.
        /// </summary>
        private const string BATCH_SOG = Constants.MOD_NAME + "BATCHSOG";

        /// <summary>
        /// Const string, data name for resetting the SOG.
        /// </summary>
        private const string RESET_SOG = Constants.MOD_NAME + "RESETSOG";

        /// <summary>
        /// Const string, data name for batching the save percentage.
        /// </summary>
        private const string BATCH_SAVEPERC = Constants.MOD_NAME + "BATCHSAVEPERC";

        /// <summary>
        /// Const string, data name for resetting the save percentage.
        /// </summary>
        private const string RESET_SAVEPERC = Constants.MOD_NAME + "RESETSAVEPERC";

        private const string SOG_HEADER_LABEL_NAME = "SOGHeaderLabel";

        private const string SOG_LABEL = "SOGLabel";

        /// <summary>
        /// Const string, tag to ask the server for the startup data.
        /// </summary>
        private const string ASK_SERVER_FOR_STARTUP_DATA = Constants.MOD_NAME + "ASKDATA";
        #endregion

        #region Fields
        // Server-side.
        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        internal static ServerConfig _serverConfig = new ServerConfig();

        private static bool? _rulesetModEnabled = null;

        /// <summary>
        /// LockDictionary of ulong and string, dictionary of all players
        /// </summary>
        private static readonly LockDictionary<ulong, string> _players_ClientId_SteamId = new LockDictionary<ulong, string>();

        private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

        private static readonly LockDictionary<PlayerTeam, SaveCheck> _checkIfPuckWasSaved = new LockDictionary<PlayerTeam, SaveCheck> {
            { PlayerTeam.Blue, new SaveCheck() },
            { PlayerTeam.Red, new SaveCheck() },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _lastShotWasCounted = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, true },
            { PlayerTeam.Red, true },
        };

        private static readonly LockDictionary<PlayerTeam, string> _lastPlayerOnPuckTipIncludedSteamId = new LockDictionary<PlayerTeam, string> { // TODO : Create a communication with Ruleset.
            { PlayerTeam.Blue, "" },
            { PlayerTeam.Red, "" },
        };

        private static PuckRaycast _puckRaycast;

        // Server-side from Ruleset.
        private static bool _paused = false; // TODO : Create a communication with Ruleset.

        // Client-side and server-side.
        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static readonly Harmony _harmony = new Harmony(Constants.MOD_NAME);

        /// <summary>
        /// Bool, true if the mod has been patched in.
        /// </summary>
        private static bool _harmonyPatched = false;

        /// <summary>
        /// Bool, true if the mod has registered with the named message handler for server/client communication.
        /// </summary>
        private static bool _hasRegisteredWithNamedMessageHandler = false;

        private static readonly LockDictionary<string, int> _sog = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, (int Saves, int Shots)> _savePerc = new LockDictionary<string, (int Saves, int Shots)>();

        // Client-side.
        /// <summary>
        /// ClientConfig, config set by the client.
        /// </summary>
        internal static ClientConfig _clientConfig = new ClientConfig();

        /// <summary>
        /// DateTime, last time client asked the server for startup data.
        /// </summary>
        private static DateTime _lastDateTimeAskStartupData = DateTime.MinValue;

        /// <summary>
        /// Bool, true if the server has responded and sent the startup data.
        /// </summary>
        private static bool _serverHasResponded = false;

        /// <summary>
        /// Bool, true if the client asked to be kicked because of versionning problems.
        /// </summary>
        private static bool _askForKick = false;

        /// <summary>
        /// Bool, true if the client needs to notify the user that the server is running an out of date version of the mod.
        /// </summary>
        private static bool _addServerModVersionOutOfDateMessage = false;

        /// <summary>
        /// Int, number of time client asked the server for startup data.
        /// </summary>
        private static int _askServerForStartupDataCount = 0;

        private static readonly List<string> _hasUpdatedUIScoreboard = new List<string>();

        private static readonly LockDictionary<string, Label> _sogLabels = new LockDictionary<string, Label>();
        #endregion

        #region Harmony Patches
        /// <summary>
        /// Class that patches the Server_SpawnPuck event from PuckManager.
        /// </summary>
        [HarmonyPatch(typeof(PuckManager), nameof(PuckManager.Server_SpawnPuck))]
        public class PuckManager_Server_SpawnPuck_Patch {
            [HarmonyPostfix]
            public static void Postfix(ref Puck __result, Vector3 position, Quaternion rotation, Vector3 velocity, bool isReplay) {
                try {
                    // If this is not the server or this is a replay or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || isReplay || (GameManager.Instance.Phase != GamePhase.Playing && GameManager.Instance.Phase != GamePhase.FaceOff))
                        return;

                    __result.gameObject.AddComponent<PuckRaycast>();
                    _puckRaycast = __result.gameObject.GetComponent<PuckRaycast>();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Postfix().\n{ex}", _serverConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the Server_GoalScored event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_GoalScored))]
        public class GameManager_Server_GoalScored_Patch {
            [HarmonyPrefix]
            public static bool Prefix(PlayerTeam team, ref Player lastPlayer, ref Player goalPlayer, ref Player assistPlayer, ref Player secondAssistPlayer, Puck puck) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || RulesetModEnabled())
                        return true;

                    if (goalPlayer != null) {
                        Player lastTouchPlayerTipIncluded = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team]).FirstOrDefault();
                        if (lastTouchPlayerTipIncluded != null && lastTouchPlayerTipIncluded.SteamId.Value.ToString() != goalPlayer.SteamId.Value.ToString()) {
                            secondAssistPlayer = assistPlayer;
                            assistPlayer = goalPlayer;
                            goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team]).FirstOrDefault();

                            while (assistPlayer != null && assistPlayer.SteamId.Value.ToString() == goalPlayer.SteamId.Value.ToString()) {
                                assistPlayer = secondAssistPlayer;
                                secondAssistPlayer = null;
                            }

                            if (secondAssistPlayer != null && (secondAssistPlayer.SteamId.Value.ToString() == assistPlayer.SteamId.Value.ToString() || secondAssistPlayer.SteamId.Value.ToString() == goalPlayer.SteamId.Value.ToString()))
                                secondAssistPlayer = null;
                        }
                        SendSavePercDuringGoal(team, SendSOGDuringGoal(goalPlayer));
                        return true;
                    }

                    // If own goal, add goal attribution to last player on puck on the other team.
                    UIChat.Instance.Server_SendSystemChatMessage($"OWN GOAL BY {PlayerManager.Instance.GetPlayerBySteamId(_lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(team)]).Username.Value}");
                    goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team]).FirstOrDefault();

                    bool saveWasCounted = false;
                    if (goalPlayer != null) {
                        lastPlayer = goalPlayer;
                        saveWasCounted = SendSOGDuringGoal(goalPlayer);
                    }

                    SendSavePercDuringGoal(team, saveWasCounted);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_GoalScored_Patch Prefix().\n{ex}", _serverConfig);
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the RemovePlayer event from UIScoreboard.
        /// </summary>
        [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.RemovePlayer))]
        public class UIScoreboard_RemovePlayer_Patch {
            [HarmonyPostfix]
            public static void Postfix(Player player) {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return;

                    _sogLabels.Remove(player.SteamId.Value.ToString());
                    _hasUpdatedUIScoreboard.Remove(player.SteamId.Value.ToString());
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIScoreboard_RemovePlayer_Patch Postfix().\n{ex}", _clientConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the Server_ResetGameState event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_ResetGameState))]
        public class GameManager_Server_ResetGameState_Patch {
            [HarmonyPostfix]
            public static void Postfix(bool resetPhase) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return;

                    // Reset s%.
                    List<Player> players = PlayerManager.Instance.GetPlayers();
                    foreach (string key in new List<string>(_savePerc.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _savePerc[key] = (0, 0);
                        else
                            _savePerc.Remove(key);
                    }
                    NetworkCommunication.SendDataToAll(RESET_SAVEPERC, "1", Constants.FROM_SERVER_TO_CLIENT, _serverConfig);

                    // Reset SOG.
                    foreach (string key in new List<string>(_sog.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _sog[key] = 0;
                        else
                            _sog.Remove(key);
                    }
                    NetworkCommunication.SendDataToAll(RESET_SOG, "1", Constants.FROM_SERVER_TO_CLIENT, _serverConfig);

                    _sentOutOfDateMessage.Clear();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_ResetGameState_Patch Postfix().\n{ex}", _serverConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the Update event from ServerManager.
        /// </summary>
        [HarmonyPatch(typeof(ServerManager), "Update")]
        public class ServerManager_Update_Patch {
            [HarmonyPostfix]
            public static void Postfix() {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing || _paused)
                        return;

                    foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys)) {
                        SaveCheck saveCheck = _checkIfPuckWasSaved[key];
                        if (!saveCheck.HasToCheck) {
                            _checkIfPuckWasSaved[key] = new SaveCheck();
                            continue;
                        }

                        //Logging.Log($"kvp.Check {saveCheck.FramesChecked} for team net {key} by {saveCheck.ShooterSteamId}.", _serverConfig, true);

                        string shotPlayerSteamId = saveCheck.ShooterSteamId;
                        PlayerTeam shotPlayerTeam = PlayerManager.Instance.GetPlayerBySteamId(shotPlayerSteamId).Team.Value;
                        if (!_puckRaycast.PuckIsGoingToNet[key] && !_lastShotWasCounted[shotPlayerTeam]) {
                            if (!_sog.TryGetValue(shotPlayerSteamId, out int _))
                                _sog.Add(shotPlayerSteamId, 0);

                            _sog[shotPlayerSteamId] += 1;
                            NetworkCommunication.SendDataToAll(Codebase.Constants.SOG + shotPlayerSteamId, _sog[shotPlayerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
                            LogSOG(shotPlayerSteamId, _sog[shotPlayerSteamId]);

                            _lastShotWasCounted[shotPlayerTeam] = true;

                            // Get other team goalie.
                            Player goalie = Codebase.PlayerFunc.GetOtherTeamGoalie(shotPlayerTeam);
                            if (goalie != null) {
                                string _goaliePlayerSteamId = goalie.SteamId.Value.ToString();
                                if (!_savePerc.TryGetValue(_goaliePlayerSteamId, out var savePercValue)) {
                                    _savePerc.Add(_goaliePlayerSteamId, (0, 0));
                                    savePercValue = (0, 0);
                                }

                                (int saves, int sog) = _savePerc[_goaliePlayerSteamId] = (++savePercValue.Saves, ++savePercValue.Shots);

                                NetworkCommunication.SendDataToAll(Codebase.Constants.SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
                                LogSavePerc(_goaliePlayerSteamId, saves, sog);
                            }

                            _checkIfPuckWasSaved[key] = new SaveCheck();
                        }
                        else {
                            if (++saveCheck.FramesChecked > ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate)
                                _checkIfPuckWasSaved[key] = new SaveCheck();
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Postfix().\n{ex}", _serverConfig);
                }

                return;
            }
        }

        /// <summary>
        /// Class that patches the UpdatePlayer event from UIScoreboard.
        /// </summary>
        [HarmonyPatch(typeof(UIScoreboard), nameof(UIScoreboard.UpdatePlayer))]
        public class UIScoreboard_UpdatePlayer_Patch {
            [HarmonyPostfix]
            public static void Postfix(Player player) {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return;

                    if (!_hasRegisteredWithNamedMessageHandler || !_serverHasResponded) {
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;

                        DateTime now = DateTime.UtcNow;
                        if (_lastDateTimeAskStartupData + TimeSpan.FromSeconds(1) < now && _askServerForStartupDataCount++ < 10) {
                            _lastDateTimeAskStartupData = now;
                            NetworkCommunication.SendData(ASK_SERVER_FOR_STARTUP_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, _clientConfig);
                        }
                    }
                    else if (_askForKick) {
                        _askForKick = false;
                        NetworkCommunication.SendData(Constants.MOD_NAME + "_kick", "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, _clientConfig);
                    }
                    else if (_addServerModVersionOutOfDateMessage) {
                        _addServerModVersionOutOfDateMessage = false;
                        UIChat.Instance.AddChatMessage($"{player.Username.Value} : Server's {Constants.WORKSHOP_MOD_NAME} mod is out of date. Some functionalities might not work properly.");
                    }

                    ScoreboardModifications(true);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIScoreboard_UpdateServer_Patch Postfix().\n{ex}", _clientConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the OnCollisionEnter event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
        public class Puck_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Puck __instance, Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing)
                    return;

                try {
                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        if (_puckRaycast.PuckIsGoingToNet[playerBody.Player.Team.Value]) {
                            if (PlayerFunc.IsGoalie(playerBody.Player)) {
                                string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(playerBody.Player.Team.Value)];
                                if (!string.IsNullOrEmpty(shooterSteamId)) {
                                    _checkIfPuckWasSaved[playerBody.Player.Team.Value] = new SaveCheck {
                                        HasToCheck = true,
                                        ShooterSteamId = shooterSteamId,
                                    };
                                }
                            }
                            // Use else condition here to add a shot blocked stat.
                        }
                        return;
                    }

                    if (!stick.Player)
                        return;

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);

                    if (_puckRaycast.PuckIsGoingToNet[stick.Player.Team.Value]) {
                        if (PlayerFunc.IsGoalie(stick.Player)) {
                            string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(stick.Player.Team.Value)];
                            if (!string.IsNullOrEmpty(shooterSteamId)) {
                                _checkIfPuckWasSaved[stick.Player.Team.Value] = new SaveCheck {
                                    HasToCheck = true,
                                    ShooterSteamId = shooterSteamId,
                                };
                            }
                        }
                        // Use else condition here to add a shot blocked stat.
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionEnter_Patch Postfix().\n{ex}", _serverConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the OnCollisionStay event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionStay")]
        public class Puck_OnCollisionStay_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing)
                        return;

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        _lastPlayerOnPuckTipIncludedSteamId[playerBody.Player.Team.Value] = playerBody.Player.SteamId.Value.ToString();

                        return;
                    }

                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = stick.Player.SteamId.Value.ToString();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionStay_Patch Postfix().\n{ex}", _serverConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the OnCollisionExit event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionExit")]
        public class Puck_OnCollisionExit_Patch {
            [HarmonyPostfix]
            public static void Postfix(Puck __instance, Collision collision) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing)
                        return;

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = stick.Player.SteamId.Value.ToString();
                    _lastShotWasCounted[stick.Player.Team.Value] = false;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}", _serverConfig);
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

                    if (phase == GamePhase.FaceOff || phase == GamePhase.Warmup || phase == GamePhase.GameOver) {
                        // Reset puck was saved states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
                            _checkIfPuckWasSaved[key] = new SaveCheck();

                        // Reset player on puck.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
                            _lastPlayerOnPuckTipIncludedSteamId[key] = "";
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Prefix().\n{ex}", _serverConfig);
                }

                return true;
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

                Logging.Log($"Enabling...", _serverConfig, true);

                _harmony.PatchAll();

                Logging.Log($"Enabled.", _serverConfig, true);

                //NetworkCommunication.AddToNotLogList(DATA_NAMES_TO_IGNORE);

                if (ServerFunc.IsDedicatedServer()) {
                    Server_RegisterNamedMessageHandler();

                    Logging.Log("Setting server sided config.", _serverConfig, true);
                    _serverConfig = ServerConfig.ReadConfig();
                }
                else {
                    Logging.Log("Setting client sided config.", _serverConfig, true);
                    _clientConfig = ClientConfig.ReadConfig();
                }

                Logging.Log("Subscribing to events.", _serverConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                }
                else {
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                }

                _harmonyPatched = true;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to enable.\n{ex}", _serverConfig);
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

                Logging.Log($"Disabling...", _serverConfig, true);

                Logging.Log("Unsubscribing from events.", _serverConfig, true);
                //NetworkCommunication.RemoveFromNotLogList(DATA_NAMES_TO_IGNORE);
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Codebase.Constants.STATS_FROM_SERVER_TO_SERVER);
                }
                else {
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    Event_Client_OnClientStopped(new Dictionary<string, object>());
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT);
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _rulesetModEnabled = false;
                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                ScoreboardModifications(false);

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", _serverConfig, true);

                _harmonyPatched = false;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}", _serverConfig);
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

            Logging.Log("Event_OnClientConnected", _serverConfig);

            try {
                Server_RegisterNamedMessageHandler();
                CheckForRulesetMod();

                ulong clientId = (ulong)message["clientId"];
                string clientSteamId = PlayerManager.Instance.GetPlayerByClientId(clientId).SteamId.Value.ToString();
                try {
                    _players_ClientId_SteamId.Add(clientId, "");
                }
                catch {
                    _players_ClientId_SteamId.Remove(clientId);
                    _players_ClientId_SteamId.Add(clientId, "");
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientConnected.\n{ex}", _serverConfig);
            }
        }

        /// <summary>
        /// Method called when a client has disconnect (left a server) on the server-side.
        /// Used to unset data linked to the player like rule status.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnClientDisconnected(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_OnClientDisconnected", _serverConfig);

            try {
                ulong clientId = (ulong)message["clientId"];
                string clientSteamId;
                try {
                    clientSteamId = _players_ClientId_SteamId[clientId];
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(_players_ClientId_SteamId)}.", _serverConfig);
                    return;
                }

                _sentOutOfDateMessage.Remove(clientId);

                _sog.Remove(clientSteamId);
                _savePerc.Remove(clientSteamId);

                _players_ClientId_SteamId.Remove(clientId);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientDisconnected.\n{ex}", _serverConfig);
            }
        }

        /// <summary>
        /// Method called when the client has stopped on the client-side.
        /// Used to reset the config so that it doesn't carry over between servers.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStopped(Dictionary<string, object> message) {
            //Logging.Log("Event_Client_OnClientStopped", _clientConfig);

            try {
                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                ScoreboardModifications(false);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStopped.\n{ex}", _clientConfig);
            }
        }

        public static void Event_OnPlayerRoleChanged(Dictionary<string, object> message) {
            // Use the event to link client Ids to Steam Ids.
            Dictionary<ulong, string> players_ClientId_SteamId_ToChange = new Dictionary<ulong, string>();
            foreach (var kvp in _players_ClientId_SteamId) {
                if (string.IsNullOrEmpty(kvp.Value))
                    players_ClientId_SteamId_ToChange.Add(kvp.Key, PlayerManager.Instance.GetPlayerByClientId(kvp.Key).SteamId.Value.ToString());
            }

            foreach (var kvp in players_ClientId_SteamId_ToChange) {
                if (!string.IsNullOrEmpty(kvp.Value)) {
                    _players_ClientId_SteamId[kvp.Key] = kvp.Value;
                    Logging.Log($"Added clientId {kvp.Key} linked to Steam Id {kvp.Value}.", _serverConfig);
                }
            }

            Player player = (Player)message["player"];

            string playerSteamId = player.SteamId.Value.ToString();

            if (string.IsNullOrEmpty(playerSteamId))
                return;

            PlayerRole newRole = (PlayerRole)message["newRole"];

            if (newRole != PlayerRole.Goalie) {
                if (!_sog.TryGetValue(playerSteamId, out int _))
                    _sog.Add(playerSteamId, 0);

                NetworkCommunication.SendDataToAll(Codebase.Constants.SOG + playerSteamId, _sog[playerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
            }
            else {
                if (!_savePerc.TryGetValue(playerSteamId, out var _))
                    _savePerc.Add(playerSteamId, (0, 0));

                NetworkCommunication.SendDataToAll(Codebase.Constants.SAVEPERC + playerSteamId, _savePerc[playerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
            }
        }
        #endregion

        #region Methods/Functions
        private static void Server_RegisterNamedMessageHandler() {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null && !_hasRegisteredWithNamedMessageHandler) {
                Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT_TO_SERVER}.", _serverConfig);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER, ReceiveData);

                Logging.Log($"RegisterNamedMessageHandler {Codebase.Constants.STATS_FROM_SERVER_TO_SERVER}.", _serverConfig);
                NetworkManager.Singleton.CustomMessagingManager.
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Codebase.Constants.STATS_FROM_SERVER_TO_SERVER, ReceiveDataServerToServer);

                _hasRegisteredWithNamedMessageHandler = true;
            }
        }

        private static void CheckForRulesetMod() {
            if (ModManagerV2.Instance == null || ModManagerV2.Instance.EnabledModIds == null)
                return;

            _rulesetModEnabled = ModManagerV2.Instance.EnabledModIds.Contains(3501446576) || ModManagerV2.Instance.EnabledModIds.Contains(3500559233);
            Logging.Log($"{nameof(_rulesetModEnabled)} : {_rulesetModEnabled}", _serverConfig, true); // TODO : Remove debug logs.
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
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _clientConfig);
                else
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _serverConfig);

                switch (dataName) {
                    case Constants.MOD_NAME + "_" + nameof(MOD_VERSION): // CLIENT-SIDE : Mod version check, kick if client and server versions are not the same.
                        _serverHasResponded = true;
                        if (MOD_VERSION == dataStr) // TODO : Maybe add a chat message and a 3-5 sec wait.
                            break;
                        else if (OLD_MOD_VERSIONS.Contains(dataStr)) {
                            _addServerModVersionOutOfDateMessage = true;
                            break;
                        }

                        _askForKick = true;
                        break;

                    case Constants.MOD_NAME + "_kick": // SERVER-SIDE : Kick the client that asked to be kicked.
                        if (dataStr != "1")
                            break;

                        Logging.Log($"Kicking client {clientId}.", _serverConfig);
                        //NetworkManager.Singleton.DisconnectClient(clientId,
                        //$"Mod is out of date. Please unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop and restart your game to update.");

                        if (!_sentOutOfDateMessage.TryGetValue(clientId, out DateTime lastCheckTime)) {
                            lastCheckTime = DateTime.MinValue;
                            _sentOutOfDateMessage.Add(clientId, lastCheckTime);
                        }

                        DateTime utcNow = DateTime.UtcNow;
                        if (lastCheckTime + TimeSpan.FromSeconds(900) < utcNow) {
                            if (string.IsNullOrEmpty(PlayerManager.Instance.GetPlayerByClientId(clientId).Username.Value.ToString()))
                                break;
                            UIChat.Instance.Server_SendSystemChatMessage($"{PlayerManager.Instance.GetPlayerByClientId(clientId).Username.Value} : {Constants.WORKSHOP_MOD_NAME} Mod is out of date. Please unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop and restart your game to update.");
                            _sentOutOfDateMessage[clientId] = utcNow;
                        }
                        break;

                    case ASK_SERVER_FOR_STARTUP_DATA: // SERVER-SIDE : Send the necessary data to client.
                        if (dataStr != "1")
                            break;

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER_TO_CLIENT, _serverConfig);

                        if (_sog.Count != 0) {
                            string batchSOG = "";
                            foreach (string key in new List<string>(_sog.Keys))
                                batchSOG += key + ';' + _sog[key].ToString() + ';';
                            batchSOG = batchSOG.Remove(batchSOG.Length - 1);
                            NetworkCommunication.SendData(BATCH_SOG, batchSOG, clientId, Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
                        }

                        if (_savePerc.Count != 0) {
                            string batchSavePerc = "";
                            foreach (string key in new List<string>(_savePerc.Keys))
                                batchSavePerc += key + ';' + _savePerc[key].ToString() + ';';
                            batchSavePerc = batchSavePerc.Remove(batchSavePerc.Length - 1);
                            NetworkCommunication.SendData(BATCH_SAVEPERC, batchSavePerc, clientId, Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
                        }
                        break;

                    case RESET_SOG:
                        if (dataStr != "1")
                            break;

                        foreach (string key in new List<string>(_sog.Keys)) {
                            if (_sogLabels.TryGetValue(key, out Label label)) {
                                _sog[key] = 0;
                                label.text = "0";

                                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(key);
                                if (currentPlayer != null && currentPlayer && Codebase.PlayerFunc.IsGoalie(currentPlayer))
                                    label.text = "0.000";
                            }
                            else {
                                _sog.Remove(key);
                                _savePerc.Remove(key);
                            }
                        }
                        break;

                    case RESET_SAVEPERC:
                        if (dataStr != "1")
                            break;

                        foreach (string key in new List<string>(_savePerc.Keys))
                            _savePerc[key] = (0, 0);
                        break;

                    case BATCH_SOG:
                        string[] splittedSOG = dataStr.Split(';');
                        string steamIdSOG = "";
                        for (int i = 0; i < splittedSOG.Length; i++) {
                            if (i % 2 == 0) // SteamId
                                steamIdSOG = splittedSOG[i];
                            else // SOG
                                ReceiveData_SOG(steamIdSOG, splittedSOG[i]);
                        }
                        break;

                    case BATCH_SAVEPERC:
                        string[] splittedSavePerc = dataStr.Split(';');
                        string steamIdSavePerc = "";
                        for (int i = 0; i < splittedSavePerc.Length; i++) {
                            if (i % 2 == 0) // SteamId
                                steamIdSavePerc = splittedSavePerc[i];
                            else // SOG
                                ReceiveData_SavePerc(steamIdSavePerc, splittedSavePerc[i]);
                        }
                        break;

                    default:
                        if (dataName.StartsWith(Codebase.Constants.SOG)) {
                            string playerSteamId = dataName.Replace(Codebase.Constants.SOG, "");
                            if (string.IsNullOrEmpty(playerSteamId))
                                return;

                            ReceiveData_SOG(playerSteamId, dataStr);
                        }

                        if (dataName.StartsWith(Codebase.Constants.SAVEPERC)) {
                            string playerSteamId = dataName.Replace(Codebase.Constants.SAVEPERC, "");
                            if (string.IsNullOrEmpty(playerSteamId))
                                return;

                            ReceiveData_SavePerc(playerSteamId, dataStr);
                        }
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveData.\n{ex}", _serverConfig);
            }
        }

        /// <summary>
        /// Method that manages received data from client-server communications.
        /// </summary>
        /// <param name="clientId">Ulong, Id of the client that sent the data. (0 if the server sent the data)</param>
        /// <param name="reader">FastBufferReader, stream containing the received data.</param>
        public static void ReceiveDataServerToServer(ulong clientId, FastBufferReader reader) {
            try {
                Logging.Log($"ReceiveDataServerToServer", _serverConfig, true); // TODO : Remove debug logs.
                (string dataName, string dataStr) = NetworkCommunication.GetData(clientId, reader, _serverConfig);
                Logging.Log($"{dataName}, {dataStr}", _serverConfig, true); // TODO : Remove debug logs.

                switch (dataName) {
                    case Codebase.Constants.SOG: // SERVER-SIDE : Another mod wants to add a SOG.
                        Logging.Log($"Player goal steamId : {dataStr}", _serverConfig, true); // TODO : Remove debug logs.
                        Player player = PlayerManager.Instance.GetPlayerBySteamId(dataStr);
                        SendSavePercDuringGoal(player.Team.Value, SendSOGDuringGoal(player));
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveDataServerToServer.\n{ex}", _serverConfig);
            }
        }

        private static void ReceiveData_SOG(string playerSteamId, string dataStr) {
            int sog = int.Parse(dataStr);

            if (_sog.TryGetValue(playerSteamId, out int _)) {
                _sog[playerSteamId] = sog;
                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                if (currentPlayer != null && currentPlayer && !Codebase.PlayerFunc.IsGoalie(currentPlayer))
                    _sogLabels[playerSteamId].text = sog.ToString();
            }
            else
                _sog.Add(playerSteamId, sog);
        }

        private static void ReceiveData_SavePerc(string playerSteamId, string dataStr) {
            string[] dataStrSplitted = SystemFunc.RemoveWhitespace(dataStr.Replace("(", "").Replace(")", "")).Split(',');
            int saves = int.Parse(dataStrSplitted[0]);
            int shots = int.Parse(dataStrSplitted[1]);

            if (_savePerc.TryGetValue(playerSteamId, out var _)) {
                _savePerc[playerSteamId] = (saves, shots);
                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                if (currentPlayer != null && currentPlayer && Codebase.PlayerFunc.IsGoalie(currentPlayer))
                    _sogLabels[playerSteamId].text = GetGoalieSavePerc(saves, shots);
            }
            else
                _savePerc.Add(playerSteamId, (saves, shots));
        }

        /// <summary>
        /// Method used to modify the scoreboard to add additional stats.
        /// </summary>
        /// <param name="enable">Bool, true if new stats scoreboard has to added to the scoreboard. False if they need to be removed.</param>
        private static void ScoreboardModifications(bool enable) {
            if (UIScoreboard.Instance == null)
                return;

            VisualElement scoreboardContainer = SystemFunc.GetPrivateField<VisualElement>(typeof(UIScoreboard), UIScoreboard.Instance, "container");

            if (!_hasUpdatedUIScoreboard.Contains("header") && enable) {
                foreach (VisualElement ve in scoreboardContainer.Children()) {
                    if (ve is TemplateContainer && ve.childCount == 1) {
                        VisualElement templateContainer = ve.Children().First();

                        Label sogHeader = new Label("SOG/s%") {
                            name = SOG_HEADER_LABEL_NAME
                        };
                        templateContainer.Add(sogHeader);
                        sogHeader.transform.position = new Vector3(sogHeader.transform.position.x - 260, sogHeader.transform.position.y + 15, sogHeader.transform.position.z);

                        foreach (VisualElement child in templateContainer.Children()) {
                            if (child.name == "GoalsLabel" || child.name == "AssistsLabel" || child.name == "PointsLabel")
                                child.transform.position = new Vector3(child.transform.position.x - 100, child.transform.position.y, child.transform.position.z);
                        }
                    }
                }

                _hasUpdatedUIScoreboard.Add("header");
            }
            else if (_hasUpdatedUIScoreboard.Contains("header") && !enable) {
                foreach (VisualElement ve in scoreboardContainer.Children()) {
                    if (ve is TemplateContainer && ve.childCount == 1) {
                        VisualElement templateContainer = ve.Children().First();

                        templateContainer.Remove(templateContainer.Children().First(x => x.name == SOG_HEADER_LABEL_NAME));

                        foreach (VisualElement child in templateContainer.Children()) {
                            if (child.name == "GoalsLabel" || child.name == "AssistsLabel" || child.name == "PointsLabel")
                                child.transform.position = new Vector3(child.transform.position.x + 100, child.transform.position.y, child.transform.position.z);
                        }
                    }
                }
            }

            foreach (var kvp in SystemFunc.GetPrivateField<Dictionary<Player, VisualElement>>(typeof(UIScoreboard), UIScoreboard.Instance, "playerVisualElementMap")) {
                string playerSteamId = kvp.Key.SteamId.Value.ToString();

                if (string.IsNullOrEmpty(playerSteamId))
                    continue;

                if (!_hasUpdatedUIScoreboard.Contains(playerSteamId) && enable) {
                    if (kvp.Value.childCount == 1) {
                        VisualElement playerContainer = kvp.Value.Children().First();

                        Label sogLabel = new Label("0") {
                            name = SOG_LABEL
                        };
                        sogLabel.style.flexGrow = 1;
                        sogLabel.style.unityTextAlign = TextAnchor.UpperRight;
                        playerContainer.Add(sogLabel);
                        sogLabel.transform.position = new Vector3(sogLabel.transform.position.x - 225, sogLabel.transform.position.y, sogLabel.transform.position.z);
                        _sogLabels.Add(playerSteamId, sogLabel);

                        foreach (VisualElement child in playerContainer.Children()) {
                            if (child.name == "GoalsLabel" || child.name == "AssistsLabel" || child.name == "PointsLabel")
                                child.transform.position = new Vector3(child.transform.position.x - 100, child.transform.position.y, child.transform.position.z);
                        }

                        _hasUpdatedUIScoreboard.Add(playerSteamId);

                        if (!_sog.TryGetValue(playerSteamId, out int _))
                            _sog.Add(playerSteamId, 0);

                        if (!_savePerc.TryGetValue(playerSteamId, out (int, int) _))
                            _savePerc.Add(playerSteamId, (0, 0));
                    }
                    else if (_hasUpdatedUIScoreboard.Contains(playerSteamId) && !enable) {
                        VisualElement playerContainer = kvp.Value.Children().First();

                        playerContainer.Remove(playerContainer.Children().First(x => x.name == SOG_LABEL));

                        foreach (VisualElement child in playerContainer.Children()) {
                            if (child.name == "GoalsLabel" || child.name == "AssistsLabel" || child.name == "PointsLabel")
                                child.transform.position = new Vector3(child.transform.position.x + 100, child.transform.position.y, child.transform.position.z);
                        }
                    }
                    else {
                        Logging.Log($"Not adding player {kvp.Key.Username.Value}, childCount {kvp.Value.childCount}.", _clientConfig, true);
                        foreach (var test in kvp.Value.Children())
                            Logging.Log($"{test.name}", _clientConfig, true);
                    }
                }
            }

            if (!enable) {
                _sog.Clear();
                _savePerc.Clear();
                _sogLabels.Clear();
                _hasUpdatedUIScoreboard.Clear();
            }
        }

        /// <summary>
        /// Function that sends and sets the SOG for a player when a goal is scored.
        /// </summary>
        /// <param name="player">Player, player that scored.</param>
        /// <returns>Bool, true if it was already sent and set.</returns>
        private static bool SendSOGDuringGoal(Player player) {
            Logging.Log($"{nameof(_lastShotWasCounted)} : {_lastShotWasCounted}", _serverConfig, true); // TODO : Remove debug logs.
            if (!_lastShotWasCounted[player.Team.Value]) {
                string playerSteamId = player.SteamId.Value.ToString();

                if (string.IsNullOrEmpty(playerSteamId))
                    return true;

                if (!_sog.TryGetValue(playerSteamId, out int _))
                    _sog.Add(playerSteamId, 0);

                _sog[playerSteamId] += 1;
                int sog = _sog[playerSteamId];
                NetworkCommunication.SendDataToAll(Codebase.Constants.SOG + playerSteamId, sog.ToString(), Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
                LogSOG(playerSteamId, sog);

                _lastShotWasCounted[player.Team.Value] = true;

                return false;
            }

            return true;
        }

        /// <summary>
        /// Function that sends and sets the s% for a goalie when a goal is scored.
        /// </summary>
        /// <param name="team">PlayerTeam, team that scored the goal.</param>
        /// <param name="saveWasCounted">Bool, true if a save was already counted for that shot.</param>
        private static void SendSavePercDuringGoal(PlayerTeam team, bool saveWasCounted) {
            // Get other team goalie.
            Player goalie = PlayerFunc.GetOtherTeamGoalie(team);
            if (goalie == null)
                return;

            string _goaliePlayerSteamId = goalie.SteamId.Value.ToString();
            if (!_savePerc.TryGetValue(_goaliePlayerSteamId, out var _savePercValue)) {
                _savePerc.Add(_goaliePlayerSteamId, (0, 0));
                _savePercValue = (0, 0);
            }

            (int saves, int sog) = _savePerc[_goaliePlayerSteamId] = saveWasCounted ? (--_savePercValue.Saves, _savePercValue.Shots) : (_savePercValue.Saves, ++_savePercValue.Shots);

            NetworkCommunication.SendDataToAll(Codebase.Constants.SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, _serverConfig);
            LogSavePerc(_goaliePlayerSteamId, saves, sog);
        }

        /// <summary>
        /// Method that logs the save percentage of a goalie.
        /// </summary>
        /// <param name="goaliePlayerSteamId">String, steam Id of the goalie.</param>
        /// <param name="saves">Int, number of saves.</param>
        /// <param name="sog">Int, number of shots on goal on the goalie.</param>
        private static void LogSavePerc(string goaliePlayerSteamId, int saves, int sog) {
            Logging.Log($"playerSteamId:{goaliePlayerSteamId},saveperc:{GetGoalieSavePerc(saves, sog)},saves:{saves},sog:{sog}", _serverConfig);
        }

        /// <summary>
        /// Method that logs the shots on goal of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="sog">Int, number of shots on goal.</param>
        private static void LogSOG(string playerSteamId, int sog) {
            Logging.Log($"playerSteamId:{playerSteamId},sog:{sog}", _serverConfig);
        }

        private static string GetGoalieSavePerc(int saves, int shots) {
            if (shots == 0)
                return "0.000";

            return (((double)saves) / ((double)shots)).ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static bool RulesetModEnabled() {
            return _rulesetModEnabled != null && (bool)_rulesetModEnabled;
        }
        #endregion

        #region Classes
        internal class SaveCheck {
            internal bool HasToCheck { get; set; } = false;
            internal string ShooterSteamId { get; set; } = "";
            internal int FramesChecked { get; set; } = 0;
        }
        #endregion
    }
}
