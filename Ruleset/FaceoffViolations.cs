using Codebase;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset.FaceoffViolation {
    /// <summary>
    /// Tracks player positions and enforces role-based tethers during faceoffs
    /// </summary>
    public class FaceOffPlayerUnfreezer : MonoBehaviour {
        private class PlayerTether {
            public PlayerBodyV2 PlayerBody;
            public Vector3 SpawnPosition;
            public PlayerRole Role;
            public float MaxForwardDistance;
            public float MaxBackwardDistance;
            public float MaxLeftDistance;
            public float MaxRightDistance;
        }

        private List<PlayerTether> _playerTethers = new List<PlayerTether>();
        private bool _isFaceOffActive = false;
        private Vector3 _currentFaceoffDot = Vector3.zero;
        private float _freezeStartTime = -1f;

        // Static list of players serving penalties (shared with FaceOffPuckValidator)
        public static HashSet<Player> PenalizedPlayers { get; set; } = new HashSet<Player>();

        private void Awake() {
            MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            if (MonoBehaviourSingleton<EventManager>.Instance != null) {
                MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
            }
        }

        private void OnGamePhaseChanged(Dictionary<string, object> message) {
            _isFaceOffActive = (GamePhase)message["newGamePhase"] == GamePhase.FaceOff;

            if (_isFaceOffActive) {
                // Detect faceoff location from puck position
                DetectFaceoffLocation();

                // Start countdown to freeze players before puck drop
                if (Ruleset.ServerConfig.Faceoff.FreezePlayersBeforeDrop)
                    _freezeStartTime = Time.time;
            }
            else {
                _playerTethers.Clear();
                _currentFaceoffDot = Vector3.zero;
                _freezeStartTime = -1f;
            }
        }

        private void DetectFaceoffLocation() {
            var pucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
            if (pucks.Length > 0) {
                _currentFaceoffDot = pucks[0].transform.position;
                Logging.Log($"Detected faceoff location at {_currentFaceoffDot}", Ruleset.ServerConfig);
            }
        }

        public void RegisterPlayer(PlayerBodyV2 playerBody) {
            if (playerBody == null || playerBody.Player == null) return;

            // Delay registration to allow Ruleset mod to position players first
            StartCoroutine(RegisterPlayerDelayed(playerBody));
        }

        private System.Collections.IEnumerator RegisterPlayerDelayed(PlayerBodyV2 playerBody) {
            // Wait for Ruleset mod to finish positioning players
            yield return new WaitForSeconds(0.1f);

            if (playerBody == null || playerBody.Player == null) yield break;

            // Remove if already registered
            _playerTethers.RemoveAll(t => t.PlayerBody == playerBody);

            // Get player role and position AFTER ruleset has positioned them
            PlayerRole role = playerBody.Player.Role.Value;
            string positionName = playerBody.Player.PlayerPosition?.Name ?? "Unknown";
            Vector3 spawnPos = playerBody.transform.position;

            // Create tether with role-specific restrictions
            PlayerTether tether = new PlayerTether {
                PlayerBody = playerBody,
                SpawnPosition = spawnPos,
                Role = role,
                MaxForwardDistance = GetMaxForwardDistance(positionName),
                MaxBackwardDistance = GetMaxBackwardDistance(positionName),
                MaxLeftDistance = GetMaxLeftDistance(positionName),
                MaxRightDistance = GetMaxRightDistance(positionName),
            };

            _playerTethers.Add(tether);
            Logging.Log($"Tethered {playerBody.Player.Username.Value} ({positionName}) at {spawnPos} - Forward:{tether.MaxForwardDistance}, Backward:{tether.MaxBackwardDistance}, Left:{tether.MaxLeftDistance}, Right:{tether.MaxRightDistance}", Ruleset.ServerConfig); // TODO
        }

        private float GetMaxForwardDistance(string positionName) {
            switch (positionName) {
                case "C": // Center - NO forward movement at all
                    return Ruleset.ServerConfig.Faceoff.CenterMaxForward;
                case "LW": // Left Wing
                case "RW": // Right Wing
                    return Ruleset.ServerConfig.Faceoff.WingerMaxForward;
                case "LD": // Left Defense
                case "RD": // Right Defense
                    return Ruleset.ServerConfig.Faceoff.DefenseMaxForward;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxForward;
                default:
                    return 0f;
            }
        }

        private float GetMaxBackwardDistance(string positionName) {
            switch (positionName) {
                case "C": // Center
                    return Ruleset.ServerConfig.Faceoff.CenterMaxBackward;
                case "LW": // Left Wing
                case "RW": // Right Wing
                    return Ruleset.ServerConfig.Faceoff.WingerMaxBackward;
                case "LD": // Left Defense
                case "RD": // Right Defense
                    return Ruleset.ServerConfig.Faceoff.DefenseMaxBackward;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxBackward;
                default:
                    return 2.0f;
            }
        }

        private float GetMaxLeftDistance(string positionName) {
            switch (positionName) {
                case "C": // Center - limited side movement
                    return Ruleset.ServerConfig.Faceoff.CenterMaxLeft;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxLeft;
                case "LW": // Left winger can move left more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxAway;
                case "RW": // Right winger can't move much left (toward center)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxToward;
                case "LD": // Left defense can move left more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.DefenseMaxAway;
                case "RD": // Right defense can't move much left (toward center)
                    return Ruleset.ServerConfig.Faceoff.DefenseMaxToward;
                default:
                    return 2.0f;
            }
        }

        private float GetMaxRightDistance(string positionName) {
            switch (positionName) {
                case "C": // Center - limited side movement
                    return Ruleset.ServerConfig.Faceoff.CenterMaxRight;
                case "G": // Goalie
                    return Ruleset.ServerConfig.Faceoff.GoalieMaxRight;
                case "LW": // Left winger can't move much right (toward center)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxToward;
                case "RW": // Right winger can move right more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.WingerMaxAway;
                case "LD": // Left defense can't move much right (toward center)
                    return Ruleset.ServerConfig.Faceoff.DefenseMaxToward;
                case "RD": // Right defense can move right more (away from center toward boards)
                    return Ruleset.ServerConfig.Faceoff.DefenseMaxAway;
                default:
                    return 2.0f;
            }
        }

        private void FreezeAllPlayersBeforeDrop() {
            foreach (var tether in _playerTethers) {
                if (tether.PlayerBody != null && tether.PlayerBody.Rigidbody != null) {
                    // Freeze all movement
                    tether.PlayerBody.Rigidbody.linearVelocity = Vector3.zero;
                    tether.PlayerBody.Rigidbody.angularVelocity = Vector3.zero;
                    tether.PlayerBody.Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                }
            }

            Logging.Log($"Froze {_playerTethers.Count} players before puck drop", Ruleset.ServerConfig); // TODO
        }

        public void ReloadConfig() {
            // Force re-registration of all players with new config values
            List<PlayerTether> currentTethers = new List<PlayerTether>(_playerTethers);
            _playerTethers.Clear();

            foreach (var tether in currentTethers) {
                if (tether.PlayerBody != null)
                    RegisterPlayer(tether.PlayerBody);
            }

            Logging.Log("Reloaded tether config for all players", Ruleset.ServerConfig); // TODO
        }

        private void FixedUpdate() {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            if (!_isFaceOffActive) return;

            // Continuously freeze players during faceoff if enabled (game will unfreeze when transitioning to Playing)
            if (Ruleset.ServerConfig.Faceoff.FreezePlayersBeforeDrop && _freezeStartTime > 0) {
                float timeInFaceoff = Time.time - _freezeStartTime;
                // Start freezing after specified time before drop
                if (timeInFaceoff >= Ruleset.ServerConfig.Faceoff.FreezeBeforeDropTime) {
                    FreezeAllPlayersBeforeDrop();
                }
            }

            // Enforce tethers and keep players unfrozen (except penalized ones)
            for (int i = _playerTethers.Count - 1; i >= 0; i--) {
                PlayerTether tether = _playerTethers[i];

                if (tether.PlayerBody == null || tether.PlayerBody.Rigidbody == null) {
                    _playerTethers.RemoveAt(i);
                    continue;
                }

                // Skip players who are serving a penalty
                if (PenalizedPlayers.Contains(tether.PlayerBody.Player)) {
                    continue;
                }

                // Unfreeze if frozen
                if (tether.PlayerBody.Rigidbody.constraints == RigidbodyConstraints.FreezeAll) {
                    tether.PlayerBody.Rigidbody.constraints = RigidbodyConstraints.None;
                }

                // Enforce position tether
                EnforceTether(tether);
            }
        }

        private void EnforceTether(PlayerTether tether) {
            Vector3 currentPos = tether.PlayerBody.transform.position;
            Vector3 spawnPos = tether.SpawnPosition;
            Vector3 clampedPos = currentPos;
            bool wasClamped = false;

            // Determine team direction (Blue team faces negative Z, Red faces positive Z)
            PlayerTeam team = tether.PlayerBody.Player.Team.Value;
            float forwardDirection = (team == PlayerTeam.Blue) ? -1f : 1f;

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

            // Check left movement
            float leftDelta = spawnPos.x - currentPos.x; // Positive = moved left
            if (leftDelta > tether.MaxLeftDistance) {
                clampedPos.x = spawnPos.x - tether.MaxLeftDistance;
                wasClamped = true;
            }

            // Check right movement
            float rightDelta = currentPos.x - spawnPos.x; // Positive = moved right
            if (rightDelta > tether.MaxRightDistance) {
                clampedPos.x = spawnPos.x + tether.MaxRightDistance;
                wasClamped = true;
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

                Logging.Log($"Tethered {tether.PlayerBody.Player.Username.Value} back from {currentPos} to {clampedPos}", Ruleset.ServerConfig); // TODO
            }
        }
    }

    /// <summary>
    /// Manages boundary restrictions during faceoffs
    /// </summary>
    public class FaceOffBoundaryManager : MonoBehaviour {
        private List<FaceOffBoundary> boundaries = new List<FaceOffBoundary>();
        private GameObject centerIceBoundary;

        private void Awake() {
            MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            if (MonoBehaviourSingleton<EventManager>.Instance != null) {
                MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
            }
        }

        private void OnGamePhaseChanged(Dictionary<string, object> message) {
            GamePhase newGamePhase = (GamePhase)message["newGamePhase"];

            if (newGamePhase == GamePhase.FaceOff) {
                // Detect if faceoff is at center ice
                var pucks = FindObjectsByType<Puck>(FindObjectsSortMode.None);
                if (pucks.Length > 0) {
                    Vector3 faceoffPos = pucks[0].transform.position;
                    bool isCenterIce = Mathf.Abs(faceoffPos.z) < 2f; // Within 2m of center

                    if (centerIceBoundary != null) {
                        centerIceBoundary.SetActive(isCenterIce);
                        Logging.Log($"Center ice boundary {(isCenterIce ? "enabled" : "disabled")} for faceoff at {faceoffPos}", Ruleset.ServerConfig); // TODO
                    }
                }
            }
        }

        private void Start() {
            // Create boundary zones
            CreateBoundaries();
        }

        private void CreateBoundaries() {
            // Center ice boundary (prevents crossing center line) - only for center ice faceoffs
            CreateCenterIceBoundary();

            // Faceoff circle boundaries (keeps sticks behind faceoff dots)
            CreateFaceOffCircleBoundaries();
        }

        private void CreateCenterIceBoundary() {
            centerIceBoundary = new GameObject("CenterIceBoundary");
            centerIceBoundary.transform.parent = transform;
            centerIceBoundary.transform.position = new Vector3(0, 1f, 0); // Center of rink, 1m up

            BoxCollider collider = centerIceBoundary.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(1f, 5f, 40f); // Wall across center ice

            FaceOffBoundary boundary = centerIceBoundary.AddComponent<FaceOffBoundary>();
            boundary.boundaryType = BoundaryType.CenterIce;
            boundaries.Add(boundary);

            centerIceBoundary.SetActive(false);

            Logging.Log("Created center ice boundary at position (0, 1, 0) with size (1, 5, 40)", Ruleset.ServerConfig); // TODO
        }

        private void CreateFaceOffCircleBoundaries() {
            // Create boundaries at typical faceoff dot locations
            Vector3[] faceoffPositions = new Vector3[]
            {
            new Vector3(0, 0, 0),      // Center ice
            new Vector3(7f, 0, 7f),    // Blue zone - right
            new Vector3(-7f, 0, 7f),   // Blue zone - left
            new Vector3(7f, 0, -7f),   // Red zone - right
            new Vector3(-7f, 0, -7f)   // Red zone - left
            };

            foreach (Vector3 position in faceoffPositions) {
                GameObject boundaryObj = new GameObject($"FaceOffCircleBoundary_{position}");
                boundaryObj.transform.parent = transform;
                boundaryObj.transform.position = position;

                // Create a cylinder around the faceoff dot
                CapsuleCollider collider = boundaryObj.AddComponent<CapsuleCollider>();
                collider.isTrigger = true;
                collider.radius = 2f; // Faceoff circle radius
                collider.height = 5f;
                collider.direction = 1; // Y-axis

                FaceOffBoundary boundary = boundaryObj.AddComponent<FaceOffBoundary>();
                boundary.boundaryType = BoundaryType.FaceOffCircle;
                boundary.faceoffPosition = position;
                boundaries.Add(boundary);

                boundaryObj.SetActive(false);
            }
        }

        public void ActivateBoundaries() {
            foreach (var boundary in boundaries) {
                if (boundary != null) {
                    boundary.gameObject.SetActive(true);
                }
            }
            Logging.Log("Boundaries activated", Ruleset.ServerConfig); // TODO
        }

        public void DeactivateBoundaries() {
            foreach (var boundary in boundaries) {
                if (boundary != null) {
                    boundary.gameObject.SetActive(false);
                }
            }
            Logging.Log("Boundaries deactivated", Ruleset.ServerConfig); // TODO
        }
    }

    public enum BoundaryType {
        CenterIce,
        FaceOffCircle
    }

    /// <summary>
    /// Individual boundary component that enforces movement restrictions
    /// </summary>
    public class FaceOffBoundary : MonoBehaviour {
        public BoundaryType boundaryType;
        public Vector3 faceoffPosition;

        private float pushBackForce = 250f; // Increased from 50 to 250
        private float velocityDampening = 0.7f;
        private Dictionary<Rigidbody, Vector3> playerStartingSides = new Dictionary<Rigidbody, Vector3>();

        private void OnTriggerEnter(Collider other) {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;

            // Store which side the player started on
            PlayerBodyV2 playerBody = other.GetComponentInParent<PlayerBodyV2>();
            if (playerBody != null && playerBody.Rigidbody != null) {
                if (!playerStartingSides.ContainsKey(playerBody.Rigidbody)) {
                    playerStartingSides[playerBody.Rigidbody] = playerBody.transform.position;
                    Logging.Log($"Player {playerBody.Player.Username.Value} registered on side: X={playerBody.transform.position.x}", Ruleset.ServerConfig); // TODO
                }
            }
        }

        private void OnTriggerStay(Collider other) {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;

            // Check if it's a player body
            PlayerBodyV2 playerBody = other.GetComponentInParent<PlayerBodyV2>();
            if (playerBody != null) {
                HandlePlayerBoundary(playerBody, other);
                return;
            }

            // Check if it's a stick
            Stick stick = other.GetComponentInParent<Stick>();
            if (stick != null && boundaryType == BoundaryType.FaceOffCircle) {
                HandleStickBoundary(stick, other);
                return;
            }
        }

        private void HandlePlayerBoundary(PlayerBodyV2 playerBody, Collider collider) {
            if (boundaryType == BoundaryType.CenterIce && playerBody.Rigidbody != null) {
                Vector3 currentPos = playerBody.transform.position;

                // Determine which side they should stay on
                float startingSide;
                if (playerStartingSides.ContainsKey(playerBody.Rigidbody))
                    startingSide = playerStartingSides[playerBody.Rigidbody].x;
                else {
                    startingSide = currentPos.x;
                    playerStartingSides[playerBody.Rigidbody] = currentPos;
                }

                // Strong push back to their side
                Vector3 pushDirection;

                if (startingSide > 0) // Started on positive X side
                    pushDirection = Vector3.right;
                else // Started on negative X side
                    pushDirection = Vector3.left;

                // Apply strong force
                playerBody.Rigidbody.AddForce(pushDirection * pushBackForce, ForceMode.Force);

                // Also dampen velocity crossing center
                Vector3 velocity = playerBody.Rigidbody.linearVelocity;
                if ((startingSide > 0 && velocity.x < 0) || (startingSide < 0 && velocity.x > 0)) {
                    velocity.x *= velocityDampening;
                    playerBody.Rigidbody.linearVelocity = velocity;
                }

                Logging.Log($"Pushing player back: direction={pushDirection}, force={pushBackForce}, startSide={startingSide}", Ruleset.ServerConfig); // TODO
            }
        }

        private void HandleStickBoundary(Stick stick, Collider collider) {
            if (boundaryType == BoundaryType.FaceOffCircle) {
                // Keep stick blade outside the faceoff circle
                Vector3 stickBladePos = stick.BladeHandlePosition;
                Vector3 directionFromCenter = (stickBladePos - faceoffPosition).normalized;

                float distance = Vector3.Distance(new Vector3(stickBladePos.x, 0, stickBladePos.z),
                                                 new Vector3(faceoffPosition.x, 0, faceoffPosition.z));

                // If stick is too close to or over the faceoff dot, push it back
                if (distance < 0.5f) // Within faceoff dot radius
                {
                    if (stick.Rigidbody != null) {
                        Vector3 pushDirection = new Vector3(directionFromCenter.x, 0, directionFromCenter.z).normalized;
                        stick.Rigidbody.AddForce(pushDirection * pushBackForce * 2f, ForceMode.Force);
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other) {
            // Don't remove from dictionary - we want to remember their starting side throughout the faceoff
        }

        private void OnDisable() {
            // Clear when boundaries deactivate
            playerStartingSides.Clear();
        }

        private void OnDrawGizmos() {
            // Visualize boundaries in editor
            Gizmos.color = boundaryType == BoundaryType.CenterIce ? Color.red : Color.yellow;
            Gizmos.DrawWireCube(transform.position, GetComponent<Collider>()?.bounds.size ?? Vector3.one);
        }
    }
}
