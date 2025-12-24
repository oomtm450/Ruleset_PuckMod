using Codebase;
using HarmonyLib;
using Newtonsoft.Json;
using oomtm450PuckMod_Stats.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace oomtm450PuckMod_Stats {
    public class Stats : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private static readonly string MOD_VERSION = "0.7.0";

        /// <summary>
        /// List of string, last released versions of the mod.
        /// </summary>
        private static readonly ReadOnlyCollection<string> OLD_MOD_VERSIONS = new ReadOnlyCollection<string>(new List<string> {
            "0.1.0",
            "0.1.1",
            "0.1.2",
            "0.2.0",
            "0.2.1",
            "0.2.2",
            "0.3.0",
            "0.4.0",
            "0.4.1",
            "0.5.0",
            "0.6.0",
        });

        /// <summary>
        /// ReadOnlyCollection of string, collection of datanames to not log.
        /// </summary>
        private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> {
            "eventName",
            Codebase.Constants.NEXT_FACEOFF,
            Codebase.Constants.PLUSMINUS,
            //Codebase.Constants.TAKEAWAY, // TODO : Remove debug logs.
            //Codebase.Constants.TURNOVER, // TODO : Remove debug logs.
            Codebase.Constants.BLOCK,
            Codebase.Constants.HIT,
            Codebase.Constants.PASS,
        });

        /// <summary>
        /// Const string, data name for batching the SOG.
        /// </summary>
        private const string BATCH_SOG = Constants.MOD_NAME + "BATCHSOG";

        /*/// <summary>
        /// Const string, data name for resetting the SOG.
        /// </summary>
        private const string RESET_SOG = Constants.MOD_NAME + "RESETSOG";*/

        /// <summary>
        /// Const string, data name for batching the save percentage.
        /// </summary>
        private const string BATCH_SAVEPERC = Constants.MOD_NAME + "BATCHSAVEPERC";

        /*/// <summary>
        /// Const string, data name for resetting the save percentage.
        /// </summary>
        private const string RESET_SAVEPERC = Constants.MOD_NAME + "RESETSAVEPERC";*/

        /// <summary>
        /// Const string, data name for batching the blocked shots.
        /// </summary>
        private const string BATCH_BLOCK = Constants.MOD_NAME + "BATCHBLOCK";

        /*/// <summary>
        /// Const string, data name for resetting the blocked shots.
        /// </summary>
        private const string RESET_BLOCK = Constants.MOD_NAME + "RESETBLOCK";*/

        /// <summary>
        /// Const string, data name for batching the hits.
        /// </summary>
        private const string BATCH_HIT = Constants.MOD_NAME + "BATCHHIT";

        /*/// <summary>
        /// Const string, data name for resetting the hits.
        /// </summary>
        private const string RESET_HIT = Constants.MOD_NAME + "RESETHIT";*/

        /// <summary>
        /// Const string, data name for batching the takeaways.
        /// </summary>
        private const string BATCH_TAKEAWAY = Constants.MOD_NAME + "BATCHTAKEAWAY";

        /*/// <summary>
        /// Const string, data name for resetting the takeaways.
        /// </summary>
        private const string RESET_TAKEAWAY = Constants.MOD_NAME + "RESETTAKEAWAY";*/

        /// <summary>
        /// Const string, data name for batching the turnovers.
        /// </summary>
        private const string BATCH_TURNOVER = Constants.MOD_NAME + "BATCHTURNOVER";

        /*/// <summary>
        /// Const string, data name for resetting the turnovers.
        /// </summary>
        private const string RESET_TURNOVER = Constants.MOD_NAME + "RESETTURNOVER";*/

        /// <summary>
        /// Const string, data name for batching the passes.
        /// </summary>
        private const string BATCH_PASS = Constants.MOD_NAME + "BATCHPASS";

        /*/// <summary>
        /// Const string, data name for resetting the passes.
        /// </summary>
        private const string RESET_PASS = Constants.MOD_NAME + "RESETPASS";*/

        /// <summary>
        /// Const string, data name for batching the +/-.
        /// </summary>
        private const string BATCH_PLUSMINUS = Constants.MOD_NAME + "BATCHPLUSMINUS";

        /*/// <summary>
        /// Const string, data name for resetting the +/-.
        /// </summary>
        private const string RESET_PLUSMINUS = Constants.MOD_NAME + "RESETPLUSMINUS";*/

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

        private static Vector3 _puckLastCoordinate = Vector3.zero;

        private static float _puckZCoordinateDifference = 0;

        /// <summary>
        /// LockDictionary of ulong and string, dictionary of all players clientId, steamId and username.
        /// </summary>
        private static readonly LockDictionary<ulong, (string SteamId, string Username)> _playersInfo = new LockDictionary<ulong, (string, string)>();

        /// <summary>
        /// LockDictionary of ulong and DateTime, last time a mod out of date message was sent to a client (ulong clientId).
        /// </summary>
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

        private static readonly LockDictionary<PlayerTeam, bool> _lastBlockWasCounted = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, true },
            { PlayerTeam.Red, true },
        };

        private static readonly LockDictionary<PlayerTeam, (string SteamId, DateTime Time)> _lastPlayerOnPuckTipIncludedSteamId = new LockDictionary<PlayerTeam, (string, DateTime)> {
            { PlayerTeam.Blue, ("", DateTime.MinValue) },
            { PlayerTeam.Red, ("", DateTime.MinValue) },
        };

        private static readonly LockDictionary<PlayerTeam, (string SteamId, DateTime Time)> _lastPlayerOnPuckSteamId = new LockDictionary<PlayerTeam, (string, DateTime)> {
            { PlayerTeam.Blue, ("", DateTime.MinValue) },
            { PlayerTeam.Red, ("", DateTime.MinValue) },
        };

        /// <summary>
        /// LockDictionary of string and Stopwatch, dictionary of all players current puck touch time.
        /// </summary>
        private static readonly LockDictionary<string, Stopwatch> _playersCurrentPuckTouch = new LockDictionary<string, Stopwatch>();

        /// <summary>
        /// LockDictionary of string and Stopwatch, dictionary of all players last puck OnCollisionStay or OnCollisionExit time.
        /// </summary>
        private static readonly LockDictionary<string, Stopwatch> _lastTimeOnCollisionStayOrExitWasCalled = new LockDictionary<string, Stopwatch>();

        private static readonly LockDictionary<string, bool> _playerIsDown = new LockDictionary<string, bool>();

        private static Possession _lastPossession = new Possession();

        private static PlayerTeam _lastTeamOnPuckTipIncluded = PlayerTeam.Blue;

        //private static PlayerTeam _lastTeamOnPuck = PlayerTeam.Blue;

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

        private static readonly LockDictionary<string, int> _stickSaves = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, int> _blocks = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, int> _hits = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, int> _takeaways = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, int> _turnovers = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, int> _passes = new LockDictionary<string, int>();

        private static readonly LockList<string> _blueGoals = new LockList<string>();

        private static readonly LockList<string> _redGoals = new LockList<string>();

        private static readonly LockList<string> _blueAssists = new LockList<string>();

        private static readonly LockList<string> _redAssists = new LockList<string>();

        private static readonly LockDictionary<int, string> _stars = new LockDictionary<int, string> {
            { 1, "" },
            { 2, "" },
            { 3, "" },
        };

        private static readonly LockDictionary<string, int> _plusMinus = new LockDictionary<string, int>();

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

                    foreach (Player player in PlayerManager.Instance.GetPlayers()) {
                        if (!PlayerFunc.IsPlayerPlaying(player) || PlayerFunc.IsGoalie(player))
                            continue;

                        string playerSteamId = player.SteamId.Value.ToString();
                        if (!_plusMinus.TryGetValue(playerSteamId, out int _))
                            _plusMinus.Add(playerSteamId, 0);

                        if (player.Team.Value == team)
                            _plusMinus[playerSteamId] += 1;
                        else
                            _plusMinus[playerSteamId] -= 1;

                        NetworkCommunication.SendDataToAll(Codebase.Constants.PLUSMINUS + playerSteamId, _plusMinus[playerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        LogPlusMinus(playerSteamId, _plusMinus[playerSteamId]);
                    }

                    if (team == PlayerTeam.Blue) {
                        _blueGoals.Add(goalPlayer.SteamId.Value.ToString());
                        if (assistPlayer != null)
                            _blueAssists.Add(assistPlayer.SteamId.Value.ToString());
                        if (secondAssistPlayer != null)
                            _blueAssists.Add(secondAssistPlayer.SteamId.Value.ToString());
                    }
                    else {
                        _redGoals.Add(goalPlayer.SteamId.Value.ToString());
                        if (assistPlayer != null)
                            _redAssists.Add(assistPlayer.SteamId.Value.ToString());
                        if (secondAssistPlayer != null)
                            _redAssists.Add(secondAssistPlayer.SteamId.Value.ToString());
                    }
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

                    // Reset stick saves.
                    foreach (string key in new List<string>(_stickSaves.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _stickSaves[key] = 0;
                        else
                            _stickSaves.Remove(key);
                    }

                    // Reset blocked shots.
                    foreach (string key in new List<string>(_blocks.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _blocks[key] = 0;
                        else
                            _blocks.Remove(key);
                    }

                    // Reset hits.
                    foreach (string key in new List<string>(_hits.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _hits[key] = 0;
                        else
                            _hits.Remove(key);
                    }

                    // Reset takeaways.
                    foreach (string key in new List<string>(_takeaways.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _takeaways[key] = 0;
                        else
                            _takeaways.Remove(key);
                    }

                    // Reset turnovers.
                    foreach (string key in new List<string>(_turnovers.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _turnovers[key] = 0;
                        else
                            _turnovers.Remove(key);
                    }

                    // Reset passes.
                    foreach (string key in new List<string>(_passes.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _passes[key] = 0;
                        else
                            _passes.Remove(key);
                    }

                    // Reset +/-.
                    foreach (string key in new List<string>(_plusMinus.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _plusMinus[key] = 0;
                        else
                            _plusMinus.Remove(key);
                    }

                    // Reset goal and assists trackers.
                    _blueGoals.Clear();
                    _blueAssists.Clear();
                    _redGoals.Clear();
                    _redAssists.Clear();

                    // Reset last possession.
                    _lastPossession = new Possession();

                    NetworkCommunication.SendDataToAll(RESET_ALL, "1", Constants.FROM_SERVER_TO_CLIENT, ServerConfig);

                    _sentOutOfDateMessage.Clear();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(GameManager_Server_ResetGameState_Patch)} Postfix().\n{ex}", ServerConfig);
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

                    bool sendSavePercDuringGoalNextFrame = _sendSavePercDuringGoalNextFrame;
                    if (sendSavePercDuringGoalNextFrame) {
                        _sendSavePercDuringGoalNextFrame = false;
                        SendSavePercDuringGoal(_sendSavePercDuringGoalNextFrame_Player.Team.Value, SendSOGDuringGoal(_sendSavePercDuringGoalNextFrame_Player));
                    }

                    // If game is not started, do not use the rest of the patch.
                    if (PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing || _paused)
                        return;

                    // Save logic.
                    if (!sendSavePercDuringGoalNextFrame) {
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
                                    if (saveCheck.HitStick) {
                                        if (!_stickSaves.TryGetValue(_goaliePlayerSteamId, out int stickSaveValue)) {
                                            _stickSaves.Add(_goaliePlayerSteamId, 0);
                                            stickSaveValue = 0;
                                        }

                                        int stickSaves = _stickSaves[_goaliePlayerSteamId] = ++stickSaveValue;
                                        LogStickSave(_goaliePlayerSteamId, stickSaves);
                                    }
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

                            if (!_puckRaycast.PuckIsGoingToNet[key] && !_lastBlockWasCounted[blockCheck.ShooterTeam]) {
                                ProcessBlock(blockCheck.BlockerSteamId);

                                _lastBlockWasCounted[blockCheck.ShooterTeam] = true;

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

                    Puck puck = PuckManager.Instance.GetPuck();
                    if (puck) {
                        _puckZCoordinateDifference = (puck.Rigidbody.transform.position.z - _puckLastCoordinate.z) / 240 * ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate;
                        _puckLastCoordinate = new Vector3(puck.Rigidbody.transform.position.x, puck.Rigidbody.transform.position.y, puck.Rigidbody.transform.position.z);
                    }

                    // Takeaways/turnovers logic.
                    string currentPossessionSteamId = PlayerFunc.GetPlayerSteamIdInPossession(ServerConfig.MinPossessionMilliseconds, _playersCurrentPuckTouch);
                    if (!string.IsNullOrEmpty(currentPossessionSteamId)) {
                        Player possessionPlayer = PlayerManager.Instance.GetPlayerBySteamId(currentPossessionSteamId);

                        if (PlayerFunc.IsPlayerPlaying(possessionPlayer)) {
                            if (_lastPossession.Team != PlayerTeam.None && _lastPossession.Team != possessionPlayer.Team.Value &&
                                (DateTime.UtcNow - _lastPossession.Date).TotalMilliseconds < ServerConfig.TurnoverThresholdMilliseconds) {
                                ProcessTakeaways(currentPossessionSteamId);
                                ProcessTurnovers(_lastPossession.SteamId);
                            }

                            _lastPossession = new Possession {
                                SteamId = currentPossessionSteamId,
                                Team = possessionPlayer.Team.Value,
                                Date = DateTime.UtcNow,
                            };
                        }
                        else
                            _lastPossession = new Possession();
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
                            NetworkCommunication.SendData(Constants.ASK_SERVER_FOR_STARTUP_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, _clientConfig);
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

                        player = stick.Player;
                    }

                    string currentPlayerSteamId = player.SteamId.Value.ToString();


                    if (!PlayerFunc.IsGoalie(player)) {
                        // Start tipped timer.
                        if (!_playersCurrentPuckTouch.TryGetValue(currentPlayerSteamId, out Stopwatch watch)) {
                            watch = new Stopwatch();
                            watch.Start();
                            _playersCurrentPuckTouch.Add(currentPlayerSteamId, watch);
                        }

                        string lastPlayerOnPuckTipIncludedSteamId = _lastPlayerOnPuckTipIncludedSteamId[_lastTeamOnPuckTipIncluded].SteamId;

                        if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionExitWatch)) {
                            lastTimeCollisionExitWatch = new Stopwatch();
                            lastTimeCollisionExitWatch.Start();
                            _lastTimeOnCollisionStayOrExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionExitWatch);
                        }
                        else if (lastTimeCollisionExitWatch.ElapsedMilliseconds > ServerConfig.MaxPossessionMilliseconds || (!string.IsNullOrEmpty(lastPlayerOnPuckTipIncludedSteamId) && lastPlayerOnPuckTipIncludedSteamId != currentPlayerSteamId)) {
                            watch.Restart();

                            if (!string.IsNullOrEmpty(lastPlayerOnPuckTipIncludedSteamId) && lastPlayerOnPuckTipIncludedSteamId != currentPlayerSteamId) {
                                if (_playersCurrentPuckTouch.TryGetValue(lastPlayerOnPuckTipIncludedSteamId, out Stopwatch lastPlayerWatch))
                                    lastPlayerWatch.Reset();
                            }
                        }
                    }
                    else
                        _lastPossession = new Possession();

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(player.Team.Value);

                    if (_puckRaycast.PuckIsGoingToNet[player.Team.Value]) {
                        if (PlayerFunc.IsGoalie(player) && Math.Abs(player.PlayerBody.Rigidbody.transform.position.z) > 13.5) {
                            PlayerTeam shooterTeam = otherTeam;
                            string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[shooterTeam].SteamId;
                            if (!string.IsNullOrEmpty(shooterSteamId)) {
                                _checkIfPuckWasSaved[player.Team.Value] = new SaveCheck {
                                    HasToCheck = true,
                                    ShooterSteamId = shooterSteamId,
                                    ShooterTeam = shooterTeam,
                                    HitStick = stick,
                                };
                            }
                        }
                        else {
                            PlayerTeam shooterTeam = otherTeam;
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
                    else {
                        if (_lastTeamOnPuckTipIncluded == otherTeam && PlayerFunc.IsGoalie(player) && Math.Abs(player.PlayerBody.Rigidbody.transform.position.z) > 13.5) {
                            if ((player.Team.Value == PlayerTeam.Blue && _puckZCoordinateDifference > ServerConfig.GoalieSaveCreaseSystemZDelta) || (player.Team.Value == PlayerTeam.Red && _puckZCoordinateDifference < -ServerConfig.GoalieSaveCreaseSystemZDelta)) {
                                (double startX, double endX) = (0, 0);
                                (double startZ, double endZ) = (0, 0);
                                if (player.Team.Value == PlayerTeam.Blue) {
                                    (startX, endX) = ZoneFunc.ICE_X_POSITIONS[IceElement.BlueTeam_BluePaint];
                                    (startZ, endZ) = ZoneFunc.ICE_Z_POSITIONS[IceElement.BlueTeam_BluePaint];
                                }
                                else {
                                    (startX, endX) = ZoneFunc.ICE_X_POSITIONS[IceElement.RedTeam_BluePaint];
                                    (startZ, endZ) = ZoneFunc.ICE_Z_POSITIONS[IceElement.RedTeam_BluePaint];
                                }

                                bool goalieIsInHisCrease = true;
                                if (player.PlayerBody.Rigidbody.transform.position.x - ServerConfig.GoalieRadius < startX ||
                                    player.PlayerBody.Rigidbody.transform.position.x + ServerConfig.GoalieRadius > endX ||
                                    player.PlayerBody.Rigidbody.transform.position.z - ServerConfig.GoalieRadius < startZ ||
                                    player.PlayerBody.Rigidbody.transform.position.z + ServerConfig.GoalieRadius > endZ) {
                                    goalieIsInHisCrease = false;
                                }

                                if (goalieIsInHisCrease) {
                                    PlayerTeam shooterTeam = TeamFunc.GetOtherTeam(player.Team.Value);
                                    string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[shooterTeam].SteamId;
                                    if (!string.IsNullOrEmpty(shooterSteamId)) {
                                        _checkIfPuckWasSaved[player.Team.Value] = new SaveCheck {
                                            HasToCheck = true,
                                            ShooterSteamId = shooterSteamId,
                                            ShooterTeam = shooterTeam,
                                            HitStick = stick,
                                        };
                                    }
                                }
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

                    if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(playerSteamId, out Stopwatch lastTimeCollisionWatch)) {
                        lastTimeCollisionWatch = new Stopwatch();
                        lastTimeCollisionWatch.Start();
                        _lastTimeOnCollisionStayOrExitWasCalled.Add(playerSteamId, lastTimeCollisionWatch);
                    }
                    lastTimeCollisionWatch.Restart();

                    string lastPlayerOnPuckTipIncluded = _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value].SteamId;

                    if (playerSteamId != lastPlayerOnPuckTipIncluded) {
                        if (!string.IsNullOrEmpty(lastPlayerOnPuckTipIncluded) && _lastTeamOnPuckTipIncluded == player.Team.Value) {
                            double timeSinceLastTouchMs = (DateTime.UtcNow - _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value].Time).TotalMilliseconds;
                            if (timeSinceLastTouchMs < 5000 && timeSinceLastTouchMs > 80) {
                                if (!_passes.TryGetValue(lastPlayerOnPuckTipIncluded, out int _))
                                    _passes.Add(lastPlayerOnPuckTipIncluded, 0);

                                _passes[lastPlayerOnPuckTipIncluded] += 1;
                                NetworkCommunication.SendDataToAll(Codebase.Constants.PASS + lastPlayerOnPuckTipIncluded, _passes[lastPlayerOnPuckTipIncluded].ToString(),
                                    Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                                LogPass(lastPlayerOnPuckTipIncluded, _passes[lastPlayerOnPuckTipIncluded]);
                            }
                        }

                        _lastPlayerOnPuckTipIncludedSteamId[player.Team.Value] = (playerSteamId, DateTime.UtcNow);
                    }

                    _lastTeamOnPuckTipIncluded = player.Team.Value;

                    if (!PuckFunc.PuckIsTipped(playerSteamId, ServerConfig.MaxTippedMilliseconds, _playersCurrentPuckTouch, _lastTimeOnCollisionStayOrExitWasCalled)) {
                        //_lastTeamOnPuck = player.Team.Value;
                        _lastPlayerOnPuckSteamId[player.Team.Value] = (playerSteamId, DateTime.UtcNow);
                    }
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

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    _lastShotWasCounted[stick.Player.Team.Value] = false;
                    _lastBlockWasCounted[stick.Player.Team.Value] = false;

                    if (!__instance.IsTouchingStick)
                        return;

                    string playerSteamId = stick.Player.SteamId.Value.ToString();

                    if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(playerSteamId, out Stopwatch lastTimeCollisionWatch)) {
                        lastTimeCollisionWatch = new Stopwatch();
                        lastTimeCollisionWatch.Start();
                        _lastTimeOnCollisionStayOrExitWasCalled.Add(playerSteamId, lastTimeCollisionWatch);
                    }
                    lastTimeCollisionWatch.Restart();

                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = (playerSteamId, DateTime.UtcNow);
                    _lastTeamOnPuckTipIncluded = stick.Player.Team.Value;

                    if (!PuckFunc.PuckIsTipped(playerSteamId, ServerConfig.MaxTippedMilliseconds, _playersCurrentPuckTouch, _lastTimeOnCollisionStayOrExitWasCalled)) {
                        //_lastTeamOnPuck = stick.Player.Team.Value;
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = (playerSteamId, DateTime.UtcNow);
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}", ServerConfig);
                }
            }
        }

        #region PlayerBodyV2_OnCollision
        /// <summary>
        /// Class that patches the OnCollisionEnter event from PlayerBodyV2.
        /// </summary>
        [HarmonyPatch(typeof(PlayerBodyV2), "OnCollisionEnter")]
        public class PlayerBodyV2_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(PlayerBodyV2 __instance, Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing || !_logic)
                    return;

                try {
                    if (collision.gameObject.layer != LayerMask.NameToLayer("Player"))
                        return;

                    PlayerBodyV2 collisionPlayerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);

                    if (!collisionPlayerBody || !collisionPlayerBody.Player || !collisionPlayerBody.Player.IsCharacterFullySpawned)
                        return;

                    if (!__instance || !__instance.Player || !__instance.Player.IsCharacterFullySpawned)
                        return;

                    //float force = Utils.GetCollisionForce(collision);

                    // If the player has been hit by the same team, return;
                    if (collisionPlayerBody.Player.Team.Value == __instance.Player.Team.Value)
                        return;

                    string collisionPlayerBodySteamId = collisionPlayerBody.Player.SteamId.Value.ToString();
                    if (!_playerIsDown.TryGetValue(collisionPlayerBodySteamId, out bool collisionPlayerBodyIsDown))
                        collisionPlayerBodyIsDown = false;

                    string instancePlayerSteamId = __instance.Player.SteamId.Value.ToString();

                    if (!collisionPlayerBodyIsDown && (collisionPlayerBody.HasFallen || collisionPlayerBody.HasSlipped)) {
                        if (_playerIsDown.TryGetValue(collisionPlayerBodySteamId, out bool _))
                            _playerIsDown[collisionPlayerBodySteamId] = true;
                        else
                            _playerIsDown.Add(collisionPlayerBodySteamId, true);

                        if (__instance.Player.PlayerBody.HasFallen || __instance.Player.PlayerBody.HasSlipped) {
                            if (_playerIsDown.TryGetValue(instancePlayerSteamId, out bool _))
                                _playerIsDown[instancePlayerSteamId] = true;
                            else
                                _playerIsDown.Add(instancePlayerSteamId, true);

                            return;
                        }

                        ProcessHit(__instance.Player.SteamId.Value.ToString());
                    }

                    if (__instance.Player.PlayerBody.HasFallen || __instance.Player.PlayerBody.HasSlipped) {
                        if (_playerIsDown.TryGetValue(instancePlayerSteamId, out bool _))
                            _playerIsDown[instancePlayerSteamId] = true;
                        else
                            _playerIsDown.Add(instancePlayerSteamId, true);
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(PlayerBodyV2_OnCollisionEnter_Patch)} Postfix().\n{ex}", ServerConfig);
                }

                return;
            }
        }
        #endregion

        /// <summary>
        /// Class that patches the OnStandUp event from PlayerBodyV2.
        /// </summary>
        [HarmonyPatch(typeof(PlayerBodyV2), nameof(PlayerBodyV2.OnStandUp))]
        public class PlayerBodyV2_OnStandUp_Patch {
            [HarmonyPostfix]
            public static void Postfix(PlayerBodyV2 __instance) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing || !_logic)
                    return;

                try {
                    string playerSteamId = __instance.Player.SteamId.Value.ToString();
                    if (_playerIsDown.TryGetValue(playerSteamId, out bool _))
                        _playerIsDown[playerSteamId] = false;
                    else
                        _playerIsDown.Add(playerSteamId, false);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(PlayerBodyV2_OnStandUp_Patch)} Postfix().\n{ex}", ServerConfig);
                }

                return;
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
                        ResetPuckWasSavedOrBlockedChecks();

                        _puckLastCoordinate = Vector3.zero;
                        _puckZCoordinateDifference = 0;

                        // Reset player on puck.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
                            _lastPlayerOnPuckTipIncludedSteamId[key] = ("", DateTime.MinValue);

                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckSteamId.Keys))
                            _lastPlayerOnPuckSteamId[key] = ("", DateTime.MinValue);

                        // Reset shot counted states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastShotWasCounted.Keys))
                            _lastShotWasCounted[key] = true;

                        // Reset block counted states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastBlockWasCounted.Keys))
                            _lastBlockWasCounted[key] = true;

                        // Reset puck collision stay or exit times.
                        foreach (Stopwatch watch in _lastTimeOnCollisionStayOrExitWasCalled.Values)
                            watch.Stop();
                        _lastTimeOnCollisionStayOrExitWasCalled.Clear();

                        // Reset tipped times.
                        foreach (Stopwatch watch in _playersCurrentPuckTouch.Values)
                            watch.Stop();
                        _playersCurrentPuckTouch.Clear();

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
                                        starPoints[steamId] += (((double)saveValues.Saves) / ((double)saveValues.Shots) - 0.750d) * ((double)saveValues.Saves) * 18.5d;

                                    if (_sog.TryGetValue(steamId, out int shots))
                                        starPoints[steamId] += ((double)shots) * 1d;

                                    if (_passes.TryGetValue(steamId, out int passes))
                                        starPoints[steamId] += ((double)passes) * 2d;

                                    const double GOALIE_GOAL_MODIFIER = 175d;
                                    const double GOALIE_ASSIST_MODIFIER = 30d;

                                    starPoints[steamId] += GOALIE_GOAL_MODIFIER * gwgModifier;
                                    starPoints[steamId] += ((double)player.Goals.Value) * GOALIE_GOAL_MODIFIER;
                                    starPoints[steamId] += ((double)player.Assists.Value) * GOALIE_ASSIST_MODIFIER;
                                }
                                else {
                                    if (_sog.TryGetValue(steamId, out int shots)) {
                                        starPoints[steamId] += ((double)shots) * 5d;
                                        starPoints[steamId] += (((double)(player.Goals.Value + 1)) / ((double)shots) - 0.25d) * ((double)shots) * 4d;
                                    }

                                    if (_passes.TryGetValue(steamId, out int passes))
                                        starPoints[steamId] += ((double)passes) * 0.5d;

                                    if (_blocks.TryGetValue(steamId, out int blocks))
                                        starPoints[steamId] += ((double)blocks) * 5d;

                                    const double SKATER_GOAL_MODIFIER = 70d;
                                    const double SKATER_ASSIST_MODIFIER = 30d;

                                    starPoints[steamId] += SKATER_GOAL_MODIFIER * gwgModifier;
                                    starPoints[steamId] += ((double)player.Goals.Value) * SKATER_GOAL_MODIFIER;
                                    starPoints[steamId] += ((double)player.Assists.Value) * SKATER_ASSIST_MODIFIER;
                                }

                                if (_hits.TryGetValue(steamId, out int hits))
                                    starPoints[steamId] += ((double)hits) * 0.2d;

                                if (_takeaways.TryGetValue(steamId, out int takeaways))
                                    starPoints[steamId] += ((double)takeaways) * 0.2d;

                                if (_turnovers.TryGetValue(steamId, out int turnovers))
                                    starPoints[steamId] -= ((double)turnovers) * 0.2d;

                                if (_plusMinus.TryGetValue(steamId, out int plusMinus))
                                    starPoints[steamId] += ((double)plusMinus) * 5d;

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

                            Dictionary<string, string> playersUsername = new Dictionary<string, string>();
                            foreach ((string steamId, string username) in _playersInfo.Values) {
                                Logging.Log($"steamId {steamId} username {username}", ServerConfig, true); // TODO : Remove debug logs.
                                playersUsername.Add(steamId, username);
                            }

                            Dictionary<string, (string, int)> sogDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _sog)
                                sogDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, int)> passesDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _passes)
                                passesDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, int)> blocksDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _blocks)
                                blocksDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, int)> hitsDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _hits)
                                hitsDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, int)> takeawaysDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _takeaways)
                                takeawaysDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, int)> turnoversDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _turnovers)
                                turnoversDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, (int, int))> savePercDict = new Dictionary<string, (string, (int, int))>();
                            foreach (var kvp in _savePerc)
                                savePercDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            Dictionary<string, (string, int)> stickSavesDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _stickSaves)
                                stickSavesDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            List<string> blueGoalsDict = new List<string>();
                            foreach (var goalSteamId in _blueGoals)
                                blueGoalsDict.Add(goalSteamId + "," + (playersUsername.TryGetValue(goalSteamId, out string username) == true ? username : ""));

                            List<string> redGoalsDict = new List<string>();
                            foreach (var goalSteamId in _redGoals)
                                redGoalsDict.Add(goalSteamId + "," + (playersUsername.TryGetValue(goalSteamId, out string username) == true ? username : ""));

                            List<string> blueAssistsDict = new List<string>();
                            foreach (var assistSteamId in _blueAssists)
                                blueAssistsDict.Add(assistSteamId + "," + (playersUsername.TryGetValue(assistSteamId, out string username) == true ? username : ""));

                            List<string> redAssistsDict = new List<string>();
                            foreach (var assistSteamId in _redAssists)
                                redAssistsDict.Add(assistSteamId + "," + (playersUsername.TryGetValue(assistSteamId, out string username) == true ? username : ""));

                            Dictionary<int, (string, string)> starsDict = new Dictionary<int, (string, string)>();
                            foreach (var kvp in _stars)
                                starsDict.Add(kvp.Key, (kvp.Value, playersUsername.TryGetValue(kvp.Value, out string username) == true ? username : ""));

                            Dictionary<string, (string, int)> plusMinusDict = new Dictionary<string, (string, int)>();
                            foreach (var kvp in _plusMinus)
                                plusMinusDict.Add(kvp.Key, (playersUsername.TryGetValue(kvp.Key, out string username) == true ? username : "", kvp.Value));

                            // Log JSON for game stats.
                            Dictionary<string, object> jsonDict = new Dictionary<string, object> {
                                { "sog", sogDict },
                                { "passes", passesDict },
                                { "blocks", blocksDict },
                                { "hits", hitsDict },
                                { "takeaways", takeawaysDict },
                                { "turnovers", turnoversDict },
                                { "saveperc", savePercDict },
                                { "sticksaves", stickSavesDict },
                                { "bluegoals", blueGoalsDict },
                                { "redgoals", redGoalsDict },
                                { "blueassists", blueAssistsDict },
                                { "redassists", redAssistsDict },
                                { "gwg", gwgSteamId + "," + (playersUsername.TryGetValue(gwgSteamId, out string gwgUsername) == true ? gwgUsername : "") },
                                { "stars", starsDict },
                                { "plusminus", plusMinusDict },
                            };

                            string jsonContent = JsonConvert.SerializeObject(jsonDict, Formatting.Indented);
                            Logging.Log("Stats:" + jsonContent, ServerConfig);

                            if (ServerConfig.SaveEOGJSON) {
                                try {
                                    string statsFolderPath = Path.Combine(Path.GetFullPath("."), "stats");
                                    if (!Directory.Exists(statsFolderPath))
                                        Directory.CreateDirectory(statsFolderPath);
                                    string jsonPath = Path.Combine(statsFolderPath, Constants.MOD_NAME + "_" + DateTime.UtcNow.ToString("dd-MM-yyyy_HH-mm-ss") + ".json");

                                    File.WriteAllText(jsonPath, jsonContent);
                                }
                                catch (Exception ex) {
                                    Logging.LogError($"Can't write the end of game stats in the stats folder. (Permission error ?)\n{ex}", ServerConfig);
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
                    EventManager.Instance.AddEventListener(Codebase.Constants.RULESET_MOD_NAME, Event_OnRulesetTrigger);
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
                    EventManager.Instance.RemoveEventListener(Codebase.Constants.RULESET_MOD_NAME, Event_OnRulesetTrigger);
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

                        case Codebase.Constants.LOGIC:
                            _logic = bool.Parse(value);
                            break;
                    }
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnStatsTrigger)}.\n{ex}", ServerConfig);
            }
        }

        public static void Event_OnRulesetTrigger(Dictionary<string, object> message) {
            try {
                foreach (KeyValuePair<string, object> kvp in message) {
                    string value = (string)kvp.Value;
                    if (!NetworkCommunication.GetDataNamesToIgnore().Contains(kvp.Key))
                        Logging.Log($"Received data {kvp.Key}. Content : {value}", ServerConfig);

                    switch (kvp.Key) {
                        case Codebase.Constants.PAUSE:
                            _paused = bool.Parse(value);
                            break;
                    }
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnRulesetTrigger)}.\n{ex}", ServerConfig);
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

            //Logging.Log("Event_OnClientConnected", ServerConfig);

            try {
                Server_RegisterNamedMessageHandler();

                ulong clientId = (ulong)message["clientId"];
                string clientSteamId = PlayerManager.Instance.GetPlayerByClientId(clientId).SteamId.Value.ToString();
                try {
                    _playersInfo.Add(clientId, ("", ""));
                }
                catch {
                    _playersInfo.Remove(clientId);
                    _playersInfo.Add(clientId, ("", ""));
                }

                CheckForRulesetMod();
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnClientConnected)}.\n{ex}", ServerConfig);
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

            //Logging.Log("Event_OnClientDisconnected", ServerConfig);

            try {
                ulong clientId = (ulong)message["clientId"];
                string clientSteamId;
                try {
                    clientSteamId = _playersInfo[clientId].SteamId;
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(_playersInfo)}.", ServerConfig);
                    return;
                }

                _sentOutOfDateMessage.Remove(clientId);

                _playerIsDown.Remove(clientSteamId);
                _playersCurrentPuckTouch.Remove(clientSteamId);
                _lastTimeOnCollisionStayOrExitWasCalled.Remove(clientSteamId);

                _playersInfo.Remove(clientId);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnClientDisconnected)}.\n{ex}", ServerConfig);
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
                _stickSaves.Clear();
                _passes.Clear();
                _blocks.Clear();
                _hits.Clear();
                _takeaways.Clear();
                _turnovers.Clear();
                _blueGoals.Clear();
                _blueAssists.Clear();
                _redGoals.Clear();
                _redAssists.Clear();
                _plusMinus.Clear();

                ScoreboardModifications(false);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_Client_OnClientStopped)}.\n{ex}", _clientConfig);
            }
        }

        public static void Event_OnPlayerRoleChanged(Dictionary<string, object> message) {
            // Use the event to link client Ids to Steam Ids.
            Dictionary<ulong, (string SteamId, string Username)> playersInfo_ToChange = new Dictionary<ulong, (string, string)>();
            foreach (var kvp in _playersInfo) {
                if (string.IsNullOrEmpty(kvp.Value.SteamId)) {
                    Player _player = PlayerManager.Instance.GetPlayerByClientId(kvp.Key);
                    playersInfo_ToChange.Add(kvp.Key, (_player.SteamId.Value.ToString(), _player.Username.Value.ToString()));
                }
            }

            foreach (var kvp in playersInfo_ToChange) {
                if (!string.IsNullOrEmpty(kvp.Value.SteamId)) {
                    _playersInfo[kvp.Key] = kvp.Value;
                    Logging.Log($"Added clientId {kvp.Key} linked to Steam Id {kvp.Value.SteamId} ({kvp.Value.Username}).", ServerConfig);
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
        /// <summary>
        /// Method that processes a hit by a player.
        /// </summary>
        /// <param name="hitterSteamId">String, steam Id of the player that made a hit.</param>
        private static void ProcessHit(string hitterSteamId) {
            if (!_hits.TryGetValue(hitterSteamId, out int _))
                _hits.Add(hitterSteamId, 0);

            _hits[hitterSteamId] += 1;
            NetworkCommunication.SendDataToAll(Codebase.Constants.HIT + hitterSteamId, _hits[hitterSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            LogHit(hitterSteamId, _hits[hitterSteamId]);
        }

        /// <summary>
        /// Method that processes a blocked shot by a player.
        /// </summary>
        /// <param name="blockerSteamId">String, steam Id of the player that blocked a shot.</param>
        private static void ProcessBlock(string blockerSteamId) {
            if (!_blocks.TryGetValue(blockerSteamId, out int _))
                _blocks.Add(blockerSteamId, 0);

            _blocks[blockerSteamId] += 1;
            NetworkCommunication.SendDataToAll(Codebase.Constants.BLOCK + blockerSteamId, _blocks[blockerSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            LogBlock(blockerSteamId, _blocks[blockerSteamId]);
        }

        private static void ProcessTakeaways(string takeawaySteamId) {
            if (!_takeaways.TryGetValue(takeawaySteamId, out int _))
                _takeaways.Add(takeawaySteamId, 0);

            _takeaways[takeawaySteamId] += 1;
            NetworkCommunication.SendDataToAll(Codebase.Constants.TAKEAWAY + takeawaySteamId, _takeaways[takeawaySteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            LogTakeaways(takeawaySteamId, _takeaways[takeawaySteamId]);
        }

        private static void ProcessTurnovers(string turnoverSteamId) {
            if (!_turnovers.TryGetValue(turnoverSteamId, out int _))
                _turnovers.Add(turnoverSteamId, 0);

            _turnovers[turnoverSteamId] += 1;
            NetworkCommunication.SendDataToAll(Codebase.Constants.TURNOVER + turnoverSteamId, _turnovers[turnoverSteamId].ToString(), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            LogTurnovers(turnoverSteamId, _turnovers[turnoverSteamId]);
        }

        private static void Server_RegisterNamedMessageHandler() {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null && !_hasRegisteredWithNamedMessageHandler) {
                Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT_TO_SERVER}.", ServerConfig);
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER, ReceiveData);

                _hasRegisteredWithNamedMessageHandler = true;
            }
        }

        private static void CheckForRulesetMod() {
            if (ModManagerV2.Instance == null || ModManagerV2.Instance.EnabledModIds == null || (_rulesetModEnabled != null && (bool)_rulesetModEnabled))
                return;

            _rulesetModEnabled = ModManagerV2.Instance.EnabledModIds.Contains(3501446576) ||
                                 ModManagerV2.Instance.EnabledModIds.Contains(3500559233);
            Logging.Log($"Ruleset mod is enabled : {_rulesetModEnabled}.", ServerConfig, true);
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

                    case Constants.MOD_NAME + "_kick": // SERVER-SIDE : Warn the client that the mod is out of date.
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

                    case Constants.ASK_SERVER_FOR_STARTUP_DATA: // SERVER-SIDE : Send the necessary data to client.
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

                    /*case RESET_SOG:
                        if (dataStr != "1")
                            break;

                        Client_ResetSOG();
                        break;

                    case RESET_SAVEPERC:
                        if (dataStr != "1")
                            break;

                        Client_ResetSavePerc();
                        break;*/

                    case RESET_ALL:
                        if (dataStr != "1")
                            break;

                        Client_ResetSOG();
                        Client_ResetSavePerc();
                        Client_ResetPasses();
                        Client_ResetBlocks();
                        Client_ResetHits();
                        Client_ResetTakeaways();
                        Client_ResetTurnovers();
                        Client_ResetStickSaves();
                        Client_ResetPlusMinus();
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

            // Write to client-side file.
            WriteClientSideFile_SOG();
        }

        private static void WriteClientSideFile_SOG() {
            if (_clientConfig.LogClientSideStats) {
                StringBuilder csvContent = new StringBuilder();
                foreach (var kvp in _sog) {
                    Player player = PlayerManager.Instance.GetPlayerBySteamId(kvp.Key);
                    if (!player || PlayerFunc.IsGoalie(player))
                        continue;

                    csvContent.AppendLine($"{player.Username.Value};{player.Number.Value};{player.Team.Value};{kvp.Key};{kvp.Value}");
                }

                File.WriteAllText(Path.Combine(Path.GetFullPath("."), Constants.MOD_NAME + "_shots.csv"), csvContent.ToString());
            }
        }

        private static void ReceiveData_SavePerc(string playerSteamId, string dataStr) {
            string[] dataStrSplitted = SystemFunc.RemoveWhitespace(dataStr.Replace("(", "").Replace(")", "")).Split(',');
            int saves = int.Parse(dataStrSplitted[0]);
            int shots = int.Parse(dataStrSplitted[1]);

            if (_savePerc.TryGetValue(playerSteamId, out var _)) {
                _savePerc[playerSteamId] = (saves, shots);
                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                if (currentPlayer && PlayerFunc.IsGoalie(currentPlayer))
                    _sogLabels[playerSteamId].text = GetGoalieSavePerc(saves, shots);
            }
            else
                _savePerc.Add(playerSteamId, (saves, shots));

            // Write to client-side file.
            WriteClientSideFile_SavePerc();
        }

        private static void WriteClientSideFile_SavePerc() {
            if (_clientConfig.LogClientSideStats) {
                StringBuilder csvContent = new StringBuilder();
                foreach (var kvp in _savePerc) {
                    Player player = PlayerManager.Instance.GetPlayerBySteamId(kvp.Key);
                    if (!player || !PlayerFunc.IsGoalie(player))
                        continue;
                    csvContent.AppendLine($"{player.Username.Value};{player.Number.Value};{player.Team.Value};{kvp.Key};{kvp.Value.Saves};{kvp.Value.Shots}");
                }

                File.WriteAllText(Path.Combine(Path.GetFullPath("."), Constants.MOD_NAME + "_saves.csv"), csvContent.ToString());
            }
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

                        Label sogHeader = new Label("S/SV%") {
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
            ResetPuckWasSavedOrBlockedChecks();

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

        private static void ResetPuckWasSavedOrBlockedChecks() {
            // Reset puck was saved states.
            foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
                _checkIfPuckWasSaved[key] = new SaveCheck();

            // Reset puck was blocked states.
            foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasBlocked.Keys))
                _checkIfPuckWasBlocked[key] = new BlockCheck();
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
        /// Method that logs the stick saves of a goalie.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="stickSaves">Int, number of stick saves.</param>
        private static void LogStickSave(string playerSteamId, int stickSaves) {
            Logging.Log($"playerSteamId:{playerSteamId},sticksv:{stickSaves}", ServerConfig);
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
        /// Method that logs the hits of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="hit">Int, number of hits.</param>
        private static void LogHit(string playerSteamId, int hit) {
            Logging.Log($"playerSteamId:{playerSteamId},hit:{hit}", ServerConfig);
        }

        /// <summary>
        /// Method that logs the takeaways of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="takeaway">Int, number of takeaways.</param>
        private static void LogTakeaways(string playerSteamId, int takeaway) {
            Logging.Log($"playerSteamId:{playerSteamId},takeaway:{takeaway}", ServerConfig);
        }

        /// <summary>
        /// Method that logs the turnovers of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="turnover">Int, number of turnovers.</param>
        private static void LogTurnovers(string playerSteamId, int turnover) {
            Logging.Log($"playerSteamId:{playerSteamId},turnover:{turnover}", ServerConfig);
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

        /// <summary>
        /// Method that logs the +/- of a player.
        /// </summary>
        /// <param name="playerSteamId">String, steam Id of the player.</param>
        /// <param name="plusminus">Int, +/-.</param>
        private static void LogPlusMinus(string playerSteamId, int plusminus) {
            Logging.Log($"playerSteamId:{playerSteamId},plusminus:{plusminus}", ServerConfig);
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

        private static void Client_ResetSOG() {
            foreach (string key in new List<string>(_sog.Keys)) {
                if (_sogLabels.TryGetValue(key, out Label label)) {
                    _sog[key] = 0;
                    label.text = "0";

                    Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(key);
                    if (currentPlayer != null && currentPlayer && PlayerFunc.IsGoalie(currentPlayer))
                        label.text = "0.000";
                }
                else {
                    _sog.Remove(key);
                    _savePerc.Remove(key);
                }
            }

            WriteClientSideFile_SOG();
        }

        private static void Client_ResetSavePerc() {
            foreach (string key in new List<string>(_savePerc.Keys))
                _savePerc[key] = (0, 0);

            WriteClientSideFile_SavePerc();
        }

        private static void Client_ResetPasses() {
            foreach (string key in new List<string>(_passes.Keys))
                _passes[key] = 0;
        }

        private static void Client_ResetBlocks() {
            foreach (string key in new List<string>(_blocks.Keys))
                _blocks[key] = 0;
        }

        private static void Client_ResetHits() {
            foreach (string key in new List<string>(_hits.Keys))
                _hits[key] = 0;
        }

        private static void Client_ResetTakeaways() {
            foreach (string key in new List<string>(_takeaways.Keys))
                _takeaways[key] = 0;
        }

        private static void Client_ResetTurnovers() {
            foreach (string key in new List<string>(_turnovers.Keys))
                _turnovers[key] = 0;
        }

        private static void Client_ResetStickSaves() {
            foreach (string key in new List<string>(_stickSaves.Keys))
                _stickSaves[key] = 0;
        }

        private static void Client_ResetPlusMinus() {
            foreach (string key in new List<string>(_plusMinus.Keys))
                _plusMinus[key] = 0;
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
            internal bool HitStick { get; set; } = false;
        }

        internal class BlockCheck : Check {
            internal string BlockerSteamId { get; set; } = "";
            internal PlayerTeam ShooterTeam { get; set; } = PlayerTeam.Blue;
        }
        
        internal class Possession {
            internal string SteamId { get; set; } = "";

            internal PlayerTeam Team { get; set; } = PlayerTeam.None;

            internal DateTime Date { get; set; } = DateTime.MinValue;
        }
        #endregion
    }
}
