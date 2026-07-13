using UnityEngine;

namespace TeekayUtils
{
    public static class NumberExtensions
    {
        /// <summary>Fluent alias for Mathf.Approximately.</summary>
        public static bool Approx(this float f1, float f2) => Mathf.Approximately(f1, f2);

        /// <summary>True for odd integers, including negatives.</summary>
        public static bool IsOdd(this int i) => i % 2 != 0;

        /// <summary>True for even integers, including negatives.</summary>
        public static bool IsEven(this int i) => i % 2 == 0;

        /// <summary>Clamps the value to be no less than min.</summary>
        public static int AtLeast(this int value, int min) => Mathf.Max(value, min);

        /// <summary>Clamps the value to be no greater than max.</summary>
        public static int AtMost(this int value, int max) => Mathf.Min(value, max);

        /// <summary>Clamps the value to be no less than min.</summary>
        public static float AtLeast(this float value, float min) => Mathf.Max(value, min);

        /// <summary>Clamps the value to be no greater than max.</summary>
        public static float AtMost(this float value, float max) => Mathf.Min(value, max);

        /// <summary>
        /// Remaps the value from range [from1, to1] into range [from2, to2],
        /// clamped to the output range.
        /// </summary>
        public static float Remap(this float value, float from1, float to1, float from2, float to2)
        {
            return Mathf.Lerp(from2, to2, Mathf.InverseLerp(from1, to1, value));
        }
    }
}
