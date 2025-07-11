using HarmonyLib;
using oomtm450PuckMod_Ruleset.Configs;
using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
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
        private static readonly string MOD_VERSION = "V0.14.0DEV14";

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

        private const string RESET_SOG = Constants.MOD_NAME + "RESETSOG";

        private const string SAVEPERC = Constants.MOD_NAME + "SAVEPERC";

        private const string RESET_SAVEPERC = Constants.MOD_NAME + "RESETSAVEPERC";

        private const string SOG_HEADER_LABEL_NAME = "SOGHeaderLabel";

        private const string SOG_LABEL = "SOGLabel";

        private const string ASK_SERVER_FOR_DATA = Constants.MOD_NAME + "ASKDATA";
        #endregion

        #region Fields
        /// <summary>
        /// Harmony, harmony instance to patch the Puck's code.
        /// </summary>
        private static readonly Harmony _harmony = new Harmony(Constants.MOD_NAME);

        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        internal static ServerConfig _serverConfig = new ServerConfig();

        /// <summary>
        /// ServerConfig, config set by the client.
        /// </summary>
        internal static ClientConfig _clientConfig = new ClientConfig();

        private static readonly LockDictionary<string, (PlayerTeam Team, bool IsOffside)> _isOffside = new LockDictionary<string, (PlayerTeam, bool)>();

        private static readonly LockDictionary<PlayerTeam, bool> _callOffHighStick = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static readonly LockDictionary<PlayerTeam, Stopwatch> _isIcingPossible = new LockDictionary<PlayerTeam, Stopwatch> {
            { PlayerTeam.Blue, null },
            { PlayerTeam.Red, null },
        };

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
            { Rule.Offside, (Vector3.zero, Zone.BlueTeam_Center) },
            { Rule.Icing, (Vector3.zero, Zone.BlueTeam_Center) },
            { Rule.HighStick, (Vector3.zero, Zone.BlueTeam_Center) },
            { Rule.GoalieInt, (Vector3.zero, Zone.BlueTeam_Center) },
        };

        private static Zone _puckZone = Zone.BlueTeam_Center;

        private static readonly LockDictionary<string, (PlayerTeam Team, Zone Zone)> _playersZone = new LockDictionary<string, (PlayerTeam, Zone)>();

        private static readonly LockDictionary<string, Stopwatch> _playersCurrentPuckTouch = new LockDictionary<string, Stopwatch>();

        private static readonly LockDictionary<string, Stopwatch> _playersLastTimePuckPossession = new LockDictionary<string, Stopwatch>();

        private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

        //private static InputAction _getStickLocation;

        private static readonly LockDictionary<string, Stopwatch> _lastTimeOnCollisionExitWasCalled = new LockDictionary<string, Stopwatch>();

        private static bool _changedPhase = false;

        private static int _periodTimeRemaining = 0;

        private static PlayerTeam _lastPlayerOnPuckTeam = PlayerTeam.Blue;

        private static readonly LockDictionary<PlayerTeam, string> _lastPlayerOnPuckSteamId = new LockDictionary<PlayerTeam, string> {
            { PlayerTeam.Blue, "" },
            { PlayerTeam.Red, "" },
        };

        private static readonly LockDictionary<PlayerTeam, string> _lastPlayerOnPuckTipIncludedSteamId = new LockDictionary<PlayerTeam, string> {
            { PlayerTeam.Blue, "" },
            { PlayerTeam.Red, "" },
        };

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

        private static bool _hasRegisteredWithNamedMessageHandler = false;

        private static PuckRaycast _puckRaycast;

        private static readonly LockDictionary<PlayerTeam, SaveCheck> _checkIfPuckWasSaved = new LockDictionary<PlayerTeam, SaveCheck> {
            { PlayerTeam.Blue, new SaveCheck() },
            { PlayerTeam.Red, new SaveCheck() },
        };

        private static bool _hasPlayedLastMinuteMusic = false;

        private static bool _hasPlayedFirstFaceoffMusic = false;

        private static bool _hasPlayedSecondFaceoffMusic = false;

        // Client-side and server-side.
        private static readonly LockDictionary<string, int> _sog = new LockDictionary<string, int>();

        private static readonly LockDictionary<string, (int Saves, int Shots)> _savePerc = new LockDictionary<string, (int Saves, int Shots)>();

        // Client-side.
        private static Sounds _sounds = null;

        private static RefSignals _refSignalsBlueTeam = null;

        private static RefSignals _refSignalsRedTeam = null;

        private static string _currentMusicPlaying = "";

        private static readonly List<string> _hasUpdatedUIScoreboard = new List<string>(); // TODO : Clear if player quits server

        private static readonly LockDictionary<string, Label> _sogLabels = new LockDictionary<string, Label>(); // TODO : Clear if player quits server

        private static DateTime _lastDateTimeAskData = DateTime.MinValue;

        private static bool _serverHasResponded = false;

        // Barrier collider, position 0 -19 0 is realistic.
        #endregion

        #region Properties
        /// <summary>
        /// FaceoffSpot, where the next faceoff has to be taken.
        /// </summary>
        internal static FaceoffSpot NextFaceoffSpot { get; set; } = FaceoffSpot.Center;
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

                        PlayerTeam playerOtherTeam = TeamFunc.GetOtherTeam(playerBody.Player.Team.Value);
                        if (IsIcingPossible(playerOtherTeam)) {
                            if (playerBody.Player.Role.Value != PlayerRole.Goalie) {
                                if (_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var result)) {
                                    if (result.Zone != ZoneFunc.GetTeamZones(playerBody.Player.Team.Value)[1]) {
                                        if (IsIcing(playerOtherTeam)) {
                                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerOtherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerOtherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                                        }
                                        ResetIcings();
                                    }
                                }
                            }

                            if (IsIcing(playerOtherTeam)) {
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerOtherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerOtherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            }
                            ResetIcings();
                        }
                        else if (IsIcingPossible(playerBody.Player.Team.Value)) {
                            if (_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var result)) {
                                if (ZoneFunc.GetTeamZones(playerOtherTeam, true).Any(x => x == result.Zone)) {
                                    if (IsIcing(playerBody.Player.Team.Value)) {
                                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerBody.Player.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                                        UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerBody.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                                    }
                                    ResetIcings();
                                }
                            }
                        }

                        if (_puckRaycast.PuckIsGoingToNet[playerBody.Player.Team.Value] && playerBody.Player.Role.Value == PlayerRole.Goalie) {
                            string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[TeamFunc.GetOtherTeam(playerBody.Player.Team.Value)];
                            if (!string.IsNullOrEmpty(shooterSteamId))
                                _checkIfPuckWasSaved[playerBody.Player.Team.Value] = new SaveCheck { HasToCheck = true, ShooterSteamId = shooterSteamId };
                        }
                        return;
                    }

                    if (!stick.Player)
                        return;

                    string currentPlayerSteamId = stick.Player.SteamId.Value.ToString();

                    //Logging.Log($"Puck was hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (enter)!", _serverConfig);

                    // Start tipped timer.
                    if (!_playersCurrentPuckTouch.TryGetValue(currentPlayerSteamId, out Stopwatch watch)) {
                        watch = new Stopwatch();
                        watch.Start();
                        _playersCurrentPuckTouch.Add(currentPlayerSteamId, watch);
                    }

                    string lastPlayerOnPuckTipIncludedSteamId = _lastPlayerOnPuckTipIncludedSteamId[_lastPlayerOnPuckTeam];

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
                        if (stick.Player.Role.Value != PlayerRole.Goalie && puck.Rigidbody.transform.position.y > _serverConfig.HighStickHeight + stick.Player.PlayerBody.Rigidbody.transform.position.y) {
                            _isHighStickActiveTimers.TryGetValue(stick.Player.Team.Value, out Timer highStickTimer);

                            highStickTimer.Change(_serverConfig.HighStickMaxMilliseconds, Timeout.Infinite);
                            if (!IsHighStick(stick.Player.Team.Value)) {
                                _isHighStickActive[stick.Player.Team.Value] = true;
                                _puckLastStateBeforeCall[Rule.HighStick] = (puck.Rigidbody.transform.position, _puckZone);
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, stick.Player.Team.Value), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                                UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM");
                            }
                        }
                        else if (puck.IsGrounded) {
                            if (IsHighStick(stick.Player.Team.Value)) {
                                Faceoff.SetNextFaceoffPosition(stick.Player.Team.Value, false, _puckLastStateBeforeCall[Rule.HighStick]);
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, stick.Player.Team.Value), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, stick.Player.Team.Value), RefSignals.HIGHSTICK_REF, Constants.FROM_SERVER, _serverConfig);
                                UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED");

                                _isHighStickActiveTimers.TryGetValue(stick.Player.Team.Value, out Timer highStickTimer);
                                highStickTimer.Change(Timeout.Infinite, Timeout.Infinite);

                                DoFaceoff();
                            }
                        }
                    }

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);
                    if (IsHighStick(otherTeam)) {
                        _isHighStickActive[otherTeam] = false;
                        NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, otherTeam), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                        UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                    }

                    if (_puckRaycast.PuckIsGoingToNet[stick.Player.Team.Value] && stick.Player.Role.Value == PlayerRole.Goalie) {
                        string shooterSteamId = _lastPlayerOnPuckTipIncludedSteamId[otherTeam];
                        if (!string.IsNullOrEmpty(shooterSteamId))
                            _checkIfPuckWasSaved[stick.Player.Team.Value] = new SaveCheck { HasToCheck = true, ShooterSteamId = shooterSteamId };
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionEnter_Patch Postfix().\n{ex}");
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
                    if (!stick)
                        return;

                    string playerSteamId = stick.Player.SteamId.Value.ToString();

                    if (!PuckIsTipped(playerSteamId)) {
                        _lastPlayerOnPuckTeam = stick.Player.Team.Value;
                        if (stick.Player.Role.Value != PlayerRole.Goalie)
                            ResetAssists(TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam));
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = playerSteamId;
                    }

                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = playerSteamId;

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
                        Faceoff.SetNextFaceoffPosition(stick.Player.Team.Value, false, _puckLastStateBeforeCall[Rule.Offside]);
                        UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED");
                        DoFaceoff();
                    }

                    // Icing logic.
                    if (IsIcing(otherTeam)) {
                        if (stick.Player.PlayerPosition.Role != PlayerRole.Goalie) {
                            Faceoff.SetNextFaceoffPosition(otherTeam, true, _puckLastStateBeforeCall[Rule.Icing]);
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                            ResetIcings();
                            DoFaceoff();
                        }
                        else if (stick.Player.PlayerPosition.Role == PlayerRole.Goalie) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, otherTeam), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            ResetIcings();
                        }
                    }
                    else {
                        if (IsIcing(stick.Player.Team.Value)) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, stick.Player.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                        }
                        ResetIcings();
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Puck_OnCollisionStay_Patch Postfix().\n{ex}");
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

                    string currentPlayerSteamId = stick.Player.SteamId.Value.ToString();

                    if (!_lastTimeOnCollisionExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionWatch)) {
                        lastTimeCollisionWatch = new Stopwatch();
                        lastTimeCollisionWatch.Start();
                        _lastTimeOnCollisionExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionWatch);
                    }

                    lastTimeCollisionWatch.Restart();

                    if (!PuckIsTipped(currentPlayerSteamId)) {
                        _lastPlayerOnPuckTeam = stick.Player.Team.Value;
                        if (stick.Player.Role.Value != PlayerRole.Goalie)
                            ResetAssists(TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam));
                        _lastPlayerOnPuckSteamId[stick.Player.Team.Value] = currentPlayerSteamId;

                        Puck puck = PuckManager.Instance.GetPuck();
                        if (puck)
                            _puckLastStateBeforeCall[Rule.GoalieInt] = _puckLastStateBeforeCall[Rule.Offside] = (puck.Rigidbody.transform.position, _puckZone);
                    }

                    _lastPlayerOnPuckTipIncludedSteamId[stick.Player.Team.Value] = currentPlayerSteamId;

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
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}");
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

                    if (!playerBody) {
                        /*PlayerLegPad playerLegPad = GetPlayerLegPad(collision.gameObject);
                        if (!playerLegPad)
                            return;

                        playerBody = playerLegPad.GetComponentInParent<PlayerBodyV2>();
                        Logging.Log($"This is a pad !!!", _serverConfig);
                        if (playerBody == null || !playerBody)
                            return;*/
                        return;
                    }

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
                    if (playerBody.Player.Role.Value == PlayerRole.Goalie)
                        goalie = playerBody.Player;
                    else
                        goalie = lastPlayerHit;

                    (double startX, double endX) = (0, 0);
                    (double startZ, double endZ) = (0, 0);
                    if (goalie.Team.Value == PlayerTeam.Blue) {
                        (startX, endX) = ZoneFunc.ICE_X_POSITIONS[ArenaElement.BlueTeam_BluePaint];
                        (startZ, endZ) = ZoneFunc.ICE_Z_POSITIONS[ArenaElement.BlueTeam_BluePaint];
                    }
                    else {
                        (startX, endX) = ZoneFunc.ICE_X_POSITIONS[ArenaElement.RedTeam_BluePaint];
                        (startZ, endZ) = ZoneFunc.ICE_Z_POSITIONS[ArenaElement.RedTeam_BluePaint];
                    }

                    bool goalieIsInHisCrease = true;
                    if (goalie.PlayerBody.Rigidbody.transform.position.x - _serverConfig.GoalieRadius < startX ||
                        goalie.PlayerBody.Rigidbody.transform.position.x + _serverConfig.GoalieRadius > endX ||
                        goalie.PlayerBody.Rigidbody.transform.position.z - _serverConfig.GoalieRadius < startZ ||
                        goalie.PlayerBody.Rigidbody.transform.position.z + _serverConfig.GoalieRadius > endZ)
                        goalieIsInHisCrease = false;

                    PlayerTeam goalieOtherTeam = TeamFunc.GetOtherTeam(goalie.Team.Value);

                    bool goalieDown = goalie.PlayerBody.IsSlipping || goalie.PlayerBody.HasSlipped;
                    _lastGoalieStateCollision[goalieOtherTeam] = goalieDown;

                    if (goalieDown || (force > _serverConfig.GIntCollisionForceThreshold && goalieIsInHisCrease)) {
                        if (!_goalieIntTimer.TryGetValue(goalieOtherTeam, out Stopwatch watch))
                            return;

                        if (watch == null) {
                            watch = new Stopwatch();
                            _goalieIntTimer[goalieOtherTeam] = watch;
                        }

                        watch.Restart();
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in PlayerBodyV2_OnCollisionEnter_Patch Postfix().\n{ex}");
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
                        NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, _currentMusicPlaying, Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.RedScore) {
                        _currentMusicPlaying = Sounds.RED_GOAL_MUSIC;
                        NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, _currentMusicPlaying, Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.PeriodOver) {
                        _currentMusicPlaying = Sounds.BETWEEN_PERIODS_MUSIC;
                        NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, _currentMusicPlaying, Constants.FROM_SERVER, _serverConfig);
                    }
                    else if (phase == GamePhase.FaceOff || phase == GamePhase.Warmup || phase == GamePhase.GameOver || phase == GamePhase.PeriodOver) {
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
                            _puckLastStateBeforeCall[key] = (Vector3.zero, Zone.BlueTeam_Center);

                        ResetOffsides();
                        ResetHighSticks();
                        ResetIcings();
                        ResetGoalieInt();

                        // Reset puck was saved states.
                        foreach (PlayerTeam key in new List<PlayerTeam>(_checkIfPuckWasSaved.Keys))
                            _checkIfPuckWasSaved[key] = new SaveCheck();

                        _puckZone = ZoneFunc.GetZone(NextFaceoffSpot);

                        _lastPlayerOnPuckTeam = PlayerTeam.Blue;
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckSteamId.Keys))
                            _lastPlayerOnPuckSteamId[key] = "";

                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckTipIncludedSteamId.Keys))
                            _lastPlayerOnPuckTipIncludedSteamId[key] = "";

                        NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL_BLUE, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);
                        NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL_RED, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);
                    }

                    if (!_changedPhase) {
                        if (phase == GamePhase.FaceOff && string.IsNullOrEmpty(_currentMusicPlaying)) {
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

                            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, _currentMusicPlaying, Constants.FROM_SERVER, _serverConfig);
                            _currentMusicPlaying = Sounds.FACEOFF_MUSIC;
                            return true;
                        }

                        if (phase == GamePhase.GameOver)
                            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.GAMEOVER_MUSIC, Constants.FROM_SERVER, _serverConfig);
                        else if (phase == GamePhase.Warmup)
                            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.WARMUP_MUSIC, Constants.FROM_SERVER, _serverConfig);

                        return true;
                    }

                    if (phase == GamePhase.Playing) {
                        _changedPhase = false;
                        time = _periodTimeRemaining;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Prefix().\n{ex}");
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
                        if (NextFaceoffSpot == FaceoffSpot.Center)
                            return;

                        Vector3 dot = Faceoff.GetFaceoffDot(NextFaceoffSpot);

                        List<Player> players = PlayerManager.Instance.GetPlayers();
                        foreach (Player player in players)
                            PlayerFunc.TeleportOnFaceoff(player, dot, NextFaceoffSpot);

                        return;
                    }
                    else if (phase == GamePhase.Playing) {
                        NetworkCommunication.SendDataToAll(Sounds.STOP_SOUND, Sounds.MUSIC, Constants.FROM_SERVER, _serverConfig);
                        _currentMusicPlaying = "";
                        return;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_SetPhase_Patch Postfix().\n{ex}");
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
                    if (!ServerFunc.IsDedicatedServer() || isReplay || (GameManager.Instance.Phase != GamePhase.Playing && GameManager.Instance.Phase != GamePhase.FaceOff))
                        return true;

                    Vector3 dot = Faceoff.GetFaceoffDot(NextFaceoffSpot);
                    position = new Vector3(dot.x, 1.1f, dot.z);
                    NextFaceoffSpot = FaceoffSpot.Center;
                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Prefix().\n{ex}");
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
                    Logging.LogError($"Error in PuckManager_Server_SpawnPuck_Patch Postfix().\n{ex}");
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
                Zone oldZone = Zone.BlueTeam_Center;
                Dictionary<PlayerTeam, bool?> icingHasToBeWarned = new Dictionary<PlayerTeam, bool?> {
                    {PlayerTeam.Blue, null},
                    {PlayerTeam.Red, null},
                };

                try {
                    foreach (PlayerTeam callOffHighStickTeam in new List<PlayerTeam>(_callOffHighStick.Keys)) {
                        if (_callOffHighStick[callOffHighStickTeam]) {
                            _callOffHighStick[callOffHighStickTeam] = false;
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, callOffHighStickTeam), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                            UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {callOffHighStickTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                        }
                    }

                    if (_paused) {
                        if (_doFaceoff)
                            PostDoFaceoff();
                        else
                            return true;
                    }
                    else if (_doFaceoff)
                        PostDoFaceoff();

                    players = PlayerManager.Instance.GetPlayers();
                    puck = PuckManager.Instance.GetPuck();

                    if (players.Count == 0 || puck == null || _paused)
                        return true;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 1.\n{ex}");
                }
                try {
                    oldZone = _puckZone;
                    _puckZone = ZoneFunc.GetZone(puck.Rigidbody.transform.position, _puckZone, PUCK_RADIUS);

                    // Icing logic.
                    ServerManager_Update_IcingLogic(PlayerTeam.Blue, puck, icingHasToBeWarned);
                    ServerManager_Update_IcingLogic(PlayerTeam.Red, puck, icingHasToBeWarned);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 2.\n{ex}");
                }
                try {
                    string playerWithPossessionSteamId = GetPlayerSteamIdInPossession();
                    Dictionary<PlayerTeam, bool> isTeamOffside = new Dictionary<PlayerTeam, bool> {
                        { PlayerTeam.Blue, IsOffside(PlayerTeam.Blue) },
                        { PlayerTeam.Red, IsOffside(PlayerTeam.Red) },
                    };

                    Dictionary<Player, float> dictPlayersZPositionsForDeferredIcing = new Dictionary<Player, float>();
                    foreach (Player player in players) {
                        if (!PlayerFunc.IsPlayerPlaying(player))
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
                        if (playerZone != otherTeamZones[0] && playerZone != otherTeamZones[1] && _isOffside[playerSteamId].IsOffside)
                            _isOffside[playerSteamId] = (player.Team.Value, false);

                        // Deferred icing logic.
                        if (_serverConfig.DeferredIcing && player.Role.Value != PlayerRole.Goalie) {
                            if (IsIcing(player.Team.Value) && ZoneFunc.IsBehindHashmarks(otherTeam, player.PlayerBody.transform.position, oldPlayerZone, PLAYER_RADIUS))
                                dictPlayersZPositionsForDeferredIcing.Add(player, Math.Abs(player.PlayerBody.transform.position.z));
                            else if (IsIcing(otherTeam) && ZoneFunc.IsBehindHashmarks(player.Team.Value, player.PlayerBody.transform.position, oldPlayerZone, PLAYER_RADIUS)) {
                                dictPlayersZPositionsForDeferredIcing.Add(player, Math.Abs(player.PlayerBody.transform.position.z));
                                Faceoff.SetNextFaceoffPosition(otherTeam, true, _puckLastStateBeforeCall[Rule.Icing]);
                            }
                        }
                    }

                    // Remove offside if the other team entered the zone with the puck.
                    List<Zone> lastPlayerOnPuckTeamZones = ZoneFunc.GetTeamZones(_lastPlayerOnPuckTeam, true);
                    if (oldZone == lastPlayerOnPuckTeamZones[2] && _puckZone == lastPlayerOnPuckTeamZones[0]) {
                        PlayerTeam lastPlayerOnPuckOtherTeam = TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam);
                        foreach (string key in new List<string>(_isOffside.Keys)) {
                            if (_isOffside[key].IsOffside && _isOffside[key].Team == lastPlayerOnPuckOtherTeam)
                                _isOffside[key] = (lastPlayerOnPuckOtherTeam, false);
                        }
                    }

                    // Deferred icing logic.
                    if (dictPlayersZPositionsForDeferredIcing.Count != 0) {
                        Player closestPlayerToEndBoard = dictPlayersZPositionsForDeferredIcing.OrderByDescending(x => x.Value).First().Key;
                        PlayerTeam closestPlayerToEndBoardOtherTeam = TeamFunc.GetOtherTeam(closestPlayerToEndBoard.Team.Value);
                        if (IsIcing(closestPlayerToEndBoard.Team.Value)) {
                            if (icingHasToBeWarned[closestPlayerToEndBoard.Team.Value] == null) {
                                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, closestPlayerToEndBoard.Team.Value), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {closestPlayerToEndBoard.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            }
                            else
                                icingHasToBeWarned[closestPlayerToEndBoard.Team.Value] = false;
                            ResetIcings();
                        }
                        else if (IsIcing(closestPlayerToEndBoardOtherTeam)) {
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {closestPlayerToEndBoardOtherTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                            ResetIcings();
                            DoFaceoff();
                        }
                    }

                    foreach (var kvp in icingHasToBeWarned) {
                        if (kvp.Value != null && (bool)kvp.Value) {
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, kvp.Key), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send show icing signal for client-side UI.
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {kvp.Key.ToString().ToUpperInvariant()} TEAM");
                        }
                    }

                    foreach (var kvp in isTeamOffside) {
                        if (!kvp.Value && IsOffside(kvp.Key))
                            WarnOffside(true, kvp.Key);
                        else if (kvp.Value && !IsOffside(kvp.Key))
                            WarnOffside(false, kvp.Key);
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 3.\n{ex}");
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

                            _lastShotWasCounted[shotPlayerTeam] = true;

                            // Get other team goalie.
                            Player goalie = PlayerFunc.GetOtherTeamGoalie(shotPlayerTeam);
                            if (goalie != null) {
                                string _goaliePlayerSteamId = goalie.SteamId.Value.ToString();
                                if (!_savePerc.TryGetValue(_goaliePlayerSteamId, out var savePercValue)) {
                                    _savePerc.Add(_goaliePlayerSteamId, (0, 0));
                                    savePercValue = (0, 0);
                                }

                                _savePerc[_goaliePlayerSteamId] = (++savePercValue.Saves, ++savePercValue.Shots);

                                NetworkCommunication.SendDataToAll(SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
                            }

                            _checkIfPuckWasSaved[key] = new SaveCheck();
                        }
                        else {
                            if (++saveCheck.FramesChecked > 400)
                                _checkIfPuckWasSaved[key] = new SaveCheck();
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Postfix().\n{ex}");
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

                    PlayerTeam playerTeam = (PlayerTeam)message["team"];
                    playerTeam = TeamFunc.GetOtherTeam(playerTeam);

                    // No goal if offside or high stick or goalie interference.
                    bool isOffside = false, isHighStick = false, isGoalieInt = false;
                    isOffside = IsOffside(playerTeam);
                    isHighStick = IsHighStick(playerTeam);
                    isGoalieInt = IsGoalieInt(playerTeam);

                    if (isOffside || isHighStick || isGoalieInt) {
                        if (isOffside) {
                            Faceoff.SetNextFaceoffPosition(playerTeam, false, _puckLastStateBeforeCall[Rule.Offside]);
                            UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {playerTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                        }
                        else if (isHighStick) {
                            Faceoff.SetNextFaceoffPosition(playerTeam, false, _puckLastStateBeforeCall[Rule.HighStick]);
                            UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {playerTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, playerTeam), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER, _serverConfig);
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, playerTeam), RefSignals.HIGHSTICK_REF, Constants.FROM_SERVER, _serverConfig);
                        }
                        else if (isGoalieInt) {
                            Faceoff.SetNextFaceoffPosition(playerTeam, false, _puckLastStateBeforeCall[Rule.GoalieInt]);
                            UIChat.Instance.Server_SendSystemChatMessage($"GOALIE INT {playerTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, playerTeam), RefSignals.INTERFERENCE_REF, Constants.FROM_SERVER, _serverConfig);
                        }
                        
                        DoFaceoff();
                        return false;
                    }

                    NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL_BLUE, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);
                    NetworkCommunication.SendDataToAll(RefSignals.STOP_SIGNAL_RED, RefSignals.ALL, Constants.FROM_SERVER, _serverConfig);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManagerController_Event_Server_OnPuckEnterTeamGoal_Patch Prefix().\n{ex}");
                }

                NextFaceoffSpot = FaceoffSpot.Center;

                return true;
            }
        }

        /// <summary>
        /// Class that patches the Server_GoalScored event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_GoalScored))]
        public class GameManager_Server_GoalScored_Patch {
            [HarmonyPrefix]
            public static bool Prefix(PlayerTeam team, ref Player lastPlayer, ref Player goalPlayer, Player assistPlayer, Player secondAssistPlayer, Puck puck) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return true;

                    bool saveWasCounted = false;
                    if (goalPlayer != null) {
                        saveWasCounted = SendSOGDuringGoal(goalPlayer);

                        // Get other team goalie.
                        Player _goalie = PlayerFunc.GetOtherTeamGoalie(team);
                        if (_goalie == null)
                            return true;

                        string _goaliePlayerSteamId = _goalie.SteamId.Value.ToString();
                        if (!_savePerc.TryGetValue(_goaliePlayerSteamId, out var _savePercValue)) {
                            _savePerc.Add(_goaliePlayerSteamId, (0, 0));
                            _savePercValue = (0, 0);
                        }

                        _savePerc[_goaliePlayerSteamId] = saveWasCounted ? (--_savePercValue.Saves, _savePercValue.Shots) : (_savePercValue.Saves, ++_savePercValue.Shots);

                        NetworkCommunication.SendDataToAll(SAVEPERC + _goaliePlayerSteamId, _savePerc[_goaliePlayerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
                        return true;
                    }

                    // If own goal, add goal attribution to last player on puck on the other team.
                    UIChat.Instance.Server_SendSystemChatMessage($"OWN GOAL");
                    goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckTipIncludedSteamId[team]).FirstOrDefault();
                    if (goalPlayer != null) {
                        lastPlayer = goalPlayer;
                        saveWasCounted = SendSOGDuringGoal(goalPlayer);
                    }

                    // Get other team goalie.
                    Player goalie = PlayerFunc.GetOtherTeamGoalie(team);
                    if (goalie == null)
                        return true;

                    string goaliePlayerSteamId = goalie.SteamId.Value.ToString();
                    if (!_savePerc.TryGetValue(goaliePlayerSteamId, out var savePercValue)) {
                        _savePerc.Add(goaliePlayerSteamId, (0, 0));
                        savePercValue = (0, 0);
                    }

                    _savePerc[goaliePlayerSteamId] = saveWasCounted ? (--savePercValue.Saves, savePercValue.Shots) : (savePercValue.Saves, ++savePercValue.Shots);

                    NetworkCommunication.SendDataToAll(SAVEPERC + goaliePlayerSteamId, _savePerc[goaliePlayerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_GoalScored_Patch Prefix().\n{ex}");
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
                    // If this is not the server, game is not started or faceoff is on the default dot (center), do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.FaceOff || NextFaceoffSpot == FaceoffSpot.Center)
                        return;

                    // Reteleport player on faceoff to the correct faceoff.
                    Player player = PlayerManager.Instance.GetPlayers()
                        .Where(x =>
                            PlayerFunc.IsPlayerPlaying(x) && x.PlayerBody != null &&
                            x.PlayerBody.transform.position.x == position.x &&
                            x.PlayerBody.transform.position.y == position.y &&
                            x.PlayerBody.transform.position.z == position.z).FirstOrDefault();

                    if (!player)
                        return;

                    PlayerFunc.TeleportOnFaceoff(player, Faceoff.GetFaceoffDot(NextFaceoffSpot), NextFaceoffSpot);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in Player_Server_RespawnCharacter_Patch Postfix().\n{ex}");
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
                        if (_lastDateTimeAskData + TimeSpan.FromSeconds(1) < now) {
                            _lastDateTimeAskData = now;
                            NetworkCommunication.SendData(ASK_SERVER_FOR_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT, _clientConfig);
                        }
                    }

                    ScoreboardModifications(true);
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in UIScoreboard_UpdateServer_Patch Postfix().\n{ex}");
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
                    Logging.LogError($"Error in UIScoreboard_RemovePlayer_Patch Postfix().\n{ex}");
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

                    NextFaceoffSpot = FaceoffSpot.Center;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManager_Server_ResetGameState_Patch Postfix().\n{ex}");
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
                    Logging.LogError($"Error in UIChat_AddChatMessage_Patch Prefix().\n{ex}");
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
                    Logging.LogError($"Error in SynchronizedAudio_Server_PlayRpc_Patch Prefix().\n{ex}");
                }

                return true;
            }
        }
        #endregion

        #region Methods/Functions
        private static void WarnOffside(bool active, PlayerTeam team) {
            if (active) {
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, team), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send show offside signal for client-side UI.
                UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {team.ToString().ToUpperInvariant()} TEAM");
            }
            else {
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, team), RefSignals.OFFSIDE_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send show offside signal for client-side UI.
                UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {team.ToString().ToUpperInvariant()} TEAM CALLED OFF");
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
            _callOffHighStick[team] = true;
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

            foreach (PlayerTeam key in new List<PlayerTeam>(_callOffHighStick.Keys))
                _callOffHighStick[key] = false;
        }

        private static void DoFaceoff(int millisecondsPauseMin = 3500, int millisecondsPauseMax = 5000) {
            if (_paused)
                return;

            _paused = true;

            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, Sounds.WHISTLE, Constants.FROM_SERVER, _serverConfig);

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

            NetworkCommunication.SendDataToAll(Sounds.PLAY_SOUND, _currentMusicPlaying, Constants.FROM_SERVER, _serverConfig);
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
            bool offsidesActivated;
            if (team == PlayerTeam.Blue)
                offsidesActivated = _serverConfig.BlueTeamOffsides;
            else
                offsidesActivated = _serverConfig.RedTeamOffsides;

            if (!offsidesActivated)
                return false;

            return _isOffside.Where(x => x.Value.Team == team).Any(x => x.Value.IsOffside);
        }

        private static bool IsHighStick(PlayerTeam team) {
            bool highStickActivated;
            if (team == PlayerTeam.Blue)
                highStickActivated = _serverConfig.BlueTeamHighStick;
            else
                highStickActivated = _serverConfig.RedTeamHighStick;

            if (!highStickActivated || !_isHighStickActive[team])
                return false;

            return true;
        }

        private static bool IsIcing(PlayerTeam team) {
            bool icingsActivated;
            if (team == PlayerTeam.Blue)
                icingsActivated = _serverConfig.BlueTeamIcings;
            else
                icingsActivated = _serverConfig.RedTeamIcings;

            if (!icingsActivated)
                return false;

            return _isIcingActive[team];
        }

        private static bool IsIcingPossible(PlayerTeam team, bool checkPossibleTime = true) {
            if (_isIcingPossible[team] != null && (!checkPossibleTime || _isIcingPossible[team].ElapsedMilliseconds < _serverConfig.MaxIcingPossibleTime))
                return true;

            return false;
        }

        private static bool IsGoalieInt(PlayerTeam team) {
            bool goalieIntActivated;
            if (team == PlayerTeam.Blue)
                goalieIntActivated = _serverConfig.BlueTeamGInt;
            else
                goalieIntActivated = _serverConfig.RedTeamGInt;

            if (!goalieIntActivated)
                return false;
            
            Stopwatch watch = _goalieIntTimer[team];
            if (watch == null)
                return false;

            Logging.Log($"Goalie is down : {_lastGoalieStateCollision[team]}.", _serverConfig);
            Logging.Log($"Goalie was last touched : {((double)watch.ElapsedMilliseconds) / 1000d} seconds ago.", _serverConfig);
            if (_lastGoalieStateCollision[team])
                return watch.ElapsedMilliseconds < _serverConfig.GIntHitNoGoalMilliseconds;

            return watch.ElapsedMilliseconds < _serverConfig.GIntPushNoGoalMilliseconds;
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
                    Logging.LogError($"Buffer field is null !!!");
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
                Logging.LogError($"Error in ResetAssists.\n{ex}");
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
        /// Method called when the client has started on the client-side.
        /// Used to register to the server messaging (config sync and version check).
        /// </summary>
        /// <param name="message">Dictionary of string and object, content of the event.</param>
        public static void Event_Client_OnClientStarted(Dictionary<string, object> message) {
            if (ServerFunc.IsDedicatedServer() || NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
                return;

            Logging.Log("Event_Client_OnClientStarted", _clientConfig);

            try {
                LoadAssets();
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
                Logging.LogError($"Error in Event_Client_OnClientStopped.\n{ex}");
            }
        }
        
        public static void Event_OnPlayerRoleChanged(Dictionary<string, object> message) {
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
        /// Used to send data to the new client that has connected (config and mod version).
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
            }
            catch (Exception ex) {
                Logging.LogError($"Error in Event_OnClientConnected.\n{ex}");
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

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_kick", "1", clientId, Constants.FROM_CLIENT, _clientConfig);
                        break;

                    case Sounds.PLAY_SOUND: // CLIENT-SIDE : Play sound.
                        if (_sounds == null)
                            break;
                        if (_sounds._errors.Count != 0) {
                            foreach (string error in _sounds._errors)
                                Logging.LogError(error);
                        }
                        else {
                            bool isFaceoffMusic = false;
                            float delay = 0;
                            if (dataStr == Sounds.FACEOFF_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FaceoffMusicList);
                                isFaceoffMusic = true;
                            }
                            else if (dataStr == Sounds.FACEOFF_MUSIC_DELAYED) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FaceoffMusicList);
                                isFaceoffMusic = true;
                                delay = 1f;
                            }
                            else if (dataStr == Sounds.BLUE_GOAL_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.BlueGoalMusicList);
                                _sounds.Play(_currentMusicPlaying, 2.25f);
                            }
                            else if (dataStr == Sounds.RED_GOAL_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.RedGoalMusicList);
                                _sounds.Play(_currentMusicPlaying, 2.25f);
                            }
                            else if (dataStr == Sounds.BETWEEN_PERIODS_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.BetweenPeriodsMusicList);
                                _sounds.Play(_currentMusicPlaying, 1.5f);
                            }
                            else if (dataStr == Sounds.WARMUP_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.WarmupMusicList);
                                _sounds.Play(_currentMusicPlaying, 0, true);
                            }
                            else if (dataStr == Sounds.LAST_MINUTE_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.LastMinuteMusicList);
                                isFaceoffMusic = true;
                            }
                            else if (dataStr == Sounds.FIRST_FACEOFF_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FirstFaceoffMusicList);
                                isFaceoffMusic = true;
                            }
                            else if (dataStr == Sounds.SECOND_FACEOFF_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.SecondFaceoffMusicList);
                                isFaceoffMusic = true;
                            }
                            else if (dataStr == Sounds.LAST_MINUTE_MUSIC_DELAYED) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.LastMinuteMusicList);
                                isFaceoffMusic = true;
                                delay = 1f;
                            }
                            else if (dataStr == Sounds.FIRST_FACEOFF_MUSIC_DELAYED) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FirstFaceoffMusicList);
                                isFaceoffMusic = true;
                                delay = 1f;
                            }
                            else if (dataStr == Sounds.SECOND_FACEOFF_MUSIC_DELAYED) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.SecondFaceoffMusicList);
                                isFaceoffMusic = true;
                                delay = 1f;
                            }
                            else if (dataStr == Sounds.GAMEOVER_MUSIC) {
                                _currentMusicPlaying = Sounds.GetRandomSound(_sounds.GameOverMusicList);
                                _sounds.Play(_currentMusicPlaying, 0.5f);
                            }
                            else if (dataStr == Sounds.WHISTLE)
                                _sounds.Play(Sounds.WHISTLE);

                            if (isFaceoffMusic) {
                                if (string.IsNullOrEmpty(_currentMusicPlaying))
                                    _currentMusicPlaying = Sounds.GetRandomSound(_sounds.FaceoffMusicList);
                                _sounds.Play(_currentMusicPlaying, delay);
                            }
                        }
                        break;

                    case Sounds.STOP_SOUND: // CLIENT-SIDE : Stop sound.
                        if (_sounds == null)
                            break;
                        if (_sounds._errors.Count != 0) {
                            foreach (string error in _sounds._errors)
                                Logging.LogError(error);
                        }
                        else {
                            if (dataStr == Sounds.MUSIC && !string.IsNullOrEmpty(_currentMusicPlaying))
                                _sounds.Stop(_currentMusicPlaying);

                            _currentMusicPlaying = "";
                        }
                        break;

                    case RefSignals.SHOW_SIGNAL_BLUE: // CLIENT-SIDE : Show blue team ref signal in the UI.
                        if (_refSignalsBlueTeam == null)
                            break;

                        if (_refSignalsBlueTeam._errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsBlueTeam.");
                            foreach (string error in _refSignalsBlueTeam._errors)
                                Logging.LogError(error);
                        }
                        else
                            _refSignalsBlueTeam.ShowSignal(dataStr);
                        break;

                    case RefSignals.STOP_SIGNAL_BLUE: // CLIENT-SIDE : Hide blue team ref signal in the UI.
                        if (_refSignalsBlueTeam == null)
                            break;

                        if (_refSignalsBlueTeam._errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsBlueTeam.");
                            foreach (string error in _refSignalsBlueTeam._errors)
                                Logging.LogError(error);
                        }
                        else {
                            if (dataStr == RefSignals.ALL)
                                _refSignalsBlueTeam.StopAllSignals();
                            else
                                _refSignalsBlueTeam.StopSignal(dataStr);
                        }
                        break;

                    case RefSignals.SHOW_SIGNAL_RED: // CLIENT-SIDE : Show red team ref signal in the UI.
                        if (_refSignalsRedTeam == null)
                            break;

                        if (_refSignalsRedTeam._errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsRedTeam.");
                            foreach (string error in _refSignalsRedTeam._errors)
                                Logging.LogError(error);
                        }
                        else
                            _refSignalsRedTeam.ShowSignal(dataStr);
                        break;

                    case RefSignals.STOP_SIGNAL_RED: // CLIENT-SIDE : Hide red team ref signal in the UI.
                        if (_refSignalsRedTeam == null)
                            break;

                        if (_refSignalsRedTeam._errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _refSignalsRedTeam.");
                            foreach (string error in _refSignalsRedTeam._errors)
                                Logging.LogError(error);
                        }
                        else {
                            if (dataStr == RefSignals.ALL)
                                _refSignalsRedTeam.StopAllSignals();
                            else
                                _refSignalsRedTeam.StopSignal(dataStr);
                        }
                        break;

                    case Constants.MOD_NAME + "_kick": // SERVER-SIDE : Kick the client that asked to be kicked.
                        if (dataStr != "1")
                            break;

                        Logging.Log($"Kicking client {clientId}.", _serverConfig);
                        //NetworkManager.Singleton.DisconnectClient(clientId,
                            //$"Mod is out of date. Please restart your game or unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop to update.");

                        DateTime utcNow = DateTime.UtcNow;
                        if (!_sentOutOfDateMessage.TryGetValue(clientId, out DateTime lastCheckTime)) {
                            lastCheckTime = DateTime.MinValue;
                            _sentOutOfDateMessage.Add(clientId, utcNow);
                        }

                        if (lastCheckTime + TimeSpan.FromSeconds(2) < utcNow) {
                            UIChat.Instance.Server_SendSystemChatMessage($"{PlayerManager.Instance.GetPlayerByClientId(clientId).Username.Value} : Mod is out of date. Please unsubscribe from {Constants.WORKSHOP_MOD_NAME} in the workshop and restart your game to update.");
                            _sentOutOfDateMessage[clientId] = utcNow;
                        }
                        break;

                    case ASK_SERVER_FOR_DATA: // SERVER-SIDE : Send the necessary data to client.
                        if (dataStr != "1")
                            break;

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER, _serverConfig);
                        //NetworkCommunication.SendData(ServerConfig.CONFIG_DATA_NAME, _serverConfig.ToString(), clientId, Constants.FROM_SERVER, _serverConfig);

                        foreach (string key in new List<string>(_sog.Keys))
                            NetworkCommunication.SendData(SOG + key, _sog[key].ToString(), clientId, Constants.FROM_SERVER, _serverConfig);

                        foreach (string key in new List<string>(_savePerc.Keys))
                            NetworkCommunication.SendData(SAVEPERC + key, _savePerc[key].ToString(), clientId, Constants.FROM_SERVER, _serverConfig);
                        break;

                    case RESET_SOG:
                        if (dataStr != "1")
                            break;

                        foreach (string key in new List<string>(_sog.Keys)) {
                            if (_sogLabels.TryGetValue(key, out Label label)) {
                                _sog[key] = 0;
                                label.text = "0";

                                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(key);
                                if (currentPlayer != null && currentPlayer && currentPlayer.Role.Value == PlayerRole.Goalie)
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

                    default:
                        if (dataName.StartsWith(SOG)) {
                            string playerSteamId = dataName.Replace(SOG, "");
                            if (string.IsNullOrEmpty(playerSteamId))
                                return;

                            int sog = int.Parse(dataStr);
                            if (_sog.TryGetValue(playerSteamId, out int _)) {
                                _sog[playerSteamId] = sog;
                                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                                if (currentPlayer != null && currentPlayer && currentPlayer.Role.Value != PlayerRole.Goalie)
                                    _sogLabels[playerSteamId].text = sog.ToString();
                            }
                            else
                                _sog.Add(playerSteamId, sog);
                        }

                        if (dataName.StartsWith(SAVEPERC)) {
                            string playerSteamId = dataName.Replace(SAVEPERC, "");
                            if (string.IsNullOrEmpty(playerSteamId))
                                return;

                            string[] dataStrSplitted = RemoveWhitespace(dataStr.Replace("(", "").Replace(")", "")).Split(',');
                            int saves = int.Parse(dataStrSplitted[0]);
                            int shots = int.Parse(dataStrSplitted[1]);

                            if (_savePerc.TryGetValue(playerSteamId, out var _)) {
                                _savePerc[playerSteamId] = (saves, shots);
                                Player currentPlayer = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
                                if (currentPlayer != null && currentPlayer && currentPlayer.Role.Value == PlayerRole.Goalie)
                                    _sogLabels[playerSteamId].text = GetGoalieSavePerc(saves, shots);
                            }
                            else
                                _savePerc.Add(playerSteamId, (saves, shots));
                        }
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveData.\n{ex}");
            }
        }

        private static void ServerManager_Update_IcingLogic(PlayerTeam team, Puck puck, Dictionary<PlayerTeam, bool?> icingHasToBeWarned) {
            if (!IsIcingPossible(team, false) && _isIcingActive[team]) {
                _isIcingActive[team] = false;
                NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(false, team), RefSignals.ICING_LINESMAN, Constants.FROM_SERVER, _serverConfig); // Send stop icing signal for client-side UI.
                UIChat.Instance.Server_SendSystemChatMessage($"ICING {team.ToString().ToUpperInvariant()} TEAM CALLED OFF");
            }
            else if (!_isIcingActive[team] && IsIcingPossible(team) && _puckZone == ZoneFunc.GetTeamZones(TeamFunc.GetOtherTeam(team))[1]) {
                _puckLastStateBeforeCall[Rule.Icing] = (puck.Rigidbody.transform.position, _puckZone);
                _isIcingActiveTimers[team].Change(_serverConfig.MaxIcingTime, Timeout.Infinite);
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
                Logging.Log($"Enabling...", _serverConfig, true);

                _harmony.PatchAll();

                Logging.Log($"Enabled.", _serverConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null) {
                        Logging.Log($"RegisterNamedMessageHandler {Constants.FROM_CLIENT}.", _serverConfig);
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;
                    }

                    Logging.Log("Setting server sided config.", _serverConfig, true);
                    _serverConfig = ServerConfig.ReadConfig(ServerManager.Instance.AdminSteamIds);
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
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                }
                else {
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.AddEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                }

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
                Logging.Log($"Disabling...", _serverConfig, true);

                Logging.Log("Unsubscribing from events.", _serverConfig, true);
                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.RemoveEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.RemoveEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT);
                }
                else {
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStarted", Event_Client_OnClientStarted);
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    Event_Client_OnClientStopped(new Dictionary<string, object>());
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER);
                }

                _hasRegisteredWithNamedMessageHandler = false;

                //_getStickLocation.Disable();

                ScoreboardModifications(false);

                if (_sounds != null) {
                    _sounds.DestroyGameObjects();
                    _sounds = null;
                }

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

        private static bool SendSOGDuringGoal(Player player) {
            if (!_lastShotWasCounted[player.Team.Value]) {
                string playerSteamId = player.SteamId.Value.ToString();

                if (string.IsNullOrEmpty(playerSteamId))
                    return true;

                if (!_sog.TryGetValue(playerSteamId, out int _))
                    _sog.Add(playerSteamId, 0);

                _sog[playerSteamId] += 1;
                NetworkCommunication.SendDataToAll(SOG + playerSteamId, _sog[playerSteamId].ToString(), Constants.FROM_SERVER, _serverConfig);

                _lastShotWasCounted[player.Team.Value] = true;

                return false;
            }

            return true;
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
        #endregion
    }

    public enum Rule {
        Offside,
        Icing,
        HighStick,
        GoalieInt,
    }

    internal class SaveCheck {
        internal bool HasToCheck { get; set; } = false;
        internal string ShooterSteamId { get; set; } = "";
        internal int FramesChecked { get; set; } = 0;
    }
}
