using HarmonyLib;
using oomtm450PuckMod_Ruleset.Configs;
using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        private const string MOD_VERSION = "0.9.1DEV";

        /// <summary>
        /// Const float, radius of the puck.
        /// </summary>
        internal const float PUCK_RADIUS = 0.13f;

        /// <summary>
        /// Const float, radius of a player.
        /// </summary>
        private const float PLAYER_RADIUS = 0.25f;

        /// <summary>
        /// Const float, height of the net's crossbar.
        /// </summary>
        private const float CROSSBAR_HEIGHT = 1.8f;

        /// <summary>
        /// Const float, height of the player's shoulders.
        /// </summary>
        internal const float SHOULDERS_HEIGHT = 1.775f;

        /// <summary>
        /// Const int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        private const int MAX_TIPPED_MILLISECONDS = 95;

        /// <summary>
        /// Const int, number of milliseconds for a possession to be considered with challenging.
        /// </summary>
        private const int MIN_POSSESSION_MILLISECONDS = 250;

        /// <summary>
        /// Const int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        private const int MAX_POSSESSION_MILLISECONDS = 500;

        /// <summary>
        /// Const int, number of milliseconds after a push on the goalie to be considered no goal.
        /// </summary>
        private const int GINT_PUSH_NO_GOAL_MILLISECONDS = 300;
        private const int GINT_HIT_NO_GOAL_MILLISECONDS = 800; // TODO : Remove when penalty is added.

        private const float GINT_COLLISION_FORCE_THRESHOLD = 1f;
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
        internal static ClientConfig _clientConfig = new ClientConfig();

        private static readonly LockDictionary<string, (PlayerTeam Team, bool IsOffside)> _isOffside = new LockDictionary<string, (PlayerTeam, bool)>();

        private static readonly LockDictionary<PlayerTeam, bool> _isIcingPossible = new LockDictionary<PlayerTeam, bool>{
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _isIcingActive = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _isHighStickActive = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
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

        private static InputAction _getStickLocation;

        private static readonly LockDictionary<string, Stopwatch> _lastTimeOnCollisionExitWasCalled = new LockDictionary<string, Stopwatch>();

        private static bool _changedPhase = false;

        private static int _periodTimeRemaining = 0;

        private static PlayerTeam _lastPlayerOnPuckTeam = PlayerTeam.Blue;

        private static readonly LockDictionary<PlayerTeam, string> _lastPlayerOnPuckSteamId = new LockDictionary<PlayerTeam, string> {
            { PlayerTeam.Blue, "" },
            { PlayerTeam.Red, "" },
        };

        private static readonly LockDictionary<PlayerTeam, bool> _lastGoalieStateCollision = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static float _lastForceOnGoalie = 0;

        private static string _lastForceOnGoaliePlayerSteamId = "";

        private static bool _paused = false;

        private static bool _doFaceoff = false;

        private static string _currentFaceoffSound = "";

        // Client-side.
        private static Sounds _sounds = null;

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
                if (_paused || !ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
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
                                        if (IsIcing(playerOtherTeam))
                                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerOtherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                                        ResetIcings();
                                    }
                                }
                            }

                            if (IsIcing(playerOtherTeam))
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerOtherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            ResetIcings();
                        }
                        else if (IsIcingPossible(playerBody.Player.Team.Value)) {
                            if (_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var result)) {
                                if (ZoneFunc.GetTeamZones(playerOtherTeam, true).Any(x => x == result.Zone)) {
                                    if (IsIcing(playerBody.Player.Team.Value))
                                        UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerBody.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                                    ResetIcings();
                                }
                            }
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

                    string lastPlayerOnPuckSteamId = _lastPlayerOnPuckSteamId[_lastPlayerOnPuckTeam];

                    if (!_lastTimeOnCollisionExitWasCalled.TryGetValue(currentPlayerSteamId, out Stopwatch lastTimeCollisionExitWatch)) {
                        lastTimeCollisionExitWatch = new Stopwatch();
                        lastTimeCollisionExitWatch.Start();
                        _lastTimeOnCollisionExitWasCalled.Add(currentPlayerSteamId, lastTimeCollisionExitWatch);
                    }
                    else if (lastTimeCollisionExitWatch.ElapsedMilliseconds > MAX_TIPPED_MILLISECONDS || lastPlayerOnPuckSteamId != currentPlayerSteamId) {
                        //if (lastPlayerOnPuckSteamId == currentPlayerSteamId || string.IsNullOrEmpty(lastPlayerOnPuckSteamId))
                            //Logging.Log($"{stick.Player.Username.Value} had the puck for {((double)(watch.ElapsedMilliseconds - lastTimeCollisionExitWatch.ElapsedMilliseconds)) / 1000d} seconds.", _serverConfig);
                        watch.Restart();

                        if (!string.IsNullOrEmpty(lastPlayerOnPuckSteamId) && lastPlayerOnPuckSteamId != currentPlayerSteamId) {
                            if (_playersCurrentPuckTouch.TryGetValue(lastPlayerOnPuckSteamId, out Stopwatch lastPlayerWatch)) {
                                //Logging.Log($"{lastPlayerOnPuckSteamId} had the puck for {((double)(lastPlayerWatch.ElapsedMilliseconds - _lastTimeOnCollisionExitWasCalled[lastPlayerOnPuckSteamId].ElapsedMilliseconds)) / 1000d} seconds.", _serverConfig);
                                lastPlayerWatch.Reset();
                            }
                        }
                    }

                    // High stick logic.
                    Puck puck = PuckManager.Instance.GetPuck();
                    if (puck) {
                        if (stick.Player.Role.Value != PlayerRole.Goalie && puck.Rigidbody.transform.position.y > _serverConfig.HighStickHeight + stick.Player.PlayerBody.Rigidbody.transform.position.y) {
                            if (!_isHighStickActive[stick.Player.Team.Value]) {
                                _isHighStickActive[stick.Player.Team.Value] = true;
                                _puckLastStateBeforeCall[Rule.HighStick] = (puck.Rigidbody.transform.position, _puckZone);
                                UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM");
                            }
                        }
                        else if (puck.IsGrounded) {
                            if (_isHighStickActive[stick.Player.Team.Value]) {
                                Faceoff.SetNextFaceoffPosition(stick.Player.Team.Value, false, _puckLastStateBeforeCall[Rule.HighStick]);
                                UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED");
                                DoFaceoff();
                            }
                        }
                    }

                    PlayerTeam otherTeam = TeamFunc.GetOtherTeam(stick.Player.Team.Value);
                    if (_isHighStickActive[otherTeam]) {
                        _isHighStickActive[otherTeam] = false;
                        UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
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
                    if (_paused || !ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
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
                            DoFaceoff();
                        }
                        else if (stick.Player.PlayerPosition.Role == PlayerRole.Goalie) {
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            ResetIcings();
                        }
                    }
                    else {
                        if (IsIcing(stick.Player.Team.Value))
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
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
                    if (_paused || !ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
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
                            _puckLastStateBeforeCall[Rule.Offside] = (puck.Rigidbody.transform.position, _puckZone);
                    }

                    // Icing logic.
                    bool icingPossible = false;
                    if (ZoneFunc.GetTeamZones(stick.Player.Team.Value, true).Any(x => x == _puckZone))
                        icingPossible = true;

                    _isIcingPossible[stick.Player.Team.Value] = icingPossible;
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
                if (_paused || !ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
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

                    if (goalie.PlayerBody.Rigidbody.transform.position.x - PLAYER_RADIUS < startX ||
                        goalie.PlayerBody.Rigidbody.transform.position.x + PLAYER_RADIUS > endX ||
                        goalie.PlayerBody.Rigidbody.transform.position.z - PLAYER_RADIUS < startZ ||
                        goalie.PlayerBody.Rigidbody.transform.position.z + PLAYER_RADIUS > endZ)
                        return;

                    PlayerTeam goalieOtherTeam = TeamFunc.GetOtherTeam(goalie.Team.Value);

                    bool goalieDown = goalie.PlayerBody.IsSlipping || goalie.PlayerBody.HasSlipped;
                    _lastGoalieStateCollision[goalieOtherTeam] = goalieDown;

                    if (goalieDown || force > GINT_COLLISION_FORCE_THRESHOLD) {
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

                    _paused = false;

                    if (phase == GamePhase.FaceOff || phase == GamePhase.Warmup || phase == GamePhase.GameOver || phase == GamePhase.PeriodOver) {
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

                        _puckZone = ZoneFunc.GetZone(NextFaceoffSpot);

                        _lastPlayerOnPuckTeam = PlayerTeam.Blue;
                        foreach (PlayerTeam key in new List<PlayerTeam>(_lastPlayerOnPuckSteamId.Keys))
                            _lastPlayerOnPuckSteamId[key] = "";
                    }

                    if (!_changedPhase)
                        return true;

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
                        NetworkCommunication.SendDataToAll("SoundEnd", _currentFaceoffSound, Constants.FROM_SERVER, _serverConfig);
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
                Zone oldZone = Zone.BlueTeam_Center;

                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing)
                        return true;

                    if (_paused) {
                        if (_doFaceoff)
                            PostDoFaceoff();
                        else
                            return true;
                    }

                    players = PlayerManager.Instance.GetPlayers();
                    puck = PuckManager.Instance.GetPuck();

                    if (players.Count == 0 || puck == null)
                        return true;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 1.\n{ex}");
                }
                try {
                    oldZone = _puckZone;
                    _puckZone = ZoneFunc.GetZone(puck.Rigidbody.transform.position, _puckZone, PUCK_RADIUS);

                    // Icing logic.
                    if (_isIcingPossible[PlayerTeam.Blue] && _puckZone == Zone.RedTeam_BehindGoalLine) {
                        if (!IsIcing(PlayerTeam.Blue)) {
                            _puckLastStateBeforeCall[Rule.Icing] = (puck.Rigidbody.transform.position, _puckZone);
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {PlayerTeam.Blue.ToString().ToUpperInvariant()} TEAM");
                        }
                        _isIcingActive[PlayerTeam.Blue] = true;
                    }
                    if (_isIcingPossible[PlayerTeam.Red] && _puckZone == Zone.BlueTeam_BehindGoalLine) {
                        if (!IsIcing(PlayerTeam.Red)) {
                            _puckLastStateBeforeCall[Rule.Icing] = (puck.Rigidbody.transform.position, _puckZone);
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {PlayerTeam.Red.ToString().ToUpperInvariant()} TEAM");
                        }
                        _isIcingActive[PlayerTeam.Red] = true;
                    }
                    

                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 2.\n{ex}");
                }
                try {
                    string playerWithPossessionSteamId = GetPlayerSteamIdInPossession();

                    // Offside logic.
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

                        List<Zone> otherTeamZones = ZoneFunc.GetTeamZones(TeamFunc.GetOtherTeam(player.Team.Value));

                        // Is offside.
                        if (playerWithPossessionSteamId != player.SteamId.Value.ToString() && (playerZone == otherTeamZones[0] || playerZone == otherTeamZones[1])) {
                            if ((_puckZone != otherTeamZones[0] && _puckZone != otherTeamZones[1]) || IsOffside(player.Team.Value)) {
                                /*if (!isPlayerTeamOffside) {
                                    _puckLastPositionBeforeCall = puck.Rigidbody.transform.position;
                                    _puckLastZoneBeforeCall = _puckZone;
                                    //UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {player.Team.Value.ToString().ToUpperInvariant()} TEAM");
                                }*/

                                _isOffside[playerSteamId] = (player.Team.Value, true);
                            }
                        }

                        // Is not offside.
                        if (_playersZone[playerSteamId].Zone != otherTeamZones[0] && _playersZone[playerSteamId].Zone != otherTeamZones[1] && _isOffside[playerSteamId].IsOffside) {
                            _isOffside[playerSteamId] = (player.Team.Value, false);
                            /*if (!IsOffside(player.Team.Value)) {
                                _puckLastPositionBeforeOffside = puck.Rigidbody.transform.position;
                                _puckLastZoneBeforeOffside = _puckZone;
                                //UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            }*/
                        }

                        // Remove offside if the other team entered the zone with the puck.
                        List<Zone> lastPlayerOnPuckTeamZones = ZoneFunc.GetTeamZones(_lastPlayerOnPuckTeam, true);
                        if (oldZone == lastPlayerOnPuckTeamZones[2] && _puckZone == lastPlayerOnPuckTeamZones[0]) {
                            PlayerTeam otherTeam = TeamFunc.GetOtherTeam(_lastPlayerOnPuckTeam);
                            foreach (string key in new List<string>(_isOffside.Keys)) {
                                if (_isOffside[key].Team == otherTeam)
                                    _isOffside[key] = (_isOffside[key].Team, false);
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 3.\n{ex}");
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
                    if (_paused)
                        return false;

                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || PlayerManager.Instance == null || PuckManager.Instance == null || GameManager.Instance.Phase != GamePhase.Playing)
                        return true;

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
                        }
                        else if (isGoalieInt) {
                            Faceoff.SetNextFaceoffPosition(playerTeam, false, _puckLastStateBeforeCall[Rule.GoalieInt]);
                            UIChat.Instance.Server_SendSystemChatMessage($"GOALIE INT {playerTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                        }
                        
                        DoFaceoff();
                        return false;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManagerController_GameManagerController_Patch Prefix().\n{ex}");
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
            public static bool Prefix(PlayerTeam team, ref Player lastPlayer, ref Player goalPlayer, Player assistPlayer, Player secondAssistPlayer, Puck puck) {
                try {
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer())
                        return true;

                    if (goalPlayer != null)
                        return true;

                    // If own goal, add goal attribution to last player on puck on the other team.
                    UIChat.Instance.Server_SendSystemChatMessage($"OWN GOAL");
                    goalPlayer = PlayerManager.Instance.GetPlayers().Where(x => x.SteamId.Value.ToString() == _lastPlayerOnPuckSteamId[team]).FirstOrDefault();
                    if (goalPlayer != null)
                        lastPlayer = goalPlayer;
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in GameManagerController_GameManagerController_Patch Prefix().\n{ex}");
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
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.FaceOff)
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
        #endregion

        #region Methods/Functions
        private static void ResetIcings() {
            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingPossible.Keys))
                _isIcingPossible[key] = false;

            foreach (PlayerTeam key in new List<PlayerTeam>(_isIcingActive.Keys))
                _isIcingActive[key] = false;
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
            foreach (PlayerTeam key in new List<PlayerTeam>(_isHighStickActive.Keys))
                _isHighStickActive[key] = false;
        }

        private static void DoFaceoff(int millisecondsPauseMin = 3500, int millisecondsPauseMax = 5000) {
            if (_paused)
                return;

            _paused = true;

            NetworkCommunication.SendDataToAll("SoundStart", Sounds.WHISTLE, Constants.FROM_SERVER, _serverConfig);
            _currentFaceoffSound = Sounds.GetRandomFaceoffSound();
            NetworkCommunication.SendDataToAll("SoundStart", _currentFaceoffSound, Constants.FROM_SERVER, _serverConfig);

            _periodTimeRemaining = GameManager.Instance.GameState.Value.Time;
            GameManager.Instance.Server_Pause();

            _ = Task.Run(() => {
                Thread.Sleep(new System.Random().Next(millisecondsPauseMin, millisecondsPauseMax));
                _doFaceoff = true;
            });
        }

        private static void PostDoFaceoff() {
            _doFaceoff = false;
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

            if (!highStickActivated)
                return false;

            return _isHighStickActive[team];
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

        private static bool IsIcingPossible(PlayerTeam team) {
            return _isIcingPossible[team];
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
                return watch.ElapsedMilliseconds < GINT_HIT_NO_GOAL_MILLISECONDS;
            else
                return watch.ElapsedMilliseconds < GINT_PUSH_NO_GOAL_MILLISECONDS;
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
        /// Function that returns a PlayerLegPad instance from a GameObject.
        /// </summary>
        /// <param name="gameObject">GameObject, GameObject to use.</param>
        /// <returns>PlayerLegPad, found PlayerLegPad object or null.</returns>
        private static PlayerLegPad GetPlayerLegPad(GameObject gameObject) {
            return gameObject.GetComponent<PlayerLegPad>();
        }

        /// <summary>
        /// Function that returns the player steam Id that has possession.
        /// </summary>
        /// <returns>String, player steam Id with the possession or an empty string if no one has the puck (or it is challenged).</returns>
        private static string GetPlayerSteamIdInPossession() {
            Dictionary<string, Stopwatch> dict;
            dict = _playersLastTimePuckPossession
                .Where(x => x.Value.ElapsedMilliseconds < MIN_POSSESSION_MILLISECONDS && x.Value.ElapsedMilliseconds > MAX_TIPPED_MILLISECONDS)
                .ToDictionary(x => x.Key, x => x.Value);

            if (dict.Count > 1) // Puck possession is challenged.
                return "";

            if (dict.Count == 1)
                return dict.First().Key;

            List<string> steamIds = _playersLastTimePuckPossession
                .Where(x => x.Value.ElapsedMilliseconds < MAX_POSSESSION_MILLISECONDS && x.Value.ElapsedMilliseconds > MAX_TIPPED_MILLISECONDS)
                .OrderBy(x => x.Value.ElapsedMilliseconds)
                .Select(x => x.Key).ToList();

            if (steamIds.Count != 0)
                return steamIds.First();

            return "";
        }
        
        private static bool PuckIsTipped(string playerSteamId) {
            Stopwatch currentPuckTouchWatch, lastPuckExitWatch;
            if (!_playersCurrentPuckTouch.TryGetValue(playerSteamId, out currentPuckTouchWatch))
                return false;

            if (!_lastTimeOnCollisionExitWasCalled.TryGetValue(playerSteamId, out lastPuckExitWatch))
                return false;

            if (currentPuckTouchWatch.ElapsedMilliseconds - lastPuckExitWatch.ElapsedMilliseconds < MAX_TIPPED_MILLISECONDS)
                return true;

            return false;
        }

        private static void ResetAssists(PlayerTeam team) {
            try {
                NetworkList<NetworkObjectCollision> buffer = GetPuckBuffer();
                if (buffer == null) {
                    Logging.Log($"Buffer field is null !!!", _serverConfig);
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

            return (NetworkList<NetworkObjectCollision>)typeof(NetworkObjectCollisionBuffer).GetField("buffer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(puck.NetworkObjectCollisionBuffer);
        }
        #endregion

        #region Events
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
        public static void Event_OnPlayerSpawned(Dictionary<string, object> message) { // TODO : Find an function that doesn't get called every respawn.
            if (!ServerFunc.IsDedicatedServer())
                return;

            Logging.Log("Event_OnPlayerSpawned", _serverConfig);

            try {
                Player player = (Player)message["player"];

                NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
                NetworkCommunication.SendData(ServerConfig.CONFIG_DATA_NAME, _serverConfig.ToString(), player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
                NetworkCommunication.SendData("loadsounds", "1", player.OwnerClientId, Constants.FROM_SERVER, _serverConfig);
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
                    case Constants.MOD_NAME + "_" + nameof(MOD_VERSION): // CLIENT-SIDE : Mod version check, kick if client and server versions are not the same.
                        if (MOD_VERSION == dataStr) // TODO : Move the kick later so that it doesn't break anything. Maybe even add a chat message and a 3-5 sec wait.
                            break;

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + "kick", "1", clientId, Constants.FROM_SERVER, _clientConfig);
                        break;

                    case ServerConfig.CONFIG_DATA_NAME: // CLIENT-SIDE : Set the server config on the client to use later if needed.
                        if (!_serverConfig.SentByServer)
                            _serverConfig = ServerConfig.SetConfig(dataStr);
                        break;

                    case "loadsounds": // CLIENT-SIDE : Load sounds.
                        if (dataStr != "1")
                            break;
                        GameObject gameObject = new GameObject("Sounds");
                        _sounds = gameObject.AddComponent<Sounds>();
                        _sounds.LoadWhistlePrefab();
                        break;

                    case "SoundStart": // CLIENT-SIDE : Play sound.
                        if (_sounds == null)
                            break;
                        if (_sounds._errors.Count != 0) {
                            foreach (string error in _sounds._errors)
                                Logging.LogError(error);
                        }
                        else
                            _sounds.Play(dataStr);
                        break;

                    case "SoundEnd": // CLIENT-SIDE : Stop sound.
                        if (_sounds == null)
                            break;
                        if (_sounds._errors.Count != 0) {
                            foreach (string error in _sounds._errors)
                                Logging.LogError(error);
                        }
                        else
                            _sounds.Stop(dataStr);
                        break;

                    case Constants.MOD_NAME + "_" + "kick": // SERVER-SIDE : Kick the client that asked to be kicked.
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
        #endregion
    }

    public enum Rule {
        Offside,
        Icing,
        HighStick,
        GoalieInt,
    }
}
