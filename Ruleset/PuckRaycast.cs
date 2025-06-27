using UnityEngine;

namespace oomtm450PuckMod_Ruleset {
    internal class PuckRaycast : MonoBehaviour {
        private Ray _ray;
        private readonly LayerMask _goalTriggerlayerMask = GetLayerMask("Goal Trigger"); // 15
        private Vector3 _startingPosition;

        internal bool PuckIsGoingToNet = false;

        internal void Start() {
            _startingPosition = transform.position;
            Update();
        }

        internal void Update() {
            _ray = new Ray(transform.position, transform.position - _startingPosition);
            CheckForColliders();
            _startingPosition = transform.position;
        }

        private void CheckForColliders() {
            PuckIsGoingToNet = Physics.Raycast(_ray, out RaycastHit hit, float.PositiveInfinity, _goalTriggerlayerMask, QueryTriggerInteraction.Collide);
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
