using UnityEngine;

namespace TeekayUtils
{
    public static class Rigidbody2DExtensions
    {
        /// <summary>
        /// Redirects the linear velocity along the given direction, preserving speed.
        /// Zero directions are ignored.
        /// </summary>
        public static Rigidbody2D ChangeDirection(this Rigidbody2D rigidbody, Vector2 direction)
        {
            if (direction.sqrMagnitude == 0f) return rigidbody;

            direction.Normalize();
            rigidbody.linearVelocity = direction * rigidbody.linearVelocity.magnitude;
            return rigidbody;
        }

        /// <summary>Zeroes both linear and angular velocity.</summary>
        public static Rigidbody2D Stop(this Rigidbody2D rigidbody)
        {
            rigidbody.linearVelocity = Vector2.zero;
            rigidbody.angularVelocity = 0f;
            return rigidbody;
        }
    }
}
