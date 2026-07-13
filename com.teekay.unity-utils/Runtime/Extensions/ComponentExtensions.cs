using UnityEngine;

namespace TeekayUtils
{
    public static class ComponentExtensions
    {
        /// <summary>
        /// Gets the component of type T on the same GameObject, adding one if it
        /// does not exist. Convenience overload of GameObject.GetOrAdd for when
        /// you hold a Component reference (e.g. "this" in a MonoBehaviour).
        /// </summary>
        public static T GetOrAdd<T>(this Component component) where T : Component
        {
            return component.gameObject.GetOrAdd<T>();
        }
    }
}
