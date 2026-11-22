using Codebase;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class that delays ref call sounds by a short "mercy" time, so a call that is cancelled
    /// (or re-triggered) right away doesn't play any sound, nor both the pending and cancelled sounds.
    /// Runs on the Unity main thread (Update), because the sounds can't be played from a Timer thread.
    /// </summary>
    internal class RefSoundDelayer : MonoBehaviour {
        #region Constants
        /// <summary>
        /// Float, mercy time in seconds before a ref sound is actually played.
        /// </summary>
        internal const float DELAY_SECONDS = 0.15f;
        #endregion

        #region Classes
        private class Entry {
            internal bool IsPending;
            internal float DueTime;
            internal Action Play;
        }
        #endregion

        #region Fields
        private static RefSoundDelayer _instance;

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        private readonly List<string> _dueKeys = new List<string>();
        #endregion

        #region Methods/Functions
        /// <summary>
        /// Function that schedules a ref sound to be played after the mercy time.
        /// If the opposite kind of sound (pending vs cancelled) is already waiting for the same key,
        /// both are dropped and nothing is played.
        /// </summary>
        /// <param name="key">String, unique key of the call (signal + team).</param>
        /// <param name="isPending">Bool, true if the sound is the "pending" one, false if it is the "cancelled" one.</param>
        /// <param name="play">Action, code that actually plays the sound.</param>
        internal static void Schedule(string key, bool isPending, Action play) {
            if (_instance == null) { // Unity null check, also covers a destroyed instance.
                GameObject go = new GameObject(nameof(RefSoundDelayer));
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RefSoundDelayer>();
            }

            _instance.Enqueue(key, isPending, play);
        }

        private void Enqueue(string key, bool isPending, Action play) {
            if (_entries.TryGetValue(key, out Entry waiting) && waiting.IsPending != isPending) {
                // Opposite sound still waiting : the call flipped within the mercy time, play nothing.
                _entries.Remove(key);
                return;
            }

            // Nothing waiting (or a duplicate of the same kind, which simply replaces the old one).
            _entries[key] = new Entry {
                IsPending = isPending,
                DueTime = Time.unscaledTime + DELAY_SECONDS,
                Play = play,
            };
        }

        private void Update() {
            if (_entries.Count == 0)
                return;

            float now = Time.unscaledTime;

            _dueKeys.Clear();
            foreach (KeyValuePair<string, Entry> entry in _entries) {
                if (entry.Value.DueTime <= now)
                    _dueKeys.Add(entry.Key);
            }

            foreach (string key in _dueKeys) {
                Entry due = _entries[key];
                _entries.Remove(key);

                try {
                    due.Play();
                }
                catch (Exception ex) {
                    Logging.LogError($"Error in {nameof(RefSoundDelayer)}.{nameof(Update)}.\n{ex}", Ruleset.ServerConfig);
                }
            }
        }
        #endregion
    }
}