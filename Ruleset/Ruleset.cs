using Codebase;
using HarmonyLib;
using oomtm450PuckMod_Ruleset.Configs;
using oomtm450PuckMod_Ruleset.FaceoffViolation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing the main code for the Ruleset patch.
    /// </summary>
    public class Ruleset : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private static readonly string MOD_VERSION = "0.27.1DEV";

        /// <summary>
        /// ReadOnlyCollection of string, last released versions of the mod.
        /// </summary>
        private static readonly ReadOnlyCollection<string> OLD_MOD_VERSIONS = new ReadOnlyCollection<string>(new List<string> {
            "0.16.0",
            "0.16.1",
            "0.16.2",
            "0.17.0",
            "0.18.0",
            "0.18.1",
            "0.18.2",
            "0.19.0",
            "0.20.0",
            "0.20.1",
            "0.20.2",
            "0.21.0",
            "0.21.1",
            "0.21.2",
            "0.22.0",
            "0.22.1",
            "0.22.2",
            "0.23.0",
            "0.24.0",
            "0.24.1",
            "0.25.0",
            "0.26.0",
            "0.26.1",
            "0.26.2",
            "0.26.3",
            "0.26.4",
        });

        /// <summary>
        /// ReadOnlyCollection of string, collection of datanames to not log.
        /// </summary>
        private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> {
            "eventName",
            RefSignals.SHOW_SIGNAL_BLUE,
            RefSignals.SHOW_SIGNAL_RED,
            RefSignals.STOP_SIGNAL_BLUE,
            RefSignals.STOP_SIGNAL_RED,
            RefSignals.STOP_SIGNAL,
        });
        #endregion

        #region Fields
        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static readonly Harmony _harmony = new Harmony(Constants.MOD_NAME);

        /// <summary>
        /// Bool, true if the mod has been patched in.
        /// </summary>
        private static bool _harmonyPatched = false;

        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        internal static ServerConfig ServerConfig { get; set; } = new ServerConfig();

        /// <summary>
        /// ClientConfig, config set by the client.
        /// </summary>
        internal static ClientConfig ClientConfig { get; set; } = new ClientConfig();

        /// <summary>
        /// LockList of PlayerIcing, positions of the players on the ice for icing logic.
        /// </summary>
        private static readonly LockList<PlayerIcing> _dictPlayersPositionsForIcing = new LockList<PlayerIcing>();

        /// <summary>
        /// LockDictionary of string and (PlayerTeam and bool), dictionary of offside status of each player with steam Id as a key.
        /// </summary>
        private static readonly LockDictionary<string, (PlayerTeam Team, bool IsOffside)> _isOffside = new LockDictionary<string, (PlayerTeam, bool)>();

        /// <summary>
        /// LockDictionary of string and bool, dictionary of number of frames since player has been in a no high stick situation with steam Id as a key.
        /// </summary>
        private static readonly LockDictionary<string,  int> _noHighStickFrames = new LockDictionary<string, int>();

        /// <summary>
        /// LockDictionary of PlayerTeam and bool, dictionary for teams if high stick has to be called next frame.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, bool> _callHighStickNextFrame = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        /// <summary>
        /// LockDictionary of PlayerTeam and IcingObject, dictionary for teams if icing is possible if it reaches the end of the ice and its delta.
        /// Stopwatch is null if it is not possible.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, IcingObject> _isIcingPossible = new LockDictionary<PlayerTeam, IcingObject> {
            { PlayerTeam.Blue, new IcingObject() },
            { PlayerTeam.Red, new IcingObject() },
        };

        /// <summary>
        /// LockDictionary of PlayerTeam and bool, dictionary for teams if icing is active.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, bool> _isIcingActive = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        /// <summary>
        /// LockDictionary of PlayerTeam and bool, dictionary for teams if puck is behind hashmarks.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, bool> _isPuckBehindHashmarks = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static readonly LockDictionary<PlayerTeam, Timer> _isIcingActiveTimers = new LockDictionary<PlayerTeam, Timer> {
            { PlayerTeam.Blue, new Timer(ResetIcingCallback, PlayerTeam.Blue, Timeout.Infinite, Timeout.Infinite) },
            { PlayerTeam.Red, new Timer(ResetIcingCallback, PlayerTeam.Red, Timeout.Infinite, Timeout.Infinite) },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _isHighStickActive = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static readonly LockDictionary<PlayerTeam, Timer> _isHighStickActiveTimers = new LockDictionary<PlayerTeam, Timer> {
            { PlayerTeam.Blue, new Timer(ResetHighStickCallback, PlayerTeam.Blue, Timeout.Infinite, Timeout.Infinite) },
            { PlayerTeam.Red, new Timer(ResetHighStickCallback, PlayerTeam.Red, Timeout.Infinite, Timeout.Infinite) },
        };

        private static readonly LockDictionary<PlayerTeam, Stopwatch> _goalieIntTimer = new LockDictionary<PlayerTeam, Stopwatch> {
            { PlayerTeam.Blue, null },
            { PlayerTeam.Red, null },
        };

        private static readonly LockDictionary<Rule, (Vector3 Position, Zone Zone)> _puckLastStateBeforeCall = new LockDictionary<Rule, (Vector3, Zone)> {
            { Rule.Offside, (Vector3.zero, ZoneFunc.DEFAULT_ZONE) },
            { Rule.Icing, (Vector3.zero, ZoneFunc.DEFAULT_ZONE) },
            { Rule.HighStick, (Vector3.zero, ZoneFunc.DEFAULT_ZONE) },
            { Rule.GoalieInt, (Vector3.zero, ZoneFunc.DEFAULT_ZONE) },
        };

        /// <summary>
        /// Zone, current zone of the puck.
        /// </summary>
        private static Zone _puckZone = ZoneFunc.DEFAULT_ZONE;

        /// <summary>
        /// Zone, zone of the puck when it was last touched.
        /// </summary>
        private static Zone _puckZoneLastTouched = ZoneFunc.DEFAULT_ZONE;

        /// <summary>
        /// LockDictionary of string and (PlayerTeam and Zone), dictionary of all the players' zone by steam Id.
        /// </summary>
        private static readonly LockDictionary<string, (PlayerTeam Team, Zone Zone)> _playersZone = new LockDictionary<string, (PlayerTeam, Zone)>();

        /// <summary>
        /// LockDictionary of string and Stopwatch, dictionary of all players current puck touch time.
        /// </summary>
        private static readonly LockDictionary<string, Stopwatch> _playersCurrentPuckTouch = new LockDictionary<string, Stopwatch>();

        /// <summary>
        /// LockDictionary of string and Stopwatch, dictionary of all players last puck possession time.
        /// </summary>
        private static readonly LockDictionary<string, Stopwatch> _playersLastTimePuckPossession = new LockDictionary<string, Stopwatch>();

        /// <summary>
        /// LockDictionary of ulong and DateTime, last time a mod out of date message was sent to a client (ulong clientId).
        /// </summary>
        private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

        //private static InputAction _getStickLocation;

        /// <summary>
        /// LockDictionary of string and Stopwatch, dictionary of all players last puck OnCollisionStay or OnCollisionExit time.
        /// </summary>
        private static readonly LockDictionary<string, Stopwatch> _lastTimeOnCollisionStayOrExitWasCalled = new LockDictionary<string, Stopwatch>();

        /// <summary>
        /// Bool, true if phase was changed by Ruleset.
        /// </summary>
        private static bool _changedPhase = false;

        private static int _periodTimeRemaining = 0;

        private static PlayerTeam _lastPlayerOnPuckTeam = TeamFunc.DEFAULT_TEAM;

        private static PlayerTeam _lastPlayerOnPuckTeamTipIncluded = TeamFunc.DEFAULT_TEAM;

        private static readonly LockDictionary<PlayerTeam, string> _lastPlayerOnPuckSteamId = new LockDictionary<PlayerTeam, string> {
            { PlayerTeam.Blue, "" },
            { PlayerTeam.Red, "" },
        };

        private static readonly LockDictionary<PlayerTeam, string> _lastPlayerOnPuckTipIncludedSteamId = new LockDictionary<PlayerTeam, string> {
            { PlayerTeam.Blue, "" },
            { PlayerTeam.Red, "" },
        };

        /// <summary>
        /// LockDictionary of string and (PlayerTeam and DateTime), dictionary of all DateTime of every player last puck touch.
        /// </summary>
        private static readonly LockDictionary<string, (PlayerTeam Team, DateTime LastTouchDateTime)> _playersOnPuckTipIncludedDateTime = new LockDictionary<string, (PlayerTeam, DateTime)>();

        private static readonly LockDictionary<PlayerTeam, bool> _lastGoalieStateCollision = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        /// <summary>
        /// LockDictionary of string and DateTime, steamId of a player that dived and when he's supposed to get up.
        /// </summary>
        private static readonly LockDictionary<string, DateTime> _dives = new LockDictionary<string, DateTime>();

        private static float _lastForceOnGoalie = 0;

        private static string _lastForceOnGoaliePlayerSteamId = "";

        /// <summary>
        /// Bool, true if there's a pause in play.
        /// </summary>
        private static bool _paused = false;

        private static bool _doFaceoff = false;

        /// <summary>
        /// Bool, true if the mod has registered with the named message handler for server/client communication.
        /// </summary>
        private static bool _hasRegisteredWithNamedMessageHandler = false;

        /// <summary>
        /// FaceoffSpot, where the next faceoff has to be taken.
        /// </summary>
        private static FaceoffSpot _nextFaceoffSpot = FaceoffSpot.Center;

        /// <summary>
        /// FaceoffSpot, property of _nextFaceoffSpot.
        /// </summary>
        internal static FaceoffSpot NextFaceoffSpot {
            get {
                return _nextFaceoffSpot;
            }
            set {
                if (_nextFaceoffSpot != value) {
                    _nextFaceoffSpot = value;

                    try {
                        EventManager.Instance.TriggerEvent(Codebase.Constants.RULESET_MOD_NAME, new Dictionary<string, object> { { Codebase.Constants.NEXT_FACEOFF, _nextFaceoffSpot.ToString() } });
                        if (!NetworkCommunication.GetDataNamesToIgnore().Contains(Codebase.Constants.NEXT_FACEOFF))
                            Logging.Log($"Sent data \"{Codebase.Constants.NEXT_FACEOFF}\" to {Codebase.Constants.RULESET_MOD_NAME}.", ServerConfig);
                    }
                    catch (Exception ex) {
                        Logging.LogError($"Error in {nameof(NextFaceoffSpot)} setter.\n{ex}", ServerConfig);
                    }
                }
            }
        }

        private static float _puckScale = 1f;

        private static Rule _lastStoppageReason = Rule.None;

        private static readonly LockDictionary<PlayerTeam, int> _lastIcing = new LockDictionary<PlayerTeam, int> {
            { PlayerTeam.Blue, int.MaxValue },
            { PlayerTeam.Red, int.MaxValue },
        };

        private static readonly LockDictionary<PlayerTeam, int> _icingStaminaDrainPenaltyAmount = new LockDictionary<PlayerTeam, int> {
            { PlayerTeam.Blue, 0 },
            { PlayerTeam.Red, 0 },
        };

        private static FaceOffBoundaryManager _boundaryManager;
        private static FaceOffPlayerUnfreezer _playerUnfreezer;
        private static FaceOffPuckValidator _puckValidator;

        // Client-side.
        private static RefSignals _refSignalsBlueTeam = null;

        private static RefSignals _refSignalsRedTeam = null;

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

        // Barrier collider, position 0 -19 0 is realistic.
        #endregion

        #region Properties
        private static bool Paused {
            get { return _paused; }
            set {
                _paused = value;
                try {
                    EventManager.Instance.TriggerEvent(Codebase.Constants.RULESET_MOD_NAME, new Dictionary<string, object>{ { Codebase.Constants.PAUSE, _paused.ToString() } });
                    if (!NetworkCommunication.GetDataNamesToIgnore().Contains(Codebase.Constants.PAUSE))
                        Logging.Log($"Sent data \"{Codebase.Constants.PAUSE}\" to {Codebase.Constants.RULESET_MOD_NAME}.", ServerConfig);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(Paused)} setter.\n{ex}", ServerConfig);
                }
            }
        }

        private static bool ChangedPhase {
            get { return _changedPhase; }
            set {
                _changedPhase = value;
                try {
                    EventManager.Instance.TriggerEvent(Codebase.Constants.RULESET_MOD_NAME, new Dictionary<string, object> { { Codebase.Constants.CHANGED_PHASE, _changedPhase.ToString() } });
                    if (!NetworkCommunication.GetDataNamesToIgnore().Contains(Codebase.Constants.CHANGED_PHASE))
                        Logging.Log($"Sent data \"{Codebase.Constants.CHANGED_PHASE}\" to {Codebase.Constants.RULESET_MOD_NAME}.", ServerConfig);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(ChangedPhase)} setter.\n{ex}", ServerConfig);
                }
            }
        }

        /// <summary>
        /// LockList of string, system chat messages to send next frame.
        /// </summary>
        internal static LockList<string> SystemChatMessages { get; } = new LockList<string>();

        /// <summary>
        /// Bool, true if the mod's logic has to be runned.
        /// </summary>
        internal static bool Logic { get; set; } = true;

        internal static float PuckRadius => Codebase.Constants.PUCK_RADIUS * _puckScale;
        #endregion

        #region Harmony Patches
        #region Puck_OnCollision
        /// <summary>
        /// Class that patches the OnCollisionEnter event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
        public class Puck_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Puck __instance, Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing || !Logic)
                    return;

                try {
                    _puckScale = __instance.transform.localScale.x;
                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        _puckZoneLastTouched = _puckZone;

                        PlayerTeam playerOtherTeam = TeamFunc.GetOtherTeam(playerBody.Player.Team.Value);
                        if (IsIcingPossible(__instance, playerOtherTeam)) {
                            if (IsIcing(playerOtherTeam)) {
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerOtherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send stop icing signal for client-side UI.
                                SendChat(Rule.Icing, playerOtherTeam, true, true);
                            }
                            ResetIcings();
                        }
                        else if (IsIcingPossible(__instance, playerBody.Player.Team.Value)) {
                            if (_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var playerZone)) {
                                if (ZoneFunc.GetTeamZones(playerOtherTeam, true).Any(x => x == playerZone.Zone)) {
                                    if (IsIcing(playerBody.Player.Team.Value)) {
                                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerBody.Player.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send stop icing signal for client-side UI.
                                        SendChat(Rule.Icing, playerBody.Player.Team.Value, true, true);
                                    }
                                    ResetIcings();
                                }
                            }
                        }
                        return;
                    }

                    if (!stick.Player)
                        return;

                    FaceOffPuckCollisionTracker.NotifyCollision(__instance, stick);

                    _puckZoneLastTouched = _puckZone;

                    string currentPlayerSteamId = stick.Player.SteamId.Value.ToString();

                    //Logging.Log($"Puck was hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (enter)!", ServerConfig);

                    // Start tipped timer.
                    if (!_playersCurrentPuckTouch.TryGetValue(currentPlayerSteamId, out Stopwatch watch)) {
                        watch = new Stopwatch();
                        watch.Start();
                        _playersCurrentPuckTouch.Add(currentPlayerSteamId, watch);
                    }

                    string lastPlayerOnPuckTipIncludedSteamId = _lastPlayerOnPuckTipIncludedSteamId[_lastPlayerOnPuckTeamTipIncluded];

                    if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionExitWatch)) {
                        lastTimeCollisionExitWatch = new Stopwatch();
                        lastTimeCollisionExitWatch.Start();
                        _lastTimeOnCollisionStayOrExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionExitWatch);
                    }
                    else if (lastTimeCollisionExitWatch.ElapsedMilliseconds > ServerConfig.MaxPossessionMilliseconds || (!string.IsNullOrEmpty(lastPlayerOnPuckTipIncludedSteamId) && lastPlayerOnPuckTipIncludedSteamId != currentPlayerSteamId)) {
                        //if (lastPlayerOnPuckTipIncludedSteamId == currentPlayerSteamId || string.IsNullOrEmpty(lastPlayerOnPuckTipIncludedSteamId))
                            //Logging.Log($"{stick.Player.Username.Value} had the puck for {((double)(watch.ElapsedMilliseconds - lastTimeCollisionExitWatch.ElapsedMilliseconds)) / 1000d} seconds.", ServerConfig);
                        watch.Restart();

                        if (!string.IsNullOrEmpty(lastPlayerOnPuckTipIncludedSteamId) && lastPlayerOnPuckTipIncludedSteamId != currentPlayerSteamId) {
                            if (_playersCurrentPuckTouch.TryGetValue(lastPlayerOnPuckTipIncludedSteamId, out Stopwatch lastPlayerWatch)) {
                                //Logging.Log($"{lastPlayerOnPuckTipIncludedSteamId} had the puck for {((double)(lastPlayerWatch.ElapsedMilliseconds - _lastTimeOnCollisionExitWasCalled[lastPlayerOnPuckTipIncludedSteamId].ElapsedMilliseconds)) / 1000d} seconds.", ServerConfig);
                                lastPlayerWatch.Reset();
                            }
                        }
                    }

                    // High stick logic.
                    Puck puck = PuckManager.Instance.GetPuck();
                    if (puck) {
                        if (puck.IsGrounded) {
                            if (IsHighStick(stick.Player.Team.Value)) {
                                NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(stick.Player.Team.Value, false, _puckLastStateBeforeCall[Rule.HighStick]);

                                _isHighStickActiveTimers.TryGetValue(stick.Player.Team.Value, out Timer highStickTimer);
                                highStickTimer.Change(Timeout.Infinite, Timeout.Infinite);

                                SendChat(Rule.HighStick, stick.Player.Team.Value, true);
                                _lastStoppageReason = Rule.HighStick;
                                DoFaceoff(RefSignals.GetSignalConstant(true, stick.Player.Team.Value), RefSignals.HIGHSTICK_REF);
                            }
                        }
                    }

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);
                    if (IsHighStick(otherTeam)) {
                        _isHighStickActive[otherTeam] = false;
                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, otherTeam), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        SendChat(Rule.HighStick, otherTeam, true, true);
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
                    if (!ServerFunc.IsDedicatedServer() || Paused || GameManager.Instance.Phase != GamePhase.Playing || !Logic)
                        return;

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        string playerBodySteamId = playerBody.Player.SteamId.Value.ToString();

                        _lastPlayerOnPuckTeamTipIncluded = playerBody.Player.Team.Value;
                        _lastPlayerOnPuckTipIncludedSteamId[playerBody.Player.Team.Value] = playerBodySteamId;
                        _playersOnPuckTipIncludedDateTime.AddOrUpdate(playerBodySteamId, (playerBody.Player.Team.Value, DateTime.UtcNow));

                        return;
                    }

                    _puckZoneLastTouched = _puckZone;

                    string playerSteamId = stick.Player.SteamId.Value.ToString();

                    if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(playerSteamId, out Stopwatch lastTimeCollisionWatch)) {
                        lastTimeCollisionWatch = new Stopwatch();
                        lastTimeCollisionWatch.Start();
                        _lastTimeOnCollisionStayOrExitWasCalled.Add(playerSteamId, lastTimeCollisionWatch);
                    }
                    lastTimeCollisionWatch.Restart();

                    if (!_noHighStickFrames.TryGetValue(playerSteamId, out int _))
                        _noHighStickFrames.Add(playerSteamId, int.MaxValue);

                    Puck puck = PuckManager.Instance.GetPuck();
                    if (puck && puck.Rigidbody.transform.position.y <= ServerConfig.HighStick.MaxHeight + stick.Player.PlayerBody.Rigidbody.transform.position.y)
                        _noHighStickFrames[playerSteamId] = 0;

                    var puckLastStateBeforeCallOffside = _puckLastStateBeforeCall[Rule.Offside];

                    if (!PuckFunc.PuckIsTipped(playerSteamId, ServerConfig.MaxTippedMilliseconds, _playersCurrentPuckTouch, _lastTimeOnCollisionStayOrExitWasCalled)) {
                        _lastPlayerOnPuckTeam = stick.Player.Team.Value;
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player))
                            ResetGoalAndAssistAttribution(TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam));
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = playerSteamId;

                        if (puck)
                            _puckLastStateBeforeCall[Rule.GoalieInt] = _puckLastStateBeforeCall[Rule.Offside] = (puck.Rigidbody.transform.position, _puckZone);
                    }

                    _lastPlayerOnPuckTeamTipIncluded = stick.Player.Team.Value;
                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = playerSteamId;
                    _playersOnPuckTipIncludedDateTime.AddOrUpdate(playerSteamId, (stick.Player.Team.Value, DateTime.UtcNow));

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);
                    // Offside logic.
                    List<Zone> otherTeamZones = ZoneFunc.GetTeamZones(otherTeam);
                    if (IsOffside(stick.Player.Team.Value) && (_puckZone == otherTeamZones[0] || _puckZone == otherTeamZones[1])) {
                        NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(stick.Player.Team.Value, false, puckLastStateBeforeCallOffside);
                        SendChat(Rule.Offside, stick.Player.Team.Value, true);
                        _lastStoppageReason = Rule.Offside;
                        DoFaceoff();
                    }

                    // Icing logic.
                    if (IsIcing(otherTeam)) {
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player)) {
                            NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(otherTeam, true, _puckLastStateBeforeCall[Rule.Icing]);
                            SendChat(Rule.Icing, otherTeam, true);

                            int remainingPlayTime = GameManager.Instance.GameState.Value.Time;
                            if (_lastStoppageReason == Rule.Icing && _lastIcing[stick.Player.Team.Value] > _lastIcing[otherTeam] && _lastIcing[otherTeam] - remainingPlayTime <= ServerConfig.Icing.StaminaDrainDivisionAmountPenaltyTime)
                                _icingStaminaDrainPenaltyAmount[otherTeam] += 1;
                            else
                                _icingStaminaDrainPenaltyAmount[otherTeam] = 0;

                            _lastStoppageReason = Rule.Icing;
                            _lastIcing[otherTeam] = remainingPlayTime;
                            DoFaceoff();
                        }
                        else {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, otherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send stop icing signal for client-side UI.
                            SendChat(Rule.Icing, otherTeam, true, true);
                            ResetIcings();
                        }
                    }
                    else {
                        if (IsIcing(stick.Player.Team.Value)) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, stick.Player.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send stop icing signal for client-side UI.
                            SendChat(Rule.Icing, stick.Player.Team.Value, true, true);
                        }
                        ResetIcings();
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
                    if (!ServerFunc.IsDedicatedServer() || Paused || GameManager.Instance.Phase != GamePhase.Playing || !Logic)
                        return;

                    //if (!__instance.IsTouchingStick)
                        //return;

                    Stick stick = SystemFunc.GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    Puck puck = PuckManager.Instance.GetPuck();

                    _puckZoneLastTouched = _puckZone;

                    string currentPlayerSteamId = stick.Player.SteamId.Value.ToString();

                    if (!_lastTimeOnCollisionStayOrExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionWatch)) {
                        lastTimeCollisionWatch = new Stopwatch();
                        lastTimeCollisionWatch.Start();
                        _lastTimeOnCollisionStayOrExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionWatch);
                    }

                    lastTimeCollisionWatch.Restart();

                    if (!PuckFunc.PuckIsTipped(currentPlayerSteamId, ServerConfig.MaxTippedMilliseconds, _playersCurrentPuckTouch, _lastTimeOnCollisionStayOrExitWasCalled)) {
                        _lastPlayerOnPuckTeam = stick.Player.Team.Value;
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player))
                            ResetGoalAndAssistAttribution(TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam));
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = currentPlayerSteamId;

                        if (puck)
                            _puckLastStateBeforeCall[Rule.GoalieInt] = _puckLastStateBeforeCall[Rule.Offside] = (puck.Rigidbody.transform.position, _puckZone);
                    }

                    _lastPlayerOnPuckTeamTipIncluded = stick.Player.Team.Value;
                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = currentPlayerSteamId;
                    _playersOnPuckTipIncludedDateTime.AddOrUpdate(currentPlayerSteamId, (stick.Player.Team.Value, DateTime.UtcNow));

                    // Icing logic.
                    bool icingPossible = false;
                    if (ZoneFunc.GetTeamZones(stick.Player.Team.Value, true).Any(x => x == _puckZone))
                        icingPossible = true;

                    if (icingPossible) {
                        Stopwatch icingPossibleWatch = new Stopwatch();
                        icingPossibleWatch.Start();
                        _isIcingPossible[stick.Player.Team.Value] = new IcingObject(icingPossibleWatch, 1f / (__instance.Speed / ServerConfig.Icing.Delta), _dictPlayersPositionsForIcing.Any(x => stick.Player.Team.Value == PlayerTeam.Red ? x.IsBehindBlueTeamHashmarks : x.IsBehindRedTeamHashmarks));
                    }
                    else
                        _isIcingPossible[stick.Player.Team.Value] = new IcingObject();

                    // High stick logic.
                    if (IsHighStickEnabled(stick.Player.Team.Value) && puck &&
                        !Codebase.PlayerFunc.IsGoalie(stick.Player) &&
                        Codebase.PlayerFunc.GetPlayerSteamIdInPossession(ServerConfig.MinPossessionMilliseconds, _playersCurrentPuckTouch, false) != currentPlayerSteamId &&
                        puck.Rigidbody.transform.position.y > ServerConfig.HighStick.MaxHeight + (stick.Player.PlayerBody.Rigidbody.transform.position.y < 0 ? 0 : stick.Player.PlayerBody.Rigidbody.transform.position.y)) {
                        if (!_noHighStickFrames.TryGetValue(currentPlayerSteamId, out int noHighStickFrames)) {
                            noHighStickFrames = int.MaxValue;
                            _noHighStickFrames.Add(currentPlayerSteamId, noHighStickFrames);
                        }

                        if (noHighStickFrames >= ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate / ServerConfig.HighStick.Delta) {
                            _isHighStickActiveTimers.TryGetValue(stick.Player.Team.Value, out Timer highStickTimer);

                            highStickTimer.Change(ServerConfig.HighStick.MaxMilliseconds, Timeout.Infinite);
                            if (!IsHighStick(stick.Player.Team.Value)) {
                                _isHighStickActive[stick.Player.Team.Value] = true;
                                _puckLastStateBeforeCall[Rule.HighStick] = (puck.Rigidbody.transform.position, _puckZone);
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, stick.Player.Team.Value), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                                SendChat(Rule.HighStick, stick.Player.Team.Value, false);
                            }
                        }
                    }
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}", ServerConfig);
                }
            }
        }
        #endregion

        #region PlayerBodyV2_OnCollision
        /// <summary>
        /// Class that patches the OnCollisionEnter event from PlayerBodyV2.
        /// </summary>
        [HarmonyPatch(typeof(PlayerBodyV2), "OnCollisionEnter")]
        public class PlayerBodyV2_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || Paused || GameManager.Instance.Phase != GamePhase.Playing || !Logic)
                    return;

                try {
                    if (collision.gameObject.layer != LayerMask.NameToLayer("Player"))
                        return;

                    PlayerBodyV2 playerBody = SystemFunc.GetPlayerBodyV2(collision.gameObject);

                    if (!playerBody || !playerBody.Player || !playerBody.Player.IsCharacterFullySpawned)
                        return;

                    float force = Utils.GetCollisionForce(collision);

                    if (_lastForceOnGoalie != force) {
                        _lastForceOnGoalie = force;
                        _lastForceOnGoaliePlayerSteamId = playerBody.Player.SteamId.Value.ToString();
                        return;
                    }

                    Player lastPlayerHit = PlayerManager.Instance.GetPlayerBySteamId(_lastForceOnGoaliePlayerSteamId);
                    // If the goalie has been hit by the same team, return;
                    if (playerBody.Player.Team.Value == lastPlayerHit.Team.Value)
                        return;

                    Player goalie;
                    if (Codebase.PlayerFunc.IsGoalie(playerBody.Player))
                        goalie = playerBody.Player;
                    else if (Codebase.PlayerFunc.IsGoalie(lastPlayerHit))
                        goalie = lastPlayerHit;
                    else {
                        _lastForceOnGoaliePlayerSteamId = "";
                        _lastForceOnGoalie = 0;
                        return;
                    }

                    (double startX, double endX) = (0, 0);
                    (double startZ, double endZ) = (0, 0);
                    if (goalie.Team.Value == PlayerTeam.Blue) {
                        (startX, endX) = ZoneFunc.ICE_X_POSITIONS[IceElement.BlueTeam_BluePaint];
                        (startZ, endZ) = ZoneFunc.ICE_Z_POSITIONS[IceElement.BlueTeam_BluePaint];
                    }
                    else {
                        (startX, endX) = ZoneFunc.ICE_X_POSITIONS[IceElement.RedTeam_BluePaint];
                        (startZ, endZ) = ZoneFunc.ICE_Z_POSITIONS[IceElement.RedTeam_BluePaint];
                    }

                    bool goalieIsInHisCrease = true;
                    if (goalie.PlayerBody.Rigidbody.transform.position.x - ServerConfig.GInt.GoalieRadius < startX ||
                        goalie.PlayerBody.Rigidbody.transform.position.x + ServerConfig.GInt.GoalieRadius > endX ||
                        goalie.PlayerBody.Rigidbody.transform.position.z - ServerConfig.GInt.GoalieRadius < startZ ||
                        goalie.PlayerBody.Rigidbody.transform.position.z + ServerConfig.GInt.GoalieRadius > endZ) {
                        goalieIsInHisCrease = false;
                    }

                    PlayerTeam goalieOtherTeam = TeamFunc.GetOtherTeam(goalie.Team.Value);

                    bool hasGoalieDived;
                    if (_dives.TryGetValue(goalie.SteamId.Value.ToString(), out DateTime dateTime) && dateTime > DateTime.UtcNow)
                        hasGoalieDived = true;
                    else
                        hasGoalieDived = false;

                    bool goalieDown = (goalie.PlayerBody.HasFallen || goalie.PlayerBody.HasSlipped) && !hasGoalieDived;
                    _lastGoalieStateCollision[goalieOtherTeam] = goalieDown;

                    if (goalieDown || (force > ServerConfig.GInt.CollisionForceThreshold && goalieIsInHisCrease)) {
                        _ = _goalieIntTimer.TryGetValue(goalieOtherTeam, out Stopwatch watch);

                        if (watch == null) {
                            watch = new Stopwatch();
                            _goalieIntTimer[goalieOtherTeam] = watch;
                        }

                        watch.Restart();
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
        /// Class that patches the Server_SetPhase event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_SetPhase))]
        public class GameManager_Server_SetPhase_Patch {
            [HarmonyPrefix]
            public static bool Prefix(GamePhase phase, ref int time) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !Logic)
                        return true;

                    if (Paused) {
                        GameManager.Instance.Server_Resume();
                        ChangedPhase = false;
                    }

                    Paused = false;
                    _doFaceoff = false;

                    if (phase == GamePhase.PeriodOver || phase == GamePhase.BlueScore || phase == GamePhase.RedScore) {
                        NextFaceoffSpot = FaceoffSpot.Center;
                        _lastStoppageReason = Rule.None;

                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastIcing.Keys))
                            _lastIcing[key] = int.MaxValue;

                        foreach (PlayerTeam key in new List<PlayerTeam>(_icingStaminaDrainPenaltyAmount.Keys))
                            _icingStaminaDrainPenaltyAmount[key] = 0;

                        NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                    }
                    else if (phase == GamePhase.FaceOff || phase == GamePhase.Warmup || phase == GamePhase.GameOver) {
                        if (phase == GamePhase.GameOver || phase == GamePhase.Warmup) {
                            NextFaceoffSpot = FaceoffSpot.Center;
                            _lastStoppageReason = Rule.None;

                            foreach (PlayerTeam key in new List<PlayerTeam>(_lastIcing.Keys))
                                _lastIcing[key] = int.MaxValue;

                            foreach (PlayerTeam key in new List<PlayerTeam>(_icingStaminaDrainPenaltyAmount.Keys))
                                _icingStaminaDrainPenaltyAmount[key] = 0;
                        }
                        else if (phase == GamePhase.FaceOff)
                            _boundaryManager?.ActivateBoundaries();

                        // Reset players zone.
                        _playersZone.Clear();

                        // Reset possession times.
                        foreach (Stopwatch watch in _playersLastTimePuckPossession.Values)
                            watch.Stop();
                        _playersLastTimePuckPossession.Clear();

                        // Reset puck collision stay or exit times.
                        foreach (Stopwatch watch in _lastTimeOnCollisionStayOrExitWasCalled.Values)
                            watch.Stop();
                        _lastTimeOnCollisionStayOrExitWasCalled.Clear();

                        // Reset tipped times.
                        foreach (Stopwatch watch in _playersCurrentPuckTouch.Values)
                            watch.Stop();
                        _playersCurrentPuckTouch.Clear();

                        // Reset puck rule states.
                        foreach (Rule key in new List<Rule>(_puckLastStateBeforeCall.Keys))
                            _puckLastStateBeforeCall[key] = (Vector3.zero, ZoneFunc.DEFAULT_ZONE);

                        // Reset dives.
                        _dives.Clear();

                        ResetOffsides();
                        ResetHighSticks();
                        ResetIcings();
                        _dictPlayersPositionsForIcing.Clear();
                        ResetGoalieInt();

                        _puckZone = ZoneFunc.GetZone(NextFaceoffSpot);
                        _puckZoneLastTouched = _puckZone;

                        _lastPlayerOnPuckTeam = TeamFunc.DEFAULT_TEAM;
                        _lastPlayerOnPuckTeamTipIncluded = TeamFunc.DEFAULT_TEAM;
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckSteamId.Keys))
                            _lastPlayerOnPuckSteamId[key] = "";

                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
                            _lastPlayerOnPuckTipIncludedSteamId[key] = "";

                        _playersOnPuckTipIncludedDateTime.Clear();

                        NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                    }
                    else if (phase == GamePhase.Playing) {
                        if (time == -1 && ServerConfig.Faceoff.ReAdd1SecondAfterFaceoff)
                            time = SystemFunc.GetPrivateField<int>(typeof(GameManager), GameManager.Instance, "remainingPlayTime") + 1;

                        _boundaryManager?.DeactivateBoundaries();
                    }

                    if (!ChangedPhase)
                        return true;

                    if (phase == GamePhase.Playing) {
                        ChangedPhase = false;
                        if (ServerConfig.Faceoff.ReAdd1SecondAfterFaceoff)
                            time = _periodTimeRemaining + 1;

                        IcingStaminaDrain();
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(GameManager_Server_SetPhase_Patch)} Prefix().\n{ex}", ServerConfig);
                }

                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(GamePhase phase, int time) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !Logic)
                        return;

                    if (phase == GamePhase.FaceOff) {
                        if (NextFaceoffSpot == FaceoffSpot.Center || !ServerConfig.Faceoff.UseCustomFaceoff)
                            return;

                        Vector3 dot = Faceoff.GetFaceoffDot(NextFaceoffSpot);

                        List<Player> players = PlayerManager.Instance.GetPlayers();
                        foreach (Player player in players)
                            PlayerFunc.TeleportOnFaceoff(player, dot, NextFaceoffSpot);

                        return;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(GameManager_Server_SetPhase_Patch)} Postfix().\n{ex}", ServerConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the Server_SpawnPuck event from PuckManager.
        /// </summary>
        [HarmonyPatch(typeof(PuckManager), nameof(PuckManager.Server_SpawnPuck))]
        public class PuckManager_Server_SpawnPuck_Patch {
            [HarmonyPrefix]
            public static bool Prefix(ref Vector3 position, Quaternion rotation, Vector3 velocity, bool isReplay) {
                try {
                    // If this is not the server or this is a replay or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || isReplay || !ServerConfig.Faceoff.UseCustomFaceoff || (GameManager.Instance.Phase != GamePhase.Playing && GameManager.Instance.Phase != GamePhase.FaceOff) || !Logic)
                        return true;

                    Vector3 dot = Faceoff.GetFaceoffDot(NextFaceoffSpot);

                    if (ServerConfig.Faceoff.UseDefaultPuckDropHeight)
                        position = new Vector3(dot.x, position.y, dot.z);
                    else
                        position = new Vector3(dot.x, ServerConfig.Faceoff.PuckDropHeight, dot.z);
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Prefix().\n{ex}", ServerConfig);
                }

                return true;
            }
        }

        /*/// <summary>
        /// Class that patches the Update event from PlayerInput.
        /// </summary>
        [HarmonyPatch(typeof(PlayerInput), "Update")]
        public class PlayerInput_Update_Patch {
            [HarmonyPrefix]
            public static bool Prefix() {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return true;

                    UIChat chat = UIChat.Instance;

                    if (chat.IsFocused)
                        return true;

                    if (_getStickLocation.WasPressedThisFrame()) {
                        Logging.Log($"Stick position : {PlayerManager.Instance.GetLocalPlayer().Stick.BladeHandlePosition}", ClientConfig);
                    }
                        
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PlayerInput_Update_Patch Prefix().\n{ex}", ClientConfig);
                }

                return true;
            }
        }*/

        /// <summary>
        /// Class that patches the Client_SendClientChatMessage event from UIChat.
        /// </summary>
        [HarmonyPatch(typeof(UIChat), nameof(UIChat.Client_SendClientChatMessage))]
        public class UIChat_Client_SendClientChatMessage_Patch {
            [HarmonyPrefix]
            public static bool Prefix(string message, bool useTeamChat) {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return true;
                    
                    if (message.StartsWith(@"/")) {
                        message = message.ToLowerInvariant();

                        if (message.StartsWith(@"/refscale")) {
                            message = message.Replace(@"/refscale", "").Trim();

                            if (string.IsNullOrEmpty(message))
                                UIChat.Instance.AddChatMessage($"Ref scale is currently at {ClientConfig.TwoDRefsScale.ToString(CultureInfo.InvariantCulture)}");
                            else {
                                if (float.TryParse(message, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float scale)) {
                                    if (scale > 2f)
                                        scale = 2f;
                                    else if (scale < 0)
                                        scale = 0;

                                    ClientConfig.TwoDRefsScale = scale;
                                    ClientConfig.Save();
                                    _refSignalsBlueTeam?.Change2DRefsScale(ClientConfig.TwoDRefsScale);
                                    _refSignalsRedTeam?.Change2DRefsScale(ClientConfig.TwoDRefsScale);
                                    UIChat.Instance.AddChatMessage($"Adjusted client 2D refs scale to {scale.ToString(CultureInfo.InvariantCulture)}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIChat_Client_SendClientChatMessage_Patch Prefix().\n{ex}", ServerConfig);
                }

                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(string message, bool useTeamChat) {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return;

                    if (message.StartsWith(@"/")) {
                        message = message.ToLowerInvariant();

                        if (message.StartsWith(@"/help"))
                            UIChat.Instance.AddChatMessage("Ruleset commands:\n* <b>/refscale</b> - Change the scale of the 2D refs images (0.0-2.0)\n");
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIChat_Client_SendClientChatMessage_Patch Postfix().\n{ex}", ServerConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the Update event from ServerManager.
        /// </summary>
        [HarmonyPatch(typeof(ServerManager), "Update")]
        public class ServerManager_Update_Patch {
            [HarmonyPrefix]
            public static bool Prefix() {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null)
                    return true;

                if (SystemChatMessages.Count != 0) { // TODO : This don't work, pls fix.
                    List<string> systemChatMessages = new List<string>(SystemChatMessages);
                    SystemChatMessages.Clear();

                    foreach (string message in SystemChatMessages)
                        UIChat.Instance.Server_SendSystemChatMessage(message);
                }

                if (GameManager.Instance.Phase != GamePhase.Playing || !Logic)
                    return true;

                Puck puck = null;
                List<Player> players = null;
                Zone oldZone = ZoneFunc.DEFAULT_ZONE;
                Dictionary<PlayerTeam, bool?> icingHasToBeWarned = new Dictionary<PlayerTeam, bool?> {
                    {PlayerTeam.Blue, null},
                    {PlayerTeam.Red, null},
                };

                try {
                    // Check if high stick has been called by an event that cannot call it off by itself.
                    foreach (PlayerTeam callHighStickTeam in new List<PlayerTeam>(_callHighStickNextFrame.Keys)) {
                        if (!_callHighStickNextFrame[callHighStickTeam])
                            continue;
                        /*_callOffHighStickNextFrame[callOffHighStickTeam] = false;
                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, callOffHighStickTeam), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        SendChat(Rule.HighStick, callOffHighStickTeam, true, true);*/

                        NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(callHighStickTeam, false, _puckLastStateBeforeCall[Rule.HighStick]);
                        SendChat(Rule.HighStick, callHighStickTeam, true);
                        ResetHighSticks();

                        _lastStoppageReason = Rule.HighStick;
                        DoFaceoff(RefSignals.GetSignalConstant(true, callHighStickTeam), RefSignals.HIGHSTICK_REF);
                        break;
                    }

                    // If game was paused by the mod, don't do anything if faceoff hasn't being set yet.
                    if (Paused && !_doFaceoff)
                        return true;

                    // Unpause game and set faceoff.
                    if (_doFaceoff)
                        PostDoFaceoff();

                    players = PlayerManager.Instance.GetPlayers();
                    puck = PuckManager.Instance.GetPuck();

                    if (players.Count == 0 || puck == null || !puck || Paused)
                        return true;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 1.\n{ex}", ServerConfig);
                }

                try {
                    oldZone = _puckZone;
                    _puckZone = ZoneFunc.GetZone(puck.Rigidbody.transform.position, _puckZone, PuckRadius);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 2.\n{ex}", ServerConfig);
                }

                Dictionary<PlayerTeam, bool> isTeamOffside = new Dictionary<PlayerTeam, bool> {
                    { PlayerTeam.Blue, IsOffside(PlayerTeam.Blue) },
                    { PlayerTeam.Red, IsOffside(PlayerTeam.Red) },
                };

                try {
                    string playerWithPossessionSteamId = Codebase.PlayerFunc.GetPlayerSteamIdInPossession(ServerConfig.MinPossessionMilliseconds, _playersCurrentPuckTouch);

                    if (!string.IsNullOrEmpty(playerWithPossessionSteamId)) {
                        if (!_playersLastTimePuckPossession.TryGetValue(playerWithPossessionSteamId, out Stopwatch watch)) {
                            watch = new Stopwatch();
                            watch.Start();
                            _playersLastTimePuckPossession.Add(playerWithPossessionSteamId, watch);
                        }

                        watch.Restart();
                    }

                    _dictPlayersPositionsForIcing.Clear();
                    foreach (Player player in players) {
                        if (!Codebase.PlayerFunc.IsPlayerPlaying(player))
                            continue;

                        string playerSteamId = player.SteamId.Value.ToString();

                        if (!_isOffside.TryGetValue(playerSteamId, out _))
                            _isOffside.Add(playerSteamId, (player.Team.Value, false));

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

                        Zone playerZone = ZoneFunc.GetZone(player.PlayerBody.transform.position, oldPlayerZone, Codebase.Constants.PLAYER_RADIUS);
                        _playersZone[playerSteamId] = (player.Team.Value, playerZone);

                        PlayerTeam otherTeam = TeamFunc.GetOtherTeam(player.Team.Value);
                        List<Zone> otherTeamZones = ZoneFunc.GetTeamZones(otherTeam);

                        // Is offside.
                        bool isPlayerTeamOffside = isTeamOffside[player.Team.Value];
                        if ((playerWithPossessionSteamId != playerSteamId || isPlayerTeamOffside) && (playerZone == otherTeamZones[0] || playerZone == otherTeamZones[1])) {
                            if ((_puckZone != otherTeamZones[0] && _puckZone != otherTeamZones[1]) || isPlayerTeamOffside)
                                _isOffside[playerSteamId] = (player.Team.Value, true);
                        }

                        // Is not offside.
                        if (playerZone != otherTeamZones[0] && playerZone != otherTeamZones[1])
                            _isOffside[playerSteamId] = (player.Team.Value, false);

                        // Deferred icing logic.
                        if (!Codebase.PlayerFunc.IsGoalie(player)) {
                            bool isPlayerBehindBlueTeamHashmarks = false, isPlayerBehindRedTeamHashmarks = false, considerForIcing = false;
                            if (ZoneFunc.IsBehindHashmarks(otherTeam, player.PlayerBody.transform.position, Codebase.Constants.PLAYER_RADIUS)) {
                                if (otherTeam == PlayerTeam.Blue)
                                    isPlayerBehindBlueTeamHashmarks = true;
                                else
                                    isPlayerBehindRedTeamHashmarks = true;

                                if (IsIcing(player.Team.Value) && AreBothNegativeOrPositive(player.PlayerBody.transform.position.x, puck.Rigidbody.transform.position.x))
                                    considerForIcing = true;
                            }
                            else if (ZoneFunc.IsBehindHashmarks(player.Team.Value, player.PlayerBody.transform.position, Codebase.Constants.PLAYER_RADIUS)) {
                                if (player.Team.Value == PlayerTeam.Blue)
                                    isPlayerBehindBlueTeamHashmarks = true;
                                else
                                    isPlayerBehindRedTeamHashmarks = true;

                                if (IsIcing(otherTeam) && AreBothNegativeOrPositive(player.PlayerBody.transform.position.x, puck.Rigidbody.transform.position.x)) {
                                    considerForIcing = true;
                                    NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(otherTeam, true, _puckLastStateBeforeCall[Rule.Icing]);
                                }
                            }

                            _dictPlayersPositionsForIcing.Add(new PlayerIcing {
                                Player = player,
                                X = Math.Abs(player.PlayerBody.transform.position.x),
                                Z = Math.Abs(player.PlayerBody.transform.position.z),
                                IsBehindBlueTeamHashmarks = isPlayerBehindBlueTeamHashmarks,
                                IsBehindRedTeamHashmarks = isPlayerBehindRedTeamHashmarks,
                                ConsiderForIcing = considerForIcing,
                            });
                        }
                    }

                    // Remove offside if the other team entered their zone with the puck.
                    if (_lastPlayerOnPuckTeamTipIncluded == _lastPlayerOnPuckTeam) {
                        List<Zone> lastPlayerOnPuckTeamZones = ZoneFunc.GetTeamZones(_lastPlayerOnPuckTeam, true);
                        if (oldZone == lastPlayerOnPuckTeamZones[2] && _puckZone == lastPlayerOnPuckTeamZones[0]) {
                            PlayerTeam lastPlayerOnPuckOtherTeam = TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam);
                            foreach (string key in new List<string>(_isOffside.Keys)) {
                                if (_isOffside[key].Team == lastPlayerOnPuckOtherTeam)
                                    _isOffside[key] = (lastPlayerOnPuckOtherTeam, false);
                            }
                        }
                    }

                    PlayerTeam puckTeamZone;
                    if (puck.Rigidbody.transform.position.z > 0)
                        puckTeamZone = PlayerTeam.Blue;
                    else
                        puckTeamZone = PlayerTeam.Red;

                    // Deferred icing logic.
                    if (ServerConfig.Icing.Deferred && _dictPlayersPositionsForIcing.Any(x => x.ConsiderForIcing && (puckTeamZone == PlayerTeam.Blue ? x.IsBehindBlueTeamHashmarks : x.IsBehindRedTeamHashmarks))) {
                        PlayerIcing closestPlayerToEndBoardBlueTeam;
                        PlayerIcing closestPlayerToEndBoardRedTeam;

                        if (puckTeamZone == PlayerTeam.Blue) {
                            closestPlayerToEndBoardBlueTeam = _dictPlayersPositionsForIcing.Where(x => x.ConsiderForIcing && x.Player.Team.Value == PlayerTeam.Blue && x.IsBehindBlueTeamHashmarks).OrderByDescending(x => x.Z).FirstOrDefault();
                            closestPlayerToEndBoardRedTeam = _dictPlayersPositionsForIcing.Where(x => x.ConsiderForIcing && x.Player.Team.Value == PlayerTeam.Red && x.IsBehindBlueTeamHashmarks).OrderByDescending(x => x.Z).FirstOrDefault();
                        }
                        else {
                            closestPlayerToEndBoardBlueTeam = _dictPlayersPositionsForIcing.Where(x => x.ConsiderForIcing && x.Player.Team.Value == PlayerTeam.Blue && x.IsBehindRedTeamHashmarks).OrderByDescending(x => x.Z).FirstOrDefault();
                            closestPlayerToEndBoardRedTeam = _dictPlayersPositionsForIcing.Where(x => x.ConsiderForIcing && x.Player.Team.Value == PlayerTeam.Red && x.IsBehindRedTeamHashmarks).OrderByDescending(x => x.Z).FirstOrDefault();
                        }

                        Player closestPlayerToEndBoard = null;

                        if (closestPlayerToEndBoardBlueTeam != null && closestPlayerToEndBoardRedTeam == null)
                            closestPlayerToEndBoard = closestPlayerToEndBoardBlueTeam.Player;
                        else if (closestPlayerToEndBoardRedTeam != null && closestPlayerToEndBoardBlueTeam == null)
                            closestPlayerToEndBoard = closestPlayerToEndBoardRedTeam.Player;
                        else if (closestPlayerToEndBoardBlueTeam != null && closestPlayerToEndBoardRedTeam != null) {
                            if (Math.Abs(closestPlayerToEndBoardBlueTeam.Z - closestPlayerToEndBoardRedTeam.Z) < 8f) { // Check distance with x and z coordinates.
                                float puckXCoordinate = Math.Abs(puck.Rigidbody.transform.position.x);
                                float puckZCoordinate = Math.Abs(puck.Rigidbody.transform.position.z);

                                double blueTeamPlayerDistanceToPuck = GetDistance(puckXCoordinate, puckZCoordinate, closestPlayerToEndBoardBlueTeam.X, closestPlayerToEndBoardBlueTeam.Z);
                                double redTeamPlayerDistanceToPuck = GetDistance(puckXCoordinate, puckZCoordinate, closestPlayerToEndBoardRedTeam.X, closestPlayerToEndBoardRedTeam.Z);

                                if (blueTeamPlayerDistanceToPuck < redTeamPlayerDistanceToPuck && closestPlayerToEndBoardBlueTeam.IsBehindHashmarks)
                                    closestPlayerToEndBoard = closestPlayerToEndBoardBlueTeam.Player;
                                else if (redTeamPlayerDistanceToPuck < blueTeamPlayerDistanceToPuck && closestPlayerToEndBoardRedTeam.IsBehindHashmarks)
                                    closestPlayerToEndBoard = closestPlayerToEndBoardRedTeam.Player;
                            }
                            else { // Take closest player with z coordinates.
                                if (closestPlayerToEndBoardBlueTeam.Z > closestPlayerToEndBoardRedTeam.Z && closestPlayerToEndBoardBlueTeam.IsBehindHashmarks)
                                    closestPlayerToEndBoard = closestPlayerToEndBoardBlueTeam.Player;
                                else if (closestPlayerToEndBoardRedTeam.Z > closestPlayerToEndBoardBlueTeam.Z && closestPlayerToEndBoardRedTeam.IsBehindHashmarks)
                                    closestPlayerToEndBoard = closestPlayerToEndBoardRedTeam.Player;
                            }
                        }

                        if (closestPlayerToEndBoard != null) {
                            PlayerTeam closestPlayerToEndBoardOtherTeam = TeamFunc.GetOtherTeam(closestPlayerToEndBoard.Team.Value);
                            if (IsIcing(closestPlayerToEndBoard.Team.Value)) {
                                if (icingHasToBeWarned[closestPlayerToEndBoard.Team.Value] == null) {
                                    NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, closestPlayerToEndBoard.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send stop icing signal for client-side UI
                                    SendChat(Rule.Icing, closestPlayerToEndBoard.Team.Value, true, true);
                                }
                                else
                                    icingHasToBeWarned[closestPlayerToEndBoard.Team.Value] = false;
                                ResetIcings();
                            }
                            else if (IsIcing(closestPlayerToEndBoardOtherTeam)) {
                                SendChat(Rule.Icing, closestPlayerToEndBoardOtherTeam, true);

                                int remainingPlayTime = GameManager.Instance.GameState.Value.Time;
                                if (_lastStoppageReason == Rule.Icing && _lastIcing[closestPlayerToEndBoard.Team.Value] > _lastIcing[closestPlayerToEndBoardOtherTeam] && _lastIcing[closestPlayerToEndBoardOtherTeam] - remainingPlayTime <= ServerConfig.Icing.StaminaDrainDivisionAmountPenaltyTime)
                                    _icingStaminaDrainPenaltyAmount[closestPlayerToEndBoardOtherTeam] += 1;
                                else
                                    _icingStaminaDrainPenaltyAmount[closestPlayerToEndBoardOtherTeam] = 0;

                                _lastStoppageReason = Rule.Icing;
                                _lastIcing[closestPlayerToEndBoardOtherTeam] = remainingPlayTime;
                                DoFaceoff();
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 3.\n{ex}", ServerConfig);
                }

                try {
                    // Icing logic.
                    ServerManager_Update_IcingLogic(PlayerTeam.Blue, puck, icingHasToBeWarned, _dictPlayersPositionsForIcing.Any(x => x.IsBehindRedTeamHashmarks));
                    ServerManager_Update_IcingLogic(PlayerTeam.Red, puck, icingHasToBeWarned, _dictPlayersPositionsForIcing.Any(x => x.IsBehindBlueTeamHashmarks));
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 4.\n{ex}", ServerConfig);
                }

                try {
                    // Warn icings.
                    foreach (var kvp in icingHasToBeWarned) {
                        if (kvp.Value != null && (bool)kvp.Value) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, kvp.Key), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send show icing signal for client-side UI.
                            SendChat(Rule.Icing, kvp.Key, false);
                            break;
                        }
                    }

                    // Warn or call off offsides.
                    foreach (var kvp in isTeamOffside) {
                        if (!kvp.Value && IsOffside(kvp.Key))
                            WarnOffside(true, kvp.Key);
                        else if (kvp.Value && !IsOffside(kvp.Key))
                            WarnOffside(false, kvp.Key);
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 5.\n{ex}", ServerConfig);
                }

                try {
                    _isPuckBehindHashmarks[PlayerTeam.Blue] = ZoneFunc.IsBehindHashmarks(PlayerTeam.Blue, puck.Rigidbody.transform.position, PuckRadius);
                    _isPuckBehindHashmarks[PlayerTeam.Red] = ZoneFunc.IsBehindHashmarks(PlayerTeam.Red, puck.Rigidbody.transform.position, PuckRadius);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 6.\n{ex}", ServerConfig);
                }

                try {
                    foreach (string playerSteamId in new List<string>(_noHighStickFrames.Keys))
                        _noHighStickFrames[playerSteamId] += 1;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 7.\n{ex}", ServerConfig);
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the Event_Server_OnPuckEnterTeamGoal event from GameManagerController.
        /// </summary>
        [HarmonyPatch(typeof(GameManagerController), "Event_Server_OnPuckEnterTeamGoal")]
        public class GameManagerController_Event_Server_OnPuckEnterTeamGoal_Patch {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing || !Logic)
                        return true;

                    if (Paused)
                        return false;

                    NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);

                    NextFaceoffSpot = FaceoffSpot.Center;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManagerController_Event_Server_OnPuckEnterTeamGoal_Patch Prefix().\n{ex}", ServerConfig);
                }

                return true;
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
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !Logic)
                        return true;

                    bool isGoalieInt = IsGoalieInt(team);

                    if (goalPlayer != null) {
                        // No goal if offside or high stick or goalie interference.
                        bool isOffside = false, isHighStick = false;
                        isOffside = IsOffside(team);
                        isHighStick = IsHighStick(team);

                        if (isOffside || isHighStick || isGoalieInt) {
                            if (isOffside) {
                                NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.Offside]);
                                SendChat(Rule.Offside, team, true, false);
                                _lastStoppageReason = Rule.Offside;
                                DoFaceoff();
                            }
                            else if (isHighStick) {
                                NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.HighStick]);
                                SendChat(Rule.HighStick, team, true, false);
                                _lastStoppageReason = Rule.HighStick;
                                DoFaceoff(RefSignals.GetSignalConstant(true, team), RefSignals.HIGHSTICK_REF);
                            }
                            else if (isGoalieInt) {
                                NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.GoalieInt]);
                                SendChat(Rule.GoalieInt, team, true, false);
                                _lastStoppageReason = Rule.GoalieInt;
                                DoFaceoff(RefSignals.GetSignalConstant(true, team), RefSignals.INTERFERENCE_REF);
                            }
                            return false;
                        }

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
                        SendSOGDuringGoal(goalPlayer);
                        return true;
                    }

                    if (isGoalieInt) {
                        NextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.GoalieInt]);
                        SendChat(Rule.GoalieInt, team, true, false);
                        _lastStoppageReason = Rule.GoalieInt;
                        DoFaceoff(RefSignals.GetSignalConstant(true, team), RefSignals.INTERFERENCE_REF);
                        return false;
                    }

                    // If own goal, add goal attribution to last player on puck on the other team.
                    Player ownGoalPlayer = PlayerManager.Instance.GetPlayerBySteamId(_lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(team)]);
                    UIChat.Instance.Server_SendSystemChatMessage($"OWN GOAL BY #{ownGoalPlayer.Number.Value} {ownGoalPlayer.Username.Value}");
                    goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team]).FirstOrDefault();

                    if (goalPlayer != null) {
                        lastPlayer = goalPlayer;
                        SendSOGDuringGoal(goalPlayer);
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_GoalScored_Patch Prefix().\n{ex}", ServerConfig);
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the Server_RespawnCharacter event from Player.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Server_RespawnCharacter))]
        public class Player_Server_RespawnCharacter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Vector3 position, Quaternion rotation, PlayerRole role) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !Logic)
                        return;

                    // If this game is not started or faceoff is on the default dot (center), do not use the patch.
                    if (GameManager.Instance.Phase != GamePhase.FaceOff || NextFaceoffSpot == FaceoffSpot.Center || !ServerConfig.Faceoff.UseCustomFaceoff)
                        return;

                    Player player = PlayerManager.Instance.GetPlayers()
                        .Where(x =>
                            Codebase.PlayerFunc.IsPlayerPlaying(x) && x.PlayerBody != null &&
                            x.PlayerBody.transform.position.x == position.x &&
                            x.PlayerBody.transform.position.y == position.y &&
                            x.PlayerBody.transform.position.z == position.z).FirstOrDefault();

                    if (!player)
                        return;

                    // Reteleport player on faceoff to the correct faceoff.
                    PlayerFunc.TeleportOnFaceoff(player, Faceoff.GetFaceoffDot(NextFaceoffSpot), NextFaceoffSpot);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Player_Server_RespawnCharacter_Patch Postfix().\n{ex}", ServerConfig);
                }
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
                        //Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_SERVER_TO_CLIENT}.", ClientConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;

                        DateTime now = DateTime.UtcNow;
                        if (_lastDateTimeAskStartupData + TimeSpan.FromSeconds(1) < now && _askServerForStartupDataCount++ < 10) {
                            _lastDateTimeAskStartupData = now;
                            NetworkCommunication.SendData(Constants.ASK_SERVER_FOR_STARTUP_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, ClientConfig);
                        }
                    }
                    else if (_askForKick) {
                        _askForKick = false;
                        NetworkCommunication.SendData(Constants.MOD_NAME + "_kick", "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, ClientConfig);
                    }
                    else if (_addServerModVersionOutOfDateMessage) {
                        _addServerModVersionOutOfDateMessage = false;
                        UIChat.Instance.AddChatMessage($"Server's {Constants.WORKSHOP_MOD_NAME} mod is out of date. Some functionalities might not work properly.");
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIScoreboard_UpdateServer_Patch Postfix().\n{ex}", ClientConfig);
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

                    if (NextFaceoffSpot != FaceoffSpot.Center && Logic) {
                        foreach (Player player in PlayerManager.Instance.GetPlayers()) {
                            if (player.PlayerPosition && player.PlayerBody)
                                player.PlayerBody.Server_Teleport(player.PlayerPosition.transform.position, player.PlayerPosition.transform.rotation);
                        }

                        NextFaceoffSpot = FaceoffSpot.Center;
                    }

                    if (resetPhase) {
                        _lastStoppageReason = Rule.None;

                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastIcing.Keys))
                            _lastIcing[key] = int.MaxValue;

                        foreach (PlayerTeam key in new List<PlayerTeam>(_icingStaminaDrainPenaltyAmount.Keys))
                            _icingStaminaDrainPenaltyAmount[key] = 0;
                    }

                    _sentOutOfDateMessage.Clear();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(GameManager_Server_ResetGameState_Patch)} Postfix().\n{ex}", ServerConfig);
                }
            }
        }

        /// <summary>
        /// Class that patches the AddChatMessage event from UIChat.
        /// </summary>
        [HarmonyPatch(typeof(UIChat), nameof(UIChat.AddChatMessage))]
        public class UIChat_AddChatMessage_Patch {
            [HarmonyPrefix]
            public static bool Prefix(string message) {
                try {
                    // If this is the server or server doesn't use the mod, do not use the patch.
                    if (ServerFunc.IsDedicatedServer() || !_serverHasResponded)
                        return true;

                    if ((message.StartsWith("HIGH STICK") || message.StartsWith("OFFSIDE") || message.StartsWith("ICING")) && !message.EndsWith("CALLED"))
                        return false;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(UIChat_AddChatMessage_Patch)} Prefix().\n{ex}", ClientConfig);
                }

                return true;
            }
        }
        #endregion

        #region Methods/Functions
        private static void SendChat(Rule rule, PlayerTeam team, bool called, bool off = false) {
            string ruleStr = rule.GetDescription("ToString");
            if (string.IsNullOrEmpty(ruleStr))
                return;

            UIChat.Instance.Server_SendSystemChatMessage($"{ruleStr} {team.ToString().ToUpperInvariant()} TEAM" + (called ? (" CALLED" + (off ? " OFF" : "")) : ""));
        }

        private static void WarnOffside(bool active, PlayerTeam team) {
            if (!IsOffsideEnabled(team))
                return;

            if (active) {
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, team), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send show offside signal for client-side UI.
                SendChat(Rule.Offside, team, false);
            }
            else {
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, team), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send show offside signal for client-side UI.
                SendChat(Rule.Offside, team, true, true);
            }
        }

        private static void ResetIcings() {
            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingPossible.Keys))
                _isIcingPossible[key] = new IcingObject();

            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingActive.Keys))
                _isIcingActive[key] = false;

            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingActiveTimers.Keys))
                _isIcingActiveTimers[key].Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void ResetIcingCallback(object stateInfo) {
            PlayerTeam team = (PlayerTeam)stateInfo;
            _isIcingPossible[team] = new IcingObject();
        }

        private static void ResetHighStickCallback(object stateInfo) {
            PlayerTeam team = (PlayerTeam)stateInfo;
            if (!_isHighStickActive[team])
                return;

            _isHighStickActive[team] = false;
            _isHighStickActiveTimers[team].Change(Timeout.Infinite, Timeout.Infinite);
            _callHighStickNextFrame[team] = true;
        }

        private static void ResetGoalieInt() {
            foreach (PlayerTeam key in new List<PlayerTeam>(_goalieIntTimer.Keys))
                _goalieIntTimer[key] = null;

            foreach (PlayerTeam key in new List<PlayerTeam>(_lastGoalieStateCollision.Keys))
                _lastGoalieStateCollision[key] = false;

            _lastForceOnGoalie = 0;
            _lastForceOnGoaliePlayerSteamId = "";
        }

        private static void ResetOffsides() {
            _isOffside.Clear();
        }

        private static void ResetHighSticks() {
            foreach (PlayerTeam key in new List<PlayerTeam>(_isHighStickActiveTimers.Keys))
                _isHighStickActiveTimers[key].Change(Timeout.Infinite, Timeout.Infinite);

            foreach (PlayerTeam key in new List<PlayerTeam>(_isHighStickActive.Keys))
                _isHighStickActive[key] = false;

            foreach (PlayerTeam key in new List<PlayerTeam>(_callHighStickNextFrame.Keys))
                _callHighStickNextFrame[key] = false;

            _noHighStickFrames.Clear();
        }

        private static void DoFaceoff(string dataName = "", string dataStr = "", int millisecondsPauseMin = 3750, int millisecondsPauseMax = 6000, bool clearViolations = true) {
            if (Paused)
                return;

            if (clearViolations)
                _puckValidator?.ClearViolations();

            ResetIcings();
            ResetHighSticks();

            Paused = true;

            NetworkCommunication.SendDataToAll(SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(SoundsSystem.WHISTLE),
                Codebase.Constants.SOUNDS_FROM_SERVER_TO_CLIENT, ServerConfig);

            if (!string.IsNullOrEmpty(dataName) && !string.IsNullOrEmpty(dataStr)) {
                NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                NetworkCommunication.SendDataToAll(dataName, dataStr, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            }

            try {
                EventManager.Instance.TriggerEvent(Codebase.Constants.SOUNDS_MOD_NAME, new Dictionary<string, object> { { SoundsSystem.PLAY_SOUND, SoundsSystem.FACEOFF_MUSIC } });
                if (!NetworkCommunication.GetDataNamesToIgnore().Contains(SoundsSystem.PLAY_SOUND))
                    Logging.Log($"Sent data \"{SoundsSystem.PLAY_SOUND}\" ({SoundsSystem.FACEOFF_MUSIC}) to {Codebase.Constants.SOUNDS_MOD_NAME}.", ServerConfig);
            }
            catch (Exception ex) {
                Logging.LogError(ex.ToString(), ServerConfig);
            }

            _periodTimeRemaining = GameManager.Instance.GameState.Value.Time;
            GameManager.Instance.Server_Pause();

            _ = Task.Run(() => {
                Thread.Sleep(new System.Random().Next(millisecondsPauseMin, millisecondsPauseMax + 1));
                _doFaceoff = true;
            });
        }

        private static void PostDoFaceoff() {
            _doFaceoff = false;
            Paused = false;

            GameManager.Instance.Server_Resume();
            if (GameManager.Instance.GameState.Value.Phase != GamePhase.Playing)
                return;

            ChangedPhase = true;
            GameManager.Instance.Server_SetPhase(GamePhase.FaceOff,
                ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.phaseDurationMap[GamePhase.FaceOff]);
        }

        private static bool IsOffside(PlayerTeam team) {
            if (!IsOffsideEnabled(team))
                return false;

            return _isOffside.Where(x => x.Value.Team == team).Any(x => x.Value.IsOffside);
        }

        private static bool IsOffsideEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return ServerConfig.Offside.BlueTeam;
            else
                return ServerConfig.Offside.RedTeam;
        }

        private static bool IsHighStick(PlayerTeam team) {
            if (!IsHighStickEnabled(team) || !_isHighStickActive[team])
                return false;

            return true;
        }

        private static bool IsHighStickEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return ServerConfig.HighStick.BlueTeam;
            else
                return ServerConfig.HighStick.RedTeam;
        }

        private static bool IsIcing(PlayerTeam team) {
            if (!IsIcingEnabled(team))
                return false;

            return _isIcingActive[team];
        }

        private static bool IsIcingEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return ServerConfig.Icing.BlueTeam;
            else
                return ServerConfig.Icing.RedTeam;
        }

        private static bool IsIcingPossible(Puck puck, PlayerTeam team, bool checkPossibleTime = true) {
            IcingObject icingObj = _isIcingPossible[team];

            if (!IsIcingEnabled(team) || icingObj.Watch == null || icingObj.AnyPlayersBehindHashmarks || !puck || !puck.Rigidbody)
                return false;

            if (!checkPossibleTime)
                return true;
            else {
                float maxPossibleTime = ServerConfig.Icing.MaxPossibleTime[_puckZoneLastTouched] * icingObj.Delta;

                if (!icingObj.DeltaHasBeenChecked && ++icingObj.FrameCheck > ((float)ServerManager.Instance.ServerConfigurationManager.ServerConfiguration.serverTickRate)) {
                    icingObj.DeltaHasBeenChecked = true;

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(team);
                    List<Zone> otherTeamZones = ZoneFunc.GetTeamZones(otherTeam, true);
                    List<string> otherTeamPlayersSteamId = _playersZone.Where(x => x.Value.Team == otherTeam && x.Value.Zone == otherTeamZones[0]).Select(x => x.Key).ToList();

                    if (otherTeamPlayersSteamId.Count != 0 && puck.Rigidbody.transform.position.y < ServerConfig.Icing.DeferredMaxHeight) {
                        foreach (string playerSteamId in otherTeamPlayersSteamId) {
                            Player player = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                            if (player == null || !player || !player.IsCharacterFullySpawned)
                                continue;

                            float maxPossibleTimeLimit = ((float)((GetDistance(puck.Rigidbody.transform.position.x, puck.Rigidbody.transform.position.z, player.PlayerBody.transform.position.x, player.PlayerBody.transform.position.z) * ServerConfig.Icing.DeferredMaxPossibleTimeMultiplicator) + ServerConfig.Icing.DeferredMaxPossibleTimeAddition)) - (Math.Abs(player.PlayerBody.transform.position.z) * ServerConfig.Icing.DeferredMaxPossibleTimeDistanceDelta);
                            //Logging.Log($"Possible time is : {maxPossibleTime}. Limit is : {maxPossibleTimeLimit}. Puck Y is : {puck.Rigidbody.transform.position.y}.", ServerConfig, true);

                            if (maxPossibleTime >= maxPossibleTimeLimit) {
                                _isIcingPossible[team] = new IcingObject();
                                return false;
                            }
                        }
                    }
                }

                if (icingObj.Watch.ElapsedMilliseconds < maxPossibleTime)
                    return true;
            }

            return false;
        }

        private static bool IsGoalieInt(PlayerTeam team) {
            if (!IsGoalieIntEnabled(team))
                return false;
            
            Stopwatch watch = _goalieIntTimer[team];
            if (watch == null)
                return false;

            Logging.Log($"Goalie is down : {_lastGoalieStateCollision[team]}.", ServerConfig);
            Logging.Log($"Goalie was last touched : {((double)watch.ElapsedMilliseconds) / 1000d} seconds ago.", ServerConfig);
            if (_lastGoalieStateCollision[team])
                return watch.ElapsedMilliseconds < ServerConfig.GInt.HitNoGoalMilliseconds;

            return watch.ElapsedMilliseconds < ServerConfig.GInt.PushNoGoalMilliseconds;
        }

        private static bool IsGoalieIntEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return ServerConfig.GInt.BlueTeam;
            else
                return ServerConfig.GInt.RedTeam;
        }

        private static void ResetGoalAndAssistAttribution(PlayerTeam team) {
            try {
                NetworkList<NetworkObjectCollision> buffer = GetPuckBuffer() ?? throw new NullReferenceException("Buffer field is null !!!");

                List<NetworkObjectCollision> collisionToRemove = new List<NetworkObjectCollision>();
                foreach (NetworkObjectCollision collision in buffer) {
                    if (!collision.NetworkObjectReference.TryGet(out NetworkObject networkObject, null))
                        continue;

                    networkObject.TryGetComponent<PlayerBodyV2>(out PlayerBodyV2 playerBody);
                    if (playerBody && playerBody.Player.Team.Value == team)
                        collisionToRemove.Add(collision);
                    else {
                        networkObject.TryGetComponent<Stick>(out Stick stick);
                        if (stick && stick.Player.Team.Value == team)
                            collisionToRemove.Add(collision);
                    }
                }
                
                foreach (NetworkObjectCollision collision in collisionToRemove)
                    buffer.Remove(collision);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ResetAssists.\n{ex}", ServerConfig);
            }
        }

        private static NetworkList<NetworkObjectCollision> GetPuckBuffer(Puck puck = null) {
            if (puck == null) {
                puck = PuckManager.Instance.GetPuck();
                if (!puck)
                    return null;
            }

            return SystemFunc.GetPrivateField<NetworkList<NetworkObjectCollision>>(typeof(NetworkObjectCollisionBuffer), puck.NetworkObjectCollisionBuffer, "buffer");
        }

        private static void IcingStaminaDrain() {
            if (_lastStoppageReason != Rule.Icing || !ServerConfig.Icing.StaminaDrain)
                return;

            PlayerTeam icingTeam;
            if (_lastIcing[PlayerTeam.Red] - _lastIcing[PlayerTeam.Blue] < 0) // Red team has the last icing.
                icingTeam = PlayerTeam.Red;
            else
                icingTeam = PlayerTeam.Blue;

            float staminaDrainDivisionAmount = ServerConfig.Icing.StaminaDrainDivisionAmount;
            for (int i = 0; i < _icingStaminaDrainPenaltyAmount[icingTeam]; i++)
                staminaDrainDivisionAmount *= ServerConfig.Icing.StaminaDrainDivisionAmount - ServerConfig.Icing.StaminaDrainDivisionAmountPenaltyDelta;

            foreach (Player player in PlayerManager.Instance.GetPlayersByTeam(icingTeam)) {
                if (!Codebase.PlayerFunc.IsPlayerPlaying(player))
                    continue;

                if (Codebase.PlayerFunc.IsGoalie(player) && !ServerConfig.Icing.StaminaDrainGoalie)
                    continue;

                player.PlayerBody.Stamina = 1f / staminaDrainDivisionAmount;
            }
        }
        #endregion

        #region Events
        public static void Event_OnRulesetTrigger(Dictionary<string, object> message) {
            try {
                KeyValuePair<string, object> messageKvp = message.ElementAt(0);
                string value = (string)messageKvp.Value;
                if (!NetworkCommunication.GetDataNamesToIgnore().Contains(messageKvp.Key))
                    Logging.Log($"Received data {messageKvp.Key}. Content : {value}", ServerConfig);

                switch (messageKvp.Key) {
                    case Codebase.Constants.PAUSE:
                        _paused = bool.Parse(value);
                        break;

                    case Codebase.Constants.LOGIC:
                        Logic = bool.Parse(value);
                        break;

                    case "dive":
                        KeyValuePair<string, object> extraMessageKvp = message.ElementAt(1);
                        if (extraMessageKvp.Key != "duration")
                            break;

                        DateTime getUpTime = DateTime.UtcNow + TimeSpan.FromMilliseconds(int.Parse((string)extraMessageKvp.Value));
                        if (!_dives.TryGetValue(value, out DateTime _))
                            _dives.Add(value, getUpTime);
                        else
                            _dives[value] = getUpTime;

                        break;

                    case Codebase.Constants.INSTANT_FACEOFF:
                        NextFaceoffSpot = (FaceoffSpot)ushort.Parse(value);
                        DoFaceoff("", "", 0, 0, false);
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnRulesetTrigger.\n{ex}", ServerConfig);
            }
        }

        /// <summary>
        /// Method called when a scene has being loaded by Unity.
        /// Used to load assets.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_OnSceneLoaded(Dictionary<string, object> message) {
            try {
                // If this is the server, do not use the patch.
                if (ServerFunc.IsDedicatedServer())
                    return;

                Scene scene = (Scene)message["scene"];
                if (scene == null || scene.buildIndex != 2)
                    return;

                LoadAssets();
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnSceneLoaded.\n{ex}", ClientConfig);
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

            try {
                ServerConfig = new ServerConfig();

                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                if (_refSignalsBlueTeam == null && _refSignalsRedTeam == null)
                    return;

                if (_refSignalsBlueTeam != null) {
                    _refSignalsBlueTeam.StopAllSignals();
                    _refSignalsBlueTeam.DestroyGameObjects();
                    _refSignalsBlueTeam = null;
                }

                if (_refSignalsRedTeam != null) {
                    _refSignalsRedTeam.StopAllSignals();
                    _refSignalsRedTeam.DestroyGameObjects();
                    _refSignalsRedTeam = null;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStopped.\n{ex}", ClientConfig);
            }
        }
        
        public static void Event_OnPlayerRoleChanged(Dictionary<string, object> message) {
            // Use the event to link client Ids to Steam Ids.
            Dictionary<ulong, string> players_ClientId_SteamId_ToChange = new Dictionary<ulong, string>();
            foreach (var kvp in PlayerFunc.Players_ClientId_SteamId) {
                if (string.IsNullOrEmpty(kvp.Value))
                    players_ClientId_SteamId_ToChange.Add(kvp.Key, PlayerManager.Instance.GetPlayerByClientId(kvp.Key).SteamId.Value.ToString());
            }

            foreach (var kvp in players_ClientId_SteamId_ToChange) {
                if (!string.IsNullOrEmpty(kvp.Value)) {
                    PlayerFunc.Players_ClientId_SteamId[kvp.Key] = kvp.Value;
                    Logging.Log($"Added clientId {kvp.Key} linked to Steam Id {kvp.Value}.", ServerConfig);
                }
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

            try {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null && !_hasRegisteredWithNamedMessageHandler) {
                    Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT_TO_SERVER}.", ServerConfig);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER, ReceiveData);
                    _hasRegisteredWithNamedMessageHandler = true;
                }

                ulong clientId = (ulong)message["clientId"];
                string clientSteamId = PlayerManager.Instance.GetPlayerByClientId(clientId).SteamId.Value.ToString();
                try {
                    PlayerFunc.Players_ClientId_SteamId.Add(clientId, "");
                }
                catch {
                    PlayerFunc.Players_ClientId_SteamId.Remove(clientId);
                    PlayerFunc.Players_ClientId_SteamId.Add(clientId, "");
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

            try {
                ulong clientId = (ulong)message["clientId"];
                string clientSteamId;
                try {
                    clientSteamId = PlayerFunc.Players_ClientId_SteamId[clientId];
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(PlayerFunc.Players_ClientId_SteamId)}.", ServerConfig);
                    return;
                }

                _sentOutOfDateMessage.Remove(clientId);

                try {
                    var offsideValue = _isOffside[clientSteamId];
                    _isOffside.Remove(clientSteamId);
                    // Remove offside warning.
                    if (offsideValue.IsOffside && !IsOffside(offsideValue.Team))
                        WarnOffside(false, offsideValue.Team);
                }
                catch {
                    _isOffside.Remove(clientSteamId);
                }

                _playersZone.Remove(clientSteamId);
                _playersCurrentPuckTouch.Remove(clientSteamId);
                _playersLastTimePuckPossession.Remove(clientSteamId);
                _lastTimeOnCollisionStayOrExitWasCalled.Remove(clientSteamId);

                PlayerFunc.Players_ClientId_SteamId.Remove(clientId);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnClientDisconnected)}.\n{ex}", ServerConfig);
            }
        }

        private void Event_OnPlayerBodySpawned(Dictionary<string, object> message) {
            if (!ServerFunc.IsDedicatedServer())
                return;

            try {
                // Prevent the default freeze behavior during faceoffs.
                if (GameManager.Instance.GameState.Value.Phase == GamePhase.FaceOff) {
                    PlayerBodyV2 playerBody = (PlayerBodyV2)message["playerBody"];
                    _playerUnfreezer?.RegisterPlayer(playerBody);
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnPlayerBodySpawned)}.\n{ex}", ServerConfig);
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
                if (clientId == NetworkManager.ServerClientId) { // If client Id is 0, we received data from the server, so we are client-sided.
                    //Logging.Log("ReceiveData", ClientConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, ClientConfig);
                }
                else {
                    //Logging.Log("ReceiveData", ServerConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, ServerConfig);
                }

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

                    case RefSignals.SHOW_SIGNAL_BLUE: // CLIENT-SIDE : Show blue team ref signal in the UI.
                        if (_refSignalsBlueTeam == null)
                            break;

                        if (_refSignalsBlueTeam.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsBlueTeam.", ClientConfig);
                            foreach (string error in _refSignalsBlueTeam.Errors)
                                Logging.LogError(error, ClientConfig);
                        }
                        else {
                            if (ClientConfig.TeamColor2DRefs)
                                _refSignalsBlueTeam.ShowSignal(dataStr + "_" + RefSignals.BLUE);
                            else
                                _refSignalsBlueTeam.ShowSignal(dataStr);
                        }
                        break;

                    case RefSignals.STOP_SIGNAL_BLUE: // CLIENT-SIDE : Hide blue team ref signal in the UI.
                        StopBlueRefSignals(dataStr);
                        break;

                    case RefSignals.SHOW_SIGNAL_RED: // CLIENT-SIDE : Show red team ref signal in the UI.
                        if (_refSignalsRedTeam == null)
                            break;

                        if (_refSignalsRedTeam.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsRedTeam.", ClientConfig);
                            foreach (string error in _refSignalsRedTeam.Errors)
                                Logging.LogError(error, ClientConfig);
                        }
                        else {
                            if (ClientConfig.TeamColor2DRefs)
                                _refSignalsRedTeam.ShowSignal(dataStr + "_" + RefSignals.RED);
                            else
                                _refSignalsRedTeam.ShowSignal(dataStr);
                        }
                        break;

                    case RefSignals.STOP_SIGNAL_RED: // CLIENT-SIDE : Hide red team ref signal in the UI.
                        StopRedRefSignals(dataStr);
                        break;

                    case RefSignals.STOP_SIGNAL: // CLIENT-SIDE : Hide all ref signals in the UI.
                        StopBlueRefSignals(dataStr);
                        StopRedRefSignals(dataStr);
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

                    case Constants.ASK_SERVER_FOR_STARTUP_DATA: // SERVER-SIDE : Send the necessary data to client.
                        if (dataStr != "1")
                            break;

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        NetworkCommunication.SendData(SoundsSystem.LOAD_EXTRA_SOUNDS, Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sounds"),
                            clientId, Codebase.Constants.SOUNDS_FROM_SERVER_TO_CLIENT, ServerConfig);
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveData.\n{ex}", ServerConfig);
            }
        }

        private static void StopBlueRefSignals(string dataStr) {
            if (_refSignalsBlueTeam == null)
                return;

            if (_refSignalsBlueTeam.Errors.Count != 0) {
                Logging.LogError("There was an error when initializing _refSignalsBlueTeam.", ClientConfig);
                foreach (string error in _refSignalsBlueTeam.Errors)
                    Logging.LogError(error, ClientConfig);
            }
            else {
                if (dataStr == RefSignals.ALL)
                    _refSignalsBlueTeam.StopAllSignals();
                else if (ClientConfig.TeamColor2DRefs)
                    _refSignalsBlueTeam.StopSignal(dataStr + "_" + RefSignals.BLUE);
                else
                    _refSignalsBlueTeam.StopSignal(dataStr);
            }
        }

        private static void StopRedRefSignals(string dataStr) {
            if (_refSignalsRedTeam == null)
                return;

            if (_refSignalsRedTeam.Errors.Count != 0) {
                Logging.LogError("There was an error when initializing _refSignalsRedTeam.", ClientConfig);
                foreach (string error in _refSignalsRedTeam.Errors)
                    Logging.LogError(error, ClientConfig);
            }
            else {
                if (dataStr == RefSignals.ALL)
                    _refSignalsRedTeam.StopAllSignals();
                else if (ClientConfig.TeamColor2DRefs)
                    _refSignalsRedTeam.StopSignal(dataStr + "_" + RefSignals.RED);
                else
                    _refSignalsRedTeam.StopSignal(dataStr);
            }
        }

        private static void ServerManager_Update_IcingLogic(PlayerTeam team, Puck puck, Dictionary<PlayerTeam, bool?> icingHasToBeWarned, bool anyPlayersBehindHashmarks) {
            if (!IsIcingEnabled(team))
                return;

            PlayerTeam otherTeam = TeamFunc.GetOtherTeam(team);

            if (!IsIcingPossible(puck, team, false) && _isIcingActive[team]) {
                _isIcingActive[team] = false;
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, team), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, ServerConfig); // Send stop icing signal for client-side UI.
                SendChat(Rule.Icing, team, true, true);
            }
            else if (!_isIcingActive[team] && IsIcingPossible(puck, team) && _puckZone == ZoneFunc.GetTeamZones(otherTeam)[1]) {
                _puckLastStateBeforeCall[Rule.Icing] = (puck.Rigidbody.transform.position, _puckZone);
                _isIcingActiveTimers[team].Change(ServerConfig.Icing.MaxActiveTime, Timeout.Infinite);
                icingHasToBeWarned[team] = true;
                _isIcingActive[team] = true;
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
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null) {
                        Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT_TO_SERVER}.", ServerConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;
                    }

                    Logging.Log("Setting server sided config.", ServerConfig, true);
                    ServerConfig = ServerConfig.ReadConfig();
                }
                else {
                    Logging.Log("Setting client sided config.", ServerConfig, true);
                    ClientConfig = ClientConfig.ReadConfig();

                    //_getStickLocation = new InputAction(binding: "<keyboard>/#(o)");
                    //_getStickLocation.Enable();
                }

                Logging.Log("Subscribing to events.", ServerConfig, true);
                
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    EventManager.Instance.AddEventListener(Codebase.Constants.RULESET_MOD_NAME, Event_OnRulesetTrigger);
                    EventManager.Instance.AddEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);

                    if (ServerConfig.Faceoff.EnableViolations) {
                        // Create boundary manager
                        GameObject boundaryManagerObj = new GameObject("FaceOffBoundaryManager");
                        _boundaryManager = boundaryManagerObj.AddComponent<FaceOffBoundaryManager>();
                        UnityEngine.Object.DontDestroyOnLoad(boundaryManagerObj);

                        // Create player unfreezer/tether system
                        GameObject playerUnfreezerObj = new GameObject("FaceOffPlayerUnfreezer");
                        _playerUnfreezer = playerUnfreezerObj.AddComponent<FaceOffPlayerUnfreezer>();
                        UnityEngine.Object.DontDestroyOnLoad(playerUnfreezerObj);

                        // Create puck validator
                        GameObject puckValidatorObj = new GameObject("FaceOffPuckValidator");
                        _puckValidator = puckValidatorObj.AddComponent<FaceOffPuckValidator>();
                        UnityEngine.Object.DontDestroyOnLoad(puckValidatorObj);
                    }
                }
                else {
                    //EventManager.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.AddEventListener("Event_OnSceneLoaded", Event_OnSceneLoaded);
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                }

                _harmonyPatched = true;
                Logic = true;
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

                if (_refSignalsBlueTeam != null && _refSignalsBlueTeam.Errors.Count != 0) {
                    Logging.LogError("There was an error when initializing _refSignalsBlueTeam.", ServerConfig);
                    foreach (string error in _refSignalsBlueTeam.Errors)
                        Logging.LogError(error, ServerConfig);
                }

                if (_refSignalsRedTeam != null && _refSignalsRedTeam.Errors.Count != 0) {
                    Logging.LogError("There was an error when initializing _refSignalsRedTeam.", ServerConfig);
                    foreach (string error in _refSignalsRedTeam.Errors)
                        Logging.LogError(error, ServerConfig);
                }

                Logging.Log($"Disabling...", ServerConfig, true);

                Logging.Log("Unsubscribing from events.", ServerConfig, true);
                NetworkCommunication.RemoveFromNotLogList(DATA_NAMES_TO_IGNORE);
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    EventManager.Instance.RemoveEventListener(Codebase.Constants.RULESET_MOD_NAME, Event_OnRulesetTrigger);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerBodySpawned", Event_OnPlayerBodySpawned);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER);
                }
                else {
                    //EventManager.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.RemoveEventListener("Event_OnSceneLoaded", Event_OnSceneLoaded);
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    Event_Client_OnClientStopped(new Dictionary<string, object>());
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT);
                    //_getStickLocation.Disable();
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                if (_refSignalsBlueTeam != null) {
                    _refSignalsBlueTeam.StopAllSignals();
                    _refSignalsBlueTeam.DestroyGameObjects();
                    _refSignalsBlueTeam = null;
                }

                if (_refSignalsRedTeam != null) {
                    _refSignalsRedTeam.StopAllSignals();
                    _refSignalsRedTeam.DestroyGameObjects();
                    _refSignalsRedTeam = null;
                }

                if (_boundaryManager != null) {
                    UnityEngine.Object.Destroy(_boundaryManager.gameObject);
                    _boundaryManager = null;
                }

                if (_playerUnfreezer != null) {
                    UnityEngine.Object.Destroy(_playerUnfreezer.gameObject);
                    _playerUnfreezer = null;
                }

                if (_puckValidator != null) {
                    UnityEngine.Object.Destroy(_puckValidator.gameObject);
                    _puckValidator = null;
                }

                _harmony.UnpatchSelf();

                Logging.Log($"Disabled.", ServerConfig, true);

                _harmonyPatched = false;
                Logic = true;
                return true;
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to disable.\n{ex}", ServerConfig);
                return false;
            }
        }

        /// <summary>
        /// Function that sends and sets the SOG for a player when a goal is scored.
        /// </summary>
        /// <param name="player">Player, player that scored.</param>
        private static void SendSOGDuringGoal(Player player) {
            try {
                EventManager.Instance.TriggerEvent(Codebase.Constants.STATS_MOD_NAME, new Dictionary<string, object> { { Codebase.Constants.SOG, player.SteamId.Value.ToString() } });
                if (!NetworkCommunication.GetDataNamesToIgnore().Contains(Codebase.Constants.SOG))
                    Logging.Log($"Sent data \"{Codebase.Constants.SOG}\" to {Codebase.Constants.STATS_MOD_NAME}.", ServerConfig);
            }
            catch (Exception ex) {
                Logging.LogError(ex.ToString(), ServerConfig);
            }
        }

        /// <summary>
        /// Method that loads the assets for the client-side ref UI.
        /// </summary>
        private static void LoadAssets() {
            if (_refSignalsBlueTeam == null) {
                GameObject refSignalsBlueTeamGameObject = new GameObject(Constants.MOD_NAME + "RefSignalsBlueTeam");
                _refSignalsBlueTeam = refSignalsBlueTeamGameObject.AddComponent<RefSignals>();
                _refSignalsBlueTeam.LoadImages(PlayerTeam.Blue);
            }

            if (_refSignalsRedTeam == null) {
                GameObject refSignalsRedTeamGameObject = new GameObject(Constants.MOD_NAME + "RefSignalsRedTeam");
                _refSignalsRedTeam = refSignalsRedTeamGameObject.AddComponent<RefSignals>();
                _refSignalsRedTeam.LoadImages(PlayerTeam.Red);
            }
        }

        private static void GetAllLayersName() {
            for (int i = 0; i < 32; i++) {
                Logging.Log($"Layer {i} name : {LayerMask.LayerToName(i)}.", ServerConfig, true);
            }
        }

        private static bool AreBothNegativeOrPositive(float num1, float num2) {
            return (num1 <= 0 && num2 <= 0) || (num1 >= 0 && num2 >= 0);
        }

        public static double GetDistance(double x1, double z1, double x2, double z2) {
            return Math.Sqrt(Math.Pow(Math.Abs(x1 - x2), 2) + Math.Pow(Math.Abs(z1 - z2), 2));
        }

        private static void CleanupClientIds() {
            foreach (ulong clientId in new List<ulong>(PlayerFunc.Players_ClientId_SteamId.Keys)) {
                Player _player = PlayerManager.Instance.GetPlayerByClientId(clientId);
                if (_player == null || _player.Equals(default(Player)) || !_player)
                    PlayerFunc.Players_ClientId_SteamId.Remove(clientId);
            }
        }
        #endregion

        /*private static InputAction _getStickLocation;

        /// <summary>
        /// Class that patches the Update event from PlayerInput.
        /// </summary>
        [HarmonyPatch(typeof(PlayerInput), "Update")]
        public class PlayerInput_Update_Patch {
            [HarmonyPrefix]
            public static bool Prefix() {
                try {
                    // If this is the server, do not use the patch.
                    if (ServerFunc.IsDedicatedServer())
                        return true;

                    UIChat chat = UIChat.Instance;

                    if (chat.IsFocused)
                        return true;

                    if (_getStickLocation.WasPressedThisFrame()) {
                        Logging.Log($"Puck scale : {_puckScale}. Puck position : ZMin = {PuckManager.Instance.GetPuck().Rigidbody.transform.position.z - PuckRadius}, ZMax = {PuckManager.Instance.GetPuck().Rigidbody.transform.position.z + PuckRadius}", ClientConfig);
                        Logging.Log($"Player position : {PlayerManager.Instance.GetLocalPlayer().PlayerBody.Rigidbody.transform.position}", ClientConfig);
                    }

                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PlayerInput_Update_Patch Prefix().\n{ex}", ClientConfig);
                }

                return true;
            }
        }*/
    }

    public enum Rule {
        None,
        [Description("OFFSIDE"), Category("ToString")]
        Offside,
        [Description("ICING"), Category("ToString")]
        Icing,
        [Description("HIGH STICK"), Category("ToString")]
        HighStick,
        [Description("GOALIE INT"), Category("ToString")]
        GoalieInt,
    }

    internal class PlayerIcing {
        internal Player Player { get; set; }

        internal float X { get; set; }

        internal float Z { get; set; }

        internal bool IsBehindBlueTeamHashmarks { get; set; }

        internal bool IsBehindRedTeamHashmarks { get; set; }

        internal bool IsBehindHashmarks => IsBehindBlueTeamHashmarks || IsBehindRedTeamHashmarks;

        internal bool ConsiderForIcing { get; set; }
    }

    internal class IcingObject { // TODO : Change name.
        internal Stopwatch Watch { get; set; } = null;

        internal float Delta { get; set; } = 0;

        internal bool DeltaHasBeenChecked { get; set; } = false;

        internal int FrameCheck { get; set; } = 0;

        internal bool AnyPlayersBehindHashmarks { get; set; } = false;

        internal IcingObject() { }

        internal IcingObject(Stopwatch watch, float delta, bool anyPlayersBehindHashmarks) {
            Watch = watch;
            Delta = delta;
            AnyPlayersBehindHashmarks = anyPlayersBehindHashmarks;
        }
    }

    public static class EnumExtensions {
        public static string GetDescription(this Enum enumValue, string category = "") {
            // Get the FieldInfo for the enum member
            FieldInfo fieldInfo = enumValue.GetType().GetField(enumValue.ToString());

            // Check if the field exists and has a DescriptionAttribute
            if (fieldInfo != null) {
                if (!string.IsNullOrEmpty(category)) {
                    CategoryAttribute[] categoryAttributes = (CategoryAttribute[])fieldInfo.GetCustomAttributes(typeof(CategoryAttribute), false);
                    if (categoryAttributes == null || categoryAttributes.Length == 0 || categoryAttributes[0].Category.ToLower() != category.ToLower())
                        return "";
                }

                DescriptionAttribute[] descriptionAttributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (descriptionAttributes != null && descriptionAttributes.Length > 0)
                    return descriptionAttributes[0].Description;
            }

            return "";
        }
    }
}
