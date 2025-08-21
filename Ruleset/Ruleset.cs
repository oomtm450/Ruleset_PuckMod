using Codebase;
using HarmonyLib;
using oomtm450PuckMod_Ruleset.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing the main code for the Ruleset patch.
    /// </summary>
    public class Ruleset : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private static readonly string MOD_VERSION = "0.19.0DEV10";

        /// <summary>
        /// Const string, last released version of the mod.
        /// </summary>
        private static readonly string OLD_MOD_VERSION = "0.18.2";

        /// <summary>
        /// ReadOnlyCollection of string, collection of datanames to not log.
        /// </summary>
        private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> {
            RefSignals.SHOW_SIGNAL_BLUE,
            RefSignals.SHOW_SIGNAL_RED,
            RefSignals.STOP_SIGNAL_BLUE,
            RefSignals.STOP_SIGNAL_RED,
            RefSignals.STOP_SIGNAL,
        });

        /// <summary>
        /// Const float, radius of the puck.
        /// </summary>
        internal const float PUCK_RADIUS = 0.13f;

        /// <summary>
        /// Const float, radius of a player.
        /// </summary>
        private const float PLAYER_RADIUS = 0.26f;

        /// <summary>
        /// Const float, height of the net's crossbar.
        /// </summary>
        private const float CROSSBAR_HEIGHT = 1.8f;

        private const string SOG = Constants.MOD_NAME + "SOG";

        private const string BATCH_SOG = Constants.MOD_NAME + "BATCHSOG";

        private const string RESET_SOG = Constants.MOD_NAME + "RESETSOG";

        private const string SAVEPERC = Constants.MOD_NAME + "SAVEPERC";

        private const string BATCH_SAVEPERC = Constants.MOD_NAME + "BATCHSAVEPERC";

        private const string RESET_SAVEPERC = Constants.MOD_NAME + "RESETSAVEPERC";

        private const string SOG_HEADER_LABEL_NAME = "SOGHeaderLabel";

        private const string SOG_LABEL = "SOGLabel";

        /// <summary>
        /// Const string, tag to ask the server for the startup data.
        /// </summary>
        private const string ASK_SERVER_FOR_STARTUP_DATA = Constants.MOD_NAME + "ASKDATA";
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
        internal static ServerConfig _serverConfig = new ServerConfig();

        /// <summary>
        /// ServerConfig, config set by the client.
        /// </summary>
        internal static ClientConfig _clientConfig = new ClientConfig();

        /// <summary>
        /// LockList of PlayerIcing, positions of the players on the ice for icing logic.
        /// </summary>
        private static readonly LockList<PlayerIcing> _dictPlayersPositionsForIcing = new LockList<PlayerIcing>();

        /// <summary>
        /// LockDictionary of string and (PlayerTeam and bool), dictionary of offside status of each player with steam Id as a key.
        /// </summary>
        private static readonly LockDictionary<string, (PlayerTeam Team, bool IsOffside)> _isOffside = new LockDictionary<string, (PlayerTeam, bool)>();

        /// <summary>
        /// LockDictionary of PlayerTeam and bool, dictionary for teams if high stick has to be called next frame.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, bool> _callHighStickNextFrame = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        /// <summary>
        /// LockDictionary of PlayerTeam and Stopwatch, dictionary for teams if icing is possible if it reaches the end of the ice.
        /// Stopwatch is null if it is not possible.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, Stopwatch> _isIcingPossible = new LockDictionary<PlayerTeam, Stopwatch> {
            { PlayerTeam.Blue, null },
            { PlayerTeam.Red, null },
        };

        /// <summary>
        /// LockDictionary of PlayerTeam and bool, dictionary for teams if icing is active.
        /// </summary>
        private static readonly LockDictionary<PlayerTeam, bool> _isIcingActive = new LockDictionary<PlayerTeam, bool> {
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

        private static readonly LockDictionary<string, Stopwatch> _playersCurrentPuckTouch = new LockDictionary<string, Stopwatch>();

        private static readonly LockDictionary<string, Stopwatch> _playersLastTimePuckPossession = new LockDictionary<string, Stopwatch>();

        private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

        //private static InputAction _getStickLocation;

        private static readonly LockDictionary<string, Stopwatch> _lastTimeOnCollisionExitWasCalled = new LockDictionary<string, Stopwatch>();

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

        private static readonly LockDictionary<PlayerTeam, bool> _lastShotWasCounted = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, true },
            { PlayerTeam.Red, true },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _lastGoalieStateCollision = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static float _lastForceOnGoalie = 0;

        private static string _lastForceOnGoaliePlayerSteamId = "";

        private static bool _paused = false;

        private static bool _doFaceoff = false;

        /// <summary>
        /// Bool, true if the mod has registered with the named message handler for server/client communication.
        /// </summary>
        private static bool _hasRegisteredWithNamedMessageHandler = false;

        private static PuckRaycast _puckRaycast;

        private static readonly LockDictionary<PlayerTeam, SaveCheck> _checkIfPuckWasSaved = new LockDictionary<PlayerTeam, SaveCheck> {
            { PlayerTeam.Blue, new SaveCheck() },
            { PlayerTeam.Red, new SaveCheck() },
        };

        private static bool _hasPlayedLastMinuteMusic = false;

        private static bool _hasPlayedFirstFaceoffMusic = false;

        private static bool _hasPlayedSecondFaceoffMusic = false;

        /// <summary>
        /// FaceoffSpot, where the next faceoff has to be taken.
        /// </summary>
        private static FaceoffSpot _nextFaceoffSpot = FaceoffSpot.Center;

        // Client-side and server-side.
        private static readonly LockDictionary<string, int> _sog = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, (int Saves, int Shots)> _savePerc = new LockDictionary<string, (int Saves, int Shots)>();

        // Client-side.
        private static Sounds _sounds = null;

        private static RefSignals _refSignalsBlueTeam = null;

        private static RefSignals _refSignalsRedTeam = null;

        private static string _currentMusicPlaying = "";

        private static readonly List<string> _hasUpdatedUIScoreboard = new List<string>();

        private static readonly LockDictionary<string, Label> _sogLabels = new LockDictionary<string, Label>();

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

        // Barrier collider, position 0 -19 0 is realistic.
        #endregion

        #region Harmony Patches
        #region Puck_OnCollision
        /// <summary>
        /// Class that patches the OnCollisionEnter event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
        public class Puck_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing)
                    return;

                try {
                    Stick stick = GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody || !playerBody.Player)
                            return;

                        _puckZoneLastTouched = _puckZone;

                        PlayerTeam playerOtherTeam = TeamFunc.GetOtherTeam(playerBody.Player.Team.Value);
                        if (IsIcingPossible(playerOtherTeam, _dictPlayersPositionsForIcing.Any(x => playerOtherTeam == PlayerTeam.Blue ? x.IsBehindRedTeamHashmarks : x.IsBehindBlueTeamHashmarks))) {
                            if (!Codebase.PlayerFunc.IsGoalie(playerBody.Player)) {
                                if (_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var playerZone)) {
                                    if (playerZone.Zone != ZoneFunc.GetTeamZones(playerBody.Player.Team.Value)[1]) {
                                        if (IsIcing(playerOtherTeam)) {
                                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerOtherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                                            SendChat(Rule.Icing, playerOtherTeam, true, true);
                                        }
                                        ResetIcings();
                                    }
                                }
                            }

                            if (IsIcing(playerOtherTeam)) {
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerOtherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                                SendChat(Rule.Icing, playerOtherTeam, true, true);
                            }
                            ResetIcings();
                        }
                        else if (IsIcingPossible(playerBody.Player.Team.Value, _dictPlayersPositionsForIcing.Any(x => playerBody.Player.Team.Value == PlayerTeam.Blue ? x.IsBehindRedTeamHashmarks : x.IsBehindBlueTeamHashmarks))) {
                            if (_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var playerZone)) {
                                if (ZoneFunc.GetTeamZones(playerOtherTeam, true).Any(x => x == playerZone.Zone)) {
                                    if (IsIcing(playerBody.Player.Team.Value)) {
                                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerBody.Player.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                                        SendChat(Rule.Icing, playerBody.Player.Team.Value, true, true);
                                    }
                                    ResetIcings();
                                }
                            }
                        }

                        if (_puckRaycast.PuckIsGoingToNet[playerBody.Player.Team.Value]) {
                            if (Codebase.PlayerFunc.IsGoalie(playerBody.Player)) {
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

                    _puckZoneLastTouched = _puckZone;

                    string currentPlayerSteamId = stick.Player.SteamId.Value.ToString();

                    //Logging.Log($"Puck was hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (enter)!", _serverConfig);

                    // Start tipped timer.
                    if (!_playersCurrentPuckTouch.TryGetValue(currentPlayerSteamId, out Stopwatch watch)) {
                        watch = new Stopwatch();
                        watch.Start();
                        _playersCurrentPuckTouch.Add(currentPlayerSteamId, watch);
                    }

                    string lastPlayerOnPuckTipIncludedSteamId = _lastPlayerOnPuckTipIncludedSteamId[_lastPlayerOnPuckTeamTipIncluded];

                    if (!_lastTimeOnCollisionExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionExitWatch)) {
                        lastTimeCollisionExitWatch = new Stopwatch();
                        lastTimeCollisionExitWatch.Start();
                        _lastTimeOnCollisionExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionExitWatch);
                    }
                    else if (lastTimeCollisionExitWatch.ElapsedMilliseconds > _serverConfig.MaxTippedMilliseconds || lastPlayerOnPuckTipIncludedSteamId != currentPlayerSteamId) {
                        //if (lastPlayerOnPuckSteamId == currentPlayerSteamId || string.IsNullOrEmpty(lastPlayerOnPuckSteamId))
                            //Logging.Log($"{stick.Player.Username.Value} had the puck for {((double)(watch.ElapsedMilliseconds - lastTimeCollisionExitWatch.ElapsedMilliseconds)) / 1000d} seconds.", _serverConfig);
                        watch.Restart();

                        if (!string.IsNullOrEmpty(lastPlayerOnPuckTipIncludedSteamId) && lastPlayerOnPuckTipIncludedSteamId != currentPlayerSteamId) {
                            if (_playersCurrentPuckTouch.TryGetValue(lastPlayerOnPuckTipIncludedSteamId, out Stopwatch lastPlayerWatch)) {
                                //Logging.Log($"{lastPlayerOnPuckSteamId} had the puck for {((double)(lastPlayerWatch.ElapsedMilliseconds - _lastTimeOnCollisionExitWasCalled[lastPlayerOnPuckSteamId].ElapsedMilliseconds)) / 1000d} seconds.", _serverConfig);
                                lastPlayerWatch.Reset();
                            }
                        }
                    }

                    // High stick logic.
                    Puck puck = PuckManager.Instance.GetPuck();
                    if (puck) {
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player) && puck.Rigidbody.transform.position.y > _serverConfig.HighStick.MaxHeight + stick.Player.PlayerBody.Rigidbody.transform.position.y) {
                            _isHighStickActiveTimers.TryGetValue(stick.Player.Team.Value, out Timer highStickTimer);

                            highStickTimer.Change(_serverConfig.HighStick.MaxMilliseconds, Timeout.Infinite);
                            if (!IsHighStick(stick.Player.Team.Value)) {
                                _isHighStickActive[stick.Player.Team.Value] = true;
                                _puckLastStateBeforeCall[Rule.HighStick] = (puck.Rigidbody.transform.position, _puckZone);
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, stick.Player.Team.Value), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                                SendChat(Rule.HighStick, stick.Player.Team.Value, false);
                            }
                        }
                        else if (puck.IsGrounded) {
                            if (IsHighStick(stick.Player.Team.Value)) {
                                _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(stick.Player.Team.Value, false, _puckLastStateBeforeCall[Rule.HighStick]);

                                _isHighStickActiveTimers.TryGetValue(stick.Player.Team.Value, out Timer highStickTimer);
                                highStickTimer.Change(Timeout.Infinite, Timeout.Infinite);

                                SendChat(Rule.HighStick, stick.Player.Team.Value, true);
                                DoFaceoff(RefSignals.GetSignalConstant(true, stick.Player.Team.Value), RefSignals.HIGHSTICK_REF);
                            }
                        }
                    }

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);
                    if (IsHighStick(otherTeam)) {
                        _isHighStickActive[otherTeam] = false;
                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, otherTeam), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                        SendChat(Rule.HighStick, otherTeam, true, true);
                    }

                    if (_puckRaycast.PuckIsGoingToNet[stick.Player.Team.Value]) {
                        if (Codebase.PlayerFunc.IsGoalie(stick.Player)) {
                            string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[otherTeam];
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

                    Stick stick = GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = GetPlayerBodyV2(collision.gameObject);
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

                    var puckLastStateBeforeCallOffside = _puckLastStateBeforeCall[Rule.Offside];

                    if (!PuckIsTipped(playerSteamId)) {
                        _lastPlayerOnPuckTeam = stick.Player.Team.Value;
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player))
                            ResetAssists(TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam));
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = playerSteamId;

                        Puck puck = PuckManager.Instance.GetPuck();
                        if (puck)
                            _puckLastStateBeforeCall[Rule.GoalieInt] = _puckLastStateBeforeCall[Rule.Offside] = (puck.Rigidbody.transform.position, _puckZone);
                    }

                    _lastPlayerOnPuckTeamTipIncluded = stick.Player.Team.Value;
                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = playerSteamId;
                    _playersOnPuckTipIncludedDateTime.AddOrUpdate(playerSteamId, (stick.Player.Team.Value, DateTime.UtcNow));

                    if (!_playersLastTimePuckPossession.TryGetValue(playerSteamId, out Stopwatch watch)) {
                        watch = new Stopwatch();
                        watch.Start();
                        _playersLastTimePuckPossession.Add(playerSteamId, watch);
                    }

                    watch.Restart();

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);
                    // Offside logic.
                    List<Zone> otherTeamZones = ZoneFunc.GetTeamZones(otherTeam);
                    if (IsOffside(stick.Player.Team.Value) && (_puckZone == otherTeamZones[0] || _puckZone == otherTeamZones[1])) {
                        _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(stick.Player.Team.Value, false, puckLastStateBeforeCallOffside);
                        SendChat(Rule.Offside, stick.Player.Team.Value, true);
                        DoFaceoff();
                    }

                    // Icing logic.
                    if (IsIcing(otherTeam)) {
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player)) {
                            _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(otherTeam, true, _puckLastStateBeforeCall[Rule.Icing]);
                            SendChat(Rule.Icing, otherTeam, true);
                            ResetIcings();
                            DoFaceoff();
                        }
                        else {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, otherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                            SendChat(Rule.Icing, otherTeam, true, true);
                            ResetIcings();
                        }
                    }
                    else {
                        if (IsIcing(stick.Player.Team.Value)) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, stick.Player.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                            SendChat(Rule.Icing, stick.Player.Team.Value, true, true);
                        }
                        ResetIcings();
                    }
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
            public static void Postfix(Collision collision) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing)
                        return;

                    Stick stick = GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    _puckZoneLastTouched = _puckZone;

                    string currentPlayerSteamId = stick.Player.SteamId.Value.ToString();

                    if (!_lastTimeOnCollisionExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionWatch)) {
                        lastTimeCollisionWatch = new Stopwatch();
                        lastTimeCollisionWatch.Start();
                        _lastTimeOnCollisionExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionWatch);
                    }

                    lastTimeCollisionWatch.Restart();

                    if (!PuckIsTipped(currentPlayerSteamId)) {
                        _lastPlayerOnPuckTeam = stick.Player.Team.Value;
                        if (!Codebase.PlayerFunc.IsGoalie(stick.Player))
                            ResetAssists(TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam));
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = currentPlayerSteamId;

                        Puck puck = PuckManager.Instance.GetPuck();
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
                        _isIcingPossible[stick.Player.Team.Value] = icingPossibleWatch;
                    }
                    else
                        _isIcingPossible[stick.Player.Team.Value] = null;

                    _lastShotWasCounted[stick.Player.Team.Value] = false;
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}", _serverConfig);
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
                if (!ServerFunc.IsDedicatedServer() || _paused || GameManager.Instance.Phase != GamePhase.Playing)
                    return;

                try {
                    if (collision.gameObject.layer != LayerMask.NameToLayer("Player"))
                        return;

                    PlayerBodyV2 playerBody = GetPlayerBodyV2(collision.gameObject);

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
                    if (goalie.PlayerBody.Rigidbody.transform.position.x - _serverConfig.GInt.GoalieRadius < startX ||
                        goalie.PlayerBody.Rigidbody.transform.position.x + _serverConfig.GInt.GoalieRadius > endX ||
                        goalie.PlayerBody.Rigidbody.transform.position.z - _serverConfig.GInt.GoalieRadius < startZ ||
                        goalie.PlayerBody.Rigidbody.transform.position.z + _serverConfig.GInt.GoalieRadius > endZ) {
                        goalieIsInHisCrease = false;
                    }

                    PlayerTeam goalieOtherTeam = TeamFunc.GetOtherTeam(goalie.Team.Value);

                    bool goalieDown = goalie.PlayerBody.HasFallen;
                    _lastGoalieStateCollision[goalieOtherTeam] = goalieDown;

                    if (goalieDown || (force > _serverConfig.GInt.CollisionForceThreshold && goalieIsInHisCrease)) {
                        _ = _goalieIntTimer.TryGetValue(goalieOtherTeam, out Stopwatch watch);

                        if (watch == null) {
                            watch = new Stopwatch();
                            _goalieIntTimer[goalieOtherTeam] = watch;
                        }

                        watch.Restart();
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PlayerBodyV2_OnCollisionEnter_Patch Postfix().\n{ex}", _serverConfig);
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
                    if (!ServerFunc.IsDedicatedServer())
                        return true;

                    if (_paused) {
                        GameManager.Instance.Server_Resume();
                        _changedPhase = false;
                    }
                            
                    _paused = false;
                    _doFaceoff = false;

                    if (phase == GamePhase.BlueScore) {
                        _currentMusicPlaying = Sounds.BLUE_GOAL_MUSIC;
                        NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.RedScore) {
                        _currentMusicPlaying = Sounds.RED_GOAL_MUSIC;
                        NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.PeriodOver) {
                        _nextFaceoffSpot = FaceoffSpot.Center; // Fix faceoff if the period is over because of deferred icing.

                        NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);

                        _currentMusicPlaying = Sounds.BETWEEN_PERIODS_MUSIC;
                        NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.FaceOff || phase == GamePhase.Warmup || phase == GamePhase.GameOver) {
                        if (phase == GamePhase.GameOver) // Fix faceoff if the period is over because of deferred icing.
                            _nextFaceoffSpot = FaceoffSpot.Center;

                        // Reset players zone.
                        _playersZone.Clear();

                        // Reset possession times.
                        foreach (Stopwatch watch in _playersLastTimePuckPossession.Values)
                            watch.Stop();
                        _playersLastTimePuckPossession.Clear();

                        // Reset puck collision exit times.
                        foreach (Stopwatch watch in _lastTimeOnCollisionExitWasCalled.Values)
                            watch.Stop();
                        _lastTimeOnCollisionExitWasCalled.Clear();

                        // Reset tipped times.
                        foreach (Stopwatch watch in _playersCurrentPuckTouch.Values)
                            watch.Stop();
                        _playersCurrentPuckTouch.Clear();

                        // Reset puck rule states.
                        foreach (Rule key in new List<Rule>(_puckLastStateBeforeCall.Keys))
                            _puckLastStateBeforeCall[key] = (Vector3.zero, ZoneFunc.DEFAULT_ZONE);

                        ResetOffsides();
                        ResetHighSticks();
                        ResetIcings();
                        _dictPlayersPositionsForIcing.Clear();
                        ResetGoalieInt();

                        // Reset puck was saved states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
                            _checkIfPuckWasSaved[key] = new SaveCheck();

                        _puckZone = ZoneFunc.GetZone(_nextFaceoffSpot);
                        _puckZoneLastTouched = _puckZone;

                        _lastPlayerOnPuckTeam = TeamFunc.DEFAULT_TEAM;
                        _lastPlayerOnPuckTeamTipIncluded = TeamFunc.DEFAULT_TEAM;
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckSteamId.Keys))
                            _lastPlayerOnPuckSteamId[key] = "";

                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
                            _lastPlayerOnPuckTipIncludedSteamId[key] = "";

                        _playersOnPuckTipIncludedDateTime.Clear();

                        NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.Playing) {
                        if (time == -1 && _serverConfig.ReAdd1SecondAfterFaceoff)
                            time = GetPrivateField<int>(typeof(GameManager), GameManager.Instance, "remainingPlayTime") + 1;
                    }

                    if (!_changedPhase) {
                        if (string.IsNullOrEmpty(_currentMusicPlaying) || _currentMusicPlaying == Sounds.WARMUP_MUSIC) {
                            NetworkCommunication.SendDataToAll(Sounds.STOP_SOUND, Sounds.ALL, Constants.FROM_SERVER, _serverConfig);

                            if (phase == GamePhase.FaceOff) {
                                if (!_hasPlayedLastMinuteMusic && GameManager.Instance.GameState.Value.Time <= 60 && GameManager.Instance.GameState.Value.Period == 3) {
                                    _hasPlayedLastMinuteMusic = true;
                                    _currentMusicPlaying = Sounds.LAST_MINUTE_MUSIC;
                                }
                                else if (!_hasPlayedFirstFaceoffMusic) {
                                    _hasPlayedFirstFaceoffMusic = true;
                                    _currentMusicPlaying = Sounds.FIRST_FACEOFF_MUSIC;
                                }
                                else if (!_hasPlayedSecondFaceoffMusic) {
                                    _hasPlayedSecondFaceoffMusic = true;
                                    _currentMusicPlaying = Sounds.SECOND_FACEOFF_MUSIC;
                                }
                                else
                                    _currentMusicPlaying = Sounds.FACEOFF_MUSIC;

                                NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER, _serverConfig);
                                _currentMusicPlaying = Sounds.FACEOFF_MUSIC;
                                return true;
                            }
                        }

                        if (phase == GamePhase.GameOver) {
                            NetworkCommunication.SendDataToAll(Sounds.STOP_SOUND, Sounds.ALL, Constants.FROM_SERVER, _serverConfig);
                            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(Sounds.GAMEOVER_MUSIC), Constants.FROM_SERVER, _serverConfig);
                            _currentMusicPlaying = Sounds.GAMEOVER_MUSIC;
                        }
                        else if (phase == GamePhase.Warmup) {
                            NetworkCommunication.SendDataToAll(Sounds.STOP_SOUND, Sounds.ALL, Constants.FROM_SERVER, _serverConfig);
                            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(Sounds.WARMUP_MUSIC), Constants.FROM_SERVER, _serverConfig);
                            _currentMusicPlaying = Sounds.WARMUP_MUSIC;
                        }

                        return true;
                    }

                    if (phase == GamePhase.Playing) {
                        _changedPhase = false;
                        if (_serverConfig.ReAdd1SecondAfterFaceoff)
                            time = _periodTimeRemaining + 1;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Prefix().\n{ex}", _serverConfig);
                }

                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(GamePhase phase, int time) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return;

                    if (phase == GamePhase.FaceOff) {
                        if (_nextFaceoffSpot == FaceoffSpot.Center || !_serverConfig.UseCustomFaceoff)
                            return;

                        Vector3 dot = Faceoff.GetFaceoffDot(_nextFaceoffSpot);

                        List<Player> players = PlayerManager.Instance.GetPlayers();
                        foreach (Player player in players)
                            PlayerFunc.TeleportOnFaceoff(player, dot, _nextFaceoffSpot);

                        return;
                    }
                    else if (phase == GamePhase.Playing) {
                        NetworkCommunication.SendDataToAll(Sounds.STOP_SOUND, Sounds.MUSIC, Constants.FROM_SERVER, _serverConfig);
                        _currentMusicPlaying = "";
                        return;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Postfix().\n{ex}", _serverConfig);
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
                    if (!ServerFunc.IsDedicatedServer() || isReplay || !_serverConfig.UseCustomFaceoff || (GameManager.Instance.Phase != GamePhase.Playing && GameManager.Instance.Phase != GamePhase.FaceOff))
                        return true;

                    Vector3 dot = Faceoff.GetFaceoffDot(_nextFaceoffSpot);

                    if (_serverConfig.UseDefaultPuckDropHeight)
                        position = new Vector3(dot.x, position.y, dot.z);
                    else
                        position = new Vector3(dot.x, _serverConfig.PuckDropHeight, dot.z);

                    _nextFaceoffSpot = FaceoffSpot.Center;
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Prefix().\n{ex}", _serverConfig);
                }

                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(ref Puck __result, Vector3 position, Quaternion rotation, Vector3 velocity, bool isReplay) {
                try {
                    // If this is not the server or this is a replay or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || isReplay || (GameManager.Instance.Phase != GamePhase.Playing && GameManager.Instance.Phase != GamePhase.FaceOff))
                        return;

                    __result.gameObject.AddComponent<PuckRaycast>();
                    _puckRaycast = __result.gameObject.GetComponent<PuckRaycast>();
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Postfix().\n{ex}", _serverConfig);
                }
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
                        Logging.Log($"Stick position : {PlayerManager.Instance.GetLocalPlayer().Stick.BladeHandlePosition}", _clientConfig);
                    }
                        
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PlayerInput_Update_Patch Prefix().\n{ex}");
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
                        if (message.StartsWith(@"/musicvol ")) {
                            message = message.Replace(@"/musicvol ", "").Trim();
                            if (float.TryParse(message, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float vol)) {
                                if (vol > 1f)
                                    vol = 1f;
                                else if (vol < 0)
                                    vol = 0;

                                _clientConfig.MusicVolume = vol;
                                _clientConfig.Save();
                                _sounds?.ChangeMusicVolume();
                                Logging.Log($"Adjusted client music volume to {vol}f.", _clientConfig);
                            }
                        }

                        if (message.StartsWith(@"/help")) {
                            UIChat.Instance.AddChatMessage("Ruleset commands:\n* <b>/musicvol</b> - Adjust music volume (0.0-1.0)\n");
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIChat_Client_SendClientChatMessage_Patch Prefix().\n{ex}", _serverConfig);
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
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing)
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
                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, callOffHighStickTeam), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                        SendChat(Rule.HighStick, callOffHighStickTeam, true, true);*/

                        _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(callHighStickTeam, false, _puckLastStateBeforeCall[Rule.HighStick]);
                        SendChat(Rule.HighStick, callHighStickTeam, true);
                        ResetHighSticks();

                        DoFaceoff(RefSignals.GetSignalConstant(true, callHighStickTeam), RefSignals.HIGHSTICK_REF);
                        break;
                    }

                    // If game was paused by the mod, don't do anything if faceoff hasn't being set yet.
                    if (_paused && !_doFaceoff)
                        return true;

                    // Unpause game and set faceoff.
                    if (_doFaceoff)
                        PostDoFaceoff();

                    players = PlayerManager.Instance.GetPlayers();
                    puck = PuckManager.Instance.GetPuck();

                    if (players.Count == 0 || puck == null || _paused)
                        return true;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 1.\n{ex}", _serverConfig);
                }

                try {
                    oldZone = _puckZone;
                    _puckZone = ZoneFunc.GetZone(puck.Rigidbody.transform.position, _puckZone, PUCK_RADIUS);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 2.\n{ex}", _serverConfig);
                }

                Dictionary<PlayerTeam, bool> isTeamOffside = new Dictionary<PlayerTeam, bool> {
                    { PlayerTeam.Blue, IsOffside(PlayerTeam.Blue) },
                    { PlayerTeam.Red, IsOffside(PlayerTeam.Red) },
                };

                try {
                    string playerWithPossessionSteamId = GetPlayerSteamIdInPossession();

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

                        Zone playerZone = ZoneFunc.GetZone(player.PlayerBody.transform.position, oldPlayerZone, PLAYER_RADIUS);
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
                            if (ZoneFunc.IsBehindHashmarks(otherTeam, player.PlayerBody.transform.position, PLAYER_RADIUS)) {
                                if (otherTeam == PlayerTeam.Blue)
                                    isPlayerBehindBlueTeamHashmarks = true;
                                else
                                    isPlayerBehindRedTeamHashmarks = true;

                                if (IsIcing(player.Team.Value) && AreBothNegativeOrPositive(player.PlayerBody.transform.position.x, puck.Rigidbody.transform.position.x))
                                    considerForIcing = true;
                            }
                            else if (ZoneFunc.IsBehindHashmarks(player.Team.Value, player.PlayerBody.transform.position, PLAYER_RADIUS)) {
                                if (player.Team.Value == PlayerTeam.Blue)
                                    isPlayerBehindBlueTeamHashmarks = true;
                                else
                                    isPlayerBehindRedTeamHashmarks = true;

                                if (IsIcing(otherTeam) && AreBothNegativeOrPositive(player.PlayerBody.transform.position.x, puck.Rigidbody.transform.position.x)) {
                                    considerForIcing = true;
                                    _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(otherTeam, true, _puckLastStateBeforeCall[Rule.Icing]);
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

                    // Remove offside if the other team entered the zone with the puck.
                    List<Zone> lastPlayerOnPuckTeamZones = ZoneFunc.GetTeamZones(_lastPlayerOnPuckTeam, true);
                    if (oldZone == lastPlayerOnPuckTeamZones[2] && _puckZone == lastPlayerOnPuckTeamZones[0]) {
                        PlayerTeam lastPlayerOnPuckOtherTeam = TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam);
                        foreach (string key in new List<string>(_isOffside.Keys)) {
                            if (_isOffside[key].Team == lastPlayerOnPuckOtherTeam)
                                _isOffside[key] = (lastPlayerOnPuckOtherTeam, false);
                        }
                    }

                    PlayerTeam puckTeamZone;
                    if (puck.Rigidbody.transform.position.z > 0)
                        puckTeamZone = PlayerTeam.Blue;
                    else
                        puckTeamZone = PlayerTeam.Red;

                    // Deferred icing logic.
                    if (_serverConfig.Icing.Deferred && _dictPlayersPositionsForIcing.Any(x => x.ConsiderForIcing && (puckTeamZone == PlayerTeam.Blue ? x.IsBehindBlueTeamHashmarks : x.IsBehindRedTeamHashmarks))) {
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

                                double blueTeamPlayerDistanceToPuck = Math.Sqrt(Math.Pow(Math.Abs(puckXCoordinate - closestPlayerToEndBoardBlueTeam.X), 2) + Math.Pow(Math.Abs(puckZCoordinate - closestPlayerToEndBoardBlueTeam.Z), 2));
                                double redTeamPlayerDistanceToPuck = Math.Sqrt(Math.Pow(Math.Abs(puckXCoordinate - closestPlayerToEndBoardRedTeam.X), 2) + Math.Pow(Math.Abs(puckZCoordinate - closestPlayerToEndBoardRedTeam.Z), 2));

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
                                    NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, closestPlayerToEndBoard.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI
                                    SendChat(Rule.Icing, closestPlayerToEndBoard.Team.Value, true, true);
                                }
                                else
                                    icingHasToBeWarned[closestPlayerToEndBoard.Team.Value] = false;
                                ResetIcings();
                            }
                            else if (IsIcing(closestPlayerToEndBoardOtherTeam)) {
                                SendChat(Rule.Icing, closestPlayerToEndBoardOtherTeam, true);
                                ResetIcings();
                                DoFaceoff();
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 3.\n{ex}", _serverConfig);
                }

                try {
                    // Icing logic.
                    ServerManager_Update_IcingLogic(PlayerTeam.Blue, puck, icingHasToBeWarned, _dictPlayersPositionsForIcing.Any(x => x.IsBehindRedTeamHashmarks));
                    ServerManager_Update_IcingLogic(PlayerTeam.Red, puck, icingHasToBeWarned, _dictPlayersPositionsForIcing.Any(x => x.IsBehindBlueTeamHashmarks));
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 4.\n{ex}", _serverConfig);
                }

                try {
                    // Warn icings.
                    foreach (var kvp in icingHasToBeWarned) {
                        if (kvp.Value != null && (bool)kvp.Value) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, kvp.Key), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send show icing signal for client-side UI.
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
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 5.\n{ex}", _serverConfig);
                }

                return true;
            }

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
                            NetworkCommunication.SendDataToAll(SOG + shotPlayerSteamId, _sog[shotPlayerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
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

                                NetworkCommunication.SendDataToAll(SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
                                LogSavePerc(_goaliePlayerSteamId, saves, sog);
                            }

                            _checkIfPuckWasSaved[key] = new SaveCheck();
                        }
                        else {
                            if (++saveCheck.FramesChecked > 480)
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
        /// Class that patches the Event_Server_OnPuckEnterTeamGoal event from GameManagerController.
        /// </summary>
        [HarmonyPatch(typeof(GameManagerController), "Event_Server_OnPuckEnterTeamGoal")]
        public class GameManagerController_Event_Server_OnPuckEnterTeamGoal_Patch {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing)
                        return true;

                    if (_paused)
                        return false;

                    NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);

                    _nextFaceoffSpot = FaceoffSpot.Center;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManagerController_Event_Server_OnPuckEnterTeamGoal_Patch Prefix().\n{ex}", _serverConfig);
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
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return true;

                    bool isGoalieInt = IsGoalieInt(team);

                    if (goalPlayer != null) {
                        // No goal if offside or high stick or goalie interference.
                        bool isOffside = false, isHighStick = false;
                        isOffside = IsOffside(team);
                        isHighStick = IsHighStick(team);

                        if (isOffside || isHighStick || isGoalieInt) {
                            if (isOffside) {
                                _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.Offside]);
                                SendChat(Rule.Offside, team, true, false);
                                DoFaceoff();
                            }
                            else if (isHighStick) {
                                _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.HighStick]);
                                SendChat(Rule.HighStick, team, true, false);
                                DoFaceoff(RefSignals.GetSignalConstant(true, team), RefSignals.HIGHSTICK_REF);
                            }
                            else if (isGoalieInt) {
                                _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.GoalieInt]);
                                SendChat(Rule.GoalieInt, team, true, false);
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
                        SendSavePercDuringGoal(team, SendSOGDuringGoal(goalPlayer));
                        return true;
                    }

                    if (isGoalieInt) {
                        _nextFaceoffSpot = Faceoff.GetNextFaceoffPosition(team, false, _puckLastStateBeforeCall[Rule.GoalieInt]);
                        SendChat(Rule.GoalieInt, team, true, false);
                        DoFaceoff(RefSignals.GetSignalConstant(true, team), RefSignals.INTERFERENCE_REF);
                        return false;
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
        /// Class that patches the Server_RespawnCharacter event from Player.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.Server_RespawnCharacter))]
        public class Player_Server_RespawnCharacter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Vector3 position, Quaternion rotation, PlayerRole role) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return;

                    // If this game is not started or faceoff is on the default dot (center), do not use the patch.
                    if (GameManager.Instance.Phase != GamePhase.FaceOff || _nextFaceoffSpot == FaceoffSpot.Center || !_serverConfig.UseCustomFaceoff)
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
                    PlayerFunc.TeleportOnFaceoff(player, Faceoff.GetFaceoffDot(_nextFaceoffSpot), _nextFaceoffSpot);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Player_Server_RespawnCharacter_Patch Postfix().\n{ex}", _serverConfig);
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
                        //Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_SERVER}.", _clientConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;

                        DateTime now = DateTime.UtcNow;
                        if (_lastDateTimeAskStartupData + TimeSpan.FromSeconds(1) < now) {
                            _lastDateTimeAskStartupData = now;
                            NetworkCommunication.SendData(ASK_SERVER_FOR_STARTUP_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT, _clientConfig);
                        }
                    }

                    if (_askForKick) {
                        _askForKick = false;
                        NetworkCommunication.SendData(Constants.MOD_NAME + "_kick", "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT, _clientConfig);
                    }

                    if (_addServerModVersionOutOfDateMessage) {
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
                    NetworkCommunication.SendDataToAll(RESET_SAVEPERC, "1", Constants.FROM_SERVER, _serverConfig);

                    // Reset SOG.
                    foreach (string key in new List<string>(_sog.Keys)) {
                        if (players.FirstOrDefault(x => x.SteamId.Value.ToString() == key) != null)
                            _sog[key] = 0;
                        else
                            _sog.Remove(key);
                    }
                    NetworkCommunication.SendDataToAll(RESET_SOG, "1", Constants.FROM_SERVER, _serverConfig);

                    // Reset music.
                    NetworkCommunication.SendDataToAll(Sounds.STOP_SOUND, Sounds.MUSIC, Constants.FROM_SERVER, _serverConfig);
                    _currentMusicPlaying = "";
                    _hasPlayedLastMinuteMusic = false;
                    _hasPlayedFirstFaceoffMusic = false;
                    _hasPlayedSecondFaceoffMusic = false;

                    if (_nextFaceoffSpot != FaceoffSpot.Center) {
                        foreach (Player player in PlayerManager.Instance.GetPlayers()) {
                            if (player.PlayerPosition && player.PlayerBody)
                                player.PlayerBody.Server_Teleport(player.PlayerPosition.transform.position, player.PlayerPosition.transform.rotation);
                        }

                        _nextFaceoffSpot = FaceoffSpot.Center;
                    }

                    _sentOutOfDateMessage.Clear();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_ResetGameState_Patch Postfix().\n{ex}", _serverConfig);
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
                    Logging.LogError($"Error in UIChat_AddChatMessage_Patch Prefix().\n{ex}", _clientConfig);
                }

                return true;
            }
        }

        /// <summary>
        /// Class that patches the Server_PlayRpc event from SynchronizedAudio.
        /// </summary>
        [HarmonyPatch(typeof(SynchronizedAudio), "Server_PlayRpc")]
        public class SynchronizedAudio_Server_PlayRpc_Patch {
            [HarmonyPrefix]
            public static bool Prefix(SynchronizedAudio __instance, ref float volume, ref float pitch, ref bool isOneShot, int clipIndex, float time, bool fadeIn, float fadeInDuration, bool fadeOut, float fadeOutDuration, float duration, RpcParams rpcParams) {
                try {
                    // If this is the server or the custom goal horns are turned off, do not use the patch.
                    if (ServerFunc.IsDedicatedServer() || !_clientConfig.CustomGoalHorns)
                        return true;

                    AudioSource audioSource = GetPrivateField<AudioSource>(typeof(SynchronizedAudio), __instance, "audioSource");

                    if (audioSource.name == "Blue Goal" || audioSource.name == "Red Goal") {
                        if (audioSource.clip == null)
                            return false;

                        volume = 1f;
                        pitch = 1f;
                        isOneShot = true;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in SynchronizedAudio_Server_PlayRpc_Patch Prefix().\n{ex}", _clientConfig);
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
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, team), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send show offside signal for client-side UI.
                SendChat(Rule.Offside, team, false);
            }
            else {
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, team), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send show offside signal for client-side UI.
                SendChat(Rule.Offside, team, true, true);
            }
        }

        private static void ResetIcings() {
            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingPossible.Keys))
                _isIcingPossible[key] = null;

            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingActive.Keys))
                _isIcingActive[key] = false;

            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingActiveTimers.Keys))
                _isIcingActiveTimers[key].Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void ResetIcingCallback(object stateInfo) {
            PlayerTeam team = (PlayerTeam)stateInfo;
            _isIcingPossible[team] = null;
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
        }

        private static void DoFaceoff(string dataName = "", string dataStr = "", int millisecondsPauseMin = 3500, int millisecondsPauseMax = 5000) {
            if (_paused)
                return;

            _paused = true;

            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(Sounds.WHISTLE), Constants.FROM_SERVER, _serverConfig);

            if (!string.IsNullOrEmpty(dataName) && !string.IsNullOrEmpty(dataStr)) {
                NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);
                NetworkCommunication.SendDataToAll(dataName, dataStr, Constants.FROM_SERVER, _serverConfig);
            }

            if (!_hasPlayedLastMinuteMusic && GameManager.Instance.GameState.Value.Time <= 60 && GameManager.Instance.GameState.Value.Period == 3) {
                _hasPlayedLastMinuteMusic = true;
                _currentMusicPlaying = Sounds.LAST_MINUTE_MUSIC_DELAYED;
            }
            else if (!_hasPlayedFirstFaceoffMusic) {
                _hasPlayedFirstFaceoffMusic = true;
                _currentMusicPlaying = Sounds.FIRST_FACEOFF_MUSIC_DELAYED;
            }
            else if (!_hasPlayedSecondFaceoffMusic) {
                _hasPlayedSecondFaceoffMusic = true;
                _currentMusicPlaying = Sounds.SECOND_FACEOFF_MUSIC_DELAYED;
            }
            else
                _currentMusicPlaying = Sounds.FACEOFF_MUSIC_DELAYED;

            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER, _serverConfig);
            _currentMusicPlaying = Sounds.FACEOFF_MUSIC;

            _periodTimeRemaining = GameManager.Instance.GameState.Value.Time;
            GameManager.Instance.Server_Pause();

            _ = Task.Run(() => {
                Thread.Sleep(new System.Random().Next(millisecondsPauseMin, millisecondsPauseMax + 1));
                _doFaceoff = true;
            });
        }

        private static void PostDoFaceoff() {
            _doFaceoff = false;
            _paused = false;

            GameManager.Instance.Server_Resume();
            if (GameManager.Instance.GameState.Value.Phase != GamePhase.Playing)
                return;

            _changedPhase = true;
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
                return _serverConfig.Offside.BlueTeam;
            else
                return _serverConfig.Offside.RedTeam;
        }

        private static bool IsHighStick(PlayerTeam team) {
            if (!IsHighStickEnabled(team) || !_isHighStickActive[team])
                return false;

            return true;
        }

        private static bool IsHighStickEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return _serverConfig.HighStick.BlueTeam;
            else
                return _serverConfig.HighStick.RedTeam;
        }

        private static bool IsIcing(PlayerTeam team) {
            if (!IsIcingEnabled(team))
                return false;

            return _isIcingActive[team];
        }

        private static bool IsIcingEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return _serverConfig.Icing.BlueTeam;
            else
                return _serverConfig.Icing.RedTeam;
        }

        private static bool IsIcingPossible(PlayerTeam team, bool anyPlayersBehindHashmarks, bool checkPossibleTime = true) {
            if (IsIcingEnabled(team) && _isIcingPossible[team] != null && !anyPlayersBehindHashmarks) {
                PlayerTeam otherTeam = TeamFunc.GetOtherTeam(team);
                List<Zone> otherTeamZones = ZoneFunc.GetTeamZones(otherTeam, true);

                int maxPossibleTime = _serverConfig.Icing.MaxPossibleTime.Values.Max();
                foreach ((PlayerTeam playerTeam, Zone playerZone) in _playersZone.Values) {
                    if (playerTeam == otherTeam && otherTeamZones.Any(x => x == playerZone)) {
                        maxPossibleTime = _serverConfig.Icing.MaxPossibleTime[_puckZoneLastTouched];
                        break;
                    }
                }

                if (!checkPossibleTime || _isIcingPossible[team].ElapsedMilliseconds < maxPossibleTime)
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

            Logging.Log($"Goalie is down : {_lastGoalieStateCollision[team]}.", _serverConfig);
            Logging.Log($"Goalie was last touched : {((double)watch.ElapsedMilliseconds) / 1000d} seconds ago.", _serverConfig);
            if (_lastGoalieStateCollision[team])
                return watch.ElapsedMilliseconds < _serverConfig.GInt.HitNoGoalMilliseconds;

            return watch.ElapsedMilliseconds < _serverConfig.GInt.PushNoGoalMilliseconds;
        }

        private static bool IsGoalieIntEnabled(PlayerTeam team) {
            if (team == PlayerTeam.Blue)
                return _serverConfig.GInt.BlueTeam;
            else
                return _serverConfig.GInt.RedTeam;
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
        /// Function that returns a PlayerBodyV2 instance from a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject, GameObject to use.</param>
        /// <returns>PlayerBodyV2, found PlayerBodyV2 object or null.</returns>
        private static PlayerBodyV2 GetPlayerBodyV2(GameObject gameObject) {
            return gameObject.GetComponent<PlayerBodyV2>();
        }

        /// <summary>
        /// Function that returns the player steam Id that has possession.
        /// </summary>
        /// <returns>String, player steam Id with the possession or an empty string if no one has the puck (or it is challenged).</returns>
        private static string GetPlayerSteamIdInPossession() {
            Dictionary<string, Stopwatch> dict;
            dict = _playersLastTimePuckPossession
                .Where(x => x.Value.ElapsedMilliseconds < _serverConfig.MinPossessionMilliseconds && x.Value.ElapsedMilliseconds > _serverConfig.MaxTippedMilliseconds)
                .ToDictionary(x => x.Key, x => x.Value);

            if (dict.Count > 1) // Puck possession is challenged.
                return "";

            if (dict.Count == 1)
                return dict.First().Key;

            List<string> steamIds = _playersLastTimePuckPossession
                .Where(x => x.Value.ElapsedMilliseconds < _serverConfig.MaxPossessionMilliseconds && x.Value.ElapsedMilliseconds > _serverConfig.MaxTippedMilliseconds)
                .OrderBy(x => x.Value.ElapsedMilliseconds)
                .Select(x => x.Key).ToList();

            if (steamIds.Count != 0)
                return steamIds.First();

            return "";
        }
        
        private static bool PuckIsTipped(string playerSteamId) {
            if (!_playersCurrentPuckTouch.TryGetValue(playerSteamId, out Stopwatch currentPuckTouchWatch))
                return false;

            if (!_lastTimeOnCollisionExitWasCalled.TryGetValue(playerSteamId, out Stopwatch lastPuckExitWatch))
                return false;

            if (currentPuckTouchWatch.ElapsedMilliseconds - lastPuckExitWatch.ElapsedMilliseconds < _serverConfig.MaxTippedMilliseconds)
                return true;

            return false;
        }

        private static void ResetAssists(PlayerTeam team) {
            try {
                NetworkList<NetworkObjectCollision> buffer = GetPuckBuffer();
                if (buffer == null) {
                    Logging.LogError($"Buffer field is null !!!", _serverConfig);
                    return;
                }

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
                Logging.LogError($"Error in ResetAssists.\n{ex}", _serverConfig);
            }
        }

        private static NetworkList<NetworkObjectCollision> GetPuckBuffer(Puck puck = null) {
            if (puck == null) {
                puck = PuckManager.Instance.GetPuck();
                if (!puck)
                    return null;
            }

            return GetPrivateField<NetworkList<NetworkObjectCollision>>(typeof(NetworkObjectCollisionBuffer), puck.NetworkObjectCollisionBuffer, "buffer");
        }

        internal static T GetPrivateField<T>(Type typeContainingField, object instanceOfType, string fieldName) {
            return (T)typeContainingField.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instanceOfType);
        }
        #endregion

        #region Events
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
                Logging.LogError($"Error in Event_OnSceneLoaded.\n{ex}", _clientConfig);
            }
        }

        /*/// <summary>
                            /// Method called when the client has started on the client-side.
                            /// Used to register to load assets.
                            /// </summary>
                            /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStarted(Dictionary<string, object> message) {
            if (ServerFunc.IsDedicatedServer() || NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
                return;

            //Logging.Log("Event_Client_OnClientStarted", _clientConfig);

            try {
                LoadAssets();
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_Client_OnClientStarted.\n{ex}");
            }
        }*/

        /// <summary>
        /// Method called when the client has stopped on the client-side.
        /// Used to reset the config so that it doesn't carry over between servers.
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStopped(Dictionary<string, object> message) {
            //Logging.Log("Event_Client_OnClientStopped", _clientConfig);

            try {
                _serverHasResponded = false;

                if (_refSignalsBlueTeam == null && _refSignalsRedTeam == null && _sounds == null)
                    return;

                ScoreboardModifications(false);

                if (_sounds != null) {
                    if (!string.IsNullOrEmpty(_currentMusicPlaying)) {
                        _sounds.Stop(_currentMusicPlaying);
                        _currentMusicPlaying = "";
                    }

                    _sounds.DestroyGameObjects();
                    _sounds = null;
                }

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
                Logging.LogError($"Error in Event_Client_OnClientStopped.\n{ex}", _clientConfig);
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

                NetworkCommunication.SendDataToAll(SOG + playerSteamId, _sog[playerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
            }
            else {
                if (!_savePerc.TryGetValue(playerSteamId, out var _))
                    _savePerc.Add(playerSteamId, (0, 0));

                NetworkCommunication.SendDataToAll(SAVEPERC + playerSteamId, _savePerc[playerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
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

            Logging.Log("Event_OnClientConnected", _serverConfig);

            try {
                if (NetworkManager.Singleton != null && !_hasRegisteredWithNamedMessageHandler) {
                    Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT}.", _serverConfig);
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT, ReceiveData);
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
                    clientSteamId = PlayerFunc.Players_ClientId_SteamId[clientId];
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(PlayerFunc.Players_ClientId_SteamId)}.", _serverConfig);
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
                _lastTimeOnCollisionExitWasCalled.Remove(clientSteamId);
                _sog.Remove(clientSteamId);
                _savePerc.Remove(clientSteamId);

                PlayerFunc.Players_ClientId_SteamId.Remove(clientId);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientDisconnected.\n{ex}", _serverConfig);
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
                    //Logging.Log("ReceiveData", _clientConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _clientConfig);
                }
                else {
                    //Logging.Log("ReceiveData", _serverConfig);
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, _serverConfig);
                }

                switch (dataName) {
                    case Constants.MOD_NAME + "_" + nameof(MOD_VERSION): // CLIENT-SIDE : Mod version check, kick if client and server versions are not the same.
                        _serverHasResponded = true;
                        if (MOD_VERSION == dataStr) // TODO : Maybe add a chat message and a 3-5 sec wait.
                            break;
                        else if (OLD_MOD_VERSION == dataStr) {
                            _addServerModVersionOutOfDateMessage = true;
                            break;
                        }

                        _askForKick = true;
                        break;

                    case Sounds.PLAY_SOUND: // CLIENT-SIDE : Play sound.
                        if (_sounds == null)
                            break;
                        if (_sounds.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _sounds.", _clientConfig);
                            foreach (string error in _sounds.Errors)
                                Logging.LogError(error, _clientConfig);
                        }

                        int? seed = null;
                        string[] dataStrSplitted = dataStr.Split(';');

                        if (int.TryParse(dataStrSplitted[1], out int _seed))
                            seed = _seed;

                        bool isFaceoffMusic = false;
                        float delay = 0;
                        if (dataStrSplitted[0] == Sounds.FACEOFF_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FaceoffMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (dataStrSplitted[0] == Sounds.FACEOFF_MUSIC_DELAYED) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FaceoffMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (dataStrSplitted[0] == Sounds.BLUE_GOAL_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.BlueGoalMusicList, seed);
                            _sounds.Play(_currentMusicPlaying, Sounds.MUSIC, 2.25f);
                        }
                        else if (dataStrSplitted[0] == Sounds.RED_GOAL_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.RedGoalMusicList, seed);
                            _sounds.Play(_currentMusicPlaying, Sounds.MUSIC, 2.25f);
                        }
                        else if (dataStrSplitted[0] == Sounds.BETWEEN_PERIODS_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.BetweenPeriodsMusicList, seed);
                            _sounds.Play(_currentMusicPlaying, Sounds.MUSIC, 1.5f);
                        }
                        else if (dataStrSplitted[0] == Sounds.WARMUP_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.WarmupMusicList, seed);
                            _sounds.Play(_currentMusicPlaying, Sounds.MUSIC, 0, true);
                        }
                        else if (dataStrSplitted[0] == Sounds.LAST_MINUTE_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.LastMinuteMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (dataStrSplitted[0] == Sounds.FIRST_FACEOFF_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FirstFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (dataStrSplitted[0] == Sounds.SECOND_FACEOFF_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.SecondFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (dataStrSplitted[0] == Sounds.LAST_MINUTE_MUSIC_DELAYED) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.LastMinuteMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (dataStrSplitted[0] == Sounds.FIRST_FACEOFF_MUSIC_DELAYED) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FirstFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (dataStrSplitted[0] == Sounds.SECOND_FACEOFF_MUSIC_DELAYED) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.SecondFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (dataStrSplitted[0] == Sounds.GAMEOVER_MUSIC) {
                            _currentMusicPlaying = Sounds.GetRandomSound(_sounds.GameOverMusicList, seed);
                            _sounds.Play(_currentMusicPlaying, Sounds.MUSIC, 0.5f);
                        }
                        else if (dataStrSplitted[0] == Sounds.WHISTLE)
                            _sounds.Play(Sounds.WHISTLE, "");

                        if (isFaceoffMusic) {
                            if (string.IsNullOrEmpty(_currentMusicPlaying))
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FaceoffMusicList, seed);
                            _sounds.Play(_currentMusicPlaying, Sounds.MUSIC, delay);
                        }
                        break;

                    case Sounds.STOP_SOUND: // CLIENT-SIDE : Stop sound.
                        if (_sounds == null)
                            break;
                        if (_sounds.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _sounds.", _clientConfig);
                            foreach (string error in _sounds.Errors)
                                Logging.LogError(error, _clientConfig);
                        }

                        if (dataStr == Sounds.MUSIC) {
                            if (!string.IsNullOrEmpty(_currentMusicPlaying))
                                _sounds.Stop(_currentMusicPlaying);
                        }
                        else if (dataStr == Sounds.ALL)
                            _sounds.StopAll();

                        _currentMusicPlaying = "";
                        break;

                    case RefSignals.SHOW_SIGNAL_BLUE: // CLIENT-SIDE : Show blue team ref signal in the UI.
                        if (_refSignalsBlueTeam == null)
                            break;

                        if (_refSignalsBlueTeam.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsBlueTeam.", _clientConfig);
                            foreach (string error in _refSignalsBlueTeam.Errors)
                                Logging.LogError(error, _clientConfig);
                        }
                        else {
                            if (_clientConfig.TeamColor2DRefs)
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
                            Logging.LogError("There was an error when initializing _refSignalsRedTeam.", _clientConfig);
                            foreach (string error in _refSignalsRedTeam.Errors)
                                Logging.LogError(error, _clientConfig);
                        }
                        else {
                            if (_clientConfig.TeamColor2DRefs)
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

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER, _serverConfig);

                        if (_sog.Count != 0) {
                            string batchSOG = "";
                            foreach (string key in new List<string>(_sog.Keys))
                                batchSOG += key + ';' + _sog[key].ToString() + ';';
                            batchSOG = batchSOG.Remove(batchSOG.Length - 1);
                            NetworkCommunication.SendData(BATCH_SOG, batchSOG, clientId, Constants.FROM_SERVER, _serverConfig);
                        }

                        if (_savePerc.Count != 0) {
                            string batchSavePerc = "";
                            foreach (string key in new List<string>(_savePerc.Keys))
                                batchSavePerc += key + ';' + _savePerc[key].ToString() + ';';
                            batchSavePerc = batchSavePerc.Remove(batchSavePerc.Length - 1);
                            NetworkCommunication.SendData(BATCH_SAVEPERC, batchSavePerc, clientId, Constants.FROM_SERVER, _serverConfig);
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
                        if (dataName.StartsWith(SOG)) {
                            string playerSteamId = dataName.Replace(SOG, "");
                            if (string.IsNullOrEmpty(playerSteamId))
                                return;

                            ReceiveData_SOG(playerSteamId, dataStr);
                        }

                        if (dataName.StartsWith(SAVEPERC)) {
                            string playerSteamId = dataName.Replace(SAVEPERC, "");
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

        private static void StopBlueRefSignals(string dataStr) {
            if (_refSignalsBlueTeam == null)
                return;

            if (_refSignalsBlueTeam.Errors.Count != 0) {
                Logging.LogError("There was an error when initializing _refSignalsBlueTeam.", _clientConfig);
                foreach (string error in _refSignalsBlueTeam.Errors)
                    Logging.LogError(error, _clientConfig);
            }
            else {
                if (dataStr == RefSignals.ALL)
                    _refSignalsBlueTeam.StopAllSignals();
                else if (_clientConfig.TeamColor2DRefs)
                    _refSignalsBlueTeam.StopSignal(dataStr + "_" + RefSignals.BLUE);
                else
                    _refSignalsBlueTeam.StopSignal(dataStr);
            }
        }

        private static void StopRedRefSignals(string dataStr) {
            if (_refSignalsRedTeam == null)
                return;

            if (_refSignalsRedTeam.Errors.Count != 0) {
                Logging.LogError("There was an error when initializing _refSignalsRedTeam.", _clientConfig);
                foreach (string error in _refSignalsRedTeam.Errors)
                    Logging.LogError(error, _clientConfig);
            }
            else {
                if (dataStr == RefSignals.ALL)
                    _refSignalsRedTeam.StopAllSignals();
                else if (_clientConfig.TeamColor2DRefs)
                    _refSignalsRedTeam.StopSignal(dataStr + "_" + RefSignals.RED);
                else
                    _refSignalsRedTeam.StopSignal(dataStr);
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
            string[] dataStrSplitted = RemoveWhitespace(dataStr.Replace("(", "").Replace(")", "")).Split(',');
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

        private static void ServerManager_Update_IcingLogic(PlayerTeam team, Puck puck, Dictionary<PlayerTeam, bool?> icingHasToBeWarned, bool anyPlayersBehindHashmarks) {
            if (!IsIcingEnabled(team))
                return;

            if (!IsIcingPossible(team, anyPlayersBehindHashmarks, false) && _isIcingActive[team]) {
                _isIcingActive[team] = false;
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, team), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                SendChat(Rule.Icing, team, true, true);
            }
            else if (!_isIcingActive[team] && IsIcingPossible(team, anyPlayersBehindHashmarks) && _puckZone == ZoneFunc.GetTeamZones(TeamFunc.GetOtherTeam(team))[1]) {
                _puckLastStateBeforeCall[Rule.Icing] = (puck.Rigidbody.transform.position, _puckZone);
                _isIcingActiveTimers[team].Change(_serverConfig.Icing.MaxActiveTime, Timeout.Infinite);
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

                Logging.Log($"Enabling...", _serverConfig, true);

                _harmony.PatchAll();

                Logging.Log($"Enabled.", _serverConfig, true);

                NetworkCommunication.AddToNotLogList(DATA_NAMES_TO_IGNORE);

                if (ServerFunc.IsDedicatedServer()) {
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null) {
                        Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT}.", _serverConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;
                    }

                    Logging.Log("Setting server sided config.", _serverConfig, true);
                    _serverConfig = ServerConfig.ReadConfig();
                }
                else {
                    Logging.Log("Setting client sided config.", _serverConfig, true);
                    _clientConfig = ClientConfig.ReadConfig();

                    //_getStickLocation = new InputAction(binding: "<keyboard>/#(o)");
                    //_getStickLocation.Enable();
                }

                Logging.Log("Subscribing to events.", _serverConfig, true);
                
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                }
                else {
                    //EventManager.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.AddEventListener("Event_OnSceneLoaded", Event_OnSceneLoaded);
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
                NetworkCommunication.RemoveFromNotLogList(DATA_NAMES_TO_IGNORE);
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.RemoveEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT);
                }
                else {
                    //EventManager.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.RemoveEventListener("Event_OnSceneLoaded", Event_OnSceneLoaded);
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    Event_Client_OnClientStopped(new Dictionary<string, object>());
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER);
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _serverHasResponded = false;

                //_getStickLocation.Disable();

                ScoreboardModifications(false);

                if (_sounds != null) {
                    if (!string.IsNullOrEmpty(_currentMusicPlaying)) {
                        _sounds.Stop(_currentMusicPlaying);
                        _currentMusicPlaying = "";
                    }

                    _sounds.DestroyGameObjects();
                    _sounds = null;
                }

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

        /// <summary>
        /// Method that loads the assets for the client-side (sounds and ref UI).
        /// </summary>
        private static void LoadAssets() {
            if (_sounds == null) {
                GameObject soundsGameObject = new GameObject(Constants.MOD_NAME + "_Sounds");
                _sounds = soundsGameObject.AddComponent<Sounds>();
            }
            _sounds.LoadSounds();

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

        /// <summary>
        /// Method used to modify the scoreboard to add additional stats.
        /// </summary>
        /// <param name="enable">Bool, true if new stats scoreboard has to added to the scoreboard. False if they need to be removed.</param>
        private static void ScoreboardModifications(bool enable) {
            if (UIScoreboard.Instance == null)
                return;

            VisualElement scoreboardContainer = GetPrivateField<VisualElement>(typeof(UIScoreboard), UIScoreboard.Instance, "container");

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

            foreach (var kvp in GetPrivateField<Dictionary<Player, VisualElement>>(typeof(UIScoreboard), UIScoreboard.Instance, "playerVisualElementMap")) {
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
                NetworkCommunication.SendDataToAll(SOG + playerSteamId, sog.ToString(), Constants.FROM_SERVER, _serverConfig);
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
            Player goalie = Codebase.PlayerFunc.GetOtherTeamGoalie(team);
            if (goalie == null)
                return;

            string _goaliePlayerSteamId = goalie.SteamId.Value.ToString();
            if (!_savePerc.TryGetValue(_goaliePlayerSteamId, out var _savePercValue)) {
                _savePerc.Add(_goaliePlayerSteamId, (0, 0));
                _savePercValue = (0, 0);
            }

            (int saves, int sog) = _savePerc[_goaliePlayerSteamId] = saveWasCounted ? (--_savePercValue.Saves, _savePercValue.Shots) : (_savePercValue.Saves, ++_savePercValue.Shots);

            NetworkCommunication.SendDataToAll(SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
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

        public static string RemoveWhitespace(string input) {
            return new string(input
                .Where(c => !Char.IsWhiteSpace(c))
                .ToArray());
        }

        private static string GetGoalieSavePerc(int saves, int shots) {
            if (shots == 0)
                return "0.000";

            return (((double)saves) / ((double)shots)).ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static void GetAllLayersName() {
            for (int i = 0; i < 32; i++) {
                Logging.Log($"Layer {i} name : {LayerMask.LayerToName(i)}.", _serverConfig, true);
            }
        }

        private static bool AreBothNegativeOrPositive(float num1, float num2) {
            return (num1 <= 0 && num2 <= 0) || (num1 >= 0 && num2 >= 0);
        }
        #endregion
    }

    public enum Rule {
        [Description("OFFSIDE"), Category("ToString")]
        Offside,
        [Description("ICING"), Category("ToString")]
        Icing,
        [Description("HIGH STICK"), Category("ToString")]
        HighStick,
        [Description("GOALIE INT"), Category("ToString")]
        GoalieInt,
    }

    internal class SaveCheck {
        internal bool HasToCheck { get; set; } = false;
        internal string ShooterSteamId { get; set; } = "";
        internal int FramesChecked { get; set; } = 0;
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
