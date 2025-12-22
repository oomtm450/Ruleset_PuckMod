using Codebase;
using SingularityGroup.HotReload;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset.FaceoffViolation {
    /// <summary>
    /// Static tracker for stick-puck collisions.
    /// </summary>
    internal static class FaceOffPuckCollisionTracker {
        internal static Stick LastStickCollision { get; set; }
        private static bool _isMonitoring;

        internal static void NotifyCollision(Puck puck, Stick stick) {
            if (!_isMonitoring || !Ruleset.Logic)
                return;

            LastStickCollision = stick;
        }

        internal static void Reset() {
            _isMonitoring = true;
            LastStickCollision = null;
        }

        internal static void StopMonitoring() {
            Reset();
            _isMonitoring = false;
        }
    }

    /// <summary>
    /// Monitors puck drops during faceoffs and enforces rules.
    /// </summary>
    internal class FaceOffPuckValidator : MonoBehaviour {
        private class PlayerViolation {
            internal Player Player;
            internal int ViolationCount;
        }

        private bool _isFaceOffActive = false;
        private bool _puckTouchedIce = false;
        private bool _isMonitoring = false;
        private float _puckDropHeight = float.MaxValue; // Track puck height at start
        private readonly LockDictionary<ulong, PlayerViolation> _playerViolations = new LockDictionary<ulong, PlayerViolation>();
        private readonly LockList<Player> _frozenPlayers = new LockList<Player>();

        private void Awake() {
            EventManager.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            EventManager.Instance?.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnGamePhaseChanged(Dictionary<string, object> message) {
            GamePhase newGamePhase = (GamePhase)message["newGamePhase"];
            if (newGamePhase == GamePhase.FaceOff) {
                // Faceoff started - prepare for monitoring
                _isFaceOffActive = true;
                _puckTouchedIce = false;
                _puckDropHeight = float.MaxValue;
                _isMonitoring = true;

                FaceOffPuckCollisionTracker.Reset();
            }
            else if ((GamePhase)message["oldGamePhase"] == GamePhase.FaceOff) {
                // Faceoff ended - keep monitoring
                _isFaceOffActive = false;
            }
            else if (newGamePhase == GamePhase.Warmup || newGamePhase == GamePhase.BlueScore || newGamePhase == GamePhase.RedScore || newGamePhase == GamePhase.PeriodOver || newGamePhase == GamePhase.GameOver)
                ClearViolations();
        }

        internal void ClearViolations() {
            _playerViolations.Clear();
        }

        private void Update() {
            if (!_isMonitoring || !Ruleset.Logic)
                return;

            // Monitor during faceoff and after drop
            if (!_puckTouchedIce || _isFaceOffActive) {
                if (_puckDropHeight == float.MaxValue) {
                    Puck puck = PuckManager.Instance.GetPuck();
                    if (!puck)
                        return;
                    _puckDropHeight = puck.transform.position.y;
                }

                CheckStickContact(); // Check stick contact BEFORE ice contact
                CheckPuckIceContact(); // Ice contact checked last
            }
        }

        private void CheckPuckIceContact() {
            Puck puck = PuckManager.Instance.GetPuck();

            if (!puck)
                return;

            // Check if puck has dropped below the allowed height threshold
            if (puck.transform.position.y > Ruleset.ServerConfig.Faceoff.PuckIceContactHeight)
                return;

            _puckTouchedIce = true;
            _isMonitoring = false;
            FaceOffPuckCollisionTracker.StopMonitoring();
        }

        private void CheckStickContact() {
            // Check if static collision tracker detected any stick contact
            if (FaceOffPuckCollisionTracker.LastStickCollision == null || !FaceOffPuckCollisionTracker.LastStickCollision.Player)
                return;

            Stick stick = FaceOffPuckCollisionTracker.LastStickCollision;

            // Only penalize center players
            string positionName = stick.Player.PlayerPosition?.Name ?? "";
            if (positionName != "C") {
                Logging.Log($"Non-center player {stick.Player.Username.Value} ({positionName}) touched puck - ignoring", Ruleset.ServerConfig);
                return;
            }

            HandlePuckViolation(stick.Player);
            _isMonitoring = false; // Stop monitoring after violation
            FaceOffPuckCollisionTracker.StopMonitoring();
        }

        private void HandlePuckViolation(Player violatingPlayer) {
            if (!violatingPlayer)
                return;

            ulong clientId = violatingPlayer.OwnerClientId;

            // Track violation
            if (!_playerViolations.ContainsKey(clientId)) {
                _playerViolations[clientId] = new PlayerViolation {
                    Player = violatingPlayer,
                    ViolationCount = 0,
                };
            }

            PlayerViolation violation = _playerViolations[clientId];
            violation.ViolationCount++;

            Logging.Log($"VIOLATION! Player {violatingPlayer.Username.Value} #{violatingPlayer.Number.Value} touched puck before ice contact. Count: {violation.ViolationCount}", Ruleset.ServerConfig);

            // Always restart the faceoff first
            RestartFaceoff();

            // Check if player has hit the penalty threshold.
            if (violation.ViolationCount >= Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty) {
                // Send penalty chat message.
                Ruleset.SystemChatMessages.Add(
                    $"PENALTY: {violatingPlayer.Username.Value} has {Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty} faceoff violations! Will be frozen after spawn."
                );

                // Freeze player after they respawn at faceoff. (with delay)
                StartCoroutine(FreezePlayerAfterRespawn(violatingPlayer));
                violation.ViolationCount = 0; // Reset after punishment
            }
            else {
                // Just a violation - send chat message.
                Ruleset.SystemChatMessages.Add(
                    $"Faceoff Violation: {violatingPlayer.Username.Value} touched puck before ice! Restarting... ({violation.ViolationCount}/{Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty})"
                );
            }
        }

        private System.Collections.IEnumerator FreezePlayerAfterRespawn(Player player) {
            // Wait for faceoff restart to spawn player back at dot.
            yield return new WaitForSeconds(0.5f);

            if (!player)
                yield break;

            // Add to penalized players list. (prevents unfreezing during faceoff)
            FaceOffPlayerUnfreezer.PenalizedPlayers.Add(player);

            // Calculate back wall position based on player position.
            Vector3 currentPos = player.PlayerBody.transform.position;

            // Teleport player backward. (toward their own goal)
            Vector3 penaltyPos = new Vector3(
                currentPos.x,
                currentPos.y,
                currentPos.z + (player.Team.Value == PlayerTeam.Blue ? Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance : -Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance)
            );

            // Use Server_Teleport for proper networked teleportation.
            player.PlayerBody.Server_Teleport(penaltyPos, player.PlayerBody.transform.rotation);
            player.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;

            player.PlayerBody.Server_Freeze();
            _frozenPlayers.Add(player);

            Logging.Log($"Player {player.Username.Value} frozen at ({Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance}m back) after {Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty} violations!", Ruleset.ServerConfig);

            // Unfreeze after configured duration.
            StartCoroutine(UnfreezePlayerAfterDelay(player, Ruleset.ServerConfig.Faceoff.PenaltyFreezeDuration));
        }

        private System.Collections.IEnumerator UnfreezePlayerAfterDelay(Player player, float delay) {
            yield return new WaitForSeconds(delay);

            if (!player)
                yield break;

            // Remove from penalized list BEFORE unfreezing.
            FaceOffPlayerUnfreezer.PenalizedPlayers.Remove(player);

            player.PlayerBody.Server_Unfreeze();
            _frozenPlayers.Remove(player);
            Logging.Log($"Player {player.Username.Value} unfrozen after penalty", Ruleset.ServerConfig);
        }

        private void RestartFaceoff() {
            // Reset monitoring
            _puckTouchedIce = false;
            _isMonitoring = false;

            // Use Ruleset mod's instant faceoff event to restart at the same spot.
            if (EventManager.Instance == null || !NetworkManager.Singleton.IsServer)
                return;

            try {
                EventManager.Instance.TriggerEvent(Codebase.Constants.RULESET_MOD_NAME,
                    new Dictionary<string, object> { { Codebase.Constants.INSTANT_FACEOFF, ((ushort)Ruleset.NextFaceoffSpot).ToString() } });

                // Clear the flag after a short delay to allow the restart to complete.
                StartCoroutine(ClearRestartFlagAfterDelay());
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to restart faceoff.\n{ex}", Ruleset.ServerConfig);
            }
        }

        private System.Collections.IEnumerator ClearRestartFlagAfterDelay() {
            yield return new WaitForSeconds(0.5f);
        }
    }
}
