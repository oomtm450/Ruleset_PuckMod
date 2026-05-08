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
            EventManager.AddEventListener("Event_Everyone_OnGameStateChanged", Event_Everyone_OnGameStateChanged);
        }

        private void OnDestroy() {
            EventManager.RemoveEventListener("Event_Everyone_OnGameStateChanged", Event_Everyone_OnGameStateChanged);
        }

        private void Event_Everyone_OnGameStateChanged(Dictionary<string, object> message) {
            GameState oldGameState = (GameState)message["oldGameState"];
            GameState newGameState = (GameState)message["newGameState"];

            if (oldGameState.Phase == newGameState.Phase)
                return;

            _isFaceOffActive = newGameState.Phase == GamePhase.FaceOff;

            if (_isFaceOffActive) {
                // Start countdown to freeze players before puck drop
                if (Ruleset.ServerConfig.Faceoff.FreezePlayersBeforeDrop) {
                    _freezeStartTime = Time.time;

                    if (oldGameState.Phase == GamePhase.Replay || oldGameState.Phase == GamePhase.Intermission)
                        _freezeStartTime -= 1f;
                }
            }
            else {
                _playerTethers.Clear();
                _freezeStartTime = float.MinValue;
            }
        }

        internal void RegisterPlayer(PlayerBody playerBody, FaceoffSpot currentFaceoffSpot) {
            if (playerBody == null || !playerBody.Player)
                return;

            if (PenaltyModule.PenalizedPlayers.TryGetValue(playerBody.Player.SteamId.Value.ToString(), out LockList<Penalty> penalties) && penalties.Count != 0)
                return;

            // Delay registration to allow Ruleset mod to position players first
            StartCoroutine(RegisterPlayerDelayed(playerBody, currentFaceoffSpot));
        }

        private System.Collections.IEnumerator RegisterPlayerDelayed(PlayerBody playerBody, FaceoffSpot currentFaceoffSpot) {
            // Wait for Ruleset mod to finish positioning players
            yield return new WaitForSeconds(0.1f);

            if (playerBody == null || playerBody.Player == null)
                yield break;

            // Remove if already registered
            _playerTethers.RemoveAll(t => t.PlayerBody == playerBody);

            // Get player role and position AFTER ruleset has positioned them
            PlayerTeam team = playerBody.Player.Team;

            string positionName = PenaltyModule.GetPlayerPositionForFaceoff(playerBody.Player.PlayerPosition.Name, team, currentFaceoffSpot, Ruleset.GetClaimedPositions(team));

            if (positionName == "LD" && (team == PlayerTeam.Blue && currentFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft || team == PlayerTeam.Red && currentFaceoffSpot == FaceoffSpot.RedteamDZoneRight))
                positionName = "RW";
            else if (positionName == "RD" && (team == PlayerTeam.Blue && currentFaceoffSpot == FaceoffSpot.BlueteamDZoneRight || team == PlayerTeam.Red && currentFaceoffSpot == FaceoffSpot.RedteamDZoneLeft))
                positionName = "LW";

            // Create tether with role-specific restrictions
            PlayerTether tether = new PlayerTether {
                PlayerBody = playerBody,
                SpawnPosition = playerBody.transform.position,
                MaxForwardDistance = GetMaxForwardDistance(positionName, playerBody.Player.Team, currentFaceoffSpot),
                MaxBackwardDistance = GetMaxBackwardDistance(positionName, playerBody.Player.Team, currentFaceoffSpot),
                MaxLeftDistance = GetMaxLeftDistance(positionName, playerBody.Player.Team, currentFaceoffSpot),
                MaxRightDistance = GetMaxRightDistance(positionName, playerBody.Player.Team, currentFaceoffSpot),
            };

            if (positionName != "G" && currentFaceoffSpot == FaceoffSpot.Center) {
                tether.MaxForwardDistance *= 2;
                tether.MaxBackwardDistance *= 2;
                tether.MaxLeftDistance *= 2;
                tether.MaxRightDistance *= 2;
            }

            _playerTethers.Add(tether);
        }

        private float GetMaxForwardDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot) {
            switch (positionName) {
                case "C": // Center - NO forward movement at all
                    return Ruleset.ServerConfig.Faceoff.CenterMaxForward;
                case "LW": // Left Wing
                case "RW": // Right Wing
                    return Ruleset.ServerConfig.Faceoff.WingerMaxForward;
                case "LD": // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxForward;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxForward;
                case "RD": // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxForward;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxForward;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxForward;
                default:
                    return 0f;
                }
        }

        private float GetMaxBackwardDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot) {
            switch (positionName) {
                case "C": // Center
                    return Ruleset.ServerConfig.Faceoff.CenterMaxBackward;
                case "LW": // Left Wing
                case "RW": // Right Wing
                    return Ruleset.ServerConfig.Faceoff.WingerMaxBackward;
                case "LD": // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxBackward;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxBackward;
                case "RD": // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxBackward;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxBackward;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxBackward;
                default:
                    return 2f;
            }
        }

        private float GetMaxLeftDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot) {
            switch (positionName) {
                case "C": // Center - limited side movement
                    return Ruleset.ServerConfig.Faceoff.CenterMaxLeft;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxLeft;
                case "LW": // Left winger can move left more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxAway;
                case "RW": // Right winger can't move much left (toward center)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxToward;
                case "LD": // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxAway;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxAway;
                case "RD": // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxToward;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxToward;
                default:
                    return 2f;
            }
        }

        private float GetMaxRightDistance(string positionName, PlayerTeam team, FaceoffSpot currentFaceoffSpot) {
            switch (positionName) {
                case "C": // Center - limited side movement
                    return Ruleset.ServerConfig.Faceoff.CenterMaxRight;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxRight;
                case "LW": // Left winger can't move much right (toward center)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxToward;
                case "RW": // Right winger can move right more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxAway;
                case "LD": // Left Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneRight && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxToward;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxToward;
                case "RD": // Right Defense
                    if ((currentFaceoffSpot == FaceoffSpot.BlueteamDZoneRight && team == PlayerTeam.Blue) || (currentFaceoffSpot == FaceoffSpot.RedteamDZoneLeft && team == PlayerTeam.Red))
                        return Ruleset.ServerConfig.Faceoff.WingerMaxAway;
                    else
                        return Ruleset.ServerConfig.Faceoff.DefenseMaxAway;
                default:
                    return 2f;
            }
        }

        private void FreezeAllPlayersBeforeDrop() {
            foreach (PlayerTether tether in _playerTethers) {
                if (tether.PlayerBody == null || tether.PlayerBody.Rigidbody == null)
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
            if (_freezeStartTime > 0) {
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
