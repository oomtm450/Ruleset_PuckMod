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
        private static float _lastStickCollisionTime;
        private static bool _isMonitoring;

        internal static void NotifyCollision(Puck puck, Stick stick) {
            if (!_isMonitoring) return; // Only track when we're actively monitoring

            LastStickCollision = stick;
            _lastStickCollisionTime = Time.time;
            Logging.Log($"PuckCollisionTracker: {stick.Player.Username.Value} stick collided with puck at y={puck.transform.position.y}", Ruleset.ServerConfig); // TODO : Remove debug logs.
        }

        internal static void Reset(Puck puck) {
            _isMonitoring = true;
            LastStickCollision = null;
            _lastStickCollisionTime = 0f;
            Logging.Log($"PuckCollisionTracker reset and monitoring enabled at {puck.transform.position}", Ruleset.ServerConfig); // TODO : Remove debug logs.
        }

        internal static void StopMonitoring() {
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
            internal float LastViolationTime;
        }

        private bool _isFaceOffActive = false;
        private bool _puckDropped = false;
        private bool _puckTouchedIce = false;
        private bool _isMonitoring = false;
        private float _puckDropHeight = 999f; // Track puck height at start
        private Vector3 _faceoffDotPosition = Vector3.zero;
        private Dictionary<ulong, PlayerViolation> _playerViolations = new Dictionary<ulong, PlayerViolation>();
        private List<Player> _frozenPlayers = new List<Player>();
        private bool _isRestartingFaceoff = false; // Flag to prevent position update during our own restart
        private PlayerTeam _icingTeam = default; // Track which team iced the puck

        private void Awake() {
            MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            MonoBehaviourSingleton<EventManager>.Instance?.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnGamePhaseChanged(Dictionary<string, object> message) {
            GamePhase oldGamePhase = (GamePhase)message["oldGamePhase"];
            GamePhase newGamePhase = (GamePhase)message["newGamePhase"];

            Logging.Log($"PuckValidator phase change: {oldGamePhase} -> {newGamePhase}", Ruleset.ServerConfig); // TODO : Remove debug logs.

            if (newGamePhase == GamePhase.FaceOff) {
                // Clear restart flag when a new faceoff actually starts
                _isRestartingFaceoff = false;

                // Faceoff started - prepare for monitoring
                _isFaceOffActive = true;
                _puckDropped = false;
                _puckTouchedIce = false;
                _isMonitoring = false;
                _puckDropHeight = 999f;

                Logging.Log($"Faceoff started - lastFaceoffPosition is currently: '{Ruleset.NextFaceoffSpot}'", Ruleset.ServerConfig); // TODO : Remove debug logs.

                // Try to find faceoff dot position from puck and set up collision tracking
                var pucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
                if (pucks.Length != 0) {
                    Puck puck = pucks[0];
                    _faceoffDotPosition = puck.transform.position;
                    _puckDropHeight = puck.transform.position.y;

                    // Reset static collision tracker for this puck
                    FaceOffPuckCollisionTracker.Reset(puck);
                    Logging.Log("Collision tracker reset for puck", Ruleset.ServerConfig); // TODO : Remove debug logs.

                    Logging.Log($"PuckValidator ready! Faceoff at {_faceoffDotPosition}, puck height: {_puckDropHeight}", Ruleset.ServerConfig); // TODO : Remove debug logs.
                }

                // Start monitoring immediately during faceoff phase
                _isMonitoring = true;
            }
            else if (oldGamePhase == GamePhase.FaceOff) {
                // Faceoff ended - keep monitoring
                _isFaceOffActive = false;
                _puckDropped = true;
                _isMonitoring = true;
                Logging.Log("PuckValidator ACTIVE - Phase changed from FaceOff! Monitoring for violations...", Ruleset.ServerConfig); // TODO : Remove debug logs.
            }
        }

        private void Update() {
            // Remove server check - run on all instances for better detection
            if (!_isMonitoring)
                return;

            if (!Ruleset.ServerConfig.Faceoff.EnableViolations)
                return;

            // Monitor during faceoff and after drop
            if ((_puckDropped && !_puckTouchedIce) || _isFaceOffActive) {
                CheckPuckDrop();
                CheckStickContact(); // Check stick contact BEFORE ice contact
                CheckPuckIceContact(); // Ice contact checked last
            }
        }

        private void CheckPuckDrop() {
            if (!_isFaceOffActive)
                return;

            var pucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (pucks.Length == 0) return;

            Puck puck = pucks[0];

            // Detect when puck starts falling (drops below initial height)
            if (puck.transform.position.y < _puckDropHeight - 0.2f && !_puckDropped) {
                _puckDropped = true;
                _puckTouchedIce = false;
                Logging.Log($"PuckValidator: PUCK DROPPED! Height {puck.transform.position.y} (from {_puckDropHeight}) - NOW MONITORING FOR VIOLATIONS!", Ruleset.ServerConfig); // TODO : Remove debug logs.
            }
        }

        private void CheckPuckIceContact() {
            Puck[] pucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (pucks.Length == 0)
                return;

            Puck puck = pucks[0];

            // Check if puck has dropped below the allowed height threshold
            if (puck.transform.position.y < Ruleset.ServerConfig.Faceoff.PuckIceContactHeight && !_puckTouchedIce) {
                _puckTouchedIce = true;
                _isMonitoring = false; // Stop monitoring
                FaceOffPuckCollisionTracker.StopMonitoring();
                Logging.Log($"✓ Puck dropped below height threshold ({puck.transform.position.y} <= {Ruleset.ServerConfig.Faceoff.PuckIceContactHeight}) - Faceoff VALID! Stopping monitor.", Ruleset.ServerConfig); // TODO : Remove debug logs.
            }
        }

        private void CheckStickContact() {
            if (_puckTouchedIce) // Already valid, no need to check
                return;

            // Check if static collision tracker detected any stick contact
            if (FaceOffPuckCollisionTracker.LastStickCollision == null || FaceOffPuckCollisionTracker.LastStickCollision.Player == null)
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

            // Reset tracker
            Puck[] pucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (pucks.Length != 0)
                FaceOffPuckCollisionTracker.Reset(pucks[0]);
        }

        private void HandlePuckViolation(Player violatingPlayer) {
            if (violatingPlayer == null) return;

            ulong clientId = violatingPlayer.OwnerClientId;

            // Track violation
            if (!_playerViolations.ContainsKey(clientId)) {
                _playerViolations[clientId] = new PlayerViolation {
                    Player = violatingPlayer,
                    ViolationCount = 0,
                    LastViolationTime = Time.time,
                };
            }

            PlayerViolation violation = _playerViolations[clientId];

            // Check if this is a consecutive violation (within short time window)
            if (Time.time - violation.LastViolationTime > 10f) {
                // Reset count if it's been more than 10 seconds since last violation
                violation.ViolationCount = 0;
            }

            violation.ViolationCount++;
            violation.LastViolationTime = Time.time;

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
                Logging.Log($"Applying penalty to {player.Username.Value} - teleporting to back wall", Ruleset.ServerConfig); // TODO

                // Add to penalized players list (prevents unfreezing during faceoff)
                FaceOffPlayerUnfreezer.PenalizedPlayers.Add(player);

                // Calculate back wall position based on player position
                Vector3 currentPos = player.PlayerBody.transform.position;
                string positionName = player.PlayerPosition?.Name ?? "C";

                // Use config value for backward distance
                float backwardDistance = Ruleset.ServerConfig.Faceoff.PenaltyFreezeDistance;

                Logging.Log($"PENALTY: Current position: {currentPos}", Ruleset.ServerConfig);

                // Teleport player backward (toward their own goal)
                // Determine team direction by checking current Z position relative to center
                bool defendingPositiveZ = currentPos.z > 0;
                Vector3 penaltyPos = new Vector3(
                    currentPos.x,
                    currentPos.y,
                    currentPos.z + (defendingPositiveZ ? backwardDistance : -backwardDistance)
                );

                Logging.Log($"PENALTY: Teleporting to: {penaltyPos} (backward distance: {backwardDistance}, defending +Z: {defendingPositiveZ})", Ruleset.ServerConfig); // TODO

                // Use Server_Teleport for proper networked teleportation
                player.PlayerBody.Server_Teleport(penaltyPos, player.PlayerBody.transform.rotation);
                player.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;

                Logging.Log($"PENALTY: Player teleported, new position: {player.PlayerBody.transform.position}", Ruleset.ServerConfig); // TODO

                // Freeze them at the back wall position
                player.PlayerBody.Server_Freeze();
                _frozenPlayers.Add(player);

                Logging.Log($"Player {player.Username.Value} ({positionName}) frozen at back wall ({backwardDistance}m back) after {Ruleset.ServerConfig.Faceoff.MaxViolationsBeforePenalty} violations!", Ruleset.ServerConfig);

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
            _puckDropped = false;
            _puckTouchedIce = false;
            _isMonitoring = false;

            Logging.Log($"⚠ Restarting faceoff due to violation at last position: {Ruleset.NextFaceoffSpot}", Ruleset.ServerConfig); // TODO

            // Set flag to prevent position updates during our restart
            _isRestartingFaceoff = true;

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
                _isRestartingFaceoff = false;
            }
        }

        private System.Collections.IEnumerator ClearRestartFlagAfterDelay() {
            // Wait for restart sequence to complete
            yield return new WaitForSeconds(0.5f);
            // Flag will be cleared when FaceOff phase starts
            Logging.Log($"Restart delay complete, flag will clear when FaceOff phase begins", Ruleset.ServerConfig);
        }

        public void SetFaceoffPosition(Vector3 position) {
            _faceoffDotPosition = position;
        }

        public void ReloadConfig() {
            //Logging.Log("PuckValidator config reloaded");
        }
    }
}
