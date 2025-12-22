using Codebase;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset.FaceoffViolation {
    /// <summary>
    /// Tracks player positions and enforces role-based tethers during faceoffs
    /// </summary>
    internal class FaceOffPlayerUnfreezer : MonoBehaviour {
        private class PlayerTether {
            internal PlayerBodyV2 PlayerBody { get; set; }
            internal Vector3 SpawnPosition { get; set; }
            internal float MaxForwardDistance { get; set; }
            internal float MaxBackwardDistance { get; set; }
            internal float MaxLeftDistance { get; set; }
            internal float MaxRightDistance { get; set; }
        }

        private readonly LockList<PlayerTether> _playerTethers = new LockList<PlayerTether>();
        private bool _isFaceOffActive = false;
        private Vector3 _currentFaceoffDot = Vector3.zero;
        private float _freezeStartTime = -1f;

        internal static HashSet<Player> PenalizedPlayers { get; } = new HashSet<Player>();

        private void Awake() {
            EventManager.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            EventManager.Instance?.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
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
            Puck puck = PuckManager.Instance.GetPuck();
            if (!puck)
                return;

            _currentFaceoffDot = puck.transform.position;
        }

        internal void RegisterPlayer(PlayerBodyV2 playerBody) {
            if (playerBody == null || !playerBody.Player)
                return;

            // Delay registration to allow Ruleset mod to position players first
            StartCoroutine(RegisterPlayerDelayed(playerBody));
        }

        private System.Collections.IEnumerator RegisterPlayerDelayed(PlayerBodyV2 playerBody) {
            // Wait for Ruleset mod to finish positioning players
            yield return new WaitForSeconds(0.1f);

            if (playerBody == null || playerBody.Player == null)
                yield break;

            // Remove if already registered
            _playerTethers.RemoveAll(t => t.PlayerBody == playerBody);

            // Get player role and position AFTER ruleset has positioned them
            string positionName = playerBody.Player.PlayerPosition?.Name ?? "Unknown";
            Vector3 spawnPos = playerBody.transform.position;

            // Create tether with role-specific restrictions
            PlayerTether tether = new PlayerTether {
                PlayerBody = playerBody,
                SpawnPosition = spawnPos,
                MaxForwardDistance = GetMaxForwardDistance(positionName),
                MaxBackwardDistance = GetMaxBackwardDistance(positionName),
                MaxLeftDistance = GetMaxLeftDistance(positionName),
                MaxRightDistance = GetMaxRightDistance(positionName),
            };

            _playerTethers.Add(tether);
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

            if (!_isFaceOffActive)
                return;

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
            float forwardDirection = (tether.PlayerBody.Player.Team.Value == PlayerTeam.Blue) ? -1f : 1f;

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
            if (spawnPos.x - currentPos.x > tether.MaxLeftDistance) {
                clampedPos.x = spawnPos.x - tether.MaxLeftDistance;
                wasClamped = true;
            }

            // Check right movement
            if (currentPos.x - spawnPos.x > tether.MaxRightDistance) {
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
            }
        }
    }

    /// <summary>
    /// Manages boundary restrictions during faceoffs
    /// </summary>
    public class FaceOffBoundaryManager : MonoBehaviour {
        private Vector3[] FACEOFF_POSITIONS { get; } = new Vector3[] {
            new Vector3(0, 0, 0),      // Center ice
            new Vector3(7f, 0, 7f),    // Blue zone - right
            new Vector3(-7f, 0, 7f),   // Blue zone - left
            new Vector3(7f, 0, -7f),   // Red zone - right
            new Vector3(-7f, 0, -7f),   // Red zone - left
        };

        private readonly LockList<FaceOffBoundary> _boundaries = new LockList<FaceOffBoundary>();
        private GameObject _centerIceBoundary;

        private void Start() {
            CreateBoundaries();
        }

        private void CreateBoundaries() {
            // Center ice boundary (prevents crossing center line) - only for center ice faceoffs
            CreateCenterIceBoundary();

            // Faceoff circle boundaries (keeps sticks behind faceoff dots)
            CreateFaceOffCircleBoundaries();
        }

        private void CreateCenterIceBoundary() {
            _centerIceBoundary = new GameObject("CenterIceBoundary");
            _centerIceBoundary.transform.parent = transform;
            _centerIceBoundary.transform.position = new Vector3(0, 1f, 0); // Center of rink, 1m up

            BoxCollider collider = _centerIceBoundary.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(1f, 5f, 40f); // Wall across center ice

            FaceOffBoundary boundary = _centerIceBoundary.AddComponent<FaceOffBoundary>();
            boundary.BoundaryType = BoundaryType.CenterIce;
            _boundaries.Add(boundary);

            _centerIceBoundary.SetActive(false);
        }

        private void CreateFaceOffCircleBoundaries() {
            // Create boundaries at typical faceoff dot locations
            foreach (Vector3 position in FACEOFF_POSITIONS) {
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
                boundary.BoundaryType = BoundaryType.FaceOffCircle;
                boundary.FaceoffPosition = position;
                _boundaries.Add(boundary);

                boundaryObj.SetActive(false);
            }
        }

        public void ActivateBoundaries() {
            foreach (FaceOffBoundary boundary in _boundaries)
                boundary?.gameObject.SetActive(true);
        }

        public void DeactivateBoundaries() {
            foreach (FaceOffBoundary boundary in _boundaries)
                boundary?.gameObject.SetActive(false);
        }
    }

    public enum BoundaryType {
        CenterIce,
        FaceOffCircle,
    }

    /// <summary>
    /// Individual boundary component that enforces movement restrictions
    /// </summary>
    internal class FaceOffBoundary : MonoBehaviour {
        internal BoundaryType BoundaryType { get; set; }
        internal Vector3 FaceoffPosition { get; set; }

        private const float PUSH_BACK_FORCE = 250f;
        private const float VELOCITY_DAMPENING = 0.7f;
        private readonly LockDictionary<Rigidbody, Vector3> _playerStartingSides = new LockDictionary<Rigidbody, Vector3>();

        private void OnTriggerEnter(Collider other) {
            // Store which side the player started on
            PlayerBodyV2 playerBody = other.GetComponentInParent<PlayerBodyV2>();
            if (playerBody == null || playerBody.Rigidbody == null)
                return;

            if (!_playerStartingSides.ContainsKey(playerBody.Rigidbody))
                _playerStartingSides[playerBody.Rigidbody] = playerBody.transform.position;
        }

        private void OnTriggerStay(Collider other) {
            // Check if it's a player body
            PlayerBodyV2 playerBody = other.GetComponentInParent<PlayerBodyV2>();
            if (playerBody != null) {
                HandlePlayerBoundary(playerBody, other);
                return;
            }

            // Check if it's a stick
            if (BoundaryType == BoundaryType.FaceOffCircle) {
                Stick stick = other.GetComponentInParent<Stick>();
                if (stick != null) {
                    HandleStickBoundary(stick, other);
                    return;
                }
            }
        }

        private void HandlePlayerBoundary(PlayerBodyV2 playerBody, Collider collider) {
            if (playerBody.Rigidbody == null || BoundaryType != BoundaryType.CenterIce)
                return;

            Vector3 currentPos = playerBody.transform.position;

            // Determine which side they should stay on
            float startingSide;
            if (_playerStartingSides.ContainsKey(playerBody.Rigidbody))
                startingSide = _playerStartingSides[playerBody.Rigidbody].x;
            else {
                startingSide = currentPos.x;
                _playerStartingSides[playerBody.Rigidbody] = currentPos;
            }

            // Strong push back to their side
            Vector3 pushDirection;

            if (startingSide > 0) // Started on positive X side
                pushDirection = Vector3.right;
            else // Started on negative X side
                pushDirection = Vector3.left;

            // Apply strong force
            playerBody.Rigidbody.AddForce(pushDirection * PUSH_BACK_FORCE, ForceMode.Force);

            // Also dampen velocity crossing center
            Vector3 velocity = playerBody.Rigidbody.linearVelocity;
            if ((startingSide > 0 && velocity.x < 0) || (startingSide < 0 && velocity.x > 0)) {
                velocity.x *= VELOCITY_DAMPENING;
                playerBody.Rigidbody.linearVelocity = velocity;
            }
        }

        private void HandleStickBoundary(Stick stick, Collider collider) {
            if (stick.Rigidbody == null || BoundaryType != BoundaryType.FaceOffCircle)
                return;

            // Keep stick blade outside the faceoff circle.
            Vector3 stickBladePos = stick.BladeHandlePosition;
            Vector3 directionFromCenter = (stickBladePos - FaceoffPosition).normalized;

            if (Vector3.Distance(new Vector3(stickBladePos.x, 0, stickBladePos.z), new Vector3(FaceoffPosition.x, 0, FaceoffPosition.z)) > 0.5f)
                return;

            // If stick is too close to or over the faceoff dot, push it back.
            Vector3 pushDirection = new Vector3(directionFromCenter.x, 0, directionFromCenter.z).normalized;
            stick.Rigidbody.AddForce(pushDirection * PUSH_BACK_FORCE * 2f, ForceMode.Force);
        }

        private void OnDisable() {
            // Clear when boundaries deactivate
            _playerStartingSides.Clear();
        }
    }
}
