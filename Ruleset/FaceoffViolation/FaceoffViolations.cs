using Codebase;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset.FaceoffViolation {
    /// <summary>
    /// Tracks player positions and enforces role-based tethers during faceoffs
    /// </summary>
    internal class FaceOffPlayerUnfreezer : MonoBehaviour {
        private class PlayerTether {
            internal PlayerBody PlayerBody { get; set; }
            internal Vector3 SpawnPosition { get; set; }
            internal float MaxForwardDistance { get; set; }
            internal float MaxBackwardDistance { get; set; }
            internal float MaxLeftDistance { get; set; }
            internal float MaxRightDistance { get; set; }
        }

        private readonly LockList<PlayerTether> _playerTethers = new LockList<PlayerTether>();
        private bool _isFaceOffActive = false;
        private float _freezeStartTime = float.MinValue;

        internal static HashSet<Player> PenalizedPlayers { get; } = new HashSet<Player>();

        private void Awake() {
            EventManager.AddEventListener(nameof(Event_Everyone_OnGameStateChanged), Event_Everyone_OnGameStateChanged);
        }

        private void OnDestroy() {
            EventManager.RemoveEventListener(nameof(Event_Everyone_OnGameStateChanged), Event_Everyone_OnGameStateChanged);
        }

        private void Event_Everyone_OnGameStateChanged(Dictionary<string, object> message) {
            GameState oldGameState = (GameState)message["oldGameState"];
            GameState newGameState = (GameState)message["newGameState"];

            if (oldGameState.Phase == newGameState.Phase)
                return;

            _isFaceOffActive = newGameState.Phase == GamePhase.FaceOff;

            if (_isFaceOffActive) {
                // Start countdown to freeze players before puck drop
                _freezeStartTime = Time.time;

                if (oldGameState.Phase == GamePhase.Play || oldGameState.Phase == GamePhase.Intermission) { // Fix for tether since B312 doesn't respawn players in these cases.
                    foreach (Player player in PlayerManager.Instance.GetSpawnedPlayers()) {
                        Dictionary<string, object> onPlayerBodySpawnedMessage = new Dictionary<string, object> {
                            { "playerBody", player.PlayerBody },
                        };
                        Ruleset.Event_Everyone_OnPlayerBodySpawned(onPlayerBodySpawnedMessage);
                    }
                }
            }
            else {
                _playerTethers.Clear();
                _freezeStartTime = float.MinValue;
            }
        }

        internal void RegisterPlayer(PlayerBody playerBody, FaceoffSpot currentFaceoffSpot, float arenaScaleX = 1f, float arenaScaleZ = 1f) {
            if (playerBody == null || !playerBody.Player)
                return;

            if (PenaltyModule.PenalizedPlayers.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out LockList<Penalty> penalties) && penalties.Count != 0)
                return;

            // Delay registration to allow Ruleset mod to position players first
            StartCoroutine(RegisterPlayerDelayed(playerBody, currentFaceoffSpot, arenaScaleX, arenaScaleZ));
        }

        private System.Collections.IEnumerator RegisterPlayerDelayed(PlayerBody playerBody, FaceoffSpot currentFaceoffSpot, float arenaScaleX = 1f, float arenaScaleZ = 1f) {
            // Wait for Ruleset mod to finish positioning players
            yield return new WaitForSeconds(0.1f);

            if (playerBody == null || playerBody.Player == null)
                yield break;

            // Remove if already registered
            _playerTethers.RemoveAll(t => t.PlayerBody == playerBody);

            // Get player role and position AFTER ruleset has positioned them
            PlayerTeam team = playerBody.Player.Team;
            List<(string Position, bool IsPenalized)> claimedPositions = Ruleset.GetClaimedPositions(team);
            string positionName = PenaltyModule.GetPlayerPositionForFaceoff(playerBody.Player.PlayerPosition.Name, team, currentFaceoffSpot, claimedPositions,
                Ruleset.GetFakedClaimedPositions(team, claimedPositions));

            if (positionName == Codebase.PlayerFunc.LEFT_DEFENDER_POSITION && ((team == PlayerTeam.Blue && currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneLeft) || (team == PlayerTeam.Red && currentFaceoffSpot == FaceoffSpot.RedTeamDZoneRight)))
                positionName = Codebase.PlayerFunc.RIGHT_WINGER_POSITION;
            else if (positionName == Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION && ((team == PlayerTeam.Blue && currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneRight) || (team == PlayerTeam.Red && currentFaceoffSpot == FaceoffSpot.RedTeamDZoneLeft)))
                positionName = Codebase.PlayerFunc.LEFT_WINGER_POSITION;

            // Create tether with role-specific restrictions
            PlayerTether tether = new PlayerTether {
                PlayerBody = playerBody,
                SpawnPosition = playerBody.transform.position,
                MaxForwardDistance = GetMaxForwardDistance(positionName, playerBody.Player.Team, currentFaceoffSpot, arenaScaleZ),
                MaxBackwardDistance = GetMaxBackwardDistance(positionName, playerBody.Player.Team, currentFaceoffSpot, arenaScaleZ),
                MaxLeftDistance = GetMaxLeftDistance(positionName, playerBody.Player.Team, currentFaceoffSpot, arenaScaleX),
                MaxRightDistance = GetMaxRightDistance(positionName, playerBody.Player.Team, currentFaceoffSpot, arenaScaleX),
            };

            if (positionName != Codebase.PlayerFunc.GOALIE_POSITION && positionName != Codebase.PlayerFunc.CENTER_POSITION && currentFaceoffSpot == FaceoffSpot.Center) {
                tether.MaxForwardDistance *= 2f;
                tether.MaxBackwardDistance *= 2f;
                tether.MaxLeftDistance *= 2f;
                tether.MaxRightDistance *= 2f;
            }

            _playerTethers.Add(tether);
        }

        private float GetMaxForwardDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot, float arenaScaleZ = 1f) {
            if (arenaScaleZ > 1f)
                arenaScaleZ = 1f;

            switch (positionName) {
                case Codebase.PlayerFunc.CENTER_POSITION: // Center
                    return Ruleset.ServerConfig.Faceoff.CenterMaxForward * arenaScaleZ;
                case Codebase.PlayerFunc.LEFT_WINGER_POSITION: // Left Wing
                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION: // Right Wing
                    return Ruleset.ServerConfig.Faceoff.WingerMaxForward * arenaScaleZ;
                case Codebase.PlayerFunc.LEFT_DEFENDER_POSITION: // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxForward * arenaScaleZ;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxForward * arenaScaleZ;
                case Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION: // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxForward * arenaScaleZ;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxForward * arenaScaleZ;
                case Codebase.PlayerFunc.GOALIE_POSITION: // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxForward * arenaScaleZ;
                default:
                    return 0 * arenaScaleZ;
            }
        }

        private float GetMaxBackwardDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot, float arenaScaleZ = 1f) {
            if (arenaScaleZ > 1f)
                arenaScaleZ = 1f;

            switch (positionName) {
                case Codebase.PlayerFunc.CENTER_POSITION: // Center
                    return Ruleset.ServerConfig.Faceoff.CenterMaxBackward * arenaScaleZ;
                case Codebase.PlayerFunc.LEFT_WINGER_POSITION: // Left Wing
                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION: // Right Wing
                    return Ruleset.ServerConfig.Faceoff.WingerMaxBackward * arenaScaleZ;
                case Codebase.PlayerFunc.LEFT_DEFENDER_POSITION: // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxBackward * arenaScaleZ;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxBackward * arenaScaleZ;
                case Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION: // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxBackward * arenaScaleZ;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxBackward * arenaScaleZ;
                case Codebase.PlayerFunc.GOALIE_POSITION: // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxBackward * arenaScaleZ;
                default:
                    return 2f * arenaScaleZ;
            }
        }

        private float GetMaxLeftDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot, float arenaScaleX = 1f) {
            if (arenaScaleX > 1f)
                arenaScaleX = 1f;

            switch (positionName) {
                case Codebase.PlayerFunc.CENTER_POSITION: // Center - limited side movement
                    return Ruleset.ServerConfig.Faceoff.CenterMaxLeft * arenaScaleX;
                case Codebase.PlayerFunc.GOALIE_POSITION: // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxLeft * arenaScaleX;
                case Codebase.PlayerFunc.LEFT_WINGER_POSITION: // Left winger can move left more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxAway * arenaScaleX;
                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION: // Right winger can't move much left (toward center)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxToward * arenaScaleX;
                case Codebase.PlayerFunc.LEFT_DEFENDER_POSITION: // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxAway * arenaScaleX;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxAway * arenaScaleX;
                case Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION: // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxToward * arenaScaleX;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxToward * arenaScaleX;
                default:
                    return 2f * arenaScaleX;
            }
        }

        private float GetMaxRightDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot, float arenaScaleX = 1f) {
            if (arenaScaleX > 1f)
                arenaScaleX = 1f;

            switch (positionName) {
                case Codebase.PlayerFunc.CENTER_POSITION: // Center - limited side movement
                    return Ruleset.ServerConfig.Faceoff.CenterMaxRight * arenaScaleX;
                case Codebase.PlayerFunc.GOALIE_POSITION: // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxRight * arenaScaleX;
                case Codebase.PlayerFunc.LEFT_WINGER_POSITION: // Left winger can't move much right (toward center)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxToward * arenaScaleX;
                case Codebase.PlayerFunc.RIGHT_WINGER_POSITION: // Right winger can move right more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxAway * arenaScaleX;
                case Codebase.PlayerFunc.LEFT_DEFENDER_POSITION: // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxToward * arenaScaleX;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxToward * arenaScaleX;
                case Codebase.PlayerFunc.RIGHT_DEFENDER_POSITION: // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueTeamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedTeamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxAway * arenaScaleX;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxAway * arenaScaleX;
                default:
                    return 2f * arenaScaleX;
            }
        }

        private void FreezeAllPlayersBeforeDrop() {
            foreach (PlayerTether tether in _playerTethers) {
                if (tether.PlayerBody == null || tether.PlayerBody.Rigidbody == null)
                    continue;
                if (Codebase.PlayerFunc.IsGoalie(tether.PlayerBody.Player) && !Ruleset.ServerConfig.Faceoff.FreezeGoaliesBeforeDrop)
                    continue;
                if (!Codebase.PlayerFunc.IsGoalie(tether.PlayerBody.Player) && !Ruleset.ServerConfig.Faceoff.FreezeSkatersBeforeDrop)
                    continue;

                // Freeze all movement
                tether.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;
                tether.PlayerBody.Rigidbody.angularVelocity = Vector3.zero;
                tether.PlayerBody.Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        private void FixedUpdate() {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
                return;

            if (!_isFaceOffActive || !Ruleset.Logic)
                return;

            // Continuously freeze players during faceoff if enabled (game will unfreeze when transitioning to Playing)
            if (_freezeStartTime != float.MinValue) {
                float timeInFaceoff = Time.time - _freezeStartTime;
                // Start freezing after specified time before drop
                if (timeInFaceoff >= Ruleset.ServerConfig.Faceoff.FreezeBeforeDropTime)
                    FreezeAllPlayersBeforeDrop();
            }

            // Enforce tethers and keep players unfrozen (except penalized ones)
            foreach (PlayerTether tether in _playerTethers) {
                if (tether.PlayerBody == null || tether.PlayerBody.Rigidbody == null)
                    continue;

                // Skip players who are serving a penalty
                if (PenalizedPlayers.Contains(tether.PlayerBody.Player))
                    continue;

                // Unfreeze if frozen
                if (tether.PlayerBody.Rigidbody.constraints == RigidbodyConstraints.FreezeAll)
                    tether.PlayerBody.Rigidbody.constraints = RigidbodyConstraints.None;

                // Enforce position tether
                EnforceTether(tether);
            }
        }

        private static void EnforceTether(PlayerTether tether) {
            Vector3 currentPos = tether.PlayerBody.transform.position;
            Vector3 spawnPos = tether.SpawnPosition;
            Vector3 clampedPos = currentPos;
            bool wasClamped = false;

            float forwardDirection = (tether.PlayerBody.Player.Team == PlayerTeam.Blue) ? -1f : 1f;

            // Check forward movement (toward opponent goal)
            float forwardDelta = (currentPos.z - spawnPos.z) * forwardDirection;
            if (forwardDelta > tether.MaxForwardDistance) {
                clampedPos.z = spawnPos.z + (tether.MaxForwardDistance * forwardDirection);
                wasClamped = true;
            }

            // Check backward movement (too far back)
            if (forwardDelta < -tether.MaxBackwardDistance) {
                clampedPos.z = spawnPos.z + (-tether.MaxBackwardDistance * forwardDirection);
                wasClamped = true;
            }

            float xMovement = spawnPos.x - currentPos.x;
            if (tether.PlayerBody.Player.Team == PlayerTeam.Blue) {
                if (xMovement > 0) { // Check right movement
                    if (Mathf.Abs(xMovement) > tether.MaxRightDistance) {
                        clampedPos.x = spawnPos.x - tether.MaxRightDistance;
                        wasClamped = true;
                    }
                }
                else { // Check left movement
                    if (Mathf.Abs(xMovement) > tether.MaxLeftDistance) {
                        clampedPos.x = spawnPos.x + tether.MaxLeftDistance;
                        wasClamped = true;
                    }
                }
            }
            else {
                if (xMovement > 0) { // Check left movement
                    if (Mathf.Abs(xMovement) > tether.MaxLeftDistance) {
                        clampedPos.x = spawnPos.x - tether.MaxLeftDistance;
                        wasClamped = true;
                    }
                }
                else { // Check right movement
                    if (Mathf.Abs(xMovement) > tether.MaxRightDistance) {
                        clampedPos.x = spawnPos.x + tether.MaxRightDistance;
                        wasClamped = true;
                    }
                }
            }

            if (wasClamped) {
                // Teleport player back to boundary
                tether.PlayerBody.transform.position = clampedPos;

                // Zero out velocity in clamped directions
                Vector3 velocity = tether.PlayerBody.Rigidbody.linearVelocity;
                if (Mathf.Abs(clampedPos.z - currentPos.z) > 0.01f)
                    velocity.z = 0f;
                if (Mathf.Abs(clampedPos.x - currentPos.x) > 0.01f)
                    velocity.x = 0f;
                tether.PlayerBody.Rigidbody.linearVelocity = velocity;
            }
        }
    }
}
