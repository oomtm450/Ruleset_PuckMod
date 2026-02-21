using Codebase;
using System;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class PenaltyModule {
        private const int MAX_SAME_PLAYER_PENALTY_COUNT = 2;
        private const int MAX_PENALIZED_PLAYERS = 2;

        private static readonly Vector3 BLUE_PENALTY_BOX_POSITION = new Vector3(26f, 1.5f, 3f);

        private static readonly Vector3 RED_PENALTY_BOX_POSITION = new Vector3(26f, 1.5f, -3f);

        private static readonly Vector3 INFRONT_BLUE_PENALTY_BOX_POSITION = new Vector3(22f, 0.05f, 3f);

        private static readonly Vector3 INFRONT_RED_PENALTY_BOX_POSITION = new Vector3(22f, 0.05f, -3f);

        private static readonly Quaternion PENALTY_ROTATION = Quaternion.Euler(0f, 270f, 0f);

        internal static readonly Vector3 DELAY_OF_GAME_POSITION = new Vector3(25f, 0f, 44f);

        internal static LockDictionary<string, LockList<Penalty>> PenalizedPlayers { get; } = new LockDictionary<string, LockList<Penalty>>();

        internal static int PenalizedPlayersCountBlueTeam { get; set; } = 0;

        internal static int PenalizedPlayersCountRedTeam { get; set; } = 0;

        internal static LockDictionary<PlayerTeam, bool> PenaltyToBeCalled { get; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal static void ResetPenalties() {
            PenalizedPlayers.Clear();
        }

        internal static void GivePenalty(PenaltyType penaltyType, Player penalizedPlayer) {
            if (!Ruleset.ServerConfig.Penalty.Interference && penaltyType == PenaltyType.Interference)
                return;
            if (!Ruleset.ServerConfig.Penalty.GoalieInterference && penaltyType == PenaltyType.GoalieInterference)
                return;
            if (!Ruleset.ServerConfig.Penalty.DelayOfGame && penaltyType == PenaltyType.DelayOfGame)
                return;

            if (penalizedPlayer.Team.Value == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == MAX_PENALIZED_PLAYERS)
                return;

            if (penalizedPlayer.Team.Value == PlayerTeam.Red && PenalizedPlayersCountRedTeam == MAX_PENALIZED_PLAYERS)
                return;

            string penalizedPlayerSteamId = penalizedPlayer.SteamId.Value.ToString();
            if (!PenalizedPlayers.TryGetValue(penalizedPlayerSteamId, out LockList<Penalty> penaltyList)) {
                penaltyList = new LockList<Penalty>();
                PenalizedPlayers.Add(penalizedPlayerSteamId, penaltyList);
            }

            if (penaltyList.Count == MAX_SAME_PLAYER_PENALTY_COUNT)
                return;

            if (penaltyList.Any(x => (x.PenaltyDateTime - DateTime.UtcNow).TotalMilliseconds < 4000))
                return;

            PenaltyToBeCalled[penalizedPlayer.Team.Value] = true;
            if (penaltyList.Count == 0) {
                if (penalizedPlayer.Team.Value == PlayerTeam.Blue)
                    PenalizedPlayersCountBlueTeam++;
                else
                    PenalizedPlayersCountRedTeam++;
            }

            Penalty newPenalty = new Penalty(penalizedPlayerSteamId, penaltyType);
            penaltyList.Add(newPenalty);
            Ruleset.SystemChatMessages.Add($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value}, {penaltyType}");
            Logging.Log($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value}, {penaltyType}", Ruleset.ServerConfig);
        }

        internal static void StartPenalties() {
            PenaltyToBeCalled[PlayerTeam.Blue] = false;
            PenaltyToBeCalled[PlayerTeam.Red] = false;

            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                // Player to the box and start first penalty.
                if (penalties.Count != 0 && penalties.All(x => !x.CurrentPenalty)) {
                    Penalty firstPenalty = penalties.First();
                    firstPenalty.CurrentPenalty = true;

                    Player penalizedPlayer = PlayerManager.Instance.GetPlayerBySteamId(firstPenalty.SteamId);
                    if (penalizedPlayer == null || !penalizedPlayer)
                        return;

                    TeleportPlayer(penalizedPlayer);
                }
            }
        }

        internal static void TeleportPlayers() {
            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                // Player to the box and start first penalty.
                if (penalties.Any(x => x.CurrentPenalty)) {
                    TeleportPlayer(penalties.First().SteamId);
                }
            }
        }

        internal static void TeleportPlayer(string playerSteamId) {
            Player player = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
            if (player == null || !player)
                return;

            TeleportPlayer(player);
        }

        internal static void TeleportPlayer(Player player) {
            player.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;
            player.PlayerBody.Rigidbody.angularVelocity = Vector3.zero;
            player.PlayerBody.Server_Freeze();

            if (player.Team.Value == PlayerTeam.Blue)
                player.PlayerBody.Server_Teleport(BLUE_PENALTY_BOX_POSITION, PENALTY_ROTATION);
            else
                player.PlayerBody.Server_Teleport(RED_PENALTY_BOX_POSITION, PENALTY_ROTATION);
        }

        internal static void PausePenalties() {
            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                foreach (Penalty penalty in penalties) {
                    if (penalty.CurrentPenalty)
                        penalty.Timer.Pause();
                }
            }
        }

        internal static void UnpausePenalties() {
            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                foreach (Penalty penalty in penalties) {
                    if (penalty.CurrentPenalty)
                        penalty.Timer.Start();
                }
            }
        }

        internal static void UnpenalizePlayer(Player penalizedPlayer) {
            if (penalizedPlayer.Team.Value == PlayerTeam.Blue) {
                PenalizedPlayersCountBlueTeam--;
                penalizedPlayer.PlayerBody.Server_Teleport(INFRONT_BLUE_PENALTY_BOX_POSITION, PENALTY_ROTATION);
            }
            else {
                PenalizedPlayersCountRedTeam--;
                penalizedPlayer.PlayerBody.Server_Teleport(INFRONT_RED_PENALTY_BOX_POSITION, PENALTY_ROTATION);
            }

            penalizedPlayer.PlayerBody.Server_Unfreeze();

            Ruleset.SystemChatMessages.Add($"#{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} UNPENALIZED");
            Logging.Log($"#{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} UNPENALIZED", Ruleset.ServerConfig);
        }
    }

    internal class Penalty {
        internal string SteamId { get; }

        internal PenaltyType PenaltyType { get; }

        internal PausableTimer Timer { get; set; }

        internal bool CurrentPenalty { get; set; }

        internal DateTime PenaltyDateTime { get; set; }

        internal Penalty(string steamId, PenaltyType penaltyType) {
            SteamId = steamId;
            PenaltyType = penaltyType;
            CurrentPenalty = false;
            SetTimer();
            PenaltyDateTime = DateTime.UtcNow;
        }

        internal void SetTimer() {
            switch (PenaltyType) {
                case PenaltyType.Interference:
                    Timer = new PausableTimer(PenaltyTimer_Elapsed, Ruleset.ServerConfig.Penalty.InterferenceTime);
                    break;

                case PenaltyType.GoalieInterference:
                    Timer = new PausableTimer(PenaltyTimer_Elapsed, Ruleset.ServerConfig.Penalty.GoalieInterferenceTime);
                    break;

                case PenaltyType.DelayOfGame:
                    Timer = new PausableTimer(PenaltyTimer_Elapsed, Ruleset.ServerConfig.Penalty.DelayOfGameTime);
                    break;
            }
        }

        private void PenaltyTimer_Elapsed() {
            Penalty penaltyToRemove = null;
            // Remove elapsed penalty.
            foreach (Penalty penalty in PenaltyModule.PenalizedPlayers[SteamId]) {
                if (penalty.Timer.TimerEnded())
                    penaltyToRemove = penalty;
            }

            PenaltyModule.PenalizedPlayers[SteamId].Remove(penaltyToRemove);

            Player penalizedPlayer = PlayerManager.Instance.GetPlayerBySteamId(SteamId);
            if (penalizedPlayer == null || !penalizedPlayer) {
                Penalty firstPenalty = PenaltyModule.PenalizedPlayers[SteamId].First();
                firstPenalty.CurrentPenalty = true;
                firstPenalty.Timer.Start();
                return;
            }

            Ruleset.SystemChatMessages.Add($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} OVER");
            Logging.Log($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} OVER", Ruleset.ServerConfig);

            // Unpenalize player if no more penalties or start the next one.
            if (PenaltyModule.PenalizedPlayers[SteamId].Count == 0)
                PenaltyModule.UnpenalizePlayer(penalizedPlayer);
            else {
                Penalty firstPenalty = PenaltyModule.PenalizedPlayers[SteamId].First();
                firstPenalty.CurrentPenalty = true;
                firstPenalty.Timer.Start();
            }
        }
    }

    public enum PenaltyType {
        Interference,
        GoalieInterference,
        DelayOfGame,
    }
}
