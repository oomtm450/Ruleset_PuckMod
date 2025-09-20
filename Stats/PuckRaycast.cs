using Codebase;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace oomtm450PuckMod_Stats {
    /// <summary>
    /// Class containing code for the puck raycasts with the goal trigger to know if the puck is going towards the net or not.
    /// </summary>
    internal class PuckRaycast : MonoBehaviour {
        private const int CHECK_EVERY_X_FRAMES = 6;
        //private readonly Vector3 TOP_VECTOR = new Vector3(0, 0.175f, 0);
        private readonly Vector3 ABOVE_GROUND_VECTOR = new Vector3(0, 0.001f, 0);
        private readonly float RIGHT_OFFSET = Codebase.Constants.PUCK_RADIUS + 0.02f;
        private readonly Vector3 BOTTOM_VECTOR = new Vector3(0, -0.6f, 0);
        private readonly float MAX_DISTANCE = 26f;
        private readonly LayerMask _goalTriggerlayerMask = GetLayerMask("Goal Trigger"); // 15

        private Ray _rayBottomLeft;
        //private LineRenderer _lineRendererBottomLeft;

        private Ray _rayBottomRight;
        //private LineRenderer _lineRendererBottomRight;

        private Ray _rayFarBottomLeft;
        //private LineRenderer _lineRendererFarBottomLeft;

        private Ray _rayFarBottomRight;
        //private LineRenderer _lineRendererFarBottomRight;

        //private Material _noHitMaterial = new Material(Shader.Find("Shader Graphs/Stick Shader"));
        //private Material _hitMaterial = new Material(Shader.Find("Shader Graphs/Stick Simple"));

        private Vector3 _startingPosition;

        private int _increment;

        internal LockDictionary<PlayerTeam, bool> PuckIsGoingToNet { get; set; } = new LockDictionary<PlayerTeam, bool> {
            { PlayerTeam.Blue, false },
            { PlayerTeam.Red, false },
        };

        internal void Start() {
            /*_lineRendererBottomLeft = CreateLineRenderer();
            _lineRendererBottomRight = CreateLineRenderer();
            _lineRendererFarBottomLeft = CreateLineRenderer();
            _lineRendererFarBottomRight = CreateLineRenderer();*/

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
            _startingPosition.y = transform.position.y; // Adjust Y of starting position so that the rays are all parallel to the ice.

            Vector3 direction = transform.position - _startingPosition;

            Vector3 leftVector = transform.position + Vector3.Cross(Vector3.down, direction.normalized).normalized * RIGHT_OFFSET;
            Vector3 rightVector = transform.position + Vector3.Cross(Vector3.up, direction.normalized).normalized * RIGHT_OFFSET;

            /*_lineRendererBottomLeft.SetPosition(0, _rayBottomLeft.origin);
            _lineRendererBottomLeft.SetPosition(1, _rayBottomLeft.origin + (_rayBottomLeft.direction * MAX_DISTANCE));
            _lineRendererBottomLeft.material = _noHitMaterial;

            _lineRendererBottomRight.SetPosition(0, _rayBottomRight.origin);
            _lineRendererBottomRight.SetPosition(1, _rayBottomRight.origin + (_rayBottomRight.direction * MAX_DISTANCE));
            _lineRendererBottomRight.material = _noHitMaterial;

            _lineRendererFarBottomLeft.SetPosition(0, _rayFarBottomLeft.origin);
            _lineRendererFarBottomLeft.SetPosition(1, _rayFarBottomLeft.origin + (_rayFarBottomLeft.direction * MAX_DISTANCE));
            _lineRendererFarBottomLeft.material = _noHitMaterial;

            _lineRendererFarBottomRight.SetPosition(0, _rayFarBottomRight.origin);
            _lineRendererFarBottomRight.SetPosition(1, _rayFarBottomRight.origin + (_rayFarBottomRight.direction * MAX_DISTANCE));
            _lineRendererFarBottomRight.material = _noHitMaterial;*/

            _rayBottomLeft = new Ray(leftVector + ABOVE_GROUND_VECTOR, direction);
            bool hasHit = Physics.Raycast(_rayBottomLeft, out RaycastHit hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
            if (!hasHit) {
                _rayBottomRight = new Ray(rightVector + ABOVE_GROUND_VECTOR, direction);
                hasHit = Physics.Raycast(_rayBottomRight, out hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                if (!hasHit) {
                    _rayFarBottomLeft = new Ray(leftVector + BOTTOM_VECTOR, direction);
                    hasHit = Physics.Raycast(_rayFarBottomLeft, out hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                    if (!hasHit) {
                        _rayFarBottomRight = new Ray(rightVector + BOTTOM_VECTOR, direction);
                        hasHit = Physics.Raycast(_rayFarBottomRight, out hit, MAX_DISTANCE, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
                        if (!hasHit)
                            return;
                        /*else {
                            _lineRendererFarBottomRight.material = _hitMaterial;
                            //Logging.Log("Far bottom right ray has hit !", Stats.ServerConfig, true);
                        }*/
                    }
                    /*else {
                        _lineRendererFarBottomLeft.material = _hitMaterial;
                        //Logging.Log("Far bottom left ray has hit !", Stats.ServerConfig, true);
                    }*/
                }
                /*else {
                    _lineRendererBottomRight.material = _hitMaterial;
                    //Logging.Log("Bottom right ray has hit !", Stats.ServerConfig, true);
                }*/
            }
            /*else {
                _lineRendererBottomLeft.material = _hitMaterial;
                //Logging.Log("Bottom left ray has hit !", Stats.ServerConfig, true);
            }*/

            Goal goal = SystemFunc.GetPrivateField<Goal>(typeof(GoalTrigger), hit.collider.gameObject.GetComponent<GoalTrigger>(), "goal");
            PlayerTeam team = SystemFunc.GetPrivateField<PlayerTeam>(typeof(Goal), goal, "Team");
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

        /*private LineRenderer CreateLineRenderer() {
            LineRenderer lineRenderer = new GameObject().gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 0.0275f;
            lineRenderer.endWidth = 0.0275f;
            lineRenderer.material = _noHitMaterial;
            return lineRenderer;
        }*/
    }
}
