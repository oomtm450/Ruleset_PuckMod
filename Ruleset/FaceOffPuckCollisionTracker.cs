using Codebase;
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
            if (!_isMonitoring) return; // Only track when we're actively monitoring

            LastStickCollision = stick;
            Logging.Log($"PuckCollisionTracker: {stick.Player.Username.Value} stick collided with puck at y={puck.transform.position.y}", Ruleset.ServerConfig); // TODO : Remove debug logs.
        }

        internal static void Reset() {
            _isMonitoring = true;
            LastStickCollision = null;
        }

        internal static void StopMonitoring() {
            Reset();
            _isMonitoring = false;
            Logging.Log($"PuckCollisionTracker monitoring stopped", Ruleset.ServerConfig); // TODO : Remove debug logs.
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
        private readonly Dictionary<ulong, PlayerViolation> _playerViolations = new Dictionary<ulong, PlayerViolation>();
        private readonly List<Player> _frozenPlayers = new List<Player>();

        private void Awake() {
            EventManager.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            EventManager.Instance?.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnGamePhaseChanged(Dictionary<string, object> message) {
            GamePhase oldGamePhase = (GamePhase)message["oldGamePhase"];
            GamePhase newGamePhase = (GamePhase)message["newGamePhase"];

            Logging.Log($"PuckValidator phase change: {oldGamePhase} -> {newGamePhase}", Ruleset.ServerConfig); // TODO : Remove debug logs.

            if (newGamePhase == GamePhase.FaceOff) {
                // Faceoff started - prepare for monitoring
                _isFaceOffActive = true;
                _puckTouchedIce = false;
                _puckDropHeight = float.MaxValue;
                _isMonitoring = true;

                FaceOffPuckCollisionTracker.Reset();

                Logging.Log($"Faceoff started - lastFaceoffPosition is currently: '{Ruleset.NextFaceoffSpot}'", Ruleset.ServerConfig); // TODO : Remove debug logs.
            }
            else if (oldGamePhase == GamePhase.FaceOff) {
                // Faceoff ended - keep monitoring
                _isFaceOffActive = false;
                Logging.Log($"PuckValidator ACTIVE - Phase changed from FaceOff! Monitoring for violations...", Ruleset.ServerConfig); // TODO : Remove debug logs.
            }
        }

        private void Update() {
            if (!_isMonitoring || !Ruleset.ServerConfig.Faceoff.EnableViolations)
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
            if (puck.transform.position.y < Ruleset.ServerConfig.Faceoff.PuckIceContactHeight) {
                _puckTouchedIce = true;
                _isMonitoring = false; // Stop monitoring
                FaceOffPuckCollisionTracker.StopMonitoring();
                Logging.Log($"✓ Puck dropped below height threshold ({puck.transform.position.y} < {Ruleset.ServerConfig.Faceoff.PuckIceContactHeight}) - Faceoff VALID! Stopping monitor.", Ruleset.ServerConfig); // TODO : Remove debug logs.
            }
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

            Logging.Log($"⚠ VIOLATION! Center {stick.Player.Username.Value} touched puck before ice contact!", Ruleset.ServerConfig);
            HandlePuckViolation(stick.Player);
            _isMonitoring = false; // Stop monitoring after violation
            FaceOffPuckCollisionTracker.StopMonitoring();
        }

        private void HandlePuckViolation(Player violatingPlayer) {
            if (violatingPlayer == null) return;

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

            Logging.Log($"VIOLATION! Player {violatingPlayer.Username.Value} touched puck before ice contact. Count: {violation.ViolationCount}", Ruleset.ServerConfig);

            // Always restart the faceoff first
            RestartFaceoff();

            // Check if player has hit the penalty threshold
            if (violation.ViolationCount >= Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty) {
                // Send penalty chat message
                NetworkBehaviourSingleton<UIChat>.Instance?.Server_SendSystemChatMessage(
                    $"⚠ PENALTY: {violatingPlayer.Username.Value} has {Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty} faceoff violations! Will be frozen after spawn."
                );

                // Freeze player after they respawn at faceoff (with delay)
                StartCoroutine(FreezePlayerAfterRespawn(violatingPlayer));
                violation.ViolationCount = 0; // Reset after punishment
            }
            else {
                // Just a violation - send chat message
                NetworkBehaviourSingleton<UIChat>.Instance?.Server_SendSystemChatMessage(
                    $"⚠ Faceoff Violation: {violatingPlayer.Username.Value} touched puck before ice! Restarting... ({violation.ViolationCount}/{Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty})"
                );
            }
        }

        private System.Collections.IEnumerator FreezePlayerAfterRespawn(Player player) {
            // Wait for faceoff restart to spawn player back at dot
            yield return new WaitForSeconds(0.5f);

            if (player != null && player.PlayerBody != null) {
                // Add to penalized players list (prevents unfreezing during faceoff)
                FaceOffPlayerUnfreezer.PenalizedPlayers.Add(player);

                // Calculate back wall position based on player position
                Vector3 currentPos = player.PlayerBody.transform.position;

                // Teleport player backward (toward their own goal)
                Vector3 penaltyPos = new Vector3(
                    currentPos.x,
                    currentPos.y,
                    currentPos.z + (player.Team.Value == PlayerTeam.Blue ? Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance : -Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance)
                );

                // Use Server_Teleport for proper networked teleportation
                player.PlayerBody.Server_Teleport(penaltyPos, player.PlayerBody.transform.rotation);
                player.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;

                // Freeze them at the back wall position
                player.PlayerBody.Server_Freeze();
                _frozenPlayers.Add(player);

                Logging.Log($"Player {player.Username.Value} frozen at back wall ({Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance}m back) after {Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty} violations!", Ruleset.ServerConfig);

                // Unfreeze after configured duration
                StartCoroutine(UnfreezePlayerAfterDelay(player, Ruleset.ServerConfig.Faceoff.PenaltyFreezeDuration));
            }
        }

        private System.Collections.IEnumerator UnfreezePlayerAfterDelay(Player player, float delay) {
            yield return new WaitForSeconds(delay);

            if (player != null && player.PlayerBody != null) {
                // Remove from penalized list BEFORE unfreezing
                FaceOffPlayerUnfreezer.PenalizedPlayers.Remove(player);

                player.PlayerBody.Server_Unfreeze();
                _frozenPlayers.Remove(player);
                Logging.Log($"Player {player.Username.Value} unfrozen after penalty", Ruleset.ServerConfig);
            }
        }

        private void RestartFaceoff() {
            // Reset monitoring
            _puckTouchedIce = false;
            _isMonitoring = false;

            Logging.Log($"⚠ Restarting faceoff due to violation at last position: {Ruleset.NextFaceoffSpot}", Ruleset.ServerConfig); // TODO

            // Use Ruleset mod's instant faceoff event to restart at the same spot
            if (MonoBehaviourSingleton<EventManager>.Instance == null || !NetworkManager.Singleton.IsServer)
                return;

            try {
                MonoBehaviourSingleton<EventManager>.Instance.TriggerEvent(Codebase.Constants.RULESET_MOD_NAME,
                    new Dictionary<string, object> { { Codebase.Constants.INSTANT_FACEOFF, ((ushort)Ruleset.NextFaceoffSpot).ToString() } });

                // Clear the flag after a short delay to allow the restart to complete
                StartCoroutine(ClearRestartFlagAfterDelay());
            }
            catch (Exception ex) {
                Logging.LogError($"Failed to restart faceoff.\n{ex}", Ruleset.ServerConfig);
            }
        }

        private System.Collections.IEnumerator ClearRestartFlagAfterDelay() {
            // Wait for restart sequence to complete
            yield return new WaitForSeconds(0.5f);
            // Flag will be cleared when FaceOff phase starts
            Logging.Log($"Restart delay complete, flag will clear when FaceOff phase begins", Ruleset.ServerConfig);
        }
    }
}
