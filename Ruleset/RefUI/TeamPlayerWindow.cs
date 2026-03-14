using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset.RefUI {
    internal class TeamPlayerWindow : ResizableWindow {
        private static readonly string[] PlayerPenalties = { "GINT", "INT", "TRIP", "EMBEL", "DOG", "FOFF" };
        private static readonly string[] DoubleIdPenalties = { "GINT", "INT", "TRIP" };

        private readonly string _teamName;
        private readonly PlayerTeam _team;

        protected override string Title => $"{_teamName} Team";

        internal TeamPlayerWindow(string teamName, PlayerTeam team, int windowId, Rect defaultRect)
            : base(windowId, defaultRect) {
            _teamName = teamName;
            _team = team;
        }

        protected override void DrawContent() {
            GUILayout.Label($"── {_teamName} ──", RefUIStyles.GetHeaderStyle(_teamName), GUILayout.Height(28));
            GUILayout.Space(4);

            List<Player> players = GetTeamPlayers();

            if (players.Count == 0) {
                GUILayout.Label("  No players spawned.");
                return;
            }

            foreach (Player player in players) {
                DrawPlayerRow(player);
            }
        }

        private List<Player> GetTeamPlayers() {
            try {
                var all = PlayerManager.Instance?.GetSpawnedPlayers(false);
                if (all == null)
                    return new List<Player>();

                return all.Where(p => p.Team.Value == _team).OrderBy(p => p.Number.Value).ToList();
            }
            catch {
                return new List<Player>();
            }
        }

        private void DrawPlayerRow(Player player) {
            string playerName = player.Username.Value.ToString();
            string playerNumber = player.Number.Value.ToString();
            string steamId = player.SteamId.Value.ToString();
            string displayName = $"#{playerNumber} {playerName}";

            if (displayName.Length > 16)
                displayName = displayName.Substring(0, 16) + "…";

            GUIStyle rowStyle = RefUIStyles.GetRowStyle(_teamName);
            GUILayout.BeginHorizontal(rowStyle);
            GUILayout.Label(displayName, RefUIStyles.PlayerLabelStyle, GUILayout.Width(180), GUILayout.Height(28));

            foreach (string penalty in PlayerPenalties) {
                if (GUILayout.Button(penalty, GUILayout.Height(28))) {
                    string penaltyLower = penalty.ToLower();
                    bool isDoubleId = Array.Exists(DoubleIdPenalties, p => p == penalty);

                    string command = isDoubleId
                        ? $"/pen {penaltyLower} {steamId} {steamId}"
                        : $"/pen {penaltyLower} {steamId}";

                    ChatService.Send(command);
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}
