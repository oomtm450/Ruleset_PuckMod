using UnityEngine;

namespace Codebase {
    internal static class ImpactUtil {
        /// <summary>
        /// Impulse-like hit strength. Uses Unity's impulse when it is available, otherwise
        /// estimates it from the pre-collision relative velocity and the reduced mass.
        /// </summary>
        internal static float GetImpact(Collision c) {
            float impulse = c.impulse.magnitude;
            if (impulse > 0.0001f)
                return impulse;

            ContactPoint cp = c.GetContact(0);
            float normalSpeed = Mathf.Abs(Vector3.Dot(c.relativeVelocity, cp.normal));

            Rigidbody a = cp.thisCollider.attachedRigidbody;
            Rigidbody b = cp.otherCollider.attachedRigidbody;
            bool aDyn = a && !a.isKinematic;
            bool bDyn = b && !b.isKinematic;

            float reducedMass;
            if (aDyn && bDyn) reducedMass = a.mass * b.mass / (a.mass + b.mass);
            else if (aDyn) reducedMass = a.mass;
            else if (bDyn) reducedMass = b.mass;
            else reducedMass = 1f;

            return reducedMass * normalSpeed;
        }
    }
}