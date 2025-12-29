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
            internal PlayerBodyV2 PlayerBody { get; set; }
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
            EventManager.Instance.AddEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnDestroy() {
            EventManager.Instance?.RemoveEventListener("Event_OnGamePhaseChanged", OnGamePhaseChanged);
        }

        private void OnGamePhaseChanged(Dictionary<string, object> message) {
            _isFaceOffActive = (GamePhase)message["newGamePhase"] == GamePhase.FaceOff;

            if (_isFaceOffActive) {
                // Start countdown to freeze players before puck drop
                if (Ruleset.ServerConfig.Faceoff.FreezePlayersBeforeDrop) {
                    _freezeStartTime = Time.time;

                    GamePhase oldGamePhase = (GamePhase)message["oldGamePhase"];
                    if (oldGamePhase == GamePhase.Replay || oldGamePhase == GamePhase.PeriodOver)
                        _freezeStartTime -= 1f;
                }
            }
            else {
                _playerTethers.Clear();
                _freezeStartTime = float.MinValue;
            }
        }

        internal void RegisterPlayer(PlayerBodyV2 playerBody, FaceoffSpot currentFaceoffSpot) {
            if (playerBody == null || !playerBody.Player)
                return;

            // Delay registration to allow Ruleset mod to position players first
            StartCoroutine(RegisterPlayerDelayed(playerBody, currentFaceoffSpot));
        }

        private System.Collections.IEnumerator RegisterPlayerDelayed(PlayerBodyV2 playerBody, FaceoffSpot currentFaceoffSpot) {
            // Wait for Ruleset mod to finish positioning players
            yield return new WaitForSeconds(0.1f);

            if (playerBody == null || playerBody.Player == null)
                yield break;

            // Remove if already registered
            _playerTethers.RemoveAll(t => t.PlayerBody == playerBody);

            // Get player role and position AFTER ruleset has positioned them
            string positionName = playerBody.Player.PlayerPosition?.Name ?? "Unknown";
            PlayerTeam team = playerBody.Player.Team.Value;

            if (positionName == "LD" && (team == PlayerTeam.Blue && currentFaceoffSpot == FaceoffSpot.BlueteamDZoneLeft || team == PlayerTeam.Red && currentFaceoffSpot == FaceoffSpot.RedteamDZoneRight))
                positionName = "RW";
            else if (positionName == "RD" && (team == PlayerTeam.Blue && currentFaceoffSpot == FaceoffSpot.BlueteamDZoneRight || team == PlayerTeam.Red && currentFaceoffSpot == FaceoffSpot.RedteamDZoneLeft))
                positionName = "LW";

            // Create tether with role-specific restrictions
            PlayerTether tether = new PlayerTether {
                PlayerBody = playerBody,
                SpawnPosition = playerBody.transform.position,
                MaxForwardDistance = GetMaxForwardDistance(positionName, playerBody.Player.Team.Value, currentFaceoffSpot),
                MaxBackwardDistance = GetMaxBackwardDistance(positionName, playerBody.Player.Team.Value, currentFaceoffSpot),
                MaxLeftDistance = GetMaxLeftDistance(positionName, playerBody.Player.Team.Value, currentFaceoffSpot),
                MaxRightDistance = GetMaxRightDistance(positionName, playerBody.Player.Team.Value, currentFaceoffSpot),
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

            float xMovement = spawnPos.x - currentPos.x;
            if (tether.PlayerBody.Player.Team.Value == PlayerTeam.Blue) {
                if (xMovement > 0) { // Check right movement
                    if (Mathf.Abs(xMovement) > tether.MaxRightDistance) {
                        clampedPos.x = spawnPos.x + tether.MaxRightDistance;
                        wasClamped = true;
                    }
                }
                else { // Check left movement
                    if (Mathf.Abs(xMovement) > tether.MaxLeftDistance) {
                        clampedPos.x = spawnPos.x - tether.MaxLeftDistance;
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

    /*/// <summary>
    /// Manages boundary restrictions during faceoffs
    /// </summary>
    public class FaceOffBoundaryManager : MonoBehaviour {
        private readonly LockList<FaceOffBoundary> _boundaries = new LockList<FaceOffBoundary>();

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
            GameObject centerIceBoundary = new GameObject("CenterIceBoundary");
            centerIceBoundary.transform.parent = transform;
            centerIceBoundary.transform.position = new Vector3(0, 1f, 0); // Center of rink, 1m up

            BoxCollider collider = centerIceBoundary.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(1f, 5f, 40f); // Wall across center ice

            FaceOffBoundary boundary = centerIceBoundary.AddComponent<FaceOffBoundary>();
            boundary.BoundaryType = BoundaryType.CenterIce;
            _boundaries.Add(boundary);

            centerIceBoundary.SetActive(false);
        }

        private void CreateFaceOffCircleBoundaries() {
            // Create boundaries at typical faceoff dot locations
            foreach (FaceoffSpot spot in new List<FaceoffSpot> {
                FaceoffSpot.Center,
                FaceoffSpot.BlueteamBLLeft,
                FaceoffSpot.BlueteamBLRight,
                FaceoffSpot.BlueteamDZoneLeft,
                FaceoffSpot.BlueteamDZoneRight,
                FaceoffSpot.RedteamBLLeft,
                FaceoffSpot.RedteamBLRight,
                FaceoffSpot.RedteamDZoneLeft,
                FaceoffSpot.RedteamDZoneRight,
            }) {
                Vector3 position = Faceoff.GetFaceoffDot(spot);
                position.y = 0;
                GameObject boundaryObj = new GameObject($"FaceOffCircleBoundary_{spot}");
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
        private readonly LockDictionary<ulong, Vector3> _playerStartingSides = new LockDictionary<ulong, Vector3>();

        private void OnTriggerEnter(Collider other) {
            // Store which side the player started on
            PlayerBodyV2 playerBody = other.GetComponentInParent<PlayerBodyV2>();
            if (playerBody == null || !Codebase.PlayerFunc.IsPlayerPlaying(playerBody.Player))
                return;

            if (!_playerStartingSides.ContainsKey(playerBody.Player.OwnerClientId))
                _playerStartingSides[playerBody.Player.OwnerClientId] = playerBody.transform.position;
        }

        private void OnTriggerStay(Collider other) {
            // Check if it's a player body
            PlayerBodyV2 playerBody = other.GetComponentInParent<PlayerBodyV2>();
            if (playerBody != null) {
                if (!Codebase.PlayerFunc.IsPlayerPlaying(playerBody.Player))
                    return;

                HandlePlayerBoundary(playerBody, other);
                return;
            }

            // Check if it's a stick
            if (BoundaryType == BoundaryType.FaceOffCircle) {
                Stick stick = other.GetComponentInParent<Stick>();
                if (stick != null) {
                    if (!Codebase.PlayerFunc.IsPlayerPlaying(stick.Player))
                        return;
                    HandleStickBoundary(stick, other);
                    return;
                }
            }
        }

        private void HandlePlayerBoundary(PlayerBodyV2 playerBody, Collider collider) {
            if (BoundaryType != BoundaryType.CenterIce)
                return;

            Vector3 currentPos = playerBody.transform.position;

            // Determine which side they should stay on
            float startingSide;
            if (_playerStartingSides.ContainsKey(playerBody.Player.OwnerClientId))
                startingSide = _playerStartingSides[playerBody.Player.OwnerClientId].x;
            else {
                startingSide = currentPos.x;
                _playerStartingSides.Add(playerBody.Player.OwnerClientId, currentPos);
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
    }*/
}
