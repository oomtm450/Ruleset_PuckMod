using Codebase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace oomtm450PuckMod_Ruleset.UI {
    internal static class PenLabelUI {
        private const string PEN_LABEL_UI_FOLDER = "ui";
        private const string PEN_LABEL_UI_ASSET_BUNDLE = "penlabelui";

        internal static GameObject GameObject { get; set; } = null;

        private static AssetBundle _assetBundle = null;

        private static VisualElement _root = null;
        private static VisualElement _bluePanel = null;
        private static VisualElement _redPanel = null;

        private static readonly LockDictionary<PlayerTeam, Label> _activeLabels = new LockDictionary<PlayerTeam, Label>();

        internal static LockList<(string SteamId, PausableTimer Timer)> PenaltyTimers { get; } = new LockList<(string SteamId, PausableTimer Timer)>();

        private static Timer _penaltiesLabelTimer = null;

        internal static void Initialize(VisualElement root) {
            _root = root;
            _bluePanel = _root.Q<VisualElement>("blue-team-panel");
            _redPanel = _root.Q<VisualElement>("red-team-panel");

            AddTeamLabel(PlayerTeam.Blue);
            AddTeamLabel(PlayerTeam.Red);
        }

        internal static void AddTeamLabel(PlayerTeam team) {
            var label = new Label();
            label.AddToClassList("player-label");
            label.AddToClassList("player-name");
            label.AddToClassList(team == PlayerTeam.Blue ? "player-name--blue" : "player-name--red");

            GetPanel(team).Add(label);
            _activeLabels[team] = label;
        }

        private static VisualElement GetPanel(PlayerTeam team) => team == PlayerTeam.Blue ? _bluePanel : _redPanel;

        internal static void AddPenaltiesLabel() {
            try {
                if (_activeLabels.Count != 0)
                    return;

                string bundlePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), PEN_LABEL_UI_FOLDER, PEN_LABEL_UI_ASSET_BUNDLE);

                if (_assetBundle == null)
                    _assetBundle = AssetBundle.LoadFromFile(bundlePath);

                if (_assetBundle == null) {
                    Logging.LogError("Failed to load AssetBundle !!!", Ruleset.ClientConfig);
                    return;
                }

                VisualTreeAsset visualTree = _assetBundle.LoadAsset<VisualTreeAsset>("assets/penlabelui.uxml");
                PanelSettings panelSettings = _assetBundle.LoadAsset<PanelSettings>("assets/penlabeluipanelsettings.asset");
                StyleSheet styleSheet = _assetBundle.LoadAsset<StyleSheet>("assets/penlabelui.uss");

                if (visualTree == null) {
                    Logging.LogError($"Missing {nameof(visualTree)} in asset bundle !!!", Ruleset.ClientConfig);
                    _assetBundle.Unload(false);
                    return;
                }

                if (panelSettings == null) {
                    Logging.LogError($"Missing {nameof(panelSettings)} in asset bundle !!!", Ruleset.ClientConfig);
                    _assetBundle.Unload(false);
                    return;
                }

                if (styleSheet == null) {
                    Logging.LogError($"Missing {nameof(styleSheet)} in asset bundle !!!", Ruleset.ClientConfig);
                    _assetBundle.Unload(false);
                    return;
                }

                _assetBundle.Unload(false);

                GameObject = new GameObject("PenLabelUI");
                GameObject.DontDestroyOnLoad(GameObject);
                UIDocument uiDocument = GameObject.AddComponent<UIDocument>();
                uiDocument.panelSettings = panelSettings;
                uiDocument.visualTreeAsset = visualTree;

                _root = uiDocument.rootVisualElement;
                _root.styleSheets.Add(styleSheet);

                Initialize(_root);

                _penaltiesLabelTimer = new Timer(PenaltiesLabelTimerCallback, null, 0, 1000);
            }
            catch (Exception ex) {
                Logging.LogError($"Error in {nameof(AddPenaltiesLabel)}.\n{ex}", Ruleset.ClientConfig);
            }
        }

        private static void PenaltiesLabelTimerCallback(object stateInfo) {
            try {
                if (Ruleset.Paused)
                    return;

                if (!_activeLabels.TryGetValue(PlayerTeam.Blue, out Label blueTeamLabel))
                    return;

                if (blueTeamLabel == null)
                    return;

                if (!_activeLabels.TryGetValue(PlayerTeam.Red, out Label redTeamLabel))
                    return;

                if (redTeamLabel == null)
                    return;

                List<(string PlayerIdentity, PausableTimer Timer)> penaltyTimers = new List<(string, PausableTimer)>(PenaltyTimers);

                // Blue team.
                string penaltyTimersTextBlueTeam = "";
                var bluePenaltyTimers = penaltyTimers.Where(x => x.PlayerIdentity.StartsWith("B"));

                foreach (var timer in bluePenaltyTimers) {
                    TimeSpan ts = TimeSpan.FromMilliseconds(timer.Timer.MillisecondsLeft);
                    penaltyTimersTextBlueTeam += $"{timer.PlayerIdentity.Remove(0, 2)} {string.Format("{0}:{1:00}", (int)ts.TotalMinutes, ts.Seconds)}\n";
                }

                if (!string.IsNullOrEmpty(penaltyTimersTextBlueTeam))
                    penaltyTimersTextBlueTeam = penaltyTimersTextBlueTeam.Remove(penaltyTimersTextBlueTeam.Length - 1);

                var redPenaltyTimers = penaltyTimers.Where(x => x.PlayerIdentity.StartsWith("R"));

                blueTeamLabel.text = penaltyTimersTextBlueTeam;

                // Red team.
                string penaltyTimersTextRedTeam = "";
                foreach (var timer in redPenaltyTimers) {
                    TimeSpan ts = TimeSpan.FromMilliseconds(timer.Timer.MillisecondsLeft);
                    penaltyTimersTextRedTeam += $"{timer.PlayerIdentity.Remove(0, 2)} {string.Format("{0}:{1:00}", (int)ts.TotalMinutes, ts.Seconds)}\n";
                }

                if (!string.IsNullOrEmpty(penaltyTimersTextRedTeam))
                    penaltyTimersTextRedTeam = penaltyTimersTextRedTeam.Remove(penaltyTimersTextRedTeam.Length - 1);

                redTeamLabel.text = penaltyTimersTextRedTeam;
            }
            catch (Exception ex) {
                _penaltiesLabelTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _penaltiesLabelTimer.Dispose();
                _penaltiesLabelTimer = null;
                Logging.LogError($"Error in {nameof(PenaltiesLabelTimerCallback)}.\n{ex}", Ruleset.ClientConfig);
            }
        }

        internal static void Dispose() {
            if (GameObject == null)
                return;

            GameObject.Destroy(GameObject);

            if (_penaltiesLabelTimer != null) {
                _penaltiesLabelTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _penaltiesLabelTimer.Dispose();
                _penaltiesLabelTimer = null;
            }

            _root = null;
            _bluePanel = null;
            _redPanel = null;

            _activeLabels.Clear();
            PenaltyTimers.Clear();

            GameObject = null;
        }
    }
}
