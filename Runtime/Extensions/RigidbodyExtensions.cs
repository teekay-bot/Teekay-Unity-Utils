using UnityEngine;

namespace TeekayUtils
{
    public static class RigidbodyExtensions
    {
        /// <summary>
        /// Redirects the linear velocity along the given direction, preserving speed.
        /// Zero directions are ignored.
        /// </summary>
        public static Rigidbody ChangeDirection(this Rigidbody rigidbody, Vector3 direction)
        {
            if (direction.sqrMagnitude == 0f) return rigidbody;

            direction.Normalize();
            rigidbody.linearVelocity = direction * rigidbody.linearVelocity.magnitude;
            return rigidbody;
        }

        /// <summary>Zeroes both linear and angular velocity.</summary>
        public static Rigidbody Stop(this Rigidbody rigidbody)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            return rigidbody;
        }
    }
}
