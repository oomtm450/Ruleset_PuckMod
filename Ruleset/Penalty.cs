using Codebase;
using System.Linq;

namespace oomtm450PuckMod_Ruleset {
    internal static class PenaltyModule {
        private const int MAX_SAME_PLAYER_PENALTY_COUNT = 2;
        private const int MAX_PENALIZED_PLAYERS = 2;

        internal static LockDictionary<string, LockList<Penalty>> PenalizedPlayers { get; } = new LockDictionary<string, LockList<Penalty>>();

        internal static LockDictionary<PlayerTeam, bool> PenaltyToBeCalled { get; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal static void ResetPenalties() {
            PenalizedPlayers.Clear();
        }

        internal static void GivePenalty(PenaltyType penaltyType, Player penalizedPlayer) {
            string penalizedPlayerSteamId = penalizedPlayer.SteamId.Value.ToString();
            if (!PenalizedPlayers.TryGetValue(penalizedPlayerSteamId, out LockList<Penalty> penaltyList)) {
                penaltyList = new LockList<Penalty>();
                PenalizedPlayers.Add(penalizedPlayerSteamId, penaltyList);
            }

            if (penaltyList.Count == MAX_SAME_PLAYER_PENALTY_COUNT)
                return;

            PenaltyToBeCalled[penalizedPlayer.Team.Value] = true;

            Penalty newPenalty = new Penalty(penalizedPlayerSteamId, penaltyType);
            penaltyList.Add(newPenalty);
            Ruleset.SystemChatMessages.Add($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value}, {penaltyType}");
        }

        internal static void StartPenalties() {
            PenaltyToBeCalled[PlayerTeam.Blue] = false;
            PenaltyToBeCalled[PlayerTeam.Red] = false;

            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                // Player to the box and start first penalty.
                if (penalties.All(x => !x.CurrentPenalty)) {
                    Penalty firstPenalty = penalties.First();
                    firstPenalty.CurrentPenalty = true;

                    // TODO : Teleport player and freeze.
                }
            }
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

        internal static void UnpenalizePlayer(string penalizedPlayerSteamId) {

        }
    }

    internal class Penalty {
        internal string SteamId { get; }

        internal PenaltyType PenaltyType { get; }

        internal PausableTimer Timer { get; set; }

        internal bool CurrentPenalty { get; set; }

        internal Penalty(string steamId, PenaltyType penaltyType) {
            SteamId = steamId;
            PenaltyType = penaltyType;
            CurrentPenalty = false;
            SetTimer();
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

            Penalty penaltyToStart = null;
            
            foreach (Penalty penalty in PenaltyModule.PenalizedPlayers[SteamId]) {
                if (penalty.Timer.TimerEnded())
                    penaltyToRemove = penalty;
            }

            if (penaltyToStart == null)
                PenaltyModule.UnpenalizePlayer(SteamId);
            else {
                penaltyToStart.
            }
        }
    }

    public enum PenaltyType {
        Interference,
        GoalieInterference,
        DelayOfGame,
    }
}
