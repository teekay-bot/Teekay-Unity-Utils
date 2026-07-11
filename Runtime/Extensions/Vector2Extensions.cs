using UnityEngine;

namespace TeekayUtils
{
    public static class Vector2Extensions
    {
        /// <summary>Returns a copy with any of x/y replaced.</summary>
        public static Vector2 With(this Vector2 vector, float? x = null, float? y = null)
        {
            return new Vector2(x ?? vector.x, y ?? vector.y);
        }

        /// <summary>Returns a copy with the given amounts added per component.</summary>
        public static Vector2 Add(this Vector2 vector, float x = 0, float y = 0)
        {
            return new Vector2(vector.x + x, vector.y + y);
        }

        /// <summary>True if <paramref name="current"/> is within <paramref name="range"/> of <paramref name="target"/> (sqrMagnitude compare, no sqrt).</summary>
        public static bool InRangeOf(this Vector2 current, Vector2 target, float range)
        {
            return (current - target).sqrMagnitude <= range * range;
        }

        /// <summary>Normalized direction from this point to the target.</summary>
        public static Vector2 DirectionTo(this Vector2 from, Vector2 to)
        {
            return (to - from).normalized;
        }

        /// <summary>Squared distance to the target — cheaper than Vector2.Distance for comparisons.</summary>
        public static float SqrDistanceTo(this Vector2 from, Vector2 to)
        {
            return (to - from).sqrMagnitude;
        }

        /// <summary>Rotates the vector counter-clockwise by the given angle in degrees.</summary>
        public static Vector2 Rotate(this Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
        }

        /// <summary>
        /// Random point in an annulus (ring) around <paramref name="origin"/>,
        /// uniformly distributed by area.
        /// </summary>
        public static Vector2 RandomPointInAnnulus(this Vector2 origin, float minRadius, float maxRadius)
        {
            float angle = Random.value * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float minRadiusSquared = minRadius * minRadius;
            float maxRadiusSquared = maxRadius * maxRadius;
            float distance = Mathf.Sqrt(Random.value * (maxRadiusSquared - minRadiusSquared) + minRadiusSquared);

            return origin + direction * distance;
        }
    }
}
