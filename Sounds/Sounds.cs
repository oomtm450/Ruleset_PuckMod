using Codebase;
using HarmonyLib;
using oomtm450PuckMod_Sounds.Configs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace oomtm450PuckMod_Sounds {
    public class Sounds : IPuckMod {
        #region Constants
        /// <summary>
        /// Const string, version of the mod.
        /// </summary>
        private static readonly string MOD_VERSION = "0.2.0";

        /// <summary>
        /// List of string, last released versions of the mod.
        /// </summary>
        private static readonly ReadOnlyCollection<string> OLD_MOD_VERSIONS = new ReadOnlyCollection<string>(new List<string> {
            "0.1.0",
        });

        /// <summary>
        /// ReadOnlyCollection of string, collection of datanames to not log.
        /// </summary>
        private static readonly ReadOnlyCollection<string> DATA_NAMES_TO_IGNORE = new ReadOnlyCollection<string>(new List<string> {
            "eventName",
        });
        #endregion

        #region Fields and Properties
        // Server-side.
        /// <summary>
        /// ServerConfig, config set and sent by the server.
        /// </summary>
        internal static ServerConfig ServerConfig { get; set; } = new ServerConfig();

        /// <summary>
        /// LockDictionary of ulong and string, dictionary of all players
        /// </summary>
        private static readonly LockDictionary<ulong, string> _players_ClientId_SteamId = new LockDictionary<ulong, string>();

        private static readonly LockDictionary<ulong, DateTime> _sentOutOfDateMessage = new LockDictionary<ulong, DateTime>();

        /// <summary>
        /// Bool, true if there's a pause in play.
        /// </summary>
        private static bool _paused = false;

        /// <summary>
        /// Bool, true if the mod's logic has to be runned.
        /// </summary>
        private static bool _logic = true;

        /// <summary>
        /// Bool, true if phase was changed by a mod.
        /// </summary>
        private static bool _changedPhase = false;

        private static bool _hasPlayedLastMinuteMusic = false;

        private static bool _hasPlayedFirstFaceoffMusic = false;

        private static bool _hasPlayedSecondFaceoffMusic = false;

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

        // Client-side.
        /// <summary>
        /// ClientConfig, config set by the client.
        /// </summary>
        internal static ClientConfig ClientConfig = new ClientConfig();

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

        /// <summary>
        /// SoundsSystem, object containing all the code to load and play sounds.
        /// </summary>
        private static SoundsSystem _soundsSystem = null;

        /// <summary>
        /// String, current music playing.
        /// </summary>
        private static string _currentMusicPlaying = "";

        // Client-side.
        private static LockList<string> _extraSoundsToLoad = new LockList<string>();
        #endregion

        #region Harmony Patches
        /// <summary>
        /// Class that patches the Server_SetPhase event from GameManager.
        /// </summary>
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Server_SetPhase))]
        public class GameManager_Server_SetPhase_Patch {
            [HarmonyPrefix]
            public static bool Prefix(GamePhase phase, ref int time) {
                try {
                    // If this is not the server, do not use the patch.
                    if (!ServerFunc.IsDedicatedServer() || !_logic)
                        return true;

                    if (phase == GamePhase.BlueScore && ServerConfig.EnableMusic) {
                        _currentMusicPlaying = Codebase.SoundsSystem.BLUE_GOAL_MUSIC;
                        NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                    }
                    else if (phase == GamePhase.RedScore && ServerConfig.EnableMusic) {
                        _currentMusicPlaying = Codebase.SoundsSystem.RED_GOAL_MUSIC;
                        NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                    }
                    else if (phase == GamePhase.PeriodOver && ServerConfig.EnableMusic) {
                        _currentMusicPlaying = Codebase.SoundsSystem.BETWEEN_PERIODS_MUSIC;
                        NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                    }

                    if (!_changedPhase) {
                        if ((string.IsNullOrEmpty(_currentMusicPlaying) || _currentMusicPlaying == Codebase.SoundsSystem.WARMUP_MUSIC) && ServerConfig.EnableMusic) {
                            NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);

                            if (phase == GamePhase.FaceOff) {
                                if (!_hasPlayedLastMinuteMusic && GameManager.Instance.GameState.Value.Time <= 60 && GameManager.Instance.GameState.Value.Period == 3) {
                                    _hasPlayedLastMinuteMusic = true;
                                    _currentMusicPlaying = Codebase.SoundsSystem.LAST_MINUTE_MUSIC;
                                }
                                else if (!_hasPlayedFirstFaceoffMusic) {
                                    _hasPlayedFirstFaceoffMusic = true;
                                    _currentMusicPlaying = Codebase.SoundsSystem.FIRST_FACEOFF_MUSIC;
                                }
                                else if (!_hasPlayedSecondFaceoffMusic) {
                                    _hasPlayedSecondFaceoffMusic = true;
                                    _currentMusicPlaying = Codebase.SoundsSystem.SECOND_FACEOFF_MUSIC;
                                }
                                else
                                    _currentMusicPlaying = Codebase.SoundsSystem.FACEOFF_MUSIC;

                                NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                                _currentMusicPlaying = Codebase.SoundsSystem.FACEOFF_MUSIC;
                                return true;
                            }
                        }

                        if (phase == GamePhase.GameOver && ServerConfig.EnableMusic) {
                            NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(Codebase.SoundsSystem.GAMEOVER_MUSIC), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            _currentMusicPlaying = Codebase.SoundsSystem.GAMEOVER_MUSIC;
                        }
                        else if (phase == GamePhase.Warmup && ServerConfig.EnableMusic) {
                            NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(Codebase.SoundsSystem.WARMUP_MUSIC), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            _currentMusicPlaying = Codebase.SoundsSystem.WARMUP_MUSIC;
                        }

                        return true;
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
                    if (!ServerFunc.IsDedicatedServer() || !_logic)
                        return;

                    if (phase == GamePhase.Playing) {
                        NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.MUSIC, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        _currentMusicPlaying = "";
                        return;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(GameManager_Server_SetPhase_Patch)} Postfix().\n{ex}", ServerConfig);
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

                    // Reset music.
                    NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.MUSIC, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                    _currentMusicPlaying = "";
                    _hasPlayedLastMinuteMusic = false;
                    _hasPlayedFirstFaceoffMusic = false;
                    _hasPlayedSecondFaceoffMusic = false;

                    _sentOutOfDateMessage.Clear();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(GameManager_Server_ResetGameState_Patch)} Postfix().\n{ex}", ServerConfig);
                }
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
                    if (ServerFunc.IsDedicatedServer() || !ClientConfig.CustomGoalHorns)
                        return true;

                    AudioSource audioSource = SystemFunc.GetPrivateField<AudioSource>(typeof(SynchronizedAudio), __instance, "audioSource");

                    if (audioSource.name == "Blue Goal" || audioSource.name == "Red Goal") {
                        if (audioSource.clip == null)
                            return false;

                        volume = 1f;
                        pitch = 1f;
                        isOneShot = true;
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(SynchronizedAudio_Server_PlayRpc_Patch)} Prefix().\n{ex}", ClientConfig);
                }

                return true;
            }
        }

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

                        if (message.StartsWith(@"/musicvol")) {
                            message = message.Replace(@"/musicvol", "").Trim();

                            if (string.IsNullOrEmpty(message))
                                UIChat.Instance.AddChatMessage($"Music volume is currently at {ClientConfig.MusicVolume.ToString(CultureInfo.InvariantCulture)}");
                            else {
                                if (float.TryParse(message, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float vol)) {
                                    if (vol > 1f)
                                        vol = 1f;
                                    else if (vol < 0)
                                        vol = 0;

                                    ClientConfig.MusicVolume = vol;
                                    ClientConfig.Save();
                                    _soundsSystem?.ChangeMusicVolume(ClientConfig.MusicVolume);
                                    UIChat.Instance.AddChatMessage($"Adjusted client music volume to {vol.ToString(CultureInfo.InvariantCulture)}");
                                }
                            }
                        }
                        else if (message.StartsWith(@"/warmupmusic")) {
                            message = message.Replace(@"/warmupmusic", "").Trim();

                            if (string.IsNullOrEmpty(message))
                                UIChat.Instance.AddChatMessage($"Warmup music is currently {(ClientConfig.WarmupMusic ? "enabled" : "disabled")}");
                            else {
                                bool? enableWarmupMusic = null;
                                if (int.TryParse(message, out int warmupMusicValue)) {
                                    if (warmupMusicValue >= 1)
                                        enableWarmupMusic = true;
                                    else
                                        enableWarmupMusic = false;
                                }
                                else if (message == "true")
                                    enableWarmupMusic = true;
                                else if (message == "false")
                                    enableWarmupMusic = false;

                                if (enableWarmupMusic != null) {
                                    if (_soundsSystem != null && _soundsSystem.WarmupMusicList.Contains(_currentMusicPlaying)) {
                                        if (ClientConfig.WarmupMusic && !((bool)enableWarmupMusic))
                                            _soundsSystem.StopAll();
                                        else if (!ClientConfig.WarmupMusic && (bool)enableWarmupMusic)
                                            _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, 0, true);
                                    }

                                    ClientConfig.WarmupMusic = (bool)enableWarmupMusic;
                                    ClientConfig.Save();
                                    if ((bool)enableWarmupMusic)
                                        UIChat.Instance.AddChatMessage($"Enabled warmup music");
                                    else
                                        UIChat.Instance.AddChatMessage($"Disabled warmup music");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(UIChat_Client_SendClientChatMessage_Patch)} Prefix().\n{ex}", ServerConfig);
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
                            UIChat.Instance.AddChatMessage("Sounds commands:\n* <b>/musicvol</b> - Adjust music volume (0.0-1.0)\n* <b>/warmupmusic</b> - Disable or enable warmup music (false-true)\n");
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(UIChat_Client_SendClientChatMessage_Patch)} Postfix().\n{ex}", ServerConfig);
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
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT, ReceiveData);
                        _hasRegisteredWithNamedMessageHandler = true;

                        DateTime now = DateTime.UtcNow;
                        if (_lastDateTimeAskStartupData != DateTime.MinValue && _lastDateTimeAskStartupData + TimeSpan.FromSeconds(5) < now && _askServerForStartupDataCount++ < 6) {
                            _lastDateTimeAskStartupData = now;
                            NetworkCommunication.SendData(Constants.ASK_SERVER_FOR_STARTUP_DATA, "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, ClientConfig);
                        }
                        else if (_lastDateTimeAskStartupData == DateTime.MinValue)
                            _lastDateTimeAskStartupData = now;
                    }
                    else if (_askForKick) {
                        _askForKick = false;
                        NetworkCommunication.SendData(Constants.MOD_NAME + "_kick", "1", NetworkManager.ServerClientId, Constants.FROM_CLIENT_TO_SERVER, ClientConfig);
                    }
                    else if (_addServerModVersionOutOfDateMessage) {
                        _addServerModVersionOutOfDateMessage = false;
                        UIChat.Instance.AddChatMessage($"Server's {Constants.WORKSHOP_MOD_NAME} mod is out of date. Some functionalities might not work properly.");
                    }

                    if (_soundsSystem != null) {
                        while (_extraSoundsToLoad.Count != 0) {
                            string path = _extraSoundsToLoad.First();
                            _soundsSystem.LoadSounds(ClientConfig.Music, ClientConfig.CustomGoalHorns, path);
                            _extraSoundsToLoad.Remove(path);
                        }
                    }
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(UIScoreboard_UpdatePlayer_Patch)} Postfix().\n{ex}", ClientConfig);
                }
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
                    ClientConfig = ClientConfig.ReadConfig();
                }

                Logging.Log("Subscribing to events.", ServerConfig, true);

                if (ServerFunc.IsDedicatedServer()) {
                    EventManager.Instance.AddEventListener("Event_OnClientConnected", Event_OnClientConnected);
                    EventManager.Instance.AddEventListener("Event_OnClientDisconnected", Event_OnClientDisconnected);
                    EventManager.Instance.AddEventListener("Event_OnPlayerRoleChanged", Event_OnPlayerRoleChanged);
                    EventManager.Instance.AddEventListener(Codebase.Constants.SOUNDS_MOD_NAME, Event_OnSoundsTrigger);
                }
                else {
                    EventManager.Instance.AddEventListener("Event_OnSceneLoaded", Event_OnSceneLoaded);
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
                    EventManager.Instance.RemoveEventListener(Codebase.Constants.SOUNDS_MOD_NAME, Event_OnSoundsTrigger);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_CLIENT_TO_SERVER);
                }
                else {
                    EventManager.Instance.RemoveEventListener("Event_OnSceneLoaded", Event_OnSceneLoaded);
                    EventManager.Instance.RemoveEventListener("Event_Client_OnClientStopped", Event_Client_OnClientStopped);
                    NetworkManager.Singleton?.CustomMessagingManager?.UnregisterNamedMessageHandler(Constants.FROM_SERVER_TO_CLIENT);
                }

                _hasRegisteredWithNamedMessageHandler = false;
                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                if (_soundsSystem != null) {
                    _soundsSystem.StopAll();
                    _currentMusicPlaying = "";

                    _soundsSystem.DestroyGameObjects();
                    _soundsSystem = null;
                }

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
                Logging.LogError($"Error in {nameof(Event_OnSceneLoaded)}.\n{ex}", ClientConfig);
            }
        }

        public static void Event_OnSoundsTrigger(Dictionary<string, object> message) {
            try {
                foreach (KeyValuePair<string, object> kvp in message) {
                    string value = (string)kvp.Value;
                    if (!NetworkCommunication.GetDataNamesToIgnore().Contains(kvp.Key))
                        Logging.Log($"Received data {kvp.Key}. Content : {value}", ServerConfig);

                    switch (kvp.Key) {
                        case Codebase.Constants.LOGIC:
                            _logic = bool.Parse(value);
                            break;

                        case Codebase.SoundsSystem.PLAY_SOUND:
                            if (value == Codebase.SoundsSystem.FACEOFF_MUSIC && ServerConfig.EnableMusic)
                                PlayFaceoffMusic();
                            break;

                        case Codebase.SoundsSystem.STOP_SOUND:
                            if (_soundsSystem == null)
                                break;

                            if (value == Codebase.SoundsSystem.ALL)
                                NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.ALL, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            else if (value == Codebase.SoundsSystem.MUSIC) {
                                NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.STOP_SOUND, Codebase.SoundsSystem.MUSIC, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                                _currentMusicPlaying = "";
                            }
                            break;
                    }
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_OnSoundsTrigger)}.\n{ex}", ServerConfig);
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

                        case Codebase.Constants.CHANGED_PHASE:
                            _changedPhase = bool.Parse(value);
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
                    _players_ClientId_SteamId.Add(clientId, "");
                }
                catch {
                    _players_ClientId_SteamId.Remove(clientId);
                    _players_ClientId_SteamId.Add(clientId, "");
                }
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
                    clientSteamId = _players_ClientId_SteamId[clientId];
                }
                catch {
                    Logging.LogError($"Client Id {clientId} steam Id not found in {nameof(_players_ClientId_SteamId)}.", ServerConfig);
                    return;
                }

                _sentOutOfDateMessage.Remove(clientId);

                _players_ClientId_SteamId.Remove(clientId);
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

            //Logging.Log("Event_Client_OnClientStopped", ClientConfig);

            try {
                ServerConfig = new ServerConfig();

                _serverHasResponded = false;
                _askServerForStartupDataCount = 0;

                if (_soundsSystem == null)
                    return;

                if (_soundsSystem != null) {
                    if (!string.IsNullOrEmpty(_currentMusicPlaying)) {
                        _soundsSystem.Stop(_currentMusicPlaying);
                        _currentMusicPlaying = "";
                    }

                    _soundsSystem.DestroyGameObjects();
                    _soundsSystem = null;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(Event_Client_OnClientStopped)}.\n{ex}", ClientConfig);
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

        /// <summary>
        /// Method that loads the assets for the client-side sounds.
        /// </summary>
        private static void LoadAssets() {
            if (_soundsSystem == null) {
                GameObject soundsGameObject = new GameObject(Constants.MOD_NAME + "_Sounds");
                _soundsSystem = soundsGameObject.AddComponent<SoundsSystem>();
            }

            //_soundsSystem.LoadSounds(ClientConfig.Music, ClientConfig.CustomGoalHorns);
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
                    (dataName, dataStr) = NetworkCommunication.GetData(clientId, reader, ClientConfig);
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

                    case Constants.ASK_SERVER_FOR_STARTUP_DATA: // SERVER-SIDE : Send the necessary data to client.
                        if (dataStr != "1") {
                            string[] dataStrSplitted = dataStr.Substring(1).Substring(0, dataStr.Length - 2).Split(',');

                            NetworkCommunication.SendData(dataStrSplitted[0] + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, dataStrSplitted[0] + Constants.FROM_SERVER_TO_CLIENT, ServerConfig);

                            if (ServerConfig.ForceServerPacks && !GetModsList().Contains(dataStrSplitted[0])) {
                                Logging.Log($"Can't load the SoundsPack \"{dataStrSplitted[0]}\" because of server's ForceServerPacks option.", ServerConfig);
                                break;
                            }

                            NetworkCommunication.SendData(Codebase.SoundsSystem.LOAD_EXTRA_SOUNDS, dataStrSplitted[1], clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                            break;
                        }

                        NetworkCommunication.SendData(Constants.MOD_NAME + "_" + nameof(MOD_VERSION), MOD_VERSION, clientId, Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
                        break;

                    case Codebase.SoundsSystem.PLAY_SOUND: // CLIENT-SIDE : Play sound.
                        if (_soundsSystem == null)
                            break;
                        if (_soundsSystem.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _soundsSystem.", ClientConfig);
                            foreach (string error in _soundsSystem.Errors)
                                Logging.LogError(error, ClientConfig);
                        }

                        int? seed = null;
                        string[] playSoundDataStrSplitted = dataStr.Split(';');

                        if (int.TryParse(playSoundDataStrSplitted[1], out int _seed))
                            seed = _seed;

                        bool isFaceoffMusic = false;
                        float delay = 0;
                        if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.FACEOFF_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.FaceoffMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.FACEOFF_MUSIC_DELAYED) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.FaceoffMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.BLUE_GOAL_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.BlueGoalMusicList, seed);
                            _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, 2.25f);
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.RED_GOAL_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.RedGoalMusicList, seed);
                            _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, 2.25f);
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.BETWEEN_PERIODS_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.BetweenPeriodsMusicList, seed);
                            _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, 1.5f);
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.WARMUP_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.WarmupMusicList, seed);
                            if (ClientConfig.WarmupMusic)
                                _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, 0, true);
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.LAST_MINUTE_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.LastMinuteMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.FIRST_FACEOFF_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.FirstFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.SECOND_FACEOFF_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.SecondFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.LAST_MINUTE_MUSIC_DELAYED) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.LastMinuteMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.FIRST_FACEOFF_MUSIC_DELAYED) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.FirstFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.SECOND_FACEOFF_MUSIC_DELAYED) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.SecondFaceoffMusicList, seed);
                            isFaceoffMusic = true;
                            delay = 1f;
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.GAMEOVER_MUSIC) {
                            _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.GameOverMusicList, seed);
                            _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, 0.5f);
                        }
                        else if (playSoundDataStrSplitted[0] == Codebase.SoundsSystem.WHISTLE)
                            _soundsSystem.Play(Codebase.SoundsSystem.WHISTLE, "");

                        if (isFaceoffMusic) {
                            if (string.IsNullOrEmpty(_currentMusicPlaying))
                                _currentMusicPlaying = SoundsSystem.GetRandomSound(_soundsSystem.FaceoffMusicList, seed);
                            _soundsSystem.Play(_currentMusicPlaying, Codebase.SoundsSystem.MUSIC, delay);
                        }
                        break;

                    case Codebase.SoundsSystem.STOP_SOUND: // CLIENT-SIDE : Stop sound.
                        if (_soundsSystem == null)
                            break;
                        if (_soundsSystem.Errors.Count != 0) {
                            Logging.LogError("There was an error when initializing _soundsSystem.", ClientConfig);
                            foreach (string error in _soundsSystem.Errors)
                                Logging.LogError(error, ClientConfig);
                        }

                        if (dataStr == Codebase.SoundsSystem.MUSIC) {
                            if (!string.IsNullOrEmpty(_currentMusicPlaying))
                                _soundsSystem.Stop(_currentMusicPlaying);
                        }
                        else if (dataStr == Codebase.SoundsSystem.ALL)
                            _soundsSystem.StopAll();

                        _currentMusicPlaying = "";
                        break;

                    case Codebase.SoundsSystem.LOAD_EXTRA_SOUNDS:
                        if (_soundsSystem == null) {
                            _extraSoundsToLoad.Add(dataStr);
                            break;
                        }

                        _soundsSystem.LoadSounds(ClientConfig.Music, ClientConfig.CustomGoalHorns, dataStr);

                        while (_extraSoundsToLoad.Count != 0) {
                            string path = _extraSoundsToLoad.First();
                            _soundsSystem.LoadSounds(ClientConfig.Music, ClientConfig.CustomGoalHorns, path);
                            _extraSoundsToLoad.Remove(path);
                        }
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in ReceiveData.\n{ex}", ServerConfig);
            }
        }

        private static void PlayFaceoffMusic() {
            if (!ServerConfig.EnableMusic)
                return;

            if (!_hasPlayedLastMinuteMusic && GameManager.Instance.GameState.Value.Time <= 60 && GameManager.Instance.GameState.Value.Period == 3) {
                _hasPlayedLastMinuteMusic = true;
                _currentMusicPlaying = Codebase.SoundsSystem.LAST_MINUTE_MUSIC_DELAYED;
            }
            else if (!_hasPlayedFirstFaceoffMusic) {
                _hasPlayedFirstFaceoffMusic = true;
                _currentMusicPlaying = Codebase.SoundsSystem.FIRST_FACEOFF_MUSIC_DELAYED;
            }
            else if (!_hasPlayedSecondFaceoffMusic) {
                _hasPlayedSecondFaceoffMusic = true;
                _currentMusicPlaying = Codebase.SoundsSystem.SECOND_FACEOFF_MUSIC_DELAYED;
            }
            else
                _currentMusicPlaying = Codebase.SoundsSystem.FACEOFF_MUSIC_DELAYED;

            NetworkCommunication.SendDataToAll(Codebase.SoundsSystem.PLAY_SOUND, SoundsSystem.FormatSoundStrForCommunication(_currentMusicPlaying), Constants.FROM_SERVER_TO_CLIENT, ServerConfig);
            _currentMusicPlaying = Codebase.SoundsSystem.FACEOFF_MUSIC;
        }

        /// <summary>
        /// Function that returns a list of all mods assembly's name.
        /// </summary>
        /// <returns>List of string, all mods assembly's name.</returns>
        private static List<string> GetModsList() {
            List<string> mods = new List<string>();
            if (ModManagerV2.Instance == null || !ModManagerV2.Instance)
                return mods;

            foreach (Mod mod in ModManagerV2.Instance.Mods) {
                Assembly modAssembly = SystemFunc.GetPrivateField<Assembly>(typeof(Mod), mod, "assembly");
                if (modAssembly == null)
                    continue;

                mods.Add(modAssembly.GetName().Name);
            }

            return mods;
        }
        #endregion
    }
}
