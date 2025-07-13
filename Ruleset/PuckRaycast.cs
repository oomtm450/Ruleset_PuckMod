using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    /// <summary>
    /// Class containing code for the puck raycasts with the goal trigger to know if the puck is going towards the net or not.
    /// </summary>
    internal class PuckRaycast : MonoBehaviour {
        private const int CHECK_EVERY_X_FRAMES = 4;
        //private readonly Vector3 TOP_VECTOR = new Vector3(0, 0.15f, 0);
        private readonly Vector3 RIGHT_VECTOR = new Vector3(Ruleset.PUCK_RADIUS + 0.01f, 0, 0);
        private readonly LayerMask _goalTriggerlayerMask = GetLayerMask("Goal Trigger"); // 15

        private Ray _rayBottomLeft;
        private Ray _rayBottomRight;
        //private Ray _rayTopLeft;
        //private Ray _rayTopRight;
        
        private Vector3 _startingPosition;

        private int _increment;

        internal LockDictionary<PlayerTeam, bool> PuckIsGoingToNet { get; set; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal void Start() {
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

                //_rayTopLeft = new Ray(transform.position + TOP_VECTOR - RIGHT_VECTOR, transform.position - _startingPosition);
                //_rayTopRight = new Ray(transform.position + TOP_VECTOR + RIGHT_VECTOR, transform.position - _startingPosition);
                _rayBottomRight = new Ray(transform.position + RIGHT_VECTOR, transform.position - _startingPosition);
                _rayBottomLeft = new Ray(transform.position - RIGHT_VECTOR, transform.position - _startingPosition);
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
            bool hasHit = Physics.Raycast(_rayBottomLeft, out RaycastHit hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
            if (!hasHit) {
                hasHit = Physics.Raycast(_rayBottomRight, out hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                if (!hasHit) {
                    return;
                    /*hasHit = Physics.Raycast(_rayTopLeft, out hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                    if (!hasHit) {
                        hasHit = Physics.Raycast(_rayTopRight, out hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                        if (!hasHit)
                            return;
                        else
                            Logging.Log("Top right ray has hit !", Ruleset._serverConfig, true);
                    }
                    else
                        Logging.Log("Top left ray has hit !", Ruleset._serverConfig, true);*/
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
