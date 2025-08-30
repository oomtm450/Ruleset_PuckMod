using Codebase;
using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing code for the puck raycasts with the goal trigger to know if the puck is going towards the net or not.
    /// </summary>
    internal class PuckRaycast : MonoBehaviour {
        private const int CHECK_EVERY_X_FRAMES = 4;
        //private readonly Vector3 TOP_VECTOR = new Vector3(0, 0.175f, 0);
        private readonly Vector3 RIGHT_VECTOR = new Vector3(Ruleset.PUCK_RADIUS + 0.018f, 0.001f, 0);
        private readonly Vector3 DOWN_VECTOR = new Vector3(0, -0.51f, 0);
        private Vector3 DOWN_RIGHT_VECTOR;
        private Vector3 DOWN_LEFT_VECTOR;
        private readonly float MAX_DISTANCE = 25f;
        private readonly LayerMask _goalTriggerlayerMask = GetLayerMask("Goal Trigger"); // 15

        private Ray _rayBottomLeft;
        private Ray _rayBottomRight;
        private Ray _rayFarBottomLeft;
        private Ray _rayFarBottomRight;

        private Vector3 _startingPosition;

        private int _increment;

        internal LockDictionary<PlayerTeam, bool> PuckIsGoingToNet { get; set; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal void Start() {
            DOWN_RIGHT_VECTOR = DOWN_VECTOR + RIGHT_VECTOR;
            DOWN_LEFT_VECTOR = DOWN_VECTOR - RIGHT_VECTOR;

            ResetStartingPosition();
            _increment = CHECK_EVERY_X_FRAMES - 1;
            Update();
        }

        /// <summary>
        /// Method that updates every frame to check for collision with the puck's raycasts and a goal trigger.
        /// </summary>
        internal void Update() {
            if (++_increment == CHECK_EVERY_X_FRAMES) {
                foreach (PlayerTeam key in new List<PlayerTeam>(PuckIsGoingToNet.Keys))
                    PuckIsGoingToNet[key] = false;

                _startingPosition.y = transform.position.y; // Adjust Y of starting position so that the rays are all parallel to the ice.

                _rayBottomLeft = new Ray(transform.position - RIGHT_VECTOR, transform.position - _startingPosition);
                _rayBottomRight = new Ray(transform.position + RIGHT_VECTOR, transform.position - _startingPosition);
                _rayFarBottomLeft = new Ray(transform.position + DOWN_LEFT_VECTOR, transform.position - _startingPosition);
                _rayFarBottomRight = new Ray(transform.position + DOWN_RIGHT_VECTOR, transform.position - _startingPosition);
                CheckForColliders();

                ResetStartingPosition();
            }
        }

        /// <summary>
        /// Method that resets the starting position of the puck.
        /// </summary>
        private void ResetStartingPosition() {
            _startingPosition = transform.position;
            _increment = 0;
        }

        private void CheckForColliders() {
            bool hasHit = Physics.Raycast(_rayBottomLeft, out RaycastHit hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
            if (!hasHit) {
                hasHit = Physics.Raycast(_rayBottomRight, out hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                if (!hasHit) {
                    hasHit = Physics.Raycast(_rayFarBottomLeft, out hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                    if (!hasHit) {
                        hasHit = Physics.Raycast(_rayFarBottomRight, out hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                        if (!hasHit)
                            return;
                        //else
                            //Logging.Log("Far bottom right ray has hit !", Ruleset._serverConfig, true);
                    }
                    //else
                        //Logging.Log("Far bottom left ray has hit !", Ruleset._serverConfig, true);
                }
                //else
                    //Logging.Log("Bottom right ray has hit !", Ruleset._serverConfig, true);
            }
            //else
                //Logging.Log("Bottom left ray has hit !", Ruleset._serverConfig, true);

            Goal goal = Ruleset.GetPrivateField<Goal>(typeof(GoalTrigger), hit.collider.gameObject.GetComponent<GoalTrigger>(), "goal");
            PlayerTeam team = Ruleset.GetPrivateField<PlayerTeam>(typeof(Goal), goal, "Team");
            PuckIsGoingToNet[team] = hasHit;
        }

        private static LayerMask GetLayerMask(string layerName) {
            LayerMask layerMask = 0;

            for (int i = 0; i < 32; i++) {
                string _layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(_layerName) && _layerName == layerName) {
                    layerMask |= 1 << i;
                    break;
                }
            }

            return layerMask;
        }
    }
}
