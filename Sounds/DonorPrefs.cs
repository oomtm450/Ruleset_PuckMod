using Codebase;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace oomtm450PuckMod_Sounds {
    /// <summary>
    /// Class that reads per-steamId donor goal-song / goal-horn picks from
    /// poncepuck_user_data.json, the file owned and written atomically by TagMod
    /// (UnifiedTagModPlugin). Mtime-cached so a goal lookup never reparses JSON
    /// unless TagMod actually updated the file. Non-donors return "" — callers
    /// use that as the "fall through to default behavior" signal.
    /// </summary>
    internal static class DonorPrefs {
        #region Fields
        /// <summary>
        /// Object, lock to guard the cached dictionaries from concurrent reads and reloads.
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// Dictionary of string and string, cached map of steamId to donor's chosen song id.
        /// </summary>
        private static Dictionary<string, string> _songs = new Dictionary<string, string>();

        /// <summary>
        /// Dictionary of string and string, cached map of steamId to donor's chosen horn id.
        /// </summary>
        private static Dictionary<string, string> _horns = new Dictionary<string, string>();

        /// <summary>
        /// DateTime, last mtime we successfully read from the prefs file. Used to skip parses.
        /// </summary>
        private static DateTime _lastReadUtc = DateTime.MinValue;
        #endregion

        #region Properties
        /// <summary>
        /// String, full path to the donor prefs file owned by TagMod.
        /// </summary>
        private static string PrefsPath => Path.Combine(Path.GetFullPath("."), "config", "poncepuck_user_data.json");
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Method that returns the donor's chosen song id for a given steamId, or "" if none.
        /// </summary>
        /// <param name="steamId">String, steamId of the player to look up.</param>
        /// <returns>String, donor's chosen song id or "" if non-donor / no pick.</returns>
        internal static string GetSong(string steamId) {
            if (string.IsNullOrEmpty(steamId))
                return "";

            RefreshIfStale();
            lock (_lock) {
                return _songs.TryGetValue(steamId, out string v) ? (v ?? "") : "";
            }
        }

        /// <summary>
        /// Method that returns the donor's chosen horn id for a given steamId, or "" if none.
        /// </summary>
        /// <param name="steamId">String, steamId of the player to look up.</param>
        /// <returns>String, donor's chosen horn id or "" if non-donor / no pick.</returns>
        internal static string GetHorn(string steamId) {
            if (string.IsNullOrEmpty(steamId))
                return "";

            RefreshIfStale();
            lock (_lock) {
                return _horns.TryGetValue(steamId, out string v) ? (v ?? "") : "";
            }
        }

        /// <summary>
        /// Method that reparses the prefs file only if its mtime has changed since the last read.
        /// On error, leaves _lastReadUtc untouched so the next call retries.
        /// </summary>
        private static void RefreshIfStale() {
            try {
                string path = PrefsPath;
                if (!File.Exists(path))
                    return;

                DateTime mtime = File.GetLastWriteTimeUtc(path);
                lock (_lock) {
                    if (mtime <= _lastReadUtc)
                        return;

                    string json = File.ReadAllText(path);
                    UserDataDto parsed = JsonConvert.DeserializeObject<UserDataDto>(json);
                    if (parsed != null) {
                        _songs = parsed.GoalSongs ?? new Dictionary<string, string>();
                        _horns = parsed.GoalHorns ?? new Dictionary<string, string>();
                    }
                    _lastReadUtc = mtime;
                }
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(RefreshIfStale)}.\n{ex}", Sounds.ServerConfig);
            }
        }
        #endregion

        #region Nested Types
        /// <summary>
        /// Class that mirrors the GoalSongs / GoalHorns subset of TagMod's UserData JSON shape.
        /// </summary>
        private class UserDataDto {
            public Dictionary<string, string> GoalSongs { get; set; }
            public Dictionary<string, string> GoalHorns { get; set; }
        }
        #endregion
    }
}
