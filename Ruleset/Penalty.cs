using Codebase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal static class PenaltyModule {
        #region Constants
        internal const string GIVE_PENALTY_DATANAME = Constants.MOD_NAME + "pen";
        internal const string REMOVE_ALL_PENALTIES_DATANAME = Constants.MOD_NAME + "removeallpen";
        internal const string REMOVE_PENALTY_DATANAME = Constants.MOD_NAME + "removepen";

        private static readonly Vector3 BLUE_PENALTY_BOX_POSITION = new Vector3(26f, 0.9f, 1.5f); // TODO : Config.

        private static readonly Vector3 RED_PENALTY_BOX_POSITION = new Vector3(BLUE_PENALTY_BOX_POSITION.x, BLUE_PENALTY_BOX_POSITION.y, BLUE_PENALTY_BOX_POSITION.z * -1); // TODO : Config.

        private static readonly Vector3 INFRONT_BLUE_PENALTY_BOX_POSITION = new Vector3(22f, 0.05f, 1.5f); // TODO : Config.

        private static readonly Vector3 INFRONT_RED_PENALTY_BOX_POSITION = new Vector3(INFRONT_BLUE_PENALTY_BOX_POSITION.x, INFRONT_BLUE_PENALTY_BOX_POSITION.y, INFRONT_BLUE_PENALTY_BOX_POSITION.z * -1); // TODO : Config.

        private static readonly Quaternion PENALTY_ROTATION = Quaternion.Euler(0f, 270f, 0f); // TODO : Config.

        private static readonly Vector3 DELAY_OF_GAME_POSITION = new Vector3(22.47f, 0f, 45.72f); // TODO : Config.

        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1 = new Vector3(19.45f, 0f, 43.8f); // TODO : Config.
        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2 = new Vector3(23.20f, 0f, 33.7f); // TODO : Config.
        
        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_1_POSITION_1 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.x, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.z * -1);
        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_1_POSITION_2 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.x, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.z * -1);

        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_1_POSITION_1 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.z);
        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_1_POSITION_2 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.z);

        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_1_POSITION_1 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1.z * -1);
        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_1_POSITION_2 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2.z * -1);

        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1 = new Vector3(15.55f, 0f, 45.3f); // TODO : Config.
        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2 = new Vector3(21.65f, 0f, 39.15f); // TODO : Config.

        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_2_POSITION_1 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.x, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.z * -1);
        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_2_POSITION_2 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.x, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.z * -1);

        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_2_POSITION_1 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.z);
        private static readonly Vector3 DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_2_POSITION_2 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.z);

        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_2_POSITION_1 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1.z * -1);
        private static readonly Vector3 DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_2_POSITION_2 = new Vector3(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.x * -1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.y, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2.z * -1);

        private static readonly Dictionary<string, bool> POSITION_IS_PENALIZED_DEFAULT = new Dictionary<string, bool> {
            { Codebase.PlayerFunc.GOALIE_POSITION, false },
            { Codebase.PlayerFunc.CENTER_POSITION, false },
            { Codebase.PlayerFunc.LEFT_WINGER_POSITION, false },
            { Codebase.PlayerFunc.RIGHT_WINGER_POSITION, false },
            { Codebase.PlayerFunc.LEFT_DEFENDER_POSITION, false },
            { Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION, false },
        };

        private static readonly Dictionary<int, bool> PENALTY_BENCH_POSITION_DEFAULT = new Dictionary<int, bool> {
            { 0, false },
            { 1, false },
            { 2, false },
        };
        #endregion

        #region Properties
        internal static LockDictionary<string, LockList<Penalty>> PenalizedPlayers { get; } = new LockDictionary<string, LockList<Penalty>>();

        internal static int PenalizedPlayersCountBlueTeam { get; set; } = 0;

        internal static int PenalizedPlayersInBoxCountBlueTeam { get; set; } = 0;

        internal static int PenalizedPlayersCountRedTeam { get; set; } = 0;

        internal static int PenalizedPlayersInBoxCountRedTeam { get; set; } = 0;

        internal static LockDictionary<PlayerTeam, bool> PenaltyToBeCalled { get; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal static LockDictionary<PlayerTeam, LockDictionary<string, bool>> PositionIsPenalized { get; } = new LockDictionary<PlayerTeam, LockDictionary<string, bool>> {
            { PlayerTeam.Blue, new LockDictionary<string, bool>(POSITION_IS_PENALIZED_DEFAULT) },
            { PlayerTeam.Red, new LockDictionary<string, bool>(POSITION_IS_PENALIZED_DEFAULT) },
        };

        internal static LockDictionary<PlayerTeam, LockDictionary<int, bool>> PenaltyBenchPositionIsOccupied { get; } = new LockDictionary<PlayerTeam, LockDictionary<int, bool>> {
            { PlayerTeam.Blue, new LockDictionary<int, bool>(PENALTY_BENCH_POSITION_DEFAULT) },
            { PlayerTeam.Red, new LockDictionary<int, bool>(PENALTY_BENCH_POSITION_DEFAULT) },
        };

        internal static PenaltyType LastPenaltyCalled { get; set; }
        #endregion

        #region Methods/Functions
        internal static bool PuckIsOutsideOfBounds(Puck puck) {
            if (Math.Abs(puck.Rigidbody.transform.position.x) > DELAY_OF_GAME_POSITION.x ||
                puck.Rigidbody.transform.position.y < DELAY_OF_GAME_POSITION.y ||
                Math.Abs(puck.Rigidbody.transform.position.z) > DELAY_OF_GAME_POSITION.z)
                return true;

            // Check for corners.
            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_1_POSITION_2) <= 0) // TOP RIGHT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_1, DELAY_OF_GAME_CORNER_TOP_RIGHT_LINE_2_POSITION_2) <= 0) // TOP RIGHT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_1_POSITION_1, DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_1_POSITION_2) >= 0) // TOP RIGHT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_2_POSITION_1, DELAY_OF_GAME_CORNER_BOTTOM_RIGHT_LINE_2_POSITION_2) >= 0) // TOP RIGHT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_1_POSITION_1, DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_1_POSITION_2) >= 0) // TOP LEFT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_2_POSITION_1, DELAY_OF_GAME_CORNER_TOP_LEFT_LINE_2_POSITION_2) >= 0) // TOP LEFT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_1_POSITION_1, DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_1_POSITION_2) <= 0) // TOP LEFT BLUE ZONE
                return true;

            if (puck.Rigidbody.transform.position.GetSideOfLine(DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_2_POSITION_1, DELAY_OF_GAME_CORNER_BOTTOM_LEFT_LINE_2_POSITION_2) <= 0) // TOP LEFT BLUE ZONE
                return true;

            return false;
        }

        internal static void ResetPenalties() {
            PenalizedPlayers.Clear();
            PenalizedPlayersCountBlueTeam = 0;
            PenalizedPlayersInBoxCountBlueTeam = 0;
            PenalizedPlayersCountRedTeam = 0;
            PenalizedPlayersInBoxCountRedTeam = 0;

            foreach (PlayerTeam key in new List<PlayerTeam>(PositionIsPenalized.Keys))
                PositionIsPenalized[key] = new LockDictionary<string, bool>(POSITION_IS_PENALIZED_DEFAULT);

            foreach (PlayerTeam key in new List<PlayerTeam>(PenaltyBenchPositionIsOccupied.Keys))
                PenaltyBenchPositionIsOccupied[key] = new LockDictionary<int, bool>(PENALTY_BENCH_POSITION_DEFAULT);

            NetworkCommunication.SendDataToAll("removeallpen", "1", Constants.FROM_SERVER_TO_CLIENT, Ruleset.ServerConfig);
        }

        internal static bool GivePenalty(PenaltyType penaltyType, Player penalizedPlayer, string receivingPlayerSteamId = "", Player referee = null) {
            if (!Ruleset.ServerConfig.Penalty.Interference && (penaltyType == PenaltyType.Interference || penaltyType == PenaltyType.Tripping))
                return false;
            if (!Ruleset.ServerConfig.Penalty.GoalieInterference && penaltyType == PenaltyType.GoalieInterference)
                return false;
            if (!Ruleset.ServerConfig.Penalty.DelayOfGame && penaltyType == PenaltyType.DelayOfGame)
                return false;
            if (!Ruleset.ServerConfig.Penalty.FaceoffViolation && penaltyType == PenaltyType.FaceoffViolation)
                return false;
            if (!Ruleset.ServerConfig.Penalty.Embellishment && penaltyType == PenaltyType.Embellishment)
                return false;

            List<Player> teamPlayers = PlayerManager.Instance.GetPlayersByTeam(penalizedPlayer.Team.Value).Where(x => !Codebase.PlayerFunc.IsGoalie(x)).ToList();

            if (teamPlayers.Count < 2)
                return false;

            string penalizedPlayerSteamId = penalizedPlayer.SteamId.Value.ToString();
            if (!PenalizedPlayers.TryGetValue(penalizedPlayerSteamId, out LockList<Penalty> penaltyList)) {
                penaltyList = new LockList<Penalty>();
                PenalizedPlayers.Add(penalizedPlayerSteamId, penaltyList);
            }

            if (penaltyList.Count == Ruleset.ServerConfig.Penalty.MaxPenaltiesCountPerPlayer)
                return false;

            DateTime now = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(receivingPlayerSteamId) && PenalizedPlayers.SelectMany(x => x.Value).Where(x => x.Team == penalizedPlayer.Team.Value && x.PenaltyType == penaltyType && x.ReceivingPlayerSteamId == receivingPlayerSteamId).Any(x => (x.PenaltyDateTime - now).TotalMilliseconds < 4000))
                return false;

            if ((penalizedPlayer.Team.Value == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == Ruleset.ServerConfig.Penalty.MaxPenalizedPlayersPerTeam) ||
                (penalizedPlayer.Team.Value == PlayerTeam.Red && PenalizedPlayersCountRedTeam == Ruleset.ServerConfig.Penalty.MaxPenalizedPlayersPerTeam) ||
                (teamPlayers.Count(x => !PenalizedPlayers.TryGetValue(x.SteamId.Value.ToString(), out LockList<Penalty> penalties) || penalties.Count == 0) < 2)) {
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
                    return false;

                KeyValuePair<string, LockList<Penalty>> _penalties = PenalizedPlayers.Where(x => x.Value.Count == 1 && x.Value.First().Team == penalizedPlayer.Team.Value).OrderBy(x => x.Value.Min(y => y.Timer.MillisecondsLeft)).FirstOrDefault();
                if (_penalties.Equals(default(KeyValuePair<string, LockList<Penalty>>)))
                    return false;

                Player _playerToUnpenalize = teamPlayers.FirstOrDefault(x => x.SteamId.Value.ToString() == _penalties.Key);
                if (_playerToUnpenalize.Equals(default(Player)))
                    return false;

                RemoveOnePenalty(_playerToUnpenalize.Team.Value); // TODO : Remove penalty after stoppage.
            }

            // If goalie has a penalty, take another player.
            if (Codebase.PlayerFunc.IsGoalie(penalizedPlayer)) {
                List<Player> possiblePlayersToPenalize = new List<Player>();
                foreach (Player teamPlayer in teamPlayers) {
                    if (!PenalizedPlayers.TryGetValue(penalizedPlayer.SteamId.Value.ToString(), out LockList<Penalty> __penaltyList))
                        continue;

                    if (__penaltyList.Count != 0)
                        continue;

                    possiblePlayersToPenalize.Add(teamPlayer);
                }

                if (possiblePlayersToPenalize.Count == 0)
                    return false;

                penalizedPlayer = possiblePlayersToPenalize.OrderBy(x => x.Goals.Value + x.Assists.Value).First();
                penalizedPlayerSteamId = penalizedPlayer.SteamId.Value.ToString();

                if (!PenalizedPlayers.TryGetValue(penalizedPlayer.SteamId.Value.ToString(), out LockList<Penalty> _penaltyList)) {
                    _penaltyList = new LockList<Penalty>();
                    PenalizedPlayers.Add(penalizedPlayerSteamId, _penaltyList);
                }
                penaltyList = _penaltyList;
            }

            PositionIsPenalized[penalizedPlayer.Team.Value][penalizedPlayer.PlayerPosition.Name] = true;
            PenaltyToBeCalled[penalizedPlayer.Team.Value] = true;
            if (penaltyList.Count == 0) {
                if (penalizedPlayer.Team.Value == PlayerTeam.Blue)
                    PenalizedPlayersCountBlueTeam++;
                else
                    PenalizedPlayersCountRedTeam++;
            }

            LastPenaltyCalled = penaltyType;

            Penalty newPenalty = new Penalty(
                penalizedPlayerSteamId,
                penalizedPlayer.Team.Value,
                penalizedPlayer.Number.Value.ToString(),
                penalizedPlayer.Username.Value.ToString(),
                penaltyType,
                penalizedPlayer.PlayerPosition.Name,
                receivingPlayerSteamId
            );

            penaltyList.Add(newPenalty);
            string message = $"Penalty #{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value}, {GetPenaltyTypeTime(penaltyType) / 1000} seconds for {penaltyType.GetDescription("ToString")}";
            if (referee != null)
                message += $", called by #{referee.Number.Value} {referee.Username.Value}";
            Ruleset.SystemChatMessages.Add(message);
            Logging.Log(message, Ruleset.ServerConfig);
            // TODO : Get actual ref signal.
            NetworkCommunication.SendDataToAll(RefSignals.GetSignalConstant(true, penalizedPlayer.Team.Value), RefSignals.HIGHSTICK_LINESMAN, Constants.FROM_SERVER_TO_CLIENT, Ruleset.ServerConfig);

            if (PenaltyToBeCalled.Values.All(x => x))
                Ruleset.CallPenalty(PlayerTeam.None);

            return true;
        }

        internal static void StartPenalties() {
            PenaltyToBeCalled[PlayerTeam.Blue] = false;
            PenaltyToBeCalled[PlayerTeam.Red] = false;

            PenalizedPlayersInBoxCountBlueTeam = PenalizedPlayersCountBlueTeam;
            PenalizedPlayersInBoxCountRedTeam = PenalizedPlayersCountRedTeam;

            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                // Player to the box and start first penalty.
                if (penalties.Count != 0 && penalties.All(x => !x.CurrentPenalty)) {
                    Penalty firstPenalty = penalties.First();
                    firstPenalty.CurrentPenalty = true;

                    Player penalizedPlayer = PlayerManager.Instance.GetPlayerBySteamId(firstPenalty.SteamId);
                    if (penalizedPlayer == null || !penalizedPlayer || !penalizedPlayer.IsCharacterFullySpawned)
                        return;

                    TeleportPlayer(penalizedPlayer);
                }
            }
        }

        internal static void TeleportPlayers() {
            foreach (PlayerTeam key in new List<PlayerTeam>(PenaltyBenchPositionIsOccupied.Keys))
                PenaltyBenchPositionIsOccupied[key] = new LockDictionary<int, bool>(PENALTY_BENCH_POSITION_DEFAULT);

            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                // Player to the box and start first penalty.
                if (penalties.Any(x => x.CurrentPenalty)) {
                    TeleportPlayer(penalties.First().SteamId);
                }
            }
        }

        internal static void TeleportPlayer(string playerSteamId) {
            Player player = PlayerManager.Instance.GetPlayerBySteamId(playerSteamId);
            if (player == null || !player || !player.IsCharacterFullySpawned)
                return;

            TeleportPlayer(player);
        }

        internal static void TeleportPlayer(Player player) {
            if (player.IsCharacterFullySpawned) {
                player.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;
                player.PlayerBody.Rigidbody.angularVelocity = Vector3.zero;
            }
            player.PlayerBody.Server_Freeze();

            Vector3 penaltyBoxPosition;
            float zOffset = 0;

            if (!PenaltyBenchPositionIsOccupied[player.Team.Value][0])
                PenaltyBenchPositionIsOccupied[player.Team.Value][0] = true;
            else if (!PenaltyBenchPositionIsOccupied[player.Team.Value][1]) {
                PenaltyBenchPositionIsOccupied[player.Team.Value][1] = true;
                zOffset = 1.5f;
            }
            else if (!PenaltyBenchPositionIsOccupied[player.Team.Value][2]) {
                PenaltyBenchPositionIsOccupied[player.Team.Value][2] = true;
                zOffset = 3f;
            }

            if (player.Team.Value == PlayerTeam.Blue)
                penaltyBoxPosition = new Vector3(BLUE_PENALTY_BOX_POSITION.x, BLUE_PENALTY_BOX_POSITION.y, BLUE_PENALTY_BOX_POSITION.z + zOffset);
            else
                penaltyBoxPosition = new Vector3(RED_PENALTY_BOX_POSITION.x, RED_PENALTY_BOX_POSITION.y, RED_PENALTY_BOX_POSITION.z - zOffset);

            player.PlayerBody.Server_Teleport(penaltyBoxPosition, PENALTY_ROTATION);
        }

        internal static void PausePenalties() {
            foreach (LockList<Penalty> penalties in PenalizedPlayers.Values) {
                foreach (Penalty penalty in penalties) {
                    if (penalty.CurrentPenalty)
                        penalty.Timer.Pause();
                }
            }

            foreach (PlayerTeam key in new List<PlayerTeam>(PenaltyBenchPositionIsOccupied.Keys))
                PenaltyBenchPositionIsOccupied[key] = new LockDictionary<int, bool>(PENALTY_BENCH_POSITION_DEFAULT);

            NetworkCommunication.SendDataToAll("penpause", "1", Constants.FROM_SERVER_TO_CLIENT, Ruleset.ServerConfig);
        }

        internal static void UnpausePenalties() {
            string penaltyUIMsg = "";
            foreach (Penalty penalty in new List<Penalty>(PenalizedPlayers.SelectMany(x => x.Value))) {
                if (penalty.Timer.TimerEnded())
                    continue;

                if (penalty.CurrentPenalty)
                    penalty.Timer.Start();

                penaltyUIMsg += $"{(penalty.Team == PlayerTeam.Blue ? "B" : "R")} {penalty.Position} #{penalty.PlayerNumber}!{penalty.Timer.MillisecondsLeft}!{(penalty.CurrentPenalty ? "1" : "0")};";
            }

            if (string.IsNullOrEmpty(penaltyUIMsg)) {
                NetworkCommunication.SendDataToAll("removeallpen", "1", Constants.FROM_SERVER_TO_CLIENT, Ruleset.ServerConfig);
                return;
            }

            penaltyUIMsg = penaltyUIMsg.Remove(penaltyUIMsg.Length - 1);
            NetworkCommunication.SendDataToAll("penunpause", penaltyUIMsg, Constants.FROM_SERVER_TO_CLIENT, Ruleset.ServerConfig);
        }

        internal static void UnpenalizePlayer(Player penalizedPlayer, PlayerTeam penalizedPlayerTeam, string penalizedPlayerPosition) {
            PositionIsPenalized[penalizedPlayerTeam][penalizedPlayerPosition] = false;

            if (penalizedPlayerTeam == PlayerTeam.Blue) {
                PenalizedPlayersCountBlueTeam--;
                PenalizedPlayersInBoxCountBlueTeam--;
            }
            else {
                PenalizedPlayersCountRedTeam--;
                PenalizedPlayersInBoxCountRedTeam--;
            }

            if (penalizedPlayer != null && penalizedPlayer && penalizedPlayer.IsCharacterFullySpawned) {
                if (penalizedPlayerTeam == PlayerTeam.Blue)
                    penalizedPlayer.PlayerBody.Server_Teleport(INFRONT_BLUE_PENALTY_BOX_POSITION, PENALTY_ROTATION);
                else
                    penalizedPlayer.PlayerBody.Server_Teleport(INFRONT_RED_PENALTY_BOX_POSITION, PENALTY_ROTATION);

                penalizedPlayer.PlayerBody.Server_Unfreeze();
                Ruleset.SystemChatMessages.Add($"#{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} UNPENALIZED");
                Logging.Log($"#{penalizedPlayer.Number.Value} {penalizedPlayer.Username.Value} UNPENALIZED", Ruleset.ServerConfig);
            }
        }

        internal static bool RemoveOnePenalty(PlayerTeam penalizedPlayerTeam) {
            if (penalizedPlayerTeam == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == 0)
                return false;

            if (penalizedPlayerTeam == PlayerTeam.Red && PenalizedPlayersCountRedTeam == 0)
                return false;

            Penalty penaltyToRemove = PenalizedPlayers.SelectMany(x => x.Value).Where(x => x.Team == penalizedPlayerTeam).OrderBy(x => x.Timer.MillisecondsLeft).FirstOrDefault();
            if (penaltyToRemove == null || penaltyToRemove.Equals(default(Penalty)))
                return false;

            penaltyToRemove.Timer.Pause();
            penaltyToRemove.Timer.TimerCallback(null);

            return true;
        }

        internal static string FakePlayerPositionForFaceoffByAvailability(string position, PlayerTeam team, List<string> claimedPositions) {
            if (team == PlayerTeam.Blue && PenalizedPlayersCountBlueTeam == 0)
                return position;

            if (team == PlayerTeam.Red && PenalizedPlayersCountRedTeam == 0)
                return position;

            switch (position) {
                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION:
                    if (!claimedPositions.Contains(Codebase.PlayerFunc.LEFT_WINGER_POSITION))
                        return Codebase.PlayerFunc.LEFT_WINGER_POSITION;
                    break;

                case Codebase.PlayerFunc.LEFT_DEFENDER_POSITION:
                    if (!claimedPositions.Contains(Codebase.PlayerFunc.LEFT_WINGER_POSITION) && !claimedPositions.Contains(Codebase.PlayerFunc.RIGHT_WINGER_POSITION))
                        return Codebase.PlayerFunc.LEFT_WINGER_POSITION;

                    if (!claimedPositions.Contains(Codebase.PlayerFunc.RIGHT_WINGER_POSITION))
                        return Codebase.PlayerFunc.RIGHT_WINGER_POSITION;
                    break;

                case Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION:
                    if (!claimedPositions.Contains(Codebase.PlayerFunc.LEFT_WINGER_POSITION) && !claimedPositions.Contains(Codebase.PlayerFunc.RIGHT_WINGER_POSITION) && !claimedPositions.Contains(Codebase.PlayerFunc.LEFT_DEFENDER_POSITION))
                        return Codebase.PlayerFunc.LEFT_WINGER_POSITION;

                    if (!claimedPositions.Contains(Codebase.PlayerFunc.RIGHT_WINGER_POSITION) && !claimedPositions.Contains(Codebase.PlayerFunc.LEFT_DEFENDER_POSITION))
                        return Codebase.PlayerFunc.RIGHT_WINGER_POSITION;

                    if (!claimedPositions.Contains(Codebase.PlayerFunc.LEFT_DEFENDER_POSITION))
                        return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                    break;
            }

            return position;
        }

        internal static string GetPlayerPositionForFaceoff(string position, PlayerTeam team, FaceoffSpot faceoffSpot, List<string> claimedPositions) {
            position = FakePlayerPositionForFaceoffByAvailability(position, team, claimedPositions);

            switch (position) {
                case Codebase.PlayerFunc.LEFT_WINGER_POSITION:
                    if (team == PlayerTeam.Blue) {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION])
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                            return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                    }
                    else {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION])
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                            return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                    }
                    break;

                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION:
                    if (team == PlayerTeam.Blue) {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_WINGER_POSITION])
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && (faceoffSpot == FaceoffSpot.BlueteamBLLeft || faceoffSpot == FaceoffSpot.RedteamBLLeft || faceoffSpot == FaceoffSpot.BlueteamDZoneLeft || faceoffSpot == FaceoffSpot.RedteamDZoneLeft || faceoffSpot == FaceoffSpot.Center)) {
                            return Codebase.PlayerFunc.LEFT_WINGER_POSITION;
                        }
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION])
                            return Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION;
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                            return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                    }
                    else {
                        if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_WINGER_POSITION])
                            return Codebase.PlayerFunc.CENTER_POSITION;
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && (faceoffSpot == FaceoffSpot.BlueteamBLRight || faceoffSpot == FaceoffSpot.RedteamBLRight || faceoffSpot == FaceoffSpot.BlueteamDZoneRight || faceoffSpot == FaceoffSpot.RedteamDZoneRight || faceoffSpot == FaceoffSpot.Center)) {
                            return Codebase.PlayerFunc.LEFT_WINGER_POSITION;
                        }
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION])
                            return Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION;
                        else if (PositionIsPenalized[team][Codebase.PlayerFunc.CENTER_POSITION] && PositionIsPenalized[team][Codebase.PlayerFunc.LEFT_DEFENDER_POSITION])
                            return Codebase.PlayerFunc.LEFT_DEFENDER_POSITION;
                    }
                    break;
            }

            return position;
        }

        internal static int GetPenaltyTypeTime(PenaltyType penaltyType) {
            switch (penaltyType) {
                case PenaltyType.Interference:
                case PenaltyType.Tripping:
                    return Ruleset.ServerConfig.Penalty.InterferenceTime;

                case PenaltyType.GoalieInterference:
                    return Ruleset.ServerConfig.Penalty.GoalieInterferenceTime;

                case PenaltyType.DelayOfGame:
                    return Ruleset.ServerConfig.Penalty.DelayOfGameTime;

                case PenaltyType.FaceoffViolation:
                    return Ruleset.ServerConfig.Penalty.FaceoffViolationTime;

                case PenaltyType.Embellishment:
                    return Ruleset.ServerConfig.Penalty.EmbellishmentTime;
            }

            return 45000;
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

        internal string PlayerNumber { get; set; }

        internal string PlayerUsername { get; set; }

        internal string ReceivingPlayerSteamId { get; set; }

        internal Penalty(string steamId, PlayerTeam team, string playerNumber, string playerUsername, PenaltyType penaltyType, string position, string receivingPlayerSteamId) {
            SteamId = steamId;
            Team = team;
            PlayerNumber = playerNumber;
            PlayerUsername = playerUsername;
            PenaltyType = penaltyType;
            Position = position;
            CurrentPenalty = false;
            ReceivingPlayerSteamId = receivingPlayerSteamId;

            SetTimer();

            PenaltyDateTime = DateTime.UtcNow;
        }

        internal void SetTimer() {
            Timer = new PausableTimer(PenaltyTimer_Elapsed, PenaltyModule.GetPenaltyTypeTime(PenaltyType));
        }

        private void PenaltyTimer_Elapsed() {
            Penalty penaltyToRemove = null;
            // Remove elapsed penalty.
            foreach (Penalty penalty in PenaltyModule.PenalizedPlayers[SteamId]) {
                if (penalty.Timer.TimerEnded()) {
                    penaltyToRemove = penalty;
                    break;
                }
            }

            PenaltyModule.PenalizedPlayers[SteamId].Remove(penaltyToRemove);

            Ruleset.SystemChatMessages.Add($"PENALTY #{PlayerNumber} {PlayerUsername} OVER");
            Logging.Log($"PENALTY #{PlayerNumber} {PlayerUsername} OVER", Ruleset.ServerConfig);

            // Unpenalize player if no more penalties or start the next one.
            if (PenaltyModule.PenalizedPlayers[SteamId].Count == 0)
                PenaltyModule.UnpenalizePlayer(PlayerManager.Instance.GetPlayerBySteamId(SteamId), Team, Position);
            else {
                Penalty firstPenalty = PenaltyModule.PenalizedPlayers[SteamId].First();
                firstPenalty.CurrentPenalty = true;
            }

            PenaltyModule.UnpausePenalties();
            if (GameManager.Instance.Phase != GamePhase.Playing)
                PenaltyModule.PausePenalties();
        }
    }

    public enum PenaltyType {
        Interference,
        [Description("Goalie interference"), Category("ToString")]
        GoalieInterference,
        [Description("Delay of game"), Category("ToString")]
        DelayOfGame,
        Tripping,
        Embellishment,
        [Description("Faceoff violation"), Category("ToString")]
        FaceoffViolation,
    }
}
