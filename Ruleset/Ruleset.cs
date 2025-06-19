using HarmonyLib;
using oomtm450PuckMod_Ruleset.Configs;
using oomtm450PuckMod_Ruleset.SystemFunc;
using System;
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
        private const string MOD_VERSION = "0.6.0DEV";

        /// <summary>
        /// Const float, radius of the puck.
        /// </summary>
        private const float PUCK_RADIUS = 0.13f;

        /// <summary>
        /// Const float, radius of a player.
        /// </summary>
        private const float PLAYER_RADIUS = 0.25f;

        /// <summary>
        /// Const float, height of the net's crossbar.
        /// </summary>
        private const float CROSSBAR_HEIGHT = 1.75f;

        /// <summary>
        /// Const int, number of milliseconds for a puck to not be considered tipped by a player's stick.
        /// </summary>
        private const int MAX_TIPPED_MILLISECONDS = 90;

        /// <summary>
        /// Const int, number of milliseconds for a possession to be considered with challenging.
        /// </summary>
        private const int MIN_POSSESSION_MILLISECONDS = 250;

        /// <summary>
        /// Const int, number of milliseconds for a possession to be considered without challenging.
        /// </summary>
        private const int MAX_POSSESSION_MILLISECONDS = 500;
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

        private static readonly Dictionary<PlayerTeam, bool> _isHighStickActive = new Dictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        private static Vector3 _puckLastPositionBeforeCall = Vector3.zero;

        private static Zone _puckLastZoneBeforeCall = Zone.BlueTeam_Center;

        private static Zone _puckZone = Zone.BlueTeam_Center;

        private static Dictionary<string, (PlayerTeam Team, Zone Zone)> _playersZone = new Dictionary<string, (PlayerTeam, Zone)>();

        private static Dictionary<string, Stopwatch> _playersCurrentPuckTouch = new Dictionary<string, Stopwatch>();

        private static Dictionary<string, Stopwatch> _playersLastTimePuckPossession = new Dictionary<string, Stopwatch>();

        private static InputAction _getStickLocation;

        private static bool _changedPhase = false;

        private static int _periodTimeRemaining = 0;

        private static PlayerTeam _lastPlayerOnPuckTeam = PlayerTeam.Blue;

        private static readonly object _locker = new object();

        private static FaceoffSpot _nextFaceoffSpot = FaceoffSpot.Center;

        // Barrier collider, position 0 -19 0 is realistic.
        #endregion

        /// <summary>
        /// Class that patches the OnCollisionEnter event from Puck.
        /// </summary>
        [HarmonyPatch(typeof(Puck), "OnCollisionEnter")]
        public class Puck_OnCollisionEnter_Patch {
            [HarmonyPostfix]
            public static void Postfix(Collision collision) {
                // If this is not the server or game is not started, do not use the patch.
                if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
                    return;

                try {
                    Stick stick = GetStick(collision.gameObject);
                    if (!stick)
                        return;

                    //Logging.Log($"Puck was hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (enter)!", _serverConfig);

                    // Start tipped timer.
                    lock (_locker) {
                        if (!_playersCurrentPuckTouch.TryGetValue(stick.Player.SteamId.Value.ToString(), out Stopwatch watch)) {
                            watch = new Stopwatch();
                            watch.Start();
                            _playersCurrentPuckTouch.Add(stick.Player.SteamId.Value.ToString(), watch);
                        }
                    }

                    // High stick logic.
                    if (stick.Player.Role.Value != PlayerRole.Goalie) {
                        Puck puck = PuckManager.Instance.GetPuck();
                        if (puck.Rigidbody.transform.position.y > CROSSBAR_HEIGHT + stick.Player.PlayerBody.Rigidbody.transform.position.y) {
                            lock (_locker) {
                                if (!_isHighStickActive[stick.Player.Team.Value]) {
                                    _isHighStickActive[stick.Player.Team.Value] = true;
                                    _puckLastPositionBeforeCall = puck.Rigidbody.transform.position;
                                    _puckLastZoneBeforeCall = _puckZone;
                                    UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM");
                                }
                            }
                        }
                        else if (puck.IsGrounded) {
                            lock (_locker) {
                                if (_isHighStickActive[stick.Player.Team.Value]) {
                                    SetNextFaceoffPosition(stick.Player.Team.Value, false);
                                    UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED");
                                    Faceoff();
                                }
                            }
                        }
                    }

                    PlayerTeam otherTeam = GetOtherTeam(stick.Player.Team.Value);
                    lock (_locker) {
                        if (_isHighStickActive[otherTeam]) {
                            _isHighStickActive[otherTeam] = false;
                            UIChat.Instance.Server_SendSystemChatMessage($"HIGH STICK {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                        }
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
                    if (!ServerFunc.IsDedicatedServer() || GameManager.Instance.Phase != GamePhase.Playing)
                        return;

                    Stick stick = GetStick(collision.gameObject);
                    if (!stick) {
                        PlayerBodyV2 playerBody = GetPlayerBodyV2(collision.gameObject);
                        if (!playerBody)
                            return;

                        PlayerTeam playerOtherTeam = GetOtherTeam(playerBody.Player.Team.Value);
                        if (IsIcingPossible(playerOtherTeam)) {
                            if (playerBody.Player.Role.Value != PlayerRole.Goalie) {
                                Zone playerZone;
                                if (!_playersZone.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out var result))
                                    return;
                                else {
                                    playerZone = result.Zone;

                                    if (playerZone != GetTeamZones(playerBody.Player.Team.Value)[1])
                                        ResetIcings();
                                }

                                // TODO : Icing logic called off or remove icing possible if tipped.

                                return;
                            }

                            if (IsIcing(playerOtherTeam))
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {playerOtherTeam.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                            ResetIcings();
                        }

                        return;
                    }

                    _lastPlayerOnPuckTeam = stick.Player.Team.Value;

                    string playerSteamId = stick.Player.SteamId.Value.ToString();

                    //Logging.Log($"Puck is being hit by \"{stick.Player.SteamId.Value} {stick.Player.Username.Value}\" (stay)!", _serverConfig);

                    Stopwatch watch;
                    lock (_locker) {
                        if (!_playersLastTimePuckPossession.TryGetValue(playerSteamId, out watch)) {
                            watch = new Stopwatch();
                            watch.Start();
                            _playersLastTimePuckPossession.Add(playerSteamId, watch);
                        }
                    }

                    PlayerTeam otherTeam = GetOtherTeam(stick.Player.Team.Value);
                    // Offside logic.
                    List<Zone> zones = GetTeamZones(otherTeam);
                    Puck puck = PuckManager.Instance.GetPuck();
                    if (IsOffside(stick.Player.Team.Value) && (_puckZone == zones[0] || _puckZone == zones[1])) {
                        SetNextFaceoffPosition(stick.Player.Team.Value, false);
                        UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {stick.Player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED");
                        Faceoff();
                    }

                    // Icing logic.
                    if (IsIcing(otherTeam)) {
                        if (stick.Player.PlayerPosition.Role != PlayerRole.Goalie) {
                            SetNextFaceoffPosition(otherTeam, true);
                            UIChat.Instance.Server_SendSystemChatMessage($"ICING {otherTeam.ToString().ToUpperInvariant()} TEAM CALLED");
                            Faceoff();
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
                            // Reset offsides.
                            _isOffside.Clear();

                            // Reset players zone.
                            _playersZone.Clear();

                            // Reset possession times.
                            foreach (Stopwatch watch in _playersLastTimePuckPossession.Values)
                                watch.Stop();
                            _playersLastTimePuckPossession.Clear();

                            // Reset tipped times.
                            foreach (Stopwatch watch in _playersCurrentPuckTouch.Values)
                                watch.Stop();
                            _playersCurrentPuckTouch.Clear();

                            // Reset high sticks.
                            foreach (PlayerTeam key in new List<PlayerTeam>(_isHighStickActive.Keys))
                                _isHighStickActive[key] = false;
                        }
                        // Reset icings.
                        ResetIcings();

                        _puckZone = Zone.BlueTeam_Center;
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
                        if (_nextFaceoffSpot == FaceoffSpot.Center)
                            return;

                        Vector3 dot = GetFaceoffDot();

                        foreach (Player player in PlayerManager.Instance.GetPlayers()) {
                            if (!IsPlayerPlaying(player) || player.Role.Value == PlayerRole.Goalie)
                                continue;

                            float xOffset = 0, zOffset = 0;
                            switch (player.PlayerPosition.Name) {
                                case PlayerFunc.CENTER_POSITION:
                                    zOffset = 1.5f;
                                    break;
                                case PlayerFunc.LEFT_WINGER_POSITION:
                                    zOffset = 1.5f;
                                    xOffset = 9f;
                                    break;
                                case PlayerFunc.RIGHT_WINGER_POSITION:
                                    zOffset = 1.5f;
                                    xOffset = -9f;
                                    break;
                                case PlayerFunc.LEFT_DEFENDER_POSITION:
                                    zOffset = 13.5f;
                                    if ((ushort)_nextFaceoffSpot >= 5)
                                        zOffset -= 1f;
                                    xOffset = 4f;
                                    break;
                                case PlayerFunc.RIGHT_DEFENDER_POSITION:
                                    zOffset = 13.5f;
                                    if ((ushort)_nextFaceoffSpot >= 5)
                                        zOffset -= 1f;
                                    xOffset = -4f;
                                    break;
                            }

                            if (player.Team.Value == PlayerTeam.Red) {
                                xOffset *= -1;
                                zOffset *= -1;
                            }

                            player.PlayerBody.Server_Teleport(new Vector3(dot.x + xOffset, dot.y, dot.z + zOffset), player.PlayerBody.Rigidbody.rotation);
                        }

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
                    // If this is not the server or game is not started, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || isReplay || (GameManager.Instance.Phase != GamePhase.Playing && GameManager.Instance.Phase != GamePhase.FaceOff))
                        return true;

                    Vector3 dot = GetFaceoffDot();
                    position = new Vector3(dot.x, 1.1f, dot.z);
                    _nextFaceoffSpot = FaceoffSpot.Center;

                }
                catch (Exception ex)  {
                    Logging.LogError($"Error in Puck_OnCollisionExit_Patch Postfix().\n{ex}");
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

                    _lastPlayerOnPuckTeam = stick.Player.Team.Value;

                    // Icing logic.
                    if (!PuckIsTipped(stick.Player.SteamId.Value.ToString())) {
                        bool icingPossible = false;
                        if (GetTeamZones(stick.Player.Team.Value, true).Any(x => x == _puckZone))
                            icingPossible = true;

                        lock (_locker)
                            _isIcingPossible[stick.Player.Team.Value] = icingPossible;
                    }
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
                Zone oldZone = Zone.BlueTeam_Center;

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
                    oldZone = _puckZone;
                    _puckZone = GetZone(puck.Rigidbody.transform.position, oldZone, PUCK_RADIUS);

                    // Icing logic.
                    lock (_locker) {
                        if (_isIcingPossible[PlayerTeam.Blue] && _puckZone == Zone.RedTeam_BehindGoalLine) {
                            if (!IsIcing(PlayerTeam.Blue)) {
                                _puckLastPositionBeforeCall = puck.Rigidbody.transform.position;
                                _puckLastZoneBeforeCall = _puckZone;
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {PlayerTeam.Blue.ToString().ToUpperInvariant()} TEAM");
                            }
                            _isIcingActive[PlayerTeam.Blue] = true;
                        }
                        if (_isIcingPossible[PlayerTeam.Red] && _puckZone == Zone.BlueTeam_BehindGoalLine) {
                            if (!IsIcing(PlayerTeam.Red)) {
                                _puckLastPositionBeforeCall = puck.Rigidbody.transform.position;
                                _puckLastZoneBeforeCall = _puckZone;
                                UIChat.Instance.Server_SendSystemChatMessage($"ICING {PlayerTeam.Red.ToString().ToUpperInvariant()} TEAM");
                            }
                            _isIcingActive[PlayerTeam.Red] = true;
                        }
                    }

                }
                catch (Exception ex) {
                    Logging.LogError($"Error in ServerManager_Update_Patch Prefix() 2.\n{ex}");
                }
                try {
                    string playerWithPossessionSteamId = GetPlayerSteamIdInPossession();

                    // Offside logic.
                    foreach (Player player in players) {
                        if (!IsPlayerPlaying(player))
                            continue;

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

                        Zone playerZone = GetZone(player.PlayerBody.transform.position, oldPlayerZone, PLAYER_RADIUS);
                        _playersZone[playerSteamId] = (player.Team.Value, playerZone);

                        // Is offside.
                        if (!PuckIsTipped(playerSteamId)) {
                            List<Zone> otherTeamZones = GetTeamZones(GetOtherTeam(player.Team.Value));
                            if (playerWithPossessionSteamId != player.SteamId.Value.ToString() && _puckZone != otherTeamZones[0] && _puckZone != otherTeamZones[1] && (playerZone == otherTeamZones[0] || playerZone == otherTeamZones[1])) {
                                if (!IsOffside(player.Team.Value)) {
                                    _puckLastPositionBeforeCall = puck.Rigidbody.transform.position;
                                    _puckLastZoneBeforeCall = _puckZone;
                                    //UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {player.Team.Value.ToString().ToUpperInvariant()} TEAM");
                                }

                                lock (_locker)
                                    _isOffside[playerSteamId] = (player.Team.Value, true);
                            }

                            // Is not offside.
                            lock (_locker) {
                                if (_playersZone[playerSteamId].Zone != otherTeamZones[0] && _playersZone[playerSteamId].Zone != otherTeamZones[1] && _isOffside[playerSteamId].IsOffside) {
                                    _isOffside[playerSteamId] = (player.Team.Value, false);
                                    if (!IsOffside(player.Team.Value)) {
                                        _puckLastPositionBeforeCall = puck.Rigidbody.transform.position;
                                        _puckLastZoneBeforeCall = _puckZone;
                                        //UIChat.Instance.Server_SendSystemChatMessage($"OFFSIDE {player.Team.Value.ToString().ToUpperInvariant()} TEAM CALLED OFF");
                                    }
                                }
                            }

                            // Remove offside if the other team entered the zone with the puck.
                            List<Zone> lastPlayerOnPuckTeamZones = GetTeamZones(_lastPlayerOnPuckTeam, true);
                            if (oldZone == lastPlayerOnPuckTeamZones[2] && _puckZone == lastPlayerOnPuckTeamZones[0]) {
                                PlayerTeam otherTeam = GetOtherTeam(_lastPlayerOnPuckTeam);
                                lock (_locker) {
                                    foreach (string key in new List<string>(_isOffside.Keys)) {
                                        if (_isOffside[key].Team == otherTeam)
                                            _isOffside[key] = (_isOffside[key].Team, false);
                                    }
                                }
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
        public class GameManagerController_GameManagerController_Patch {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message) {
                try {
                    PlayerTeam playerTeam = (PlayerTeam)message["team"];
                    playerTeam = GetOtherTeam(playerTeam);

                    // No goal if offside.
                    if (IsOffside(playerTeam)) {
                        SetNextFaceoffPosition(playerTeam, false);
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

        private static void SetNextFaceoffPosition(PlayerTeam team, bool isIcing) {
            ushort teamOffset;
            if (team == PlayerTeam.Red)
                teamOffset = 2;
            else
                teamOffset = 0;

            if (_puckLastPositionBeforeCall.x < 0) {
                if (isIcing)
                    _nextFaceoffSpot = FaceoffSpot.BlueteamDZoneLeft + teamOffset;
                else
                    SetNextFaceoffPositionFromLastTouch(team, true);
            }
            else {
                if (isIcing)
                    _nextFaceoffSpot = FaceoffSpot.BlueteamDZoneRight + teamOffset;
                else
                    SetNextFaceoffPositionFromLastTouch(team, false);
            }
        }

        private static void SetNextFaceoffPositionFromLastTouch(PlayerTeam team, bool left) {
            Zone puckZone = GetZone(_puckLastPositionBeforeCall, _puckLastZoneBeforeCall, PUCK_RADIUS);
            if (puckZone == Zone.BlueTeam_BehindGoalLine || puckZone == Zone.BlueTeam_Zone) {
                if (team == PlayerTeam.Blue) {
                    if (left)
                        _nextFaceoffSpot = FaceoffSpot.BlueteamDZoneLeft;
                    else
                        _nextFaceoffSpot = FaceoffSpot.BlueteamDZoneRight;
                }
                else {
                    if (left)
                        _nextFaceoffSpot = FaceoffSpot.BlueteamBLLeft;
                    else
                        _nextFaceoffSpot = FaceoffSpot.BlueteamBLRight;
                }
            }
            else if (puckZone == Zone.RedTeam_BehindGoalLine || puckZone == Zone.RedTeam_Zone) {
                if (team == PlayerTeam.Red) {
                    if (left)
                        _nextFaceoffSpot = FaceoffSpot.RedteamDZoneLeft;
                    else
                        _nextFaceoffSpot = FaceoffSpot.RedteamDZoneRight;
                }
                else {
                    if (left)
                        _nextFaceoffSpot = FaceoffSpot.RedteamBLLeft;
                    else
                        _nextFaceoffSpot = FaceoffSpot.RedteamBLRight;
                }
            }
            else if (puckZone == Zone.BlueTeam_Center) {
                if (left)
                    _nextFaceoffSpot = FaceoffSpot.BlueteamBLLeft;
                else
                    _nextFaceoffSpot = FaceoffSpot.BlueteamBLRight;
            }
            else if (puckZone == Zone.RedTeam_Center) {
                if (left)
                    _nextFaceoffSpot = FaceoffSpot.RedteamBLLeft;
                else
                    _nextFaceoffSpot = FaceoffSpot.RedteamBLRight;
            }
        }

        private static Vector3 GetFaceoffDot() {
            Vector3 dot;

            switch (_nextFaceoffSpot) {
                case FaceoffSpot.BlueteamBLLeft:
                    dot = new Vector3(-9.975f, 0.01f, 11f);
                    break;

                case FaceoffSpot.BlueteamBLRight:
                    dot = new Vector3(9.975f, 0.01f, 11f);
                    break;

                case FaceoffSpot.RedteamBLLeft:
                    dot = new Vector3(-9.975f, 0.01f, -11f);
                    break;

                case FaceoffSpot.RedteamBLRight:
                    dot = new Vector3(9.975f, 0.01f, -11f);
                    break;

                case FaceoffSpot.BlueteamDZoneLeft:
                    dot = new Vector3(-9.95f, 0.01f, 29.75f);
                    break;

                case FaceoffSpot.BlueteamDZoneRight:
                    dot = new Vector3(9.95f, 0.01f, 29.75f);
                    break;

                case FaceoffSpot.RedteamDZoneLeft:
                    dot = new Vector3(-9.95f, 0.01f, -29.75f);
                    break;

                case FaceoffSpot.RedteamDZoneRight:
                    dot = new Vector3(9.95f, 0.01f, -29.75f);
                    break;

                default:
                    dot = new Vector3(0f, 0.01f, 0f);
                    break;
            }

            return dot;
        }

        private static bool IsPlayerPlaying(Player player) {
            return !(player.Role.Value == PlayerRole.None || !player.IsCharacterFullySpawned);
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

        private static Zone GetZone(Vector3 position, Zone oldZone, float radius) {
            float zMax = position.z + radius;
            
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

        private static bool IsIcingPossible(PlayerTeam team) {
            lock (_locker)
                return _isIcingPossible[team];
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
            lock (_locker) {
                dict = _playersLastTimePuckPossession
                    .Where(x => x.Value.ElapsedMilliseconds < MIN_POSSESSION_MILLISECONDS && x.Value.ElapsedMilliseconds > MAX_TIPPED_MILLISECONDS)
                    .ToDictionary(x => x.Key, x => x.Value);
            }
            if (dict.Count > 1) // Puck possession is challenged.
                return "";

            if (dict.Count == 1)
                return dict.First().Key;

            lock (_locker) {
                List<string> steamIds = _playersLastTimePuckPossession
                    .Where(x => x.Value.ElapsedMilliseconds < MAX_POSSESSION_MILLISECONDS && x.Value.ElapsedMilliseconds > MAX_TIPPED_MILLISECONDS)
                    .OrderBy(x => x.Value.ElapsedMilliseconds)
                    .Select(x => x.Key).ToList();

                if (steamIds.Count != 0)
                    return steamIds.First();
            }

            return "";
        }
        
        private static bool PuckIsTipped(string playerSteamId) {
            Stopwatch watch;
            lock (_locker) {
                if (!_playersCurrentPuckTouch.TryGetValue(playerSteamId, out watch))
                    return true;
            }

            if (watch.ElapsedMilliseconds < MAX_TIPPED_MILLISECONDS)
                return true;

            return false;
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

    public enum FaceoffSpot : ushort {
        Center,
        BlueteamBLLeft,
        BlueteamBLRight,
        RedteamBLLeft,
        RedteamBLRight,
        BlueteamDZoneLeft,
        BlueteamDZoneRight,
        RedteamDZoneLeft,
        RedteamDZoneRight,
    }
}
