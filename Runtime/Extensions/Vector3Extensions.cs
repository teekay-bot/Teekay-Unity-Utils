using UnityEngine;

namespace TeekayUtils
{
    public static class Vector3Extensions
    {
        /// <summary>Returns a copy with any of x/y/z replaced.</summary>
        public static Vector3 With(this Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            return new Vector3(x ?? vector.x, y ?? vector.y, z ?? vector.z);
        }

        /// <summary>Returns a copy with the given amounts added per component.</summary>
        public static Vector3 Add(this Vector3 vector, float x = 0, float y = 0, float z = 0)
        {
            return new Vector3(vector.x + x, vector.y + y, vector.z + z);
        }

        /// <summary>True if <paramref name="current"/> is within <paramref name="range"/> of <paramref name="target"/> (sqrMagnitude compare, no sqrt).</summary>
        public static bool InRangeOf(this Vector3 current, Vector3 target, float range)
        {
            return (current - target).sqrMagnitude <= range * range;
        }

        /// <summary>Normalized direction from this point to the target.</summary>
        public static Vector3 DirectionTo(this Vector3 from, Vector3 to)
        {
            return (to - from).normalized;
        }

        /// <summary>Squared distance to the target — cheaper than Vector3.Distance for comparisons.</summary>
        public static float SqrDistanceTo(this Vector3 from, Vector3 to)
        {
            return (to - from).sqrMagnitude;
        }

        /// <summary>
        /// Divides component-wise. Components of <paramref name="v1"/> that are zero
        /// leave the corresponding component of <paramref name="v0"/> unchanged.
        /// </summary>
        public static Vector3 ComponentDivide(this Vector3 v0, Vector3 v1)
        {
            return new Vector3(
                v1.x != 0 ? v0.x / v1.x : v0.x,
                v1.y != 0 ? v0.y / v1.y : v0.y,
                v1.z != 0 ? v0.z / v1.z : v0.z);
        }

        /// <summary>Maps a Vector2 onto the XZ plane: (x, y) becomes (x, 0, y).</summary>
        public static Vector3 ToVector3XZ(this Vector2 v2)
        {
            return new Vector3(v2.x, 0, v2.y);
        }

        /// <summary>Returns a copy with a random offset in [-range, range] added to each component.</summary>
        public static Vector3 RandomOffset(this Vector3 vector, float range)
        {
            return vector + new Vector3(
                Random.Range(-range, range),
                Random.Range(-range, range),
                Random.Range(-range, range));
        }

        /// <summary>
        /// Random point in an annulus (ring) on the XZ plane around <paramref name="origin"/>,
        /// uniformly distributed by area.
        /// </summary>
        public static Vector3 RandomPointInAnnulus(this Vector3 origin, float minRadius, float maxRadius)
        {
            float angle = Random.value * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float minRadiusSquared = minRadius * minRadius;
            float maxRadiusSquared = maxRadius * maxRadius;
            float distance = Mathf.Sqrt(Random.value * (maxRadiusSquared - minRadiusSquared) + minRadiusSquared);

            var position = new Vector3(direction.x, 0, direction.y) * distance;
            return origin + position;
        }

        /// <summary>
        /// Rounds each component down to the nearest multiple of the corresponding
        /// quantization step — e.g. for snapping positions to a grid.
        /// </summary>
        public static Vector3 Quantize(this Vector3 position, Vector3 quantization)
        {
            return Vector3.Scale(
                quantization,
                new Vector3(
                    Mathf.Floor(position.x / quantization.x),
                    Mathf.Floor(position.y / quantization.y),
                    Mathf.Floor(position.z / quantization.z)));
        }
    }
}
