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
        private static readonly string MOD_VERSION = "0.2.2DEV";

        /// <summary>
        /// List of string, last released versions of the mod.
        /// </summary>
        private static readonly ReadOnlyCollection<string> OLD_MOD_VERSIONS = new ReadOnlyCollection<string>(new List<string> {
            "0.1.0",
            "0.1.1",
            "0.1.2",
            "0.2.0",
            "0.2.1",
        });

        /// <summary>
        /// ReadOnlyCollection of string, collection of datanames to not log.
        /// </summary>
        private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> {
            "eventName",
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

        /// <summary>
        /// Const string, data name for batching the blocked shots.
        /// </summary>
        private const string BATCH_BLOCK = Constants.MOD_NAME + "BATCHBLOCK";

        /// <summary>
        /// Const string, data name for resetting the blocked shots.
        /// </summary>
        private const string RESET_BLOCK = Constants.MOD_NAME + "RESETBLOCK";

        /// <summary>
        /// Const string, data name for resetting all stats.
        /// </summary>
        private const string RESET_ALL = Constants.MOD_NAME + "RESETALL";

        /// <summary>
        /// Const string, data name for receiving a star player.
        /// </summary>
        private const string STAR = Constants.MOD_NAME + "STAR";

        private const string SOG_HEADER_LABEL_NAME = "SOGHeaderLabel";

        private const string SOG_LABEL = "SOGLabel";

        /// <summary>
        /// Const string, tag to ask the server for the startup data.
        /// </summary>
        private const string ASK_SERVER_FOR_STARTUP_DATA = Constants.MOD_NAME + "ASKDATA";
        #endregion

        #region Fields and Properties
        // Server-side.
        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        internal static ServerConfig ServerConfig { get; set; } = new ServerConfig();

        private static bool? _rulesetModEnabled = null;

        private static bool _sendSavePercDuringGoalNextFrame = false;

        private static Player _sendSavePercDuringGoalNextFrame_Player = null;

        /// <summary>
        /// LockDictionary of ulong and string, dictionary of all players
        /// </summary>
        private static readonly LockDictionary<ulong, string> _players_ClientId_SteamId = new LockDictionary<ulong, string>();

        private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

        private static readonly LockDictionary<PlayerTeam, SaveCheck> _checkIfPuckWasSaved = new LockDictionary<PlayerTeam, SaveCheck> {
            { PlayerTeam.Blue, new SaveCheck() },
            { PlayerTeam.Red, new SaveCheck() },
        };

        private static readonly LockDictionary<PlayerTeam, BlockCheck> _checkIfPuckWasBlocked = new LockDictionary<PlayerTeam, BlockCheck> {
            { PlayerTeam.Blue, new BlockCheck() },
            { PlayerTeam.Red, new BlockCheck() },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _lastShotWasCounted = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, true },
            { PlayerTeam.Red, true },
        };

        private static readonly LockDictionary<PlayerTeam, (string SteamId, DateTime Time)> _lastPlayerOnPuckTipIncludedSteamId = new LockDictionary<PlayerTeam, (string, DateTime)> {
            { PlayerTeam.Blue, ("", DateTime.MinValue) },
            { PlayerTeam.Red, ("", DateTime.MinValue) },
        };

        private static PlayerTeam _lastTeamOnPuckTipIncluded = PlayerTeam.Blue;

        private static PuckRaycast _puckRaycast;

        /// <summary>
        /// Bool, true if there's a pause in play.
        /// </summary>
        private static bool _paused = false;

        /// <summary>
        /// Bool, true if the mod's logic has to be runned.
        /// </summary>
        private static bool _logic = true;

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

        private static readonly LockDictionary<string, int> _blocks = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, int> _passes = new LockDictionary<string, int>();

        private static readonly LockList<string> _blueGoals = new LockList<string>();

        private static readonly LockList<string> _redGoals = new LockList<string>();

        private static readonly LockDictionary<int, string> _stars = new LockDictionary<int, string> {
            { 1, "" },
            { 2, "" },
            { 3, "" },
        };

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
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Postfix().\n{ex}", ServerConfig);
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
                    if (!ServerFunc.IsDedicatedServer() || RulesetModEnabled() || !_logic)
                        return true;

                    if (goalPlayer != null) {
                        Player lastTouchPlayerTipIncluded = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team].SteamId).FirstOrDefault();
                        if (lastTouchPlayerTipIncluded != null && lastTouchPlayerTipIncluded.SteamId.Value.ToString() != goalPlayer.SteamId.Value.ToString()) {
                            secondAssistPlayer = assistPlayer;
                            assistPlayer = goalPlayer;
                            goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team].SteamId).FirstOrDefault();

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
                    UIChat.Instance.Server_SendSystemChatMessage($"OWN GOAL BY {PlayerManager.Instance.GetPlayerBySteamId(_lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(team)].SteamId).Username.Value}");
                    goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team].SteamId).FirstOrDefault();

                    bool saveWasCounted = false;
                    if (goalPlayer != null) {
                        lastPlayer = goalPlayer;
                        saveWasCounted = SendSOGDuringGoal(goalPlayer);
                    }

                    SendSavePercDuringGoal(team, saveWasCounted);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_GoalScored_Patch Prefix().\n{ex}", ServerConfig);
                }

                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(PlayerTeam team, Player lastPlayer, Player goalPlayer, Player assistPlayer, Player secondAssistPlayer, Puck puck) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return;

                    if (team == PlayerTeam.Blue)
                        _blueGoals.Add(goalPlayer.SteamId.Value.ToString());
                    else
                        _redGoals.Add(goalPlayer.SteamId.Value.ToString());
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_GoalScored_Patch Postfix().\n{ex}", ServerConfig);
                }
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

                    // Reset SOG.
                    foreach (string key in new List<string>(_sog.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _sog[key] = 0;
                        else
                            _sog.Remove(key);
                    }

                    // Reset blocked shots.
                    foreach (string key in new List<string>(_blocks.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _blocks[key] = 0;
                        else
                            _blocks.Remove(key);
                    }

                    // Reset passes.
                    foreach (string key in new List<string>(_passes.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _passes[key] = 0;
                        else
                            _passes.Remove(key);
                    }

                    // Reset goal trackers.
                    _blueGoals.Clear();
                    _redGoals.Clear();

                    NetworkCommunication.SendDataToAll(RESET_ALL, "1", Constants.FROM_SERVER_TO_CLIENT, ServerConfig);

                    _sentOutOfDateMessage.Clear();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_ResetGameState_Patch Postfix().\n{ex}", ServerConfig);
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
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !_logic)
                        return;

                    if (_sendSavePercDuringGoalNextFrame) {
                        _sendSavePercDuringGoalNextFrame = false;
                        SendSavePercDuringGoal(_sendSavePercDuringGoalNextFrame_Player.Team.Value, SendSOGDuringGoal(_sendSavePercDuringGoalNextFrame_Player));
                    }

                    // If game is not started, do not use the rest of the patch.
                    if (PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing || _paused)
                        return;

                    // Save logic.
                    foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys)) {
                        SaveCheck saveCheck = _checkIfPuckWasSaved[key];
                        if (!saveCheck.HasToCheck) {
                            _checkIfPuckWasSaved[key] = new SaveCheck();
                            continue;
                        }

                        //Logging.Log($"kvp.Check {saveCheck.FramesChecked} for team net {key} by {saveCheck.ShooterSteamId}.", ServerConfig, true);

                        if (!_puckRaycast.PuckIsGoingToNet[key] && !_lastShotWasCounted[saveCheck.ShooterTeam]) {
                            if (!_sog.TryGetValue(saveCheck.ShooterSteamId, out int _))
                                _sog.Add(saveCheck.ShooterSteamId, 0);

                            _sog[saveCheck.ShooterSteamId] += 1;
                            NetworkCommunication.SendDataToAll(Codebase.Constants.SOG + saveCheck.ShooterSteamId, _sog[saveCheck.ShooterSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            LogSOG(saveCheck.ShooterSteamId, _sog[saveCheck.ShooterSteamId]);

                            _lastShotWasCounted[saveCheck.ShooterTeam] = true;

                            // Get other team goalie.
                            Player goalie = PlayerFunc.GetOtherTeamGoalie(saveCheck.ShooterTeam);
                            if (goalie != null) {
                                string _goaliePlayerSteamId = goalie.SteamId.Value.ToString();
                                if (!_savePerc.TryGetValue(_goaliePlayerSteamId, out var savePercValue)) {
                                    _savePerc.Add(_goaliePlayerSteamId, (0, 0));
                                    savePercValue = (0, 0);
                                }

                                (int saves, int sog) = _savePerc[_goaliePlayerSteamId] = (++savePercValue.Saves, ++savePercValue.Shots);

                                NetworkCommunication.SendDataToAll(Codebase.Constants.SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                                LogSavePerc(_goaliePlayerSteamId, saves, sog);
                            }

                            _checkIfPuckWasSaved[key] = new SaveCheck();
                            _checkIfPuckWasBlocked[key] = new BlockCheck();
                        }
                        else {
                            if (++saveCheck.FramesChecked > ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate)
                                _checkIfPuckWasSaved[key] = new SaveCheck();
                        }
                    }

                    // Block logic.
                    foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasBlocked.Keys)) {
                        BlockCheck blockCheck = _checkIfPuckWasBlocked[key];
                        if (!blockCheck.HasToCheck) {
                            _checkIfPuckWasBlocked[key] = new BlockCheck();
                            continue;
                        }

                        //Logging.Log($"kvp.Check {blockCheck.FramesChecked} for team {key} blocked by {blockCheck.BlockerSteamId}.", ServerConfig, true);

                        if (!_puckRaycast.PuckIsGoingToNet[key] && !_lastShotWasCounted[blockCheck.ShooterTeam]) {
                            if (!_blocks.TryGetValue(blockCheck.BlockerSteamId, out int _))
                                _blocks.Add(blockCheck.BlockerSteamId, 0);

                            _blocks[blockCheck.BlockerSteamId] += 1;
                            NetworkCommunication.SendDataToAll(Codebase.Constants.BLOCK + blockCheck.BlockerSteamId, _blocks[blockCheck.BlockerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            LogBlock(blockCheck.BlockerSteamId, _blocks[blockCheck.BlockerSteamId]);

                            _lastShotWasCounted[blockCheck.ShooterTeam] = true;

                            // Get other team goalie.
                            Player goalie = PlayerFunc.GetOtherTeamGoalie(blockCheck.ShooterTeam);

                            _checkIfPuckWasSaved[key] = new SaveCheck();
                            _checkIfPuckWasBlocked[key] = new BlockCheck();
                        }
                        else {
                            if (++blockCheck.FramesChecked > ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate)
                                _checkIfPuckWasBlocked[key] = new BlockCheck();
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Postfix().\n{ex}", ServerConfig);
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
            public static void Postfix(UIScoreboard __instance, Player player) {
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
                        UIChat.Instance.AddChatMessage($"Server's {Constants.WORKSHOP_MOD_NAME} mod is out of date. Some functionalities might not work properly.");
                    }

                    ScoreboardModifications(true);

                    string playerSteamId = player.SteamId.Value.ToString();
                    if (!string.IsNullOrEmpty(playerSteamId) && _stars.Values.Contains(playerSteamId)) {
                        Dictionary<Player, VisualElement> playerVisualElementMap =
                            SystemFunc.GetPrivateField<Dictionary<Player, VisualElement>>(typeof(UIScoreboard), __instance, "playerVisualElementMap");

                        if (playerVisualElementMap.ContainsKey(player)) {
                            VisualElement visualElement = playerVisualElementMap[player];
                            Label label = visualElement.Query<Label>("UsernameLabel");
                            label.text = GetStarTag(playerSteamId) + label.text;
                        }
                    }
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
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing || !_logic)
                    return;

                try {
                    Player player = null;
                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        player = playerBody.Player;
                    }
                    else {
                        if (!stick.Player)
                            return;

                        _lastShotWasCounted[stick.Player.Team.Value] = false;
                        player = stick.Player;
                    }

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(player.Team.Value);

                    if (_puckRaycast.PuckIsGoingToNet[player.Team.Value]) {
                        if (PlayerFunc.IsGoalie(player) && Math.Abs(player.PlayerBody.Rigidbody.transform.position.z) > 13.5) {
                            PlayerTeam shooterTeam = TeamFunc.GetOtherTeam(player.Team.Value);
                            string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[shooterTeam].SteamId;
                            if (!string.IsNullOrEmpty(shooterSteamId)) {
                                _checkIfPuckWasSaved[player.Team.Value] = new SaveCheck {
                                    HasToCheck = true,
                                    ShooterSteamId = shooterSteamId,
                                    ShooterTeam = shooterTeam,
                                };
                            }
                        }
                        else {
                            PlayerTeam shooterTeam = TeamFunc.GetOtherTeam(player.Team.Value);
                            string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[shooterTeam].SteamId;
                            if (!string.IsNullOrEmpty(shooterSteamId)) {
                                _checkIfPuckWasBlocked[player.Team.Value] = new BlockCheck {
                                    HasToCheck = true,
                                    BlockerSteamId = player.SteamId.Value.ToString(),
                                    ShooterTeam = shooterTeam,
                                };
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionEnter_Patch Postfix().\n{ex}", ServerConfig);
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
                    if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing || !_logic)
                        return;

                    Player player;

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        player = playerBody.Player;
                    }
                    else {
                        if (!stick.Player)
                            return;

                        player = stick.Player;
                    }

                    string playerSteamId = player.SteamId.Value.ToString();

                    string lastPlayerOnPuck = _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value].SteamId;

                    if (playerSteamId != lastPlayerOnPuck) {
                        if (!string.IsNullOrEmpty(lastPlayerOnPuck) && _lastTeamOnPuckTipIncluded == player.Team.Value) {
                            if ((DateTime.UtcNow - _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value].Time).TotalMilliseconds < 5000) {
                                if (!_passes.TryGetValue(lastPlayerOnPuck, out int _))
                                    _passes.Add(lastPlayerOnPuck, 0);

                                _passes[lastPlayerOnPuck] += 1;
                                NetworkCommunication.SendDataToAll(Codebase.Constants.PASS + lastPlayerOnPuck, _passes[lastPlayerOnPuck].ToString(), Constants.FROM_SERVER_TO_CLIENT,
                                    ServerConfig);
                                LogPass(lastPlayerOnPuck, _passes[lastPlayerOnPuck]);
                            }
                        }

                        _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value] = (playerSteamId, DateTime.UtcNow);
                    }

                    _lastTeamOnPuckTipIncluded = player.Team.Value;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionStay_Patch Postfix().\n{ex}", ServerConfig);
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
                    if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing || !_logic)
                        return;

                    if (!__instance.IsTouchingStick)
                        return;

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = (stick.Player.SteamId.Value.ToString(), DateTime.UtcNow);
                    _lastTeamOnPuckTipIncluded = stick.Player.Team.Value;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}", ServerConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the Server_SetPhase event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_SetPhase))]
        public class GameManager_Server_SetPhase_Patch {
            [HarmonyPrefix]
            public static bool Prefix(GameManager __instance, GamePhase phase, ref int time) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !_logic)
                        return true;

                    if (phase == GamePhase.FaceOff || phase == GamePhase.Warmup || phase == GamePhase.GameOver) {
                        // Reset puck was saved states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
                            _checkIfPuckWasSaved[key] = new SaveCheck();

                        // Reset player on puck.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
                            _lastPlayerOnPuckTipIncludedSteamId[key] = ("", DateTime.MinValue);

                        // Reset shot counted states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastShotWasCounted.Keys))
                            _lastShotWasCounted[key] = true;

                        if (phase == GamePhase.GameOver) {
                            string gwgSteamId = "";
                            PlayerTeam winningTeam = PlayerTeam.None;
                            try {
                                if (__instance.GameState.Value.BlueScore > __instance.GameState.Value.RedScore) {
                                    winningTeam = PlayerTeam.Blue;
                                    gwgSteamId = _blueGoals[__instance.GameState.Value.RedScore];
                                }
                                else {
                                    winningTeam = PlayerTeam.Red;
                                    gwgSteamId = _redGoals[__instance.GameState.Value.BlueScore];
                                }

                                LogGWG(gwgSteamId);
                            }
                            catch (IndexOutOfRangeException) { } // Shootout goal or something, so no GWG.

                            Dictionary<string, double> starPoints = new Dictionary<string, double>();
                            foreach (Player player in PlayerManager.Instance.GetPlayers()) {
                                if (player == null || !player)
                                    continue;

                                string steamId = player.SteamId.Value.ToString();
                                starPoints.Add(steamId, 0);

                                double gwgModifier = gwgSteamId == player.SteamId.Value.ToString() ? 0.5d : 0;
                                double teamModifier = winningTeam == player.Team.Value ? 1.1d : 1d;

                                if (PlayerFunc.IsGoalie(player)) {
                                    if (_savePerc.TryGetValue(steamId, out var saveValues))
                                        starPoints[steamId] += (((double)saveValues.Saves) / ((double)saveValues.Shots) - 0.700d) * ((double)saveValues.Saves) * 18d;

                                    if (_sog.TryGetValue(steamId, out int shots))
                                        starPoints[steamId] += ((double)shots) * 1d;

                                    if (_passes.TryGetValue(steamId, out int passes))
                                        starPoints[steamId] += ((double)passes) * 2d;

                                    const double GOALIE_GOAL_MODIFIER = 175d;
                                    const double GOALIE_ASSIST_MODIFIER = 30d;

                                    starPoints[steamId] += GOALIE_GOAL_MODIFIER * gwgModifier;
                                    starPoints[steamId] += ((double)player.Goals.Value) * GOALIE_GOAL_MODIFIER * gwgModifier;
                                    starPoints[steamId] += ((double)player.Assists.Value) * GOALIE_ASSIST_MODIFIER;
                                }
                                else {
                                    if (_sog.TryGetValue(steamId, out int shots)) {
                                        starPoints[steamId] += ((double)shots) * 5d;
                                        starPoints[steamId] += (((double)(player.Goals.Value + 1)) / ((double)shots) - 0.4d) * ((double)shots) * 4d;
                                    }

                                    if (_passes.TryGetValue(steamId, out int passes))
                                        starPoints[steamId] += ((double)passes) * 2d;

                                    if (_blocks.TryGetValue(steamId, out int blocks))
                                        starPoints[steamId] += ((double)blocks) * 6d;

                                    const double SKATER_GOAL_MODIFIER = 70d;
                                    const double SKATER_ASSIST_MODIFIER = 30d;

                                    starPoints[steamId] += SKATER_GOAL_MODIFIER * gwgModifier;
                                    starPoints[steamId] += ((double)player.Goals.Value) * SKATER_GOAL_MODIFIER * gwgModifier;
                                    starPoints[steamId] += ((double)player.Assists.Value) * SKATER_ASSIST_MODIFIER;
                                }

                                starPoints[steamId] *= teamModifier;
                            }

                            starPoints = starPoints.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

                            if (starPoints.Count >= 1)
                                _stars[1] = starPoints.ElementAt(0).Key;
                            else
                                _stars[1] = "";

                            if (starPoints.Count >= 2)
                                _stars[2] = starPoints.ElementAt(1).Key;
                            else
                                _stars[2] = "";

                            if (starPoints.Count >= 3)
                                _stars[3] = starPoints.ElementAt(2).Key;
                            else
                                _stars[3] = "";

                            UIChat.Instance.Server_SendSystemChatMessage("STARS OF THE MATCH");
                            foreach (KeyValuePair<int, string> star in _stars.OrderByDescending(x => x.Key)) {
                                if (!string.IsNullOrEmpty(star.Value)) {
                                    Player player = PlayerManager.Instance.GetPlayerBySteamId(star.Value);
                                    if (player != null && player)
                                        UIChat.Instance.Server_SendSystemChatMessage($"The {(star.Key == 1 ? "first" : (star.Key == 2 ? "second" : "third"))} star is... #{player.Number.Value} {player.Username.Value} !");

                                    NetworkCommunication.SendDataToAll(STAR, $"{star.Value};{star.Key}", Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                                    LogStar(star.Value, star.Key);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Prefix().\n{ex}", ServerConfig);
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches WrapPlayerUsername event from UIChat.
        /// </summary>
        [HarmonyPatch(typeof(UIChat), nameof(UIChat.WrapPlayerUsername))]
        public static class UIChat_WrapPlayerUsername_Patch {
            public static void Postfix(Player player, ref string __result) {
                if (player == null || !player)
                    return;

                string steamId = player.SteamId.Value.ToString();
                if (string.IsNullOrEmpty(steamId))
                    return;

                 __result = GetStarTag(steamId) + __result;
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

                Logging.Log($"Enabling...", ServerConfig, true);

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
                    _clientConfig = ClientConfig.ReadConfig();
                }

                Logging.Log("Subscribing to events.", ServerConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    EventManager.Instance.AddEventListener(Codebase.Constants.STATS_MOD_NAME, Event_OnStatsTrigger);
                }
                else {
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                }

                _harmonyPatched = true;
                _logic = true;
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
                    EventManager.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    EventManager.Instance.RemoveEventListener(Codebase.Constants.STATS_MOD_NAME, Event_OnStatsTrigger);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER);
                }
                else {
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    Event_Client_OnClientStopped(new Dictionary<string, object>());
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT);
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _rulesetModEnabled = null;
                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                ScoreboardModifications(false);

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", ServerConfig, true);

                _harmonyPatched = false;
                _logic = true;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}", ServerConfig);
                return false;
            }
        }
        #endregion

        #region Events
        public static void Event_OnStatsTrigger(Dictionary<string, object> message) {
            try {
                foreach (KeyValuePair<string, object> kvp in message) {
                    string value = (string)kvp.Value;
                    if (!NetworkCommunication.GetDataNamesToIgnore().Contains(kvp.Key))
                        Logging.Log($"Received data {kvp.Key}. Content : {value}", ServerConfig);

                    switch (kvp.Key) {
                        case Codebase.Constants.SOG:
                            _sendSavePercDuringGoalNextFrame_Player = PlayerManager.Instance.GetPlayerBySteamId(value);
                            if (_sendSavePercDuringGoalNextFrame_Player == null || !_sendSavePercDuringGoalNextFrame_Player)
                                Logging.LogError($"{nameof(_sendSavePercDuringGoalNextFrame_Player)} is null.", ServerConfig);
                            else
                                _sendSavePercDuringGoalNextFrame = true;

                            break;

                        case Codebase.Constants.PAUSE:
                            _paused = bool.Parse(value);
                            break;

                        case Codebase.Constants.LOGIC:
                            _logic = bool.Parse(value);
                            break;
                    }
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnStatsTrigger.\n{ex}", ServerConfig);
            }
        }

        /// <summary>
        /// Method called when a client has connected (joined a server) on the server-side.
        /// Used to set server-sided stuff after the game has loaded.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnClientConnected(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_OnClientConnected", ServerConfig);

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
                Logging.LogError($"Error in Event_OnClientConnected.\n{ex}", ServerConfig);
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

            Logging.Log("Event_OnClientDisconnected", ServerConfig);

            try {
                ulong clientId = (ulong)message["clientId"];
                string clientSteamId;
                try {
                    clientSteamId = _players_ClientId_SteamId[clientId];
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(_players_ClientId_SteamId)}.", ServerConfig);
                    return;
                }

                _sentOutOfDateMessage.Remove(clientId);

                _sog.Remove(clientSteamId);
                _savePerc.Remove(clientSteamId);

                _players_ClientId_SteamId.Remove(clientId);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientDisconnected.\n{ex}", ServerConfig);
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

            //Logging.Log("Event_Client_OnClientStopped", _clientConfig);

            try {
                ServerConfig = new ServerConfig();

                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                foreach (int key in new List<int>(_stars.Keys))
                    _stars[key] = "";
                _passes.Clear();
                _blocks.Clear();
                _blueGoals.Clear();
                _redGoals.Clear();

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
                    Logging.Log($"Added clientId {kvp.Key} linked to Steam Id {kvp.Value}.", ServerConfig);
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

                NetworkCommunication.SendDataToAll(Codebase.Constants.SOG + playerSteamId, _sog[playerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            }
            else {
                if (!_savePerc.TryGetValue(playerSteamId, out var _))
                    _savePerc.Add(playerSteamId, (0, 0));

                NetworkCommunication.SendDataToAll(Codebase.Constants.SAVEPERC + playerSteamId, _savePerc[playerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            }
        }
        #endregion

        #region Methods/Functions
        private static void Server_RegisterNamedMessageHandler() {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null && !_hasRegisteredWithNamedMessageHandler) {
                Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT_TO_SERVER}.", ServerConfig);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER, ReceiveData);

                _hasRegisteredWithNamedMessageHandler = true;
            }
        }

        private static void CheckForRulesetMod() {
            if (ModManagerV2.Instance == null || ModManagerV2.Instance.EnabledModIds == null || _rulesetModEnabled != null)
                return;

            _rulesetModEnabled = ModManagerV2.Instance.EnabledModIds.Contains(3501446576) || ModManagerV2.Instance.EnabledModIds.Contains(3500559233);
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
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, ServerConfig);

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

                            Logging.Log($"Warning client {clientId} mod out of date.", ServerConfig);
                            UIChat.Instance.Server_SendSystemChatMessage($"{PlayerManager.Instance.GetPlayerByClientId(clientId).Username.Value} : {Constants.WORKSHOP_MOD_NAME} Mod is out of date. Please unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop and restart your game to update.");
                            _sentOutOfDateMessage[clientId] = utcNow;
                        }
                        break;

                    case ASK_SERVER_FOR_STARTUP_DATA: // SERVER-SIDE : Send the necessary data to client.
                        if (dataStr != "1")
                            break;

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);

                        if (_sog.Count != 0) {
                            string batchSOG = "";
                            foreach (string key in new List<string>(_sog.Keys))
                                batchSOG += key + ';' + _sog[key].ToString() + ';';
                            batchSOG = batchSOG.Remove(batchSOG.Length - 1);
                            NetworkCommunication.SendData(BATCH_SOG, batchSOG, clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        }

                        if (_savePerc.Count != 0) {
                            string batchSavePerc = "";
                            foreach (string key in new List<string>(_savePerc.Keys))
                                batchSavePerc += key + ';' + _savePerc[key].ToString() + ';';
                            batchSavePerc = batchSavePerc.Remove(batchSavePerc.Length - 1);
                            NetworkCommunication.SendData(BATCH_SAVEPERC, batchSavePerc, clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        }

                        foreach (int key in new List<int>(_stars.Keys))
                            NetworkCommunication.SendData(STAR, $"{_stars[key]};{key}", clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
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
                            else // SavePerc
                                ReceiveData_SavePerc(steamIdSavePerc, splittedSavePerc[i]);
                        }
                        break;

                    case STAR:
                        string[] splittedStar = dataStr.Split(';');
                        string steamIdStar = "";
                        for (int i = 0; i < splittedStar.Length; i++) {
                            if (i % 2 == 0) // SteamId
                                steamIdStar = splittedStar[i];
                            else // Star index
                                ReceiveData_Star(steamIdStar, splittedStar[i]);
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
                Logging.LogError($"Error in ReceiveData.\n{ex}", ServerConfig);
            }
        }

        private static void ReceiveData_SOG(string playerSteamId, string dataStr) {
            int sog = int.Parse(dataStr);

            if (_sog.TryGetValue(playerSteamId, out int _)) {
                _sog[playerSteamId] = sog;
                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                if (currentPlayer != null && currentPlayer && !PlayerFunc.IsGoalie(currentPlayer))
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
                if (currentPlayer != null && currentPlayer && PlayerFunc.IsGoalie(currentPlayer))
                    _sogLabels[playerSteamId].text = GetGoalieSavePerc(saves, shots);
            }
            else
                _savePerc.Add(playerSteamId, (saves, shots));
        }

        private static void ReceiveData_Star(string playerSteamId, string dataStr) {
            int starIndex = int.Parse(dataStr);

            if (_stars.TryGetValue(starIndex, out string _))
                _stars[starIndex] = playerSteamId;
            else
                _stars.Add(starIndex, playerSteamId);
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
                            name = SOG_HEADER_LABEL_NAME,
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
            if (!_lastShotWasCounted[player.Team.Value]) {
                string playerSteamId = player.SteamId.Value.ToString();

                if (string.IsNullOrEmpty(playerSteamId))
                    return true;

                if (!_sog.TryGetValue(playerSteamId, out int _))
                    _sog.Add(playerSteamId, 0);

                _sog[playerSteamId] += 1;
                int sog = _sog[playerSteamId];
                NetworkCommunication.SendDataToAll(Codebase.Constants.SOG + playerSteamId, sog.ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
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

            NetworkCommunication.SendDataToAll(Codebase.Constants.SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            LogSavePerc(_goaliePlayerSteamId, saves, sog);
        }

        /// <summary>
        /// Method that logs the save percentage of a goalie.
        /// </summary>
        /// <param name="goaliePlayerSteamId">String, steam Id of the goalie.</param>
        /// <param name="saves">Int, number of saves.</param>
        /// <param name="sog">Int, number of shots on goal on the goalie.</param>
        private static void LogSavePerc(string goaliePlayerSteamId, int saves, int sog) {
            Logging.Log($"playerSteamId:{goaliePlayerSteamId},saveperc:{GetGoalieSavePerc(saves, sog)},saves:{saves},sog:{sog}", ServerConfig);
        }

        /// <summary>
        /// Method that logs the shots on goal of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="sog">Int, number of shots on goal.</param>
        private static void LogSOG(string playerSteamId, int sog) {
            Logging.Log($"playerSteamId:{playerSteamId},sog:{sog}", ServerConfig);
        }

        /// <summary>
        /// Method that logs the blocked shots of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="block">Int, number of blocked shots.</param>
        private static void LogBlock(string playerSteamId, int block) {
            Logging.Log($"playerSteamId:{playerSteamId},block:{block}", ServerConfig);
        }

        /// <summary>
        /// Method that logs the passes of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="pass">Int, number of passes.</param>
        private static void LogPass(string playerSteamId, int pass) {
            Logging.Log($"playerSteamId:{playerSteamId},pass:{pass}", ServerConfig);
        }

        /// <summary>
        /// Method that logs the game winning goal of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        private static void LogGWG(string playerSteamId) {
            Logging.Log($"playerSteamId:{playerSteamId},gwg:1", ServerConfig);
        }

        /// <summary>
        /// Method that logs the match star of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="starIndex">Int, star number of the player (1 is first star, etc.).</param>
        private static void LogStar(string playerSteamId, int starIndex) {
            Logging.Log($"playerSteamId:{playerSteamId},star:{starIndex}", ServerConfig);
        }

        private static string GetGoalieSavePerc(int saves, int shots) {
            if (shots == 0)
                return "0.000";

            return (((double)saves) / ((double)shots)).ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static bool RulesetModEnabled() {
            return _rulesetModEnabled != null && (bool)_rulesetModEnabled;
        }

        private static string GetStarTag(string playerSteamId) {
            string star = "";
            if (_stars[1] == playerSteamId)
                star = "<color=#FFD700FF><b>★</b></color> ";
            else if (_stars[2] == playerSteamId)
                star = "<color=#C0C0C0FF><b>★</b></color> ";
            else if (_stars[3] == playerSteamId)
                star = "<color=#CD7F32FF><b>★</b></color> ";

            return star;
        }
        #endregion

        #region Classes
        internal abstract class Check {
            internal bool HasToCheck { get; set; } = false;
            internal int FramesChecked { get; set; } = 0;
        }
        internal class SaveCheck : Check {
            internal string ShooterSteamId { get; set; } = "";
            internal PlayerTeam ShooterTeam { get; set; } = PlayerTeam.Blue;
        }

        internal class BlockCheck : Check {
            internal string BlockerSteamId { get; set; } = "";
            internal PlayerTeam ShooterTeam { get; set; } = PlayerTeam.Blue;
        }
        #endregion
    }
}
