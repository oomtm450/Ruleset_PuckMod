using Codebase;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class PenaltyModule {
        #region Constants
        private const int MAX_SAME_PLAYER_PENALTY_COUNT = 2;
        private const int MAX_PENALIZED_PLAYERS = 2;

        private static readonly Vector3 BLUE_PENALTY_BOX_POSITION = new Vector3(26f, 0.9f, 3f);

        private static readonly Vector3 RED_PENALTY_BOX_POSITION = new Vector3(BLUE_PENALTY_BOX_POSITION.x, BLUE_PENALTY_BOX_POSITION.y, BLUE_PENALTY_BOX_POSITION.z * -1);

        private static readonly Vector3 INFRONT_BLUE_PENALTY_BOX_POSITION = new Vector3(22f, 0.05f, 3f);

        private static readonly Vector3 INFRONT_RED_PENALTY_BOX_POSITION = new Vector3(22f, 0.05f, -3f);

        private static readonly Quaternion PENALTY_ROTATION = Quaternion.Euler(0f, 270f, 0f);

        internal static readonly Vector3 DELAY_OF_GAME_POSITION = new Vector3(24f, 0f, (float)ZoneFunc.ICE_Z_POSITIONS[IceElement.BlueTeam_BlueLine].End + 16f);

        internal static readonly float DELAY_OF_GAME_POSITION_END_Z = 46.5f;

        private static readonly Dictionary<string, bool> POSITION_IS_PENALIZED_DEFAULT = new Dictionary<string, bool> {
            { Codebase.PlayerFunc.CENTER_POSITION, false },
            { Codebase.PlayerFunc.LEFT_WINGER_POSITION, false },
            { Codebase.PlayerFunc.RIGHT_WINGER_POSITION, false },
            { Codebase.PlayerFunc.LEFT_DEFENDER_POSITION, false },
            { Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION, false },
        };
        #endregion

        #region Properties
        internal static LockDictionary<string, LockList<Penalty>> PenalizedPlayers { get; } = new LockDictionary<string, LockList<Penalty>>();

        internal static int PenalizedPlayersCountBlueTeam { get; set; } = 0;

        internal static int PenalizedPlayersCountRedTeam { get; set; } = 0;

        internal static LockDictionary<PlayerTeam, bool> PenaltyToBeCalled { get; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal static LockDictionary<PlayerTeam, LockDictionary<string, bool>> PositionIsPenalized { get; } = new LockDictionary<PlayerTeam, LockDictionary<string, bool>> {
            { PlayerTeam.Blue, new LockDictionary<string, bool>(POSITION_IS_PENALIZED_DEFAULT) },
            { PlayerTeam.Red, new LockDictionary<string, bool>(POSITION_IS_PENALIZED_DEFAULT) },
        };
        #endregion

        #region Methods/Functions
        internal static void ResetPenalties() {
            PenalizedPlayers.Clear();
            PenalizedPlayersCountBlueTeam = 0;
            PenalizedPlayersCountRedTeam = 0;

            foreach (PlayerTeam key in new List<PlayerTeam>(PositionIsPenalized.Keys))
                PositionIsPenalized[key] = new LockDictionary<string, bool>(POSITION_IS_PENALIZED_DEFAULT);
        }

        internal static void GivePenalty(PenaltyType penaltyType, Player penalizedPlayer, string receivingPlayerSteamId = "") {
            if (!Ruleset.ServerConfig.Penalty.Interference && (penaltyType == PenaltyType.Interference || penaltyType == PenaltyType.Tripping))
                return;
            if (!Ruleset.ServerConfig.Penalty.GoalieInterference && penaltyType == PenaltyType.GoalieInterference)
                return;
            if (!Ruleset.ServerConfig.Penalty.DelayOfGame && penaltyType == PenaltyType.DelayOfGame)
                return;

            if (penalizedPlayer.Team.Value == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == MAX_PENALIZED_PLAYERS)
                return;

            if (penalizedPlayer.Team.Value == PlayerTeam.Red && PenalizedPlayersCountRedTeam == MAX_PENALIZED_PLAYERS)
                return;

            List<Player> teamPlayers = PlayerManager.Instance.GetPlayersByTeam(penalizedPlayer.Team.Value).Where(x => !Codebase.PlayerFunc.IsGoalie(x)).ToList();

            if (teamPlayers.Count < 2)
                return;

            string penalizedPlayerSteamId = penalizedPlayer.SteamId.Value.ToString();
            if (!PenalizedPlayers.TryGetValue(penalizedPlayerSteamId, out LockList<Penalty> penaltyList)) {
                penaltyList = new LockList<Penalty>();
                PenalizedPlayers.Add(penalizedPlayerSteamId, penaltyList);
            }

            if (penaltyList.Count == MAX_SAME_PLAYER_PENALTY_COUNT)
                return;

            DateTime now = DateTime.UtcNow;
            if (PenalizedPlayers.SelectMany(x => x.Value).Where(x => x.Team == penalizedPlayer.Team.Value && x.PenaltyType == penaltyType && x.ReceivingPlayerSteamId == receivingPlayerSteamId).Any(x => (x.PenaltyDateTime - now).TotalMilliseconds < 4000))
                return;

            if (teamPlayers.Count(x => !PenalizedPlayers.TryGetValue(x.SteamId.Value.ToString(), out LockList<Penalty> penalties) || penalties.Count == 0) < 2) {
                bool unpenalizeOnePlayer = false;
                if (penalizedPlayer.Team.Value == PlayerTeam.Blue) {
                    if (PenalizedPlayersCountBlueTeam != 0)
                        unpenalizeOnePlayer = true;
                }
                else if (penalizedPlayer.Team.Value == PlayerTeam.Red) {
                    if (PenalizedPlayersCountRedTeam != 0)
                        unpenalizeOnePlayer = true;
                }

                if (!unpenalizeOnePlayer)
                    return;

                KeyValuePair<string, LockList<Penalty>> _penalties = PenalizedPlayers.FirstOrDefault(x => x.Value.Count == 1 && x.Value.First().Team == penalizedPlayer.Team.Value);
                if (_penalties.Equals(default(KeyValuePair<string, LockList<Penalty>>)))
                    return;

                Player _playerToUnpenalize = teamPlayers.FirstOrDefault(x => x.SteamId.Value.ToString() == _penalties.Key);
                if (_playerToUnpenalize.Equals(default(Player)))
                    return;

                PositionIsPenalized[_playerToUnpenalize.Team.Value][_playerToUnpenalize.PlayerPosition.Name] = false;
                UnpenalizePlayer(_playerToUnpenalize);
            }

            // If goalie has a penalty, take another player.
            if (Codebase.PlayerFunc.IsGoalie(penalizedPlayer))
                penalizedPlayer = teamPlayers.First();

            PositionIsPenalized[penalizedPlayer.Team.Value][penalizedPlayer.PlayerPosition.Name] = true;
            PenaltyToBeCalled[penalizedPlayer.Team.Value] = true;
            if (penaltyList.Count == 0) {
                if (penalizedPlayer.Team.Value == PlayerTeam.Blue)
                    PenalizedPlayersCountBlueTeam++;
                else
                    PenalizedPlayersCountRedTeam++;
            }

            Penalty newPenalty = new Penalty(penalizedPlayerSteamId, penalizedPlayer.Team.Value, penaltyType, penalizedPlayer.PlayerPosition.Name, receivingPlayerSteamId);
            penaltyList.Add(newPenalty);
            Ruleset.SystemChatMessages.Add($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value}, {penaltyType}");
            Logging.Log($"PENALTY #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value}, {penaltyType}", Ruleset.ServerConfig);

            if (PenaltyToBeCalled.Values.All(x => x))
                Ruleset.CallPenalty(PlayerTeam.None);
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
            if (player.IsCharacterFullySpawned) {
                player.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;
                player.PlayerBody.Rigidbody.angularVelocity = Vector3.zero;
            }
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

            PenaltyModule.PositionIsPenalized[penalizedPlayer.Team.Value][penalizedPlayer.PlayerPosition.Name] = false;

            penalizedPlayer.PlayerBody.Server_Unfreeze();

            Ruleset.SystemChatMessages.Add($"#{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} UNPENALIZED");
            Logging.Log($"#{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} UNPENALIZED", Ruleset.ServerConfig);
        }

        internal static void RemoveOnePenalty(PlayerTeam penalizedPlayerTeam) {
            if (penalizedPlayerTeam == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == 0)
                return;

            if (penalizedPlayerTeam == PlayerTeam.Red && PenalizedPlayersCountRedTeam == 0)
                return;

            Penalty penaltyToRemove = PenalizedPlayers.SelectMany(x => x.Value).Where(x => x.Team == penalizedPlayerTeam).OrderBy(x => x.Timer.MillisecondsLeft).FirstOrDefault();
            if (penaltyToRemove == null || penaltyToRemove.Equals(default(Penalty)))
                return;

            penaltyToRemove.Timer.Pause();
            penaltyToRemove.Timer.TimerCallback(null);
        }

        // TODO : Create function that will fake a player's position to a more priority one (Left wing then right wing, if those positions are not filled)
        /*internal static string FakePlayerPositionForFaceoffByAvailability(string position, PlayerTeam team, FaceoffSpot faceoffSpot) {
            if (team == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == 0)
                return position;

            if (team == PlayerTeam.Red && PenalizedPlayersCountRedTeam == 0)
                return position;

            switch (position) {
                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION:
                    if (left_wing_no_exists)
                        return Codebase.PlayerFunc.LEFT_WINGER_POSITION;
                    break;

                case Codebase.PlayerFunc.LEFT_DEFENDER_POSITION:
                    if (left_wing_no_exists && right_wing_no_exists)
                        return Codebase.PlayerFunc.LEFT_WINGER_POSITION;

                    if (right_wing_no_exists)
                        return Codebase.PlayerFunc.RIGHT_WINGER_POSITION;
                    break;

                case Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION:
                    if (left_wing_no_exists && right_wing_no_exists && left_defense_no_exists)
                        return Codebase.PlayerFunc.LEFT_WINGER_POSITION;

                    if (right_wing_no_exists && left_defense_no_exists)
                        return Codebase.PlayerFunc.RIGHT_WINGER_POSITION;

                    if (left_defense_no_exists)
                        return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                    break;
            }

            return position;
        }*/

        internal static string GetPlayerPositionForFaceoff(string position, PlayerTeam team, FaceoffSpot faceoffSpot) {
            switch (position) {
                case Codebase.PlayerFunc.LEFT_WINGER_POSITION:
                    if (team == PlayerTeam.Blue) {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && (faceoffSpot == FaceoffSpot.BlueteamBLLeft || faceoffSpot == FaceoffSpot.RedteamBLLeft || faceoffSpot == FaceoffSpot.BlueteamDZoneLeft || faceoffSpot == FaceoffSpot.RedteamDZoneLeft))
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else {
                            if (PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                                return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                        }
                    }
                    else {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && (faceoffSpot == FaceoffSpot.BlueteamBLRight || faceoffSpot == FaceoffSpot.RedteamBLRight || faceoffSpot == FaceoffSpot.BlueteamDZoneRight || faceoffSpot == FaceoffSpot.RedteamDZoneRight))
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else {
                            if (PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                                return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                        }
                    }
                    break;

                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION:
                    if (team == PlayerTeam.Blue) {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_WINGER_POSITION] && (faceoffSpot == FaceoffSpot.BlueteamBLLeft || faceoffSpot == FaceoffSpot.RedteamBLLeft || faceoffSpot == FaceoffSpot.BlueteamDZoneLeft || faceoffSpot == FaceoffSpot.RedteamDZoneLeft))
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else {
                            if (PositionIsPenalized[team][Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION])
                                return Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION;
                            else if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                                return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                        }
                    }
                    else {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_WINGER_POSITION] && (faceoffSpot == FaceoffSpot.BlueteamBLRight || faceoffSpot == FaceoffSpot.RedteamBLRight || faceoffSpot == FaceoffSpot.BlueteamDZoneRight || faceoffSpot == FaceoffSpot.RedteamDZoneRight))
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else {
                            if (PositionIsPenalized[team][Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION])
                                return Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION;
                            else if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                                return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                        }
                    }
                    break;
            }

            return position;
        }
        #endregion
    }

    internal class Penalty {
        internal string SteamId { get; }

        internal PenaltyType PenaltyType { get; }

        internal PausableTimer Timer { get; set; }

        internal bool CurrentPenalty { get; set; }

        internal DateTime PenaltyDateTime { get; set; }

        internal PlayerTeam Team { get; set; }

        internal string Position { get; set; }

        internal string ReceivingPlayerSteamId { get; set; }

        internal Penalty(string steamId, PlayerTeam team, PenaltyType penaltyType, string position, string receivingPlayerSteamId) {
            SteamId = steamId;
            Team = team;
            PenaltyType = penaltyType;
            Position = position;
            CurrentPenalty = false;
            ReceivingPlayerSteamId = receivingPlayerSteamId;

            SetTimer();

            PenaltyDateTime = DateTime.UtcNow;
        }

        internal void SetTimer() {
            switch (PenaltyType) {
                case PenaltyType.Interference:
                case PenaltyType.Tripping:
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
                PenaltyModule.PositionIsPenalized[Team][Position] = false;
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
        Tripping,
    }
}
