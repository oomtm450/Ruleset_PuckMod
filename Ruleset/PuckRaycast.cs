using oomtm450PuckMod_Ruleset.SystemFunc;
using System.Collections.Generic;
using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal class PuckRaycast : MonoBehaviour {
        private Ray _rayDown;
        private Ray _rayUp;
        private Ray _rayLeft;
        private Ray _rayRight;
        private readonly LayerMask _goalTriggerlayerMask = GetLayerMask("Goal Trigger"); // 15
        private Vector3 _startingPosition;

        internal LockDictionary<PlayerTeam, bool> PuckIsGoingToNet { get; set; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal void Start() {
            ResetStartingPosition();
            Update();
        }

        internal void Update() {
            foreach (PlayerTeam key in new List<PlayerTeam>(PuckIsGoingToNet.Keys))
                PuckIsGoingToNet[key] = false;

            _rayDown = new Ray(transform.position, transform.position - _startingPosition); // TODO : Change rays to corners. (Bottom left, bottom right, etc.)
            _rayUp = new Ray(transform.position + new Vector3(0, 0.15f, 0), transform.position - _startingPosition);
            _rayRight = new Ray(transform.position + new Vector3(Ruleset.PUCK_RADIUS + 0.01f, 0, Ruleset.PUCK_RADIUS + 0.01f), transform.position - _startingPosition);
            _rayLeft = new Ray(transform.position - new Vector3(Ruleset.PUCK_RADIUS + 0.01f, 0, Ruleset.PUCK_RADIUS + 0.01f), transform.position - _startingPosition);
            CheckForColliders();

            ResetStartingPosition();
        }

        private void ResetStartingPosition() {
            _startingPosition = transform.position;
        }

        private void CheckForColliders() {
            bool hasHit = Physics.Raycast(_rayDown, out RaycastHit hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
            if (!hasHit) {
                hasHit = Physics.Raycast(_rayUp, out hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                if (!hasHit) {
                    hasHit = Physics.Raycast(_rayRight, out hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                    if (!hasHit) {
                        hasHit = Physics.Raycast(_rayLeft, out hit, 14f, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                        if (!hasHit)
                            return;
                        else
                            Logging.Log("Left ray has hit !", Ruleset._serverConfig, true);
                    }
                    else
                        Logging.Log("Right ray has hit !", Ruleset._serverConfig, true);
                }
                else
                    Logging.Log("Up ray has hit !", Ruleset._serverConfig, true);
            }
            else
                Logging.Log("Down ray has hit !", Ruleset._serverConfig, true);

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
