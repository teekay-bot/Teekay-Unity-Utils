using Object = UnityEngine.Object;

namespace TeekayUtils
{
    /// <summary>
    /// Liveness checks for references that may point at UnityEngine.Object instances through
    /// a non-Object static type (interfaces, <c>object</c>). An interface reference to a
    /// destroyed MonoBehaviour is NOT CLR-null — Unity's overloaded null check only kicks in
    /// through an Object-typed reference, which this helper provides. Complements
    /// <see cref="GameObjectExtensions.OrNull{T}"/>, whose <c>where T : Object</c> constraint
    /// rejects interface types.
    /// </summary>
    public static class UnityObjectExtensions
    {
        /// <summary>
        /// True when the reference is CLR-null, or a UnityEngine.Object that has been destroyed.
        /// Non-Unity objects are always considered alive.
        /// </summary>
        public static bool IsUnityNull(this object obj)
        {
            if (obj == null) return true;
            return obj is Object unityObj && unityObj == null;
        }
    }
}
